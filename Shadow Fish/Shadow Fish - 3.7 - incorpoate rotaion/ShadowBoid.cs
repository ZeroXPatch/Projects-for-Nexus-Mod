using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using System;
using System.Collections.Generic;

namespace ShadowsOfTheDeep
{
    public class ShadowBoid
    {
        public Vector2 Position;
        public bool IsDead => _lifeState == LifeState.Dead;
        public bool IsVisible => _currentAlpha > 0.01f;

        // STATES
        private enum LifeState { Spawning, Active, Despawning, Dead }
        private LifeState _lifeState;

        private enum SwimState { Idle, Accelerating, Gliding }
        private SwimState _swimState;

        // PHYSICS
        private Vector2 _velocity;
        private Vector2 _acceleration;
        private float _currentAlpha;
        private float _rotation;
        private float _wanderAngle;

        // PERSONALITY
        private float _stateTimer;
        private float _individualSpeedFactor;
        private float _individualIdleBias;
        private bool _isBurster;
        private bool _isConstantSwimmer;

        // INANIMATE & VISUALS
        private bool _isInanimate;
        private float _idleDriftSpeed;
        private float _baseRotationOffset;

        // FIX: Store this so we know when to apply user config
        private bool _isVanilla;

        // RENDERING
        private Texture2D? _texture;
        private Rectangle _sourceRect;
        private readonly GameLocation _location;
        private readonly float _scaleVariation;
        private const float BaseScale = 3.5f;

        // CONSTANTS
        private const float DragWater = 0.94f;
        private const float DragIdle = 0.90f;
        private const float BaseBurstPower = 0.15f;
        private const float WhiskerLength = 55f;
        private const float SeparationRadius = 40f;
        private const float SeparationForce = 0.08f;

        private static readonly HashSet<string> InanimateIds = new()
        {
            "(O)152", "(O)153", "(O)157", "(O)167", "(O)168",
            "(O)169", "(O)170", "(O)171", "(O)172", "(O)SpecificBaits"
        };

        public ShadowBoid(Vector2 startPos, string itemId, GameLocation location)
        {
            Position = startPos;
            _location = location;
            _wanderAngle = (float)Game1.random.NextDouble() * MathHelper.TwoPi;

            if (ModEntry.Config.EnableFadeEffects)
            {
                _lifeState = LifeState.Spawning;
                _currentAlpha = 0f;
            }
            else
            {
                _lifeState = LifeState.Active;
                _currentAlpha = 1f;
            }

            var data = ItemRegistry.GetData(itemId);
            string internalName = data?.InternalName ?? "";

            if (InanimateIds.Contains(itemId) ||
                internalName.Contains("Algae") ||
                internalName.Contains("Seaweed") ||
                internalName.Contains("Trash") ||
                internalName.Contains("Driftwood") ||
                internalName.Contains("Weed"))
            {
                _isInanimate = true;
            }

            // ROTATION LOGIC
            // Check if it's a standard vanilla Object ID
            _isVanilla = itemId.StartsWith("(O)") && int.TryParse(itemId.Replace("(O)", ""), out _);

            // Vanilla = 45 deg offset. Modded = 0 deg offset.
            _baseRotationOffset = _isVanilla ? MathHelper.PiOver4 : 0f;

            if (_isInanimate)
            {
                _individualSpeedFactor = 0.2f;
                _individualIdleBias = 1.0f;
                _isBurster = false;
                _isConstantSwimmer = false;
            }
            else
            {
                _individualSpeedFactor = 0.7f + ((float)Game1.random.NextDouble() * 0.6f);
                _individualIdleBias = 0.8f + ((float)Game1.random.NextDouble() * 0.4f);
                _isBurster = Game1.random.NextDouble() < ModEntry.Config.BurstChance;
                _isConstantSwimmer = Game1.random.NextDouble() < ModEntry.Config.ConstantSwimChance;
            }

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

            SwitchState(_isInanimate ? SwimState.Idle : SwimState.Gliding);
        }

