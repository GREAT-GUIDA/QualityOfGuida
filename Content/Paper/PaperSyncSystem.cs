using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;

namespace QualityOfGuida.Content.Paper {
    // 只需要添加一个简单的ModSystem处理网络消息
    public class PaperSyncSystem : ModSystem {
        public override void PostSetupContent() {
            NetworkManager.RegisterHandler("SyncPaperContent", HandlePaperContentSync);
            On_Main.SubmitSignText += On_Main_SubmitSignText;
        }


        private void On_Main_SubmitSignText(On_Main.orig_SubmitSignText orig) {
            var editWatcher = ModContent.GetInstance<PaperEditWatcher>();
            if (editWatcher._isEditing) {
                int signIndex = Main.player[Main.myPlayer].sign;
                Sign.TextSign(signIndex, Main.npcChatText);
                Main.editSign = false;
                return;
            }
            orig();
        }
        private static void HandlePaperContentSync(BinaryReader reader, int whoAmI) {
            if (Main.netMode != NetmodeID.Server) return;

            int playerIndex = reader.ReadInt32();
            int itemSlot = reader.ReadInt32();
            string newContent = reader.ReadString();

            if (playerIndex >= 0 && playerIndex < Main.maxPlayers && Main.player[playerIndex].active) {
                Player player = Main.player[playerIndex];
                if (itemSlot >= 0 && itemSlot < player.inventory.Length &&
                    player.inventory[itemSlot]?.ModItem is PaperItem paperItem) {
                    paperItem.SetContent(newContent);
                }
            }
        }

        public static void SyncPaperToServer(Player player, int itemSlot, string content) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                NetworkManager.SendMessage("SyncPaperContent", writer => {
                    writer.Write(player.whoAmI);
                    writer.Write(itemSlot);
                    writer.Write(content ?? "");
                });
            }
        }
    }
}
