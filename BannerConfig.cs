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

        /// <summary>
        /// How often banner text is re-evaluated, in seconds, for edges containing a token
        /// whose value changes over time such as {time} or {ipaddress}. Set to 0 to
        /// disable refreshing entirely.
        /// </summary>
        public int TextRefreshIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// Left, center, and right text for the top banner. When omitted, the top banner
        /// shows <see cref="ClassificationText"/> centered.
        /// </summary>
        public BannerEdgeConfig? Top { get; set; }

        /// <summary>
        /// Left, center, and right text for the bottom banner. When omitted, the bottom
        /// banner shows <see cref="ClassificationText"/> centered.
        /// </summary>
        public BannerEdgeConfig? Bottom { get; set; }

        /// <summary>
        /// Resolves the layout for one edge. Falls back to <see cref="ClassificationText"/>
        /// centered when that edge is not configured, so settings files written before
        /// per-edge text was supported continue to render unchanged.
        /// </summary>
        public BannerEdgeConfig GetEdge(AppBarHelper.ABEdge edge)
        {
            BannerEdgeConfig? configured = edge == AppBarHelper.ABEdge.ABE_TOP ? Top : Bottom;

            if (configured == null || configured.IsEmpty)
            {
                return new BannerEdgeConfig { Center = ClassificationText };
            }

            return configured;
        }
    }
}
