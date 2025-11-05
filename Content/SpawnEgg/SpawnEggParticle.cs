using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QualityOfGuida.Content.Particles;
using System;
using Terraria;
using Terraria.Graphics.Renderers;
using Terraria.ModLoader;

namespace QualityOfGuida.Content.SpawnEgg {
    public class SpawnEggParticle : Particle {
        private RenderTarget2D customTexture;
        private Vector2 customOrigin;
        private float gravity = 0.3f;
        private float bounceReduction = 0.7f;
        private float rotationSpeed;
        private bool hasBouncedOnce = false;
        public Vector2 centerOffset;

        public override Texture2D Texture => customTexture;

        public override void SetDefaults() {
            base.SetDefaults();

            // 设置粒子基本属性
            width = 8;
            height = 8;
            timeLeft = 240; // 3秒生命周期
            maxTimeLeft = 240;
            scale = 1f;
            alpha = 1f;
            color = Color.White;
            tileCollide = true;
            drawLayer = ParticleLayer.BeforeNPCs;
            useLighting = true;
            // 设置随机旋转速度
            rotationSpeed = Main.rand.NextFloat(-0.3f, 0.3f);
            rotation = Main.rand.NextFloat(0f, MathHelper.TwoPi);
        }

        public override void AI() {
            // 保存旧位置
            oldPosition = position;
            oldVelocity = velocity;
            oldRotation = rotation;

            // 应用重力
            velocity.Y += gravity;

            // 应用旋转
            rotation += rotationSpeed;
            oldVelocity = velocity;
            if (DoTileCollision()) {
                velocity = 2 * velocity - oldVelocity;
                velocity *= 0.8f;
                rotationSpeed *= 0.5f;
            }
            
            // 移动粒子
            position += velocity;

            // 淡出效果
            if (timeLeft < 60) {
                alpha = timeLeft / 60f;
            }

            // 减少生命时间
            timeLeft--;

            if (timeLeft <= 0) {
                Kill();
            }
        }

        public override void Draw(SpriteBatch spriteBatch, Color lightColor) {
            if (customTexture == null) return;

            Vector2 drawPosition = GetDrawPosition();

            Color finalColor = lightColor * alpha;

            // 绘制粒子
            spriteBatch.Draw(
                customTexture,
                drawPosition,
                null,
                finalColor,
                rotation,
                customOrigin,
                scale * 0.9f,
                GetSpriteEffects(),
                0f
            );
        }

        public override void OnKill() {
            Main.RunOnMainThread(() => {
                if (customTexture != null && !customTexture.IsDisposed) {
                    customTexture.Dispose();
                    customTexture = null;
                }
            });
        }

        public static void CreateEggShellParticles(Vector2 position, Vector2 baseVelocity, Texture2D eggTexture) {
            if (ParticleManager.Instance == null || eggTexture == null) return;

            try {
                // 获取蒙版纹理
                var mask1 = ModContent.Request<Texture2D>("QualityOfGuida/Content/SpawnEgg/SpawnEggItem_Mask1", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
                var mask2 = ModContent.Request<Texture2D>("QualityOfGuida/Content/SpawnEgg/SpawnEggItem_Mask2", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;

                if (mask1 == null || mask2 == null) return;

                var upperParticle = ParticleManager.Instance.NewParticle<SpawnEggParticle>(
                    position + new Vector2(Main.rand.NextFloat(-8f, 8f), Main.rand.NextFloat(-8f, 8f)),
                    baseVelocity + new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-6f, -3f) + 3f)
                );
                upperParticle?.CreateMaskedTexture(eggTexture, mask1);
                upperParticle.customOrigin = new Vector2(eggTexture.Width * 0.5f, eggTexture.Height * 0.3f);
                var lowerParticle = ParticleManager.Instance.NewParticle<SpawnEggParticle>(
                    position + new Vector2(Main.rand.NextFloat(-8f, 8f), Main.rand.NextFloat(-8f, 8f)),
                    baseVelocity + new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-4f, -1f) + 3f)
                );
                lowerParticle?.CreateMaskedTexture(eggTexture, mask2);
                lowerParticle.customOrigin = new Vector2(eggTexture.Width * 0.5f, eggTexture.Height * 0.7f);
            } catch (Exception ex) {
                ModContent.GetInstance<QualityOfGuida>().Logger.Error($"Failed to create egg shell particles: {ex.Message}");
            }
        }

        public static void CreateEggShellParticlesFromItem(Vector2 position, Vector2 velocity, SpawnEggItem spawnEggItem) {
            if (spawnEggItem == null) return;

            // 获取物品的动态纹理
            Texture2D itemTexture = spawnEggItem.GetDynamicTexture();
            CreateEggShellParticles(position, velocity, itemTexture);
        }

        public void CreateMaskedTexture(Texture2D sourceTexture, Texture2D maskTexture) {
            if (sourceTexture == null || maskTexture == null) return;
            if (Main.graphics?.GraphicsDevice == null) return;

            var device = Main.graphics.GraphicsDevice;
            var renderTargets = device.GetRenderTargets();
            RenderTarget2D previousTarget = renderTargets.Length > 0 ? renderTargets[0].RenderTarget as RenderTarget2D : null;

            try {
                // 创建与源纹理相同大小的渲染目标
                customTexture = new RenderTarget2D(device, sourceTexture.Width, sourceTexture.Height);

                // 设置渲染目标
                device.SetRenderTarget(customTexture);
                device.Clear(Color.Transparent);

                // 创建用于蒙版混合的BlendState
                var maskBlendState = new BlendState {
                    ColorSourceBlend = Blend.Zero,
                    ColorDestinationBlend = Blend.SourceColor,
                    ColorBlendFunction = BlendFunction.Add,
                    AlphaSourceBlend = Blend.Zero,
                    AlphaDestinationBlend = Blend.SourceAlpha,
                    AlphaBlendFunction = BlendFunction.Add
                };

                // 开始绘制
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

                // 首先绘制源纹理
                Main.spriteBatch.Draw(sourceTexture, Vector2.Zero, Color.White);

                Main.spriteBatch.End();

                // 应用蒙版
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, maskBlendState);

                // 绘制蒙版（白色部分保留，黑色部分移除）
                Main.spriteBatch.Draw(maskTexture, Vector2.Zero, Color.White);

                Main.spriteBatch.End();

                // 恢复之前的渲染目标
                device.SetRenderTarget(previousTarget ?? Main.screenTarget);

                // 设置原点
                if (customTexture != null) {
                    customOrigin = new Vector2(customTexture.Width * 0.5f, customTexture.Height * 0.5f);
                }
            } catch (Exception ex) {
                customTexture?.Dispose();
                customTexture = null;
            }
        }
    }
}