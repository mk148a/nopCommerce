namespace Nop.Plugin.Misc.StripeBnplCore.Domain;

public enum BnplEligibilityState
{
    Unknown = 0,
    Allowed = 10,
    ApprovalRequired = 20,
    Prohibited = 30
}
