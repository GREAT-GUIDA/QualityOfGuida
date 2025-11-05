using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using System.Reflection;
using QualityOfGuida.Content.Particles;

namespace QualityOfGuida.Content.Torcherino {
    public class TorcherinoSystem : ModSystem {
        // 存储所有活跃火把的位置和计时器
        private static Dictionary<Point16, int> activeTorches = new Dictionary<Point16, int>();
        // 存储每个火把对应的圆形粒子
        private static Dictionary<Point16, TorcherinoCircleParticle> torchCircles = new Dictionary<Point16, TorcherinoCircleParticle>();
        private static bool worldScanned = false;
        private static int radius = 16;
        // 反射获取的方法
        public static MethodInfo updateOvergroundTileMethod;
        public static MethodInfo updateUndergroundTileMethod;

        public override void Load() {
            // 通过反射获取 WorldGen 类中的私有方法
            if (updateOvergroundTileMethod == null) {
                Type worldGenType = typeof(WorldGen);

                updateOvergroundTileMethod = worldGenType.GetMethod("UpdateWorld_OvergroundTile",
                    BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new Type[] { typeof(int), typeof(int), typeof(bool), typeof(int) },
                    null);

                updateUndergroundTileMethod = worldGenType.GetMethod("UpdateWorld_UndergroundTile",
                    BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new Type[] { typeof(int), typeof(int), typeof(bool), typeof(int) },
                    null);
            }
        }

        public static void AddTorchPosition(Point16 position) {
            if (!activeTorches.ContainsKey(position)) {
                activeTorches[position] = Main.rand.Next(0, 120); // 随机初始计时器
                // 不在这里创建圆形粒子，在PostUpdateEverything中统一处理
            }
        }

        public static void RemoveTorchPosition(Point16 position) {
            activeTorches.Remove(position);
            // 移除对应的圆形粒子
            if (torchCircles.ContainsKey(position)) {
                var circle = torchCircles[position];
                if (circle != null && circle.active) {
                    circle.Kill(); // 如果粒子有Kill方法的话
                }
                torchCircles.Remove(position);
            }
        }

        // 扫描整个世界寻找现有的火把
        private void ScanWorldForTorches() {
            if (worldScanned) return;

            activeTorches.Clear();
            torchCircles.Clear(); // 也清理圆形粒子字典

            for (int x = 10; x < Main.maxTilesX - 10; x++) {
                for (int y = 10; y < Main.maxTilesY - 10; y++) {
                    Tile tile = Main.tile[x, y];
                    if (tile != null && tile.HasTile && tile.TileType == ModContent.TileType<TorcherinoTile>()) {
                        activeTorches[new Point16(x, y)] = Main.rand.Next(0, 120); // 随机初始计时器
                    }
                }
            }
            worldScanned = true;
        }

        // 这个方法每帧都会被调用
        public override void PostUpdateEverything() {
            // 如果还没扫描过世界，先扫描一遍
            if (!worldScanned && Main.gameMenu == false && Main.LocalPlayer != null) {
                ScanWorldForTorches();
            }

            // 创建一个临时列表来存储需要移除的位置
            List<Point16> toRemove = new List<Point16>();

            foreach (var kvp in activeTorches) {
                Point16 pos = kvp.Key;
                int timer = kvp.Value;

                // 检查这个位置是否还有火把
                if (pos.X < 0 || pos.X >= Main.maxTilesX || pos.Y < 0 || pos.Y >= Main.maxTilesY) {
                    toRemove.Add(pos);
                    continue;
                }

                Tile tile = Main.tile[pos.X, pos.Y];
                if (tile == null || !tile.HasTile || tile.TileType != ModContent.TileType<TorcherinoTile>()) {
                    toRemove.Add(pos);
                    continue;
                }

                // 更新计时器
                timer++;
                activeTorches[pos] = timer;

                // 每120帧（约2秒）有概率放置泥土块
                if (timer >= 1) {
                    activeTorches[pos] = 0; // 重置计时器
                    DoUpdate(pos.X, pos.Y, radius);
                }

                if (Main.netMode != NetmodeID.Server) {
                    CreateRangeParticles(pos.X, pos.Y);
                    // 更新或创建圆形粒子
                    UpdateCircleParticle(pos);
                }
            }

            // 移除无效的位置
            foreach (Point16 pos in toRemove) {
                activeTorches.Remove(pos);
                // 移除对应的圆形粒子
                if (torchCircles.ContainsKey(pos)) {
                    var circle = torchCircles[pos];
                    if (circle != null && circle.active) {
                        circle.Kill(); // 如果粒子有Kill方法的话
                    }
                    torchCircles.Remove(pos);
                }
            }
        }