        public void StartDespawn()
        {
            if (_lifeState != LifeState.Dead && _lifeState != LifeState.Despawning)
                _lifeState = LifeState.Despawning;
        }

        public void Cleanup() { _texture = null; }

        public void Update(GameTime time, List<ShadowBoid> allShadows)
        {
            HandleLifecycle();
            if (_lifeState == LifeState.Dead) return;

            Vector2 avoidance = GetAvoidanceSteering();

            if (ModEntry.Config.AvoidCrowding && !_isInanimate)
            {
                Vector2 separation = GetSeparationForce(allShadows);
                if (separation != Vector2.Zero)
                {
                    _acceleration += separation;
                }
            }

            if (avoidance != Vector2.Zero)
            {
                float avoidanceStr = _isInanimate ? 0.05f : 0.15f;
                _acceleration += avoidance * avoidanceStr;
                float escapeAngle = (float)Math.Atan2(_velocity.Y + avoidance.Y, _velocity.X + avoidance.X);
                _wanderAngle = LerpRadians(_wanderAngle, escapeAngle, 0.10f);
            }

            if (ModEntry.Config.EnableFishPersonalities)
            {
                UpdateSwimBehavior();
            }
            else
            {
                if (_isInanimate)
                {
                    Vector2 force = new Vector2((float)Math.Cos(_wanderAngle), (float)Math.Sin(_wanderAngle));
                    _acceleration += force * 0.005f;
                }
                else
                {
                    _swimState = SwimState.Accelerating;
                    Vector2 force = new Vector2((float)Math.Cos(_wanderAngle), (float)Math.Sin(_wanderAngle));
                    _acceleration += force * 0.05f;
                }
            }

            _velocity += _acceleration;

            float currentDrag = (_swimState == SwimState.Idle) ? DragIdle : DragWater;
            _velocity *= currentDrag;

            float speedLimit = 1.8f * _individualSpeedFactor * ModEntry.Config.MoveSpeedMultiplier;
            if (_velocity.Length() > speedLimit)
            {
                _velocity.Normalize();
                _velocity *= speedLimit;
            }

            Vector2 nextPos = Position + _velocity;

            if (IsPointWater(nextPos))
            {
                Position = nextPos;

                if (!_isInanimate)
                {
                    if (_velocity.LengthSquared() > 0.02f)
                    {
                        float targetRot = (float)Math.Atan2(_velocity.Y, _velocity.X);
                        _rotation = LerpRadians(_rotation, targetRot, 0.2f);
                    }
                }
                else
                {
                    _rotation += (float)(Math.Sin(time.TotalGameTime.TotalSeconds) * 0.005f);
                }
            }
            else
            {
                _wanderAngle += MathHelper.Pi + (float)((Game1.random.NextDouble() - 0.5));
                _velocity = -_velocity * 0.5f;
                float tileCenterX = ((int)(Position.X / 64) * 64) + 32;
                float tileCenterY = ((int)(Position.Y / 64) * 64) + 32;
                Position += (new Vector2(tileCenterX, tileCenterY) - Position) * 0.1f;
            }

            _acceleration = Vector2.Zero;
        }

        private Vector2 GetSeparationForce(List<ShadowBoid> allShadows)
        {
            Vector2 force = Vector2.Zero;
            int count = 0;
            int checkLimit = 15;
            int totalFish = allShadows.Count;

            for (int i = 0; i < checkLimit; i++)
            {
                int rndIndex = Game1.random.Next(totalFish);
                ShadowBoid other = allShadows[rndIndex];

                if (other == this || !other.IsVisible) continue;

                float distSq = Vector2.DistanceSquared(this.Position, other.Position);

                if (distSq < SeparationRadius * SeparationRadius && distSq > 0.001f)
                {
                    Vector2 push = this.Position - other.Position;
                    push.Normalize();
                    push /= (float)Math.Sqrt(distSq);
                    force += push;
                    count++;
                }
            }

            if (count > 0)
            {
                force /= count;
                force.Normalize();
                force *= SeparationForce;
            }

            return force;
        }

