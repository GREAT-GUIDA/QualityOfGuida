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
using Terraria.ModLoader;
using Terraria;
using Microsoft.Xna.Framework;

namespace QualityOfGuida.Content.Spawner {
    public class SpawnerClientEffects : ModSystem {
        // 为每个spawner单独跟踪warning状态
        private class SpawnerWarningState {
            public bool isWarningActive;
            public int warningFramesLeft;
            public List<Vector2> warningPositions = new List<Vector2>();
        }

        // 使用Dictionary为每个spawner存储独立的warning状态
        private static Dictionary<Point16, SpawnerWarningState> spawnerWarnings = new Dictionary<Point16, SpawnerWarningState>();

        private const int WARNING_DURATION = 60; // 60帧持续时间

        public override void PostSetupContent() {
            base.PostSetupContent();
            NetworkManager.RegisterHandler("SetSpawnerBanner", HandleSetBanner);
            NetworkManager.RegisterHandler("SpawnerWarning", HandleWarningEffects);
            NetworkManager.RegisterHandler("SpawnerSpawn", HandleSpawnEffects);
        }

        private static void HandleSetBanner(BinaryReader reader, int whoAmI) {
            int tileX = reader.ReadInt32();
            int tileY = reader.ReadInt32();
            int bannerID = reader.ReadInt32();

            if (TileEntity.ByPosition.TryGetValue(new Point16(tileX, tileY), out TileEntity entity) &&
                entity is SpawnerTileEntity spawner) {
                spawner.SetBannerID(bannerID);
            }
        }

        private static void HandleWarningEffects(BinaryReader reader, int whoAmI) {
            if (Main.netMode == NetmodeID.Server) return;

            int spawnerX = reader.ReadInt32();
            int spawnerY = reader.ReadInt32();
            int posCount = reader.ReadInt32();

            Point16 spawnerPos = new Point16(spawnerX, spawnerY);

            // 为这个特定的spawner创建或获取warning状态
            if (!spawnerWarnings.ContainsKey(spawnerPos)) {
                spawnerWarnings[spawnerPos] = new SpawnerWarningState();
            }

            SpawnerWarningState warningState = spawnerWarnings[spawnerPos];

            // 清空之前的位置并读取新的位置
            warningState.warningPositions.Clear();
            for (int i = 0; i < posCount; i++) {
                float x = reader.ReadSingle();
                float y = reader.ReadSingle();
                warningState.warningPositions.Add(new Vector2(x, y));
            }

            // 启动这个spawner的持续warning特效
            warningState.isWarningActive = true;
            warningState.warningFramesLeft = WARNING_DURATION;

            // 立即显示一次特效
            ShowWarningEffectsAt(warningState.warningPositions);
        }

        private static void HandleSpawnEffects(BinaryReader reader, int whoAmI) {
            if (Main.netMode == NetmodeID.Server) return;

            int spawnerX = reader.ReadInt32();
            int spawnerY = reader.ReadInt32();
            int posCount = reader.ReadInt32();

            for (int i = 0; i < posCount; i++) {
                float x = reader.ReadSingle();
                float y = reader.ReadSingle();
                Vector2 pos = new Vector2(x, y);

                // 显示生成特效
                for (int j = 0; j < 12; j++) {
                    Vector2 particlePos = pos + Main.rand.NextVector2Circular(16, 16);
                    ParticleManager.Instance?.NewParticle<FlameParticle>(particlePos,
                        new Vector2(Main.rand.NextFloat(-0.1f, -0.1f), Main.rand.NextFloat(-0.1f, 0.1f)));
                }
                SoundEngine.PlaySound(SoundID.Item8, pos);
            }

            // 清除这个spawner的warning状态（生成完成后）
            Point16 spawnerPos = new Point16(spawnerX, spawnerY);
            if (spawnerWarnings.ContainsKey(spawnerPos)) {
                spawnerWarnings[spawnerPos].isWarningActive = false;
                spawnerWarnings[spawnerPos].warningPositions.Clear();
            }
        }

        private static void ShowWarningEffectsAt(List<Vector2> positions) {
            // 客户端预警特效代码
            foreach (Vector2 pos in positions) {
                if(Main.rand.NextBool(4))for (int i = 0; i < 1; i++) {
                    Vector2 particlePos = pos + Main.rand.NextVector2Circular(4, 5);
                    ParticleManager.Instance?.NewParticle<FlameParticle>(particlePos,
                        new Vector2(Main.rand.NextFloat(-0.1f, -0.1f), Main.rand.NextFloat(-0.1f, 0.1f)) / 2f);
                }
            }
        }

        public override void PostUpdateInput() {
            // PostUpdateInput在客户端执行
            if (Main.netMode == NetmodeID.Server) return;

            // 更新所有spawner的warning特效
            UpdateAllWarningEffects();

            // 更新spawner常规特效
            foreach (var entity in TileEntity.ByID.Values) {
                if (entity is SpawnerTileEntity spawner && spawner.StoredBannerID > 0) {
                    UpdateSpawnerEffects(spawner);
                }
            }

            // 清理无效的spawner warning状态
            CleanupInvalidWarnings();
        }

        private static void UpdateAllWarningEffects() {
            // 更新每个spawner的warning特效
            var keysToUpdate = spawnerWarnings.Keys.ToList(); // 避免修改正在迭代的字典

            foreach (Point16 spawnerPos in keysToUpdate) {
                SpawnerWarningState warningState = spawnerWarnings[spawnerPos];

                if (!warningState.isWarningActive) continue;

                // 持续显示warning特效
                if (warningState.warningFramesLeft > 0) {
                    ShowWarningEffectsAt(warningState.warningPositions);
                    warningState.warningFramesLeft--;
                } else {
                    // warning特效结束
                    warningState.isWarningActive = false;
                    warningState.warningPositions.Clear();
                }
            }
        }

        private static void CleanupInvalidWarnings() {
            // 清理不再存在的spawner的warning状态
            var keysToRemove = new List<Point16>();

            foreach (Point16 spawnerPos in spawnerWarnings.Keys) {
                // 检查这个位置是否还有有效的SpawnerTileEntity
                if (!TileEntity.ByPosition.ContainsKey(spawnerPos) ||
                    !(TileEntity.ByPosition[spawnerPos] is SpawnerTileEntity)) {
                    keysToRemove.Add(spawnerPos);
                }
            }

            foreach (Point16 key in keysToRemove) {
                spawnerWarnings.Remove(key);
            }
        }

        private static void UpdateSpawnerEffects(SpawnerTileEntity spawner) {
            Vector2 spawnerPos = new Vector2(spawner.Position.X * 16 + 16, spawner.Position.Y * 16 + 16);

            bool playerNearby = false;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player.active && !player.dead &&
                    Vector2.Distance(player.Center, spawnerPos) <= SpawnerTileEntity.DETECTION_RANGE * 16) {
                    playerNearby = true;
                    break;
                }
            }

            if (!playerNearby) return;

            if (Main.rand.NextBool(12)) {
                Vector2 particlePos = spawnerPos + Main.rand.NextVector2Circular(24, 24);
                ParticleManager.Instance?.NewParticle<FlameParticle>(particlePos,
                    new Vector2(Main.rand.NextFloat(-0.1f, -0.1f), Main.rand.NextFloat(-0.1f, 0.1f)));
            }
        }

        // 当mod卸载时清理数据
        public override void Unload() {
            
        }
    }
}