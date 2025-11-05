using Microsoft.Xna.Framework;
using QualityOfGuida.Content.Particles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent;

namespace QualityOfGuida.Content.Flint
{
    class NetherPortalNet : ModSystem
    {

        public override void PostSetupContent() {
            NetworkManager.RegisterHandler("RequestAllPortals", HandleRequestAllPortals);
            base.PostSetupContent();
            // 添加传送门网络消息处理
            NetworkManager.RegisterHandler("NetherPortalTeleport", HandleNetherPortalTeleport);
            NetworkManager.RegisterHandler("NetherPortalEffect", HandleNetherPortalEffect);
            NetworkManager.RegisterHandler("ExecutePlayerTeleport", HandleExecutePlayerTeleport); // 新增
            base.PostSetupContent();
        }

        private static void HandleExecutePlayerTeleport(BinaryReader reader, int whoAmI) {
            if (Main.netMode == NetmodeID.Server) return; // 只在客户端执行

            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            Vector2 targetPos = new Vector2(x, y);

            Player localPlayer = Main.LocalPlayer;

            // 在客户端执行实际传送
            if (localPlayer != null && localPlayer.active) {
                ExecuteLocalTeleportStatic(localPlayer, targetPos);
            }
        }

        private static void ExecuteLocalTeleportStatic(Player player, Vector2 targetPos) {
            // 将ExecuteLocalTeleport的逻辑提取为静态方法
            try {
                player.environmentBuffImmunityTimer = 4;
                player.RemoveAllGrapplingHooks();
                player.StopVanityActions();

                if (player.shimmering || player.shimmerWet) {
                    player.shimmering = false;
                    player.shimmerWet = false;
                    player.wet = false;
                    player.ClearBuff(353);
                }

                Vector2 oldPos = player.position;
                Main.TeleportEffect(player.getRect(), 0, 0, 1f, TeleportationSide.Entry, targetPos);

                PressurePlateHelper.UpdatePlayerPosition(player);
                player.position = targetPos;
                player.fallStart = (int)(player.position.Y / 16f);

                if (player.whoAmI == Main.myPlayer) {
                    float distance = Vector2.Distance(oldPos, targetPos);

                    if (distance > new Vector2(Main.screenWidth, Main.screenHeight).Length() / 2f + 100f) {
                        NPC.ResetNetOffsets();
                        Main.BlackFadeIn = 255;
                        Lighting.Clear();
                        Main.screenLastPosition = Main.screenPosition;
                        Main.screenPosition.X = player.position.X + (float)(player.width / 2) - (float)(Main.screenWidth / 2);
                        Main.screenPosition.Y = player.position.Y + (float)(player.height / 2) - (float)(Main.screenHeight / 2);
                        Main.instantBGTransitionCounter = 10;
                        player.ForceUpdateBiomes();
                    } else {
                        Main.SetCameraLerp(0.1f, 0);
                    }

                    if (Main.mapTime < 5) Main.mapTime = 5;
                    Main.maxQ = true;
                    Main.renderNow = true;
                }

                PressurePlateHelper.UpdatePlayerPosition(player);
                player.ResetAdvancedShadows();

                for (int i = 0; i < 3; i++) {
                    player.UpdateSocialShadow();
                }

                player.oldPosition = player.position + player.BlehOldPositionFixer;
                Main.TeleportEffect(player.getRect(), 0, 0, 1f, TeleportationSide.Exit, oldPos);

                SoundEngine.PlaySound(ModAssets.PortalArrive, player.position);

            } catch {
                // 传送失败处理
            }
        }
        private static void HandleRequestAllPortals(BinaryReader reader, int whoAmI) {
            if (Main.netMode != NetmodeID.Server) return;

            // 扫描所有传送门位置并强制同步tile区域
            foreach (var entity in TileEntity.ByID.Values.OfType<NetherPortalTileEntity>()) {
                // 发送传送门所在的tile区域，这会自动同步TileEntity
                NetMessage.SendTileSquare(whoAmI, entity.Position.X, entity.Position.Y, 4, 5);

                // 小延迟确保数据正确到达
                Main.RunOnMainThread(() => {
                    // 再发送一次TileEntitySharing确保同步
                    NetMessage.SendData(MessageID.TileEntitySharing, whoAmI, -1, null,
                        entity.ID, entity.Position.X, entity.Position.Y);
                });
            }
        }

        public override void PostUpdateWorld() {
            // 客户端请求所有传送门区域的tile数据
            if (Main.netMode == NetmodeID.MultiplayerClient && Main.gameMenu == false) {
                bool hasRequested = false;
                if (!hasRequested) {
                    hasRequested = true;
                    // 延迟请求，确保客户端已完全加入
                    Main.RunOnMainThread(() => {
                        NetworkManager.SendMessage("RequestAllPortals", writer => {
                            writer.Write(Main.myPlayer);
                        });
                    });
                }
            }
        }

        private static void HandleNetherPortalTeleport(BinaryReader reader, int whoAmI) {
            if (Main.netMode != NetmodeID.Server) return;

            int portalX = reader.ReadInt32();
            int portalY = reader.ReadInt32();
            int playerIndex = reader.ReadInt32();

            // 在服务器端查找传送门实体并执行传送
            if (TileEntity.ByPosition.TryGetValue(new Point16(portalX, portalY), out TileEntity entity) &&
                entity is NetherPortalTileEntity portalEntity) {
                Player player = Main.player[playerIndex];
                if (player.active) {
                    // 直接调用服务端传送逻辑
                    portalEntity.HandleServerSideTeleport(player);
                }
            }
        }

        private static void HandleNetherPortalEffect(BinaryReader reader, int whoAmI) {
            if (Main.netMode == NetmodeID.Server) return;

            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            int playerIndex = reader.ReadInt32();

            Vector2 effectPos = new Vector2(x, y);

            // 在客户端播放传送特效
            GenerateClientTeleportEffects(effectPos, playerIndex);
        }

        private static void GenerateClientTeleportEffects(Vector2 position, int playerIndex) {
            if (Main.netMode == NetmodeID.Server) return;

            var particleManager = ParticleManager.Instance;
            if (particleManager == null) return;

            SoundEngine.PlaySound(ModAssets.PortalArrive, position);

            // 生成特效粒子
            for (int i = 0; i < 40; i++) {
                float angle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(0f, 20f);

                Vector2 startOffset = new Vector2(
                    (float)System.Math.Cos(angle) * distance,
                    (float)System.Math.Sin(angle) * distance
                );

                Vector2 startPosition = position + startOffset;

                var particle = particleManager.NewParticle<NetherPortalParticle>(
                    startPosition,
                    startOffset * 0.6f,
                    type: 0,
                    alpha: 0f,
                    scale: Main.rand.NextFloat(0.8f, 1.5f)
                );

                if (particle != null) {
                    particle.state = 1;
                    particle.life = Main.rand.Next(100, 180);
                    particle.timeLeft = particle.life - 10;
                }
            }

            // 如果是本地玩家，添加屏幕效果
            if (playerIndex == Main.myPlayer) {
                Main.BlackFadeIn = 255;
                Lighting.Clear();
                Main.screenLastPosition = Main.screenPosition;
                Main.screenPosition.X = position.X - (float)(Main.screenWidth / 2);
                Main.screenPosition.Y = position.Y - (float)(Main.screenHeight / 2);
                Main.instantBGTransitionCounter = 10;
                Main.player[Main.myPlayer].ForceUpdateBiomes();
            }
        }
    }
}
