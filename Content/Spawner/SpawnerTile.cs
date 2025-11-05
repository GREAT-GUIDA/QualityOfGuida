using QualityOfGuida.Content.SpawnEgg;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader.IO;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria;
using Microsoft.Xna.Framework;
using Terraria.Localization;
using QualityOfGuida.Content.Particles;

namespace QualityOfGuida.Content.Spawner {
    public class SpawnerTile : ModTile {
        public override bool IsLoadingEnabled(Mod mod) {
            return ModContent.GetInstance<ItemToggleConfig>().EnableSpawner;
        }
        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileSolid[Type] = false;
            Main.tileBlockLight[Type] = false;
            Main.tileNoAttach[Type] = true;
            Main.tileLighted[Type] = true;
            Main.tileSolidTop[Type] = true;

            TileID.Sets.HasOutlines[Type] = true;
            TileID.Sets.DisableSmartCursor[Type] = true;
            TileID.Sets.PreventsSandfall[Type] = false;
            TileID.Sets.Torch[Type] = false;
            DustType = DustID.Obsidian;
            MinPick = 30;
            MineResist = 6f;
            TileObjectData.newTile.Width = 2;
            TileObjectData.newTile.Height = 2;
            TileObjectData.newTile.Origin = new Point16(0, 0);
            TileObjectData.newTile.CoordinateHeights = new[] { 16, 18 };
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.newTile.UsesCustomCanPlace = true;
            TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(AfterPlacement, -1, 0, false);
            TileObjectData.addTile(Type);

            AddMapEntry(new Color(100, 100, 150), CreateMapEntryName());
        }

        public override void AnimateIndividualTile(int type, int i, int j, ref int frameXOffset, ref int frameYOffset) {
            // 获取该tile对应的TileEntity
            Tile tile = Main.tile[i, j];
            int entityX = i - tile.TileFrameX / 18;
            int entityY = j - tile.TileFrameY / 18;

            bool isActive = false;

            if (TileEntity.ByPosition.TryGetValue(new Point16(entityX, entityY), out TileEntity entity) &&
                entity is SpawnerTileEntity spawner && spawner.StoredBannerID > 0) {

                Vector2 spawnerPos = new Vector2(spawner.Position.X * 16 + 16, spawner.Position.Y * 16 + 16);

                // 检查是否有玩家在范围内
                for (int p = 0; p < Main.maxPlayers; p++) {
                    Player player = Main.player[p];
                    if (player.active && !player.dead &&
                        Vector2.Distance(player.Center, spawnerPos) <= SpawnerTileEntity.DETECTION_RANGE * 16) {
                        isActive = true;
                        break;
                    }
                }
            }

            if (isActive) {
                // 使用Main.GameUpdateCount来创建动画效果，每个spawner有独立的动画
                int animationSpeed = 8; // 动画速度
                int frame = (int)(Main.GameUpdateCount / animationSpeed) % 4; // 4帧动画循环
                frameXOffset = frame * 36; // 每帧宽度36像素 (18*2)
            } else {
                frameXOffset = 4 * 36; // 静止帧
            }
        }

        public override bool CanDrop(int i, int j) {
            return false;
        }

        public override void KillMultiTile(int i, int j, int frameX, int frameY) {
            Tile tile = Main.tile[i, j];
            int subX = tile.TileFrameX / 18;
            int subY = tile.TileFrameY / 18;
            Point16 entityPos = new Point16(i - subX, j - subY);

            ModContent.GetInstance<SpawnerTileEntity>().Kill(entityPos.X, entityPos.Y);

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int coinAmount = Main.rand.Next(100, 200);
                Item.NewItem(new EntitySource_TileBreak(i, j), i * 16, j * 16, 32, 32, ItemID.GoldCoin, coinAmount / 100);
                Item.NewItem(new EntitySource_TileBreak(i, j), i * 16, j * 16, 32, 32, ItemID.SilverCoin, coinAmount % 100);
            }
        }

        public override bool RightClick(int i, int j) {
            Tile tile = Main.tile[i, j];
            int entityX = i - tile.TileFrameX / 18;
            int entityY = j - tile.TileFrameY / 18;

            if (!TileEntity.ByPosition.TryGetValue(new Point16(entityX, entityY), out TileEntity entity) ||
                !(entity is SpawnerTileEntity spawner))
                return false;

            Player player = Main.LocalPlayer;
            Item heldItem = player.HeldItem;

            if (heldItem?.type == ModContent.ItemType<SpawnEggItem>() && heldItem.ModItem is SpawnEggItem eggItem) {
                if (!eggItem.IsEmpty()) {
                    // 发送网络消息设置Banner
                    if (Main.netMode == NetmodeID.MultiplayerClient) {
                        NetworkManager.SendMessage("SetSpawnerBanner", writer => {
                            writer.Write(entityX);
                            writer.Write(entityY);
                            writer.Write(eggItem.storedBannerID);
                        });
                    } else {
                        // 在单人游戏或服务器端直接设置
                        spawner.SetBannerID(eggItem.storedBannerID);
                    }

                    var str = Language.GetTextValue("Mods.QualityOfGuida.Items.SpawnerItem.SetSpawner",
                        SpawnEggItem.GetNPCDisplayName(Item.BannerToNPC(eggItem.storedBannerID)), Color.Green);
                    Main.NewText(str);
                    SoundEngine.PlaySound(SoundID.Item2, player.Center);
                }
            }
            return true;
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) {
            Player player = Main.LocalPlayer;
            Item heldItem = player.HeldItem;

            if (heldItem?.type == ModContent.ItemType<SpawnEggItem>() && heldItem.ModItem is SpawnEggItem eggItem) {
                if (!eggItem.IsEmpty()) {
                    return true;
                }
            }
            return false;
        }

        public override void MouseOver(int i, int j) {
            Tile tile = Main.tile[i, j];
            int entityX = i - tile.TileFrameX / 18;
            int entityY = j - tile.TileFrameY / 18;

            if (TileEntity.ByPosition.TryGetValue(new Point16(entityX, entityY), out TileEntity entity) &&
                entity is SpawnerTileEntity) {
                Player player = Main.LocalPlayer;
                player.noThrow = 2;
                player.cursorItemIconEnabled = true;
                player.cursorItemIconID = ModContent.ItemType<SpawnerItem>();
            }
        }

        public static int AfterPlacement(int i, int j, int type, int style, int direction, int alternate) {
            return ModContent.GetInstance<SpawnerTileEntity>().Hook_AfterPlacement(i, j, type, style, direction, alternate);
        }
    }
}