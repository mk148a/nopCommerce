using Nop.Core.Domain.Media;

namespace Nop.Services.Media;

/// <summary>
/// Video service interface
/// </summary>
public partial interface IVideoService
{
    /// <summary>
    /// Gets a video
    /// </summary>
    /// <param name="videoId">Video identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the video
    /// </returns>
    Task<ProductReviewVideo> GetVideoByIdAsync(int videoId);

    /// <summary>
    /// Gets videos by product identifier
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the videos
    /// </returns>
    Task<IList<ProductReviewVideo>> GetVideosByProductIdAsync(int productId);

    /// <summary>
    /// Inserts a video
    /// </summary>
    /// <param name="video">Video</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the video
    /// </returns>
    Task<ProductReviewVideo> InsertVideoAsync(ProductReviewVideo video);

    /// <summary>
    /// Updates the video
    /// </summary>
    /// <param name="video">Video</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the video
    /// </returns>
    Task<ProductReviewVideo> UpdateVideoAsync(ProductReviewVideo video);

    /// <summary>
    /// Deletes a video
    /// </summary>
    /// <param name="video">Video</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task DeleteVideoAsync(ProductReviewVideo video);
}