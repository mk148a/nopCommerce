using Nop.Core.Configuration;

namespace Nop.Plugin.Widgets.CustomProductReviews
{
    /// <summary>
    /// Represents a EcbExchangeRate plugin settings
    /// </summary>
    public class CustomProductReviewsSettings: ISettings
    {
       
        public string data { get; set; }
        public bool license { get; set; }
        public string WidgetZone { get; set; }
        public int MaximumFile { get; set; }
        public int MaximumSize { get; set; }
        /// <summary>
        /// Maximum bytes accepted for one review video. Photo uploads keep the
        /// existing media limit; short customer videos have a deliberately
        /// tighter limit so mobile uploads and transcoding remain predictable.
        /// </summary>
        public int MaximumVideoSizeBytes { get; set; } = 104857600;
        /// <summary>
        /// Enables server-side normalization of review videos. It is deliberately
        /// fail-closed: when enabled an explicit, trusted FFmpeg path is required.
        /// </summary>
        public bool EnableReviewVideoTranscoding { get; set; }
        public string FfmpegExecutablePath { get; set; }
        public string FfprobeExecutablePath { get; set; }
        public int MaximumVideoDurationSeconds { get; set; }
        public int MaximumVideoWidth { get; set; }
        public int MaximumVideoHeight { get; set; }
        public int NormalizedVideoMaxWidth { get; set; }
        public int VideoCrf { get; set; }
        public int VideoTranscodeTimeoutSeconds { get; set; }
    }
}
