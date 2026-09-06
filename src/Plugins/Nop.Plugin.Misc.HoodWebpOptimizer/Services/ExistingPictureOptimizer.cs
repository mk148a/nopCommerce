using Nop.Core.Domain.Media;
using Nop.Core;
using Nop.Data;
using Nop.Services.Configuration;
using Nop.Services.Media;

namespace Nop.Plugin.Misc.HoodWebpOptimizer.Services;

public interface IExistingPictureOptimizer
{
    Task<int> ProcessBatchAsync(int maxCount, CancellationToken cancellationToken = default);
}

/// <summary>Bounded backfill for legacy files. It never rewrites SVG, GIF or existing WebP images.</summary>
public sealed class ExistingPictureOptimizer : IExistingPictureOptimizer
{
    private readonly IRepository<Picture> _pictures;
    private readonly IPictureService _pictureService;
    private readonly HoodWebpSettings _settings;
    private readonly ISettingService _settingService;

    public ExistingPictureOptimizer(IRepository<Picture> pictures, IPictureService pictureService,
        HoodWebpSettings settings, ISettingService settingService)
    {
        _pictures = pictures;
        _pictureService = pictureService;
        _settings = settings;
        _settingService = settingService;
    }

    public async Task<int> ProcessBatchAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        if (_pictureService is not AdaptiveWebpPictureService)
            return 0;
        maxCount = Math.Clamp(maxCount, 1, 100);
        var pictures = await _pictures.GetAllPagedAsync(query => query
            .Where(p => p.Id > _settings.LegacyScanCursorPictureId && p.MimeType != MimeTypes.ImageWebp && p.MimeType != MimeTypes.ImageSvg && p.MimeType != MimeTypes.ImageGif)
            .OrderBy(p => p.Id), pageSize: maxCount);
        var processed = 0;
        foreach (var picture in pictures)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var binary = await _pictureService.LoadPictureBinaryAsync(picture);
            if (binary.Length == 0)
                continue;
            var updated = await _pictureService.UpdatePictureAsync(picture.Id, binary, picture.MimeType, picture.SeoFilename,
                picture.AltAttribute, picture.TitleAttribute, picture.IsNew);
            if (updated?.MimeType == MimeTypes.ImageWebp)
                processed++;
        }
        if (pictures.Any())
        {
            _settings.LegacyScanCursorPictureId = pictures.Max(picture => picture.Id);
            await _settingService.SaveSettingAsync(_settings);
        }
        return processed;
    }
}
