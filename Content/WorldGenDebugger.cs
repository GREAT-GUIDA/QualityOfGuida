using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections;
using GuidaSharedCode;
using QualityOfGuida.Content.Flint;
using Terraria.Localization;

namespace QualityOfGuida.Content
{
    /// <summary>
    /// 调试和测试工具
    /// </summary>
    public class ImageConverterDebugger : ModSystem {
        public override void PostUpdateWorld() {
            // 测试热键：按 F7 在鼠标位置生成测试结构
            if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F7) &&
                !Main.oldKeyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F7)) {
                if (Main.LocalPlayer.active) {
                    //TestImageGeneration();
                }
            }
        }

        private void TestImageGeneration() {
            int mouseX = (int)Main.MouseWorld.X / 16;
            int mouseY = (int)Main.MouseWorld.Y / 16;

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
                            item = new Item(ModContent.ItemType<Paper.PaperItem>(), 1);
                            var paper = item.ModItem as Paper.PaperItem;
                            paper.SetContent(Language.GetTextValue(Mod.GetLocalizationKey("PaperText.NetherPortal")));
                            ImageConverterHelper.AddItemToChestRandomSlot(chest, item);
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
            
            if (converter.LoadImageFromTextureDirect(ModAsset.NetherPortalGround1.Value)) {
                converter.GenerateTiles(mouseX, mouseY);
            }
            
            converter.ClearColorMapping();

            converter.WallPreset();
            converter.SetColorMapping(Color.Black, TileActionConfig.Wall(true, WallID.AncientObsidianBrickWall));
            converter.SetColorMapping(Color.Red, TileActionConfig.Wall(true, WallID.MetalFence, PaintID.WhitePaint));
            int type = GetBiomeWallType(mouseX, mouseY);
            converter.SetColorMapping(Color.Lime, TileActionConfig.Wall(true, type));
            converter.SetColorMapping(Color.Blue, TileActionConfig.Wall(true, WallID.GrayBrick));


            if (converter.LoadImageFromTextureDirect(ModAsset.NetherPortalGround2.Value)) {
                converter.GenerateTiles(mouseX, mouseY);
            }

            converter.ClearColorMapping();

            converter.TilePaintPreset();

            if (converter.LoadImageFromTextureDirect(ModAsset.NetherPortalGround3.Value)) {
                converter.GenerateTiles(mouseX, mouseY);
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
