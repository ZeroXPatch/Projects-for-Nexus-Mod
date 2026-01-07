using System;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;

namespace HorseFollower
{
    public class ModEntry : Mod
    {
        private ModConfig Config = null!;
        private bool IsFollowMode = false;

        private Horse? _cachedHorse;
        private int _searchCooldown = 0;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            _cachedHorse = null;
            IsFollowMode = false;
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player == null) return;

            if (e.Button == this.Config.ToggleKey)
            {
                IsFollowMode = !IsFollowMode;
                _cachedHorse = null;

                string status = IsFollowMode ? "ON" : "OFF";
                Game1.addHUDMessage(new HUDMessage($"Horse Follow: {status}", 2));

                if (IsFollowMode) _cachedHorse = FindHorse();
            }
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || !IsFollowMode || Game1.player == null) return;
            if (Game1.eventUp || Game1.isFestival() || Game1.CurrentEvent != null) return;

            Horse? horse = GetHorseEfficiently();
            if (horse == null) return;

            if (Game1.player.mount == horse || horse.rider != null) return;

            ProcessHorseLogic(horse, Game1.player);
        }

        private Horse? GetHorseEfficiently()
        {
            if (_cachedHorse != null && _cachedHorse.currentLocation != null)
                return _cachedHorse;

            if (_searchCooldown > 0)
            {
                _searchCooldown--;
                return null;
            }

            _cachedHorse = FindHorse();
            if (_cachedHorse == null) _searchCooldown = 60;

            return _cachedHorse;
        }

        private void ProcessHorseLogic(Horse horse, Farmer player)
        {
            // 1. Warp Logic
            if (horse.currentLocation != player.currentLocation)
            {
                WarpHorse(horse, player);
                return;
            }

            // 2. Movement Logic
            float distance = Vector2.Distance(horse.Position, player.Position);

            if (distance > this.Config.TeleportThreshold)
            {
                WarpHorse(horse, player);
                return;
            }

            if (distance > this.Config.FollowDistance)
            {
                MoveHorse(horse, player);
            }
            else
            {
                horse.Sprite.StopAnimation();
                FacePlayer(horse, player);
            }
        }

        private void MoveHorse(Horse horse, Farmer player)
        {
            // 1. Apply Movement
            Vector2 direction = player.Position - horse.Position;
            direction.Normalize();
            horse.Position += direction * this.Config.MovementSpeed;

            // 2. Determine Facing Direction
            // 0=Up, 1=Right, 2=Down, 3=Left
            int facing;
            if (Math.Abs(direction.X) > Math.Abs(direction.Y))
                facing = direction.X > 0 ? 1 : 3;
            else
                facing = direction.Y > 0 ? 2 : 0;

            horse.FacingDirection = facing;

            // 3. CORRECT VANILLA ANIMATION MAPPING
            // Stardew Horse Sheet Layout:
            // Row 0 (Frames 0-6): Side View (Used for Right & Left)
            // Row 1 (Frames 7-13): Up View (Butt)
            // Row 2 (Frames 14-20): Down View (Face)

            int startFrame = 0;
            bool flip = false;

            switch (facing)
            {
                case 0: // Up (Back)
                    startFrame = 7;
                    flip = false;
                    break;
                case 1: // Right
                    startFrame = 0;
                    flip = false;
                    break;
                case 2: // Down (Face)
                    startFrame = 14;
                    flip = false;
                    break;
                case 3: // Left
                    startFrame = 0;
                    flip = true; // Use Right frames + Flip
                    break;
            }

            // Apply Animation
            if (Game1.currentGameTime.TotalGameTime.TotalMilliseconds % 200 < 100)
            {
                horse.Sprite.Animate(Game1.currentGameTime, startFrame, 7, 100f);

                // Manually handle Flip to fix Left direction
                // We use faceDirection() because it updates the internal sprite flip state safely
                if (flip)
                    horse.Sprite.faceDirection(3); // Left
                else if (facing == 1)
                    horse.Sprite.faceDirection(1); // Right
                else
                    horse.Sprite.faceDirection(facing); // Up/Down
            }
        }

        private void FacePlayer(Horse horse, Farmer player)
        {
            Vector2 diff = player.Position - horse.Position;
            int facing;
            if (Math.Abs(diff.X) > Math.Abs(diff.Y))
                facing = diff.X > 0 ? 1 : 3;
            else
                facing = diff.Y > 0 ? 2 : 0;

            horse.FacingDirection = facing;
            horse.Sprite.faceDirection(facing);

            // Set static idle frame (Standing still)
            switch (facing)
            {
                case 0: horse.Sprite.currentFrame = 7; break;  // Up
                case 1: horse.Sprite.currentFrame = 0; break;  // Right
                case 2: horse.Sprite.currentFrame = 14; break; // Down
                case 3: horse.Sprite.currentFrame = 0; break;  // Left
            }
        }

        private void WarpHorse(Horse horse, Farmer player)
        {
            if (horse.currentLocation != null)
                horse.currentLocation.characters.Remove(horse);

            horse.currentLocation = player.currentLocation;
            horse.Position = player.Position;

            if (!player.currentLocation.characters.Contains(horse))
                player.currentLocation.addCharacter(horse);

            if (this.Config.PlayTeleportSound)
                Game1.playSound("wand");

            player.currentLocation.temporarySprites.Add(new TemporaryAnimatedSprite(
                "TileSheets\\animations", new Rectangle(0, 320, 64, 64),
                50f, 8, 0, horse.Position, flicker: false, flipped: false));
        }

        private Horse? FindHorse()
        {
            if (Game1.player.mount != null) return Game1.player.mount;
            foreach (var character in Game1.currentLocation.characters)
                if (character is Horse h) return h;
            foreach (GameLocation location in Game1.locations)
                foreach (var character in location.characters)
                    if (character is Horse h) return h;
            return null;
        }
    }
}