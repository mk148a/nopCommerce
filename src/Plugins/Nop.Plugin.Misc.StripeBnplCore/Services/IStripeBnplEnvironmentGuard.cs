namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public interface IStripeBnplEnvironmentGuard
{
    BnplEnvironmentGuardResult Check();
    void EnsureSafe();
}

public sealed record BnplEnvironmentGuardResult(bool IsSafe, string DatabaseName, string Reason);
