using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using Terraria.Audio;
using Microsoft.Xna.Framework;
using Terraria.Localization;

namespace QualityOfGuida.Content.NameTag
{
    // 只需要添加一个简单的ModSystem处理网络消息
    public class NameTagSyncSystem : ModSystem {
        public override void PostSetupContent() {
            NetworkManager.RegisterHandler("SyncNameTagContent", HandleNameTagContentSync);
            NetworkManager.RegisterHandler("RequestNaming", HandleNamingRequest);
            NetworkManager.RegisterHandler("UpdateNPCName", HandleNPCNameUpdate);

            On_Main.SubmitSignText += On_Main_SubmitSignText;
        }
        private void On_Main_SubmitSignText(On_Main.orig_SubmitSignText orig) {
            var editWatcher = ModContent.GetInstance<NameTagEditWatcher>();
            if (editWatcher._isEditing) {
                int signIndex = Main.player[Main.myPlayer].sign;
                Sign.TextSign(signIndex, Main.npcChatText);
                Main.editSign = false;
                return;
            }
            orig();
        }
        public void SendNamingRequest(NPC targetNPC, string nameContent) {
            if (Main.netMode != NetmodeID.MultiplayerClient || targetNPC == null) return;

            NetworkManager.SendMessage("RequestNaming", writer => {
                writer.Write(targetNPC.whoAmI);
                writer.Write(targetNPC.type);
                writer.Write(targetNPC.Center.X);
                writer.Write(targetNPC.Center.Y);
                writer.Write(nameContent ?? "");
            });
        }

        private static void HandleNamingRequest(BinaryReader reader, int whoAmI) {
            if (Main.netMode != NetmodeID.Server) return;

            try {
                int npcWhoAmI = reader.ReadInt32();
                int npcType = reader.ReadInt32();
                float npcX = reader.ReadSingle();
                float npcY = reader.ReadSingle();
                string nameContent = reader.ReadString();

                NPC targetNPC = FindTargetNPC(npcWhoAmI, npcType, new Vector2(npcX, npcY));
                if (targetNPC != null) {
                    bool success = ProcessNamingOnServer(targetNPC, nameContent);

                    if (success) {
                        BroadcastNPCNameUpdate(targetNPC, nameContent);
                    }
                }
            } catch (Exception ex) {
                ModContent.GetInstance<QualityOfGuida>().Logger.Error($"Error handling naming request: {ex.Message}");
            }
        }

        private static NPC FindTargetNPC(int whoAmI, int type, Vector2 position) {
            if (whoAmI >= 0 && whoAmI < Main.maxNPCs && Main.npc[whoAmI].active &&
                Main.npc[whoAmI].type == type && Vector2.Distance(Main.npc[whoAmI].Center, position) < 50f) {
                return Main.npc[whoAmI];
            }

            foreach (NPC npc in Main.npc) {
                if (npc.active && npc.type == type && Vector2.Distance(npc.Center, position) < 50f) {
                    return npc;
                }
            }

            return null;
        }

        private static bool ProcessNamingOnServer(NPC npc, string nameContent) {
            try {
                if (string.IsNullOrEmpty(nameContent)) {
                    ModContent.GetInstance<SimpleNamedNPCRevengeSystem>().StopTracking(npc.whoAmI);
                    NPCNamingSystem.SetNPCName(npc, null);
                } else {
                    NPCNamingSystem.SetNPCName(npc, nameContent);
                    ModContent.GetInstance<SimpleNamedNPCRevengeSystem>().StartTracking(npc.whoAmI, nameContent);
                }

                return true;
            } catch (Exception ex) {
                ModContent.GetInstance<QualityOfGuida>().Logger.Error($"Error processing naming: {ex.Message}");
                return false;
            }
        }

