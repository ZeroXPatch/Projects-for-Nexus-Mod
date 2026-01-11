using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Buildings;
using StardewValley.GameData.Objects;
using StardewValley.Menus;

namespace TractorDelivery
{
    public class ModEntry : Mod
    {
        private const string TractorBuildingId = "Pathoschild.TractorMod_Stable";
        private const string KitItemId = "TractorDelivery.StarterKit";
        private const string MailId = "TractorDelivery.WelcomeMail";

        public override void Entry(IModHelper helper)
        {
            helper.Events.Content.AssetRequested += this.OnAssetRequested;
            helper.Events.GameLoop.DayEnding += this.OnDayEnding;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
        }

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            // 1. Define the Tractor Kit Item (Now looking like Joja Cola)
            if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects"))
            {
                e.Edit(editor =>
                {
                    var data = editor.AsDictionary<string, ObjectData>().Data;
                    data[KitItemId] = new ObjectData
                    {
                        Name = "Tractor Deed", // Internal name
                        DisplayName = this.Helper.Translation.Get("item.name"), // From i18n
                        Description = this.Helper.Translation.Get("item.description"), // From i18n
                        Type = "Basic",
                        Category = StardewValley.Object.junkCategory,
                        Price = 500,
                        Texture = "Maps/springobjects",
                        SpriteIndex = 167, // <--- 167 is the standard Joja Cola icon
                        ContextTags = new List<string> { "not_placeable", "not_giftable" }
                    };
                });
            }

            // 2. Define the Mail (Text from i18n)
            if (e.NameWithoutLocale.IsEquivalentTo("Data/Mail"))
            {
                e.Edit(editor =>
                {
                    var data = editor.AsDictionary<string, string>().Data;

                    // Get text from translation file
                    string mailText = this.Helper.Translation.Get("mail.content");

                    // Programmatically append the item attachment code.
                    // This prevents the translator from accidentally deleting the code that gives the item.
                    mailText += $" %item object {KitItemId} 1 %%";

                    data[MailId] = mailText;
                });
            }

            // 3. Patch the Building Data to be Free and Instant
            if (e.NameWithoutLocale.IsEquivalentTo("Data/Buildings"))
            {
                e.Edit(editor =>
                {
                    var data = editor.AsDictionary<string, BuildingData>().Data;
                    if (data.TryGetValue(TractorBuildingId, out var tractorBuilding))
                    {
                        tractorBuilding.BuildCost = 0;
                        tractorBuilding.BuildMaterials = new List<BuildingMaterial>();
                        tractorBuilding.BuildDays = 0;
                        tractorBuilding.Builder = "Robin";
                    }
                });
            }
        }

        private void OnDayEnding(object? sender, DayEndingEventArgs e)
        {
            if (!Game1.player.mailReceived.Contains(MailId) && !Game1.player.mailbox.Contains(MailId))
            {
                Game1.addMailForTomorrow(MailId);
            }
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsPlayerFree) return;

            if (e.Button.IsActionButton())
            {
                if (Game1.player.CurrentItem != null && Game1.player.CurrentItem.QualifiedItemId == $"(O){KitItemId}")
                {
                    if (Game1.currentLocation.IsBuildableLocation())
                    {
                        this.Helper.Input.Suppress(e.Button);

                        // Open Robin's Menu
                        var carpenterMenu = new CarpenterMenu("Robin");
                        Game1.activeClickableMenu = carpenterMenu;

                        // Try to select the Tractor (Safe Mode)
                        try
                        {
                            var blueprints = this.Helper.Reflection.GetField<List<object>>(carpenterMenu, "Blueprints").GetValue();

                            object? tractorBlueprint = null;
                            foreach (var bp in blueprints)
                            {
                                var id = this.Helper.Reflection.GetProperty<string>(bp, "Id").GetValue();
                                if (id == TractorBuildingId)
                                {
                                    tractorBlueprint = bp;
                                    break;
                                }
                            }

                            if (tractorBlueprint != null)
                            {
                                // "SetNewActiveBlueprint" is the standard name in 1.6+ (PascalCase)
                                this.Helper.Reflection.GetMethod(carpenterMenu, "SetNewActiveBlueprint").Invoke(tractorBlueprint);
                            }
                        }
                        catch (Exception)
                        {
                            // If auto-select fails, the menu still opens on the Coop, so no crash.
                        }

                        // Consume the item
                        Game1.player.reduceActiveItemByOne();
                    }
                    else
                    {
                        // Optional: Add an i18n string for this warning too if you want
                        Game1.addHUDMessage(new HUDMessage("You can only build this on the Farm!", 3));
                    }
                }
            }
        }
    }
}