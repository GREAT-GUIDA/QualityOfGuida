using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria;
using Microsoft.Xna.Framework;

namespace QualityOfGuida.Content.SpawnEgg
{
    public class SpawnEggGlobalNPC : GlobalNPC {
        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Player player = Main.LocalPlayer;
            if (player.HeldItem.ModItem is SpawnEggItem eggItem && eggItem.IsEmpty() && SpawnEggItem.HasBanner(npc)) {

                float interactionRange = Player.tileRangeX * 16f;
                if (Vector2.Distance(player.Center, npc.Center) <= interactionRange && !npc.DoesntDespawnToInactivity()) {

                    string captureText = Language.GetTextValue("Mods.QualityOfGuida.Items.SpawnEggItem.CanCapture");
                    Vector2 textSize = FontAssets.MouseText.Value.MeasureString(captureText) * 0.8f;

                    NPC targetNPC = eggItem.targetNPC;
                    bool isTarget = targetNPC != null && targetNPC.whoAmI == npc.whoAmI;

                    Color textColor = isTarget ? Color.Yellow : Color.Gray;

                    Vector2 textPos = npc.Top - screenPos - new Vector2(textSize.X / 2, textSize.Y + 10);

                    Utils.DrawBorderString(spriteBatch, captureText, textPos, textColor, 0.8f);
                }
            }
        }
    }
}
