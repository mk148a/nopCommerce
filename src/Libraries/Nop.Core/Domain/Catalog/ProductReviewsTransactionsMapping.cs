namespace Nop.Core.Domain.Catalog;

/// <summary>
/// Links a migrated product review to its external marketplace transaction.
///
/// The table is created by the legacy review-import integration and is therefore
/// intentionally modelled as read-only provenance metadata here.  No migration
/// or delete/update path is added by nopCommerce.
/// </summary>
public partial class ProductReviewsTransactionsMapping : BaseEntity
{
    /// <summary>
    /// Gets or sets the product review identifier.
    /// </summary>
    public int ProductReviewId { get; set; }

    /// <summary>
    /// Gets or sets the external Etsy review identifier.
    /// </summary>
    public int EtsyReviewId { get; set; }

    /// <summary>
    /// Gets or sets the external transaction identifier.
    /// </summary>
    public long TransactionId { get; set; }
}
