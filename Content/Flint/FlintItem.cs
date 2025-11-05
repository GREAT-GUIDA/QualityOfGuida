using Microsoft.Xna.Framework;
using QualityOfGuida.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using QualityOfGuida.Content.SmartCursor;

namespace QualityOfGuida.Content.Flint {
    public class FlintItem : ModItem {
        private SmartCursorConfig smartCursorConfig;
        public override bool IsLoadingEnabled(Mod mod) {
            return ModContent.GetInstance<ItemToggleConfig>().EnableFlint;
        }
        public override void SetDefaults() {
            Item.width = 20;
            Item.height = 20;
            Item.useTime = 10;
            Item.useAnimation = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = false;
            Item.maxStack = Item.CommonMaxStack;
            Item.value = Item.sellPrice(0, 0, 10, 0);
            Item.rare = ItemRarityID.Blue;
            Item.UseSound = SoundID.Item1;
            Item.useTurn = true;
            Item.autoReuse = false;

            // 配置智能光标：检测有效的门框位置
            smartCursorConfig = new SmartCursorConfig(IsValidPortalLocation);
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
                player.cursorItemIconID = ModContent.ItemType<FlintItem>();
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

            int tileX = targetPos.X;
            int tileY = targetPos.Y;

            // 检查点击的位置是否在有效范围内
            if (tileX < 0 || tileX >= Main.maxTilesX || tileY < 0 || tileY >= Main.maxTilesY) {
                return false;
            }

            // 检查点击的位置是否为空气
            Tile clickedTile = Main.tile[tileX, tileY];
            if (clickedTile.HasTile) {
                return false;
            }

            // 尝试在不同的可能位置检测门框结构
            for (int offsetX = -2; offsetX <= 0; offsetX++) {
                for (int offsetY = -3; offsetY <= 0; offsetY++) {
                    int frameLeft = tileX + offsetX;
                    int frameTop = tileY + offsetY;

                    if (IsValidPortalFrame(frameLeft, frameTop) && IsClickInsideFrame(tileX, tileY, frameLeft, frameTop)) {
                        // 找到有效的门框结构，且点击在内部空格
                        CreateNetherPortal(frameLeft, frameTop, player);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 智能光标检测函数：检查指定位置是否为有效的门框激活点
        /// </summary>
        private bool IsValidPortalLocation(int x, int y) {
            if (!WorldGen.InWorld(x, y)) return false;

            // 检查点击位置是否为空气
            Tile clickedTile = Main.tile[x, y];
            if (clickedTile.HasTile) {
                return false;
            }

            // 尝试在不同的可能位置检测门框结构
            for (int offsetX = -2; offsetX <= 0; offsetX++) {
                for (int offsetY = -3; offsetY <= 0; offsetY++) {
                    int frameLeft = x + offsetX;
                    int frameTop = y + offsetY;

                    if (IsValidPortalFrame(frameLeft, frameTop) && IsClickInsideFrame(x, y, frameLeft, frameTop)) {
                        return true; // 找到有效的门框结构，且点击在内部空格
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 检查指定位置是否为有效的4×5黑曜石门框结构
        /// </summary>
        private bool IsValidPortalFrame(int left, int top) {
            // 检查边界
            if (left < 0 || top < 0 || left + 4 > Main.maxTilesX || top + 5 > Main.maxTilesY) {
                return false;
            }

            // 检查4×5结构
            for (int x = 0; x < 4; x++) {
                for (int y = 0; y < 5; y++) {
                    int worldX = left + x;
                    int worldY = top + y;
                    Tile tile = Main.tile[worldX, worldY];

                    bool shouldBeObsidian = (x == 0 || x == 3 || y == 0 || y == 4); // 外框位置
                    bool shouldBeEmpty = !shouldBeObsidian; // 内部位置

                    if (shouldBeObsidian) {
                        // 外框必须是黑曜石
                        if (!tile.HasTile || tile.TileType != TileID.Obsidian) {
                            return false;
                        }
                    } else if (shouldBeEmpty) {
                        // 内部必须是空的
                        if (tile.HasTile) {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 检查点击位置是否在门框内部空格区域
        /// </summary>
        private bool IsClickInsideFrame(int clickX, int clickY, int frameLeft, int frameTop) {
            int localX = clickX - frameLeft;
            int localY = clickY - frameTop;

            // 检查是否在内部2×3区域
            return localX >= 1 && localX <= 2 && localY >= 1 && localY <= 3;
        }

        /// <summary>
        /// 创建地狱门
        /// </summary>
        private void CreateNetherPortal(int left, int top, Player player) {
            // 播放激活音效
            SoundEngine.PlaySound(SoundID.Dig, new Vector2(left * 16 + 32, top * 16 + 40));
            SoundEngine.PlaySound(ModAssets.PortalAmbience, new Vector2(left * 16 + 32, top * 16 + 40));

            // 移除现有的黑曜石块并放置地狱门瓦片
            for (int x = 0; x < 4; x++) {
                for (int y = 0; y < 5; y++) {
                    int worldX = left + x;
                    int worldY = top + y;

                    if (worldX >= 0 && worldX < Main.maxTilesX && worldY >= 0 && worldY < Main.maxTilesY) {
                        Tile tile = Main.tile[worldX, worldY];

                        // 清除现有方块
                        tile.HasTile = true;
                        tile.TileType = (ushort)ModContent.TileType<NetherPortal>();
                        tile.TileFrameX = (short)(x * 18);
                        tile.TileFrameY = (short)(y * 18);
                        tile.Slope = 0;
                        tile.IsHalfBlock = false;
                    }
                }
            }

            // 同步瓦片框架
            for (int x = 0; x < 4; x++) {
                for (int y = 0; y < 5; y++) {
                    if (left + x >= 0 && left + x < Main.maxTilesX && top + y >= 0 && top + y < Main.maxTilesY) {
                        WorldGen.SquareTileFrame(left + x, top + y);
                    }
                }
            }

            // 创建瓦片实体
            int entityID = NetherPortal.AfterPlacement(left, top, ModContent.TileType<NetherPortal>(), 0, 1, 0);

            // 同步到服务器（如果在多人游戏中）
            if (Main.netMode != NetmodeID.SinglePlayer) {
                NetMessage.SendTileSquare(-1, left, top, 4, 5);
                if (entityID >= 0) {
                    NetMessage.SendData(MessageID.TileEntityPlacement, -1, -1, null, left, top, entityID);
                }
            }

            // 给玩家一个成功的反馈
            if (Main.netMode != NetmodeID.Server) {
                if (Main.netMode == NetmodeID.Server) return;

                var particleManager = ParticleManager.Instance;
                if (particleManager == null) return;

                Vector2 arrivalCenter = new Vector2(left * 16 + 32, top * 16 + 40);

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
            }
        }

        public override void AddRecipes() {
            // 可以在这里添加打火石的合成配方
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ItemID.Hellstone, 1);
            recipe.AddRecipeGroup(RecipeGroupID.IronBar, 2);
            recipe.AddTile(TileID.Anvils);
            recipe.Register();
        }
    }
}