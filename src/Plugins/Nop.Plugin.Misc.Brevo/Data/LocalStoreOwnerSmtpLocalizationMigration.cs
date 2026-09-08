using FluentMigrator;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Data.Migrations;
using Nop.Services.Localization;
using Nop.Web.Framework.Extensions;

namespace Nop.Plugin.Misc.Brevo.Data;

/// <summary>
/// Adds local SMTP relay labels for stores that already had the Brevo plugin installed.
/// </summary>
[NopMigration("2026-09-08 10:00:00", "Misc.Brevo add local store-owner SMTP localizations", MigrationProcessType.Update)]
public class LocalStoreOwnerSmtpLocalizationMigration : MigrationBase
{
    /// <inheritdoc />
    public override void Up()
    {
        if (!DataSettingsManager.IsDatabaseInstalled())
            return;

        var localizationService = EngineContext.Current.Resolve<ILocalizationService>();
        var (languageId, _) = this.GetLanguageData();

        localizationService.AddOrUpdateLocaleResource(new Dictionary<string, string>
        {
            ["Plugins.Misc.Brevo.LocalStoreOwnerSmtp.Title"] = "Store-owner notification relay",
            ["Plugins.Misc.Brevo.LocalStoreOwnerSmtp.Description"] = "Order and other store-owner notifications can use local hMailServer. Customer emails remain on Brevo.",
            ["Plugins.Misc.Brevo.Fields.UseLocalStoreOwnerSmtp"] = "Use local SMTP for store-owner notifications",
            ["Plugins.Misc.Brevo.Fields.LocalStoreOwnerSmtpHost"] = "Local SMTP host",
            ["Plugins.Misc.Brevo.Fields.LocalStoreOwnerSmtpPort"] = "Local SMTP port",
            ["Plugins.Misc.Brevo.Fields.LocalStoreOwnerSmtpUseSsl"] = "Use SSL for local SMTP",
            ["Plugins.Misc.Brevo.Fields.LocalStoreOwnerSmtpUsername"] = "Local SMTP user name",
            ["Plugins.Misc.Brevo.Fields.LocalStoreOwnerSmtpPassword"] = "Local SMTP password",
            ["Plugins.Misc.Brevo.LocalStoreOwnerSmtp.TestButton"] = "Test local SMTP relay",
            ["Plugins.Misc.Brevo.LocalStoreOwnerSmtp.TestSuccess"] = "Local SMTP relay test message was accepted.",
            ["Plugins.Misc.Brevo.LocalStoreOwnerSmtp.TestOnlyLoopback"] = "The local SMTP test only supports 127.0.0.1 and a valid port.",
            ["Plugins.Misc.Brevo.LocalStoreOwnerSmtp.TestCredentialsRequired"] = "Enter both the local SMTP user name and password, or leave both empty."
        }, languageId);
    }

    /// <inheritdoc />
    public override void Down()
    {
        // Resources are intentionally retained on downgrade.
    }
}