        private void UpdateSwimBehavior()
        {
            _stateTimer -= 1f;

            switch (_swimState)
            {
                case SwimState.Idle:
                    if (_isInanimate)
                    {
                        _wanderAngle += (float)(Game1.random.NextDouble() - 0.5) * 0.02f;
                        Vector2 driftForce = new Vector2((float)Math.Cos(_wanderAngle), (float)Math.Sin(_wanderAngle));
                        _acceleration += driftForce * _idleDriftSpeed;
                        return;
                    }

                    if (_idleDriftSpeed > 0)
                    {
                        _wanderAngle += (float)(Game1.random.NextDouble() - 0.5) * 0.05f;
                        Vector2 driftForce = new Vector2((float)Math.Cos(_wanderAngle), (float)Math.Sin(_wanderAngle));
                        _acceleration += driftForce * _idleDriftSpeed;
                    }

                    if (_stateTimer <= 0)
                    {
                        float turn = (float)(Game1.random.NextDouble() - 0.5) * 2.0f;
                        _wanderAngle += turn;

                        if (_isBurster || _isConstantSwimmer)
                            SwitchState(SwimState.Accelerating);
                        else
                            SwitchState(SwimState.Gliding);
                    }
                    break;

                case SwimState.Accelerating:
                    Vector2 force = new Vector2((float)Math.Cos(_wanderAngle), (float)Math.Sin(_wanderAngle));
                    _acceleration += force * BaseBurstPower * _individualSpeedFactor;

                    if (_stateTimer <= 0)
                        SwitchState(SwimState.Gliding);
                    break;

                case SwimState.Gliding:
                    if (_velocity.Length() < 0.2f * _individualSpeedFactor)
                    {
                        if (_isConstantSwimmer)
                            SwitchState(SwimState.Accelerating);
                        else
                            SwitchState(SwimState.Idle);
                    }
                    else if (_stateTimer <= 0 && (_isBurster || _isConstantSwimmer))
                    {
                        SwitchState(SwimState.Accelerating);
                    }
                    break;
            }
        }

        private void SwitchState(SwimState newState)
        {
            if (_isConstantSwimmer && newState == SwimState.Idle) newState = SwimState.Accelerating;
            _swimState = newState;
            switch (newState)
            {
                case SwimState.Idle:
                    float minFrames = ModEntry.Config.MinIdleSeconds * 60f;
                    float maxFrames = ModEntry.Config.MaxIdleSeconds * 60f;
                    float baseDuration = minFrames + ((float)Game1.random.NextDouble() * (maxFrames - minFrames));
                    _stateTimer = baseDuration * _individualIdleBias;

                    if (_isInanimate)
                    {
                        _idleDriftSpeed = 0.005f + ((float)Game1.random.NextDouble() * 0.01f);
                    }
                    else
                    {
                        if (Game1.random.NextDouble() < 0.4)
                            _idleDriftSpeed = 0f;
                        else
                            _idleDriftSpeed = 0.01f + ((float)Game1.random.NextDouble() * 0.03f);
                    }
                    break;
                case SwimState.Accelerating:
                    _stateTimer = Game1.random.Next(15, 40);
                    break;
                case SwimState.Gliding:
                    _stateTimer = Game1.random.Next(30, 90);
                    break;
            }
        }

