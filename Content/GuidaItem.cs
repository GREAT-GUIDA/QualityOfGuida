using Humanizer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using ReLogic.Content;
using System;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics;
using Terraria.Graphics.Renderers;
using Terraria.ModLoader;

namespace QualityOfGuida.Content {
    public abstract class GuidaItem : ModItem {
        public virtual string GetDynamicDisplayName() {
            return null; // 默认不使用动态名称
        }
        public virtual string GetDynamicTexturePath() {
            return null; // 默认不使用动态贴图
        }
        public virtual void UpdateDynamicProperties() {
            // 默认不做任何处理
        }
        public void RefreshItemDisplay() {
            UpdateItemName();
            UpdateDynamicProperties();
        }
        public void UpdateItemName() {
            if (!string.IsNullOrEmpty(GetDynamicDisplayName())) {
                string displayName = GetDynamicDisplayName();
                if (!string.IsNullOrEmpty(displayName)) {
                    Item.SetNameOverride(displayName);
                }
            }
        }
        public virtual Texture2D GetDynamicTexture() {
            if (string.IsNullOrEmpty(GetDynamicTexturePath())) {
                return null;
            }

            string texturePath = GetDynamicTexturePath();
            if (!string.IsNullOrEmpty(texturePath)) {
                return ModContent.Request<Texture2D>(texturePath).Value;
            }
            return null;
        }

        public override void SetDefaults() {
            //RefreshItemDisplay();
        }
        public override void UpdateInventory(Player player) {
            RefreshItemDisplay();
        }
        public override void Update(ref float gravity, ref float maxFallSpeed) {
            RefreshItemDisplay();
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            Texture2D dynamicTexture = GetDynamicTexture();
            if (dynamicTexture != null) {
                spriteBatch.Draw(dynamicTexture, position, frame, drawColor, 0f, origin, scale, SpriteEffects.None, 0f);
                return false;
            }
            return true;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI) {
            Texture2D dynamicTexture = GetDynamicTexture();
            if (dynamicTexture != null) {
                Vector2 position = Item.position - Main.screenPosition + new Vector2(Item.width / 2, Item.height / 2);
                Vector2 origin = new Vector2(dynamicTexture.Width / 2, dynamicTexture.Height / 2);
                spriteBatch.Draw(dynamicTexture, position, null, lightColor, rotation, origin, scale, SpriteEffects.None, 0f);
                return false;
            }
            return true;
        }

        public override void HoldItem(Player player) {
            Texture2D dynamicTexture = GetDynamicTexture();
            if (dynamicTexture != null) {
                GuidaItemHook.shouldReplaceTexture = true;
                GuidaItemHook.originalTexture = TextureAssets.Item[Item.type].Value;
                GuidaItemHook.newTexture = dynamicTexture;
            }
        }
    }
    public class GuidaItemHook : ModSystem {
        public static bool shouldReplaceTexture = false;
        public static int replaceType;
        public static Texture2D newTexture;
        public static Texture2D originalTexture;
        public override void PostSetupContent() {
            //On_PlayerDrawLayers.DrawPlayer_27_HeldItem += HookDrawPlayer_27_HeldItem;
            On_PlayerDrawLayers.DrawPlayer_RenderAllLayers += HookDrawPlayer_27_HeldItem;
        }


        private static void HookDrawPlayer_27_HeldItem(On_PlayerDrawLayers.orig_DrawPlayer_RenderAllLayers orig, ref PlayerDrawSet drawinfo) {
            if (shouldReplaceTexture) {
                for (int i = 0; i < drawinfo.DrawDataCache.Count; i++) {
                    DrawData drawData = drawinfo.DrawDataCache[i];
                    if (drawData.texture == originalTexture) {
                        drawData.texture = newTexture;
                        drawinfo.DrawDataCache[i] = drawData;
                    }
                }
            }
            orig(ref drawinfo);
        }
    }
}
