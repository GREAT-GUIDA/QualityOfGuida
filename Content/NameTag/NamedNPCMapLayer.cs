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
using QualityOfGuida.Content.NameTag;

namespace QualityOfGuida.Content.NameTag {
    public class NamedNPCMapLayer : ModMapLayer {
        public override void Draw(ref MapOverlayDrawContext context, ref string text) {
            const float scaleIfNotSelected = 0.8f;
            const float scaleIfSelected = scaleIfNotSelected * 1.3f;

            Texture2D iconTexture = ModAsset.NamedNPCIcon.Value;
            Texture2D kingIconTexture = ModAsset.NamedNPCKingIcon.Value;

            foreach (NPC npc in Main.npc) {
                if (!npc.active) continue;
                if (npc.boss || npc.townNPC) continue;
                if (NPCNamingSystem.NPCHasCustomName(npc)) {
                    Vector2 npcMapPosition = new Vector2(
                        npc.Center.X / 16f,
                        npc.Center.Y / 16f
                    );

                    var drawResult = context.Draw(
                        iconTexture,
                        npcMapPosition,
                        Color.White,
                        new SpriteFrame(1, 1, 0, 0),
                        scaleIfNotSelected,
                        scaleIfSelected,
                        Alignment.Center
                    );

                    if (drawResult.IsMouseOver) {
                        text = NPCNamingSystem.GetNPCName(npc);
                    }
                }
            }

            var revengeSystem = ModContent.GetInstance<SimpleNamedNPCRevengeSystem>();
            if (revengeSystem != null && revengeSystem.markers != null) {
                foreach (var marker in revengeSystem.markers) {
                    Vector2 markerMapPosition = new Vector2(
                        marker.Position.X / 16f,
                        marker.Position.Y / 16f
                    );

                    var drawResult = context.Draw(
                        marker.CustomName.ToLower().Contains("king") ? kingIconTexture : iconTexture,
                        markerMapPosition,
                        Color.White,
                        new SpriteFrame(1, 1, 0, 0),
                        scaleIfNotSelected,
                        scaleIfSelected,
                        Alignment.Center
                    );

                    if (drawResult.IsMouseOver) {
                        text = marker.CustomName;
                    }
                }
            }
        }
    }
}