        private Vector2 GetAvoidanceSteering()
        {
            float currentSpeed = _velocity.Length();
            float lookAhead = _isInanimate ? 24f : 30f + (currentSpeed * 25f);
            Vector2 forward = _velocity;
            if (forward == Vector2.Zero) forward = new Vector2((float)Math.Cos(_rotation), (float)Math.Sin(_rotation));
            forward.Normalize();
            Vector2 leftWhisker = RotateVector(forward, -0.6f);
            Vector2 rightWhisker = RotateVector(forward, 0.6f);
            bool hitCenter = IsWall(Position + forward * lookAhead);
            bool hitLeft = IsWall(Position + leftWhisker * (lookAhead * 0.8f));
            bool hitRight = IsWall(Position + rightWhisker * (lookAhead * 0.8f));
            if (hitCenter || hitLeft || hitRight)
            {
                Vector2 steering = Vector2.Zero;
                float force = 0.5f;
                if (hitCenter) steering -= forward * force;
                if (hitLeft) steering += rightWhisker * force;
                if (hitRight) steering += leftWhisker * force;
                return steering;
            }
            return Vector2.Zero;
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

        private bool IsWall(Vector2 pos)
        {
            int tileX = (int)(pos.X / 64f);
            int tileY = (int)(pos.Y / 64f);
            if (_location.doesTileHaveProperty(tileX, tileY, "Water", "Back") == null) return true;
            if (_location.getTileIndexAt(tileX, tileY, "Buildings") != -1)
            {
                if (_location.doesTileHaveProperty(tileX, tileY, "Passable", "Buildings") == null) return true;
            }
            if (pos.X < 32 || pos.Y < 32 ||
                pos.X > _location.Map.Layers[0].LayerWidth * 64 - 32 ||
                pos.Y > _location.Map.Layers[0].LayerHeight * 64 - 32) return true;

            float padding = 20f;
            float offX = pos.X % 64;
            float offY = pos.Y % 64;
            if (offX < padding && IsSolidTile(tileX - 1, tileY)) return true;
            if (offX > 64 - padding && IsSolidTile(tileX + 1, tileY)) return true;
            if (offY < padding && IsSolidTile(tileX, tileY - 1)) return true;
            if (offY > 64 - padding && IsSolidTile(tileX, tileY + 1)) return true;

            // DIAGONAL
            if (offX < padding && offY < padding && IsSolidTile(tileX - 1, tileY - 1)) return true;
            if (offX > 64 - padding && offY < padding && IsSolidTile(tileX + 1, tileY - 1)) return true;
            if (offX < padding && offY > 64 - padding && IsSolidTile(tileX - 1, tileY + 1)) return true;
            if (offX > 64 - padding && offY > 64 - padding && IsSolidTile(tileX + 1, tileY + 1)) return true;
            return false;
        }

        private bool IsSolidTile(int x, int y)
        {
            if (_location.doesTileHaveProperty(x, y, "Water", "Back") == null) return true;
            if (_location.getTileIndexAt(x, y, "Buildings") != -1)
            {
                if (_location.doesTileHaveProperty(x, y, "Passable", "Buildings") == null) return true;
            }
            return false;
        }

        private bool IsPointWater(Vector2 pos) { return !IsWall(pos); }

        private void HandleLifecycle()
        {
            float fadeSpeed = ModEntry.Config.EnableFadeEffects ? ModEntry.Config.FadeSpeed : 1.0f;
            switch (_lifeState)
            {
                case LifeState.Spawning:
                    _currentAlpha += fadeSpeed;
                    if (_currentAlpha >= 1f) { _currentAlpha = 1f; _lifeState = LifeState.Active; }
                    break;
                case LifeState.Despawning:
                    _currentAlpha -= fadeSpeed;
                    if (_currentAlpha <= 0f) { _currentAlpha = 0f; _lifeState = LifeState.Dead; }
                    break;
            }
        }

        public void Draw(SpriteBatch b)
        {
            if (_texture == null) return;

            // FIX: Only add User Config Rotation if it's a MODDED fish.
            float userRotation = _isVanilla ? 0f : MathHelper.ToRadians(ModEntry.Config.ModdedFishRotation);
            float rotation = _rotation + _baseRotationOffset + userRotation;

            float globalScale = BaseScale * ModEntry.Config.ShadowScale * _scaleVariation;
            Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, Position);
            float finalOpacity = ModEntry.Config.ShadowOpacity * _currentAlpha;

            // DRAW BLACK SHADOW
            b.Draw(_texture, screenPos, _sourceRect, Color.Black * finalOpacity,
                rotation, new Vector2(_sourceRect.Width / 2f, _sourceRect.Height / 2f),
                globalScale, SpriteEffects.None, 0.001f);
        }
    }
}