using System;

namespace ClassificationBanner
{
    /// <summary>
    /// Text displayed in the three cells of a single banner edge. Any cell left null or
    /// empty renders nothing, so a banner may show one, two, or three pieces of text.
    /// </summary>
    public class BannerEdgeConfig
    {
        /// <summary>Text shown flush against the left of the screen.</summary>
        public string? Left { get; set; }

        /// <summary>Text shown centered on the screen.</summary>
        public string? Center { get; set; }

        /// <summary>Text shown flush against the right of the screen.</summary>
        public string? Right { get; set; }

        /// <summary>True when no cell carries any text.</summary>
        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(Left) &&
            string.IsNullOrWhiteSpace(Center) &&
            string.IsNullOrWhiteSpace(Right);
    }
}
