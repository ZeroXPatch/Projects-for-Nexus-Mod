using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buffs;
using StardewValley.Menus;

namespace WeatherBuffs
{
    internal enum WeatherBuffType
    {
        None,
        // vanilla-related types
        Rain,
        Storm,
        SunnySummer,
        Snow,
        Windy,

        // Weather Wonders weathers
        Blizzard,
        Deluge,
        Drizzle,
        DryLightning,
        Hailstorm,
        Heatwave,
        Mist,
        MuddyRain,
        RainSnowMix,
        AcidRain,
        Cloudy
    }

    public class ModEntry : Mod
    {
        private const string WeatherBuffId = "ZeroXPatch.WeatherBuffs/WeatherBuff";
        private const string WeatherCacheKey = "WeatherBuffs.WeatherCache";

        private WeatherBuffType _currentBuffType = WeatherBuffType.None;
        private int _ticksSinceLastWeatherCheck;
        private Rectangle _iconRect;

        // cached Weather Wonders ID for TODAY
        private string _todayWeatherId = string.Empty;

        private ModConfig Config;

        public override void Entry(IModHelper helper)
        {
            // load config
            this.Config = this.Helper.ReadConfig<ModConfig>();

            // GMCM
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;

            // core events
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.DayEnding += this.OnDayEnding;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Display.RenderedHud += this.OnRenderedHud;
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm is null)
                return;

