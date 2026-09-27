using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader.IO;
using Terraria.ModLoader;
using Terraria;
using Microsoft.Xna.Framework;
using Terraria.Graphics.Renderers;
using GuidaSharedCode;

namespace QualityOfGuida.Content.Spawner {
    public class SpawnerTileEntity : ModTileEntity {
        public int StoredBannerID { get; private set; } = 0;
        private int spawnTimer = 0;

        // 动态属性，根据NPC类型调整
        private int currentSpawnInterval = 120;
        private int currentSpawnCount = 3;
        private int currentMaxNearby = 12;
        private bool currentNoGravity = false;
        private bool currentNoTileCollide = false;

        public const int DETECTION_RANGE = 36;
        private const int SPAWN_RANGE = 10;
        private const int WARNING_TIME = 60;

        private List<Vector2> warningPositions = new List<Vector2>();
        private bool isWarning = false;

        public override bool IsTileValidForEntity(int x, int y) {
            Tile tile = Main.tile[x, y];
            return tile.HasTile && tile.TileType == ModContent.TileType<SpawnerTile>();
        }

        // 添加多人游戏放置同步支持
        public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                int width = 2;
                int height = 2;
                NetMessage.SendTileSquare(Main.myPlayer, i, j, width, height);

                // 同步tile entity的放置
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

        // 根据NPC类型更新生成参数
        private void UpdateSpawnParameters() {
            if (StoredBannerID <= 0) return;

            int npcType = Item.BannerToNPC(StoredBannerID);

            // 获取NPC的默认属性
            NPC tempNPC = new NPC();
            tempNPC.SetDefaults(npcType);

            currentNoGravity = tempNPC.noGravity;
            currentNoTileCollide = tempNPC.noTileCollide;

            switch (tempNPC.rarity) {
                case 0:
                    currentSpawnInterval = 200;
                    currentSpawnCount = 3;
                    currentMaxNearby = 10;
                    break;
                case 1:
                case 2:
                    currentSpawnInterval = 280;
                    currentSpawnCount = 2;
                    currentMaxNearby = 5;
                    break;
                case 3:
                case 4:
                case 5:
                    currentSpawnInterval = 360;
                    currentSpawnCount = 1;
                    currentMaxNearby = 3;
                    break;
            }
        }

        public override void Update() {
            if (Main.netMode == NetmodeID.MultiplayerClient || StoredBannerID <= 0)
                return;

            // 更新生成参数
            UpdateSpawnParameters();

            bool playerNearby = false;
            Vector2 spawnerPos = new Vector2(Position.X * 16 + 16, Position.Y * 16 + 16);

            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player.active && !player.dead && Vector2.Distance(player.Center, spawnerPos) <= DETECTION_RANGE * 16) {
                    playerNearby = true;
                    break;
                }
            }

            if (!playerNearby) {
                spawnTimer = 0;
                ClearWarning();
                return;
            }

            // 当spawner被激活时，持续生成火焰粒子
            if (Main.rand.NextBool(12)) { // 大约每8帧生成一个
                Vector2 particlePos = spawnerPos + Main.rand.NextVector2Circular(24, 24);
                ParticleManager.Instance?.NewParticle<FlameParticle>(particlePos,
                    new Vector2(Main.rand.NextFloat(-0.1f, -0.1f), Main.rand.NextFloat(-0.1f, 0.1f)));
            }

            spawnTimer++;

            if (spawnTimer == currentSpawnInterval - WARNING_TIME) {
                StartWarning();
            }

            if (isWarning && spawnTimer < currentSpawnInterval) {
                ShowWarningParticles();
            }

