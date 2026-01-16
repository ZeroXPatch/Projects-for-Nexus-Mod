using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using System;

namespace ShadowsOfTheDeep
{
    public class ShadowBoid
    {
        public Vector2 Position;
        public bool IsDead => _state == BoidState.Dead;
        public bool IsVisible => _currentAlpha > 0.01f;

        private enum BoidState { Spawning, Active, Despawning, Dead }
        private BoidState _state;

        private float _currentAlpha;
        private Vector2 _velocity;
        private float _rotation;
        private Texture2D? _texture;
        private Rectangle _sourceRect;
        private readonly GameLocation _location;
        private readonly float _scaleVariation;

        // MOVEMENT VARIABLES
        private float _wanderAngle;
        private const float MaxSpeed = 0.65f; // Slightly slower for more chill movement
        private const float TurnSpeed = 0.08f; // How fast they turn
        private const float BaseScale = 3.5f;

        // COLLISION SETTINGS
        // Distance to look ahead for walls
        private const float WhiskerLength = 50f;

        public ShadowBoid(Vector2 startPos, string itemId, GameLocation location)
        {
            Position = startPos;
            _location = location;
            _wanderAngle = (float)Game1.random.NextDouble() * MathHelper.TwoPi;

            if (ModEntry.Config.EnableFadeEffects)
            {
                _state = BoidState.Spawning;
                _currentAlpha = 0f;
            }
            else
            {
                _state = BoidState.Active;
                _currentAlpha = 1f;
            }

            var data = ItemRegistry.GetData(itemId);
            if (data != null)
            {
                _texture = data.GetTexture();
                _sourceRect = data.GetSourceRect();
            }
            else
            {
                _texture = Game1.objectSpriteSheet;
                _sourceRect = Game1.getSourceRectForStandardTileSheet(Game1.objectSpriteSheet, 168, 16, 16);
            }

            _scaleVariation = (float)(0.8 + (Game1.random.NextDouble() * 0.4));
            _velocity = new Vector2((float)Math.Cos(_wanderAngle), (float)Math.Sin(_wanderAngle)) * MaxSpeed;
        }

        public void StartDespawn()
        {
            if (_state != BoidState.Dead && _state != BoidState.Despawning)
                _state = BoidState.Despawning;
        }

        public void Cleanup() { _texture = null; }

        public void Update(GameTime time)
        {
            HandleLifecycle();
            if (_state == BoidState.Dead) return;

            // --- 1. SENSORY INPUT (WHISKERS) ---

            // Define 3 whiskers: Center, Left, Right
            Vector2 forward = _velocity;
            if (forward != Vector2.Zero) forward.Normalize();

            Vector2 leftWhisker = RotateVector(forward, -0.5f); // ~30 degrees left
            Vector2 rightWhisker = RotateVector(forward, 0.5f); // ~30 degrees right

            bool hitCenter = IsWall(Position + forward * WhiskerLength);
            bool hitLeft = IsWall(Position + leftWhisker * (WhiskerLength * 0.8f));
            bool hitRight = IsWall(Position + rightWhisker * (WhiskerLength * 0.8f));

            // --- 2. STEERING LOGIC ---

            if (hitCenter || hitLeft || hitRight)
            {
                // **AVOIDANCE MODE**
                // We are heading towards a wall. We must turn AWAY.

                float turnAmount = TurnSpeed * 1.5f; // Turn faster when avoiding walls

                if (hitCenter)
                {
                    // If hitting head-on, turn hard. 
                    // Determine which side is clearer.
                    if (hitLeft && !hitRight) _wanderAngle += turnAmount; // Turn Right
                    else if (hitRight && !hitLeft) _wanderAngle -= turnAmount; // Turn Left
                    else _wanderAngle += turnAmount; // Both blocked? Just turn right arbitrarily
                }
                else if (hitLeft)
                {
                    // Wall on left -> Turn Right
                    _wanderAngle += turnAmount;
                }
                else if (hitRight)
                {
                    // Wall on right -> Turn Left
                    _wanderAngle -= turnAmount;
                }
            }
            else
            {
                // **WANDER MODE**
                // No walls ahead. Just chill and swim.
                _wanderAngle += (float)(Game1.random.NextDouble() - 0.5) * 0.05f;
            }

            // --- 3. PHYSICS UPDATE ---

            // Convert Angle to Velocity
            Vector2 desiredVelocity = new Vector2((float)Math.Cos(_wanderAngle), (float)Math.Sin(_wanderAngle)) * MaxSpeed;

            // Smoothly interpolate current velocity to desired velocity
            _velocity = Vector2.Lerp(_velocity, desiredVelocity, 0.1f);

            Vector2 nextPos = Position + _velocity;

            // --- 4. HARD COLLISION FAILSAFE ---
            // If the whiskers failed and we are physically touching a wall pixel, stop and bounce.
            if (!IsWall(nextPos))
            {
                Position = nextPos;
                if (_velocity.LengthSquared() > 0.001f)
                    _rotation = (float)Math.Atan2(_velocity.Y, _velocity.X);
            }
            else
            {
                // Stuck! Force a hard turn and bounce back.
                _wanderAngle += MathHelper.Pi;
                _velocity = -_velocity;

                // Emergency push to tile center
                float tileCenterX = ((int)(Position.X / 64) * 64) + 32;
                float tileCenterY = ((int)(Position.Y / 64) * 64) + 32;
                Position += (new Vector2(tileCenterX, tileCenterY) - Position) * 0.1f;
            }
        }

