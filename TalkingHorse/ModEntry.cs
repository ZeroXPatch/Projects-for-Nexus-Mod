using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Objects;
using SObject = StardewValley.Object;

namespace TalkingHorse
{
    /// <summary>
    /// Entry class for the Talking Horse mod. The mod adds simple personality
    /// and a light bribe mechanic to the player's horse while keeping the
    /// behaviour configurable and fully localizable.
    /// </summary>
    public class ModEntry : Mod
    {
        private readonly Random _random = new();

        private ModConfig _config = null!;

        private List<string> _lines = new();

        private int _lastSpeechTime = -9999;

        private int _quietDay = -1;

        public override void Entry(IModHelper helper)
        {
            LoadConfig();

            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
        }

        /// <summary>
        /// Reload configuration when a save is loaded, so any user edits are
        /// picked up without needing to restart the game process.
        /// </summary>
        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            _quietDay = -1;
            _lastSpeechTime = -9999;
            LoadConfig();
        }

        /// <summary>
        /// Handles the primary update loop. The logic keeps the checks light by
        /// running only every few real-time ticks and short-circuiting when the
        /// world is not ready or the player is not mounted.
        /// </summary>
        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
            {
                return;
            }

            Horse? horse = GetMountedHorse();
            if (horse == null)
            {
                return;
            }

            if (Game1.dayOfMonth == _quietDay)
            {
                return;
            }

            if (!e.IsMultipleOf(_config.UpdateCheckIntervalTicks))
            {
                return;
            }

            int currentGameSeconds = GetCurrentGameSeconds();
            if (!IsOffCooldown(currentGameSeconds))
            {
                return;
            }

            if (!_config.EnableRandomSpeech || !_lines.Any())
            {
                return;
            }

            if (_random.NextDouble() > _config.SpeechChance)
            {
                return;
            }

