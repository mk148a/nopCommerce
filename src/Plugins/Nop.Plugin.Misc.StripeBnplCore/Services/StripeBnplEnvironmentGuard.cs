using System.Data.Common;
using Nop.Core;
using Nop.Data;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public sealed class StripeBnplEnvironmentGuard : IStripeBnplEnvironmentGuard
{
    private readonly StripeBnplSettings _settings;

    public StripeBnplEnvironmentGuard(StripeBnplSettings settings)
    {
        _settings = settings;
    }

    public BnplEnvironmentGuardResult Check()
    {
        var connectionString = DataSettingsManager.LoadSettings()?.ConnectionString;
        var databaseName = GetConnectionValue(connectionString, "Initial Catalog", "Database");
        var databaseServer = GetConnectionValue(connectionString, "Data Source", "Server", "Address", "Addr", "Network Address");
        var key = _settings.GetActiveRestrictedKey()?.Trim();
        var requiredPrefix = _settings.UseSandbox ? "rk_test_" : "rk_live_";
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith(requiredPrefix, StringComparison.Ordinal))
            return new(false, databaseName,
                $"Stripe key must be a restricted key with prefix '{requiredPrefix}' for the selected mode.");

        if (string.Equals(_settings.TestRestrictedKey?.Trim(), _settings.LiveRestrictedKey?.Trim(),
                StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(_settings.TestRestrictedKey))
            return new(false, databaseName, "Test and live restricted keys must be different.");
        if (string.Equals(_settings.TestWebhookSecret?.Trim(), _settings.LiveWebhookSecret?.Trim(),
                StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(_settings.TestWebhookSecret))
            return new(false, databaseName, "Test and live webhook secrets must be different.");

        var expectedDatabase = _settings.UseSandbox ? _settings.SandboxDatabaseName : _settings.LiveDatabaseName;
        var expectedServer = _settings.UseSandbox ? _settings.SandboxDatabaseServer : _settings.LiveDatabaseServer;
        var mode = _settings.UseSandbox ? "Sandbox" : "Live";
        if (string.IsNullOrWhiteSpace(expectedDatabase) || string.IsNullOrWhiteSpace(expectedServer))
            return new(false, databaseName, $"{mode} database and server allowlists must both be configured.");

        // A copied production database contains all plugin settings. Even if an
        // administrator accidentally edits the sandbox allowlist to match that
        // database, test API calls must still fail closed. Likewise, live mode
        // may never target the configured sandbox identity.
        var oppositeDatabase = _settings.UseSandbox ? _settings.LiveDatabaseName : _settings.SandboxDatabaseName;
        var oppositeServer = _settings.UseSandbox ? _settings.LiveDatabaseServer : _settings.SandboxDatabaseServer;
        if (!string.IsNullOrWhiteSpace(oppositeDatabase) &&
            string.Equals(databaseName, oppositeDatabase.Trim(), StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(oppositeServer) ||
             string.Equals(NormalizeServer(databaseServer), NormalizeServer(oppositeServer),
                 StringComparison.OrdinalIgnoreCase)))
            return new(false, databaseName,
                $"{mode} mode refuses the database identity reserved for the opposite Stripe environment.");

        if (!string.IsNullOrWhiteSpace(_settings.SandboxDatabaseName) &&
            !string.IsNullOrWhiteSpace(_settings.LiveDatabaseName) &&
            string.Equals(_settings.SandboxDatabaseName.Trim(), _settings.LiveDatabaseName.Trim(),
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(NormalizeServer(_settings.SandboxDatabaseServer), NormalizeServer(_settings.LiveDatabaseServer),
                StringComparison.OrdinalIgnoreCase))
            return new(false, databaseName,
                "Sandbox and live database allowlists must identify different databases.");

        if (!string.Equals(databaseName, expectedDatabase.Trim(), StringComparison.OrdinalIgnoreCase))
            return new(false, databaseName,
                $"{mode} mode is restricted to database '{expectedDatabase}'.");
        if (!string.Equals(NormalizeServer(databaseServer), NormalizeServer(expectedServer),
                StringComparison.OrdinalIgnoreCase))
            return new(false, databaseName, $"{mode} mode is restricted to the configured database server.");

        return new(true, databaseName, $"{mode} database identity verified.");
    }

    public void EnsureSafe()
    {
        var result = Check();
        if (!result.IsSafe)
            throw new NopException(result.Reason);
    }

    private static string GetConnectionValue(string connectionString, params string[] keys)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        try
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            foreach (var key in keys)
            {
                if (builder.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value?.ToString()))
                    return value.ToString().Trim();
            }
        }
        catch (ArgumentException)
        {
            return null;
        }

        return null;
    }

    private static string NormalizeServer(string value) =>
        value?.Trim().TrimStart('.').Replace("tcp:", string.Empty, StringComparison.OrdinalIgnoreCase);
}
