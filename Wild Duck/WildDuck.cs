using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using System;

namespace WildSwimmingDucks
{
    public class WildDuck
    {
        // --- PHYSICS ---
        public Vector2 Position; // Top-Left of the tile they are currently in
        private Vector2 Velocity;
        private Vector2 TargetPosition;

        // --- MOVEMENT SETTINGS ---
        private float MaxSpeed = 1.0f;
        private float Acceleration = 0.04f;
        private float Friction = 0.93f;

        // --- STATE ---
        private enum State { Idle, Wandering }
        private State CurrentState;
        private float StateTimer = 0f;
        private bool IsInWater = false;
        private bool Flipped = false;

        // --- VISUALS ---
        private Texture2D Texture;
        private Rectangle SourceRect;
        private float WalkTimer = 0f;
        private float WaterBobTimer = 0f;
        private float RippleTimer = 0f;

        // --- CONSTANTS ---
        private const int SpriteSize = 16;
        private const float Scale = 4f;

        public WildDuck(Vector2 startPos)
        {
            this.Position = startPos;
            this.TargetPosition = startPos;
            this.CurrentState = State.Idle;
            this.Texture = Game1.content.Load<Texture2D>("Animals\\Duck");

            // Adult Duck, Side View (Starts at Y=64 in texture)
            this.SourceRect = new Rectangle(0, 64, 16, 16);

            CheckSurface(Game1.currentLocation);
        }

        public void Update(GameLocation location)
        {
            // 1. AI Decision Making
            UpdateAI(location);

            // 2. Physics & Movement
            UpdatePhysics(location);

            // 3. Animation & Visuals
            UpdateAnimation(location);
        }

        private void UpdateAI(GameLocation location)
        {
            StateTimer -= 0.016f; // Tick timer

            if (StateTimer <= 0)
            {
                // Randomly choose to Idle or Wander
                if (Game1.random.NextDouble() < 0.45)
                {
                    CurrentState = State.Idle;
                    StateTimer = Game1.random.Next(3, 7); // Rest for 3-7 seconds
                }
                else
                {
                    CurrentState = State.Wandering;
                    PickNewTarget(location);
                    StateTimer = Game1.random.Next(4, 9); // Move for 4-9 seconds
                }
            }
        }

        private void PickNewTarget(GameLocation location)
        {
            // Search for a valid spot
            for (int i = 0; i < 10; i++)
            {
                int range = 7;
                int tileX = (int)(Position.X / 64) + Game1.random.Next(-range, range + 1);
                int tileY = (int)(Position.Y / 64) + Game1.random.Next(-range, range + 1);

                Rectangle tileRect = new Rectangle(tileX * 64, tileY * 64, 64, 64);

                // Conditions: 
                // 1. Must be passable (Water is passble for us, Land must be clear)
                // 2. We prefer Water, or Land strictly adjacent to water.

                bool isWater = location.isWaterTile(tileX, tileY);
                bool isPassable = location.isTilePassable(tileRect, Game1.viewport);

                if (isWater || (isPassable && IsNearWater(location, tileX, tileY)))
                {
                    TargetPosition = new Vector2(tileX * 64, tileY * 64);
                    return;
                }
            }
            // Fallback: stay idle
            CurrentState = State.Idle;
        }

        private void UpdatePhysics(GameLocation location)
        {
            // --- MOVEMENT FORCE ---
            if (CurrentState == State.Wandering)
            {
                Vector2 direction = TargetPosition - Position;
                if (direction.Length() < 4f)
                {
                    CurrentState = State.Idle; // Arrived
                }
                else
                {
                    direction.Normalize();
                    Velocity += direction * Acceleration;
                }
            }
            // --- DRIFTING (Water Only) ---
            else if (CurrentState == State.Idle && IsInWater)
            {
                // Add a gentle sine-wave drift to simulate water currents
                float time = (float)Game1.currentGameTime.TotalGameTime.TotalSeconds;
                Velocity.X += (float)Math.Sin(time * 0.7f) * 0.003f;
                Velocity.Y += (float)Math.Cos(time * 0.5f) * 0.003f;
            }

            // --- FRICTION ---
            Velocity *= Friction;

            // --- CAP SPEED ---
            // Slower in water for realistic resistance
            float currentMax = IsInWater ? MaxSpeed * 0.6f : MaxSpeed;
            if (Velocity.Length() > currentMax)
            {
                Velocity.Normalize();
                Velocity *= currentMax;
            }

            // --- APPLY MOVEMENT ---
            Vector2 nextPos = Position + Velocity;

            // --- COLLISION CHECK ---
            // Verify if we can legally stand on the next tile
            if (CanStandHere(location, nextPos))
            {
                Position = nextPos;
            }
            else
            {
                // Hit a wall, bounce gently
                Velocity *= -0.4f;
                CurrentState = State.Idle;
            }

            // Re-check surface type (Land vs Water)
            CheckSurface(location);
        }

