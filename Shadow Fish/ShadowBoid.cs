using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using System;

namespace ShadowsOfTheDeep
{
    public class ShadowBoid
    {
        public Vector2 Position;
        public bool ShouldDespawn = false;

        private Vector2 _velocity;
        private float _rotation;
        private readonly Texture2D _texture;
        private readonly Rectangle _sourceRect;
        private readonly GameLocation _location;
        private readonly float _scaleVariation;
        private float _wanderAngle;

        public ShadowBoid(Vector2 startPos, string itemId, GameLocation location)
        {
            Position = startPos;
            _location = location;

            // Random start angle
            _wanderAngle = (float)Game1.random.NextDouble() * MathHelper.TwoPi;

            // Retrieve Texture Data safely
            var data = ItemRegistry.GetData(itemId);
            if (data != null)
            {
                _texture = data.GetTexture();
                _sourceRect = data.GetSourceRect();
            }
            else
            {
                // Fallback texture (Error item sprite)
                _texture = Game1.objectSpriteSheet;
                _sourceRect = Game1.getSourceRectForStandardTileSheet(Game1.objectSpriteSheet, 168, 16, 16);
            }

            // Randomize size slightly
            _scaleVariation = (float)(0.8 + (Game1.random.NextDouble() * 0.4));
            _velocity = new Vector2((float)Math.Cos(_wanderAngle), (float)Math.Sin(_wanderAngle));
        }

        public void Update(GameTime time)
        {
            // Physics: Simple Brownian Motion
            float turnSpeed = 0.05f;
            _wanderAngle += (float)(Game1.random.NextDouble() - 0.5) * turnSpeed;

            float speed = 0.5f; // Constant swim speed
            Vector2 desiredVelocity = new Vector2((float)Math.Cos(_wanderAngle), (float)Math.Sin(_wanderAngle)) * speed;

            // Smoothly interpolate velocity
            _velocity = Vector2.Lerp(_velocity, desiredVelocity, 0.05f);

            Vector2 nextPos = Position + _velocity;

            // Wall Collision Check
            if (IsWater(nextPos))
            {
                Position = nextPos;

                // Rotation Logic:
                // Standard Item Sprites face diagonal (Up-Right).
                // We calculate the angle of movement, but we must ignore rotation if velocity is near zero (floating).
                if (_velocity.LengthSquared() > 0.001f)
                {
                    _rotation = (float)Math.Atan2(_velocity.Y, _velocity.X);
                }
            }
            else
            {
                // Hit "Land" (Non-water tile) -> Bounce back
                _wanderAngle += MathHelper.Pi;
                _velocity = -_velocity;
            }
        }

        private bool IsWater(Vector2 pos)
        {
            // Convert pixel to tile coordinates
            return _location.isOpenWater((int)(pos.X / 64f), (int)(pos.Y / 64f));
        }

        public void Draw(SpriteBatch b)
        {
            if (_texture == null) return;

            // Rotation Offset:
            // Most fish icons in SDV are drawn facing Top-Right (45 degrees).
            // To make them point in the direction of travel, we offset the rotation.
            float rotationOffset = MathHelper.PiOver4;

            float globalScale = 4f * ModEntry.Config.ShadowScale * _scaleVariation;
            Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, Position);

            // SILHOUETTE RENDERING
            // By drawing with Color.Black * Opacity, we ignore the texture's colors
            // and simply draw its shape.
            b.Draw(
                _texture,
                screenPos,
                _sourceRect,
                Color.Black * ModEntry.Config.ShadowOpacity,
                _rotation + rotationOffset,
                new Vector2(_sourceRect.Width / 2f, _sourceRect.Height / 2f), // Center Origin
                globalScale,
                SpriteEffects.None,
                0.001f // Very low layer depth (handled by patch order mostly)
            );
        }
    }
}