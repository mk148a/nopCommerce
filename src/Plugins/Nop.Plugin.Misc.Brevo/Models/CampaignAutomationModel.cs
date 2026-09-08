using System.ComponentModel.DataAnnotations;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.Brevo.Models;

/// <summary>
/// Represents the Brevo seasonal campaign automation configuration.
/// </summary>
public record CampaignAutomationModel : BaseNopModel
{
    [NopResourceDisplayName("Plugins.Misc.Brevo.CampaignAutomation.Enabled")]
    public bool Enabled { get; set; }

    [NopResourceDisplayName("Plugins.Misc.Brevo.CampaignAutomation.CreateBrevoDraft")]
    public bool CreateBrevoDraft { get; set; }

    [NopResourceDisplayName("Plugins.Misc.Brevo.CampaignAutomation.ScheduleBrevoEmail")]
    public bool ScheduleBrevoEmail { get; set; }

    [Range(typeof(decimal), "0.01", "15")]
    [NopResourceDisplayName("Plugins.Misc.Brevo.CampaignAutomation.DiscountPercentage")]
    public decimal DiscountPercentage { get; set; }

    [Range(1, 720)]
    [NopResourceDisplayName("Plugins.Misc.Brevo.CampaignAutomation.CouponDurationHours")]
    public int CouponDurationHours { get; set; }

    [Range(0, 90)]
    [NopResourceDisplayName("Plugins.Misc.Brevo.CampaignAutomation.LeadTimeDays")]
    public int LeadTimeDays { get; set; }

    public DateTime? CurrentOccasionStartOverrideUtc { get; set; }

    [Required, StringLength(12)]
    [NopResourceDisplayName("Plugins.Misc.Brevo.CampaignAutomation.CouponPrefix")]
    public string CouponPrefix { get; set; }

    [NopResourceDisplayName("Plugins.Misc.Brevo.CampaignAutomation.BrevoSegmentId")]
    public long BrevoSegmentId { get; set; }

    [NopResourceDisplayName("Plugins.Misc.Brevo.CampaignAutomation.BrevoListId")]
    public long BrevoListId { get; set; }

    [NopResourceDisplayName("Plugins.Misc.Brevo.CampaignAutomation.BrevoTemplateId")]
    public long BrevoTemplateId { get; set; }

    [NopResourceDisplayName("Plugins.Misc.Brevo.CampaignAutomation.BrevoSenderId")]
    public long BrevoSenderId { get; set; }

    public string NextOccasion { get; set; }
}
