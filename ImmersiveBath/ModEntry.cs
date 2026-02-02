using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buffs;
using StardewValley.Menus;
using StardewValley.Tools;

namespace ImmersiveBath
{
    public class ModEntry : Mod
    {
        private ModConfig Config = null!;
        private float Cleanliness = 100f;
        private string CurrentState = "Clean";
        private bool WasRevitalizedEligible = false;
        private string LastTalkedNPC = "";
        private Dictionary<NPC, EmoteDisplay> ActiveEmotes = new Dictionary<NPC, EmoteDisplay>();
        private Texture2D? SickEmoteTexture = null;

        private class EmoteDisplay
        {
            public float Timer;
            public float StartY;
            public float CurrentY;
            public const float Duration = 2.0f; // 2 seconds
            public const float RiseDistance = 64f; // pixels to rise
        }

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.TimeChanged += OnTimeChanged;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.Content.AssetRequested += OnAssetRequested;
            helper.Events.Display.RenderedHud += OnRenderedHud;
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
            helper.Events.Display.MenuChanged += OnMenuChanged;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            configMenu.Register(ModManifest, () => Config = new ModConfig(), () => Helper.WriteConfig(Config));

            // --- GENERAL SECTION ---
            configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.section.general"));

            configMenu.AddBoolOption(ModManifest, () => Config.BathAnywhere, (v) => Config.BathAnywhere = v,
                () => Helper.Translation.Get("config.bath-anywhere.name"),
                () => Helper.Translation.Get("config.bath-anywhere.tooltip"));

            // --- UI SECTION (Integers) ---
            configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.section.ui"));

            configMenu.AddBoolOption(ModManifest, () => Config.ShowUI, (v) => Config.ShowUI = v, () => Helper.Translation.Get("config.show-ui.name"));

            configMenu.AddNumberOption(ModManifest, () => Config.UI_X, (v) => Config.UI_X = v, () => Helper.Translation.Get("config.ui-x.name"), min: 0, max: 2560);
            configMenu.AddNumberOption(ModManifest, () => Config.UI_Y, (v) => Config.UI_Y = v, () => Helper.Translation.Get("config.ui-y.name"), min: 0, max: 1440);

            // --- DECAY SECTION (Floats - Notice the 'f' after numbers) ---
            configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.section.decay"));

            configMenu.AddNumberOption(ModManifest, () => Config.DecayMultiplier, (v) => Config.DecayMultiplier = v, () => Helper.Translation.Get("config.decay-rate.name"), min: 0.1f, max: 5.0f, interval: 0.1f);

            configMenu.AddNumberOption(ModManifest, () => Config.ToolUseDecay, (v) => Config.ToolUseDecay = v, () => Helper.Translation.Get("config.tool-decay.name"), min: 0.0f, max: 2.0f, interval: 0.1f);

            // --- CLEAN BUFFS (Integers) ---
            configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.section.clean_buff"));

            configMenu.AddNumberOption(ModManifest, () => Config.CleanLuckBuff, (v) => Config.CleanLuckBuff = v, () => Helper.Translation.Get("config.luck-buff.name"), min: 0, max: 10);

            configMenu.AddNumberOption(ModManifest, () => Config.CleanFriendshipBonus, (v) => Config.CleanFriendshipBonus = v, () => Helper.Translation.Get("config.friendship.name"), min: 0, max: 50);

            // --- DIRTY BUFFS (Integers) ---
            configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.section.dirty_buff"));

            configMenu.AddNumberOption(ModManifest, () => Config.DirtySpeedBuff, (v) => Config.DirtySpeedBuff = v, () => Helper.Translation.Get("config.speed-buff.name"), min: 0, max: 5);

            configMenu.AddNumberOption(ModManifest, () => Config.DirtyDefenseBuff, (v) => Config.DirtyDefenseBuff = v, () => Helper.Translation.Get("config.defense-buff.name"), min: 0, max: 10);

            configMenu.AddNumberOption(ModManifest, () => Config.DirtyAttackBuff, (v) => Config.DirtyAttackBuff = v, () => Helper.Translation.Get("config.attack-buff.name"), min: 0, max: 10);