        private void UpdateAnimation(GameLocation location)
        {
            // 1. Flip Direction
            if (Velocity.X < -0.05f) Flipped = true;
            if (Velocity.X > 0.05f) Flipped = false;

            // 2. Frame Logic
            if (IsInWater)
            {
                // In Water: Always use "Standing" frame (Frame 0), looks like gliding.
                SourceRect.X = 0;

                // Ripples behind duck
                RippleTimer += 0.016f;
                if (RippleTimer > 0.5f && Velocity.Length() > 0.1f)
                {
                    RippleTimer = 0;
                    location.temporarySprites.Add(new TemporaryAnimatedSprite(
                        "TileSheets\\animations", new Rectangle(0, 0, 64, 64), 50f, 9, 1,
                        Position + new Vector2(0, 10), false, false)
                    { scale = 0.5f, alpha = 0.3f, layerDepth = 0.0001f });
                }
            }
            else
            {
                // On Land: Waddle Animation
                if (Velocity.Length() > 0.1f)
                {
                    WalkTimer += 0.15f;
                    if (WalkTimer > 1f)
                    {
                        WalkTimer = 0f;
                        // Toggle Frame 0 (Stand) and Frame 1 (Step)
                        SourceRect.X = (SourceRect.X == 0) ? 16 : 0;
                    }
                }
                else
                {
                    // Standing still on land
                    SourceRect.X = 0;
                    // Occasional Grooming (Frame 2)
                    if (Game1.random.NextDouble() < 0.005) SourceRect.X = 32;
                }
            }
        }

        private void CheckSurface(GameLocation location)
        {
            // Check the center point of the duck
            int tileX = (int)((Position.X + 32) / 64);
            int tileY = (int)((Position.Y + 32) / 64);
            IsInWater = location.isWaterTile(tileX, tileY);
        }

        private bool CanStandHere(GameLocation location, Vector2 pos)
        {
            // Use a small hitbox at the feet
            Rectangle feetBox = new Rectangle((int)pos.X + 20, (int)pos.Y + 32, 24, 24);

            // Is the tile passable?
            if (!location.isTilePassable(new Rectangle((int)pos.X, (int)pos.Y, 64, 64), Game1.viewport))
                return false;

            // If it's a bridge, is it open?
            return true;
        }

        private bool IsNearWater(GameLocation location, int x, int y)
        {
            // Search immediate neighbors
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (location.isWaterTile(x + dx, y + dy)) return true;
            return false;
        }

        public void Draw(SpriteBatch b)
        {
            // --- DRAWING OFFSET LOGIC ---

            // 1. Vertical Offset
            // Land: 0 (Feet on ground)
            // Water: +12 (Sinks body into water)
            float verticalOffset = IsInWater ? 12f : 0f;

            // 2. Bobbing (Water Only)
            if (IsInWater)
            {
                WaterBobTimer += 0.05f;
                verticalOffset += (float)Math.Sin(WaterBobTimer) * 2f;
            }

            // 3. Shadow (Land Only)
            if (!IsInWater)
            {
                b.Draw(Game1.shadowTexture,
                    Game1.GlobalToLocal(Game1.viewport, Position + new Vector2(32, 60)),
                    Game1.shadowTexture.Bounds, Color.White * 0.5f, 0f,
                    new Vector2(Game1.shadowTexture.Bounds.Center.X, Game1.shadowTexture.Bounds.Center.Y),
                    3f, SpriteEffects.None, (Position.Y) / 10000f - 0.0001f);
            }

            SpriteEffects effect = Flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // --- MAIN DRAW CALL ---
            // Origin: (8, 16) -> This is the Bottom-Center of the 16x16 sprite.
            // Position + (32, 64) -> This places the Origin at the Bottom-Center of the 64x64 tile.
            // Result: The feet sit exactly on the tile boundary.

            b.Draw(Texture,
                Game1.GlobalToLocal(Game1.viewport, Position + new Vector2(32, 64 + verticalOffset)),
                SourceRect,
                Color.White,
                0f,
                new Vector2(8, 16), // Origin: Bottom Center of sprite
                Scale,
                effect,
                (Position.Y + 64) / 10000f);
        }
    }
}