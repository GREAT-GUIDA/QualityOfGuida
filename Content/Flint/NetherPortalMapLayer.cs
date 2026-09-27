using GuidaSharedCode;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.Map;
using Terraria.UI;
using System.Linq;

namespace QualityOfGuida.Content.Flint {
    public class NetherPortalMapLayer : ModMapLayer {
        public override void Draw(ref MapOverlayDrawContext context, ref string text) {
            const float scaleIfNotSelected = 1f;
            const float scaleIfSelected = scaleIfNotSelected * 1.5f;
            
            Texture2D portalTexture = ModAsset.NetherPortalIcon.Value;
            /*
            var entities = TileEntity.ByID.Values.ToList();
            // 遍历所有地狱门实体
            foreach (var entity in entities) {
                if (entity is NetherPortalTileEntity portalEntity) {
                    Vector2 portalPosition = new Vector2(
                        portalEntity.Position.X + 2,
                        portalEntity.Position.Y + 2.5f
                    );

                    var drawResult = context.Draw(
                        portalTexture,
                        portalPosition,
                        Color.White,
                        new SpriteFrame(1, 1, 0, 0),
                        scaleIfNotSelected,
                        scaleIfSelected,
                        Alignment.Center
                    );
                    
                    if (drawResult.IsMouseOver) {
                        text = Language.GetTextValue(Mod.GetLocalizationKey("Tiles.NetherPortal.MapEntry"));
                    }
                }
            }*/
        }
    }
}