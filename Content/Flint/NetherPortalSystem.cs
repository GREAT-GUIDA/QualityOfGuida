using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.WorldBuilding;
using Terraria;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;

namespace QualityOfGuida.Content.Flint {
    /// <summary>
    /// 主系统类，负责注册世界生成步骤
    /// </summary>
    public class NetherPortalSystem : ModSystem {
        // 本地化文本
        public static LocalizedText NetherPortalPassMessage { get; private set; }

        public override void SetStaticDefaults() {
            NetherPortalPassMessage = Language.GetOrRegister(Mod.GetLocalizationKey($"WorldGen.{nameof(NetherPortalPassMessage)}"));
        }

        /// <summary>
        /// 注册自定义世界生成步骤
        /// </summary>
        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight) {
            // 找到"Buried Chests"步骤的索引
            int buriedChestsIndex = tasks.FindIndex(genpass => genpass.Name.Equals("Buried Chests"));
            if (buriedChestsIndex != -1) {
                // 在"Buried Chests"之后插入地表建筑生成步骤
                tasks.Insert(buriedChestsIndex + 1, new NetherPortalPass("Surface Buildings", 50f));
            }
        }
    }

    /// <summary>
    /// 地表建筑生成Pass - 使用图片转建筑系统
    /// </summary>
    public class NetherPortalPass : GenPass {
        // 建筑生成参数
        private const int MAX_BUILDINGS_PER_WORLD = 1;     // n: 每个世界最多生成的建筑数量
        private const int MAX_ATTEMPTS_PER_BUILDING = 40;  // m: 每个建筑的最大尝试次数

        // 建筑尺寸常量
        private const int BUILDING_TOTAL_WIDTH = 14;       // 建筑总宽度
        private const int BUILDING_TOTAL_HEIGHT = 12;      // 建筑总高度
        private const int FOUNDATION_WIDTH = 10;           // 地基宽度
        private const int FOUNDATION_DEPTH = 3;            // 地基深度（陷入地里）
        private const int BUILDING_HEIGHT = 9;             // 地上建筑高度

        public NetherPortalPass(string name, float loadWeight) : base(name, loadWeight) {
        }

        /// <summary>
        /// 执行地表建筑生成
        /// </summary>
        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            // 设置进度消息
            progress.Message = NetherPortalSystem.NetherPortalPassMessage.Value;

            //for(int i = 0; i <= 100; i += 1)WorldGen.PlaceTile(100 + i, 100, 1);

            //PlaceImageBasedBuilding(100, 100);
            if (!ModContent.GetInstance<ItemToggleConfig>().EnableFlint) return;

                int successfulBuildings = 0;
            int totalAttempts = 0;
            int maxTotalAttempts = MAX_BUILDINGS_PER_WORLD * MAX_ATTEMPTS_PER_BUILDING;

            while (successfulBuildings < MAX_BUILDINGS_PER_WORLD && totalAttempts < maxTotalAttempts) {
                totalAttempts++;

                int x = GetValidXCoordinate();
                if (x == -1) continue; // 无法找到有效坐标
                // 查找地表位置
                int surfaceY = FindSurfaceLevel(x);

                if (surfaceY != -1 && CanPlaceBuilding(x, surfaceY)) {
                    // 尝试放置建筑
                    if (PlaceImageBasedBuilding(x, surfaceY)) {
                        successfulBuildings++;
                        progress.Set((float)successfulBuildings / MAX_BUILDINGS_PER_WORLD);
                    }
                }
            }
        }

        /// <summary>
        /// 获取有效的X坐标（避开世界边缘和中间三分之一）
        /// </summary>
        /// <returns>有效的X坐标，如果无法找到返回-1</returns>
        private int GetValidXCoordinate() {
            int worldWidth = Main.maxTilesX;
            int edgeBuffer = 100; // 边缘缓冲区

            // 计算世界中间三分之一区域
            int middleStart = worldWidth / 3;
            int middleEnd = worldWidth * 2 / 3;

            // 左侧可用区域：edgeBuffer 到 middleStart
            int leftZoneStart = edgeBuffer;
            int leftZoneEnd = middleStart;

            // 右侧可用区域：middleEnd 到 worldWidth - edgeBuffer
            int rightZoneStart = middleEnd;
            int rightZoneEnd = worldWidth - edgeBuffer;

            // 检查是否有足够的空间
            if (leftZoneEnd - leftZoneStart < BUILDING_TOTAL_WIDTH &&
                rightZoneEnd - rightZoneStart < BUILDING_TOTAL_WIDTH) {
                return -1; // 没有足够空间
            }

            // 随机选择左侧或右侧区域
            bool useLeftZone = WorldGen.genRand.NextBool();

            if (useLeftZone && leftZoneEnd - leftZoneStart >= BUILDING_TOTAL_WIDTH) {
                return WorldGen.genRand.Next(leftZoneStart, leftZoneEnd - BUILDING_TOTAL_WIDTH);
            } else if (rightZoneEnd - rightZoneStart >= BUILDING_TOTAL_WIDTH) {
                return WorldGen.genRand.Next(rightZoneStart, rightZoneEnd - BUILDING_TOTAL_WIDTH);
            } else if (leftZoneEnd - leftZoneStart >= BUILDING_TOTAL_WIDTH) {
                return WorldGen.genRand.Next(leftZoneStart, leftZoneEnd - BUILDING_TOTAL_WIDTH);
            }

            return -1;
        }

        /// <summary>
        /// 查找指定x坐标的地表高度
        /// </summary>
        /// <param name="x">x坐标</param>
        /// <returns>地表y坐标，如果找不到返回-1</returns>
        private int FindSurfaceLevel(int x) {
            // 从地表附近开始向下搜索
            int startY = (int)Main.worldSurface - 250;
            int maxY = (int)Main.worldSurface + 50;

            for (int y = startY; y < maxY; y++) {
                if (!WorldGen.InWorld(x, y)) continue;

                Tile tile = Main.tile[x, y];
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    return y;
                }
            }

            return -1; // 没找到合适的地表
        }

        /// <summary>
        /// 检查是否可以在指定位置放置建筑
        /// </summary>
        /// <param name="x">中心x坐标</param>
        /// <param name="surfaceY">地表y坐标</param>
        /// <returns>是否可以放置</returns>
        private bool CanPlaceBuilding(int x, int surfaceY) {
            // 计算建筑和地基范围
            int buildingLeftX = x - BUILDING_TOTAL_WIDTH / 2;
            int buildingRightX = x + BUILDING_TOTAL_WIDTH / 2;
            int foundationLeftX = x - FOUNDATION_WIDTH / 2;
            int foundationRightX = x + FOUNDATION_WIDTH / 2;

            int buildingTopY = surfaceY - BUILDING_HEIGHT;
            int buildingBottomY = surfaceY;
            int foundationBottomY = surfaceY + FOUNDATION_DEPTH;

            // 1. 检查范围是否在世界内
            if (!WorldGen.InWorld(buildingLeftX, buildingTopY) ||
                !WorldGen.InWorld(buildingRightX, foundationBottomY))
                return false;

            // 2. 检查地基区域是否有足够的固体方块支撑
            if (!CheckFoundationSupport(foundationLeftX, foundationRightX, surfaceY, foundationBottomY))
                return false;

            // 3. 检查建筑上方空间是否足够空旷
            if (!CheckBuildingSpace(buildingLeftX, buildingRightX, buildingTopY, buildingBottomY))
                return false;

            // 4. 检查是否被水淹没
            if (!CheckNotSubmerged(buildingLeftX, buildingRightX, buildingTopY, foundationBottomY))
                return false;

            return true;
        }

        /// <summary>
        /// 检查地基支撑是否足够
        /// </summary>
        private bool CheckFoundationSupport(int leftX, int rightX, int surfaceY, int bottomY) {
            int solidCount = 0;
            int totalChecked = 0;

            // 检查地基范围内的固体方块
            for (int checkX = leftX; checkX <= rightX; checkX++) {
                for (int checkY = surfaceY; checkY <= bottomY; checkY++) {
                    if (WorldGen.InWorld(checkX, checkY)) {
                        totalChecked++;
                        Tile tile = Main.tile[checkX, checkY];
                        if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                            solidCount++;
                        }
                    }
                }
            }

            // 至少70%的地基区域需要有固体支撑
            return totalChecked > 0 && solidCount >= totalChecked * 0.7f;
        }

        /// <summary>
        /// 检查建筑空间是否空旷
        /// </summary>
        private bool CheckBuildingSpace(int leftX, int rightX, int topY, int bottomY) {
            int emptyCount = 0;
            int totalSpace = 0;

            // 检查建筑内部区域（留2格边距）
            for (int checkX = leftX + 2; checkX <= rightX - 2; checkX++) {
                for (int checkY = topY; checkY < bottomY; checkY++) {
                    if (WorldGen.InWorld(checkX, checkY)) {
                        totalSpace++;
                        Tile tile = Main.tile[checkX, checkY];
                        if (!tile.HasTile) {
                            emptyCount++;
                        }
                    }
                }
            }

            // 至少75%的空间需要是空的
            return totalSpace > 0 && emptyCount >= totalSpace * 0.75f;
        }

        /// <summary>
        /// 检查建筑是否被水淹没
        /// </summary>
        private bool CheckNotSubmerged(int leftX, int rightX, int topY, int bottomY) {
            int liquidCount = 0;
            int totalChecked = 0;

            // 检查整个建筑区域是否有液体
            for (int checkX = leftX; checkX <= rightX; checkX++) {
                for (int checkY = topY; checkY <= bottomY; checkY++) {
                    if (WorldGen.InWorld(checkX, checkY)) {
                        totalChecked++;
                        Tile tile = Main.tile[checkX, checkY];

                        // 检查是否有液体（水、岩浆、蜂蜜等）
                        if (tile.LiquidAmount > 0) {
                            liquidCount++;
                        }
                    }
                }
            }

            // 如果超过10%的区域有液体，则认为被淹没
            return totalChecked == 0 || liquidCount < totalChecked * 0.1f;
        }

        private bool PlaceImageBasedBuilding(int x, int surfaceY) {
            try {

                // 计算图片放置的偏移量
                int imageOffsetX = x - BUILDING_TOTAL_WIDTH / 2;
                int imageOffsetY = surfaceY - BUILDING_HEIGHT;
                var converter = new ImageToTileConverter();

                converter.TilePreset();
                converter.SetColorMapping(Color.Black, TileActionConfig.Tile(true, TileID.Obsidian) with {
                    CustomPostprocess = ImageConverterHelper.RandomRemoval(10)
                });
                converter.SetColorMapping(Color.Blue, TileActionConfig.Tile(true, TileID.GrayBrick) with {
                    CustomPostprocess = ImageConverterHelper.RandomReplacement(TileID.Stone, 20)
                });
                converter.SetColorMapping(Color.Red, TileActionConfig.Tile(true, TileID.GrayBrick, 0, 0, true) with {
                    CustomPostprocess = ImageConverterHelper.RandomReplacement(TileID.Stone, 20)
                });
                converter.SetColorMapping(Color.Cyan, TileActionConfig.Tile(true, TileID.EbonstoneBrick));
                converter.SetColorMapping(Color.Lime, TileActionConfig.Tile(true, TileID.Platforms, 43));

                bool left = Main.rand.NextBool();
                converter.SetColorMapping(Color.Yellow, TileActionConfig.Object(TileID.Containers, 0) with {
                    CustomPreprocess = (x, y) => {
                        left = !left;
                        return left;
                    },
                    CustomPostprocess = (x, y) => {
                        int chestIndex = Chest.FindChest(x, y - 1);
                        if (chestIndex >= 0 && chestIndex < Main.maxChests) {
                            Chest chest = Main.chest[chestIndex];
                            if (chest != null) {
                                var item = new Item(ModContent.ItemType<FlintItem>(), 1);
                                ImageConverterHelper.AddItemToChestRandomSlot(chest, item);
                                if (ModContent.GetInstance<ItemToggleConfig>().EnablePaper) {
                                    item = new Item(ModContent.ItemType<Paper.PaperItem>(), 1);
                                    var paper = item.ModItem as Paper.PaperItem;
                                    paper.SetContent(Language.GetTextValue(ModContent.GetInstance<QualityOfGuida>().GetLocalizationKey("PaperText.NetherPortal")));
                                    ImageConverterHelper.AddItemToChestRandomSlot(chest, item);
                                }
                                ImageConverterHelper.AddItemToChestRandomSlot(chest, new Item(ItemID.LavaBucket, 1));
                                ImageConverterHelper.AddItemToChestRandomSlot(chest, new Item(ItemID.WaterBucket, 1));
                                ImageConverterHelper.AddItemToChestRandomSlot(chest, new Item(Main.rand.Next(new int[] { ItemID.GoldAxe, ItemID.GoldHammer, ItemID.GoldPickaxe, ItemID.GoldShortsword }), 1, PrefixID.Legendary));
                                if (Main.rand.NextBool(2)) ImageConverterHelper.AddItemToChestRandomSlot(chest, new Item(ItemID.GoldHelmet, 1));
                                if (Main.rand.NextBool(2)) ImageConverterHelper.AddItemToChestRandomSlot(chest, new Item(ItemID.GoldGreaves, 1));
                                if (Main.rand.NextBool(2)) ImageConverterHelper.AddItemToChestRandomSlot(chest, new Item(ItemID.GoldChainmail, 1));
                            }
                        }
                    }
                });

                if (converter.LoadImageFromTexture(ModContent.Request<Texture2D>(ModAssets.ContentDir + "/Flint/NetherPortalGround1").Value)) {
                    converter.GenerateTiles(imageOffsetX, imageOffsetY);
                }

                converter.ClearColorMapping();

                converter.WallPreset();
                converter.SetColorMapping(Color.Black, TileActionConfig.Wall(true, WallID.AncientObsidianBrickWall));
                converter.SetColorMapping(Color.Red, TileActionConfig.Wall(true, WallID.MetalFence, PaintID.WhitePaint));
                int type = GetBiomeWallType(x, surfaceY);
                converter.SetColorMapping(Color.Lime, TileActionConfig.Wall(true, type));
                converter.SetColorMapping(Color.Blue, TileActionConfig.Wall(true, WallID.GrayBrick));


                if (converter.LoadImageFromTexture(ModContent.Request<Texture2D>(ModAssets.ContentDir + "/Flint/NetherPortalGround2").Value)) {
                    converter.GenerateTiles(imageOffsetX, imageOffsetY);
                }

                converter.ClearColorMapping();

                converter.TilePaintPreset();

                if (converter.LoadImageFromTexture(ModContent.Request<Texture2D>(ModAssets.ContentDir + "/Flint/NetherPortalGround3").Value)) {
                    converter.GenerateTiles(imageOffsetX, imageOffsetY);
                }
                return true;
            } catch (Exception ex) {
                return false;
            }
        }

        private static int GetBiomeWallType(int x, int y) {
            // 检测半径
            int radius = 25;

            // 计数器
            int sandCount = 0;
            int snowCount = 0;
            int jungleCount = 0;
            int corruptionCount = 0;
            int crimsonCount = 0;

            // 扫描周围区域的瓦片
            for (int i = x - radius; i <= x + radius; i++) {
                for (int j = y - radius; j <= y + radius; j++) {
                    if (WorldGen.InWorld(i, j) && Main.tile[i, j].HasTile) {
                        int tileType = Main.tile[i, j].TileType;

                        // 沙漠检测
                        if (tileType == TileID.Sand || tileType == TileID.Sandstone ||
                            tileType == TileID.HardenedSand || tileType == TileID.CorruptSandstone ||
                            tileType == TileID.CrimsonSandstone || tileType == TileID.HallowSandstone)
                            sandCount++;

                        // 雪原检测
                        if (tileType == TileID.SnowBlock || tileType == TileID.IceBlock)
                            snowCount++;

                        // 丛林检测
                        if (tileType == TileID.JungleGrass || tileType == TileID.Mud ||
                            tileType == TileID.JungleVines || tileType == TileID.RichMahogany)
                            jungleCount++;

                        // 腐化检测
                        if (tileType == TileID.CorruptGrass || tileType == TileID.Ebonstone ||
                            tileType == TileID.CorruptThorns)
                            corruptionCount++;

                        // 猩红检测
                        if (tileType == TileID.CrimsonGrass || tileType == TileID.Crimstone ||
                            tileType == TileID.CrimsonThorns)
                            crimsonCount++;
                    }
                }
            }

            // 判断主要环境（需要足够的瓦片数量）
            int threshold = 50; // 最小瓦片数阈值

            if (sandCount >= threshold) return WallID.Sandstone;
            if (snowCount >= threshold) return WallID.IceEcho;
            if (jungleCount >= threshold) return WallID.Jungle;
            if (corruptionCount >= threshold) return WallID.CorruptGrassEcho;
            if (crimsonCount >= threshold) return WallID.CrimsonGrassEcho;

            // 默认草地
            return WallID.Grass;
        }
    }
}