        // 更新或创建圆形粒子
        private void UpdateCircleParticle(Point16 pos) {
            if (ParticleManager.Instance == null) return;

            Vector2 fireCenter = new Vector2(pos.X * 16 + 8, pos.Y * 16 + 8);

            // 获取或创建圆形粒子
            TorcherinoCircleParticle circle = null;
            if (torchCircles.ContainsKey(pos)) {
                circle = torchCircles[pos];
            }

            // 检查粒子是否需要重新创建
            if (circle == null || !circle.active) {
                circle = ParticleManager.Instance.NewParticle<TorcherinoCircleParticle>(
                    fireCenter, Main.rand.NextVector2Circular(1, 1), 0);

                if (circle != null) {
                    torchCircles[pos] = circle; // 保存引用
                }
            }

            // 更新粒子属性
            if (circle != null) {
                circle.position = fireCenter;
                circle.timeLeft = Math.Min(circle.timeLeft + 2, circle.maxTimeLeft);
                circle.scale = (float)radius * 36 / 522;
            }
        }

        public static void DoUpdate(int x, int y, int rad) {
            if (Main.netMode == 1) // 1 = 客户端
                return;

            // 确保反射方法已获取
            if (updateOvergroundTileMethod == null || updateUndergroundTileMethod == null)
                return;

            TryPlantAlch(x, y, rad);

            // 在玩家周围随机更新瓦片
            for (int i = 0; i < 50; i++) {
                Vector2 off = Main.rand.NextVector2Circular(rad, rad);

                int targetX = x + (int)Math.Round(off.X);
                int targetY = y + (int)Math.Round(off.Y);

                if (targetX >= 0 && targetX < Main.maxTilesX && targetY >= 0 && targetY < Main.maxTilesY) {
                    // 参数设置
                    bool checkNPCSpawns = false; // 在玩家周围不需要检查NPC生成
                    int wallDist = 3;

                    // 地表上方的瓦片更新
                    if (targetY < Main.worldSurface) {
                        updateOvergroundTileMethod.Invoke(null, new object[]
                        {
                            targetX, targetY, checkNPCSpawns, wallDist
                        });
                    }
                    // 地下的瓦片更新
                    else {
                        updateUndergroundTileMethod.Invoke(null, new object[]
                        {
                            targetX, targetY, checkNPCSpawns, wallDist
                        });
                    }
                }
            }
        }

