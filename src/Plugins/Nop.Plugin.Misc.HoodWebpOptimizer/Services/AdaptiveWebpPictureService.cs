using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Media;
using Nop.Core.Domain.Catalog;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Seo;

namespace Nop.Plugin.Misc.HoodWebpOptimizer.Services;

/// <summary>
/// Decorates nopCommerce's local PictureService by inheritance. AzurePictureService is deliberately never replaced.
/// All unsafe formats and unprofitable conversions fall through unchanged.
/// </summary>
public sealed class AdaptiveWebpPictureService : PictureService
{
    private readonly HoodWebpSettings _settings;
    private readonly IAdaptiveWebpEncoder _encoder;
    private readonly IOriginalPictureStore _originals;

    public AdaptiveWebpPictureService(IDownloadService downloadService,
        IHttpContextAccessor httpContextAccessor,
        ILogger logger,
        INopFileProvider fileProvider,
        IProductAttributeParser productAttributeParser,
        IProductAttributeService productAttributeService,
        IRepository<Picture> pictureRepository,
        IRepository<PictureBinary> pictureBinaryRepository,
        IRepository<ProductPicture> productPictureRepository,
        ISettingService settingService,
        IUrlRecordService urlRecordService,
        IWebHelper webHelper,
        MediaSettings mediaSettings,
        HoodWebpSettings settings,
        IAdaptiveWebpEncoder encoder,
        IOriginalPictureStore originals)
        : base(downloadService, httpContextAccessor, logger, fileProvider, productAttributeParser, productAttributeService,
            pictureRepository, pictureBinaryRepository, productPictureRepository, settingService, urlRecordService, webHelper, mediaSettings)
    {
        _settings = settings;
        _encoder = encoder;
        _originals = originals;
    }

    public override async Task<Picture> InsertPictureAsync(byte[] pictureBinary, string mimeType, string seoFilename,
        string altAttribute = null, string titleAttribute = null, bool isNew = true, bool validateBinary = true)
    {
        var result = _encoder.TryEncode(pictureBinary, mimeType, _settings);
        var picture = await base.InsertPictureAsync(result.Converted ? result.Binary : pictureBinary,
            result.Converted ? MimeTypes.ImageWebp : mimeType, seoFilename, altAttribute, titleAttribute, isNew, validateBinary);
        if (result.Converted && _settings.PreserveOriginals)
            await _originals.SaveAsync(picture, pictureBinary, mimeType);
        return picture;
    }

    public override async Task<Picture> UpdatePictureAsync(int pictureId, byte[] pictureBinary, string mimeType,
        string seoFilename, string altAttribute = null, string titleAttribute = null, bool isNew = true, bool validateBinary = true)
    {
        var oldPicture = await GetPictureByIdAsync(pictureId);
        var result = _encoder.TryEncode(pictureBinary, mimeType, _settings);
        var picture = await base.UpdatePictureAsync(pictureId, result.Converted ? result.Binary : pictureBinary,
            result.Converted ? MimeTypes.ImageWebp : mimeType, seoFilename, altAttribute, titleAttribute, isNew, validateBinary);
        if (picture is not null && result.Converted && _settings.PreserveOriginals)
            await _originals.SaveAsync(picture, pictureBinary, mimeType);
        if (picture is not null && result.Converted && oldPicture is not null &&
            !string.Equals(oldPicture.MimeType, MimeTypes.ImageWebp, StringComparison.OrdinalIgnoreCase) &&
            !await IsStoreInDbAsync())
        {
            // PictureService removes generated thumbs but not the source file when its MIME extension changes.
            // The new WebP was saved successfully by base.UpdatePictureAsync before this cleanup is attempted.
            var oldExtension = await GetFileExtensionFromMimeTypeAsync(oldPicture.MimeType);
            var oldFile = _fileProvider.GetAbsolutePath("images", $"{pictureId:0000000}_0.{oldExtension}");
            if (_fileProvider.FileExists(oldFile))
                _fileProvider.DeleteFile(oldFile);
        }
        return picture;
    }
}
