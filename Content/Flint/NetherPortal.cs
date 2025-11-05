using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.Utilities;
using QualityOfGuida.Content.Particles;

namespace QualityOfGuida.Content.Flint {
    public class NetherPortal : ModTile {
        private static Point? lastClickedTile = null;
        private static int ambientSoundTimer = 0;
        private static readonly int AMBIENT_SOUND_INTERVAL = 180 * 6;
        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileSolid[Type] = false;
            Main.tileBlockLight[Type] = false;
            Main.tileLavaDeath[Type] = false;
            Main.tileSolidTop[Type] = true;
            TileID.Sets.HasOutlines[Type] = true;
            TileID.Sets.DisableSmartCursor[Type] = true;

            Main.tileLighted[Type] = true;
            TileID.Sets.Torch[Type] = false;

            MinPick = 65;

            AddMapEntry(new Color(20, 20, 20), CreateMapEntryName());
            AddMapEntry(new Color(120, 40, 200), CreateMapEntryName());

            TileObjectData.newTile.Width = 4;
            TileObjectData.newTile.Height = 5;
            TileObjectData.newTile.Origin = new Point16(0, 0);
            TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16, 16, 16 };
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.newTile.UsesCustomCanPlace = true;
            TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(AfterPlacement, -1, 0, false);
            TileObjectData.addTile(Type);
        }

        // 添加光照效果
        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            if (IsPortalCenter(i, j)) {
                // 传送门中心发出紫色光芒
                r = 0.7f;  // 红色分量
                g = 0.2f;  // 绿色分量
                b = 1f;  // 蓝色分量
            }
        }

        // 添加发光遮罩绘制
        public override void PostDraw(int i, int j, SpriteBatch spriteBatch) {
            // 获取发光纹理
            Texture2D glowTexture = ModContent.Request<Texture2D>(Texture + "_Glow").Value;

            // 获取方块信息
            Tile tile = Main.tile[i, j];
            Vector2 zero = new Vector2(Main.offScreenRange, Main.offScreenRange);

            if (Main.drawToScreen) {
                zero = Vector2.Zero;
            }

            // 计算绘制位置
            Vector2 position = new Vector2(i * 16 - (int)Main.screenPosition.X, j * 16 - (int)Main.screenPosition.Y) + zero;

            int frameXOffset = Main.tileFrame[Type] * 72;

            // 获取源矩形
            Rectangle sourceRect = new Rectangle(tile.TileFrameX + frameXOffset, tile.TileFrameY, 16, 16);
            spriteBatch.EndAndBeginExt(BlendState.Additive, null, Matrix.Identity);
            spriteBatch.Draw(glowTexture, position, sourceRect, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            spriteBatch.EndAndBeginExt(BlendState.AlphaBlend, null, Matrix.Identity);
        }

        public override ushort GetMapOption(int i, int j) => (ushort)(IsPortalCenter(i, j) ? 1 : 0);

        public override void AnimateIndividualTile(int type, int i, int j, ref int frameXOffset, ref int frameYOffset) {
            frameXOffset = Main.tileFrame[type] * 72;
        }

        public override void AnimateTile(ref int frame, ref int frameCounter) {
            frameCounter++;
            if (frameCounter > 3) {
                frameCounter = 0;
                frame = ++frame > 15 ? 0 : frame;
            }
        }

        public override void NearbyEffects(int i, int j, bool closer) {
            // 只在客户端播放音效
            if (Main.netMode == NetmodeID.Server) {
                return;
            }

            // 只有当玩家靠近时才考虑播放音效
            if (!closer) {
                return;
            }

            // 只在传送门中心位置播放音效，避免重复
            if (!IsPortalCenter(i, j)) {
                return;
            }

            // 控制音效播放频率
            ambientSoundTimer--;
            if (ambientSoundTimer <= 0) {
                ambientSoundTimer = 360 + Main.rand.Next(180);

                // 计算玩家与传送门的距离
                Player player = Main.LocalPlayer;
                Vector2 portalCenter = GetPortalCenterPosition(i, j);
                float distanceToPlayer = Vector2.Distance(player.Center, portalCenter);

                // 根据距离调整音量 (最大距离约为 10 个方块)
                float maxDistance = 160f; // 10 * 16 像素
                if (distanceToPlayer <= maxDistance) {
                    float volume = 1f - (distanceToPlayer / maxDistance);
                    volume = MathHelper.Clamp(volume, 0.3f, 0.7f);

                    SoundStyle ambientSound = new SoundStyle("QualityOfGuida/Assets/Sounds/PortalAmbience") {
                        Volume = volume,
                        PitchVariance = 0.1f,
                        MaxInstances = 10,
                        SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
                    };

                    SoundEngine.PlaySound(ambientSound, portalCenter);
                }
            }
        }

        public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref TileDrawInfo drawData) {
            if (Main.gamePaused || !Main.instance.IsActive) {
                return;
            }

            // 只在客户端生成粒子效果
            if (Main.netMode == NetmodeID.Server) {
                return;
            }

            if (!Lighting.UpdateEveryFrame || new FastRandom(Main.TileFrameSeed).WithModifier(i, j).Next(2) == 0) {
                if (IsPortalCenter(i, j)) {
                    // 获取粒子管理器实例
                    var particleManager = ParticleManager.Instance;
                    if (particleManager == null) return;

                    // 控制粒子生成频率
                    if (!Main.rand.NextBool(4)) return;

                    Vector2 portalCenter = GetPortalCenterPosition(i, j);

                    int particleCount = Main.rand.Next(2);
                    for (int k = 0; k < particleCount; k++) {
                        float angle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                        float distance = Main.rand.NextFloat(40f, 90f);

                        Vector2 startOffset = new Vector2(
                            (float)System.Math.Cos(angle) * distance,
                            (float)System.Math.Sin(angle) * distance
                        );

                        Vector2 startPosition = portalCenter + startOffset;

                        Vector2 directionToCenter = Vector2.Normalize(portalCenter - startPosition);
                        Vector2 initialVelocity = directionToCenter * Main.rand.NextFloat(0.1f, 0.6f);

                        // 创建粒子
                        var particle = particleManager.NewParticle<NetherPortalParticle>(
                            startPosition,
                            initialVelocity,
                            type: 0,
                            alpha: 0f,
                            scale: Main.rand.NextFloat(0.8f, 1.3f)
                        );

                        // 设置目标位置
                        if (particle != null) {
                            particle.SetTarget(portalCenter);
                        }
                    }
                }
            }
        }
        /// <summary>
        /// 获取传送门的实际中心位置
        /// </summary>
        private Vector2 GetPortalCenterPosition(int i, int j) {
            // 获取传送门左上角位置
            Tile tile = Main.tile[i, j];
            int left = i - (tile.TileFrameX / 18);
            int top = j - (tile.TileFrameY / 18);

            // 传送门是4x5的结构，中心位置计算
            Vector2 portalCenter = new Vector2(
                (left + 2) * 16,
                (top + 2.5f) * 16
            );

            return portalCenter;
        }

        private bool IsPortalCenter(int i, int j) {
            Tile tile = Main.tile[i, j];
            int frameX = tile.TileFrameX / 18;
            int frameY = tile.TileFrameY / 18;
            return (frameX >= 1 && frameX <= 2) && (frameY >= 1 && frameY <= 3);
        }

        private bool IsPortalCenterLocal(int localX, int localY) =>
            (localX >= 1 && localX <= 2) && (localY >= 1 && localY <= 3);

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override bool RightClick(int i, int j) {
            Player player = Main.LocalPlayer;
            Tile tile = Main.tile[i, j];
            int left = i - (tile.TileFrameX / 18);
            int top = j - (tile.TileFrameY / 18);
            Point16 entityPos = new Point16(left, top);
            if (TileEntity.ByPosition.TryGetValue(entityPos, out TileEntity entity) &&
                entity is NetherPortalTileEntity portalEntity) {
                portalEntity.TeleportPlayer(player);
                return true;
            }

            // 备用传送逻辑
            /*if (player.ZoneUnderworldHeight)
                player.Teleport(new Vector2(Main.spawnTileX * 16, Main.spawnTileY * 16));
            else
                player.Teleport(new Vector2(player.position.X, (Main.maxTilesY - 200) * 16));*/

            return true;
        }

        public override bool CanKillTile(int i, int j, ref bool blockDamaged) {
            lastClickedTile = new Point(i, j);
            return true;
        }

        public override bool CanDrop(int i, int j) => false;

        public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem) {
            noItem = true;
        }

        public override void NumDust(int i, int j, bool fail, ref int num) => num = 0;

        public override bool CreateDust(int i, int j, ref int type) {
            return false;
        }

        public override void KillMultiTile(int i, int j, int frameX, int frameY) {
            int left = i, top = j;
            int actualClickX = lastClickedTile?.X ?? i;
            int actualClickY = lastClickedTile?.Y ?? j;
            int localX = actualClickX - left;
            int localY = actualClickY - top;

            lastClickedTile = null;
            Point16 entityPos = new Point16(left, top);
            if (TileEntity.ByPosition.TryGetValue(entityPos, out TileEntity entity)) {
                TileEntity.ByPosition.Remove(entityPos);
                TileEntity.ByID.Remove(entity.ID);
            }

            SoundEngine.PlaySound(SoundID.Shatter, new Vector2(actualClickX * 16, actualClickY * 16));

            if (!IsPortalCenterLocal(localX, localY)) {
                Item.NewItem(WorldGen.GetItemSource_FromTileBreak(actualClickX, actualClickY),
                    actualClickX * 16, actualClickY * 16, 16, 16, ItemID.Obsidian, 1);
            }

            for (int x = 0; x < 4; x++) {
                for (int y = 0; y < 5; y++) {
                    int worldX = left + x, worldY = top + y;
                    if (worldX >= 0 && worldX < Main.maxTilesX && worldY >= 0 && worldY < Main.maxTilesY) {
                        Tile tile = Main.tile[worldX, worldY];
                        if ((x == 0 || x == 3 || y == 0 || y == 4) && !(x == localX && y == localY)) {
                            tile.HasTile = true;
                            tile.TileType = TileID.Obsidian;
                            tile.TileFrameX = 0;
                            tile.TileFrameY = 0;
                            tile.Slope = 0;
                            tile.IsHalfBlock = false;
                        } else {
                            tile.HasTile = false;
                            tile.TileType = 0;
                            tile.TileFrameX = 0;
                            tile.TileFrameY = 0;
                        }
                    }
                }
            }

            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 5; y++)
                    if (left + x >= 0 && left + x < Main.maxTilesX && top + y >= 0 && top + y < Main.maxTilesY)
                        WorldGen.SquareTileFrame(left + x, top + y);

            if (Main.netMode != NetmodeID.MultiplayerClient)
                NetMessage.SendTileSquare(-1, left, top, 4, 5);
        }

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
        }

        public static int AfterPlacement(int i, int j, int type, int style, int direction, int alternate) {
            return ModContent.GetInstance<NetherPortalTileEntity>().Hook_AfterPlacement(i, j, type, style, direction, alternate);
        }
    }
}