using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace LoadingProgressBar.Services
{
    /// <summary>
    /// Service for displaying a visual progress bar during day transitions
    /// </summary>
    public static class ProgressService
    {
        private static bool isActive = false;
        private static float currentProgress = 0f;
        private static string currentMessage = "";
        
        /// <summary>
        /// Start showing the progress bar
        /// </summary>
        public static void Start()
        {
            isActive = true;
            currentProgress = 0f;
            currentMessage = "Loading...";
        }
        
        /// <summary>
        /// Update the progress bar
        /// </summary>
        /// <param name="message">Status message to display</param>
        /// <param name="progress">Progress from 0.0 to 1.0</param>
        public static void UpdateProgress(string message, float progress)
        {
            currentMessage = message;
            currentProgress = Math.Min(1.0f, Math.Max(0f, progress));
        }
        
        /// <summary>
        /// Hide the progress bar
        /// </summary>
        public static void Complete()
        {
            currentProgress = 1.0f;
            currentMessage = "Complete!";
            
            // Keep showing for a brief moment
            System.Threading.Tasks.Task.Delay(500).ContinueWith(_ => 
            {
                isActive = false;
            });
        }
        
        /// <summary>
        /// Draw the progress bar on screen
        /// </summary>
        public static void Draw(SpriteBatch spriteBatch)
        {
            if (!isActive || !ModEntry.Config.ShowProgressBar)
                return;
            
            try
            {
                int barWidth = ModEntry.Config.BarWidth;
                int barHeight = ModEntry.Config.BarHeight;
                
                // Center the bar horizontally, place near bottom
                int x = (Game1.uiViewport.Width - barWidth) / 2;
                int y = Game1.uiViewport.Height - 120;
                
                // Draw shadow
                DrawRectangle(spriteBatch, x - 2, y - 2, barWidth + 4, barHeight + 4, Color.Black);
                
                // Draw background
                DrawRectangle(spriteBatch, x, y, barWidth, barHeight, new Color(30, 30, 30));
                
                // Draw progress fill
                int fillWidth = (int)((barWidth - 8) * currentProgress);
                Color fillColor = GetProgressColor(currentProgress);
                DrawRectangle(spriteBatch, x + 4, y + 4, fillWidth, barHeight - 8, fillColor);
                
                // Draw border
                DrawBorder(spriteBatch, x, y, barWidth, barHeight, Color.White, 2);
                
                // Draw message text
                if (ModEntry.Config.ShowMessage)
                {
                    Vector2 textSize = Game1.smallFont.MeasureString(currentMessage);
                    Vector2 textPosition = new Vector2(
                        x + (barWidth - textSize.X) / 2,
                        y + (barHeight - textSize.Y) / 2
                    );
                    
                    // Text shadow
                    spriteBatch.DrawString(Game1.smallFont, currentMessage, 
                        textPosition + new Vector2(2, 2), Color.Black);
                    
                    // Text
                    spriteBatch.DrawString(Game1.smallFont, currentMessage, 
                        textPosition, Color.White);
                }
                
                // Draw percentage
                if (ModEntry.Config.ShowPercentage)
                {
                    string percentText = $"{(currentProgress * 100):F0}%";
                    Vector2 percentSize = Game1.tinyFont.MeasureString(percentText);
                    Vector2 percentPosition = new Vector2(
                        x + barWidth - percentSize.X - 10,
                        y - percentSize.Y - 5
                    );
                    
                    spriteBatch.DrawString(Game1.tinyFont, percentText, 
                        percentPosition + new Vector2(1, 1), Color.Black);
                    spriteBatch.DrawString(Game1.tinyFont, percentText, 
                        percentPosition, Color.LightGray);
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error drawing progress bar: {ex.Message}", 
                    StardewModdingAPI.LogLevel.Error);
                isActive = false;
            }
        }
        
        private static void DrawRectangle(SpriteBatch b, int x, int y, int width, int height, Color color)
        {
            b.Draw(Game1.staminaRect, new Rectangle(x, y, width, height), color);
        }
        
        private static void DrawBorder(SpriteBatch b, int x, int y, int width, int height, Color color, int thickness)
        {
            // Top
            DrawRectangle(b, x, y, width, thickness, color);
            // Bottom
            DrawRectangle(b, x, y + height - thickness, width, thickness, color);
            // Left
            DrawRectangle(b, x, y, thickness, height, color);
            // Right
            DrawRectangle(b, x + width - thickness, y, thickness, height, color);
        }
        
        private static Color GetProgressColor(float progress)
        {
            // Gradient: yellow -> yellow-green -> green
            if (progress < 0.33f)
            {
                return new Color(255, 200, 50); // Yellow
            }
            else if (progress < 0.66f)
            {
                return new Color(200, 220, 50); // Yellow-green
            }
            else
            {
                return new Color(100, 220, 100); // Green
            }
        }
    }
}
