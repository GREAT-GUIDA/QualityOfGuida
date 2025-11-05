using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QualityOfGuida.Content.Particles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace QualityOfGuida.Content.Spawner {
    public class FlameParticle : Particle {
        private float initialAlpha;
        private Vector2 windEffect;
        public override Texture2D Texture => ModContent.Request<Texture2D>(ModAssets.ContentDir + "/Particles/Flame").Value;

        public override void SetDefaults() {
            base.SetDefaults();

            width = 8;
            height = 8;
            timeLeft = (int)(Main.rand.Next(60, 140) / 1.5f);
            maxTimeLeft = timeLeft;

            windEffect = new Vector2(Main.rand.NextFloat(-0.005f, 0.005f), -0.01f) * 1.5f;

            scale = Main.rand.NextFloat(0.5f, 1.2f);

            useLighting = false;

            drawLayer = ParticleLayer.AfterDust;

            light = 0.1f;
            lightColor = color;
        }

        public override void AI() {
            oldPosition = position;
            oldVelocity = velocity;
            oldRotation = rotation;

            velocity += windEffect;

            velocity.Y *= 0.985f;
            velocity.X *= 0.95f;

            position += velocity;
            rotation += angVelocity;

            alpha = Math.Max(0, (float)timeLeft / 20);

            scale *= 0.996f;

            light = 0.1f * (alpha);

            timeLeft--;

            if (timeLeft <= 0 || alpha <= 0) {
                Kill();
            }
        }
    }
}