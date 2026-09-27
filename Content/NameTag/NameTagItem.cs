using GuidaSharedCode;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QualityOfGuida;
using ReLogic.Content;
using QualityOfGuida.Content.Paper;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace QualityOfGuida.Content.NameTag {
    public class NameTagItem : GuidaItem {
        public string nameContent = "";
        public NPC targetNPC; // ??��NPC
        public override bool IsLoadingEnabled(Mod mod) {
            return ModContent.GetInstance<ItemToggleConfig>().EnableNameTag;
        }
        public override string GetDynamicDisplayName() {
            return nameContent;
        }

        public override Asset<Texture2D> GetDynamicTextureAsset() {
            if (string.IsNullOrEmpty(nameContent)) {
                return ModAsset.NameTagItem1;
            }

            string lower = nameContent.ToLower();
            if (lower.Contains("boulder")) return ModAsset.NameTagItem7;
            if (lower.Contains("king")) return ModAsset.NameTagItem8;
            if (lower.Contains("hitbox")) return ModAsset.NameTagItem3;
            if (lower.Contains("rainbow")) return ModAsset.NameTagItem6;
            if (lower.Contains("giant")) return ModAsset.NameTagItem5;
            if (lower.Contains("mini")) return ModAsset.NameTagItem9;
            if (lower.Contains("reverse")) return ModAsset.NameTagItem4;
            return ModAsset.NameTagItem2;
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
                SoundEngine.PlaySound(QoGSound.PaperOpen, player.Center);
                OpenNameTagEditor(player);
                return true;
            } else {
                Item.useStyle = ItemUseStyleID.Swing;

                if (Main.netMode == NetmodeID.SinglePlayer) {
                    // ������Ϸ��ֱ��???��
                    return ProcessNPCNaming();
                } else if (Main.netMode == NetmodeID.MultiplayerClient) {
                    // ������Ϸ�ͻ�??������???�󵽷���???
                    SendNamingRequest();
                    return true;
                } else if (Main.netMode == NetmodeID.Server) {
                    // ������Ϸ����??����������???����ͨ��������Ϣ����
                    // ������Ҫ����true��ʾʹ�óɹ�������???��??��������ʹ����Ʒ
                    return true;
                }

                return false;
            }
        }

        private void SendNamingRequest() {
            if (targetNPC == null) return;

            ModContent.GetInstance<NameTagSyncSystem>().SendNamingRequest(targetNPC, nameContent);

            // �ͻ�??����??����Ч����Ч��Ϊ��???
            SoundEngine.PlaySound(SoundID.Item1, targetNPC.Center);
            if (string.IsNullOrEmpty(nameContent)) {
                CreateClearingEffect(targetNPC);
            } else {
                CreateNamingEffect(targetNPC);
            }
        }


        // ����NPC�����߼�
        private bool ProcessNPCNaming() {
            if (targetNPC == null) return false;
            if (string.IsNullOrEmpty(nameContent)) {
                return ClearNPCName(targetNPC);
            } else {
                return NameNPC(targetNPC);
            }
        }

        // ΪNPC����
        private bool NameNPC(NPC npc) {
            if (npc == null || string.IsNullOrEmpty(nameContent))
                return false;

            NPCNamingSystem.SetNPCName(npc, nameContent);

            SoundEngine.PlaySound(SoundID.Item1, npc.Center);
            CreateNamingEffect(npc);
            return true;
        }

        // ���NPC����
        private bool ClearNPCName(NPC npc) {
            if (npc == null)
                return false;
            ModContent.GetInstance<SimpleNamedNPCRevengeSystem>().StopTracking(npc.whoAmI);
            NPCNamingSystem.SetNPCName(npc, null);
            SoundEngine.PlaySound(SoundID.Item1, npc.Center);
            CreateClearingEffect(npc);
            return true;
        }

        // ����������Ч
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

        // �������������Ч
        private void CreateClearingEffect(NPC npc) {
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustDirect(
                    npc.position,
                    npc.width,
                    npc.height,
                    DustID.Smoke); // ʹ��������Ч��ʾ���
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
            int signIndex = SignEditorHelper.OpenEditor(player, nameContent);
            ModContent.GetInstance<NameTagEditWatcher>().StartEdit(this, signIndex, player);
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