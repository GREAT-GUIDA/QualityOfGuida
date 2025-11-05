using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader;
using XPT.Core.Audio.MP3Sharp.Decoding;

namespace QualityOfGuida.Content.Particles {
    /// <summary>
    /// Abstract base class for all particles in the system, following Terraria's Projectile pattern.
    /// </summary>
    public class Particle {
        public Vector2 position;

        public Vector2 velocity;

        public Vector2 oldPosition;

        public Vector2 oldVelocity;

        public float rotation;

        public float angVelocity;

        public float oldRotation;

        public float scale = 1f;

        public int width = 16;

        public int height = 16;

        public int timeLeft = 60;

        public int maxTimeLeft = 60;

        public float alpha = 1f;

        public Color color = Color.White;

        public ParticleLayer drawLayer = ParticleLayer.AfterDust;

        public bool useLighting = false;

        public bool tileCollide = false;

        public bool active = true;

        public int type;

        public int owner = 255;

        public int[] ai = new int[4];

        public float[] localAI = new float[4];

        public int frameCounter;

        public int frame;

        public int direction = 1;

        public int spriteDirection = 1;

        public float light = 0f;

        public Color lightColor = Color.White;

        public int state = 0;

        public virtual Texture2D Texture => null;

        public virtual Rectangle? SourceRectangle => null;

        public virtual Vector2 Origin => Texture != null ? new Vector2(Texture.Width, Texture.Height) * 0.5f : Vector2.Zero;

        public virtual Color DrawColor {
            get {
                Color lightColor = useLighting ? Lighting.GetColor((int)(position.X / 16), (int)(position.Y / 16)) : Color.White;
                return color.MultiplyRGBA(lightColor) * alpha;
            }
        }

        public Rectangle Hitbox => new Rectangle((int)position.X - width / 2, (int)position.Y - height / 2, width, height);


        public virtual bool IsAlive => active && timeLeft > 0;

        public virtual void SetDefaults() {
            width = 16;
            height = 16;
            timeLeft = 60;
            scale = 1f;
            alpha = 1f;
            color = Color.White;
            active = true;
        }

        public virtual bool PreAI() {
            return true;
        }

        public virtual void AI() {
            oldPosition = position;
            oldVelocity = velocity;
            oldRotation = rotation;

            rotation += angVelocity;

            position += velocity;

            timeLeft--;

            if (timeLeft <= 0) {
                Kill();
            }
        }

        public virtual void PostAI() {
            if (ShouldCullOffscreen() && !IsOnScreen()) {
                Kill();
            }
        }

        public virtual bool PreDraw(SpriteBatch spriteBatch, Color lightColor) {
            return true;
        }

        public virtual void Draw(SpriteBatch spriteBatch, Color lightColor) {
            if (Texture == null) return;

            // Get draw position
            Vector2 drawPosition = GetDrawPosition();

            // Get final color
            Color finalColor = color.MultiplyRGBA(lightColor) * alpha;

            // Draw the particle
            spriteBatch.Draw(
                Texture,
                drawPosition,
                SourceRectangle,
                finalColor,
                rotation,
                Origin,
                scale,
                GetSpriteEffects(),
                0f
            );
        }

        public virtual void PostDraw(SpriteBatch spriteBatch, Color lightColor) {
        }

        public virtual void Kill() {
            active = false;
            OnKill();
        }

        public virtual void OnKill() {
        }

        protected virtual Vector2 GetDrawPosition() {
            Vector2 drawPosition = position;

            // Special case for water layer
            if (drawLayer == ParticleLayer.BeforeWater) {
                drawPosition += new Vector2(Main.offScreenRange, Main.offScreenRange);
            }

            // Apply screen position for world particles
            if (drawLayer != ParticleLayer.BeforeInterface && drawLayer != ParticleLayer.AfterInterface) {
                drawPosition -= Main.screenPosition;
            }

            return drawPosition;
        }

        protected virtual SpriteEffects GetSpriteEffects() {
            SpriteEffects effects = SpriteEffects.None;
            if (spriteDirection == -1) {
                effects |= SpriteEffects.FlipHorizontally;
            }
            return effects;
        }

        protected virtual bool ShouldCullOffscreen() {
            return drawLayer != ParticleLayer.BeforeInterface && drawLayer != ParticleLayer.AfterInterface;
        }

        protected virtual bool IsOnScreen() {
            Vector2 screenPos = position - Main.screenPosition;
            float margin = Math.Max(width, height) * scale + 50f;

            return screenPos.X > -margin &&
                   screenPos.X < Main.screenWidth + margin &&
                   screenPos.Y > -margin &&
                   screenPos.Y < Main.screenHeight + margin;
        }

        protected virtual bool DoTileCollision() {
            if (!tileCollide) return false;

            Vector2 newVelocity = Collision.TileCollision(position, velocity, width, height);
            if (newVelocity != velocity) {
                velocity = newVelocity;
                return true;
            }
            return false;
        }
    }

    
    /// <summary>
    /// Manages all particles in the system, handling updates and rendering across different layers.
    /// </summary>
    public class ParticleManager : ModSystem {
        private static ParticleManager _instance;
        public static ParticleManager Instance => _instance;

