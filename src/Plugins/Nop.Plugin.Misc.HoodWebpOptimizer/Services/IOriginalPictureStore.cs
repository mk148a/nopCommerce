using Nop.Core.Domain.Media;

namespace Nop.Plugin.Misc.HoodWebpOptimizer.Services;

public interface IOriginalPictureStore
{
    Task SaveAsync(Picture picture, byte[] binary, string mimeType);
    Task<StoredOriginalPicture> LoadAsync(int pictureId);
}

public sealed record StoredOriginalPicture(byte[] Binary, string MimeType, string FileName);
