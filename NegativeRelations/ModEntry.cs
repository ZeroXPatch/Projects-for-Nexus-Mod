using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace NegativeRelations
{
    public class ModEntry : Mod
    {
        private ModConfig Config = new();
        private readonly Dictionary<string, double> barkCooldowns = new();
        private readonly Dictionary<string, int> barkCooldownUntil = new();
        private List<(NPC npc, Rectangle bounds)>? cachedSocialEntries;
        private int cachedSocialTabIndex = -1;
        private bool cachedSocialForMenu;
        private readonly Random random = new();
        private bool suppressDialogueOverride;

        private List<string> talkTier1Lines = new();
        private List<string> talkTier2Lines = new();
        private List<string> talkTier3Lines = new();
        private List<string> barkTier2Lines = new();
        private List<string> barkTier3Lines = new();

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.Player.GiftGiven += this.OnGiftGiven;
            helper.Events.Display.MenuChanged += this.OnMenuChanged;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Display.RenderedActiveMenu += this.OnRenderedActiveMenu;

            this.LoadDialogueLines();
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm is null)
            {
                return;
            }

            gmcm.Register(this.ModManifest, () => this.Config = new ModConfig(), () => this.Helper.WriteConfig(this.Config));

            gmcm.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("gmcm.section.general"));
            gmcm.AddBoolOption(
                this.ModManifest,
                () => this.Config.EnableMod,
                value => this.Config.EnableMod = value,
                () => this.Helper.Translation.Get("gmcm.option.enableMod.name"),
                () => this.Helper.Translation.Get("gmcm.option.enableMod.desc")
            );
            gmcm.AddNumberOption(
                this.ModManifest,
                () => this.Config.RecoveryPerDay,
                value => this.Config.RecoveryPerDay = value,
                () => this.Helper.Translation.Get("gmcm.option.recovery.name"),
                () => this.Helper.Translation.Get("gmcm.option.recovery.desc"),
                min: 0,
                max: 50
            );

            gmcm.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("gmcm.section.dialogue"));
            gmcm.AddBoolOption(
                this.ModManifest,
                () => this.Config.EnableTalkOverride,
                value => this.Config.EnableTalkOverride = value,
                () => this.Helper.Translation.Get("gmcm.option.talkOverride.name"),
                () => this.Helper.Translation.Get("gmcm.option.talkOverride.desc")
            );
            gmcm.AddNumberOption(
                this.ModManifest,
                () => this.Config.TalkOverrideChance,
                value => this.Config.TalkOverrideChance = value,
                () => this.Helper.Translation.Get("gmcm.option.talkChance.name"),
                () => this.Helper.Translation.Get("gmcm.option.talkChance.desc"),
                min: 0f,
                max: 1f,
                interval: 0.01f,
                formatValue: val => $"{val:P0}"
            );

            gmcm.AddSectionTitle(this.ModManifest, () => this.Helper.Translation.Get("gmcm.section.barks"));
            gmcm.AddBoolOption(
                this.ModManifest,
                () => this.Config.EnableBarks,
                value => this.Config.EnableBarks = value,
                () => this.Helper.Translation.Get("gmcm.option.barks.name"),
                () => this.Helper.Translation.Get("gmcm.option.barks.desc")
            );
            gmcm.AddNumberOption(
                this.ModManifest,
                () => this.Config.BarkRadiusTiles,
                value => this.Config.BarkRadiusTiles = value,
                () => this.Helper.Translation.Get("gmcm.option.barkRadius.name"),
                () => this.Helper.Translation.Get("gmcm.option.barkRadius.desc"),
                min: 1,
                max: 8
            );
            gmcm.AddNumberOption(
                this.ModManifest,
                () => this.Config.BarkChance,
                value => this.Config.BarkChance = value,
                () => this.Helper.Translation.Get("gmcm.option.barkChance.name"),
                () => this.Helper.Translation.Get("gmcm.option.barkChance.desc"),
                min: 0f,
                max: 1f,
                interval: 0.01f,
                formatValue: val => $"{val:P0}"
            );
            gmcm.AddNumberOption(
                this.ModManifest,
                () => this.Config.BarkCooldownMinutes,
                value => this.Config.BarkCooldownMinutes = value,
                () => this.Helper.Translation.Get("gmcm.option.barkCooldown.name"),
                () => this.Helper.Translation.Get("gmcm.option.barkCooldown.desc"),
                min: 1,
                max: 30
            );
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (!Context.IsWorldReady || !this.Config.EnableMod)
            {
                return;
            }

            var player = Game1.player;
            var keys = player.modData.Keys.Where(key => key.StartsWith(this.GetNegKeyPrefix(), StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (string key in keys)
            {
                string npcName = key.Substring(this.GetNegKeyPrefix().Length);
                int current = this.GetNegPoints(npcName);
                if (current < 0 && this.Config.RecoveryPerDay > 0)
                {
                    int recovered = Math.Min(0, current + this.Config.RecoveryPerDay);
                    this.SetNegPoints(npcName, recovered);
                }
            }

            this.barkCooldowns.Clear();
        }

        private void OnGiftGiven(object? sender, GiftGivenEventArgs e)
        {
            if (!Context.IsWorldReady || !this.Config.EnableMod)
            {
                return;
            }

            if (e.Npc is null || e.Gift is null)
            {
                return;
            }

            if (this.IsFestivalOrEvent())
            {
                return;
            }

            GiftTaste taste = e.Npc.getGiftTasteForThisItem(e.Gift);

            int delta = 0;
            if (taste == GiftTaste.Dislike)
            {
                delta = -50;
            }
            else if (taste == GiftTaste.Hate)
            {
                delta = -200;
            }

            if (delta < 0)
            {
                string npcName = e.Npc.Name;
                int updated = this.GetNegPoints(npcName) + delta;
                this.SetNegPoints(npcName, updated);
            }
        }

        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            if (this.suppressDialogueOverride)
            {
                this.suppressDialogueOverride = false;
                return;
            }

            this.cachedSocialEntries = null;
            this.cachedSocialForMenu = false;
            this.cachedSocialTabIndex = -1;

            if (!Context.IsWorldReady || !this.Config.EnableMod || !this.Config.EnableTalkOverride)
            {
                return;
            }

            if (this.IsFestivalOrEvent())
            {
                return;
            }

            if (e.NewMenu is not StardewValley.Menus.DialogueBox || Game1.currentSpeaker is not NPC npc)
            {
                return;
            }

            int tier = this.GetTier(this.GetNegPoints(npc.Name));
            if (tier <= 0)
            {
                return;
            }

            if (this.random.NextDouble() > this.Config.TalkOverrideChance)
            {
                return;
            }

            string? line = this.GetTalkLineForTier(tier);
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            this.suppressDialogueOverride = true;
            Game1.drawDialogue(npc, line);
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || !this.Config.EnableMod || !this.Config.EnableBarks)
            {
                return;
            }

            if (!e.IsMultipleOf(30))
            {
                return;
            }

            if (this.IsFestivalOrEvent() || Game1.activeClickableMenu is not null)
            {
                return;
            }

            this.TryDoBarks();
        }

        private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
        {
            if (!Context.IsWorldReady || !this.Config.EnableMod)
            {
                return;
            }

            if (this.IsFestivalOrEvent())
            {
                return;
            }

            if (Game1.activeClickableMenu is not GameMenu gameMenu)
            {
                return;
            }

            IClickableMenu? currentPage = this.GetCurrentGameMenuPage(gameMenu);
            if (currentPage is not SocialPage socialPage)
            {
                return;
            }

            this.DrawNegativeHearts(socialPage, e.SpriteBatch);
        }

        private void TryDoBarks()
        {
            if (Game1.currentLocation is null || Game1.player is null)
            {
                return;
            }

            var player = Game1.player;
            Vector2 playerTile = player.TilePoint.ToVector2();
            int nowGameMinutes = this.GetAbsoluteGameMinutes();

            foreach (NPC npc in Game1.currentLocation.characters.OfType<NPC>())
            {
                if (npc.IsInvisible || !npc.isVillager())
                {
                    continue;
                }

                int tier = this.GetTier(this.GetNegPoints(npc.Name));
                if (tier < 2)
                {
                    continue;
                }

                if (!Utility.isOnScreen(npc.Position, 256))
                {
                    continue;
                }

                double distance = Vector2.Distance(npc.TilePoint.ToVector2(), playerTile);
                if (distance > this.Config.BarkRadiusTiles)
                {
                    continue;
                }

                if (this.barkCooldownUntil.TryGetValue(npc.Name, out int until) && nowGameMinutes < until)
                {
                    continue;
                }

                if (this.random.NextDouble() > this.Config.BarkChance)
                {
                    continue;
                }

                string? line = this.GetBarkLineForTier(tier);
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                npc.showTextAboveHead(line);
                int cooldownMinutes = this.Config.BarkCooldownMinutes;
                this.barkCooldownUntil[npc.Name] = nowGameMinutes + cooldownMinutes;
            }
        }

        private string? GetTalkLineForTier(int tier)
        {
            return tier switch
            {
                1 => this.PickRandomLine(this.talkTier1Lines),
                2 => this.PickRandomLine(this.talkTier2Lines),
                _ => this.PickRandomLine(this.talkTier3Lines)
            };
        }

        private string? GetBarkLineForTier(int tier)
        {
            return tier >= 3 ? this.PickRandomLine(this.barkTier3Lines) : this.PickRandomLine(this.barkTier2Lines);
        }

        private string? PickRandomLine(IReadOnlyList<string> lines)
        {
            if (lines.Count == 0)
            {
                return null;
            }

            int index = this.random.Next(lines.Count);
            return lines[index];
        }

        public int GetNegPoints(string npcName)
        {
            if (!Context.IsWorldReady || string.IsNullOrWhiteSpace(npcName))
            {
                return 0;
            }

            var modData = Game1.player.modData;
            string key = this.GetNegKey(npcName);
            if (modData.TryGetValue(key, out string? raw) && int.TryParse(raw, out int parsed))
            {
                return Math.Clamp(parsed, -2500, 0);
            }

            return 0;
        }

        public void SetNegPoints(string npcName, int value)
        {
            if (!Context.IsWorldReady || string.IsNullOrWhiteSpace(npcName))
            {
                return;
            }

            int clamped = Math.Clamp(value, -2500, 0);
            Game1.player.modData[this.GetNegKey(npcName)] = clamped.ToString();
        }

        public int GetTier(int negPoints)
        {
            if (negPoints == 0)
            {
                return 0;
            }

            if (negPoints <= -1000)
            {
                return 3;
            }

            if (negPoints <= -250)
            {
                return 2;
            }

            return 1;
        }

        private string GetNegKey(string npcName) => $"{this.ModManifest.UniqueID}/negPoints/{npcName}";

        private string GetNegKeyPrefix() => $"{this.ModManifest.UniqueID}/negPoints/";

        private bool IsFestivalOrEvent()
        {
            if (Game1.eventUp)
            {
                return true;
            }

            return Game1.currentLocation?.currentEvent?.isFestival == true;
        }

        private IClickableMenu? GetCurrentGameMenuPage(GameMenu menu)
        {
            var pagesField = this.Helper.Reflection.GetField<List<IClickableMenu>>(menu, "pages", required: false);
            List<IClickableMenu>? pages = pagesField?.GetValue();
            if (pages is null || menu.currentTab < 0 || menu.currentTab >= pages.Count)
            {
                return null;
            }

            return pages[menu.currentTab];
        }

        private void DrawNegativeHearts(SocialPage socialPage, SpriteBatch spriteBatch)
        {
            const int pointsPerHeart = 250;
            const float heartScale = 4f;
            const int heartSpriteWidth = 7;
            const int heartSpriteHeight = 6;
            const float heartSpacing = heartSpriteWidth * heartScale + 2f;

            foreach ((NPC npc, Rectangle bounds) in this.GetSocialEntriesCached(socialPage))
            {
                int negPoints = this.GetNegPoints(npc.Name);
                if (negPoints >= 0)
                {
                    continue;
                }

                int heartCount = Math.Min(10, (int)Math.Ceiling(Math.Abs(negPoints) / (float)pointsPerHeart));
                if (heartCount <= 0)
                {
                    continue;
                }

                float startX = bounds.Right - (heartSpacing * heartCount) - 16f;
                float startY = bounds.Top + (bounds.Height / 2f) - (heartSpriteHeight * heartScale / 2f);
                Rectangle heartSource = new(211, 428, heartSpriteWidth, heartSpriteHeight);

                for (int i = 0; i < heartCount; i++)
                {
                    Vector2 position = new(startX + i * heartSpacing, startY);
                    spriteBatch.Draw(Game1.mouseCursors, position, heartSource, Color.Black, 0f, Vector2.Zero, heartScale, SpriteEffects.None, 1f);
                }
            }
        }

        private IEnumerable<(NPC npc, Rectangle bounds)> GetSocialEntriesCached(SocialPage socialPage)
        {
            if (this.cachedSocialEntries is not null && this.cachedSocialForMenu && this.cachedSocialTabIndex == socialPage.currentTab)
            {
                return this.cachedSocialEntries;
            }

            this.cachedSocialEntries = this.BuildSocialEntries(socialPage).ToList();
            this.cachedSocialForMenu = true;
            this.cachedSocialTabIndex = socialPage.currentTab;
            return this.cachedSocialEntries;
        }

        private IEnumerable<(NPC npc, Rectangle bounds)> BuildSocialEntries(SocialPage socialPage)
        {
            List<(NPC, Rectangle)> results = new();
            foreach (string fieldName in this.GetSocialEntryFieldOrder(socialPage))
            {
                var field = this.Helper.Reflection.GetField<IEnumerable<object>>(socialPage, fieldName, required: false);
                IEnumerable<object>? entries = field?.GetValue();
                if (entries is null)
                {
                    continue;
                }

                foreach (object entry in entries)
                {
                    NPC? npc = this.Helper.Reflection.GetProperty<NPC?>(entry, "Character", required: false)?.GetValue()
                        ?? this.Helper.Reflection.GetField<NPC?>(entry, "character", required: false)?.GetValue()
                        ?? this.Helper.Reflection.GetProperty<NPC?>(entry, "npc", required: false)?.GetValue();
                    Rectangle bounds = this.Helper.Reflection.GetProperty<Rectangle>(entry, "Bounds", required: false)?.GetValue()
                        ?? this.Helper.Reflection.GetField<Rectangle>(entry, "bounds", required: false)?.GetValue()
                        ?? Rectangle.Empty;

                    if (npc is not null && bounds != Rectangle.Empty)
                    {
                        results.Add((npc, bounds));
                    }
                }

                if (results.Count > 0)
                {
                    break;
                }
            }

            return results;
        }

        private IEnumerable<string> GetSocialEntryFieldOrder(SocialPage page)
        {
            yield return "socialEntries";
            yield return "_socialEntries";
            yield return "friends";
            yield return "slots";
        }

        private int GetAbsoluteGameMinutes()
        {
            int days = Game1.Date.TotalDays;
            int hour = Game1.timeOfDay / 100;
            int minute = Game1.timeOfDay % 100;
            return days * 1440 + (hour * 60) + minute;
        }

        private void LoadDialogueLines()
        {
            this.talkTier1Lines = this.ReadLines(
                "talk.tier1",
                "line1",
                "line2",
                "line3",
                "line4",
                "line5"
            );
            this.talkTier2Lines = this.ReadLines(
                "talk.tier2",
                "line1",
                "line2",
                "line3",
                "line4",
                "line5"
            );
            this.talkTier3Lines = this.ReadLines(
                "talk.tier3",
                "line1",
                "line2",
                "line3",
                "line4",
                "line5"
            );
            this.barkTier2Lines = this.ReadLines(
                "bark.tier2",
                "line1",
                "line2",
                "line3",
                "line4",
                "line5"
            );
            this.barkTier3Lines = this.ReadLines(
                "bark.tier3",
                "line1",
                "line2",
                "line3",
                "line4",
                "line5"
            );
        }

        private List<string> ReadLines(string prefix, params string[] keys)
        {
            List<string> lines = new();
            foreach (string key in keys)
            {
                string text = this.Helper.Translation.Get($"{prefix}.{key}");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    lines.Add(text);
                }
            }

            return lines;
        }
    }
}
