using Nop.Web.Models.Catalog;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Models;

public sealed record HalloweenLandingModel
{
    public string Title { get; init; }
    public string MetaDescription { get; init; }
    public string Introduction { get; init; }
    public string CategoriesHeading { get; init; }
    public string ProductsHeading { get; init; }
    public IReadOnlyList<HalloweenLandingCategoryModel> Categories { get; init; } = Array.Empty<HalloweenLandingCategoryModel>();
    public IReadOnlyList<ProductOverviewModel> Products { get; init; } = Array.Empty<ProductOverviewModel>();
}

public sealed record HalloweenLandingCategoryModel(string Name, string Url);
