
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.Localization;

namespace QualityOfGuida {
    public static class ModUtils {
        public static void EndAndBeginShader(this SpriteBatch spriteBatch, Effect shader, BlendState bs = null) {
            spriteBatch.End();
            if (bs == null) {
                bs = BlendState.NonPremultiplied;
            }
            spriteBatch.Begin(default, bs, SamplerState.PointClamp, default, Main.Rasterizer, shader, Main.GameViewMatrix.TransformationMatrix);
        }
        public static void EndAndBeginShaderAdd(this SpriteBatch spriteBatch, Effect shader) {
            spriteBatch.End();
            spriteBatch.Begin(default, BlendState.Additive, SamplerState.PointClamp, default, Main.Rasterizer, shader, Main.GameViewMatrix.TransformationMatrix);
        }
        public static void EndAndBeginAlpha(this SpriteBatch spriteBatch) {
            spriteBatch.End();
            spriteBatch.Begin(default, BlendState.NonPremultiplied, SamplerState.PointClamp, default, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
        public static void EndAndBeginAdd(this SpriteBatch spriteBatch) {
            spriteBatch.End();
            spriteBatch.Begin(default, BlendState.Additive, SamplerState.PointClamp, default, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
        public static void EndAndBeginDefault(this SpriteBatch spriteBatch) {
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }
        public static void EndAndBeginBs(this SpriteBatch spriteBatch, BlendState bs) {
            spriteBatch.End();
            spriteBatch.Begin(default, bs, SamplerState.PointClamp, default, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
        public static void EndAndBeginExt(this SpriteBatch spriteBatch, BlendState bs, Effect eff, Matrix mx) {
            spriteBatch.End();
            spriteBatch.Begin(default, bs, SamplerState.PointClamp, default, Main.Rasterizer, eff, mx);
        }
        public static void DrawBorderStringEightWay(SpriteBatch sb, DynamicSpriteFont font, string text, Vector2 baseDrawPosition, Color main, Color border, float scale = 1f) {
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    Vector2 drawPosition = baseDrawPosition + new Vector2(x, y);
                    if (x == 0 && y == 0)
                        continue;

                    sb.DrawString(font, text, drawPosition, border, 0f, default, scale, SpriteEffects.None, 0f);
                }
            }
            sb.DrawString(font, text, baseDrawPosition, main, 0f, default, scale, SpriteEffects.None, 0f);
        }

        public static void NetNewText(object str) {
            if (Main.netMode == 2) {
                Terraria.Chat.ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(str.ToString()), Color.White);
            } else {
                Main.NewText(str.ToString());
            }
        }
        

        /// <summary>
        /// 获取玩家当前朝向的方向向量
        /// </summary>
        public static Vector2 GetPlayerDirection(Player player) {
            return (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
        }

        /// <summary>
        /// 根据鼠标位置设置玩家朝向
        /// </summary>
        public static void SetPlayerDirectionToMouse(Player player) {
            Vector2 direction = GetPlayerDirection(player);
            player.direction = direction.X > 0 ? 1 : -1;
        }

        /// <summary>
        /// 计算物品旋转角度（用于UseStyle中）
        /// </summary>
        public static float GetItemRotationToMouse(Player player) {
            Vector2 direction = GetPlayerDirection(player);
            float rotation = direction.ToRotation();

            return rotation;
        }
    }
    public static class Easing {

        /// <summary>线性插值，无缓动</summary>
        public static float Linear(float t) => t;


        /// <summary>二次函数缓入</summary>
        public static float QuadIn(float t) => t * t;

        /// <summary>二次函数缓出</summary>
        public static float QuadOut(float t) => 1f - (1f - t) * (1f - t);

        /// <summary>二次函数缓入缓出</summary>
        public static float QuadInOut(float t) =>
            t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);

        // ===== 三次函数缓动 =====

        /// <summary>三次函数缓入</summary>
        public static float CubicIn(float t) => t * t * t;

        /// <summary>三次函数缓出</summary>
        public static float CubicOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

        /// <summary>三次函数缓入缓出</summary>
        public static float CubicInOut(float t) =>
            t < 0.5f ? 4f * t * t * t : 1f - 4f * (1f - t) * (1f - t) * (1f - t);

        // ===== 四次函数缓动 =====

        /// <summary>四次函数缓入</summary>
        public static float QuartIn(float t) => t * t * t * t;

        /// <summary>四次函数缓出</summary>
        public static float QuartOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t) * (1f - t);

        /// <summary>四次函数缓入缓出</summary>
        public static float QuartInOut(float t) =>
            t < 0.5f ? 8f * t * t * t * t : 1f - 8f * (1f - t) * (1f - t) * (1f - t) * (1f - t);

        // ===== 五次函数缓动 =====

        /// <summary>五次函数缓入</summary>
        public static float QuintIn(float t) => t * t * t * t * t;

        /// <summary>五次函数缓出</summary>
        public static float QuintOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t) * (1f - t) * (1f - t);

        /// <summary>五次函数缓入缓出</summary>
        public static float QuintInOut(float t) =>
            t < 0.5f ? 16f * t * t * t * t * t : 1f - 16f * (1f - t) * (1f - t) * (1f - t) * (1f - t) * (1f - t);

        // ===== 正弦函数缓动 =====

        /// <summary>正弦函数缓入</summary>
        public static float SineIn(float t) => 1f - (float)Math.Cos(t * Math.PI * 0.5);

        /// <summary>正弦函数缓出</summary>
        public static float SineOut(float t) => (float)Math.Sin(t * Math.PI * 0.5);

        /// <summary>正弦函数缓入缓出</summary>
        public static float SineInOut(float t) => 0.5f * (1f - (float)Math.Cos(t * Math.PI));

        // ===== 指数函数缓动 =====

        /// <summary>指数函数缓入</summary>
        public static float ExpoIn(float t) => t == 0f ? 0f : (float)Math.Pow(2, 10 * (t - 1));

        /// <summary>指数函数缓出</summary>
        public static float ExpoOut(float t) => t == 1f ? 1f : 1f - (float)Math.Pow(2, -10 * t);



        /// <summary>圆形函数缓入</summary>
        public static float CircIn(float t) => 1f - (float)Math.Sqrt(1 - t * t);

        /// <summary>圆形函数缓出</summary>
        public static float CircOut(float t) => (float)Math.Sqrt(1 - (t - 1) * (t - 1));

        /// <summary>回弹缓入</summary>
        public static float BackIn(float t, float s = 1.70158f) => t * t * ((s + 1) * t - s);

        /// <summary>回弹缓出</summary>
        public static float BackOut(float t, float s = 1.70158f) =>
            1f + (t - 1) * (t - 1) * ((s + 1) * (t - 1) + s);

        /// <summary>回弹缓入缓出</summary>
        public static float BackInOut(float t, float s = 1.70158f) {
            s *= 1.525f;
            return t < 0.5f ?
                2f * t * t * ((s + 1) * 2f * t - s) :
                1f + 2f * (t - 1) * (t - 1) * ((s + 1) * 2f * (t - 1) + s);
        }

        /// <summary>弹性缓入</summary>
        public static float ElasticIn(float t, float amplitude = 1f, float period = 0.3f) {
            if (t == 0f || t == 1f) return t;
            float s = period / 4f;
            return -(amplitude * (float)Math.Pow(2, 10 * (t - 1)) *
                     (float)Math.Sin((t - 1 - s) * (2 * Math.PI) / period));
        }

        /// <summary>弹性缓出</summary>
        public static float ElasticOut(float t, float amplitude = 1f, float period = 0.3f) {
            if (t == 0f || t == 1f) return t;
            float s = period / 4f;
            return amplitude * (float)Math.Pow(2, -10 * t) *
                   (float)Math.Sin((t - s) * (2 * Math.PI) / period) + 1f;
        }

        /// <summary>弹性缓入缓出</summary>
        public static float ElasticInOut(float t, float amplitude = 1f, float period = 0.3f) {
            if (t == 0f || t == 1f) return t;
            float s = period / 4f;
            return t < 0.5f ?
                -0.5f * amplitude * (float)Math.Pow(2, 20 * t - 10) *
                (float)Math.Sin((2 * t - 1 - s) * Math.PI / period) :
                0.5f * amplitude * (float)Math.Pow(2, -20 * t + 10) *
                (float)Math.Sin((2 * t - 1 - s) * Math.PI / period) + 1f;
        }


        // ===== 实用函数 =====

        /// <summary>平滑步函数</summary>
        public static float SmoothStep(float t) => t * t * (3f - 2f * t);

        /// <summary>更平滑的步函数</summary>
        public static float SmootherStep(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    public struct VertexPositionColorTexture : IVertexType {
        public Vector2 Position;
        public Vector3 TexCoord;
        public Color Color;
        public VertexPositionColorTexture(Vector2 position, Vector3 texCoord, Color color) {
            Position = position;
            TexCoord = texCoord;
            Color = color;
        }
        public VertexDeclaration VertexDeclaration => _vertexDeclaration;
        private static readonly VertexDeclaration _vertexDeclaration = new VertexDeclaration(
            new VertexElement(0, VertexElementFormat.Vector2, VertexElementUsage.Position, 0),
            new VertexElement(8, VertexElementFormat.Vector3, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(20, VertexElementFormat.Color, VertexElementUsage.Color, 0)
        );
    }
    public struct VertexPositionColor : IVertexType {
        public Vector2 Position;
        public Color Color;
        public VertexPositionColor(Vector2 position, Color color) {
            Position = position;
            Color = color;
        }
        public VertexDeclaration VertexDeclaration => _vertexDeclaration;
        private static readonly VertexDeclaration _vertexDeclaration = new VertexDeclaration(
            new VertexElement(0, VertexElementFormat.Vector2, VertexElementUsage.Position, 0),
            new VertexElement(8, VertexElementFormat.Color, VertexElementUsage.Color, 0)
        );
    }

}