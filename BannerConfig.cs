using System;

namespace ClassificationBanner
{
    public class BannerConfig
    {
        public string ClassificationText { get; set; } = "UNCLASSIFIED"; // "SECRET // NOFORN";
        public string BackgroundColorHex { get; set; } = "#FF008000"; // Default Green
        public string ForegroundColorHex { get; set; } = "#FFFFFF"; // Default White
        public int BannerHeight { get; set; } = 30;
        public int EnforcementIntervalMs { get; set; } = 500; // Timer loop speed
    }
}
