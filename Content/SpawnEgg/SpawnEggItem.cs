using GuidaSharedCode;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QualityOfGuida;
using QualityOfGuida.Content.SmartCursor;
using QualityOfGuida.Content.Spawner;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace QualityOfGuida.Content.SpawnEgg {
    public class SpawnEggItem : GuidaItem {
        public int storedBannerID = 0;
        private string npcName = "";
        private RenderTarget2D cachedTexture = null;
        public NPC targetNPC;

        // NPC预览相关字段
        private UnlockableNPCEntryIcon previewIcon = null;
        private int lastPreviewNPCType = -1;
        private bool shouldShowPreview = false;
        private Vector2 spawnPosition;
        public override bool IsLoadingEnabled(Mod mod) {
            return ModContent.GetInstance<ItemToggleConfig>().EnableSpawnEgg;
        }

        public static bool HasBanner(NPC npc) => Item.NPCtoBanner(npc.BannerID()) > 0;

        public static string GetNPCDisplayName(int npcType) {
            string npcInternalName = NPCLoader.GetNPC(npcType)?.Name ?? NPCID.Search.GetName(npcType);
            if (string.IsNullOrEmpty(npcInternalName)) {
                return Language.GetTextValue("Mods.QualityOfGuida.Items.SpawnEggItem.UnknownEnemy");
            }

            string key = $"NPCName.{npcInternalName}";
            string localizedName = Language.GetTextValue(key);
            return (!string.IsNullOrEmpty(localizedName) && localizedName != key) ? localizedName : npcInternalName;
        }

        public override string GetDynamicDisplayName() {
            return storedBannerID > 0
                ? Language.GetTextValue("Mods.QualityOfGuida.Items.SpawnEggItem.FilledDisplayName", npcName)
                : Language.GetTextValue("Mods.QualityOfGuida.Items.SpawnEggItem.EmptyDisplayName");
        }

        public override void UpdateDynamicProperties() {
            if (Main.netMode != NetmodeID.Server) {
                if (cachedTexture == null || cachedTexture.IsDisposed || cachedTexture.IsContentLost) {
                    GenerateCachedTexture();
                }
            }
        }

        public override Texture2D GetDynamicTexture() {
            if (cachedTexture != null && !cachedTexture.IsDisposed) {
                return cachedTexture;
            }
            return storedBannerID > 0 ? ModAsset.SpawnEggItem_Filled4.Value : ModAsset.SpawnEggItem.Value;
        }

        // 更新预览图标
        private void UpdatePreviewIcon(int npcType) {
            if (npcType != lastPreviewNPCType) {
                lastPreviewNPCType = npcType;
                previewIcon = null;

                try {
                    var tempNPC = new NPC();
                    tempNPC.SetDefaults(npcType);
                    previewIcon = new UnlockableNPCEntryIcon(tempNPC.netID);
                } catch {
                    previewIcon = null;
                }
            }
        }

        // 绘制NPC预览
        private void DrawNPCPreview(SpriteBatch spriteBatch) {
            if (!shouldShowPreview || previewIcon == null) return;

            try {
                // 在鼠标位置绘制预览图标
                var iconBox = new Rectangle(
                    (int)Main.MouseScreen.X + 20,
                    (int)Main.MouseScreen.Y + 20,
                    64, 64
                );

                var info = new BestiaryUICollectionInfo {
                    UnlockState = BestiaryEntryUnlockState.CanShowPortraitOnly_1
                };

                var settings = new EntryIconDrawSettings {
                    iconbox = iconBox,
                    IsPortrait = true
                };
                spriteBatch.End();
                spriteBatch.Begin(default, BlendState.AlphaBlend, SamplerState.PointClamp, default, Main.Rasterizer, null, Main.UIScaleMatrix);
                // 使用裁剪渲染
                Rectangle newClip = iconBox;
                newClip.Inflate(-4, -4);

                Rectangle oldRect = spriteBatch.GraphicsDevice.ScissorRectangle;
                spriteBatch.GraphicsDevice.ScissorRectangle = newClip;

                previewIcon.Draw(info, spriteBatch, settings);

                spriteBatch.GraphicsDevice.ScissorRectangle = oldRect;
                spriteBatch.End();
                spriteBatch.Begin(default, BlendState.AlphaBlend, SamplerState.LinearClamp, default, Main.Rasterizer, null, Main.UIScaleMatrix);
            } catch {
                // 如果绘制失败，清除预览
                previewIcon = null;
                shouldShowPreview = false;
            }
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            // 正常绘制物品
            bool result = base.PreDrawInInventory(spriteBatch, position, frame, drawColor, itemColor, origin, scale);

            // 如果这是玩家手持的物品且应该显示预览，则绘制预览
            if (shouldShowPreview && Main.LocalPlayer.HeldItem == Item) {
                DrawNPCPreview(spriteBatch);
            }

            return result;
        }

        private void GenerateCachedTexture() {
            SpawnEggTextures.Regenerate(storedBannerID, ref cachedTexture);
        }

        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.value = Item.buyPrice(gold: 1);
            Item.rare = ItemRarityID.Orange;
            Item.maxStack = Item.CommonMaxStack;
            Item.consumable = storedBannerID > 0;
            Item.autoReuse = true;
        }

        public override bool? UseItem(Player player) {
            if (storedBannerID == 0) {
                return CaptureNPC(player);
            } else {
                return SpawnNPC(player);
            }
        }
        private bool SpawnNPC(Player player) {
            if (storedBannerID <= 0) return false;

            int npcType = Item.BannerToNPC(storedBannerID);

            var mouseSync = player.GetModPlayer<MouseSyncPlayer>();
            Vector2 spawnPosition = mouseSync.GetMouseWorld();

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int npcIndex = NPC.NewNPC(player.GetSource_ItemUse(Item),
                    (int)spawnPosition.X, (int)spawnPosition.Y, npcType);

                if (npcIndex < 0 || npcIndex >= Main.maxNPCs || Main.npc[npcIndex]?.active != true) {
                    return false;
                }
                Main.npc[npcIndex].velocity = Vector2.Zero;
            }
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(QoGSound.EggCrack, spawnPosition);

                for (int i = 0; i < 12; i++) {
                    Vector2 particlePos = spawnPosition + Main.rand.NextVector2Circular(16, 16);
                    ParticleManager.Instance?.NewParticle<FlameParticle>(particlePos,
                        new Vector2(Main.rand.NextFloat(-0.1f, -0.1f), Main.rand.NextFloat(-0.1f, 0.1f)));
                }
                SpawnEggParticle.CreateEggShellParticlesFromItem(spawnPosition, Vector2.Zero, this);
            }
            

            return true;
        }

        private bool CaptureNPC(Player player) {
            NPC currentTarget = GetTargetNPC(player);
            if (currentTarget == null) return false;

            int bannerID = Item.NPCtoBanner(currentTarget.BannerID());
            if (bannerID <= 0) return false;

            
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                if (Item.stack > 1) {
                    Item.stack--;
                    Item filledEgg = new Item();
                    filledEgg.SetDefaults(Item.type);
                    if (filledEgg.ModItem is SpawnEggItem eggItem) {
                        eggItem.SetBannerID(bannerID);
                    }
                    player.QuickSpawnItem(player.GetSource_ItemUse(Item), filledEgg);
                } else {
                    SetBannerID(bannerID);
                }
            }

            SoundEngine.PlaySound(SoundID.Item2);
            return true;
        }

        public override bool CanUseItem(Player player) {
            var mouseSync = player.GetModPlayer<MouseSyncPlayer>();

            if (storedBannerID == 0) {
                return GetTargetNPC(player) != null;
            } else {
                Vector2 mouseWorld = mouseSync.GetMouseWorld();
                int tileRangeX = mouseSync.GetTileRangeX();
                int tileRangeY = mouseSync.GetTileRangeY();

                Vector2 playerToMouse = mouseWorld - player.Center;

                if (Math.Abs(playerToMouse.X) > tileRangeX * 16f) return false;
                if (Math.Abs(playerToMouse.Y) > tileRangeY * 16f) return false;

                return true;
            }
        }

        public NPC GetTargetNPC(Player player) {
            var mouseSync = player.GetModPlayer<MouseSyncPlayer>();
            Vector2 mouseWorld = mouseSync.GetMouseWorld();
            Vector2 playerCenter = player.Center;
            int tileRangeX = mouseSync.GetTileRangeX();
            int tileRangeY = mouseSync.GetTileRangeY();

            targetNPC = null;
            float bestDistance = 100000;

            foreach (NPC npc in Main.npc) {
                if (!npc.active || npc.lifeMax <= 1 || !HasBanner(npc) ||
                    (npc.netID != Item.BannerToNPC(Item.NPCtoBanner(npc.netID)))) continue;

                var dis = playerCenter - npc.Center;
                // 使用同步的射程数据
                if (Math.Abs(dis.X) > tileRangeX * 16f) continue;
                if (Math.Abs(dis.Y) > tileRangeY * 16f) continue;

                float distanceToMouse = Vector2.Distance(mouseWorld, npc.Center);
                if (distanceToMouse < bestDistance) {
                    bestDistance = distanceToMouse;
                    targetNPC = npc;
                }
            }
            return targetNPC;
        }

        public override void OnConsumeItem(Player player) {
            if (Item.stack == 1) Item.TurnToAir();
        }



        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            List<TooltipLine> originalTooltips = new List<TooltipLine>(tooltips);
            tooltips.Clear();

            string usageText = storedBannerID == 0
                ? Language.GetTextValue("Mods.QualityOfGuida.Items.SpawnEggItem.CaptureUsage")
                : Language.GetTextValue("Mods.QualityOfGuida.Items.SpawnEggItem.SpawnUsage", npcName);

            foreach (var tooltip in originalTooltips) {
                if (tooltip.Name == "Tooltip0") {
                    tooltips.Add(new TooltipLine(Mod, "Usage", usageText));
                } else tooltips.Add(tooltip);
            }
        }

        public override void SaveData(TagCompound tag) => tag["storedBannerID"] = storedBannerID;

        public override void LoadData(TagCompound tag) => SetBannerID(tag.GetInt("storedBannerID"));

        public override void NetSend(BinaryWriter writer) {
            writer.Write(storedBannerID);
        }
        public override void NetReceive(BinaryReader reader) {
            int bannerID = reader.ReadInt32();
            SetBannerID(bannerID);
        }
        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.SoulofLight, 1)
                .AddIngredient(ItemID.SoulofNight, 1)
                .AddTile(TileID.MythrilAnvil)
                .DisableDecraft()
                .Register();
        }

        public override void Unload() {
            if (cachedTexture != null && !cachedTexture.IsDisposed) {
                cachedTexture.Dispose();
                cachedTexture = null;
            }

            previewIcon = null;

            base.Unload();
        }

        public static void ClearStaticCache() {
            SpawnEggBannerColors.ClearCache();
        }

        public void SetBannerID(int bannerID) {
            if (storedBannerID == bannerID) return;
            storedBannerID = bannerID;
            npcName = bannerID > 0 ? GetNPCDisplayName(Item.BannerToNPC(bannerID)) : "";
            if (bannerID > 0 && string.IsNullOrEmpty(npcName)) {
                npcName = Language.GetTextValue("Mods.QualityOfGuida.Items.SpawnEggItem.UnknownEnemy");
            }
            Item.consumable = bannerID > 0;
            cachedTexture?.Dispose();
            cachedTexture = null;
        }

        public override bool CanStack(Item item2) {
            return item2.ModItem is SpawnEggItem otherEgg && storedBannerID == otherEgg.storedBannerID;
        }

        public override void HoldItem(Player player) {
            base.HoldItem(player);

            if (Main.netMode != NetmodeID.Server) {
                NPC currentTarget = GetTargetNPC(player);

                if (IsEmpty() && currentTarget != null) {
                    player.cursorItemIconEnabled = true;
                    player.cursorItemIconID = ModContent.ItemType<SpawnEggItem>();
                    shouldShowPreview = false;
                } else if (!IsEmpty() && CanUseItem(player)) {
                    shouldShowPreview = true;
                    int npcType = Item.BannerToNPC(storedBannerID);
                    UpdatePreviewIcon(npcType);
                } else {
                    shouldShowPreview = false;
                }
            }
        }

        public override void HoldItemFrame(Player player) {
            // 当物品不再被持有时清理预览
            if (Main.LocalPlayer.HeldItem != Item) {
                shouldShowPreview = false;
            }
        }

        public bool IsEmpty() => storedBannerID == 0;
    }
}