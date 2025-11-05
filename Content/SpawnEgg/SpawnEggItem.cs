using Microsoft.Build.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QualityOfGuida.Content.Particles;
using QualityOfGuida.Content.SmartCursor;
using QualityOfGuida.Content.Spawner;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
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
        private static Dictionary<int, ColorPair> bannerColorCache = new Dictionary<int, ColorPair>();
        public NPC targetNPC;

        // NPC预览相关字段
        private UnlockableNPCEntryIcon previewIcon = null;
        private int lastPreviewNPCType = -1;
        private bool shouldShowPreview = false;
        private Vector2 spawnPosition;
        public override bool IsLoadingEnabled(Mod mod) {
            return ModContent.GetInstance<ItemToggleConfig>().EnableSpawnEgg;
        }
        public struct ColorPair {
            public Color Primary;
            public Color Secondary;
            public bool HasSecondary;

            public ColorPair(Color primary, Color secondary = default, bool hasSecondary = false) {
                Primary = primary;
                Secondary = secondary;
                HasSecondary = hasSecondary;
            }
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
            string fallbackTexturePath = storedBannerID > 0
                ? "QualityOfGuida/Content/SpawnEgg/SpawnEggItem_Filled4"
                : "QualityOfGuida/Content/SpawnEgg/SpawnEggItem";

            return ModContent.Request<Texture2D>(fallbackTexturePath).Value;
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

        // 原有的颜色提取和纹理生成方法保持不变...
        private void GenerateCachedTexture() {
            cachedTexture?.Dispose();
            cachedTexture = null;

            if (Main.graphics?.GraphicsDevice == null) return;

            var device = Main.graphics.GraphicsDevice;
            var renderTargets = device.GetRenderTargets();
            RenderTarget2D previousTarget = renderTargets.Length > 0 ? renderTargets[0].RenderTarget as RenderTarget2D : null;

            try {
                int bannerItem = Item.BannerToItem(storedBannerID);
                bool hasAsset = ModContent.HasAsset("QualityOfGuida/Content/SpawnEgg/SpawnEggItem_Filled" + bannerItem.ToString());
                if (storedBannerID == 0 || hasAsset) {
                    var emptyTexture = ModContent.Request<Texture2D>("QualityOfGuida/Content/SpawnEgg/SpawnEggItem", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;

                    if (hasAsset) emptyTexture = ModContent.Request<Texture2D>("QualityOfGuida/Content/SpawnEgg/SpawnEggItem_Filled" + bannerItem, ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;

                    if (emptyTexture == null) return;

                    cachedTexture = new RenderTarget2D(device, emptyTexture.Width, emptyTexture.Height);

                    device.SetRenderTarget(cachedTexture);
                    device.Clear(Color.Transparent);

                    Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
                    Main.spriteBatch.Draw(emptyTexture, Vector2.Zero, Color.LightGray);
                    Main.spriteBatch.End();

                    device.SetRenderTarget(previousTarget ?? Main.screenTarget);
                } else {
                    if (!bannerColorCache.TryGetValue(storedBannerID, out ColorPair colorPair)) {
                        colorPair = ExtractThemeColorsFromBanner(storedBannerID);
                    }

                    var primaryTexture = colorPair.HasSecondary ?
                        ModContent.Request<Texture2D>("QualityOfGuida/Content/SpawnEgg/SpawnEggItem_Filled1", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value :
                        ModContent.Request<Texture2D>("QualityOfGuida/Content/SpawnEgg/SpawnEggItem_Filled5", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
                    var secondaryTexture = colorPair.HasSecondary ?
                        ModContent.Request<Texture2D>("QualityOfGuida/Content/SpawnEgg/SpawnEggItem_Filled3", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value : null;

                    if (primaryTexture == null) return;

                    cachedTexture = new RenderTarget2D(device, primaryTexture.Width, primaryTexture.Height);

                    device.SetRenderTarget(cachedTexture);
                    device.Clear(Color.Transparent);

                    Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

                    Main.spriteBatch.Draw(primaryTexture, Vector2.Zero, colorPair.Primary);

                    if (colorPair.HasSecondary && secondaryTexture != null) {
                        Main.spriteBatch.Draw(secondaryTexture, Vector2.Zero, colorPair.Secondary);
                    }

                    Main.spriteBatch.End();

                    device.SetRenderTarget(previousTarget ?? Main.screenTarget);
                }
            } catch (Exception ex) {
                cachedTexture?.Dispose();
                cachedTexture = null;
                ModContent.GetInstance<QualityOfGuida>().Logger.Error($"Failed to generate cached texture: {ex.Message}");
            }
        }

        private ColorPair ExtractThemeColorsFromBanner(int bannerID) {
            // 获取对应的物品ID
            int itemID = Item.BannerToItem(bannerID);

            // 获取物品贴图
            Main.instance.LoadItem(itemID);
            Texture2D bannerTexture = TextureAssets.Item[itemID].Value;

            // 提取像素数据
            Color[] pixels = new Color[bannerTexture.Width * bannerTexture.Height];
            bannerTexture.GetData(pixels);

            // 降采样并过滤像素
            List<Color> validPixels = new List<Color>();
            for (int y = 0; y < bannerTexture.Height; y += 2) {
                for (int x = 0; x < bannerTexture.Width; x += 2) {
                    Color pixel = pixels[y * bannerTexture.Width + x];
                    // 只排除完全透明的像素
                    if (pixel.A > 0) {
                        validPixels.Add(pixel);
                    }
                }
            }
            bannerColorCache[storedBannerID] = PerformColorClustering(validPixels);
            // 使用K-means聚类找主题色
            return bannerColorCache[storedBannerID];
        }

        private ColorPair PerformColorClustering(List<Color> pixels) {
            if (pixels.Count == 0) throw new System.Exception();
            if (pixels.Count == 1) return new ColorPair(pixels[0]);

            int clusterCount = Math.Min(4, pixels.Count);

            var random = new System.Random(pixels.Count);

            var centers = Enumerable.Range(0, clusterCount)
                .Select(_ => {
                    var pixel = pixels[random.Next(pixels.Count)];
                    return new Vector3(pixel.R, pixel.G, pixel.B);
                })
                .ToList();

            var clusters = new List<List<Color>>();

            for (int iteration = 0; iteration < 10; iteration++) {
                // 分配像素到聚类中心
                clusters.Clear();
                for (int i = 0; i < clusterCount; i++) {
                    clusters.Add(new List<Color>());
                }

                foreach (var pixel in pixels) {
                    var pixelVec = new Vector3(pixel.R, pixel.G, pixel.B);
                    int closest = 0;
                    float minDistance = Vector3.DistanceSquared(pixelVec, centers[0]);

                    for (int i = 1; i < centers.Count; i++) {
                        float distance = Vector3.DistanceSquared(pixelVec, centers[i]);
                        if (distance < minDistance) {
                            minDistance = distance;
                            closest = i;
                        }
                    }
                    clusters[closest].Add(pixel);
                }

                // 更新聚类中心
                bool converged = true;
                for (int i = 0; i < centers.Count; i++) {
                    if (clusters[i].Count > 0) {
                        float avgR = (float)clusters[i].Average(p => (double)p.R);
                        float avgG = (float)clusters[i].Average(p => (double)p.G);
                        float avgB = (float)clusters[i].Average(p => (double)p.B);
                        var newCenter = new Vector3(avgR, avgG, avgB);

                        if (Vector3.DistanceSquared(centers[i], newCenter) > 1f) {
                            converged = false;
                        }
                        centers[i] = newCenter;
                    }
                }

                if (converged) break;
            }

            var validClusters = new List<ClusterInfo>();
            for (int i = 0; i < clusters.Count; i++) {
                if (clusters[i].Count > 0) {
                    validClusters.Add(new ClusterInfo {
                        Center = centers[i],
                        Count = clusters[i].Count,
                        Brightness = 0.299f * centers[i].X + 0.587f * centers[i].Y + 0.114f * centers[i].Z,
                        Color = VectorToColor(centers[i])
                    });
                }
            }

            validClusters = validClusters.OrderByDescending(c => c.Count).ToList();

            for (int i = 0; i < Math.Min(4, validClusters.Count); i++) {
                var cluster = validClusters[i];
            }

            if (validClusters.Count <= 1) {
                return validClusters.Count == 1 ? new ColorPair(validClusters[0].Color) : new ColorPair(Color.White);
            }

            // 剔除最暗和最少的
            if (validClusters.Count > 2) {
                var darkest = validClusters.OrderBy(c => c.Brightness).First();
                validClusters.Remove(darkest);
            }

            if (validClusters.Count > 2) {
                var least = validClusters.OrderBy(c => c.Count).First();
                validClusters.Remove(least);
            }

            if (validClusters.Count == 0) return new ColorPair(Color.White);
            if (validClusters.Count == 1) return new ColorPair(validClusters[0].Color);

            // 现在应该有两种颜色，计算颜色差异
            var color1 = validClusters[0];
            var color2 = validClusters[1];

            float colorDistance = Vector3.Distance(color1.Center, color2.Center);
            float brightnessDiff = Math.Abs(color1.Brightness - color2.Brightness);


            // 根据差异决定处理方式
            if (colorDistance < 50f && brightnessDiff < 30f) {
                // 差异较小，按数量平均
                int totalWeight = color1.Count + color2.Count;
                var weightedSum = (color1.Center * color1.Count + color2.Center * color2.Count) / totalWeight;
                return new ColorPair(VectorToColor(weightedSum));
            } else if (brightnessDiff <= 80f) {
                var brighter = color1.Brightness > color2.Brightness ? color1 : color2;
                var darker = color1.Brightness <= color2.Brightness ? color1 : color2;

                var enhancedBrighter = EnhanceBrightness(brighter.Center, 1.2f);
                var enhancedDarker = EnhanceBrightness(darker.Center, 0.8f);
                return new ColorPair(VectorToColor(enhancedBrighter), VectorToColor(enhancedDarker), true);
            } else {
                return new ColorPair(color1.Color, color2.Color, true);
            }
        }

        private Vector3 EnhanceBrightness(Vector3 color, float factor) {
            // 转换到HSL空间进行亮度调整
            var rgbColor = VectorToColor(color);
            float h, s, l;
            RgbToHsl(rgbColor.R / 255f, rgbColor.G / 255f, rgbColor.B / 255f, out h, out s, out l);

            // 调整亮度
            l = MathHelper.Clamp(l * factor, 0f, 1f);

            // 转换回RGB
            var newColor = HslToRgb(h, s, l);
            return new Vector3(newColor.R, newColor.G, newColor.B);
        }

        private void RgbToHsl(float r, float g, float b, out float h, out float s, out float l) {
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float diff = max - min;

            l = (max + min) / 2f;

            if (diff == 0) {
                h = s = 0;
                return;
            }

            s = l > 0.5f ? diff / (2f - max - min) : diff / (max + min);

            if (max == r) {
                h = (g - b) / diff + (g < b ? 6f : 0f);
            } else if (max == g) {
                h = (b - r) / diff + 2f;
            } else {
                h = (r - g) / diff + 4f;
            }
            h /= 6f;
        }

        private Color HslToRgb(float h, float s, float l) {
            float r, g, b;

            if (s == 0) {
                r = g = b = l;
            } else {
                float hue2rgb(float p, float q, float t) {
                    if (t < 0) t += 1;
                    if (t > 1) t -= 1;
                    if (t < 1f / 6f) return p + (q - p) * 6f * t;
                    if (t < 1f / 2f) return q;
                    if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
                    return p;
                }

                float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
                float p = 2f * l - q;
                r = hue2rgb(p, q, h + 1f / 3f);
                g = hue2rgb(p, q, h);
                b = hue2rgb(p, q, h - 1f / 3f);
            }

            return new Color((int)(r * 255), (int)(g * 255), (int)(b * 255));
        }

        private class ClusterInfo {
            public Vector3 Center { get; set; }
            public int Count { get; set; }
            public float Brightness { get; set; }
            public Color Color { get; set; }
        }

        private Color VectorToColor(Vector3 vector) => new Color(
            (int)MathHelper.Clamp(vector.X, 0, 255),
            (int)MathHelper.Clamp(vector.Y, 0, 255),
            (int)MathHelper.Clamp(vector.Z, 0, 255));

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
                SoundEngine.PlaySound(ModAssets.EggCrack, spawnPosition);

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
            bannerColorCache?.Clear();
        }

        public void SetBannerID(int bannerID) {
            if (storedBannerID == bannerID) return;
            npcName = bannerID > 0 ? GetNPCDisplayName(Item.BannerToNPC(bannerID)) : "";
            if (String.IsNullOrEmpty(npcName)) return;
            storedBannerID = bannerID;
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