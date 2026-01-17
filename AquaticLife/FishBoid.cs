using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using System;

namespace AquaticLife
{
    public class FishBoid
    {
        public Vector2 Position;
        public bool IsDead => _state == BoidState.Dead;
        public bool IsVisible => _currentAlpha > 0.01f;

        private enum BoidState { Spawning, Active, Despawning, Dead }
        private BoidState _state;

        // NEW: Swim States for natural movement
        private enum SwimState { Idle, Accelerating, Gliding }
        private SwimState _swimState;

        private float _currentAlpha;
        private Vector2 _velocity;
        private float _rotation;
        private Texture2D? _texture;
        private Rectangle _sourceRect;
        private readonly GameLocation _location;
        private readonly float _scaleVariation;

        // PHYSICS VARIABLES
        private float _wanderAngle;
        private float _swimTimer;       // Counts down to next state change
        private float _currentSpeedLimit;

        // PERSONALITY VARIABLES (Randomized per fish)
        private readonly float _cruiseSpeed;   // How fast this specific fish likes to swim
        private readonly float _agility;       // How fast it turns
        private readonly float _energy;        // How often it moves vs sits still

        // CONSTANTS
        private const float Drag = 0.96f;      // Water resistance (slows them down)
        private const float MinSpeed = 0.05f;  // Threshold to consider "stopped"
        private const float WhiskerLength = 55f;
        private const float BaseScale = 2.5f;

        public FishBoid(Vector2 startPos, string itemId, GameLocation location)
        {
            Position = startPos;
            _location = location;
            _wanderAngle = (float)Game1.random.NextDouble() * MathHelper.TwoPi;

            // randomize personality
            _cruiseSpeed = 0.4f + (float)Game1.random.NextDouble() * 0.4f; // 0.4 to 0.8
            _agility = 0.05f + (float)Game1.random.NextDouble() * 0.05f;
            _energy = (float)Game1.random.NextDouble();

            _swimState = SwimState.Gliding;
            _swimTimer = 0f;

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

            // Start with a little push
            _velocity = new Vector2((float)Math.Cos(_wanderAngle), (float)Math.Sin(_wanderAngle)) * _cruiseSpeed;
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

            // --- 1. BRAIN (State Machine) ---
            _swimTimer -= (float)time.ElapsedGameTime.TotalSeconds;

            if (_swimTimer <= 0)
            {
                PickNewState();
            }

            // --- 2. SENSORY INPUT (Collision Detection) ---
            Vector2 forward = _velocity;
            if (forward == Vector2.Zero)
                forward = new Vector2((float)Math.Cos(_wanderAngle), (float)Math.Sin(_wanderAngle));
            else
                forward.Normalize();

            Vector2 leftWhisker = RotateVector(forward, -0.6f);
            Vector2 rightWhisker = RotateVector(forward, 0.6f);

            bool hitCenter = IsWall(Position + forward * WhiskerLength);
            bool hitLeft = IsWall(Position + leftWhisker * (WhiskerLength * 0.7f));
            bool hitRight = IsWall(Position + rightWhisker * (WhiskerLength * 0.7f));
            bool panic = hitCenter || hitLeft || hitRight;

            // --- 3. PHYSICS & STEERING ---

            if (panic)
            {
                // **PANIC MODE**: Override everything to avoid wall
                if (_swimState == SwimState.Idle)
                {
                    // Wake up immediately if drifting into wall
                    _swimState = SwimState.Accelerating;
                    _swimTimer = 0.5f;
                }

                float turnForce = _agility * 3f; // Hard turn
                if (hitCenter)
                {
                    if (hitLeft && !hitRight) _wanderAngle += turnForce;
                    else if (hitRight && !hitLeft) _wanderAngle -= turnForce;
                    else _wanderAngle += turnForce;
                }
                else if (hitLeft) _wanderAngle += turnForce;
                else if (hitRight) _wanderAngle -= turnForce;

                // Apply thrust to escape
                Vector2 escapeForce = new Vector2((float)Math.Cos(_wanderAngle), (float)Math.Sin(_wanderAngle)) * (_agility * 0.5f);
                _velocity += escapeForce;
            }
            else
            {
                // **NORMAL BEHAVIOR**
                if (_swimState == SwimState.Idle)
                {
                    // Just drift, drag slows us down
                    _velocity *= Drag;

                    // Very slow random rotation while idle (drifting)
                    _wanderAngle += (float)(Game1.random.NextDouble() - 0.5) * 0.01f;
                }
                else if (_swimState == SwimState.Accelerating)
                {
                    // Apply swimming force
                    Vector2 swimForce = new Vector2((float)Math.Cos(_wanderAngle), (float)Math.Sin(_wanderAngle)) * (_agility * 0.8f);
                    _velocity += swimForce;

                    // Slight steering variation
                    _wanderAngle += (float)(Game1.random.NextDouble() - 0.5) * _agility;
                }
                else if (_swimState == SwimState.Gliding)
                {
                    // Coasting, drag slows us down, minimal steering
                    _velocity *= (Drag + 0.02f); // Less drag when gliding than stopping
                }
            }

            // --- 4. MOVEMENT ---

            // Cap Speed
            if (_velocity.Length() > _cruiseSpeed)
            {
                _velocity.Normalize();
                _velocity *= _cruiseSpeed;
            }

            Vector2 nextPos = Position + _velocity;

            // Hard Collision Failsafe
            if (!IsWall(nextPos))
            {
                Position = nextPos;
                // Update visual rotation only if moving fast enough
                if (_velocity.LengthSquared() > 0.02f)
                {
                    float targetAngle = (float)Math.Atan2(_velocity.Y, _velocity.X);
                    // Smoothly rotate sprite to match velocity
                    _rotation = LerpRadians(_rotation, targetAngle, 0.15f);
                }
            }
            else
            {
                // Bonk
                _wanderAngle += MathHelper.Pi;
                _velocity = -_velocity * 0.5f; // Lose energy on bounce

                // Push back to center
                float tileCenterX = ((int)(Position.X / 64) * 64) + 32;
                float tileCenterY = ((int)(Position.Y / 64) * 64) + 32;
                Position += (new Vector2(tileCenterX, tileCenterY) - Position) * 0.1f;
            }
        }

