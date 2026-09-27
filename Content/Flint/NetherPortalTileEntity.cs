using Microsoft.Xna.Framework;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Graphics.CameraModifiers;
using GuidaSharedCode;

namespace QualityOfGuida.Content.Flint {
    public class NetherPortalTileEntity : ModTileEntity {
        public bool isInHell = false;
        public int portalId = 0;

        // 搜索配置
        private const int SEARCH_RANGE = 200;
        private const int PORTAL_WIDTH = 4;
        private const int PORTAL_HEIGHT = 5;
        // 添加多人游戏放置同步支持
        public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                int width = 4;
                int height = 5;
                NetMessage.SendTileSquare(Main.myPlayer, i, j, width, height);

                NetMessage.SendData(MessageID.TileEntityPlacement, number: i, number2: j, number3: Type);
                return -1;
            }

            // 在服务器端放置tile entity
            int placedEntity = Place(i, j);
            return placedEntity;
        }

        // 添加网络放置事件处理
        public override void OnNetPlace() {
            if (Main.netMode == NetmodeID.Server) {
                // 向所有客户端同步tile entity信息
                NetMessage.SendData(MessageID.TileEntitySharing, number: ID, number2: Position.X, number3: Position.Y);
            }
        }
        public override bool IsTileValidForEntity(int x, int y) {
            Tile tile = Main.tile[x, y];
            return tile.HasTile && tile.TileType == ModContent.TileType<NetherPortal>();
        }

        public override void Update() {
            if (!IsTileValidForEntity(Position.X, Position.Y))
                Kill(Position.X, Position.Y);

            isInHell = Position.Y >= Main.UnderworldLayer;
        }

        public override void NetSend(BinaryWriter writer) {
            writer.Write(isInHell);
            writer.Write(portalId);
        }

        public override void NetReceive(BinaryReader reader) {
            isInHell = reader.ReadBoolean();
            portalId = reader.ReadInt32();
        }

        public override void SaveData(TagCompound tag) {
            tag["portalId"] = portalId;
        }

        public override void LoadData(TagCompound tag) {
            portalId = tag.GetInt("portalId");
        }

        public void TeleportPlayer(Player player) {
            if (Main.netMode == NetmodeID.SinglePlayer) {
                bool playerInHell = player.ZoneUnderworldHeight;
                NetherPortalTileEntity targetPortal = FindClosestPortal(player, !playerInHell);

                Vector2 targetPos;
                if (targetPortal != null) {
                    targetPos = GetPortalCenter(targetPortal.Position);
                } else {
                    targetPos = playerInHell ? GenerateOverworldPortal(player) : GenerateHellPortal(player);
                }

                EnderTeleport(player, targetPos);
            } else {
                NetworkManager.SendMessage("NetherPortalTeleport", writer => {
                    writer.Write((int)Position.X);
                    writer.Write((int)Position.Y);
                    writer.Write(player.whoAmI);
                });
            }
        }

        public void HandleServerSideTeleport(Player player) {
            bool playerInHell = player.ZoneUnderworldHeight;
            NetherPortalTileEntity targetPortal = FindClosestPortal(player, !playerInHell);

            Vector2 targetPos;
            if (targetPortal != null) {
                targetPos = GetPortalCenter(targetPortal.Position);
            } else {
                // 服务器端生成新传送门
                targetPos = playerInHell ? GenerateOverworldPortal(player) : GenerateHellPortal(player);
            }
            // 服务器端执行传送
            ExecuteTeleport(player, targetPos);
        }



        private void ExecuteTeleport(Player player, Vector2 targetPos) {
            if (Main.netMode == NetmodeID.Server) {
                // 服务端：发送传送指令给目标客户端
                NetworkManager.SendMessage("ExecutePlayerTeleport", writer => {
                    writer.Write(targetPos.X);
                    writer.Write(targetPos.Y);
                }, player.whoAmI); // 只发送给目标玩家

                // 同时发送特效给所有客户端
                NetworkManager.SendMessage("NetherPortalEffect", writer => {
                    writer.Write(targetPos.X);
                    writer.Write(targetPos.Y);
                    writer.Write(player.whoAmI);
                });
            } else {
                // 单人游戏：直接执行传送
                ExecuteLocalTeleport(player, targetPos);
            }
        }

        private void ExecuteLocalTeleport(Player player, Vector2 targetPos) {// 在服务器端设置玩家位置
            player.environmentBuffImmunityTimer = 4;
            player.RemoveAllGrapplingHooks();
            player.StopVanityActions();

            if (player.shimmering || player.shimmerWet) {
                player.shimmering = false;
                player.shimmerWet = false;
                player.wet = false;
                player.ClearBuff(BuffID.Shimmer);
            }

            PressurePlateHelper.UpdatePlayerPosition(player);
            player.Center = targetPos;
            player.fallStart = (int)(player.position.Y / 16f);
            PressurePlateHelper.UpdatePlayerPosition(player);

            // 发送特效消息到所有客户端
            NetworkManager.SendMessage("NetherPortalEffect", writer => {
                writer.Write(targetPos.X);
                writer.Write(targetPos.Y);
                writer.Write(player.whoAmI);
            });
        }
        // 统一的传送门查找逻辑
        private NetherPortalTileEntity FindClosestPortal(Player player, bool findInHell) {
            int playerX = (int)(player.position.X / 16);

            return TileEntity.ByID.Values
                .OfType<NetherPortalTileEntity>()
                .Where(portal => portal.isInHell == findInHell &&
                               System.Math.Abs(portal.Position.X - playerX) <= SEARCH_RANGE)
                .OrderBy(portal => System.Math.Abs(portal.Position.X - playerX))
                .FirstOrDefault();
        }

        private Vector2 GetPortalCenter(Point16 portalPos) {
            return new Vector2((portalPos.X + 2) * 16, (portalPos.Y + 2) * 16);
        }

        // 统一的传送门生成逻辑
        private Vector2 GenerateOverworldPortal(Player player) {
            return GeneratePortal(player, false) ?? new Vector2(Main.spawnTileX * 16, Main.spawnTileY * 16);
        }

        private Vector2 GenerateHellPortal(Player player) {
            return GeneratePortal(player, true) ?? new Vector2(player.position.X, ((int)Main.UnderworldLayer + 50) * 16);
        }

        private Vector2? GeneratePortal(Player player, bool inHell) {
            int centerX = (int)(player.position.X / 16);
            Point? location = FindSuitableLocation(centerX, inHell);

            if (location.HasValue) {
                Point pos = location.Value;
                CreatePortalStructure(pos.X, pos.Y);
                CheckAndCreatePlatform(pos.X, pos.Y);
                return GetPortalCenter(new Point16(pos.X, pos.Y));
            }

            return null;
        }

        // 统一的位置搜索逻辑
        private Point? FindSuitableLocation(int centerX, bool inHell) {
            return inHell ? FindHellPosition(centerX) : FindOverworldPosition(centerX);
        }

        // 地表位置搜索 - 简单随机选择算法
        private Point? FindOverworldPosition(int centerX) {
            int surfaceLevel = (int)Main.worldSurface;
            int hellStart = (int)Main.UnderworldLayer;

            // 在地表上方开始搜索
            int searchStart = surfaceLevel - 250;
            int searchEnd = surfaceLevel + 100;

            return FindPositionByRandomSearch(centerX, searchStart, searchEnd, false);
        }

        // 地狱位置搜索 - 简单随机选择算法
        private Point? FindHellPosition(int centerX) {
            int hellStart = (int)Main.UnderworldLayer + 3;
            int hellEnd = Main.maxTilesY - 20;

            return FindPositionByRandomSearch(centerX, hellStart, hellEnd, true);
        }

        // 核心随机搜索算法
        private Point? FindPositionByRandomSearch(int centerX, int startY, int endY, bool isHell) {
            const int maxAttempts = 200;
            int baseRange = 50;

            for (int attempt = 0; attempt < maxAttempts; attempt++) {
                // 随着尝试次数增加，扩大搜索范围
                int currentRange = baseRange + (attempt / 10) * 10;

                // 随机选择X坐标
                int randomX = centerX + Main.rand.Next(-currentRange, currentRange + 1);
                if (randomX < 10 || randomX > Main.maxTilesX - 10) continue;

                // 随机选择Y坐标
                int randomY = startY + Main.rand.Next(0, endY - startY - PORTAL_HEIGHT);
                if (randomY < 10 || randomY > Main.maxTilesY - 10) continue;

                // 检查是否是空旷区域
                bool allowBlocks = attempt > 100; // 50次尝试后放宽限制
                if (IsAreaClear(randomX, randomY, allowBlocks, isHell)) {
                    // 向下移动到合适的支撑位置
                    Point? finalPosition = MoveDownToSupport(randomX, randomY, endY);
                    if (finalPosition.HasValue && CanPlacePortalSimple(finalPosition.Value.X, finalPosition.Value.Y, isHell)) {
                        return finalPosition;
                    }
                }
            }

            return null;
        }

        // 检查区域是否空旷
        private bool IsAreaClear(int left, int top, bool allowBlocks, bool isHell) {
            for (int x = 0; x < PORTAL_WIDTH; x++) {
                for (int y = 0; y < PORTAL_HEIGHT; y++) {
                    int worldX = left + x;
                    int worldY = top + y;

                    if (worldX < 0 || worldX >= Main.maxTilesX || worldY < 0 || worldY >= Main.maxTilesY) {
                        return false;
                    }

                    Tile tile = Main.tile[worldX, worldY];

                    // 检查实心物块
                    if (!allowBlocks && tile.HasTile && Main.tileSolid[tile.TileType]) {
                        return false;
                    }

                    // 检查重要物块（始终不允许）
                    if (tile.HasTile && IsImportantTile(tile.TileType)) {
                        return false;
                    }

                    // 检查液体
                    if (tile.LiquidAmount > 0) {
                         return false;
                    }
                }
            }

            return true;
        }

        // 向下移动到支撑位置
        private Point? MoveDownToSupport(int x, int startY, int maxY) {
            for (int y = startY; y < maxY - PORTAL_HEIGHT; y++) {
                // 检查是否到达支撑位置
                if (HasSupportBelow(x, y)) {
                    return new Point(x, y);
                }
            }
            return null;
        }

        // 检查下方是否有支撑
        private bool HasSupportBelow(int left, int top) {
            int supportCount = 0;
            int lavaCount = 0;

            for (int x = 0; x < PORTAL_WIDTH; x++) {
                int worldX = left + x;
                int worldY = top + PORTAL_HEIGHT;

                if (worldY < Main.maxTilesY) {
                    Tile tile = Main.tile[worldX, worldY];

                    if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                        supportCount++;
                    }

                    if (tile.LiquidAmount > 0 ) {
                        lavaCount++;
                    }
                }
            }

            // 需要至少2个支撑点，或者地狱中有熔岩表面
            return supportCount >= 2 || (lavaCount >= 2);
        }

        // 简化的位置验证
        private bool CanPlacePortalSimple(int left, int top, bool isHell) {
            // 基本边界检查
            if (left < 5 || top < 5 ||
                left + PORTAL_WIDTH > Main.maxTilesX - 5 ||
                top + PORTAL_HEIGHT > Main.maxTilesY - 5) {
                return false;
            }

            // 检查是否有重要建筑
            for (int x = 0; x < PORTAL_WIDTH; x++) {
                for (int y = 0; y < PORTAL_HEIGHT; y++) {
                    int worldX = left + x;
                    int worldY = top + y;
                    Tile tile = Main.tile[worldX, worldY];

                    if (tile.HasTile && IsImportantTile(tile.TileType)) {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool IsImportantTile(int tileType) {
            return Main.tileContainer[tileType] ||
                   Main.tileLighted[tileType] ||
                   tileType == TileID.Beds ||
                   tileType == TileID.Chairs ||
                   tileType == TileID.Tables ||
                   tileType == TileID.WorkBenches ||
                   tileType == TileID.Anvils ||
                   TileID.Sets.BasicChest[tileType] ||
                   TileID.Sets.BasicChestFake[tileType];
        }

        // 创建传送门结构
        private void CreatePortalStructure(int left, int top) {

            // 播放音效
            SoundEngine.PlaySound(SoundID.Dig, new Vector2(left * 16 + 32, top * 16 + 40));
            SoundEngine.PlaySound(QoGSound.PortalAmbience, new Vector2(left * 16 + 32, top * 16 + 40));

            // 放置传送门瓦片
            for (int x = 0; x < PORTAL_WIDTH; x++) {
                for (int y = 0; y < PORTAL_HEIGHT; y++) {
                    int worldX = left + x;
                    int worldY = top + y;

                    if (worldX >= 0 && worldX < Main.maxTilesX && worldY >= 0 && worldY < Main.maxTilesY) {
                        Tile tile = Main.tile[worldX, worldY];

                        tile.HasTile = true;
                        tile.TileType = (ushort)ModContent.TileType<NetherPortal>();
                        tile.TileFrameX = (short)(x * 18);
                        tile.TileFrameY = (short)(y * 18);
                        tile.Slope = 0;
                        tile.IsHalfBlock = false;
                    }
                }
            }

            // 创建瓦片实体
            int entityID = NetherPortal.AfterPlacement(left, top, ModContent.TileType<NetherPortal>(), 0, 1, 0);

            // 网络同步
            if (Main.netMode != NetmodeID.SinglePlayer) {
                NetMessage.SendTileSquare(-1, left, top, PORTAL_WIDTH, PORTAL_HEIGHT);
                if (entityID >= 0) {
                    NetMessage.SendData(MessageID.TileEntityPlacement, -1, -1, null, left, top, entityID);
                }
            }

            // 网络同步
            if (Main.netMode != NetmodeID.SinglePlayer) {
                NetMessage.SendTileSquare(-1, left, top, PORTAL_WIDTH, PORTAL_HEIGHT + 1);
            }
        }


        private void CheckAndCreatePlatform(int portalLeft, int portalTop) {
            int platformY = portalTop + PORTAL_HEIGHT;

            if (platformY < Main.maxTilesY - 1) {
                CreatePlatform(portalLeft - 1, portalLeft + PORTAL_WIDTH, platformY);
            }
        }

        private void CreatePlatform(int startX, int endX, int y) {
            for (int x = startX; x <= endX; x++) {
                if (x >= 0 && x < Main.maxTilesX && y >= 0 && y < Main.maxTilesY) {
                    Tile tile = Main.tile[x, y];
                    WorldGen.PlaceTile(x, y, TileID.AshWood);
                    WorldGen.SlopeTile(x, y);
                }
            }

            // 网络同步
            if (Main.netMode != NetmodeID.SinglePlayer) {
                NetMessage.SendTileSquare(-1, startX, y, endX - startX + 1, 1);
            }
        }

        public void EnderTeleport(Player player, Vector2 newPos) {
            try {
                // 播放传送音效
                SoundEngine.PlaySound(QoGSound.PortalArrive, player.position);

                player.environmentBuffImmunityTimer = 4;
                player.RemoveAllGrapplingHooks();
                player.StopVanityActions();

                // 清理shimmer状态
                if (player.shimmering || player.shimmerWet) {
                    player.shimmering = false;
                    player.shimmerWet = false;
                    player.wet = false;
                    player.ClearBuff(BuffID.Shimmer);
                }

                // 执行传送
                PressurePlateHelper.UpdatePlayerPosition(player);
                player.Center = newPos;
                player.fallStart = (int)(player.position.Y / 16f);

                // 客户端特效处理
                if (player.whoAmI == Main.myPlayer) {
                    NPC.ResetNetOffsets();
                    Main.BlackFadeIn = 255;
                    Lighting.Clear();
                    Main.screenLastPosition = Main.screenPosition;
                    Main.screenPosition.X = player.position.X + (float)(player.width / 2) - (float)(Main.screenWidth / 2);
                    Main.screenPosition.Y = player.position.Y + (float)(player.height / 2) - (float)(Main.screenHeight / 2);
                    Main.instantBGTransitionCounter = 10;
                    player.ForceUpdateBiomes();

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

                // 生成到达特效
                GenerateArrivalEffects(player);

            } catch {
                // 传送失败处理
            }
        }

        private void GenerateArrivalEffects(Player player) {
            if (Main.netMode == NetmodeID.Server) return;

            var particleManager = ParticleManager.Instance;
            if (particleManager == null) return;

            Vector2 arrivalCenter = player.Center;
            SoundEngine.PlaySound(QoGSound.PortalArrive, arrivalCenter);

            // 生成到达粒子效果
            for (int i = 0; i < 40; i++) {
                float angle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(0f, 20f);

                Vector2 startOffset = new Vector2(
                    (float)System.Math.Cos(angle) * distance,
                    (float)System.Math.Sin(angle) * distance
                );

                Vector2 startPosition = arrivalCenter + startOffset;

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

            var cameraModifier = new PunchCameraModifier(
                arrivalCenter,
                Main.rand.NextVector2CircularEdge(1f, 1f),
                10,
                1.8f,
                80
            );
            Main.instance.CameraModifiers.Add(cameraModifier);

            var cameraModifier2 = new PunchCameraModifier(
                arrivalCenter,
                Main.rand.NextVector2CircularEdge(1f, 1f),
                15,
                2.8f,
                40
            );
            Main.instance.CameraModifiers.Add(cameraModifier2);
        }
    }
}