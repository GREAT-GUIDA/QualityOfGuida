using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using QualityOfGuida.Content.Particles;

namespace QualityOfGuida.Content.Torcherino {
    public class TorcherinoParticle : Particle {
        public float alphaMul = 1;
        public float scaleMul = 1;
        public override Texture2D Texture => ModContent.Request<Texture2D>(ModAssets.ContentDir + "/Particles/Spotlight" + state.ToString()).Value;

        public override void SetDefaults() {
            base.SetDefaults();
            width = 8;
            height = 8;
            timeLeft = 10;
            maxTimeLeft = 10;
            scale = 0.8f;
            drawLayer = ParticleLayer.AfterDust;
            useLighting = true;
            light = 0.5f;
            lightColor = Color.LightBlue;
            
        }

        public override void AI() {
            base.AI();
            if (timeLeft > maxTimeLeft) maxTimeLeft = timeLeft;
            // 计算淡入淡出alpha值
            float timeAlive = maxTimeLeft - timeLeft;

            if (timeAlive < maxTimeLeft * 0.3f) {
                // 淡入阶段
                alpha = timeAlive / (maxTimeLeft * 0.3f);
            } else if (timeLeft < (maxTimeLeft * 0.3f)) {
                // 淡出阶段
                alpha = timeLeft / (maxTimeLeft * 0.3f);
            } else {
                // 完全显示阶段
                alpha = 1f;
            }

            // 确保alpha在合理范围内
            alpha = MathHelper.Clamp(alpha, 0f, 1f);

            // 空气阻力，让粒子逐渐减速
            //velocity *= 0.98f;

            // 随时间缩放变化

            // 光照强度随alpha变化
            light = 0.1f * alpha;
        }

        public override void Draw(SpriteBatch spriteBatch, Color lightColor) {
            if (Texture == null) return;

            // 获取绘制位置
            Vector2 drawPosition = GetDrawPosition();

            // 使用加法混合模式
            var oldBlendState = Main.graphics.GraphicsDevice.BlendState;
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            // 计算最终颜色 - 在加法模式下不需要光照混合
            Color finalColor = color * alpha * alphaMul;

            // 绘制粒子
            spriteBatch.Draw(
                Texture,
                drawPosition,
                SourceRectangle,
                finalColor,
                rotation,
                Origin,
                scale * scaleMul * 0.1f,
                GetSpriteEffects(),
                0f
            );

            // 恢复原始混合模式
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, oldBlendState, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
    public class TorcherinoCircleParticle : Particle {
        public float alphaMul = 1;
        public float scaleMul = 1;
        public override Texture2D Texture => ModContent.Request<Texture2D>(ModAssets.ContentDir + "/Particles/TorchCircle").Value;

        public override void SetDefaults() {
            base.SetDefaults();
            timeLeft = 1;
            maxTimeLeft = 12;
            drawLayer = ParticleLayer.BeforeNPCs;
            useLighting = true;
            light = 0.5f;
            color = Color.LightBlue;
        }

        public override void AI() {
            base.AI();
            alpha = (float)timeLeft / maxTimeLeft;
        }

        public override void Draw(SpriteBatch spriteBatch, Color lightColor) {
            if (Texture == null) return;

            // 获取绘制位置
            Vector2 drawPosition = GetDrawPosition();


            // 使用加法混合模式
            var oldBlendState = Main.graphics.GraphicsDevice.BlendState;
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            Color finalColor = new Color(0, 0, 1f, alpha * 0.1f);

            // 绘制粒子
            spriteBatch.Draw(
                Texture,
                drawPosition,
                SourceRectangle,
                finalColor,
                rotation,
                Origin,
                scale * scaleMul,
                GetSpriteEffects(),
                0f
            );

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            finalColor = new Color(20, 220, 255) * 0.3f * alpha;

            // 绘制粒子
            spriteBatch.Draw(
                Texture,
                drawPosition,
                SourceRectangle,
                finalColor,
                rotation,
                Origin,
                scale * scaleMul,
                GetSpriteEffects(),
                0f
            );

            // 恢复原始混合模式
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, oldBlendState, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}