using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QualityOfGuida.Content.Particles;
using System;
using Terraria;
using Terraria.ModLoader;

namespace QualityOfGuida.Content.Flint {
    /// <summary>
    /// 简单的地狱传送门粒子效果 - 从四周汇聚到中心
    /// </summary>
    public class NetherPortalParticle : Particle {
        private Vector2 targetPosition;  // 目标位置（传送门中心）
        private float fadeInTime;        // 淡入时间
        private float fadeOutTime;       // 淡出时间
        private float accelerationStrength; // 加速度强度
        private float alphaMul = 1f;
        public int life = 80;
        private Texture2D texture;

        public override void SetDefaults() {
            base.SetDefaults();

            width = 4;
            height = 4;
            timeLeft = life; // 2秒生命周期
            scale = Main.rand.NextFloat(0.6f, 1.2f);
            useLighting = true;

            // 随机紫色到粉色
            float colorMix = Main.rand.NextFloat();
            color = Color.Lerp(Color.Purple, Color.HotPink, colorMix);

            // 设置发光属性
            light = 0.4f;
            lightColor = color;

            // 淡入淡出时间设置
            fadeInTime = 20f;  // 前20帧淡入
            fadeOutTime = 20f; // 后30帧淡出

            // 加速度强度
            accelerationStrength = Main.rand.NextFloat(0.02f, 0.05f) * 0.4f;

            // 初始alpha为0，用于淡入效果
            alphaMul = Main.rand.NextFloat(0.6f, 1f);
            alpha = 0f;
            texture = ModContent.Request<Texture2D>(ModAssets.ContentDir + "/Particles/Generic" + (Main.rand.Next(6) + 1).ToString()).Value;
        }

        /// <summary>
        /// 设置粒子的目标位置（传送门中心）
        /// </summary>
        public void SetTarget(Vector2 target) {
            targetPosition = target;
        }

        public override void AI() {
            base.AI();

            // 计算生命周期进度
            float lifeProgress = 1f - (float)timeLeft / life;
            int framesLived = life - timeLeft;

            // 淡入效果
            if (framesLived < fadeInTime) {
                alpha = framesLived / fadeInTime;
            }
            // 淡出效果
            else if (timeLeft < fadeOutTime) {
                alpha = timeLeft / fadeOutTime;
            }
            // 正常显示
            else {
                alpha = 1f;
            }
            if (state == 0) {
                // 向目标位置加速移动
                Vector2 directionToTarget = targetPosition - position;
                float distanceToTarget = directionToTarget.Length();

                if (distanceToTarget > 1f) {
                    // 标准化方向向量
                    directionToTarget.Normalize();

                    // 随着接近目标，加速度逐渐增强
                    float proximityMultiplier = Math.Max(0.5f, 2f - distanceToTarget / 100f);
                    Vector2 acceleration = directionToTarget * accelerationStrength * proximityMultiplier;

                    // 应用加速度
                    velocity += acceleration;

                    // 限制最大速度，避免过快
                    float maxSpeed = 2f;
                    if (velocity.Length() > maxSpeed) {
                        velocity = Vector2.Normalize(velocity) * maxSpeed;
                    }
                }
                // 如果非常接近目标，提前消失
                if (distanceToTarget < 5f) {
                    timeLeft = Math.Min(timeLeft, 10);
                }
            } else {
                velocity.Y -= 0.1f * scale;
                velocity *= 0.90f;
            }
                // 轻微旋转
                rotation += 0.02f;

            

            // 脉冲光效
            float pulse = (float)Math.Sin(lifeProgress * MathHelper.Pi * 4f) * 0.2f + 0.8f;
            light = 0.8f * pulse * alpha;
        }

        public override void Draw(SpriteBatch spriteBatch, Color lightColor) {
            // 使用简单的像素纹理绘制圆形粒子

            Vector2 drawPos = GetDrawPosition();
            Color finalColor = color.MultiplyRGBA(lightColor) * alpha * alphaMul;

            // 绘制核心
            spriteBatch.Draw(
                texture,
                drawPos,
                null,
                finalColor * 0.2f,
                rotation,
                texture.Size() / 2,
                scale,
                SpriteEffects.None,
                0f
            );
            spriteBatch.EndAndBeginAdd();
            spriteBatch.Draw(
                texture,
                drawPos,
                null,
                finalColor * 0.8f,
                rotation,
                texture.Size() / 2,
                scale,
                SpriteEffects.None,
                0f
            );
            spriteBatch.EndAndBeginDefault();

        }

        public override Vector2 Origin => new Vector2(width / 2f, height / 2f);
    }
}