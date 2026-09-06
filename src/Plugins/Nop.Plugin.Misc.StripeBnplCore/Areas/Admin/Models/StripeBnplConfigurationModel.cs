using Nop.Web.Framework.Models;
using System.ComponentModel.DataAnnotations;

namespace Nop.Plugin.Misc.StripeBnplCore.Areas.Admin.Models;

public record StripeBnplConfigurationModel : BaseNopModel
{
    public int SelectedProviderId { get; set; } = 1;
    public bool UseSandbox { get; set; }
    public string SandboxDatabaseName { get; set; }
    public string SandboxDatabaseServer { get; set; }
    public string LiveDatabaseName { get; set; }
    public string LiveDatabaseServer { get; set; }
    public string StripeAccountCountryIso2 { get; set; }

    [DataType(DataType.Password)]
    public string TestRestrictedKey { get; set; }
    public bool TestRestrictedKeyConfigured { get; set; }

    [DataType(DataType.Password)]
    public string LiveRestrictedKey { get; set; }
    public bool LiveRestrictedKeyConfigured { get; set; }

    [DataType(DataType.Password)]
    public string TestWebhookSecret { get; set; }
    public bool TestWebhookSecretConfigured { get; set; }

    [DataType(DataType.Password)]
    public string LiveWebhookSecret { get; set; }
    public bool LiveWebhookSecretConfigured { get; set; }

    public string TestKlarnaConfigurationId { get; set; }
    public string LiveKlarnaConfigurationId { get; set; }
    public string TestAffirmConfigurationId { get; set; }
    public string LiveAffirmConfigurationId { get; set; }
    public string TestAfterpayConfigurationId { get; set; }
    public string LiveAfterpayConfigurationId { get; set; }
    public string TestZipConfigurationId { get; set; }
    public string LiveZipConfigurationId { get; set; }

    public string KlarnaApprovalReference { get; set; }
    public string AffirmApprovalReference { get; set; }
    public string AfterpayApprovalReference { get; set; }
    public string ZipApprovalReference { get; set; }
    public int AfterpayMaximumFulfillmentDays { get; set; }
    public int PendingOrderCancellationHours { get; set; }
    public int WebhookSignatureToleranceSeconds { get; set; }

    public decimal KlarnaMinimumAmount { get; set; }
    public decimal KlarnaMaximumAmount { get; set; }
    public decimal AffirmMinimumAmount { get; set; }
    public decimal AffirmMaximumAmount { get; set; }
    public decimal AfterpayMinimumAmount { get; set; }
    public decimal AfterpayMaximumAmount { get; set; }
    public decimal ZipMinimumAmount { get; set; }
    public decimal ZipMaximumAmount { get; set; }

    public string WebhookUrl { get; set; }
    public string EnvironmentStatus { get; set; }
    public bool EnvironmentIsSafe { get; set; }
}