            gmcm.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config),
                titleScreenOnly: false
            );

            // Section: General
            gmcm.AddSectionTitle(
                this.ModManifest,
                () => "Weather Buffs - General",
                () => "Configure icon, position, and global stat scaling."
            );

            gmcm.AddBoolOption(
                this.ModManifest,
                getValue: () => this.Config.ShowIcon,
                setValue: v => this.Config.ShowIcon = v,
                name: () => "Show HUD icon",
                tooltip: () => "If disabled, buffs/debuffs still apply but the top-left weather icon is hidden."
            );

            gmcm.AddNumberOption(
                this.ModManifest,
                getValue: () => this.Config.IconX,
                setValue: v => this.Config.IconX = v,
                name: () => "Icon X position",
                tooltip: () => "Horizontal position of the weather buff icon (in UI units).",
                min: 0,
                max: 4000,
                interval: 1
            );

            gmcm.AddNumberOption(
                this.ModManifest,
                getValue: () => this.Config.IconY,
                setValue: v => this.Config.IconY = v,
                name: () => "Icon Y position",
                tooltip: () => "Vertical position of the weather buff icon (in UI units).",
                min: 0,
                max: 4000,
                interval: 1
            );

            gmcm.AddNumberOption(
                this.ModManifest,
                getValue: () => this.Config.BuffMultiplierPercent,
                setValue: v => this.Config.BuffMultiplierPercent = v,
                name: () => "Buff strength (%)",
                tooltip: () => "Scales all positive skill bonuses. 100 = default, 50 = half, 200 = double.",
                min: 10,
                max: 300,
                interval: 10
            );

            gmcm.AddNumberOption(
                this.ModManifest,
                getValue: () => this.Config.DebuffMultiplierPercent,
                setValue: v => this.Config.DebuffMultiplierPercent = v,
                name: () => "Debuff strength (%)",
                tooltip: () => "Scales all negative skill penalties. 100 = default, 50 = half, 200 = double.",
                min: 10,
                max: 300,
                interval: 10
            );

            gmcm.AddNumberOption(
                this.ModManifest,
                getValue: () => this.Config.FishingQualityLuckBonus,
                setValue: v => this.Config.FishingQualityLuckBonus = v,
                name: () => "Fishing quality bonus (Luck)",
                tooltip: () => "Extra Luck added on days with a positive Fishing bonus, simulating better fish quality. 0 = disabled.",
                min: 0,
                max: 5,
                interval: 1
            );
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            this._currentBuffType = WeatherBuffType.None;
            this._ticksSinceLastWeatherCheck = 0;

            // load cached TODAY weather ID (if any)
            WeatherCache cache = this.Helper.Data.ReadSaveData<WeatherCache>(WeatherCacheKey);
            this._todayWeatherId = cache?.TodayWeatherId ?? string.Empty;

            this.RefreshWeatherBuff();
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            this._currentBuffType = WeatherBuffType.None;
            this._ticksSinceLastWeatherCheck = 0;

            // load cached TODAY weather ID (written at end of previous day)
            WeatherCache cache = this.Helper.Data.ReadSaveData<WeatherCache>(WeatherCacheKey);
            this._todayWeatherId = cache?.TodayWeatherId ?? string.Empty;

            this.RefreshWeatherBuff();
        }

        /// <summary>
        /// At the end of the day, record tomorrow's weather ID so that next time we can
        /// treat it as *today* (even after reload). This fixes the 'buff for tomorrow' bug.
        /// </summary>
        private void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            string tomorrowId = Game1.weatherForTomorrow ?? string.Empty;
            WeatherCache cache = new WeatherCache
            {
                TodayWeatherId = tomorrowId
            };
            this.Helper.Data.WriteSaveData(WeatherCacheKey, cache);
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!StardewModdingAPI.Context.IsWorldReady)
                return;

            this._ticksSinceLastWeatherCheck++;

            if (this._ticksSinceLastWeatherCheck >= 60)
            {
                this._ticksSinceLastWeatherCheck = 0;
                this.RefreshWeatherBuff();
            }
        }

        private void RefreshWeatherBuff()
        {
            if (!StardewModdingAPI.Context.IsWorldReady || Game1.player == null)
                return;

            WeatherBuffType newType = this.DetermineWeatherBuffType();
            if (newType == this._currentBuffType)
                return;

            this._currentBuffType = newType;
            this.ApplyBuffForCurrentWeather();
        }

        /// <summary>
        /// Decide which buff type should be active based on current weather.
        /// Uses cached Weather Wonders ID for today if available,
        /// then falls back to vanilla weather flags.
        /// </summary>
        private WeatherBuffType DetermineWeatherBuffType()
        {
            // 1) Weather Wonders / custom IDs via cached "_todayWeatherId"
            string weatherId = this._todayWeatherId ?? string.Empty;

            WeatherBuffType wwType = this.MapWeatherWondersId(weatherId);
            if (wwType != WeatherBuffType.None)
                return wwType;

            // 2) Vanilla-based logic
            // Priority: Storm > Rain > Snow > Windy > SunnySummer > None
            if (Game1.isLightning)
                return WeatherBuffType.Storm;

            if (Game1.isRaining)
                return WeatherBuffType.Rain;

            if (Game1.isSnowing)
                return WeatherBuffType.Snow;

            if (Game1.isDebrisWeather)
                return WeatherBuffType.Windy;

            bool isSunnySummer =
                string.Equals(Game1.currentSeason, "summer", StringComparison.OrdinalIgnoreCase)
                && !Game1.isRaining
                && !Game1.isLightning
                && !Game1.isSnowing
                && !Game1.isDebrisWeather;

            if (isSunnySummer)
                return WeatherBuffType.SunnySummer;

            return WeatherBuffType.None;
        }

        /// <summary>
        /// Map Weather Wonders internal weather ID to our enum.
        /// Muddy Rain is robust, Mist uses the exact ID.
        /// </summary>
        private WeatherBuffType MapWeatherWondersId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return WeatherBuffType.None;

            // robust handling for Muddy Rain (ID variants)
            if (id.IndexOf("MuddyRain", StringComparison.OrdinalIgnoreCase) >= 0)
                return WeatherBuffType.MuddyRain;

            switch (id)
            {
                case "Kana.WeatherWonders_Blizzard":
                    return WeatherBuffType.Blizzard;
                case "Kana.WeatherWonders_Deluge":
                    return WeatherBuffType.Deluge;
                case "Kana.WeatherWonders_Drizzle":
                    return WeatherBuffType.Drizzle;
                case "Kana.WeatherWonders_DryLightning":
                    return WeatherBuffType.DryLightning;
                case "Kana.WeatherWonders_Hailstorm":
                    return WeatherBuffType.Hailstorm;
                case "Kana.WeatherWonders_Heatwave":
                    return WeatherBuffType.Heatwave;
                case "Kana.WeatherWonders_Mist":
                    return WeatherBuffType.Mist;
                case "Kana.WeatherWonders_RainSnowMix":
                    return WeatherBuffType.RainSnowMix;
                case "Kana.WeatherWonders_AcidRain":
                    return WeatherBuffType.AcidRain;
                case "Kana.WeatherWonders_Cloudy":
                    return WeatherBuffType.Cloudy;

                // lunar events intentionally ignored

                default:
                    return WeatherBuffType.None;
            }
        }

        /// <summary>Scale a raw value using config multipliers.</summary>
        private int ScaleValue(int value)
        {
            if (value > 0)
            {
                float mult = this.Config.BuffMultiplierPercent / 100f;
                return (int)Math.Round(value * mult);
            }

            if (value < 0)
            {
                float mult = this.Config.DebuffMultiplierPercent / 100f;
                return (int)Math.Round(value * mult);
            }

            return 0;
        }

        /// <summary>Apply the realistic skill buffs/debuffs for the current weather.</summary>
        private void ApplyBuffForCurrentWeather()
        {
            if (!StardewModdingAPI.Context.IsWorldReady || Game1.player == null)
                return;

            BuffEffects effects = new BuffEffects();
            string displayName;
            string description;

            // aggregate stat changes so each stat is only added ONCE
            int farm = 0, forage = 0, fish = 0, mine = 0, combat = 0, luck = 0;
            bool hasPositiveFishingBonus = false;

            switch (this._currentBuffType)
            {
                // ===== VANILLA-LIKE WEATHERS =====

                case WeatherBuffType.Rain:
                    farm = this.ScaleValue(1);
                    fish = this.ScaleValue(2);
                    forage = this.ScaleValue(-2);
                    displayName = this.Helper.Translation.Get("buff.rain.name");
                    description = this.Helper.Translation.Get("buff.rain.description");
                    break;

                case WeatherBuffType.Storm:
                    mine = this.ScaleValue(2);
                    combat = this.ScaleValue(1);
                    farm = this.ScaleValue(-2);
                    luck = this.ScaleValue(-1);
                    displayName = this.Helper.Translation.Get("buff.storm.name");
                    description = this.Helper.Translation.Get("buff.storm.description");
                    break;

                case WeatherBuffType.SunnySummer:
                    farm = this.ScaleValue(2);
                    forage = this.ScaleValue(1);
                    fish = this.ScaleValue(-2);
                    displayName = this.Helper.Translation.Get("buff.sunny.name");
                    description = this.Helper.Translation.Get("buff.sunny.description");
                    break;

                case WeatherBuffType.Snow:
                    luck = this.ScaleValue(2);
                    mine = this.ScaleValue(1);
                    forage = this.ScaleValue(-2);
                    fish = this.ScaleValue(-1);
                    displayName = this.Helper.Translation.Get("buff.snow.name");
                    description = this.Helper.Translation.Get("buff.snow.description");
                    break;

                case WeatherBuffType.Windy:
                    forage = this.ScaleValue(2);
                    luck = this.ScaleValue(1);
                    combat = this.ScaleValue(-2);
                    fish = this.ScaleValue(-1);
                    displayName = this.Helper.Translation.Get("buff.windy.name");
                    description = this.Helper.Translation.Get("buff.windy.description");
                    break;

                // ===== WEATHER WONDERS WEATHERS =====

                case WeatherBuffType.Blizzard:
                    mine = this.ScaleValue(2);
                    combat = this.ScaleValue(1);
                    forage = this.ScaleValue(-2);
                    farm = this.ScaleValue(-1);
                    displayName = this.Helper.Translation.Get("buff.blizzard.name");
                    description = this.Helper.Translation.Get("buff.blizzard.description");
                    break;

                case WeatherBuffType.Deluge:
                    fish = this.ScaleValue(2);
                    forage = this.ScaleValue(1);
                    farm = this.ScaleValue(-2);
                    mine = this.ScaleValue(-1);
                    displayName = this.Helper.Translation.Get("buff.deluge.name");
                    description = this.Helper.Translation.Get("buff.deluge.description");
                    break;

                case WeatherBuffType.Drizzle:
                    fish = this.ScaleValue(1);
                    farm = this.ScaleValue(1);
                    forage = this.ScaleValue(-1);
                    displayName = this.Helper.Translation.Get("buff.drizzle.name");
                    description = this.Helper.Translation.Get("buff.drizzle.description");
                    break;

                case WeatherBuffType.DryLightning:
                    combat = this.ScaleValue(2);
                    mine = this.ScaleValue(1);
                    farm = this.ScaleValue(-2);
                    forage = this.ScaleValue(-1);
                    displayName = this.Helper.Translation.Get("buff.drylightning.name");
                    description = this.Helper.Translation.Get("buff.drylightning.description");
                    break;

                case WeatherBuffType.Hailstorm:
                    combat = this.ScaleValue(2);
                    luck = this.ScaleValue(1);
                    forage = this.ScaleValue(-2);
                    farm = this.ScaleValue(-1);
                    displayName = this.Helper.Translation.Get("buff.hailstorm.name");
                    description = this.Helper.Translation.Get("buff.hailstorm.description");
                    break;

                case WeatherBuffType.Heatwave:
                    mine = this.ScaleValue(1);
                    luck = this.ScaleValue(1);
                    farm = this.ScaleValue(-2);
                    combat = this.ScaleValue(-1);
                    displayName = this.Helper.Translation.Get("buff.heatwave.name");
                    description = this.Helper.Translation.Get("buff.heatwave.description");
                    break;

                case WeatherBuffType.Mist:
                    forage = this.ScaleValue(2);
                    fish = this.ScaleValue(1);
                    combat = this.ScaleValue(-2);
                    luck = this.ScaleValue(-1);
                    displayName = this.Helper.Translation.Get("buff.mist.name");
                    description = this.Helper.Translation.Get("buff.mist.description");
                    break;

                case WeatherBuffType.MuddyRain:
                    farm = this.ScaleValue(2);
                    mine = this.ScaleValue(1);
                    forage = this.ScaleValue(-2);
                    combat = this.ScaleValue(-1);
                    displayName = this.Helper.Translation.Get("buff.muddyrain.name");
                    description = this.Helper.Translation.Get("buff.muddyrain.description");
                    break;

                case WeatherBuffType.RainSnowMix:
                    fish = this.ScaleValue(2);
                    mine = this.ScaleValue(1);
                    farm = this.ScaleValue(-2);
                    forage = this.ScaleValue(-1);
                    displayName = this.Helper.Translation.Get("buff.rainsnowmix.name");
                    description = this.Helper.Translation.Get("buff.rainsnowmix.description");
                    break;

                case WeatherBuffType.AcidRain:
                    mine = this.ScaleValue(2);
                    combat = this.ScaleValue(1);
                    forage = this.ScaleValue(-2);
                    farm = this.ScaleValue(-1);
                    displayName = this.Helper.Translation.Get("buff.acidrain.name");
                    description = this.Helper.Translation.Get("buff.acidrain.description");
                    break;

                case WeatherBuffType.Cloudy:
                    forage = this.ScaleValue(1);
                    fish = this.ScaleValue(1);
                    farm = this.ScaleValue(-1);
                    displayName = this.Helper.Translation.Get("buff.cloudy.name");
                    description = this.Helper.Translation.Get("buff.cloudy.description");
                    break;

                // ===== NONE =====

                case WeatherBuffType.None:
                default:
                    displayName = this.Helper.Translation.Get("buff.none.name");
                    description = this.Helper.Translation.Get("buff.none.description");
                    break;
            }

            // detect if this is a "fishing buff day"
            if (fish > 0)
                hasPositiveFishingBonus = true;

            // fishing quality = small extra Luck on fishing+ days
            if (hasPositiveFishingBonus && this.Config.FishingQualityLuckBonus > 0)
                luck += this.Config.FishingQualityLuckBonus;

            // now add each stat ONCE
            if (farm != 0)
                effects.FarmingLevel.Add(farm);
            if (forage != 0)
                effects.ForagingLevel.Add(forage);
            if (fish != 0)
                effects.FishingLevel.Add(fish);
            if (mine != 0)
                effects.MiningLevel.Add(mine);
            if (combat != 0)
                effects.CombatLevel.Add(combat);
            if (luck != 0)
                effects.LuckLevel.Add(luck);

            Buff buff = new Buff(
                id: WeatherBuffId,
                displayName: displayName,
                description: description,
                iconTexture: Game1.mouseCursors,
                iconSheetIndex: 0,
                duration: Buff.ENDLESS,
                effects: effects
            )
            {
                visible = false
            };

            Game1.player.applyBuff(buff);
        }

        private void OnRenderedHud(object sender, RenderedHudEventArgs e)
        {
            if (!StardewModdingAPI.Context.IsWorldReady || Game1.player == null)
                return;

            if (this._currentBuffType == WeatherBuffType.None)
                return;

            if (!this.Config.ShowIcon)
                return;

            if (Game1.eventUp || Game1.currentLocation == null)
                return;

            SpriteBatch spriteBatch = e.SpriteBatch;

            float scale = Game1.options.uiScale;

            // icon size
            int iconWidth = (int)(40 * scale);
            int iconHeight = (int)(32 * scale);

            // position from config, scaled with UI
            int x = (int)(this.Config.IconX * scale);
            int y = (int)(this.Config.IconY * scale);

            this._iconRect = new Rectangle(x, y, iconWidth, iconHeight);

            // background
            spriteBatch.Draw(Game1.fadeToBlackRect, this._iconRect, Color.Black * 0.35f);

            // inner rectangle
            Rectangle inner = new Rectangle(
                x + iconWidth / 8,
                y + iconHeight / 8,
                iconWidth * 3 / 4,
                iconHeight * 3 / 4
            );
            spriteBatch.Draw(Game1.fadeToBlackRect, inner, Color.White * 0.8f);

            string label = this.GetBuffIconLabel();

            // scale text down a bit so it fits inside the box
            float textScale = 0.85f;

            // measure using the scaled size
            Vector2 textSize = Game1.smallFont.MeasureString(label) * textScale;

            // center the scaled text
            Vector2 textPos = new Vector2(
                x + (iconWidth - textSize.X) / 2f,
                y + (iconHeight - textSize.Y) / 2f
            );

            spriteBatch.DrawString(
                Game1.smallFont,
                label,
                textPos,
                Color.Black,
                rotation: 0f,
                origin: Vector2.Zero,
                scale: textScale,
                effects: SpriteEffects.None,
                layerDepth: 1f
            );

            // tooltip
            Point mouse = Game1.getMousePosition();
            if (this._iconRect.Contains(mouse))
            {
                string tooltip = this.GetBuffTooltip();
                IClickableMenu.drawHoverText(spriteBatch, tooltip, Game1.smallFont);
            }
        }

        private string GetBuffIconLabel()
        {
            switch (this._currentBuffType)
            {
                case WeatherBuffType.Rain:
                    return "Ra";
                case WeatherBuffType.Storm:
                    return "St";
                case WeatherBuffType.SunnySummer:
                    return "Su";
                case WeatherBuffType.Snow:
                    return "Sn";
                case WeatherBuffType.Windy:
                    return "Wi";

                case WeatherBuffType.Blizzard:
                    return "Bl";
                case WeatherBuffType.Deluge:
                    return "De";
                case WeatherBuffType.Drizzle:
                    return "Dz";
                case WeatherBuffType.DryLightning:
                    return "DL";
                case WeatherBuffType.Hailstorm:
                    return "Ha";
                case WeatherBuffType.Heatwave:
                    return "Hw";
                case WeatherBuffType.Mist:
                    return "Mi";
                case WeatherBuffType.MuddyRain:
                    return "MR";
                case WeatherBuffType.RainSnowMix:
                    return "RS";
                case WeatherBuffType.AcidRain:
                    return "Ac";
                case WeatherBuffType.Cloudy:
                    return "Cl";

                default:
                    return "";
            }
        }

        private string GetBuffTooltip()
        {
            switch (this._currentBuffType)
            {
                case WeatherBuffType.Rain:
                    return this.Helper.Translation.Get("tooltip.rain");
                case WeatherBuffType.Storm:
                    return this.Helper.Translation.Get("tooltip.storm");
                case WeatherBuffType.SunnySummer:
                    return this.Helper.Translation.Get("tooltip.sunny");
                case WeatherBuffType.Snow:
                    return this.Helper.Translation.Get("tooltip.snow");
                case WeatherBuffType.Windy:
                    return this.Helper.Translation.Get("tooltip.windy");

                case WeatherBuffType.Blizzard:
                    return this.Helper.Translation.Get("tooltip.blizzard");
                case WeatherBuffType.Deluge:
                    return this.Helper.Translation.Get("tooltip.deluge");
                case WeatherBuffType.Drizzle:
                    return this.Helper.Translation.Get("tooltip.drizzle");
                case WeatherBuffType.DryLightning:
                    return this.Helper.Translation.Get("tooltip.drylightning");
                case WeatherBuffType.Hailstorm:
                    return this.Helper.Translation.Get("tooltip.hailstorm");
                case WeatherBuffType.Heatwave:
                    return this.Helper.Translation.Get("tooltip.heatwave");
                case WeatherBuffType.Mist:
                    return this.Helper.Translation.Get("tooltip.mist");
                case WeatherBuffType.MuddyRain:
                    return this.Helper.Translation.Get("tooltip.muddyrain");
                case WeatherBuffType.RainSnowMix:
                    return this.Helper.Translation.Get("tooltip.rainsnowmix");
                case WeatherBuffType.AcidRain:
                    return this.Helper.Translation.Get("tooltip.acidrain");
                case WeatherBuffType.Cloudy:
                    return this.Helper.Translation.Get("tooltip.cloudy");

                default:
                    return this.Helper.Translation.Get("tooltip.none");
            }
        }
    }

    /// <summary>Config for WeatherBuffs.</summary>
    public class ModConfig
    {
        // Icon toggles + position (unscaled, in UI units; multiplied by UI scale in code)
        public bool ShowIcon { get; set; } = true;
        public int IconX { get; set; } = 8;
        public int IconY { get; set; } = 8;

        // Global multipliers
        public int BuffMultiplierPercent { get; set; } = 100;
        public int DebuffMultiplierPercent { get; set; } = 100;

        // Extra Luck on days with positive Fishing bonus (simulates fish quality boost)
        public int FishingQualityLuckBonus { get; set; } = 1;
    }

    /// <summary>
    /// Save-data used to remember today's Weather Wonders ID.
    /// </summary>
    internal class WeatherCache
    {
        public string TodayWeatherId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Minimal GMCM API interface matching the real signatures.
    /// </summary>
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

        void AddSectionTitle(IManifest mod, Func<string> text, Func<string> tooltip = null);

        void AddBoolOption(
            IManifest mod,
            Func<bool> getValue,
            Action<bool> setValue,
            Func<string> name,
            Func<string> tooltip = null,
            string fieldId = null
        );

        void AddNumberOption(
            IManifest mod,
            Func<int> getValue,
            Action<int> setValue,
            Func<string> name,
            Func<string> tooltip = null,
            int? min = null,
            int? max = null,
            int? interval = null,
            Func<int, string> formatValue = null,
            string fieldId = null
        );
    }
}
