using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace QualityOfGuida {
    public abstract class GuidaItem : ModItem {
        public virtual string GetDynamicDisplayName() => null;

        public virtual Asset<Texture2D> GetDynamicTextureAsset() => null;

        public virtual void UpdateDynamicProperties() { }

        public void RefreshItemDisplay() {
            UpdateItemName();
            UpdateDynamicProperties();
        }

        public void UpdateItemName() {
            string displayName = GetDynamicDisplayName();
            if (!string.IsNullOrEmpty(displayName)) {
                Item.SetNameOverride(displayName);
            }
        }

        public virtual Texture2D GetDynamicTexture() => GetDynamicTextureAsset()?.Value;

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
    }
}
