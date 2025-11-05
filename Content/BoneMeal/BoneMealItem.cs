using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Microsoft.Xna.Framework;
using System;
using QualityOfGuida.Content.SmartCursor;
using tModPorter;
using QualityOfGuida.Content.Torcherino;
using QualityOfGuida.Content.Particles;
using System.Reflection;
using Terraria.ModLoader.UI;

namespace QualityOfGuida.Content.BoneMeal {
    public class BoneMealItem : ModItem {
        private SmartCursorConfig smartCursorConfig;
        public override bool IsLoadingEnabled(Mod mod) {
            return ModContent.GetInstance<ItemToggleConfig>().EnableBoneMeal;
        }

        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 25;
        }

        public override void SetDefaults() {
            Item.width = 16;
            Item.height = 16;
            Item.useTime = 15;
            Item.useAnimation = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(copper: 10);
            Item.rare = ItemRarityID.White;
            Item.UseSound = SoundID.Item1;
            Item.consumable = true;
            Item.maxStack = Item.CommonMaxStack;
            Item.noMelee = true;

            // 配置智能光标：要求鼠标位置为空，验证相邻是否有土壤/草
            smartCursorConfig = new SmartCursorConfig(IsAdjacentSuitableTile);
            //smartCursorConfig.RequireNoSolidMousePosition = true;
        }

        public override bool CanUseItem(Player player) {
            Vector2 mouseWorld = Main.MouseWorld;
            int targetX = (int)(mouseWorld.X / 16f);
            int targetY = (int)(mouseWorld.Y / 16f);

            if (!WorldGen.InWorld(targetX, targetY) || !SmartCursorManager.IsInRange(targetX, targetY, player)) {
                return false;
            }
            return true;
        }

        public override void HoldItem(Player player) {
            Vector2 mouseWorld = Main.MouseWorld;
            int targetX = (int)(mouseWorld.X / 16f);
            int targetY = (int)(mouseWorld.Y / 16f);

            if (WorldGen.InWorld(targetX, targetY) && SmartCursorManager.IsInRange(targetX, targetY, player)) {
                player.cursorItemIconEnabled = true;
                player.cursorItemIconID = ModContent.ItemType<BoneMealItem>();
                SmartCursorManager.UpdateDraw(targetX, targetY, player, smartCursorConfig);
            }
        }