            SayRandomLine(horse);
            _lastSpeechTime = currentGameSeconds;
        }

        /// <summary>
        /// Handle right-click interactions to allow bribing the horse with
        /// mayonnaise or prompting alternate responses when using other items.
        /// </summary>
        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady)
            {
                return;
            }

            if (!e.Button.IsActionButton())
            {
                return;
            }

            Vector2 tile = e.Cursor.GrabTile;
            Horse? horse = GetHorseAtTile(tile);
            if (horse == null)
            {
                return;
            }

            if (Game1.player.ActiveObject is SObject obj && obj is not null)
            {
                if (IsMayonnaise(obj))
                {
                    ConsumeMayo(obj);
                    QuietHorseForDay(horse);
                    return;
                }

                RespondToNonMayo(horse, obj);
                return;
            }

            horse.showTextAboveHead(GetText("horse.no-payment"));
        }

        private void LoadConfig()
        {
            _config = Helper.ReadConfig<ModConfig>();

            bool changed = false;
            if (_config.SpeechIntervalMinutes < 1)
            {
                _config.SpeechIntervalMinutes = 10;
                changed = true;
            }

            if (_config.UpdateCheckIntervalTicks < 10)
            {
                _config.UpdateCheckIntervalTicks = 30;
                changed = true;
            }

            if (_config.SpeechChance < 0 || _config.SpeechChance > 1)
            {
                _config.SpeechChance = 0.25f;
                changed = true;
            }

            if (_config.Lines == null || !_config.Lines.Any())
            {
                _config.Lines = BuildDefaultLines();
                changed = true;
            }

            _lines = new List<string>(_config.Lines);

            if (changed)
            {
                Helper.WriteConfig(_config);
            }
        }

        private List<string> BuildDefaultLines()
        {
            var translations = new List<string>();

            for (int i = 1; i <= 30; i++)
            {
                string key = $"lines.{i}";
                Translation t = Helper.Translation.Get(key);
                if (t.HasValue())
                {
                    translations.Add(t.ToString());
                }
            }

            if (translations.Count == 0)
            {
                translations.Add("Nice crop, farmer.");
                translations.Add("Are we really going mining again?");
                translations.Add("Brush me or I strike.");
                translations.Add("We could be napping instead.");
                translations.Add("Another day, another unpaid shift.");
            }

            return translations;
        }

        private bool IsOffCooldown(int currentGameSeconds)
        {
            int requiredSeconds = _config.SpeechIntervalMinutes * 60;
            return currentGameSeconds - _lastSpeechTime >= requiredSeconds;
        }

        private void SayRandomLine(Horse horse)
        {
            int index = _random.Next(_lines.Count);
            string text = _lines[index];

            horse.showTextAboveHead(text);
            Game1.playSound("horse");
        }

        private void QuietHorseForDay(Horse horse)
        {
            _quietDay = Game1.dayOfMonth;
            _lastSpeechTime = GetCurrentGameSeconds();

            horse.showTextAboveHead(GetText("horse.quiet"));
            Game1.playSound("cowboy_monsterhit");

            Monitor.Log("Horse bribed with mayonnaise. It will stay quiet today.", LogLevel.Info);
        }

        private void RespondToNonMayo(Horse horse, SObject item)
        {
            string response = GetText("horse.non-mayo", new { item = item.DisplayName });
            horse.showTextAboveHead(response);
        }

        private bool IsMayonnaise(SObject item)
        {
            return string.Equals(item.Name, "Mayonnaise", StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.DisplayName, "Mayonnaise", StringComparison.OrdinalIgnoreCase);
        }

        private void ConsumeMayo(SObject item)
        {
            item.Stack--;
            if (item.Stack <= 0)
            {
                Game1.player.removeItemFromInventory(item);
            }
        }

        private Horse? GetMountedHorse()
        {
            foreach (var character in Game1.currentLocation.characters)
            {
                if (character is Horse horse && horse.rider.Value == Game1.player)
                {
                    return horse;
                }
            }

            return null;
        }

        private Horse? GetHorseAtTile(Vector2 tile)
        {
            foreach (var character in Game1.currentLocation.characters)
            {
                if (character is Horse horse)
                {
                    Vector2 horseTile = horse.getTileLocation();
                    if (Vector2.Distance(horseTile, tile) <= 0.5f)
                    {
                        return horse;
                    }
                }
            }

            return null;
        }

        private int GetCurrentGameSeconds()
        {
            int time = Game1.timeOfDay;
            int hours = time / 100;
            int minutes = time % 100;

            int totalMinutes = (hours - 6) * 60 + minutes;
            if (totalMinutes < 0)
            {
                totalMinutes = 0;
            }

            return totalMinutes * 60;
        }

        private string GetText(string key, object? tokens = null)
        {
            Translation translation = Helper.Translation.Get(key);
            if (tokens != null)
            {
                translation = translation.Tokenize(tokens);
            }

            if (translation.HasValue())
            {
                return translation.ToString();
            }

            return key;
        }
    }

    /// <summary>
    /// User-editable configuration for the mod. The JSON is generated on first
    /// launch and may be edited while the game is closed.
    /// </summary>
    public class ModConfig
    {
        /// <summary>Enable or disable random horse speech entirely.</summary>
        public bool EnableRandomSpeech { get; set; } = true;

        /// <summary>
        /// How many in-game minutes must pass between horse comments. Setting
        /// this to a high value reduces chatter, while a low value keeps the
        /// horse talkative.
        /// </summary>
        public int SpeechIntervalMinutes { get; set; } = 10;

        /// <summary>
        /// Chance (0.0 - 1.0) that the horse speaks on a valid check. This is
        /// applied after the cooldown, so lowering it makes the horse quieter.
        /// </summary>
        public float SpeechChance { get; set; } = 0.25f;

        /// <summary>
        /// Number of real-time update ticks to wait between checks. SMAPI runs
        /// 60 ticks per second; a default of 30 keeps CPU usage light.
        /// </summary>
        public uint UpdateCheckIntervalTicks { get; set; } = 30;

        /// <summary>
        /// Custom speech lines. If empty, localized defaults are used instead.
        /// </summary>
        public List<string> Lines { get; set; } = new();
    }
}
