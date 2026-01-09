using System;
using System.Linq;
using System.Reflection;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;

namespace GMCMShortcut
{
    public class ModEntry : Mod
    {
        private ModConfig Config = new();
        private bool IsGmcmInstalled = false;

        // Reflection targets
        private object? GmcmModInstance;
        private MethodInfo? OpenListMenuMethod;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.Input.ButtonsChanged += OnButtonsChanged;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            object? api = this.Helper.ModRegistry.GetApi("spacechase0.GenericModConfigMenu");

            if (api == null)
            {
                this.Monitor.Log("GMCM Shortcut: GMCM not installed.", LogLevel.Warn);
                return;
            }

            this.IsGmcmInstalled = true;
            RegisterConfig(api);
            FindGmcmInternal(api);
        }

        private void RegisterConfig(object api)
        {
            try
            {
                Type apiType = api.GetType();
                MethodInfo? register = apiType.GetMethod("Register");
                MethodInfo? addKeybind = apiType.GetMethod("AddKeybindList");

                if (register != null)
                {
                    register.Invoke(api, new object[] {
                        this.ModManifest,
                        (Action)(() => this.Config = new ModConfig()),
                        (Action)(() => this.Helper.WriteConfig(this.Config)),
                        false
                    });
                }

                if (addKeybind != null)
                {
                    addKeybind.Invoke(api, new object?[] {
                        this.ModManifest,
                        (Func<KeybindList>)(() => this.Config.OpenMenuKey),
                        (Action<KeybindList>)(val => this.Config.OpenMenuKey = val),
                        (Func<string>)(() => "Open Menu Shortcut"),
                        (Func<string>)(() => "The keybind to open the Generic Mod Config Menu."),
                        "OpenMenuKey"
                    });
                }
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"GMCM Shortcut: Config reg failed: {ex.Message}", LogLevel.Error);
            }
        }

        private void FindGmcmInternal(object api)
        {
            try
            {
                Assembly gmcmAssembly = api.GetType().Assembly;

                // 1. Find the Main Mod Class
                Type? modClass = gmcmAssembly.GetTypes()
                    .FirstOrDefault(t => typeof(Mod).IsAssignableFrom(t) && !t.IsAbstract);

                if (modClass == null) return;

                // 2. Find the 'instance' field
                FieldInfo? instanceField = modClass.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                                        ?? modClass.GetField("Instance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                if (instanceField != null)
                {
                    this.GmcmModInstance = instanceField.GetValue(null);
                }
                else
                {
                    FieldInfo? modFieldInApi = api.GetType().GetField("Mod", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (modFieldInApi != null)
                        this.GmcmModInstance = modFieldInApi.GetValue(api);
                }

                if (this.GmcmModInstance == null) return;

                // 3. Find the Open Method
                string[] methodCandidates = new[] { "OpenListMenu", "openListMenu", "OpenMenu" };

                foreach (var name in methodCandidates)
                {
                    MethodInfo? method = modClass.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method != null)
                    {
                        this.OpenListMenuMethod = method;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"GMCM Shortcut: Reflection error: {ex.Message}", LogLevel.Error);
            }
        }

        private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
        {
            if (!this.IsGmcmInstalled) return;

            if (this.Config.OpenMenuKey.JustPressed())
            {
                // --- TOGGLE LOGIC START ---

                // 1. Check if GMCM is currently open
                if (IsGmcmMenuOpen())
                {
                    CloseGmcmMenu();
                    return;
                }

                // 2. If not open, try to OPEN it
                if (this.GmcmModInstance == null || this.OpenListMenuMethod == null) return;

                if (Context.IsPlayerFree || Game1.activeClickableMenu is GameMenu || Game1.activeClickableMenu is TitleMenu)
                {
                    try
                    {
                        ParameterInfo[] parameters = this.OpenListMenuMethod.GetParameters();
                        object?[] args = new object?[parameters.Length];

                        for (int i = 0; i < parameters.Length; i++)
                        {
                            Type type = parameters[i].ParameterType;
                            if (type.IsValueType)
                                args[i] = Activator.CreateInstance(type);
                            else
                                args[i] = null;
                        }

                        this.OpenListMenuMethod.Invoke(this.GmcmModInstance, args);
                        Game1.playSound("bigSelect");
                    }
                    catch (Exception ex)
                    {
                        this.Monitor.Log($"GMCM Shortcut: Error opening menu: {ex.Message}", LogLevel.Error);
                    }
                }
            }
        }

        // --- HELPER METHODS FOR TOGGLING ---

        private bool IsGmcmMenuOpen()
        {
            // Check the active menu
            if (Game1.activeClickableMenu != null)
            {
                string typeName = Game1.activeClickableMenu.GetType().FullName ?? "";
                if (typeName.Contains("GenericModConfigMenu")) return true;
            }

            // Check the Title Screen sub-menu
            if (Game1.activeClickableMenu is TitleMenu && TitleMenu.subMenu != null)
            {
                string typeName = TitleMenu.subMenu.GetType().FullName ?? "";
                if (typeName.Contains("GenericModConfigMenu")) return true;
            }

            return false;
        }

        private void CloseGmcmMenu()
        {
            // If on title screen, clear subMenu
            if (Game1.activeClickableMenu is TitleMenu)
            {
                TitleMenu.subMenu = null;
            }
            else
            {
                // If in game, clear active menu
                // Note: This closes it completely back to gameplay. 
                // If you want to go back to the 'Game Menu', we'd need to manually instantiate a GameMenu, 
                // but standard 'Quit' behavior is usually closing to game.
                Game1.activeClickableMenu = null;
            }

            Game1.playSound("bigDeSelect");
        }
    }
}