            // 生成NPC
            if (spawnTimer >= currentSpawnInterval) {
                TrySpawnNPCs();
            }
        }

        private void StartWarning() {
            warningPositions.Clear();
            isWarning = true;

            int npcType = Item.BannerToNPC(StoredBannerID);
            Vector2 spawnerPos = new Vector2(Position.X * 16 + 16, Position.Y * 16 + 16);

            // 检查附近NPC数量
            int nearbyCount = 0;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == npcType &&
                    Vector2.Distance(npc.Center, spawnerPos) <= (DETECTION_RANGE) * 20)
                    nearbyCount++;
            }

            float num = (1f - (float)nearbyCount / currentMaxNearby) * currentSpawnCount;
            int spawnCount = (int)num;
            if ((Main.rand.NextFloat() + spawnCount) <= num) spawnCount += 1;

            List<Point> usedPositions = new List<Point>();
            int positionsFound = 0;

            for (int attempts = 0; attempts < spawnCount * 40 && positionsFound < spawnCount; attempts++) {
                int spawnX = Position.X + Main.rand.Next(-SPAWN_RANGE, SPAWN_RANGE + 1);
                int spawnY = Position.Y + Main.rand.Next(-SPAWN_RANGE, SPAWN_RANGE + 1);
                Point spawnPoint = new Point(spawnX, spawnY);

                // 跳过已使用的位置
                if (usedPositions.Contains(spawnPoint))
                    continue;

                // 边界检查
                if (spawnX < 10 || spawnX > Main.maxTilesX - 10 || spawnY < 10 || spawnY > Main.maxTilesY - 10)
                    continue;

                // 根据NPC属性进行不同的位置验证
                if (!IsValidSpawnPosition(spawnX, spawnY))
                    continue;

                Vector2 spawnPos = new Vector2(spawnX * 16 + 8, spawnY * 16);
                warningPositions.Add(spawnPos);
                usedPositions.Add(spawnPoint);
                positionsFound++;
            }

            if (warningPositions.Count == 0) {
                ClearWarning();
                return;
            }

            if (Main.netMode == NetmodeID.Server && warningPositions.Count > 0) {
                NetworkManager.SendMessage("SpawnerWarning", writer => {
                    writer.Write((int)Position.X);
                    writer.Write((int)Position.Y);
                    writer.Write(warningPositions.Count);
                    foreach (var pos in warningPositions) {
                        writer.Write(pos.X);
                        writer.Write(pos.Y);
                    }
                });
            }
        }

        // 根据NPC属性验证生成位置
        private bool IsValidSpawnPosition(int x, int y) {
            if (currentNoTileCollide) {
                return !Main.tile[x, y].HasTile || !Main.tileSolid[Main.tile[x, y].TileType];
            }

            if (currentNoGravity) {
                if (Main.tile[x, y].HasTile && Main.tileSolid[Main.tile[x, y].TileType])
                    return false;
                if (Main.tile[x, y - 1].HasTile && Main.tileSolid[Main.tile[x, y - 1].TileType])
                    return false;
                return true;
            }

            if ((Main.tile[x, y - 1].HasTile && Main.tileSolid[Main.tile[x, y - 1].TileType]) ||
                (Main.tile[x, y].HasTile && Main.tileSolid[Main.tile[x, y].TileType]))
                return false;

            if (!Main.tile[x, y + 1].HasTile || !Main.tileSolid[Main.tile[x, y + 1].TileType])
                return false;

            return true;
        }

        private void ShowWarningParticles() {
            foreach (Vector2 pos in warningPositions) {
                for (int i = 0; i < 2; i++) {
                    Dust warning = Dust.NewDustDirect(pos, 16, 16, DustID.RedTorch);
                    warning.velocity = new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-2.5f, -0.5f));
                    warning.noGravity = true;
                    warning.scale = Main.rand.NextFloat(1f, 1.5f);
                    warning.alpha = 50;
                }

                if (Main.rand.NextBool(3)) {
                    Dust spark = Dust.NewDustDirect(pos, 16, 16, DustID.Electric);
                    spark.velocity = Main.rand.NextVector2Circular(2f, 2f);
                    spark.noGravity = true;
                    spark.color = Color.OrangeRed;
                    spark.scale = 1.2f;
                }
            }
        }

        private void ClearWarning() {
            warningPositions.Clear();
            isWarning = false;
        }

        private void TrySpawnNPCs() {
            spawnTimer = 0;

            // 如果没有预警位置，使用原有的生成逻辑
            if (!isWarning || warningPositions.Count == 0) {
                ClearWarning();
                return;
            }

            int npcType = Item.BannerToNPC(StoredBannerID);

            foreach (Vector2 spawnPos in warningPositions) {
                int npcIndex = NPC.NewNPC(new EntitySource_TileEntity(this), (int)spawnPos.X, (int)spawnPos.Y, npcType);
                for (int i = 0; i < 12; i++) {
                    Vector2 particlePos = spawnPos + Main.rand.NextVector2Circular(16, 16);
                    ParticleManager.Instance?.NewParticle<FlameParticle>(particlePos,
                        new Vector2(Main.rand.NextFloat(-0.1f, -0.1f), Main.rand.NextFloat(-0.1f, 0.1f)));
                }
                Main.npc[npcIndex].velocity = Vector2.Zero;
            }

            if (Main.netMode == NetmodeID.Server && warningPositions.Count > 0) {
                NetworkManager.SendMessage("SpawnerSpawn", writer => {
                    writer.Write((int)Position.X);
                    writer.Write((int)Position.Y);
                    writer.Write(warningPositions.Count);
                    foreach (var pos in warningPositions) {
                        writer.Write(pos.X);
                        writer.Write(pos.Y);
                    }
                });
            }
            ClearWarning();
        }

        public void SetBannerID(int bannerID) {
            StoredBannerID = bannerID;
            spawnTimer = 0;
            ClearWarning(); // 清除任何现有的预警
            UpdateSpawnParameters(); // 更新生成参数

            // 修复网络同步 - 使用正确的参数
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.TileEntitySharing, number: ID, number2: Position.X, number3: Position.Y);
        }

        public override void SaveData(TagCompound tag) => tag["storedBannerID"] = StoredBannerID;
        public override void LoadData(TagCompound tag) => StoredBannerID = tag.GetInt("storedBannerID");
        public override void NetSend(BinaryWriter writer) => writer.Write(StoredBannerID);
        public override void NetReceive(BinaryReader reader) => StoredBannerID = reader.ReadInt32();
    }
}