        public override bool? UseItem(Player player) {
            Vector2 mouseWorld = Main.MouseWorld;
            int originalTargetX = (int)(mouseWorld.X / 16f);
            int originalTargetY = (int)(mouseWorld.Y / 16f);

            // 使用智能光标管理器获取目标位置
            Point targetPos = SmartCursorManager.GetUseTarget(originalTargetX, originalTargetY, player, smartCursorConfig);

            // 检查目标位置是否有效
            if (targetPos.X == -1 || targetPos.Y == -1) {
                return false;
            }

            bool anyGrassGrown = false;

            for (int i = targetPos.X - 2; i <= targetPos.X + 2; i++) {
                for (int j = targetPos.Y - 2; j <= targetPos.Y + 2; j++) {
                    if (WorldGen.InWorld(i, j) && TryGrowGrass(i, j, player)) {
                        anyGrassGrown = true;
                    }
                }
            }
            TorcherinoSystem.TryPlantAlch(targetPos.X, targetPos.Y, 2, 25);
            for (int aaa = 0; aaa <= 4; aaa += 1) {
                for (int i = targetPos.X - 2; i <= targetPos.X + 2; i++) {
                    for (int j = targetPos.Y - 2; j <= targetPos.Y + 2; j++) {
                        if (WorldGen.InWorld(i, j)) {
                            if (j < Main.worldSurface) {
                                TorcherinoSystem.updateOvergroundTileMethod.Invoke(null, new object[]
                                {
                                    i, j, false, 3
                                });
                            } else {
                                TorcherinoSystem.updateUndergroundTileMethod.Invoke(null, new object[]
                                {
                                    i, j, false, 3
                                });
                            }
                        }
                    }
                }
            }
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                NetMessage.SendTileSquare(-1, targetPos.X - 2, targetPos.Y - 2, 5, 5);
            }
            if (anyGrassGrown) {
                Vector2 effectPosition = new Vector2(targetPos.X * 16 + 8, targetPos.Y * 16 + 10);

                SoundEngine.PlaySound(SoundID.Grass, effectPosition);

                for (int i = 0; i < 24; i++) {
                    Vector2 particlePos = effectPosition + Main.rand.NextVector2Square(-40, 40);

                    var particle = ParticleManager.Instance.NewParticle<BoneMealParticle>(
                        particlePos, Main.rand.NextVector2Circular(1, 1) * 0.1f, 0, 1f, Main.rand.NextFloat(0.8f, 1.2f));
                    if (particle != null) {
                        particle.color = Color.CornflowerBlue;
                        particle.timeLeft = Main.rand.Next(60, 100) / 2;
                        particle.lightColor = Color.Lime;
                        particle.velocity.Y -= Main.rand.NextFloat(0, 0.5f);
                    }
                }
                return true;
            }
            return false;
        }

        private bool IsAdjacentSuitableTile(int x, int y) {
            if (!WorldGen.InWorld(x, y)) return false;

            Tile tile = Main.tile[x, y];
            if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                return (IsSuitableTile(tile)) && SmartCursorManager.HasOpenAdjacentSpace(x, y);
            }

            for (int dx = -1; dx <= 1; dx++) {
                for (int dy = -1; dy <= 1; dy++) {
                    if (dx == 0 && dy == 0) continue;
                    if (dx != 0 && dy != 0) continue;
                    int checkX = x + dx;
                    int checkY = y + dy;
                    if (WorldGen.InWorld(checkX, checkY)) {
                        tile = Main.tile[checkX, checkY];
                        if (tile.HasTile && (IsSuitableAdjacentTile(tile))) {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        private bool IsSuitableTile(Tile tile) {
            if (tile.TileType == TileID.Cactus) {
                return true;
            }
            if (tile.TileType == TileID.Saplings) {
                return true;
            }
            if (Main.tileAlch[tile.TileType]) {
                return true;
            }
            if (tile.TileType == TileID.PlanterBox) {
                return true;
            }
            if (tile.TileType == TileID.ClayPot) {
                return true;
            }
            return IsSuitableAdjacentTile(tile);
        }

        private bool IsSuitableAdjacentTile(Tile tile) {
            if (IsSoilTile(tile.TileType) || IsGrassTile(tile.TileType)) {
                return true;
            }
            if (tile.TileType == TileID.SnowBlock || tile.TileType == TileID.Sand) {
                return true;
            }
            return false;
        }


        private bool TryGrowGrass(int i, int j, Player player) {
            Tile tile = Main.tile[i, j];
            if (!tile.HasTile) return false;
            if (!tile.HasTile || !IsSoilTile(tile.TileType) || !SmartCursorManager.HasOpenAdjacentSpace(i, j)) {
                return true;
            }

            int grassType = GetAppropriateGrassType(tile.TileType, player);
            tile.TileType = (ushort)grassType;
            WorldGen.SquareTileFrame(i, j);

            return true;
        }

        private bool IsSoilTile(int tileType) {
            return tileType == TileID.Dirt || tileType == TileID.Mud || tileType == TileID.Ash;
        }

        private bool IsGrassTile(int tileType) {
            return tileType == TileID.Grass || tileType == TileID.CorruptGrass ||
                   tileType == TileID.CrimsonGrass || tileType == TileID.HallowedGrass ||
                   tileType == TileID.JungleGrass || tileType == TileID.MushroomGrass ||
                   tileType == TileID.AshGrass;
        }

        private int GetAppropriateGrassType(int soilType, Player player) {
            switch (soilType) {
                case TileID.Mud: return TileID.JungleGrass;
                case TileID.Ash: return TileID.AshGrass;
                case TileID.Dirt:
                default:
                    if (player.ZoneCorrupt) return TileID.CorruptGrass;
                    if (player.ZoneCrimson) return TileID.CrimsonGrass;
                    if (player.ZoneHallow && Main.hardMode) return TileID.HallowedGrass;
                    if (player.ZoneGlowshroom) return TileID.MushroomGrass;
                    return TileID.Grass;
            }
        }
        public override void AddRecipes() {
            Recipe recipe = CreateRecipe(2);
            recipe.AddIngredient(ItemID.Bone);
            recipe.AddTile(TileID.WorkBenches);
            recipe.Register();
        }
        
    }
}