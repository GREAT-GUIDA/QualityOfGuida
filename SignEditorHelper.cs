using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace QualityOfGuida {
    public static class SignEditorHelper {
        public static int OpenEditor(Player player, string initialText) {
            Main.editChest = false;
            Main.SetNPCShopIndex(0);
            Main.playerInventory = false;
            Main.InGuideCraftMenu = false;
            player.SetTalkNPC(-1);

            Main.editSign = true;
            Main.npcChatText = initialText ?? "";

            int signIndex = FindSafeSignIndex();
            player.sign = signIndex;

            if (Main.sign[signIndex] == null) {
                Main.sign[signIndex] = new Sign();
            }

            Point anchorTile = FindNearestEmptyTile(player);
            Main.sign[signIndex].x = anchorTile.X;
            Main.sign[signIndex].y = anchorTile.Y;
            Main.sign[signIndex].text = initialText ?? "";

            return signIndex;
        }

        public static void CloseEditor(int signIndex) {
            if (signIndex >= 0 && signIndex < Main.sign.Length && Main.sign[signIndex] != null) {
                Main.sign[signIndex].text = "";
            }

            Main.LocalPlayer.sign = -1;
            Main.npcChatText = "";
        }

        public static int FindSafeSignIndex() {
            for (int i = Main.sign.Length - 1; i >= Main.sign.Length - 50; i--) {
                if (Main.sign[i] == null || string.IsNullOrEmpty(Main.sign[i].text)) {
                    return i;
                }
            }

            return Main.sign.Length - 1;
        }

        public static Point FindNearestEmptyTile(Player player) {
            int playerTileX = (int)(player.position.X / 16);
            int playerTileY = (int)(player.position.Y / 16);

            if (!Main.tile[playerTileX, playerTileY].HasTile) {
                return new Point(playerTileX, playerTileY);
            }

            const int maxRadius = 10;
            for (int radius = 1; radius <= maxRadius; radius++) {
                for (int dx = -radius; dx <= radius; dx++) {
                    for (int dy = -radius; dy <= radius; dy++) {
                        if (Math.Abs(dx) != radius && Math.Abs(dy) != radius) {
                            continue;
                        }

                        int checkX = playerTileX + dx;
                        int checkY = playerTileY + dy;

                        if (checkX < 0 || checkX >= Main.maxTilesX || checkY < 0 || checkY >= Main.maxTilesY) {
                            continue;
                        }

                        if (!Main.tile[checkX, checkY].HasTile) {
                            return new Point(checkX, checkY);
                        }
                    }
                }
            }

            return new Point(playerTileX, playerTileY);
        }

        public static void DrawPanelPortrait(SpriteBatch sb, Rectangle panel, Asset<Texture2D> textureAsset, float scale = 2f) {
            if (textureAsset == null) {
                return;
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, null, null, null,
                Main.UIScaleMatrix);

            Texture2D texture = textureAsset.Value;
            Vector2 drawPos = panel.Location.ToVector2() + new Vector2(17 + 46, 18 + 47);
            Vector2 origin = texture.Size() / 2f;
            sb.Draw(texture, drawPos, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.SamplerStateForCursor, DepthStencilState.None,
                RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }
    }
}
