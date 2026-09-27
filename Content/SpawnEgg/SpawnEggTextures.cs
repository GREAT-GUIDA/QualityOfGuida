using GuidaSharedCode;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace QualityOfGuida.Content.SpawnEgg {
    public static class SpawnEggTextures {
        public static void Regenerate(int storedBannerID, ref RenderTarget2D cachedTexture) {
            cachedTexture?.Dispose();
            cachedTexture = null;

            if (Main.graphics?.GraphicsDevice == null) {
                return;
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            RenderTarget2D previousTarget = device.GetRenderTargets() is { Length: > 0 } targets
                ? targets[0].RenderTarget as RenderTarget2D
                : null;

            try {
                int bannerItem = Item.BannerToItem(storedBannerID);
                string filledName = $"SpawnEggItem_Filled{bannerItem}";
                bool hasAsset = ModAssetSpawnEgg.Has(filledName);

                if (storedBannerID == 0 || hasAsset) {
                    Texture2D emptyTexture = ModAsset.SpawnEggItem.Value;
                    if (hasAsset) {
                        emptyTexture = ModAssetSpawnEgg.GetTexture(filledName);
                    }

                    if (emptyTexture == null) {
                        return;
                    }

                    cachedTexture = new RenderTarget2D(device, emptyTexture.Width, emptyTexture.Height);
                    device.SetRenderTarget(cachedTexture);
                    device.Clear(Color.Transparent);

                    Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
                    Main.spriteBatch.Draw(emptyTexture, Vector2.Zero, Color.LightGray);
                    Main.spriteBatch.End();

                    device.SetRenderTarget(previousTarget ?? Main.screenTarget);
                    return;
                }

                SpawnEggBannerColors.ColorPair colorPair = SpawnEggBannerColors.GetColorPair(storedBannerID);
                Texture2D primaryTexture = colorPair.HasSecondary
                    ? ModAsset.SpawnEggItem_Filled1.Value
                    : ModAsset.SpawnEggItem_Filled5.Value;
                Texture2D secondaryTexture = colorPair.HasSecondary
                    ? ModAsset.SpawnEggItem_Filled3.Value
                    : null;

                if (primaryTexture == null) {
                    return;
                }

                cachedTexture = new RenderTarget2D(device, primaryTexture.Width, primaryTexture.Height);
                device.SetRenderTarget(cachedTexture);
                device.Clear(Color.Transparent);

                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
                Main.spriteBatch.Draw(primaryTexture, Vector2.Zero, colorPair.Primary);

                if (colorPair.HasSecondary && secondaryTexture != null) {
                    Main.spriteBatch.Draw(secondaryTexture, Vector2.Zero, colorPair.Secondary);
                }

                Main.spriteBatch.End();
                device.SetRenderTarget(previousTarget ?? Main.screenTarget);
            } catch (Exception ex) {
                cachedTexture?.Dispose();
                cachedTexture = null;
                ModContent.GetInstance<QualityOfGuida>().Logger.Error($"Failed to generate cached texture: {ex.Message}");
            }
        }
    }
}
