using Nop.Core.Configuration;

namespace Nop.Plugin.Misc.Brevo;

/// <summary>
/// Settings for opt-in seasonal coupon and Brevo campaign preparation.
/// </summary>
public class CampaignAutomationSettings : ISettings
{
    /// <summary>
    /// Gets or sets whether the daily planner can prepare future campaigns.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets whether a Brevo draft is created with each prepared coupon.
    /// A draft is never sent by this option.
    /// </summary>
    public bool CreateBrevoDraft { get; set; }

    /// <summary>
    /// Gets or sets whether a configured Brevo campaign may be scheduled automatically.
    /// Defaults to false so a store administrator remains in control of sends.
    /// </summary>
    public bool ScheduleBrevoEmail { get; set; }

    /// <summary>
    /// Gets or sets the standard percentage applied to seasonal coupons.
    /// The service enforces the 15 percent ceiling independently of this value.
    /// </summary>
    public decimal DiscountPercentage { get; set; } = 7m;

    /// <summary>
    /// Gets or sets the coupon validity period in hours.
    /// </summary>
    public int CouponDurationHours { get; set; } = 72;

    /// <summary>
    /// Gets or sets the number of days before an occasion in which the planner prepares it.
    /// </summary>
    public int LeadTimeDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets the one-off launch override for the currently selected seasonal occasion.
    /// The key prevents an old override from affecting later calendar entries.
    /// </summary>
    public string CampaignStartOverrideOccasionKey { get; set; }

    /// <summary>
    /// Gets or sets the one-off UTC campaign launch override.
    /// </summary>
    public DateTime? CampaignStartOverrideUtc { get; set; }

    /// <summary>
    /// Gets or sets the prefix for generated coupon codes.
    /// </summary>
    public string CouponPrefix { get; set; } = "HOOD";

    /// <summary>
    /// Gets or sets the Brevo segment that has marketing consent for seasonal campaigns.
    /// </summary>
    public long BrevoSegmentId { get; set; }

    /// <summary>
    /// Gets or sets the Brevo list that has marketing consent for seasonal campaigns.
    /// A list may be used instead of a segment.
    /// </summary>
    public long BrevoListId { get; set; }

    /// <summary>
    /// Gets or sets the automatically managed empty list required by Brevo as the explicit campaign
    /// exclusion list. It never contains marketing contacts.
    /// </summary>
    public long BrevoExclusionListId { get; set; }

    /// <summary>
    /// Gets or sets the approved Brevo email template used for prepared campaigns.
    /// </summary>
    public long BrevoTemplateId { get; set; }

    /// <summary>
    /// Gets or sets the Brevo sender identifier selected in the plugin configuration.
    /// </summary>
    public long BrevoSenderId { get; set; }
}
