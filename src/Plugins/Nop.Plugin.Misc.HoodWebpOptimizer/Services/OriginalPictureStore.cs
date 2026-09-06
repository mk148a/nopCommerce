using System.Text.Json;
using Nop.Core;
using Nop.Core.Domain.Media;
using Nop.Core.Infrastructure;

namespace Nop.Plugin.Misc.HoodWebpOptimizer.Services;

/// <summary>Stores source uploads outside wwwroot so conversion is reversible without exposing originals publicly.</summary>
public sealed class OriginalPictureStore : IOriginalPictureStore
{
    private readonly INopFileProvider _fileProvider;

    public OriginalPictureStore(INopFileProvider fileProvider) => _fileProvider = fileProvider;

    public async Task SaveAsync(Picture picture, byte[] binary, string mimeType)
    {
        if (picture is null || picture.Id <= 0 || binary is null || binary.Length == 0)
            return;

        var directory = _fileProvider.GetAbsolutePath(HoodWebpDefaults.OriginalFilesPath);
        _fileProvider.CreateDirectory(directory);
        var extension = ExtensionFor(mimeType);
        var binaryPath = _fileProvider.Combine(directory, $"{picture.Id}.{extension}");
        var manifestPath = _fileProvider.Combine(directory, $"{picture.Id}.json");

        // A replacement upload intentionally replaces the previous recovery point for the same Picture ID.
        await _fileProvider.WriteAllBytesAsync(binaryPath, binary);
        var manifest = JsonSerializer.Serialize(new OriginalManifest(mimeType, _fileProvider.GetFileName(binaryPath)));
        await _fileProvider.WriteAllTextAsync(manifestPath, manifest, System.Text.Encoding.UTF8);
    }

    public async Task<StoredOriginalPicture> LoadAsync(int pictureId)
    {
        if (pictureId <= 0)
            return null;

        var directory = _fileProvider.GetAbsolutePath(HoodWebpDefaults.OriginalFilesPath);
        var manifestPath = _fileProvider.Combine(directory, $"{pictureId}.json");
        if (!_fileProvider.FileExists(manifestPath))
            return null;

        try
        {
            var manifest = JsonSerializer.Deserialize<OriginalManifest>(await _fileProvider.ReadAllTextAsync(manifestPath, System.Text.Encoding.UTF8));
            if (manifest is null || string.IsNullOrWhiteSpace(manifest.FileName))
                return null;
            var binaryPath = _fileProvider.Combine(directory, _fileProvider.GetFileName(manifest.FileName));
            return !_fileProvider.FileExists(binaryPath)
                ? null
                : new StoredOriginalPicture(await _fileProvider.ReadAllBytesAsync(binaryPath), manifest.MimeType, manifest.FileName);
        }
        catch
        {
            return null;
        }
    }

    private static string ExtensionFor(string mimeType) => mimeType?.ToLowerInvariant() switch
    {
        "image/jpeg" or "image/jpg" => "jpg",
        "image/png" => "png",
        "image/bmp" => "bmp",
        "image/tiff" => "tiff",
        _ => "bin"
    };

    private sealed record OriginalManifest(string MimeType, string FileName);
}
