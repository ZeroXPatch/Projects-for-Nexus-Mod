using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace PerfectCatchStreak
{
    public class ModEntry : Mod
    {
        private ModConfig Config = null!;
        private int CurrentStreak = 0;

        private bool IsFishingMenuOpen = false;
        private bool IsCurrentCatchPerfect = true;
        private bool RewardProcessed = false;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            if (Game1.activeClickableMenu is BobberBar bobberMenu)
            {
                HandleFishingTick(bobberMenu);
            }
            else
            {
                HandleMenuClosed();
            }
        }

        private void HandleFishingTick(BobberBar bobberMenu)
        {
            if (!IsFishingMenuOpen)
            {
                IsFishingMenuOpen = true;
                IsCurrentCatchPerfect = true;
                RewardProcessed = false;
            }

            bool isPerfect = this.Helper.Reflection.GetField<bool>(bobberMenu, "perfect").GetValue();
            float progress = this.Helper.Reflection.GetField<float>(bobberMenu, "distanceFromCatching").GetValue();

            if (!isPerfect)
                IsCurrentCatchPerfect = false;

            if (progress >= 1.0f && !RewardProcessed)
            {
                RewardProcessed = true;
                if (IsCurrentCatchPerfect)
                    HandlePerfectCatch();
                else
                    HandleImperfectCatch();
            }
        }

        private void HandleMenuClosed()
        {
            if (IsFishingMenuOpen)
            {
                if (!RewardProcessed)
                    ResetStreak("Fish escaped!");

                IsFishingMenuOpen = false;
            }
        }

        private void HandlePerfectCatch()
        {
            CurrentStreak++;

            // Update Max Streak Record
            if (CurrentStreak > Config.MaxStreak)
            {
                Config.MaxStreak = CurrentStreak;
                this.Helper.WriteConfig(Config); // Save to config.json
            }

            int bonusXP = Config.BaseBonusXP + (CurrentStreak * Config.XPPerStreakLevel);
            Game1.player.gainExperience(1, bonusXP);

            if (Config.ShowHUDNotification)
            {
                string msg = $"Perfect Streak: {CurrentStreak} (Best: {Config.MaxStreak})";
                Game1.addHUDMessage(new HUDMessage(msg, HUDMessage.achievement_type));
            }

            if (Config.PlaySound)
                Game1.playSound("reward");
        }

        private void HandleImperfectCatch()
        {
            ResetStreak("Streak broken!");
        }

        private void ResetStreak(string reason)
        {
            if (CurrentStreak > 0)
            {
                if (Config.ShowHUDNotification)
                {
                    string msg = $"{reason} Final Streak: {CurrentStreak}";
                    Game1.addHUDMessage(new HUDMessage(msg, HUDMessage.error_type));
                }
            }
            CurrentStreak = 0;
        }
    }
}