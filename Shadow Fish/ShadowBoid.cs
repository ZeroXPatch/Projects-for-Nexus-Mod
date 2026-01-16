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
        private Texture2D? _texture; // Nullable
        private Rectangle _sourceRect;
        private readonly GameLocation _location;
        private readonly float _scaleVariation;
        private float _wanderAngle;

        public ShadowBoid(Vector2 startPos, string itemId, GameLocation location)
        {
            Position = startPos;
            _location = location;
            _wanderAngle = (float)Game1.random.NextDouble() * MathHelper.TwoPi;

            // Lifecycle Setup
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
            _velocity = new Vector2((float)Math.Cos(_wanderAngle), (float)Math.Sin(_wanderAngle));
        }

        public void StartDespawn()
        {
            if (_state != BoidState.Dead && _state != BoidState.Despawning)
            {
                _state = BoidState.Despawning;
            }
        }

        public void Cleanup()
        {
            _texture = null;
        }

        public void Update(GameTime time)
        {
            HandleLifecycle();
            if (_state == BoidState.Dead) return;

            // Physics (Brownian Motion)
            float turnSpeed = 0.05f;
            _wanderAngle += (float)(Game1.random.NextDouble() - 0.5) * turnSpeed;
            float speed = 0.5f;
            Vector2 desiredVelocity = new Vector2((float)Math.Cos(_wanderAngle), (float)Math.Sin(_wanderAngle)) * speed;
            _velocity = Vector2.Lerp(_velocity, desiredVelocity, 0.05f);

            Vector2 nextPos = Position + _velocity;

            if (IsWater(nextPos))
            {
                Position = nextPos;
                if (_velocity.LengthSquared() > 0.001f)
                    _rotation = (float)Math.Atan2(_velocity.Y, _velocity.X);
            }
            else
            {
                _wanderAngle += MathHelper.Pi;
                _velocity = -_velocity;
            }
        }

        private void HandleLifecycle()
        {
            float fadeSpeed = ModEntry.Config.EnableFadeEffects ? ModEntry.Config.FadeSpeed : 1.0f;

            switch (_state)
            {
                case BoidState.Spawning:
                    _currentAlpha += fadeSpeed;
                    if (_currentAlpha >= 1f)
                    {
                        _currentAlpha = 1f;
                        _state = BoidState.Active;
                    }
                    break;

                case BoidState.Despawning:
                    _currentAlpha -= fadeSpeed;
                    if (_currentAlpha <= 0f)
                    {
                        _currentAlpha = 0f;
                        _state = BoidState.Dead;
                    }
                    break;
            }
        }

        private bool IsWater(Vector2 pos)
        {
            return _location.isOpenWater((int)(pos.X / 64f), (int)(pos.Y / 64f));
        }

        public void Draw(SpriteBatch b)
        {
            if (_texture == null) return;

            float rotationOffset = MathHelper.PiOver4;
            float globalScale = 4f * ModEntry.Config.ShadowScale * _scaleVariation;
            Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, Position);

            // Apply the dynamic alpha to the user-configured opacity
            float finalOpacity = ModEntry.Config.ShadowOpacity * _currentAlpha;

            b.Draw(
                _texture,
                screenPos,
                _sourceRect,
                Color.Black * finalOpacity,
                _rotation + rotationOffset,
                new Vector2(_sourceRect.Width / 2f, _sourceRect.Height / 2f),
                globalScale,
                SpriteEffects.None,
                0.001f
            );
        }
    }
}