        /// <summary>
        /// Dictionary storing particles organized by their draw layer.
        /// </summary>
        private Dictionary<ParticleLayer, List<Particle>> particlesByLayer;

        /// <summary>
        /// List of particles to be added at the end of the frame.
        /// </summary>
        private List<Particle> particlesToAdd;

        /// <summary>
        /// Maximum number of particles that can exist at once.
        /// </summary>
        public int MaxParticles { get; set; } = 1000;

        /// <summary>
        /// Current total number of particles.
        /// </summary>
        public int ParticleCount { get; private set; }

        public override bool IsLoadingEnabled(Mod mod) {
            return !Main.dedServ;
        }

        public override void Load() {
            _instance = this;

            // Initialize collections
            particlesByLayer = new Dictionary<ParticleLayer, List<Particle>>();
            particlesToAdd = new List<Particle>();

            // Initialize particle lists for each layer
            foreach (ParticleLayer layer in Enum.GetValues<ParticleLayer>()) {
                if (layer != ParticleLayer.None) {
                    particlesByLayer[layer] = new List<Particle>();
                }
            }

            // Hook into all draw layers
            DrawHooks.Hook(ParticleLayer.BeforeBackground, Render);
            DrawHooks.Hook(ParticleLayer.BeforeWalls, Render);
            DrawHooks.Hook(ParticleLayer.BeforeNonSolidTiles, Render);
            DrawHooks.Hook(ParticleLayer.BeforeNPCsBehindTiles, Render);
            DrawHooks.Hook(ParticleLayer.BeforeSolidTiles, Render);
            DrawHooks.Hook(ParticleLayer.BeforePlayersBehindNPCs, Render);
            DrawHooks.Hook(ParticleLayer.BeforeNPCs, Render);
            DrawHooks.Hook(ParticleLayer.BeforeProjectiles, Render);
            DrawHooks.Hook(ParticleLayer.BeforePlayers, Render);
            DrawHooks.Hook(ParticleLayer.BeforeItems, Render);
            DrawHooks.Hook(ParticleLayer.BeforeRain, Render);
            DrawHooks.Hook(ParticleLayer.BeforeGore, Render);
            DrawHooks.Hook(ParticleLayer.BeforeDust, Render); 
            DrawHooks.Hook(ParticleLayer.AfterDust, Render);
            DrawHooks.Hook(ParticleLayer.BeforeWater, Render);
            DrawHooks.Hook(ParticleLayer.BeforeInterface, Render);
            DrawHooks.Hook(ParticleLayer.AfterInterface, Render);
        }

        public override void Unload() {
            // Unhook from all draw layers
            DrawHooks.UnHook(ParticleLayer.BeforeBackground, Render);
            DrawHooks.UnHook(ParticleLayer.BeforeWalls, Render);
            DrawHooks.UnHook(ParticleLayer.BeforeNonSolidTiles, Render);
            DrawHooks.UnHook(ParticleLayer.BeforeNPCsBehindTiles, Render);
            DrawHooks.UnHook(ParticleLayer.BeforeSolidTiles, Render);
            DrawHooks.UnHook(ParticleLayer.BeforePlayersBehindNPCs, Render);
            DrawHooks.UnHook(ParticleLayer.BeforeNPCs, Render);
            DrawHooks.UnHook(ParticleLayer.BeforeProjectiles, Render);
            DrawHooks.UnHook(ParticleLayer.BeforePlayers, Render);
            DrawHooks.UnHook(ParticleLayer.BeforeItems, Render);
            DrawHooks.UnHook(ParticleLayer.BeforeRain, Render);
            DrawHooks.UnHook(ParticleLayer.BeforeGore, Render);
            DrawHooks.UnHook(ParticleLayer.BeforeDust, Render);
            DrawHooks.UnHook(ParticleLayer.AfterDust, Render);
            DrawHooks.UnHook(ParticleLayer.BeforeWater, Render);
            DrawHooks.UnHook(ParticleLayer.BeforeInterface, Render);
            DrawHooks.UnHook(ParticleLayer.AfterInterface, Render);

            // Clear all particles
            ClearAllParticles();

            // Clear collections
            particlesByLayer?.Clear();
            particlesToAdd?.Clear();

            _instance = null;
        }

        public override void PostUpdateDusts() {
            UpdateParticles();
            UpdateParticleLighting();
        }

        /// <summary>
        /// Creates a new particle of the specified type.
        /// </summary>
        /// <typeparam name="T">The particle type to create</typeparam>
        /// <param name="position">Initial position</param>
        /// <param name="velocity">Initial velocity</param>
        /// <param name="type">Particle type ID</param>
        /// <param name="alpha">Initial alpha</param>
        /// <param name="scale">Initial scale</param>
        /// <returns>The created particle, or null if creation failed</returns>
        public T NewParticle<T>(Vector2 position, Vector2 velocity, int type = 0, float alpha = 1f, float scale = 1f) where T : Particle, new() {
            if (ParticleCount >= MaxParticles) return null;

            T particle = new T();
            particle.SetDefaults();
            particle.position = position;
            particle.velocity = velocity;
            particle.type = type;
            particle.alpha = alpha;
            particle.scale = scale;

            if (AddParticle(particle)) {
                return particle;
            }
            return null;
        }