        private void PickNewState()
        {
            // State Machine Logic
            switch (_swimState)
            {
                case SwimState.Idle:
                    // After Idling, we swim
                    _swimState = SwimState.Accelerating;
                    _swimTimer = 0.5f + (float)Game1.random.NextDouble() * 1.0f; // Swim for 0.5-1.5s

                    // Pick a new direction to swim towards
                    _wanderAngle += (float)(Game1.random.NextDouble() - 0.5) * MathHelper.PiOver2;
                    break;

                case SwimState.Accelerating:
                    // After accelerating, we glide
                    _swimState = SwimState.Gliding;
                    _swimTimer = 1.0f + (float)Game1.random.NextDouble() * 1.0f; // Glide for 1-2s
                    break;

                case SwimState.Gliding:
                    // After gliding, maybe we idle, maybe we swim again?
                    // High energy fish rarely idle.
                    if (Game1.random.NextDouble() > _energy)
                    {
                        _swimState = SwimState.Idle;
                        _swimTimer = 1.0f + (float)Game1.random.NextDouble() * 2.0f; // Rest for 1-3s
                    }
                    else
                    {
                        _swimState = SwimState.Accelerating; // Keep swimming!
                        _swimTimer = 0.5f + (float)Game1.random.NextDouble();
                    }
                    break;
            }
        }

        private float LerpRadians(float current, float target, float amount)
        {
            float difference = MathHelper.WrapAngle(target - current);
            return MathHelper.WrapAngle(current + difference * amount);
        }

        private Vector2 RotateVector(Vector2 v, float radians)
        {
            float ca = (float)Math.Cos(radians);
            float sa = (float)Math.Sin(radians);
            return new Vector2(v.X * ca - v.Y * sa, v.X * sa + v.Y * ca);
        }

        private bool IsWall(Vector2 pos) { return !IsSafeWater(pos); }

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

            if (!IsTileWater(tileX, tileY)) return false;

            float padding = 20f;
            float offX = pos.X % 64;
            float offY = pos.Y % 64;

            if (offX < padding && !IsTileWater(tileX - 1, tileY)) return false;
            if (offX > 64 - padding && !IsTileWater(tileX + 1, tileY)) return false;
            if (offY < padding && !IsTileWater(tileX, tileY - 1)) return false;
            if (offY > 64 - padding && !IsTileWater(tileX, tileY + 1)) return false;

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
            float globalScale = BaseScale * ModEntry.Config.FishScale * _scaleVariation;
            Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, Position);
            float finalOpacity = ModEntry.Config.FishOpacity * _currentAlpha;

            b.Draw(
                _texture,
                screenPos,
                _sourceRect,
                Color.White * finalOpacity,
                _rotation + rotationOffset,
                new Vector2(_sourceRect.Width / 2f, _sourceRect.Height / 2f),
                globalScale,
                SpriteEffects.None,
                0.001f
            );
        }
    }
}