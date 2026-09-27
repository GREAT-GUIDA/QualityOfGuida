using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.WorldBuilding;
using Terraria;
using Microsoft.Xna.Framework;
using QualityOfGuida.Content.Flint;
using Terraria.DataStructures;

namespace QualityOfGuida.Content.Spawner {
    /// <summary>
    /// 刷怪笼和刷怪蛋世界生成系统
    /// </summary>
    public class SpawnerGenerationSystem : ModSystem {
        // 本地化文本
        public static LocalizedText SpawnerPassMessage { get; private set; }
        public static LocalizedText SpawnEggPassMessage { get; private set; }

        public override void SetStaticDefaults() {
            SpawnerPassMessage = Language.GetOrRegister(Mod.GetLocalizationKey($"WorldGen.{nameof(SpawnerPassMessage)}"));
            SpawnEggPassMessage = Language.GetOrRegister(Mod.GetLocalizationKey($"WorldGen.{nameof(SpawnEggPassMessage)}"));
        }

        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight) {
            // 在Cave Walls之后生成刷怪笼
            int caveWallsIndex = tasks.FindIndex(genpass => genpass.Name.Equals("Cave Walls"));
            if (caveWallsIndex != -1) {
                tasks.Insert(caveWallsIndex + 1, new ChestSpawnerPass("Underground Spawners", 30f));
            }

            // 在所有箱子生成完成后添加刷怪蛋
            int finalCleanupIndex = tasks.FindIndex(genpass => genpass.Name.Equals("Final Cleanup"));
            if (finalCleanupIndex != -1) {
                tasks.Insert(finalCleanupIndex, new SpawnEggChestPass("Spawn Egg Chests", 20f));
            }
        }
    }

    /// <summary>
    /// 地下刷怪笼生成Pass
    /// </summary>
    /// <summary>
    /// 在箱子附近生成刷怪笼的Pass
    /// </summary>
    public class ChestSpawnerPass : GenPass {
        private const int SEARCH_RADIUS = 60;  // 在箱子周围搜索的半径
        private const int SPAWNERS_PER_WORLD = 15; // 每个世界生成的刷怪笼数量
        private const int MAX_ATTEMPTS = 500;  // 总的最大尝试次数

        public ChestSpawnerPass(string name, float loadWeight) : base(name, loadWeight) { }

        public override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = SpawnerGenerationSystem.SpawnerPassMessage.Value;
            if (!ModContent.GetInstance<ItemToggleConfig>().EnableSpawner) return;
            // 先收集所有合法的箱子
            List<Chest> validChests = new List<Chest>();

            for (int i = 0; i < Main.maxChests; i++) {
                if (Main.chest[i] != null && ShouldProcessChest(Main.chest[i])) {
                    validChests.Add(Main.chest[i]);
                }
            }

            if (validChests.Count == 0) return;

            int spawnersPlaced = 0;
            int attempts = 0;

            // 固定次数的尝试
            while (spawnersPlaced < SPAWNERS_PER_WORLD && attempts < MAX_ATTEMPTS) {
                attempts++;

                // 随机选择一个合法箱子
                Chest selectedChest = validChests[WorldGen.genRand.Next(validChests.Count)];

                if (TryPlaceSpawnerNearChest(selectedChest)) {
                    spawnersPlaced++;
                }

                progress.Set((float)Math.Min(attempts, spawnersPlaced * (MAX_ATTEMPTS / SPAWNERS_PER_WORLD)) / MAX_ATTEMPTS);
            }
        }

        private bool ShouldProcessChest(Chest chest) {
            Tile chestTile = Main.tile[chest.x, chest.y];
            if (chestTile.TileType != TileID.Containers) return false;

            // 检查是否是我们关心的箱子类型
            BiomeType biome = GetChestBiomeByType(chestTile.TileFrameX);
            return biome != BiomeType.None;
        }

        private bool TryPlaceSpawnerNearChest(Chest chest) {
            BiomeType biome = GetChestBiomeByType(Main.tile[chest.x, chest.y].TileFrameX);

            // 在箱子周围寻找合适的位置（单次尝试多个位置）
            List<Point> possiblePositions = new List<Point>();

            // 收集箱子周围所有可能的位置
            for (int offsetX = -SEARCH_RADIUS; offsetX <= SEARCH_RADIUS; offsetX++) {
                for (int offsetY = -SEARCH_RADIUS; offsetY <= SEARCH_RADIUS; offsetY++) {
                    int spawnerX = chest.x + offsetX;
                    int spawnerY = chest.y + offsetY;

                    if (CanPlaceSpawner(spawnerX, spawnerY)) {
                        possiblePositions.Add(new Point(spawnerX, spawnerY));
                    }
                }
            }

            // 如果有可用位置，随机选择一个
            if (possiblePositions.Count > 0) {
                Point selectedPos = possiblePositions[WorldGen.genRand.Next(possiblePositions.Count)];
                return PlaceSpawnerWithContent(selectedPos.X, selectedPos.Y, biome);
            }

            return false;
        }

        private bool CanPlaceSpawner(int x, int y) {
            // 检查是否在世界范围内（2*2物块需要额外空间）
            if (!WorldGen.InWorld(x, y, 5) || !WorldGen.InWorld(x + 1, y + 1, 5))
                return false;

            // 检查2*2区域是否完全为空（无固体方块和液体）
            for (int i = x; i <= x + 1; i++) {
                for (int j = y; j <= y + 1; j++) {
                    if (!WorldGen.InWorld(i, j)) return false;

                    Tile tile = Main.tile[i, j];

                    // 检查是否有固体方块
                    if (tile.HasTile) return false;

                    // 检查是否有液体
                    if (tile.LiquidAmount > 0) return false;
                }
            }

            // 检查刷怪笼脚下两格是否有支撑
            for (int i = x; i <= x + 1; i++) {
                if (!WorldGen.InWorld(i, y + 2)) return false;

                Tile supportTile = Main.tile[i, y + 2];
                if (!supportTile.HasTile || !Main.tileSolid[supportTile.TileType]) {
                    return false;
                }
            }

            return true;
        }

        private bool PlaceSpawnerWithContent(int x, int y, BiomeType biome) {
            try {
                // 放置刷怪笼
                if (WorldGen.PlaceObject(x, y, ModContent.TileType<SpawnerTile>(), false, 0, 0, -1, -1)) {
                    SpawnerTile.AfterPlacement(x, y, ModContent.TileType<SpawnerTile>(), 0, 1, 0);
                    SetSpawnerContent(x, y, biome);
                    return true;
                }
                return false;
            } catch {
                return false;
            }
        }

        private void SetSpawnerContent(int x, int y, BiomeType biome) {
            // 找到刷怪笼的TileEntity
            if (TileEntity.ByPosition.TryGetValue(new Point16(x, y), out TileEntity entity) &&
                entity is SpawnerTileEntity spawner) {

                int bannerID = GetRandomBannerForBiome(biome);
                if (bannerID > 0) {
                    spawner.SetBannerID(bannerID);
                }
            }
        }

        private int GetRandomBannerForBiome(BiomeType biome) {
            return biome switch {
                BiomeType.Underground => Main.rand.Next(new int[] {
                Item.NPCtoBanner(-6),   // 洞穴史莱姆
                Item.NPCtoBanner(21),   // 骷髅
                Item.NPCtoBanner(49),  // 蝙蝠
                Item.NPCtoBanner(10),  // 洞穴蝙蝠
            }),
                BiomeType.Dungeon => Main.rand.Next(new int[] {
                Item.NPCtoBanner(31),
                Item.NPCtoBanner(32),
            }),
                BiomeType.Jungle => Main.rand.Next(new int[] {
                Item.NPCtoBanner(204),  // 丛林史莱姆
                Item.NPCtoBanner(42),  // 大黄蜂
            }),
                BiomeType.Snow => Main.rand.Next(new int[] {
                Item.NPCtoBanner(167), // 冰雪史莱姆
                Item.NPCtoBanner(185), // 雪人
            }),
                _ => 0
            };
        }

        private BiomeType GetChestBiomeByType(int tileFrameX) {
            // 根据箱子的TileFrameX判断类型
            // 每个箱子类型的frame宽度是36像素
            int chestType = tileFrameX / 36;

            return chestType switch {
                1 => BiomeType.Underground, // Gold Chest
                2 => BiomeType.Dungeon,     // Locked Gold Chest
                10 => BiomeType.Jungle,     // Ivy Chest
                11 => BiomeType.Snow,       // Frozen Chest
                _ => BiomeType.None
            };
        }
    }

    /// <summary>
    /// 箱子刷怪蛋添加Pass
    /// </summary>
    public class SpawnEggChestPass : GenPass {
        public SpawnEggChestPass(string name, float loadWeight) : base(name, loadWeight) { }

        public override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = SpawnerGenerationSystem.SpawnEggPassMessage.Value;

            int chestsProcessed = 0;
            int totalChests = 0;

            // 统计符合条件的箱子总数
            for (int i = 0; i < Main.maxChests; i++) {
                if (Main.chest[i] != null && ShouldProcessChest(Main.chest[i])) {
                    totalChests++;
                }
            }

            // 处理每个符合条件的箱子
            for (int i = 0; i < Main.maxChests; i++) {
                if (Main.chest[i] != null && ShouldProcessChest(Main.chest[i])) {
                    ProcessChest(Main.chest[i]);
                    chestsProcessed++;

                    if (totalChests > 0) {
                        progress.Set((float)chestsProcessed / totalChests);
                    }
                }
            }
        }

        private bool ShouldProcessChest(Chest chest) {
            Tile chestTile = Main.tile[chest.x, chest.y];
            if (chestTile.TileType != TileID.Containers) return false;

            // 检查是否是我们关心的箱子类型
            BiomeType biome = GetChestBiomeByType(chestTile.TileFrameX);
            return biome != BiomeType.None;
        }

        private void ProcessChest(Chest chest) {

            Tile chestTile = Main.tile[chest.x, chest.y];
            BiomeType biome = GetChestBiomeByType(chestTile.TileFrameX);
            float randseed = Main.rand.NextFloat(10);
            int egg = 0;
            int eggnum = 0;
            switch (biome) {
                case BiomeType.Surface:
                    if (randseed <= 1) {
                        egg = Main.rand.Next(-10, -5);
                        eggnum = Main.rand.Next(3, 11);
                    } else if (randseed <= 2) {
                        egg = -3;
                        eggnum = Main.rand.Next(3, 11);
                    } else if (randseed <= 3) {
                        egg = 1;
                        eggnum = Main.rand.Next(3, 11);
                    } else if (randseed <= 4) {
                        egg = -4;
                        eggnum = 1;
                    }
                    break;
                case BiomeType.Underground:  
                    if (randseed <= 0.8) {
                        egg = Main.rand.Next(2, 4);
                        eggnum = Main.rand.Next(2, 6);
                    } else if (randseed <= 1.5) {
                        egg = Main.rand.NextBool() ? 10 : 21;
                        eggnum = Main.rand.Next(2, 6);
                    } else if (randseed <= 3) {
                        egg = Main.rand.NextBool() ? 44 : 45;
                        eggnum = 1;
                    } else if (randseed <= 3.8) {
                        egg = 195;
                        eggnum = 1;
                    } else if (randseed <= 4.5) {
                        egg = 667;
                        eggnum = 1;
                    } else if (randseed <= 6.2) {
                        
                        if (ModContent.GetInstance<ItemToggleConfig>().EnableNameTag) {
                            if (ModContent.GetInstance<ItemToggleConfig>().EnablePaper) {
                                var item = new Item(ModContent.ItemType<Paper.PaperItem>(), 1);
                                var paper = item.ModItem as Paper.PaperItem;
                                paper.SetContent(Language.GetTextValue("Mods.QualityOfGuida.PaperText.NameTag"));
                                ImageConverterHelper.AddItemToChestRandomSlot(chest, item);
                            }
                            ImageConverterHelper.AddItemToChestRandomSlot(chest, new Item(ModContent.ItemType<NameTag.NameTagItem>(), 1));
                        }
                    }
                    break;
                case BiomeType.Dungeon:
                    if (randseed <= 1.8) {
                        egg = 71;
                        eggnum = Main.rand.Next(1, 3);
                    }
                    break;
                case BiomeType.Jungle:  
                break;
                case BiomeType.Snow:
                    if (randseed <= 2) {
                        egg = 185;
                        eggnum = Main.rand.Next(4, 10);
                    }
                    break;
                case BiomeType.Tree:
                    if (randseed <= 2) {
                        egg = 624;
                        eggnum = Main.rand.Next(1, 3);
                    }
                    break;
                case BiomeType.Ocean:
                    if (randseed <= 2) {
                        egg = 65;
                        eggnum = Main.rand.Next(2, 5);
                    }
                    break;
            };

            if (eggnum != 0 && ModContent.GetInstance<ItemToggleConfig>().EnableSpawnEgg) {
                var item = new Item(ModContent.ItemType<SpawnEgg.SpawnEggItem>(), eggnum);
                var paper = item.ModItem as SpawnEgg.SpawnEggItem;
                paper.SetBannerID(Item.NPCtoBanner(egg));
                ImageConverterHelper.AddItemToChestRandomSlot(chest, item);
            }



        }

        private BiomeType GetChestBiomeByType(int tileFrameX) {
            // 根据箱子的TileFrameX判断类型
            // 每个箱子类型的frame宽度是36像素
            int chestType = tileFrameX / 36;

            return chestType switch {
                0 => BiomeType.Surface,     // Chest (普通箱子) - 地下
                1 => BiomeType.Underground,     // Gold Chest - 地下
                2 => BiomeType.Dungeon,     // Locked Gold Chest - 地下
                10 => BiomeType.Jungle,         // Ivy Chest - 丛林
                11 => BiomeType.Snow,           // Frozen Chest - 雪地
                12 => BiomeType.Tree,        // Living Wood Chest - 地表生活木
                17 => BiomeType.Ocean,          // Water Chest - 海洋
                _ => BiomeType.None             // 其他类型不处理
            };
        }
    }

    public enum BiomeType {
        None,
        Surface,
        Underground,
        Dungeon,
        Snow,
        Jungle,
        Tree,
        Ocean
    }
}