        public static void BroadcastNPCNameUpdate(NPC npc, string nameContent) {
            var globalNPC = npc.GetGlobalNPC<NPCNamingSystem>();

            NetworkManager.SendMessage("UpdateNPCName", writer => {
                writer.Write(npc.whoAmI);
                writer.Write(npc.type);
                writer.Write(npc.Center.X);
                writer.Write(npc.Center.Y);
                writer.Write(nameContent ?? "");

                // 发送所有NPC状态参数
                writer.Write(npc.scale);
                writer.Write(npc.lifeMax);
                writer.Write(npc.life);
                writer.Write(npc.damage);
                writer.Write(npc.boss);
                writer.Write(npc.width);
                writer.Write(npc.height);
                writer.Write(globalNPC.effectsApplied);
                writer.Write(globalNPC.wasBoss);

                // 发送音乐信息
                int music = (npc.ModNPC != null) ? npc.ModNPC.Music : -1;
                writer.Write(music);
            });
        }
        private static void HandleNPCNameUpdate(BinaryReader reader, int whoAmI) {
            if (Main.netMode != NetmodeID.MultiplayerClient) return;

                int npcWhoAmI = reader.ReadInt32();
                int npcType = reader.ReadInt32();
                float npcX = reader.ReadSingle();
                float npcY = reader.ReadSingle();
                string nameContent = reader.ReadString();

                // 读取所有状态参数
                float scale = reader.ReadSingle();
                int lifeMax = reader.ReadInt32();
                int life = reader.ReadInt32();
                int damage = reader.ReadInt32();
                bool isBoss = reader.ReadBoolean();
                int width = reader.ReadInt32();
                int height = reader.ReadInt32();
                bool effectsApplied = reader.ReadBoolean();
                bool wasBoss = reader.ReadBoolean();
                int music = reader.ReadInt32();

                NPC targetNPC = FindTargetNPC(npcWhoAmI, npcType, new Vector2(npcX, npcY));
                if (targetNPC != null) {
                    var globalNPC = targetNPC.GetGlobalNPC<NPCNamingSystem>();

                    globalNPC.SetDirectState(nameContent, effectsApplied, wasBoss);
                    targetNPC.GivenName = nameContent;
                    targetNPC.scale = scale;
                    targetNPC.lifeMax = lifeMax;
                    targetNPC.life = life;
                    targetNPC.damage = damage;
                    targetNPC.boss = isBoss;
                    targetNPC.width = width;
                    targetNPC.height = height;

                    if (targetNPC.ModNPC != null && music != -1) {
                        targetNPC.ModNPC.Music = music;
                    }

                    // 播放特效和音效
                    SoundEngine.PlaySound(SoundID.Item1, targetNPC.Center);
                    CreateNamingEffect(targetNPC, !string.IsNullOrEmpty(nameContent));

                    // 如果是King类型且成为了boss，播放觉醒音效
                    if (!string.IsNullOrEmpty(nameContent) && nameContent.ToLower().Contains("king") && isBoss && !wasBoss) {
                        SoundEngine.PlaySound(SoundID.Roar, targetNPC.Center);
                        if (Main.netMode == NetmodeID.SinglePlayer) {
                            Main.NewText(Language.GetTextValue("Announcement.HasAwoken", targetNPC.TypeName), 175, 75);
                        }
                    }
                }
        }

        private static void CreateNamingEffect(NPC npc, bool isNaming) {
            int dustType = isNaming ? DustID.MagicMirror : DustID.Smoke;
            int dustCount = isNaming ? 12 : 8;

            for (int i = 0; i < dustCount; i++) {
                Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, dustType);
                dust.velocity = Vector2.One.RotatedByRandom(MathHelper.TwoPi) * Main.rand.NextFloat(0.5f, 1.5f);
                dust.scale = 0.8f + Main.rand.NextFloat(0.4f);
                if (!isNaming) dust.alpha = 100;
            }
        }

        private static void HandleNameTagContentSync(BinaryReader reader, int whoAmI) {
            if (Main.netMode != NetmodeID.Server) return;

            int playerIndex = reader.ReadInt32();
            int itemSlot = reader.ReadInt32();
            string newContent = reader.ReadString();

            if (playerIndex >= 0 && playerIndex < Main.maxPlayers && Main.player[playerIndex].active) {
                Player player = Main.player[playerIndex];
                if (itemSlot >= 0 && itemSlot < player.inventory.Length &&
                    player.inventory[itemSlot]?.ModItem is NameTagItem nameTagItem) {
                    nameTagItem.SetContent(newContent);
                }
            }
        }

        public static void SyncNameTagToServer(Player player, int itemSlot, string content) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                NetworkManager.SendMessage("SyncNameTagContent", writer => {
                    writer.Write(player.whoAmI);
                    writer.Write(itemSlot);
                    writer.Write(content ?? "");
                });
            }
        }
    }
}