        /// <summary>
        /// Adds a particle to the system.
        /// </summary>
        /// <param name="particle">The particle to add</param>
        /// <returns>True if the particle was added successfully</returns>
        public bool AddParticle(Particle particle) {
            if (particle == null) return false;
            if (ParticleCount >= MaxParticles) return false;

            particlesToAdd.Add(particle);
            return true;
        }

        /// <summary>
        /// Removes all particles from a specific layer.
        /// </summary>
        /// <param name="layer">The layer to clear</param>
        public void ClearLayer(ParticleLayer layer) {
            if (particlesByLayer.TryGetValue(layer, out List<Particle> particles)) {
                foreach (var particle in particles) {
                    particle.OnKill();
                }
                particles.Clear();
                RecalculateParticleCount();
            }
        }

        /// <summary>
        /// Removes all particles from the system.
        /// </summary>
        public void ClearAllParticles() {
            foreach (var kvp in particlesByLayer) {
                foreach (var particle in kvp.Value) {
                    particle.OnKill();
                }
                kvp.Value.Clear();
            }

            foreach (var particle in particlesToAdd) {
                particle.OnKill();
            }
            particlesToAdd.Clear();

            ParticleCount = 0;
        }

        /// <summary>
        /// Gets all particles in a specific layer.
        /// </summary>
        /// <param name="layer">The layer to query</param>
        /// <returns>Read-only list of particles in the layer</returns>
        public IReadOnlyList<Particle> GetParticlesInLayer(ParticleLayer layer) {
            return particlesByLayer.TryGetValue(layer, out List<Particle> particles) ? particles : new List<Particle>();
        }

        /// <summary>
        /// Gets all particles in the system.
        /// </summary>
        /// <returns>Enumerable of all particles</returns>
        public IEnumerable<Particle> GetAllParticles() {
            return particlesByLayer.Values.SelectMany(list => list);
        }

        /// <summary>
        /// Updates all particles in the system.
        /// </summary>
        private void UpdateParticles() {
            // Add pending particles
            foreach (var particle in particlesToAdd) {
                if (particlesByLayer.TryGetValue(particle.drawLayer, out List<Particle> layerList)) {
                    layerList.Add(particle);
                }
            }
            particlesToAdd.Clear();
            // Update particles in each layer
            foreach (var kvp in particlesByLayer) {
                var particles = kvp.Value;

                for (int i = particles.Count - 1; i >= 0; i--) {
                    var particle = particles[i];

                    try {
                        // Run AI sequence
                        if (particle.active && particle.PreAI()) {
                            particle.AI();
                            particle.PostAI();
                        }

                        // Remove if inactive or dead
                        if (!particle.IsAlive) {
                            particle.OnKill();
                            particles.RemoveAt(i);
                        }
                    } catch (Exception ex) {
                        particle.OnKill();
                        particles.RemoveAt(i);
                    }
                }
            }

            // Recalculate total particle count
            RecalculateParticleCount();
        }

        /// <summary>
        /// Renders particles for the specified layer.
        /// </summary>
        /// <param name="layer">The layer to render</param>
        private void Render(ParticleLayer layer) {
            if (!particlesByLayer.TryGetValue(layer, out List<Particle> particles)) return;
            if (particles.Count == 0) return;

            // Draw all particles in this layer
            foreach (var particle in particles) {
                if (!particle.active) continue;

                try {
                    // Get lighting
                    Color lightColor = particle.useLighting ? Lighting.GetColor((int)(particle.position.X / 16), (int)(particle.position.Y / 16)) : Color.White;

                    // Run draw sequence
                    if (particle.PreDraw(Main.spriteBatch, lightColor)) {
                        particle.Draw(Main.spriteBatch, lightColor);
                    }
                    particle.PostDraw(Main.spriteBatch, lightColor);
                } catch (Exception ex) {
                    particle.Kill();
                }
            }
        }
        private void UpdateParticleLighting() {
            foreach (var particle in GetAllParticles()) {
                if (particle.active && particle.light > 0f) {
                    Vector3 lightColor = particle.lightColor.ToVector3() * particle.light;
                    Lighting.AddLight((int)(particle.position.X / 16f), (int)(particle.position.Y / 16f),
                                     lightColor.X, lightColor.Y, lightColor.Z);
                }
            }
        }
        /// <summary>
        /// Recalculates the total number of particles across all layers.
        /// </summary>
        private void RecalculateParticleCount() {
            ParticleCount = particlesByLayer.Values.Sum(list => list.Count) + particlesToAdd.Count;
        }
    }
}