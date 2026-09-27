using GuidaSharedCode;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace QualityOfGuida.Content.Torcherino {
    public class TorcherinoTile : ModTile {
        private Asset<Texture2D> flameTexture;
        public override bool IsLoadingEnabled(Mod mod) {
            return ModContent.GetInstance<ItemToggleConfig>().EnableTorcherino;
        }
        public override void SetStaticDefaults() {
            // 基础属性
            Main.tileLighted[Type] = true;          // 会发光
            Main.tileFrameImportant[Type] = true;   // 帧重要（用于物体数据）
            Main.tileSolid[Type] = false;           // 非实体块
            Main.tileNoAttach[Type] = true;         // 不能被其他物块附着
            Main.tileNoFail[Type] = true;           // 不会因为支撑问题而掉落
            Main.tileWaterDeath[Type] = true;       // 遇水销毁

            // 火把特殊设置
            TileID.Sets.FramesOnKillWall[Type] = true;        // 墙壁被破坏时掉落
            TileID.Sets.DisableSmartCursor[Type] = true;      // 禁用智能光标
            TileID.Sets.DisableSmartInteract[Type] = true;    // 禁用智能交互
            TileID.Sets.Torch[Type] = true;                   // 标记为火把

            // 设置灰尘类型（破坏时的粒子效果）
            DustType = DustID.Torch;

            // 设置为火把类型（用于合成等）
            AdjTiles = new int[] { TileID.Torches };

            // 添加到房间照明需求计数
            AddToArray(ref TileID.Sets.RoomNeeds.CountsAsTorch);

            // 复制原版火把的放置数据
            TileObjectData.newTile.CopyFrom(TileObjectData.GetTileData(TileID.Torches, 0));
            TileObjectData.addTile(Type);

            // 地图颜色和名称
            AddMapEntry(new Color(100, 150, 255), Language.GetText("ItemName.Torch"));

            // 加载火焰贴图
            if (!Main.dedServ) {
                flameTexture = ModAsset.TorcherinoTile_Flame;
            }
        }

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = ModContent.ItemType<TorcherinoItem>();
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            Tile tile = Main.tile[i, j];

            // 检查火把是否点燃（帧X < 66 表示点燃状态）
            if (tile.TileFrameX < 66) {
                // 设置蓝色光照
                r = 0.4f * 2f;   // 红色分量
                g = 0.6f * 2f;   // 绿色分量  
                b = 1.0f * 2f;   // 蓝色分量
            }
        }



        public override void PlaceInWorld(int i, int j, Item item) {
            // 将这个位置添加到全局更新列表
            TorcherinoSystem.AddTorchPosition(new Point16(i, j));
        }

        public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem) {
            // 从全局更新列表中移除
            TorcherinoSystem.RemoveTorchPosition(new Point16(i, j));
        }

        public override void SetDrawPositions(int i, int j, ref int width, ref int offsetY, ref int height, ref short tileFrameX, ref short tileFrameY) {
            // 如果上方有实体块，稍微降低绘制位置，避免火焰重叠
            offsetY = 0;
            if (WorldGen.SolidTile(i, j - 1)) {
                offsetY = 4;
            }
        }

        public override void PostDraw(int i, int j, SpriteBatch spriteBatch) {
            var tile = Main.tile[i, j];

            if (!TileDrawing.IsVisible(tile) || flameTexture == null) {
                return;
            }

            // 计算偏移
            int offsetY = 0;
            if (WorldGen.SolidTile(i, j - 1)) {
                offsetY = 4;
            }

            Vector2 zero = new Vector2(Main.offScreenRange, Main.offScreenRange);
            if (Main.drawToScreen) {
                zero = Vector2.Zero;
            }

            // 创建随机种子用于火焰效果
            ulong randSeed = Main.TileFrameSeed ^ (ulong)((long)j << 32 | (long)(uint)i);

            // 蓝色火焰颜色
            Color color = new Color(0, 0, 255, 200);
            int width = 20;
            int height = 20;
            int frameX = tile.TileFrameX;
            int frameY = tile.TileFrameY;

            // 绘制多个火焰层，创造闪烁效果
            for (int k = 0; k < 5; k++) {
                color = Color.Lerp(Color.Blue, Color.White, (float)k / 4);
                float xx = Utils.RandomInt(ref randSeed, -8, 9) * 0.15f;
                float yy = Utils.RandomInt(ref randSeed, -8, 1) * 0.35f;

                Vector2 drawPosition = new Vector2(
                    i * 16 - (int)Main.screenPosition.X - (width - 16f) / 2f + xx,
                    j * 16 - (int)Main.screenPosition.Y + offsetY + yy
                ) + zero;

                spriteBatch.Draw(
                    flameTexture.Value,
                    drawPosition,
                    new Rectangle(frameX, frameY, width, height),
                    color,
                    0f,
                    default,
                    1f,
                    SpriteEffects.None,
                    0f
                );
            }


            for (int k = 0; k < 1; k++) {
                Vector2 dustPosition = new Vector2(
                    i * 16,
                    j * 16
                );

                Dust dust = Dust.NewDustDirect(
                    dustPosition,
                    16, 16,
                    DustID.Clentaminator_Cyan,  // 使用原版火把灰尘
                    0f, 0f, 100
                );

                dust.velocity *= 0.2f;
                dust.velocity.Y -= 1.2f;


                dust = Dust.NewDustDirect(
                    dustPosition,
                    16, 16,
                    DustID.BlueTorch,  // 使用原版火把灰尘
                    0f, 0f, 100
                );

                if (!Main.rand.NextBool(2)) {
                    dust.noGravity = true;
                }

                dust.velocity *= 0.3f;
                dust.velocity.Y -= 1.8f;
            }
        }

        public override void NumDust(int i, int j, bool fail, ref int num) {
            // 破坏时产生1-3个灰尘粒子
            num = Main.rand.Next(1, 4);
        }
    }
}