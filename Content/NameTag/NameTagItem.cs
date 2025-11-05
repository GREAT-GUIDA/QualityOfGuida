using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QualityOfGuida.Content.Paper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace QualityOfGuida.Content.NameTag {
    public class NameTagItem : GuidaItem {
        public string nameContent = "";
        public NPC targetNPC; // 目标NPC
        public override bool IsLoadingEnabled(Mod mod) {
            return ModContent.GetInstance<ItemToggleConfig>().EnableNameTag;
        }
        public override string GetDynamicDisplayName() {
            return nameContent;
        }

        public override string GetDynamicTexturePath() {
            if (string.IsNullOrEmpty(nameContent)) {
                return "QualityOfGuida/Content/NameTag/NameTagItem1";
            }

            if (nameContent.ToLower().Contains("boulder")) return "QualityOfGuida/Content/NameTag/NameTagItem7";
            if (nameContent.ToLower().Contains("king")) return "QualityOfGuida/Content/NameTag/NameTagItem8";
            if (nameContent.ToLower().Contains("hitbox")) return "QualityOfGuida/Content/NameTag/NameTagItem3";
            if (nameContent.ToLower().Contains("rainbow")) return "QualityOfGuida/Content/NameTag/NameTagItem6";
            if (nameContent.ToLower().Contains("giant")) return "QualityOfGuida/Content/NameTag/NameTagItem5";
            if (nameContent.ToLower().Contains("mini")) return "QualityOfGuida/Content/NameTag/NameTagItem9";
            if (nameContent.ToLower().Contains("reverse")) return "QualityOfGuida/Content/NameTag/NameTagItem4";
            return "QualityOfGuida/Content/NameTag/NameTagItem2";
        }

        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.useAnimation = 10;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.value = Item.buyPrice(silver: 5);
            Item.rare = ItemRarityID.Blue;
            Item.consumable = false;

            UpdateStackAndProperties();
        }

        private void UpdateStackAndProperties() {
            Item.maxStack = string.IsNullOrEmpty(nameContent) ? Item.CommonMaxStack : 1;
        }

        public override bool CanStack(Item item2) {
            if (item2.ModItem is NameTagItem otherNameTag) {
                return string.IsNullOrEmpty(nameContent) && string.IsNullOrEmpty(otherNameTag.nameContent);
            }
            return false;
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                return true;
            } else {
                return targetNPC != null;
            }
        }

        public override bool? UseItem(Player player) {
            if (player.altFunctionUse == 2) {
                Item.useStyle = ItemUseStyleID.HoldUp;
                SoundEngine.PlaySound(ModAssets.PaperOpen, player.Center);
                OpenNameTagEditor(player);
                return true;
            } else {
                Item.useStyle = ItemUseStyleID.Swing;

                if (Main.netMode == NetmodeID.SinglePlayer) {
                    // 单人游戏：直接处理
                    return ProcessNPCNaming();
                } else if (Main.netMode == NetmodeID.MultiplayerClient) {
                    // 多人游戏客户端：发送请求到服务端
                    SendNamingRequest();
                    return true;
                } else if (Main.netMode == NetmodeID.Server) {
                    // 多人游戏服务端：不在这里处理，通过网络消息处理
                    // 但是需要返回true表示使用成功，这样客户端才能正常使用物品
                    return true;
                }

                return false;
            }
        }

        private void SendNamingRequest() {
            if (targetNPC == null) return;

            ModContent.GetInstance<NameTagSyncSystem>().SendNamingRequest(targetNPC, nameContent);

            // 客户端立即播放音效和特效作为反馈
            SoundEngine.PlaySound(SoundID.Item1, targetNPC.Center);
            if (string.IsNullOrEmpty(nameContent)) {
                CreateClearingEffect(targetNPC);
            } else {
                CreateNamingEffect(targetNPC);
            }
        }


        // 处理NPC命名逻辑
        private bool ProcessNPCNaming() {
            if (targetNPC == null) return false;
            if (string.IsNullOrEmpty(nameContent)) {
                return ClearNPCName(targetNPC);
            } else {
                return NameNPC(targetNPC);
            }
        }

        // 为NPC命名
        private bool NameNPC(NPC npc) {
            if (npc == null || string.IsNullOrEmpty(nameContent))
                return false;

            NPCNamingSystem.SetNPCName(npc, nameContent);

            SoundEngine.PlaySound(SoundID.Item1, npc.Center);
            CreateNamingEffect(npc);
            return true;
        }

        // 清除NPC名字
        private bool ClearNPCName(NPC npc) {
            if (npc == null)
                return false;
            ModContent.GetInstance<SimpleNamedNPCRevengeSystem>().StopTracking(npc.whoAmI);
            NPCNamingSystem.SetNPCName(npc, null);
            SoundEngine.PlaySound(SoundID.Item1, npc.Center);
            CreateClearingEffect(npc);
            return true;
        }

        // 创建命名特效
        private void CreateNamingEffect(NPC npc) {
            for (int i = 0; i < 12; i++) {
                Dust dust = Dust.NewDustDirect(
                    npc.position,
                    npc.width,
                    npc.height,
                    DustID.MagicMirror);
                dust.velocity = Vector2.One.RotatedByRandom(MathHelper.TwoPi) * Main.rand.NextFloat(0.5f, 1.5f);
                dust.scale = 0.8f + Main.rand.NextFloat(0.4f);
            }
        }

        // 创建清除名字特效
        private void CreateClearingEffect(NPC npc) {
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustDirect(
                    npc.position,
                    npc.width,
                    npc.height,
                    DustID.Smoke); // 使用烟雾特效表示清除
                dust.velocity = Vector2.One.RotatedByRandom(MathHelper.TwoPi) * Main.rand.NextFloat(0.3f, 1.0f);
                dust.scale = 0.6f + Main.rand.NextFloat(0.3f);
                dust.alpha = 100;
            }
        }

    


        private bool IsValidTarget(NPC npc) {
            if (string.IsNullOrEmpty(nameContent)) {
                return !string.IsNullOrEmpty(NPCNamingSystem.GetNPCName(npc));
            } else {
                string currentName = NPCNamingSystem.GetNPCName(npc);
                return string.IsNullOrEmpty(currentName) || !currentName.Equals(nameContent);
            }
        }

        public NPC GetTargetNPC(Player player) {
            var mouseSync = player.GetModPlayer<MouseSyncPlayer>();
            Vector2 mouseWorld = mouseSync.GetMouseWorld();
            Vector2 playerCenter = player.Center;

            targetNPC = null;
            float bestDistance = 100000;

            foreach (NPC npc in Main.npc) {
                if (!npc.active || npc.lifeMax <= 1 || npc.DoesntDespawnToInactivity() || !SimpleNamedNPCRevengeSystem.CanRespawn(npc.netID))
                    continue;

                var dis = playerCenter - npc.Center;
                if (Math.Abs(dis.X) > Player.tileRangeX * 16f) continue;
                if (Math.Abs(dis.Y) > Player.tileRangeY * 16f) continue;

                if (!IsValidTarget(npc)) continue;

                float distanceToMouse = Vector2.Distance(mouseWorld, npc.Center);
                if (distanceToMouse < bestDistance) {
                    bestDistance = distanceToMouse;
                    targetNPC = npc;
                }
            }
            return targetNPC;
        }

        public override void HoldItem(Player player) {
            base.HoldItem(player);

            NPC currentTarget = GetTargetNPC(player);
            if (currentTarget != null) {
                player.cursorItemIconEnabled = true;
                player.cursorItemIconID = Item.type;
            }
        }

        private void OpenNameTagEditor(Player player) {
            Main.editChest = false;
            Main.SetNPCShopIndex(0);
            Main.playerInventory = false;
            Main.InGuideCraftMenu = false;
            player.SetTalkNPC(-1);

            Main.editSign = true;
            Main.npcChatText = nameContent;

            int signIndex = FindSafeSignIndex();
            player.sign = signIndex;

            if (Main.sign[signIndex] == null)
                Main.sign[signIndex] = new Sign();

            Point nearestEmptyTile = FindNearestEmptyTile(player);

            Main.sign[signIndex].x = nearestEmptyTile.X;
            Main.sign[signIndex].y = nearestEmptyTile.Y;
            Main.sign[signIndex].text = nameContent;

            ModContent.GetInstance<NameTagEditWatcher>().StartEdit(this, signIndex, player);
        }

        private Point FindNearestEmptyTile(Player player) {
            int playerTileX = (int)(player.position.X / 16);
            int playerTileY = (int)(player.position.Y / 16);

            if (!Main.tile[playerTileX, playerTileY].HasTile) {
                return new Point(playerTileX, playerTileY);
            }

            int maxRadius = 10;

            for (int radius = 1; radius <= maxRadius; radius++) {
                for (int dx = -radius; dx <= radius; dx++) {
                    for (int dy = -radius; dy <= radius; dy++) {
                        if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                            continue;

                        int checkX = playerTileX + dx;
                        int checkY = playerTileY + dy;

                        if (checkX < 0 || checkX >= Main.maxTilesX || checkY < 0 || checkY >= Main.maxTilesY)
                            continue;

                        if (!Main.tile[checkX, checkY].HasTile) {
                            return new Point(checkX, checkY);
                        }
                    }
                }
            }

            return new Point(playerTileX, playerTileY);
        }

        private int FindSafeSignIndex() {
            for (int i = Main.sign.Length - 1; i >= Main.sign.Length - 50; i--) {
                if (Main.sign[i] == null || string.IsNullOrEmpty(Main.sign[i].text))
                    return i;
            }
            return Main.sign.Length - 1;
        }

        public override void SaveData(TagCompound tag) => tag["nameContent"] = nameContent;

        public override void LoadData(TagCompound tag) {
            SetContent(tag.GetString("nameContent"));
        }

        public override void NetSend(BinaryWriter writer) {
            writer.Write(nameContent ?? "");
        }

        public override void NetReceive(BinaryReader reader) {
            SetContent(reader.ReadString());
        }

        public override void AddRecipes() {
            Recipe recipe = CreateRecipe(1);
            recipe.AddIngredient(ModContent.ItemType<PaperItem>(), 2);
            recipe.AddIngredient(ItemID.CopperBar, 1);
            recipe.AddTile(TileID.WorkBenches);
            recipe.Register();

            recipe = CreateRecipe(1);
            recipe.AddIngredient(ModContent.ItemType<PaperItem>(), 2);
            recipe.AddIngredient(ItemID.TinBar, 1);
            recipe.AddTile(TileID.WorkBenches);
            recipe.Register();
        }

        public void SetContent(string content) {
            if (!string.IsNullOrEmpty(content)) {
                int newlineIndex = content.IndexOfAny(new char[] { '\n', '\r' });
                if (newlineIndex >= 0) {
                    content = content.Substring(0, newlineIndex);
                }
            }

            nameContent = content ?? "";
            UpdateStackAndProperties();
        }

        public string GetContent() => nameContent;
    }
}