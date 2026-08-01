namespace Nop.Core.Domain.Catalog;

/// <summary>
/// Read-only projection of the legacy Etsy review import table.
/// It is used only to suppress imported marketplace rows from first-party
/// visible review summaries and structured data; no write or migration path is
/// defined here.
/// </summary>
public partial class EtsyReview : BaseEntity
{
    public string Sku { get; set; }

    public int Rating { get; set; }

    public string Review { get; set; }
}
