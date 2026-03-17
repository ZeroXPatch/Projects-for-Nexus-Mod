using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using LoadingProgressBar.Config;
using LoadingProgressBar.Services;

namespace LoadingProgressBar
{
    /// <summary>
    /// Main mod entry point for Loading Progress Bar
    /// Shows a 0-100% progress bar during day-to-day transitions
    /// </summary>
    public class ModEntry : Mod
    {
        public static ModEntry Instance { get; private set; }
        public static ModConfig Config { get; private set; }
        
        public override void Entry(IModHelper helper)
        {
            Instance = this;
            Config = helper.ReadConfig<ModConfig>();
            
            // Hook into day transition events
            helper.Events.GameLoop.DayEnding += OnDayEnding;
            helper.Events.GameLoop.Saving += OnSaving;
            helper.Events.GameLoop.Saved += OnSaved;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            
            // Hook into rendering to draw the progress bar
            helper.Events.Display.RenderedHud += OnRenderedHud;
            
            this.Monitor.Log("Loading Progress Bar initialized!", LogLevel.Info);
        }
        
        /// <summary>
        /// Called when the day is ending (before saving)
        /// </summary>
        private void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            ProgressService.Start();
            ProgressService.UpdateProgress("Preparing to save...", 0.0f);
        }
        
        /// <summary>
        /// Called when the game starts saving
        /// </summary>
        private void OnSaving(object sender, SavingEventArgs e)
        {
            ProgressService.UpdateProgress("Saving game...", 0.25f);
        }
        
        /// <summary>
        /// Called when the game finishes saving
        /// </summary>
        private void OnSaved(object sender, SavedEventArgs e)
        {
            ProgressService.UpdateProgress("Processing new day...", 0.60f);
        }
        
        /// <summary>
        /// Called when the new day starts
        /// </summary>
        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            ProgressService.UpdateProgress("Complete!", 1.0f);
            ProgressService.Complete();
        }
        
        /// <summary>
        /// Called to render the HUD - draws the progress bar
        /// </summary>
        private void OnRenderedHud(object sender, RenderedHudEventArgs e)
        {
            ProgressService.Draw(e.SpriteBatch);
        }
    }
}
