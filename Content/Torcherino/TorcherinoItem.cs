using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework.Graphics;
using QualityOfGuida;
using Terraria.Graphics.Renderers;
using GuidaSharedCode;

namespace QualityOfGuida.Content.Torcherino {
    public class TorcherinoItem : GuidaItem {
        // 手持火把的更新计时器
        private int holdUpdateTimer = 0;
        private int radius = 16;
        private bool updated;
        public override bool IsLoadingEnabled(Mod mod) {
            return ModContent.GetInstance<ItemToggleConfig>().EnableTorcherino;
        }

        public override ReLogic.Content.Asset<Texture2D> GetDynamicTextureAsset() => ModAsset.TorcherinoItemShow;

        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 100;
            ItemID.Sets.Torches[Type] = true;
            ItemID.Sets.SingleUseInGamepad[Type] = true;
        }

        public override void SetDefaults() {
            // 使用DefaultToTorch设置火把的通用属性
            Item.DefaultToTorch(ModContent.TileType<TorcherinoTile>(), 0, false);

            // 设置物品价值
            Item.value = Item.buyPrice(silver: 50);

            // 设置稀有度
            Item.rare = ItemRarityID.Green;

            // 最大堆叠数量
            Item.maxStack = Item.CommonMaxStack;
        }
        public override void UpdateInventory(Player player) {
            updated = false;
        }
        public override void HoldItem(Player player) {
            // 如果玩家在水中，不产生光照和粒子效果
            if (player.wet) {
                return;
            }
            if (updated) return;
            updated = true;
            // 计算火把的位置
            Vector2 torchPosition = player.RotatedRelativePoint(
                new Vector2(
                    player.itemLocation.X + 12f * player.direction + player.velocity.X,
                    player.itemLocation.Y - 14f + player.velocity.Y
                ), true);

            // 提供蓝色光照
            Lighting.AddLight(torchPosition, 0.4f * 2f, 0.6f * 2f, 1.0f * 2f);


            if(Main.rand.NextBool(4))for (int k = 0; k < 1; k++) {
                Vector2 dustPosition = new Vector2(
                    player.itemLocation.X + (player.direction == -1 ? -16f : 6f),
                    player.itemLocation.Y - 14f * player.gravDir
                );

                Dust dust = Dust.NewDustDirect(
                    dustPosition,
                    5, 5,
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


            // 手持时的加速效果
            holdUpdateTimer++;
            if (holdUpdateTimer >= 1) { // 每帧都执行加速效果
                holdUpdateTimer = 0;

                // 获取玩家位置
                int playerX = (int)(player.Center.X / 16f);
                int playerY = (int)(player.Center.Y / 16f);

                // 执行加速更新
                TorcherinoSystem.DoUpdate(playerX, playerY, radius);
            }

            // 手持时的范围粒子效果
            if (Main.netMode != NetmodeID.Server) {
                CreateHoldRangeParticles(player);
            }
        }
        private TorcherinoCircleParticle circle = null;
        private void CreateHoldRangeParticles(Player player) {
            if (ParticleManager.Instance == null) return;

            Vector2 playerCenter = player.Center;

            // 创建环形粒子效果
            for (int i = 0; i < 8; i++) {
                float angle = Main.rand.NextFloat(0, (float)(Math.PI * 2));

                Vector2 particlePos = playerCenter + new Vector2(
                    (float)Math.Cos(angle) * radius * 16,
                    (float)Math.Sin(angle) * radius * 16
                );

                float speed = Main.rand.NextFloat(-0.6f, 0.6f) * 4f;
                Vector2 particleVel = new Vector2(
                    -(float)Math.Sin(angle) * speed,
                    (float)Math.Cos(angle) * speed
                );

                var particle = ParticleManager.Instance.NewParticle<TorcherinoParticle>(
                    particlePos, particleVel, 0, 1f, Main.rand.NextFloat(0.8f, 1.2f));
                if (particle != null) {
                    particle.color = Color.CornflowerBlue;
                    particle.timeLeft = Main.rand.Next(80, 110) / 6;
                    particle.lightColor = Color.LightBlue;
                    particle.state = 2;
                    particle.alphaMul *= 0.6f;
                }
            }

            // 创建范围内的散布粒子
            for (int i = 0; i < 2; i++) {
                Vector2 particlePos = playerCenter + Main.rand.NextVector2Circular(radius * 16 - 8, radius * 16 - 8);

                var particle = ParticleManager.Instance.NewParticle<TorcherinoParticle>(
                    particlePos, Main.rand.NextVector2Circular(1, 1), 0, 1f, Main.rand.NextFloat(1.2f, 1.8f));
                if (particle != null) {
                    particle.color = Color.CornflowerBlue;
                    particle.timeLeft = Main.rand.Next(60, 100) / 2;
                    particle.lightColor = Color.LightBlue;
                    particle.state = 1;
                    particle.alphaMul *= 0.3f;
                    particle.scaleMul *= 1.8f * Main.rand.NextFloat(1, 1.5f);
                }
            }
            if (circle == null || !circle.active) {
                circle = ParticleManager.Instance.NewParticle<TorcherinoCircleParticle>(
                    playerCenter, Main.rand.NextVector2Circular(1, 1), 0);
            }
            if (circle != null) {
                circle.position = playerCenter;
                circle.timeLeft = Math.Min(circle.timeLeft + 2, circle.maxTimeLeft);
                circle.scale = (float)radius * 36 / 522;
            }
        }

        public override void PostUpdate() {
            // 当物品掉在地上时，如果不在水中就提供蓝色光照
            if (!Item.wet) {
                Lighting.AddLight(Item.Center, 0.4f, 0.6f, 1.0f);
            }
        }
        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            Texture2D dynamicTexture = GetDynamicTexture();
            if (dynamicTexture != null) {
                spriteBatch.Draw(dynamicTexture, position, null, drawColor, 0f, dynamicTexture.Size() / 2, scale, SpriteEffects.None, 0f);
                return false;
            }
            return true;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI) {
            Texture2D dynamicTexture = GetDynamicTexture();
            if (dynamicTexture != null) {
                Vector2 position = Item.position - Main.screenPosition + new Vector2(Item.width / 2, Item.height / 2);
                Vector2 origin = new Vector2(dynamicTexture.Width / 2, dynamicTexture.Height / 2);
                spriteBatch.Draw(dynamicTexture, position, null, lightColor, rotation, origin, scale, SpriteEffects.None, 0f);
                return false;
            }
            return true;
        }
        public override void AddRecipes() {
            CreateRecipe(1)
                .AddIngredient(ItemID.UltrabrightTorch, 1)
                .AddIngredient(ItemID.FastClock, 1)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }
    }
}