        // --- HELPERS ---

        private Vector2 RotateVector(Vector2 v, float radians)
        {
            float ca = (float)Math.Cos(radians);
            float sa = (float)Math.Sin(radians);
            return new Vector2(v.X * ca - v.Y * sa, v.X * sa + v.Y * ca);
        }

        // Returns TRUE if the point is LAND (Wall), FALSE if it is Water
        private bool IsWall(Vector2 pos)
        {
            return !IsSafeWater(pos);
        }

        private bool IsTileWater(int x, int y)
        {
            if (_location.doesTileHaveProperty(x, y, "Water", "Back") == null) return false;
            if (_location.getTileIndexAt(x, y, "Buildings") != -1)
            {
                if (_location.doesTileHaveProperty(x, y, "Passable", "Buildings") == null)
                    return false;
            }
            return true;
        }

        private bool IsSafeWater(Vector2 pos)
        {
            int tileX = (int)(pos.X / 64f);
            int tileY = (int)(pos.Y / 64f);

            // 1. Center Check
            if (!IsTileWater(tileX, tileY)) return false;

            // 2. Padding Check (Keep them away from edges)
            // 20px padding is enough if we use Whiskers to turn them early
            float padding = 20f;
            float offX = pos.X % 64;
            float offY = pos.Y % 64;

            if (offX < padding && !IsTileWater(tileX - 1, tileY)) return false;
            if (offX > 64 - padding && !IsTileWater(tileX + 1, tileY)) return false;
            if (offY < padding && !IsTileWater(tileX, tileY - 1)) return false;
            if (offY > 64 - padding && !IsTileWater(tileX, tileY + 1)) return false;

            // 3. Diagonal Check (Corner Bleed)
            if (offX < padding && offY < padding && !IsTileWater(tileX - 1, tileY - 1)) return false;
            if (offX > 64 - padding && offY < padding && !IsTileWater(tileX + 1, tileY - 1)) return false;
            if (offX < padding && offY > 64 - padding && !IsTileWater(tileX - 1, tileY + 1)) return false;
            if (offX > 64 - padding && offY > 64 - padding && !IsTileWater(tileX + 1, tileY + 1)) return false;

            return true;
        }

        private void HandleLifecycle()
        {
            float fadeSpeed = ModEntry.Config.EnableFadeEffects ? ModEntry.Config.FadeSpeed : 1.0f;
            switch (_state)
            {
                case BoidState.Spawning:
                    _currentAlpha += fadeSpeed;
                    if (_currentAlpha >= 1f) { _currentAlpha = 1f; _state = BoidState.Active; }
                    break;
                case BoidState.Despawning:
                    _currentAlpha -= fadeSpeed;
                    if (_currentAlpha <= 0f) { _currentAlpha = 0f; _state = BoidState.Dead; }
                    break;
            }
        }

        public void Draw(SpriteBatch b)
        {
            if (_texture == null) return;
            float rotationOffset = MathHelper.PiOver4;
            float globalScale = BaseScale * ModEntry.Config.ShadowScale * _scaleVariation;
            Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, Position);
            float finalOpacity = ModEntry.Config.ShadowOpacity * _currentAlpha;

            b.Draw(_texture, screenPos, _sourceRect, Color.Black * finalOpacity,
                _rotation + rotationOffset, new Vector2(_sourceRect.Width / 2f, _sourceRect.Height / 2f),
                globalScale, SpriteEffects.None, 0.001f);
        }
    }
}