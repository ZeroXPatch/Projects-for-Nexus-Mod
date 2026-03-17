namespace LoadingProgressBar.Config
{
    /// <summary>
    /// Configuration for Loading Progress Bar
    /// </summary>
    public class ModConfig
    {
        /// <summary>
        /// Enable/disable the progress bar
        /// </summary>
        public bool ShowProgressBar { get; set; } = true;
        
        /// <summary>
        /// Progress bar width in pixels
        /// </summary>
        public int BarWidth { get; set; } = 500;
        
        /// <summary>
        /// Progress bar height in pixels
        /// </summary>
        public int BarHeight { get; set; } = 40;
        
        /// <summary>
        /// Show percentage text on the bar
        /// </summary>
        public bool ShowPercentage { get; set; } = true;
        
        /// <summary>
        /// Show status message on the bar
        /// </summary>
        public bool ShowMessage { get; set; } = true;
    }
}