        public static void TryPlantAlch(int x, int y, int rad, int attempts = 6) {
            for (int attempt = 0; attempt < attempts; attempt++) {
                int num = x + WorldGen.genRand.Next(-rad, rad + 1);

                // 确保在安全范围内
                if (num < 20 || num >= Main.maxTilesX - 20)
                    continue;

                // 原版代码，改成tModLoader API
                int num2 = 0;
                for (num2 = Main.remixWorld ? WorldGen.genRand.Next(20, Main.maxTilesY - 20) : WorldGen.genRand.NextBool(40) ? WorldGen.genRand.Next((int)(Main.rockLayer + Main.maxTilesY) / 2, Main.maxTilesY - 20) : WorldGen.genRand.Next(10) != 0 ? WorldGen.genRand.Next((int)Main.worldSurface, Main.maxTilesY - 20) : WorldGen.genRand.Next(20, Main.maxTilesY - 20);
                    num2 < Main.maxTilesY - 20 && !Main.tile[num, num2].HasTile;
                    num2++) {
                }
                if (new Vector2(num - x, num2 - y).Distance(Vector2.Zero) > rad) continue;
                if (!Main.tile[num, num2].HasTile || Main.tile[num, num2 - 1].HasTile || Main.tile[num, num2 - 1].LiquidAmount != 0)
                    continue;
                int num3 = 15;
                int num4 = 5;
                int num5 = 0;
                num3 = (int)(num3 * (Main.maxTilesX / 4200.0));
                int num6 = Utils.Clamp(num - num3, 4, Main.maxTilesX - 4);
                int num7 = Utils.Clamp(num + num3, 4, Main.maxTilesX - 4);
                int num8 = Utils.Clamp(num2 - num3, 4, Main.maxTilesY - 4);
                int num9 = Utils.Clamp(num2 + num3, 4, Main.maxTilesY - 4);
                for (int i = num6; i <= num7; i++) {
                    for (int j = num8; j <= num9; j++) {
                        if (Main.tileAlch[Main.tile[i, j].TileType])
                            num5++;
                    }
                }
                if (num5 < num4) {
                    if (Main.tile[num, num2].TileType == 2 || Main.tile[num, num2].TileType == 109)
                        WorldGen.PlaceAlch(num, num2 - 1, 0);
                    if (Main.tile[num, num2].TileType == 60)
                        WorldGen.PlaceAlch(num, num2 - 1, 1);
                    if (Main.tile[num, num2].TileType == 0 || Main.tile[num, num2].TileType == 59)
                        WorldGen.PlaceAlch(num, num2 - 1, 2);
                    if (Main.tile[num, num2].TileType == 23 || Main.tile[num, num2].TileType == 661 || Main.tile[num, num2].TileType == 25 || Main.tile[num, num2].TileType == 203 || Main.tile[num, num2].TileType == 199 || Main.tile[num, num2].TileType == 662)
                        WorldGen.PlaceAlch(num, num2 - 1, 3);
                    if ((Main.tile[num, num2].TileType == 53 || Main.tile[num, num2].TileType == 116) && num >= WorldGen.beachDistance && num <= Main.maxTilesX - WorldGen.beachDistance)
                        WorldGen.PlaceAlch(num, num2 - 1, 4);
                    if (Main.tile[num, num2].TileType == 57 || Main.tile[num, num2].TileType == 633)
                        WorldGen.PlaceAlch(num, num2 - 1, 5);
                    if (Main.tile[num, num2].TileType == 147 || Main.tile[num, num2].TileType == 163 || Main.tile[num, num2].TileType == 164 || Main.tile[num, num2].TileType == 161 || Main.tile[num, num2].TileType == 200)
                        WorldGen.PlaceAlch(num, num2 - 1, 6);
                    if (Main.tile[num, num2 - 1].HasTile && Main.netMode == 2)
                        NetMessage.SendTileSquare(-1, num, num2 - 1);
                }
            }
        }

        private void CreateRangeParticles(int centerX, int centerY) {
            int particleCount = 1;

            if (ParticleManager.Instance != null) {
                Vector2 fireCenter = new Vector2(centerX * 16 + 8, centerY * 16 + 8);

                for (int i = 0; i < 4; i++) {
                    float angle = Main.rand.NextFloat(0, (float)(Math.PI * 2));

                    Vector2 particlePos = fireCenter + new Vector2(
                        (float)Math.Cos(angle) * radius * 16,
                        (float)Math.Sin(angle) * radius * 16
                    );

                    float speed = Main.rand.NextFloat(-0.6f, 0.6f) * 2;
                    Vector2 particleVel = new Vector2(
                        -(float)Math.Sin(angle) * speed,  // 切向X分量
                        (float)Math.Cos(angle) * speed    // 切向Y分量
                    );

                    var particle = ParticleManager.Instance.NewParticle<TorcherinoParticle>(
                        particlePos, particleVel, 0, 1f, Main.rand.NextFloat(0.8f, 1.2f));
                    if (particle != null) {
                        particle.color = Color.CornflowerBlue;
                        particle.timeLeft = (int)(Main.rand.Next(80, 110) * 0.4f);
                        particle.lightColor = Color.LightBlue;
                        particle.state = 2;
                        particle.alphaMul *= 0.6f;
                    }
                }

                for (int i = 0; i < particleCount; i++) {
                    // 随机角度
                    float angle = Main.rand.NextFloat(0, (float)(Math.PI * 2));

                    Vector2 particlePos = fireCenter + Main.rand.NextVector2Circular(radius * 16 - 8, radius * 16 - 8);

                    var particle = ParticleManager.Instance.NewParticle<TorcherinoParticle>(
                        particlePos, Main.rand.NextVector2Circular(1, 1), 0, 1f, Main.rand.NextFloat(1.2f, 1.8f));
                    if (particle != null) {
                        particle.color = Color.CornflowerBlue;
                        particle.timeLeft = Main.rand.Next(60, 100);
                        particle.lightColor = Color.LightBlue;
                        particle.state = 1;
                        particle.alphaMul *= 0.3f;
                        particle.scaleMul *= 1.8f * Main.rand.NextFloat(1, 1.5f);
                    }
                }
            }
        }

        // 清理资源
        public override void Unload() {
            
        }

        // 重置世界扫描状态
        public override void ClearWorld() {
            activeTorches.Clear();
            torchCircles.Clear();
            worldScanned = false;
        }
    }
}