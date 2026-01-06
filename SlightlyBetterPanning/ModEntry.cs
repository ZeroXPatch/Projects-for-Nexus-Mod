using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Tools;

namespace BetterPanning
{
    internal sealed class ModEntry : Mod
    {
        private static ModEntry? Instance { get; set; }

        private readonly Harmony harmony = new("Example.BetterPanning");

        private ModConfig Config = new();

        /// <summary>Stores the debris count in each location right before panning, so we can detect the new loot after panning.</summary>
        private readonly Dictionary<GameLocation, int> panDebrisCount = new();

        /// <summary>Maximum distance (in tiles) at which the HUD indicator will show.</summary>
        private const float MaxIndicatorDistanceTiles = 40f;

        public override void Entry(IModHelper helper)
        {
            Instance = this;

            this.Config = helper.ReadConfig<ModConfig>();

            this.ApplyHarmonyPatches();

            helper.Events.Display.RenderedHud += this.OnRenderedHud;
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        }

        // ----------------
        // GMCM
        // ----------------

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm is null)
                return;

            gmcm.Register(
                this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            gmcm.AddSectionTitle(
                this.ModManifest,
                () => this.Helper.Translation.Get("config.section.main")
            );

            gmcm.AddBoolOption(
                this.ModManifest,
                getValue: () => this.Config.AddExtraTreasure,
                setValue: value => this.Config.AddExtraTreasure = value,
                name: () => this.Helper.Translation.Get("config.extraTreasure.name"),
                tooltip: () => this.Helper.Translation.Get("config.extraTreasure.tooltip")
            );

            gmcm.AddBoolOption(
                this.ModManifest,
                getValue: () => this.Config.ShowIndicator,
                setValue: value => this.Config.ShowIndicator = value,
                name: () => this.Helper.Translation.Get("config.showIndicator.name"),
                tooltip: () => this.Helper.Translation.Get("config.showIndicator.tooltip")
            );
        }

        // ----------------
        // Harmony patch (extra treasure)
        // ----------------

        private void ApplyHarmonyPatches()
        {
            var doFunction = AccessTools.Method(typeof(Pan), "DoFunction");
            if (doFunction is not null)
            {
                this.harmony.Patch(
                    original: doFunction,
                    prefix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.BeforePan)),
                    postfix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.AfterPan))
                );
                this.Monitor.Log("Patched Pan.DoFunction for BetterPanning.", LogLevel.Debug);
            }
            else
            {
                this.Monitor.Log("Failed to find Pan.DoFunction; extra treasure will be disabled.", LogLevel.Warn);
            }
        }

        /// <summary>Before panning, remember how many debris items already existed in this location.</summary>
        private static void BeforePan(Pan __instance)
        {
            if (Instance is null)
                return;

            var location = Game1.currentLocation;
            if (location is null)
                return;

            Instance.panDebrisCount[location] = location.debris.Count;
        }

        /// <summary>After panning, optionally drop one extra copy of the vanilla treasure.</summary>
        private static void AfterPan(Pan __instance)
        {
            if (Instance is null)
                return;

            var location = Game1.currentLocation;
            var who = Game1.player;

            if (location is null || who is null)
                return;

            Instance.HandlePanComplete(location, who);
        }

        private void HandlePanComplete(GameLocation location, Farmer who)
        {
            if (!Context.IsWorldReady)
                return;

            if (!this.Config.AddExtraTreasure)
                return;

            // figure out which debris were spawned by this panning action
            int previousCount = 0;
            this.panDebrisCount.TryGetValue(location, out previousCount);

            if (location.debris.Count <= previousCount)
                return;

            int startIndex = previousCount;
            int endIndexExclusive = location.debris.Count;

            // pick one of the new debris at random
            int pickIndex = Game1.random.Next(startIndex, endIndexExclusive);
            Debris picked = location.debris[pickIndex];

            Item? vanillaItem = picked.item;
            if (vanillaItem is null)
                return;

            Item copy = vanillaItem.getOne();
            Vector2 dropPosition = who.Position;
            Game1.createItemDebris(copy, dropPosition, who.FacingDirection, location);
        }

        // ----------------
        // HUD indicator
        // ----------------

        private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.eventUp)
                return;

            if (!this.Config.ShowIndicator)
                return;

            if (Game1.player?.CurrentTool is not Pan)
                return;

            var location = Game1.player.currentLocation;
            if (location is null)
                return;

            if (!this.TryGetPanTile(location, out Vector2 panTile))
                return;

            Vector2 playerTile = Game1.player.Tile;
            float distance = Vector2.Distance(playerTile, panTile);

            if (distance > MaxIndicatorDistanceTiles)
                return;

            string text = this.Helper.Translation.Get("hud.found");

            float scale = 1f; // simple fixed scale
            SpriteFont font = Game1.smallFont;
            Vector2 size = font.MeasureString(text) * scale;

            Vector2 position = new(Game1.viewport.Width / 2f, 64f * Game1.options.uiScale);
            position.X -= size.X / 2f;

            e.SpriteBatch.DrawString(font, text, position, Color.Black * 0.35f, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
            e.SpriteBatch.DrawString(font, text, position + new Vector2(2f, 2f), Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
        }

        /// <summary>Get the vanilla pan spot tile for the given location, if any.</summary>
        private bool TryGetPanTile(GameLocation location, out Vector2 tile)
        {
            tile = Vector2.Zero;
            if (location is null)
                return false;

            // In 1.6 this is a NetPoint; use .Value to get the XNA Point.
            NetPoint netPoint = location.orePanPoint;
            Point p = netPoint.Value;

            if (p == Point.Zero)
                return false;

            tile = new Vector2(p.X, p.Y);
            return true;
        }
    }

    // ----------------
    // Config + GMCM API
    // ----------------

    internal sealed class ModConfig
    {
        /// <summary>If true, panning drops one extra copy of the vanilla treasure.</summary>
        public bool AddExtraTreasure { get; set; } = true;

        /// <summary>If true, show the HUD message when a pan spot is nearby.</summary>
        public bool ShowIndicator { get; set; } = true;
    }

    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

        void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);

        void AddBoolOption(
            IManifest mod,
            Func<bool> getValue,
            Action<bool> setValue,
            Func<string> name,
            Func<string>? tooltip = null,
            string? fieldId = null
        );
    }
}
