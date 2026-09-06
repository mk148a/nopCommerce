using System.Collections.Generic;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.Widgets.CustomProductReviews.Areas.Admin.Models;

public sealed record ReviewMediaManageModel : BaseNopModel
{
    public int ReviewId { get; init; }
    public IList<ReviewMediaItemModel> Items { get; init; } = new List<ReviewMediaItemModel>();
    public CustomProductReviewsSettings Settings { get; init; }
}

public sealed record ReviewMediaItemModel : BaseNopEntityModel
{
    public int ReviewId { get; init; }
    public int ProductId { get; init; }
    public string ProductName { get; init; }
    public string ReviewTitle { get; init; }
    public bool IsVideo { get; init; }
    public string MediaUrl { get; init; }
    public string MimeType { get; init; }
    public int DisplayOrder { get; set; }
    public string AltAttribute { get; set; }
    public string TitleAttribute { get; set; }
}

public sealed record ReviewMediaEditModel
{
    public int Id { get; init; }
    public int ReviewId { get; init; }
    public int DisplayOrder { get; init; }
    public string AltAttribute { get; init; }
    public string TitleAttribute { get; init; }
}

public sealed record ReviewMediaSettingsModel
{
    public bool EnableReviewVideoTranscoding { get; init; }
    public string FfmpegExecutablePath { get; init; }
    public string FfprobeExecutablePath { get; init; }
    public int MaximumVideoSizeBytes { get; init; }
    public int MaximumVideoDurationSeconds { get; init; }
    public int MaximumVideoWidth { get; init; }
    public int MaximumVideoHeight { get; init; }
    public int NormalizedVideoMaxWidth { get; init; }
    public int VideoCrf { get; init; }
    public int VideoTranscodeTimeoutSeconds { get; init; }
}
