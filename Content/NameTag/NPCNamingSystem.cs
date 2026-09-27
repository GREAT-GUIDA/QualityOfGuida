using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Audio;
using QualityOfGuida.Content.NameTag;
using Terraria.Chat;

namespace QualityOfGuida.Content.NameTag {
    public class NPCNamingSystem : GlobalNPC {
        private string customName = "";
        public bool effectsApplied = false;
        private Color originalColor;
        private bool colorSaved = false;
        public bool wasBoss = false;

        public override bool InstancePerEntity => true;

        public override void SetDefaults(NPC npc) {
            customName = "";
            effectsApplied = false;
            colorSaved = false;
            wasBoss = false;
        }

        public void SetCustomName(NPC npc, string name) {
            if (HasCustomName()) {
                ModContent.GetInstance<SimpleNamedNPCRevengeSystem>().StopTracking(npc.whoAmI);
                DeApplySpecialEffects(npc);
            }

            customName = name ?? "";
            npc.GivenName = customName;
            effectsApplied = false;

            if (HasCustomName()) {
                ModContent.GetInstance<SimpleNamedNPCRevengeSystem>().StartTracking(npc.whoAmI, customName);
                ApplySpecialEffects(npc);
            }

            // 简化网络同步，只设置netUpdate
            if (Main.netMode != NetmodeID.SinglePlayer) {
                npc.netUpdate = true;
            }
        }

        // 新增：直接设置状态，供网络同步使用
        public void SetDirectState(string name, bool effects, bool wasBossState) {
            customName = name ?? "";
            effectsApplied = effects;
            wasBoss = wasBossState;
        }

        public override bool CheckDead(NPC npc) {
            if (HasCustomName()) {
                ModContent.GetInstance<SimpleNamedNPCRevengeSystem>().StopTracking(npc.whoAmI);
            }
            return base.CheckDead(npc);
        }

        public override void BossHeadSlot(NPC npc, ref int index) {
            if (HasCustomName() && customName.ToLower().Contains("king")) {
                if (index == -1) {
                    index = QualityOfGuida.KingBossHeadIndex;
                }
            }
        }

        public override void PostAI(NPC npc) {
            if (HasCustomName()) {
                if (npc.GivenName != customName) {
                    npc.GivenName = customName;
                }

                if (!effectsApplied) {
                    ApplySpecialEffects(npc);
                }

                if (customName.ToLower().Contains("mini")) {
                    npc.velocity *= 2f;
                }
            }
        }

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!HasCustomName()) return true;

            string nameLower = customName.ToLower();

            if (!colorSaved) {
                originalColor = npc.color;
                colorSaved = true;
            }

            if (nameLower.Contains("rainbow")) {
                float hue = (Main.GameUpdateCount * 0.02f) % 1f;
                npc.color = Main.hslToRgb(hue, 1f, 0.7f);
            }

            if (nameLower.Contains("boulder")) {
                DrawBoulder(spriteBatch, npc, screenPos);
                return false;
            }

            if (nameLower.Contains("reverse")) {
                npc.spriteDirection = -npc.spriteDirection;
                npc.rotation += MathHelper.Pi;
            }

            return true;
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            string nameLower = customName.ToLower();

            if (nameLower.Contains("reverse")) {
                npc.spriteDirection = -npc.spriteDirection;
                npc.rotation -= MathHelper.Pi;
            }

            if (nameLower.Contains("rainbow")) {
                npc.color = originalColor;
            }

            if (nameLower.Contains("hitbox")) {
                DrawHitbox(spriteBatch, npc, screenPos);
            }

