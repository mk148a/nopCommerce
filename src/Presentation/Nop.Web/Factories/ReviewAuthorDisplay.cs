using Nop.Core.Domain.Customers;

namespace Nop.Web.Factories;

/// <summary>
/// Keeps imported marketplace review authors privacy-preserving without
/// exposing synthetic account identifiers or inventing a person's name.
/// </summary>
public static class ReviewAuthorDisplay
{
    public const string MarketplaceCustomerLabel = "Anonymous";

    public static bool IsMarketplaceImportedCustomer(Customer customer)
    {
        if (customer == null)
            return false;

        var username = customer.Username?.Trim();
        var email = customer.Email?.Trim();

        return (!string.IsNullOrWhiteSpace(username)
                && username.StartsWith("etsy_review_", StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(email)
                && email.EndsWith("@hoodarcheryshop.invalid", StringComparison.OrdinalIgnoreCase));
    }
}
