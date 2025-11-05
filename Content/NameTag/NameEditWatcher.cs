using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.Audio;
using Terraria.ModLoader;
using Terraria;
using Microsoft.Xna.Framework;
using QualityOfGuida.Content.Paper;

namespace QualityOfGuida.Content.NameTag
{

    // 简化的编辑监听系统
    public class NameTagEditWatcher : ModSystem {
        private NameTagItem _editingNameTag;
        private int _signIndex;
        public bool _isEditing;
        private Player _editingPlayer;
        private string _originalContent;
        private string _lastSignText;

        public override void PostSetupContent() {
            if (ModLoader.TryGetMod("DialogueTweak", out Mod dialogueTweak)) {
                dialogueTweak.Call("OnPostPortraitDraw", DrawSomething);
            }
        }

        private void DrawSomething(SpriteBatch sb, Color textColor, Rectangle panel) {
            if (_isEditing && _signIndex >= 0 && _signIndex < Main.sign.Length && Main.sign[_signIndex] != null) {
                if (_editingNameTag != null) {
                    // 选择贴图
                    string texturePath = _editingNameTag.GetDynamicTexturePath();
                    sb.End();
                    sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, null, null, null,
                        Main.UIScaleMatrix);

                    Texture2D nameTagTexture = ModContent.Request<Texture2D>(texturePath).Value;

                    Vector2 drawPos = panel.Location.ToVector2() + new Vector2(17 + 46, 18 + 47);
                    Vector2 origin = new Vector2(nameTagTexture.Width / 2, nameTagTexture.Height / 2);
                    float scale = 2.0f;
                    sb.Draw(nameTagTexture, drawPos, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);

                    sb.End();
                    sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.SamplerStateForCursor, DepthStencilState.None,
                        RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
                }
            }
        }

        public void StartEdit(NameTagItem nameTag, int signIndex, Player player) {
            _editingNameTag = nameTag;
            _signIndex = signIndex;
            _isEditing = true;
            _editingPlayer = player;
            _originalContent = nameTag.GetContent();
            _lastSignText = nameTag.GetContent();
        }

        public override void PostUpdateInput() {
            if (!_isEditing) return;

            if (Main.editSign && _editingNameTag != null && _editingPlayer != null) {
                string currentText = Main.npcChatText;

                if (!string.IsNullOrEmpty(currentText) && (currentText.Contains('\n') || currentText.Contains('\r'))) {
                    int newlineIndex = currentText.IndexOfAny(new char[] { '\n', '\r' });
                    if (newlineIndex >= 0) {
                        currentText = currentText.Substring(0, newlineIndex);
                    }

                    Main.editSign = false;
                    Main.npcChatText = currentText;
                } else {
                    _lastSignText = currentText;
                }
            }

            // 原有的编辑结束逻辑
            if (!Main.editSign) {
                if (_editingNameTag != null && _editingPlayer != null) {
                    string newContent = Main.npcChatText;

                    if (!string.IsNullOrEmpty(newContent) && _editingNameTag.Item.stack > 1) {
                        _editingNameTag.Item.stack -= 1;
                        Item writtenNameTag = new Item();
                        writtenNameTag.SetDefaults(_editingNameTag.Item.type);
                        if (writtenNameTag.ModItem is NameTagItem nameTagItem) {
                            nameTagItem.SetContent(newContent);
                        }
                        writtenNameTag.stack = 1;
                        _editingPlayer.QuickSpawnItem(_editingPlayer.GetSource_ItemUse(_editingNameTag.Item), writtenNameTag);
                        SoundEngine.PlaySound(ModAssets.PaperWrite, _editingPlayer.Center);
                    } else {
                        // 直接设置内容
                        if (string.Equals(newContent, _lastSignText, StringComparison.Ordinal) && !string.Equals(newContent, _originalContent, StringComparison.Ordinal)) {
                            _editingNameTag.SetContent(newContent);
                            SoundEngine.PlaySound(ModAssets.PaperWrite, _editingPlayer.Center);
                            for (int i = 0; i < _editingPlayer.inventory.Length; i++) {
                                if (_editingPlayer.inventory[i]?.ModItem == _editingNameTag) {
                                    NameTagSyncSystem.SyncNameTagToServer(_editingPlayer, i, newContent);
                                    break;
                                }
                            }
                        } else SoundEngine.PlaySound(ModAssets.PaperClose, _editingPlayer.Center);
                    }
                }
                StopEdit();
            }
        }

        private void StopEdit() {
            // 清理虚拟告示牌
            if (_signIndex >= 0 && _signIndex < Main.sign.Length && Main.sign[_signIndex] != null) {
                Main.sign[_signIndex].text = "";
            }
            _isEditing = false;
            _editingNameTag = null;
            _signIndex = -1;
            _editingPlayer = null;
            Main.LocalPlayer.sign = -1;
            Main.npcChatText = "";
        }
    }
}
