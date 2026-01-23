using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Buildings;
using StardewValley.GameData.Objects;
using StardewValley.Menus;

namespace HorseStarterKit
{
    public class ModEntry : Mod
    {
        // Vanilla IDs
        private const string StableBuildingId = "Stable";

        // Custom IDs
        private const string DeedItemId = "ZeroXPatch.HorseStarterKit.Deed";
        private const string InstallDayKey = "ZeroXPatch.HorseStarterKit/InstallDay";
        private const string MailDay2Id = "ZeroXPatch_HSK_Day2";
        private const string MailDay3Id = "ZeroXPatch_HSK_Day3";

        public override void Entry(IModHelper helper)
        {
            helper.Events.Content.AssetRequested += this.OnAssetRequested;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
        }

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            // 1. Create the "Stable Deed" Item
            if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects"))
            {
                e.Edit(editor =>
                {
                    var data = editor.AsDictionary<string, ObjectData>().Data;
                    data[DeedItemId] = new ObjectData
                    {
                        Name = "Stable Deed",
                        DisplayName = this.Helper.Translation.Get("item.name"),
                        Description = this.Helper.Translation.Get("item.description"),
                        Type = "Basic",
                        Category = StardewValley.Object.junkCategory,
                        Price = 0,
                        Texture = "Maps/springobjects",
                        SpriteIndex = 79, // Looks like a Secret Note
                        ContextTags = new List<string> { "not_placeable", "not_giftable", "trash_minigame_item" }
                    };
                });
            }

            // 2. Add the two Mails
            if (e.NameWithoutLocale.IsEquivalentTo("Data/Mail"))
            {
                e.Edit(editor =>
                {
                    var data = editor.AsDictionary<string, string>().Data;
                    data[MailDay2Id] = this.Helper.Translation.Get("mail.day2");
                    data[MailDay3Id] = this.Helper.Translation.Get("mail.day3");
                });
            }

            // 3. Patch the "Stable" building to be Free and Instant
            if (e.NameWithoutLocale.IsEquivalentTo("Data/Buildings"))
            {
                e.Edit(editor =>
                {
                    var data = editor.AsDictionary<string, BuildingData>().Data;
                    if (data.TryGetValue(StableBuildingId, out var stableData))
                    {
                        stableData.BuildCost = 0;
                        stableData.BuildMaterials = new List<BuildingMaterial>();
                        stableData.BuildDays = 0;
                        stableData.Builder = "Robin";
                    }
                });
            }
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            // 1. Initialize Install Day if missing
            if (!Game1.player.modData.TryGetValue(InstallDayKey, out string? installDayStr))
            {
                Game1.player.modData[InstallDayKey] = Game1.stats.DaysPlayed.ToString();
                this.Monitor.Log($"Mod installed on day {Game1.stats.DaysPlayed}. Progression started.", LogLevel.Info);
                return;
            }

            if (uint.TryParse(installDayStr, out uint installDay))
            {
                // 2. Day 2 Mail (Stable Deed)
                if (Game1.stats.DaysPlayed > installDay)
                {
                    if (!HasMail(MailDay2Id))
                    {
                        Game1.player.mailbox.Add(MailDay2Id);
                        this.Monitor.Log("Day 2 Reached: Sending Stable Deed.", LogLevel.Info);
                    }
                }

                // 3. Day 3 Mail (Horse Flute)
                if (Game1.stats.DaysPlayed > installDay + 1)
                {
                    if (!HasMail(MailDay3Id))
                    {
                        Game1.player.mailbox.Add(MailDay3Id);
                        this.Monitor.Log("Day 3 Reached: Sending Horse Flute.", LogLevel.Info);
                    }
                }
            }
        }

        private bool HasMail(string id)
        {
            return Game1.player.mailReceived.Contains(id) || Game1.player.mailbox.Contains(id);
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsPlayerFree) return;

            if (e.Button.IsActionButton())
            {
                if (Game1.player.CurrentItem != null && Game1.player.CurrentItem.QualifiedItemId == $"(O){DeedItemId}")
                {
                    if (Game1.currentLocation.IsBuildableLocation())
                    {
                        this.Helper.Input.Suppress(e.Button);

                        // 1. Create and Open Robin's Menu
                        var carpenterMenu = new CarpenterMenu("Robin");
                        Game1.activeClickableMenu = carpenterMenu;

                        // 2. Select Stable (Direct Access - No Reflection needed in 1.6)
                        try
                        {
                            // "Blueprints" is a public List<BlueprintEntry> in 1.6+
                            foreach (var bp in carpenterMenu.Blueprints)
                            {
                                if (bp.Id == StableBuildingId)
                                {
                                    // "SetNewActiveBlueprint" is a public method in 1.6+
                                    carpenterMenu.SetNewActiveBlueprint(bp);
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // Just log warning; menu still opens, user just has to click "Stable" manually
                            this.Monitor.Log($"Failed to auto-select Stable: {ex.Message}", LogLevel.Warn);
                        }

                        // 3. Consume the Deed
                        Game1.player.reduceActiveItemByOne();
                    }
                    else
                    {
                        Game1.addHUDMessage(new HUDMessage("You can only use the Deed on your Farm!", 3));
                    }
                }
            }
        }
    }
}