            configMenu.AddNumberOption(ModManifest, () => Config.DirtyFriendshipPenalty, (v) => Config.DirtyFriendshipPenalty = v, () => Helper.Translation.Get("config.dirty-friendship.name"), min: -50, max: 0);

            // --- REVITALIZED (Integers) ---
            configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("config.section.revitalized"));

            configMenu.AddNumberOption(ModManifest, () => Config.RevitalizedMaxEnergy, (v) => Config.RevitalizedMaxEnergy = v, () => Helper.Translation.Get("config.energy-buff.name"), min: 0, max: 200);
        }

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects"))
            {
                e.Edit(asset => {
                    var data = asset.AsDictionary<string, StardewValley.GameData.Objects.ObjectData>().Data;
                    data["ZeroXPatch.Soap"] = new StardewValley.GameData.Objects.ObjectData
                    {
                        Name = "Soap",
                        DisplayName = Helper.Translation.Get("item.soap.name"),
                        Description = Helper.Translation.Get("item.soap.description"),
                        Type = "Basic",
                        Category = StardewValley.Object.baitCategory,
                        Price = 10,
                        Texture = "ZeroXPatch.ImmersiveBath/Soap"
                    };
                });
            }
            if (e.NameWithoutLocale.IsEquivalentTo("Data/Tools"))
            {
                e.Edit(asset => {
                    var data = asset.AsDictionary<string, StardewValley.GameData.Tools.ToolData>().Data;
                    data["ZeroXPatch.BathSponge"] = new StardewValley.GameData.Tools.ToolData
                    {
                        ClassName = "GenericTool",
                        Name = "Bath Sponge",
                        DisplayName = Helper.Translation.Get("item.sponge.name"),
                        Description = Helper.Translation.Get("item.sponge.description"),
                        Texture = "ZeroXPatch.ImmersiveBath/Sponge",
                        AttachmentSlots = 1,
                        SalePrice = 50
                    };
                });
            }
            if (e.NameWithoutLocale.IsEquivalentTo("ZeroXPatch.ImmersiveBath/Sponge")) e.LoadFromModFile<Texture2D>("assets/sponge.png", AssetLoadPriority.Medium);
            if (e.NameWithoutLocale.IsEquivalentTo("ZeroXPatch.ImmersiveBath/Soap")) e.LoadFromModFile<Texture2D>("assets/soap.png", AssetLoadPriority.Medium);
            if (e.NameWithoutLocale.IsEquivalentTo("ZeroXPatch.ImmersiveBath/FriendshipDebuff")) e.LoadFromModFile<Texture2D>("assets/sick_emote.png", AssetLoadPriority.Medium);
            if (e.NameWithoutLocale.IsEquivalentTo("ZeroXPatch.ImmersiveBath/HeartBuff")) e.LoadFromModFile<Texture2D>("assets/heart_buff.png", AssetLoadPriority.Medium);

            if (e.NameWithoutLocale.IsEquivalentTo("Data/Shops"))
            {
                e.Edit(asset => {
                    var shops = asset.AsDictionary<string, StardewValley.GameData.Shops.ShopData>().Data;
                    if (shops.TryGetValue("SeedShop", out var pierre))
                    {
                        pierre.Items.Add(new StardewValley.GameData.Shops.ShopItemData { Id = "ZeroXPatch.Soap", ItemId = "(O)ZeroXPatch.Soap", Price = 10 });
                        pierre.Items.Add(new StardewValley.GameData.Shops.ShopItemData { Id = "ZeroXPatch.BathSponge", ItemId = "(T)ZeroXPatch.BathSponge", Price = 50 });
                    }
                });
            }
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsPlayerFree) return;
            if (e.Button.IsActionButton() && Game1.player.CurrentTool?.QualifiedItemId == "(T)ZeroXPatch.BathSponge")
            {
                if (Config.BathAnywhere || IsNearWater(Game1.player))
                {
                    Helper.Input.Suppress(e.Button);
                    Response[] responses = { new Response("Yes", Helper.Translation.Get("dialog.yes")), new Response("No", Helper.Translation.Get("dialog.no")) };
                    Game1.currentLocation.createQuestionDialogue(Helper.Translation.Get("dialog.title"), responses, (farmer, answer) => { if (answer == "Yes") TakeBath(); });
                }
            }
            if (e.Button.IsUseToolButton() && Game1.player.CurrentTool != null && Game1.player.CurrentTool.QualifiedItemId != "(T)ZeroXPatch.BathSponge")
                Cleanliness = Math.Max(0, Cleanliness - Config.ToolUseDecay);
        }

        private bool IsNearWater(Farmer f)
        {
            Vector2[] tiles = { f.Tile, f.Tile + new Vector2(1, 0), f.Tile + new Vector2(-1, 0), f.Tile + new Vector2(0, 1), f.Tile + new Vector2(0, -1), f.GetToolLocation() / 64f };
            foreach (var pos in tiles)
            {
                if (f.currentLocation.CanRefillWateringCanOnTile((int)pos.X, (int)pos.Y)) return true;
                if (f.currentLocation is StardewValley.Locations.BathHousePool) return true;
            }
            return false;
        }

        private void TakeBath()
        {
            Tool sponge = Game1.player.CurrentTool;
            bool hasSoap = (sponge?.attachments != null && sponge.attachments.Length > 0 && sponge.attachments[0]?.QualifiedItemId == "(O)ZeroXPatch.Soap");

            if (!hasSoap && Cleanliness >= 70f) return;

            Game1.player.CanMove = false;
            Game1.globalFadeToBlack(() => {
                if (hasSoap)
                {
                    Cleanliness = 100f;
                    sponge.attachments[0].Stack--;
                    if (sponge.attachments[0].Stack <= 0) sponge.attachments[0] = null;
                }
                else
                {
                    Cleanliness = 70f;
                }
                if (Game1.timeOfDay >= 1800 && hasSoap) WasRevitalizedEligible = true;

                UpdateBuffs(true);
                Game1.player.CanMove = true;
                Game1.globalFadeToClear();
                Game1.addHUDMessage(new HUDMessage(Helper.Translation.Get(hasSoap ? "notification.clean" : "notification.partial"), 2));
            });
        }

        private void OnTimeChanged(object? sender, TimeChangedEventArgs e) { Cleanliness = Math.Max(0, Cleanliness - (1.66f * Config.DecayMultiplier)); UpdateBuffs(); }

        private void UpdateBuffs(bool suppress = false)
        {
            string newState = Cleanliness >= 80 ? "Clean" : (Cleanliness >= 40 ? "Neutral" : "Dirty");
            if (newState != CurrentState)
            {
                CurrentState = newState;
                if (!suppress) Game1.addHUDMessage(new HUDMessage(Helper.Translation.Get($"notification.{newState.ToLower()}"), 2));
            }
            Game1.player.buffs.Remove("ZeroXPatch.CleanBuff");
            Game1.player.buffs.Remove("ZeroXPatch.DirtyBuff");
            Game1.player.buffs.Remove("ZeroXPatch.FriendshipDebuff");

            if (CurrentState == "Clean")
            {
                Game1.player.buffs.Apply(new Buff(
                    id: "ZeroXPatch.CleanBuff",
                    displayName: Helper.Translation.Get("buff.clean.name"),
                    iconTexture: Helper.GameContent.Load<Texture2D>("ZeroXPatch.ImmersiveBath/HeartBuff"),
                    effects: new BuffEffects() { LuckLevel = { Config.CleanLuckBuff } },
                    duration: 60000,
                    description: Helper.Translation.Get("buff.clean.description")
                ));
            }
            else if (CurrentState == "Dirty")
            {
                Game1.player.buffs.Apply(new Buff(id: "ZeroXPatch.DirtyBuff", displayName: Helper.Translation.Get("buff.dirty.name"), effects: new BuffEffects() { Speed = { Config.DirtySpeedBuff }, Defense = { Config.DirtyDefenseBuff }, Attack = { Config.DirtyAttackBuff } }, duration: 60000));

                // Add Friendship Debuff with custom icon
                Game1.player.buffs.Apply(new Buff(
                    id: "ZeroXPatch.FriendshipDebuff",
                    displayName: Helper.Translation.Get("buff.friendshipdebuff.name"),
                    iconTexture: Helper.GameContent.Load<Texture2D>("ZeroXPatch.ImmersiveBath/FriendshipDebuff"),
                    duration: 60000,
                    description: Helper.Translation.Get("buff.friendshipdebuff.description")
                ));
            }
        }

        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            if (e.NewMenu is DialogueBox)
            {
                NPC s = Game1.currentSpeaker;
                if (s != null && LastTalkedNPC != s.Name)
                {
                    if (CurrentState == "Clean")
                    {
                        Game1.player.changeFriendship(Config.CleanFriendshipBonus, s);
                    }
                    else if (CurrentState == "Dirty")
                    {
                        Game1.player.changeFriendship(Config.DirtyFriendshipPenalty, s);
                        // Show angry/frustrated emote above NPC
                        s.doEmote(12);
                    }
                    LastTalkedNPC = s.Name;
                }
            }
            if (e.NewMenu == null) LastTalkedNPC = "";
        }

        private void ShowSickEmote(NPC npc)
        {
            if (SickEmoteTexture == null)
            {
                try
                {
                    SickEmoteTexture = Helper.GameContent.Load<Texture2D>("ZeroXPatch.ImmersiveBath/SickEmote");
                }
                catch
                {
                    Monitor.Log("Failed to load sick emote texture. Make sure assets/sick_emote.png exists.", LogLevel.Warn);
                    return;
                }
            }

            var emote = new EmoteDisplay
            {
                Timer = 0f,
                StartY = npc.Position.Y - 96f, // Start above NPC's head
                CurrentY = npc.Position.Y - 96f
            };
            ActiveEmotes[npc] = emote;
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            // Update emote animations
            var toRemove = new List<NPC>();
            foreach (var kvp in ActiveEmotes)
            {
                var emote = kvp.Value;
                emote.Timer += (float)Game1.currentGameTime.ElapsedGameTime.TotalSeconds;

                if (emote.Timer >= EmoteDisplay.Duration)
                {
                    toRemove.Add(kvp.Key);
                }
                else
                {
                    // Rise up slowly and fade
                    float progress = emote.Timer / EmoteDisplay.Duration;
                    emote.CurrentY = emote.StartY - (EmoteDisplay.RiseDistance * progress);
                }
            }

            foreach (var npc in toRemove)
            {
                ActiveEmotes.Remove(npc);
            }
        }

        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (!Context.IsWorldReady || SickEmoteTexture == null) return;

            foreach (var kvp in ActiveEmotes)
            {
                var npc = kvp.Key;
                var emote = kvp.Value;

                // Calculate opacity (fade out as it rises)
                float progress = emote.Timer / EmoteDisplay.Duration;
                float opacity = 1f - progress;

                // Get screen position
                Vector2 npcScreenPos = Game1.GlobalToLocal(Game1.viewport, npc.Position);
                Vector2 emoteScreenPos = new Vector2(
                    npcScreenPos.X + 16f, // Center on NPC (32px sprite / 2 - 28px emote / 2)
                    emote.CurrentY - Game1.viewport.Y
                );

                // Draw the emote
                e.SpriteBatch.Draw(
                    SickEmoteTexture,
                    emoteScreenPos,
                    null,
                    Color.White * opacity,
                    0f,
                    Vector2.Zero,
                    2f, // Scale 2x (56x56 pixels at 2x scale)
                    SpriteEffects.None,
                    1f // Draw on top layer
                );
            }
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (WasRevitalizedEligible) Game1.player.buffs.Apply(new Buff(id: "ZeroXPatch.Revitalized", displayName: Helper.Translation.Get("buff.revitalized.name"), effects: new BuffEffects() { MaxStamina = { Config.RevitalizedMaxEnergy } }, duration: -2));
            WasRevitalizedEligible = false;
            ActiveEmotes.Clear(); // Clear any leftover emotes
        }

        private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
        {
            if (!Config.ShowUI || !Context.IsWorldReady || Game1.activeClickableMenu != null) return;
            string state = Helper.Translation.Get($"buff.{CurrentState.ToLower()}.name");
            string text = $"{state}: {(int)Cleanliness}%";
            Vector2 pos = new Vector2(Config.UI_X, Config.UI_Y);
            e.SpriteBatch.DrawString(Game1.smallFont, text, pos + new Vector2(2, 2), Color.Black * 0.5f);
            e.SpriteBatch.DrawString(Game1.smallFont, text, pos, Color.SkyBlue);
        }
    }
}