            Player localPlayer = Main.LocalPlayer;
            if (localPlayer?.HeldItem?.ModItem is NameTagItem nameTagItem) {
                
                if (nameTagItem.targetNPC == npc) {
                    string nameTagContent = nameTagItem.GetContent();
                    string previewText = "";
                    Color previewColor = Color.White;

                    if (string.IsNullOrEmpty(nameTagContent)) {
                        if (HasCustomName()) {
                            previewText = customName;
                            previewColor = Color.Red;
                        }
                    } else {
                        previewText = nameTagContent;
                        previewColor = Color.Lime;
                    }

                    if (!string.IsNullOrEmpty(previewText)) {
                        Vector2 previewSize = FontAssets.MouseText.Value.MeasureString(previewText) * 0.8f;
                        Vector2 previewPos = npc.Top - screenPos - new Vector2(previewSize.X / 2, previewSize.Y + 10);
                        Utils.DrawBorderString(spriteBatch, previewText, previewPos, previewColor, 0.8f);
                    }
                    return;
                }
            }

            Vector2 normalSize = FontAssets.MouseText.Value.MeasureString(customName) * 0.8f;
            Color normalColor = Color.White;
            if (nameLower.Contains("rainbow")) {
                float hue = (Main.GameUpdateCount * 0.02f) % 1f;
                normalColor = Main.hslToRgb(hue, 1f, 0.7f);
            }
            Vector2 normalPos = npc.Top - screenPos - new Vector2(normalSize.X / 2, normalSize.Y + 10);
            Utils.DrawBorderString(spriteBatch, customName, normalPos, normalColor, 0.8f);
        }

        public override bool PreAI(NPC npc) {
            if (HasCustomName()) {
                string nameLower = customName.ToLower();
                if (nameLower.Contains("mini")) {
                    npc.velocity /= 2f;
                }
            }
            return base.PreAI(npc);
        }

        private void ApplySpecialEffects(NPC npc) {
            if (effectsApplied) return;

            string nameLower = customName.ToLower();

            if (nameLower.Contains("giant")) {
                npc.lifeMax = (int)(npc.lifeMax * 2f);
                npc.life = (int)(npc.life * 2f);
                npc.scale *= 1.5f;
                npc.width = (int)(npc.width * 1.5f);
                npc.height = (int)(npc.height * 1.5f);
            }

            if (nameLower.Contains("mini")) {
                npc.scale *= 2f / 3f;
                npc.width = (int)(npc.width * 2f / 3f);
                npc.height = (int)(npc.height * 2f / 3f);
            }

            if (nameLower.Contains("king")) {
                wasBoss = npc.boss;
                if (!npc.boss) {
                    npc.boss = true;

                    npc.lifeMax = (int)(npc.lifeMax * 2);
                    npc.life = (int)(npc.life * 2);
                    npc.damage = (int)(npc.damage * 2);

                    if (npc.ModNPC != null) {
                        npc.ModNPC.Music = MusicID.Boss1;
                    }

                    SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                    if (Main.netMode == NetmodeID.SinglePlayer) {
                        Main.NewText(Language.GetTextValue("Announcement.HasAwoken", npc.TypeName), 175, 75);
                    }
                }
            }

            effectsApplied = true;
        }

        private void DeApplySpecialEffects(NPC npc) {
            if (!effectsApplied) return;

            string nameLower = customName.ToLower();

            if (nameLower.Contains("giant")) {
                npc.lifeMax = (int)(npc.lifeMax / 2f);
                npc.life = (int)(npc.life / 2f);
                npc.scale /= 1.5f;
                npc.width = (int)(npc.width / 1.5f);
                npc.height = (int)(npc.height / 1.5f);
            }

            if (nameLower.Contains("mini")) {
                npc.scale /= 2f / 3f;
                npc.width = (int)(npc.width / 2f * 3f);
                npc.height = (int)(npc.height / 2f * 3f);
            }

            if (nameLower.Contains("king")) {
                if (npc.boss && !wasBoss) {
                    npc.boss = false;

                    npc.lifeMax = (int)(npc.lifeMax / 2);
                    npc.life = (int)(npc.life / 2);
                    npc.damage = (int)(npc.damage / 2);

                    if (npc.ModNPC != null) {
                        npc.ModNPC.Music = -1;
                    }
                }
            }

            effectsApplied = false;
        }

        private void DrawHitbox(SpriteBatch spriteBatch, NPC npc, Vector2 screenPos) {
            Rectangle hitbox = npc.Hitbox;
            Vector2 topLeft = new Vector2(hitbox.X, hitbox.Y) - screenPos;
            Vector2 size = new Vector2(hitbox.Width, hitbox.Height);

            Color boxColor = Color.Red * 0.3f;
            spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)size.X, (int)size.Y),
                boxColor);
        }

        private void DrawBoulder(SpriteBatch spriteBatch, NPC npc, Vector2 screenPos) {
            Rectangle hitbox = npc.Hitbox;
            Vector2 center = hitbox.Center.ToVector2() - screenPos;

            Texture2D boulderTexture = TextureAssets.Projectile[ProjectileID.Boulder].Value;
            float scale = Math.Max(hitbox.Width, hitbox.Height) / (float)Math.Max(boulderTexture.Width, boulderTexture.Height);
            Vector2 origin = new Vector2(boulderTexture.Width, boulderTexture.Height) * 0.5f;

            spriteBatch.Draw(boulderTexture, center, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
        }

        public string GetCustomName() => customName;
        public bool HasCustomName() => !string.IsNullOrEmpty(customName);
        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter) {
            binaryWriter.Write(customName ?? "");
            binaryWriter.Write(effectsApplied);
            binaryWriter.Write(wasBoss);

            // 传输所有关键NPC状态
            binaryWriter.Write(npc.scale);
            binaryWriter.Write(npc.lifeMax);
            binaryWriter.Write(npc.life);
            binaryWriter.Write(npc.damage);
            binaryWriter.Write(npc.boss);
            binaryWriter.Write(npc.width);
            binaryWriter.Write(npc.height);

            // 传输音乐
            int music = (npc.ModNPC != null) ? npc.ModNPC.Music : -1;
            binaryWriter.Write(music);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader) {
            string receivedName = binaryReader.ReadString();
            bool receivedEffects = binaryReader.ReadBoolean();
            bool receivedWasBoss = binaryReader.ReadBoolean();

            // 接收所有状态
            float receivedScale = binaryReader.ReadSingle();
            int receivedLifeMax = binaryReader.ReadInt32();
            int receivedLife = binaryReader.ReadInt32();
            int receivedDamage = binaryReader.ReadInt32();
            bool receivedBoss = binaryReader.ReadBoolean();
            int receivedWidth = binaryReader.ReadInt32();
            int receivedHeight = binaryReader.ReadInt32();
            int receivedMusic = binaryReader.ReadInt32();

            // 如果有变化就更新
            if (receivedName != customName || receivedEffects != effectsApplied) {
                customName = receivedName;
                effectsApplied = receivedEffects;
                wasBoss = receivedWasBoss;
                npc.GivenName = customName;

                // 直接应用所有状态，不重复计算
                npc.scale = receivedScale;
                npc.lifeMax = receivedLifeMax;
                npc.life = receivedLife;
                npc.damage = receivedDamage;
                npc.boss = receivedBoss;
                npc.width = receivedWidth;
                npc.height = receivedHeight;

                // 设置音乐
                if (npc.ModNPC != null && receivedMusic != -1) {
                    npc.ModNPC.Music = receivedMusic;
                }
            }
        }
        public override void SaveData(NPC npc, TagCompound tag) {
            if (HasCustomName()) {
                tag["customName"] = customName;
            }
        }

        public override void LoadData(NPC npc, TagCompound tag) {
            customName = tag.GetString("customName");

            if (HasCustomName()) {
                npc.GivenName = customName;
            }
        }

        public static void SetNPCName(NPC npc, string name) {
            if (npc?.active != true) return;
            var globalNPC = npc.GetGlobalNPC<NPCNamingSystem>();
            globalNPC?.SetCustomName(npc, name);
        }

        public static string GetNPCName(NPC npc) {
            if (npc?.active != true) return "";
            var globalNPC = npc.GetGlobalNPC<NPCNamingSystem>();
            return globalNPC?.GetCustomName() ?? "";
        }

        public static bool NPCHasCustomName(NPC npc) {
            if (npc?.active != true) return false;
            var globalNPC = npc.GetGlobalNPC<NPCNamingSystem>();
            return globalNPC?.HasCustomName() ?? false;
        }
    }
}