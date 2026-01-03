using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buffs;

namespace SleepBuffs
{
    public class ModEntry : Mod
    {
        private ModConfig Config = new();
        private const string ModDataKey = "YourName.SleepBuffs.BedTime";

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.DayEnding += this.OnDayEnding;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");

            if (configMenu is null)
                return;

            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.EnableDebuffs,
                setValue: value => this.Config.EnableDebuffs = value,
                name: () => "Enable Sleep Debuffs",
                tooltip: () => "If enabled, sleeping less than 6 hours (after 12:00 AM) will give random negative stats."
            );
        }

        private void OnDayEnding(object? sender, DayEndingEventArgs e)
        {
            if (!Context.IsMainPlayer) return;

            Game1.player.modData[ModDataKey] = Game1.timeOfDay.ToString();
            this.Monitor.Log($"Bedtime recorded: {Game1.timeOfDay}", LogLevel.Debug);
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (!Context.IsMainPlayer) return;

            if (!Game1.player.modData.TryGetValue(ModDataKey, out string? timeString))
                return;

            Game1.player.modData.Remove(ModDataKey);

            if (int.TryParse(timeString, out int bedTime))
            {
                ApplySleepEffects(bedTime);
            }
        }

        private void ApplySleepEffects(int bedTime)
        {
            // 2200 = 10 PM. 2400 = 12 AM.

            // Condition 1: 8+ Hours Sleep (Bedtime <= 2200)
            if (bedTime <= 2200)
            {
                ApplyRandomStats(isDebuff: false);
                Game1.addHUDMessage(new HUDMessage("You feel well rested!", 1));
            }
            // Condition 2: < 6 Hours Sleep (Bedtime > 2400)
            else if (this.Config.EnableDebuffs && bedTime > 2400)
            {
                ApplyRandomStats(isDebuff: true);
                Game1.addHUDMessage(new HUDMessage("You didn't get enough sleep...", 3));
            }
        }

        private void ApplyRandomStats(bool isDebuff)
        {
            var effects = new BuffEffects();

            List<string> stats = new List<string>
            {
                "Farming", "Fishing", "Mining", "Combat", "Foraging",
                "Luck", "Speed", "Defense", "Attack", "MaxStamina"
            };

            // FIX: Changed Game1.Random to Game1.random (lowercase)
            var chosenStats = stats.OrderBy(x => Game1.random.Next()).Take(2).ToList();

            float value = isDebuff ? -1f : 1f;

            foreach (var stat in chosenStats)
            {
                switch (stat)
                {
                    case "Farming": effects.FarmingLevel.Value = value; break;
                    case "Fishing": effects.FishingLevel.Value = value; break;
                    case "Mining": effects.MiningLevel.Value = value; break;
                    case "Combat": effects.CombatLevel.Value = value; break;
                    case "Foraging": effects.ForagingLevel.Value = value; break;
                    case "Luck": effects.LuckLevel.Value = value; break;
                    case "Speed": effects.Speed.Value = value; break;
                    case "Defense": effects.Defense.Value = value; break;
                    case "Attack": effects.Attack.Value = value; break;
                    case "MaxStamina": effects.MaxStamina.Value = value * 30; break;
                }
            }

            string id = isDebuff ? "SleepDeprived" : "WellRested";
            string title = isDebuff ? "Sleep Deprived" : "Well Rested";
            string desc = isDebuff
                ? $"Groggy: {string.Join(", ", chosenStats)} down."
                : $"Energized: {string.Join(", ", chosenStats)} up.";

            int iconIndex = isDebuff ? 18 : 0;

            Buff buff = new Buff(
                id: $"YourName.SleepBuffs.{id}",
                displayName: title,
                iconTexture: Game1.buffsIcons,
                iconSheetIndex: iconIndex,
                duration: Buff.ENDLESS,
                effects: effects,
                isDebuff: isDebuff,
                description: desc
            );

            Game1.player.applyBuff(buff);
        }
    }
}