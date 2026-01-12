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

        private bool TreasureCaught = false;

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
                TreasureCaught = false;
            }

            // Get current status from the game
            bool isPerfect = this.Helper.Reflection.GetField<bool>(bobberMenu, "perfect").GetValue();
            float progress = this.Helper.Reflection.GetField<float>(bobberMenu, "distanceFromCatching").GetValue();
            TreasureCaught = this.Helper.Reflection.GetField<bool>(bobberMenu, "treasureCaught").GetValue();

            // Track if the bar ever leaves the fish
            if (!isPerfect)
                IsCurrentCatchPerfect = false;

            // When the fish is successfully caught
            if (progress >= 1.0f && !RewardProcessed)
            {
                RewardProcessed = true;

                // --- THE NEW LOGIC ---
                // The streak continues if:
                // 1. You caught the treasure (doesn't matter if the fish was perfect)
                // OR 
                // 2. You caught the fish perfectly (doesn't matter if you missed/ignored the treasure)
                if (TreasureCaught || IsCurrentCatchPerfect)
                {
                    HandlePerfectCatch();
                }
                else
                {
                    HandleImperfectCatch();
                }
            }
        }

        private void HandleMenuClosed()
        {
            if (IsFishingMenuOpen)
            {
                // If the menu closed but RewardProcessed is false, the fish escaped.
                if (!RewardProcessed)
                {
                    string reason = this.Helper.Translation.Get("reason.escaped");
                    ResetStreak(reason);
                }

                IsFishingMenuOpen = false;
            }
        }

        private void HandlePerfectCatch()
        {
            CurrentStreak++;

            if (CurrentStreak > Config.MaxStreak)
            {
                Config.MaxStreak = CurrentStreak;
                this.Helper.WriteConfig(Config);
            }

            int bonusXP = Config.BaseBonusXP + (CurrentStreak * Config.XPPerStreakLevel);
            Game1.player.gainExperience(1, bonusXP);

            if (Config.ShowHUDNotification)
            {
                string msg = this.Helper.Translation.Get("hud.streak", new { current = CurrentStreak, max = Config.MaxStreak });
                Game1.addHUDMessage(new HUDMessage(msg, HUDMessage.achievement_type));
            }

            if (Config.PlaySound)
                Game1.playSound("reward");
        }

        private void HandleImperfectCatch()
        {
            string reason = this.Helper.Translation.Get("reason.broken");
            ResetStreak(reason);
        }

        private void ResetStreak(string reason)
        {
            if (CurrentStreak > 0)
            {
                if (Config.ShowHUDNotification)
                {
                    string msg = this.Helper.Translation.Get("hud.streak_lost", new { reason = reason, final = CurrentStreak });
                    Game1.addHUDMessage(new HUDMessage(msg, HUDMessage.error_type));
                }
            }
            CurrentStreak = 0;
        }
    }
}