using GuidaSharedCode;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using QualityOfGuida;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace QualityOfGuida.Content.Paper {
    public class PaperItem : GuidaItem {
        private string paperContent = "";
        public override string GetDynamicDisplayName() {
            return string.IsNullOrEmpty(paperContent) ?
                Language.GetTextValue("Mods.QualityOfGuida.Items.PaperItem.BlankDisplayName") :
                Language.GetTextValue("Mods.QualityOfGuida.Items.PaperItem.DisplayName");
        }

        public override Asset<Texture2D> GetDynamicTextureAsset() =>
            string.IsNullOrEmpty(paperContent) ? ModAsset.PaperItem : ModAsset.PaperItem_1;
        public override bool IsLoadingEnabled(Mod mod) {
            return ModContent.GetInstance<ItemToggleConfig>().EnablePaper;
        }
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.useAnimation = 10;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.value = Item.buyPrice(copper: 1);
            Item.rare = ItemRarityID.White;
            Item.consumable = false;

            // ??????????????????????????
            UpdateStackAndProperties();
        }

        private void UpdateStackAndProperties() {
            Item.maxStack = string.IsNullOrEmpty(paperContent) ? Item.CommonMaxStack : 1;
        }

        public override bool CanStack(Item item2) {
            // ??????????????????????
            if (item2.ModItem is PaperItem otherPaper) {
                return string.IsNullOrEmpty(paperContent) && string.IsNullOrEmpty(otherPaper.paperContent);
            }
            return false;
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override bool? UseItem(Player player) {
            SoundEngine.PlaySound(QoGSound.PaperOpen, player.Center);
            OpenPaperEditor(player);
            return true;
        }

        private void OpenPaperEditor(Player player) {
            int signIndex = SignEditorHelper.OpenEditor(player, paperContent);
            ModContent.GetInstance<PaperEditWatcher>().StartEdit(this, signIndex, player);
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            List<TooltipLine> originalTooltips = new List<TooltipLine>(tooltips);
            tooltips.Clear();

            foreach (var tooltip in originalTooltips) {
                if (tooltip.Name == "ItemName") {
                    tooltips.Add(tooltip);
                }
            }

            // 2. ???????????????
            if (!string.IsNullOrEmpty(paperContent)) {
                string[] lines = paperContent.Split('\n');
                for (int i = 0; i < Math.Min(lines.Length, 20); i++) {
                    string line = lines[i].Trim();
                    if (!string.IsNullOrEmpty(line)) {
                        tooltips.Add(new TooltipLine(Mod, $"Content{i}", line));
                    }
                }
            }

            // 3. ??????ùù?tooltip???????ItemName??
            foreach (var tooltip in originalTooltips) {
                if (tooltip.Name != "ItemName" && tooltip.Name != "Material") {
                    tooltips.Add(tooltip);
                }
            }
        }

        public override void SaveData(TagCompound tag) => tag["paperContent"] = paperContent;

        public override void LoadData(TagCompound tag) {
            SetContent(tag.GetString("paperContent"));
        }

        // ???ùù?????
        public override void NetSend(BinaryWriter writer) {
            writer.Write(paperContent ?? "");
        }

        // ??????????
        public override void NetReceive(BinaryReader reader) {
            SetContent(reader.ReadString());
        }

        public override void AddRecipes() {
            Recipe recipe = CreateRecipe(2);
            recipe.AddIngredient(ItemID.BambooBlock, 1);
            recipe.AddTile(TileID.WorkBenches);
            recipe.Register();

            Recipe airplaneRecipe = Recipe.Create(ItemID.PaperAirplaneA);
            airplaneRecipe.AddIngredient(Type);
            airplaneRecipe.Register();

            airplaneRecipe = Recipe.Create(ItemID.PaperAirplaneB);
            airplaneRecipe.AddIngredient(Type);
            airplaneRecipe.Register();

            airplaneRecipe = Recipe.Create(ItemID.Book);
            airplaneRecipe.AddIngredient(Type, 6);
            airplaneRecipe.Register();
        }

        public void SetContent(string content) {
            paperContent = content ?? "";
            UpdateStackAndProperties();
        }

        public string GetContent() => paperContent;
    }
    // ??????????
    public class PaperEditWatcher : ModSystem {
        private PaperItem _editingPaper;
        private int _signIndex;
        public bool _isEditing;
        private Player _editingPlayer;
        private string _originalContent;
        private string _lastSignText;


        public override void PostSetupContent() {
            if (ModLoader.TryGetMod("DialogueTweak", out Mod dialogueTweak)) {
                dialogueTweak.Call("OnPostPortraitDraw", (Action<SpriteBatch, Color, Rectangle>)DrawSomething);
            }
        }
        private void DrawSomething(SpriteBatch sb, Color textColor, Rectangle panel) {
            if (!_isEditing || _editingPaper == null) {
                return;
            }

            SignEditorHelper.DrawPanelPortrait(sb, panel, _editingPaper.GetDynamicTextureAsset());
        }

        public void StartEdit(PaperItem paper, int signIndex, Player player) {
            _editingPaper = paper;
            _signIndex = signIndex;
            _isEditing = true;
            _editingPlayer = player;
            _originalContent = paper.GetContent();
            _lastSignText = paper.GetContent();
        }
        public override void PostUpdateInput() {
            if (!_isEditing) return;

            // ???????????
            if (!Main.editSign) {
                if (_editingPaper != null && _editingPlayer != null) {
                    string newContent = Main.npcChatText;

                    if (!string.IsNullOrEmpty(newContent) && _editingPaper.Item.stack > 1) {
                        // ???????????????...
                        _editingPaper.Item.stack -= 1;
                        Item writtenPaper = new Item();
                        writtenPaper.SetDefaults(_editingPaper.Item.type);
                        if (writtenPaper.ModItem is PaperItem paperItem) {
                            paperItem.SetContent(newContent);
                        }
                        writtenPaper.stack = 1;
                        _editingPlayer.QuickSpawnItem(_editingPlayer.GetSource_ItemUse(_editingPaper.Item), writtenPaper);
                        SoundEngine.PlaySound(QoGSound.PaperWrite, _editingPlayer.Center);
                    } else {
                        if (string.Equals(newContent, _lastSignText, StringComparison.Ordinal) &&
                            !string.Equals(newContent, _originalContent, StringComparison.Ordinal)) {

                            _editingPaper.SetContent(newContent);
                            SoundEngine.PlaySound(QoGSound.PaperWrite, _editingPlayer.Center);

                            // ??????????????????ùù?ùù??
                            for (int i = 0; i < _editingPlayer.inventory.Length; i++) {
                                if (_editingPlayer.inventory[i]?.ModItem == _editingPaper) {
                                    PaperSyncSystem.SyncPaperToServer(_editingPlayer, i, newContent);
                                    break;
                                }
                            }
                        } else {
                            SoundEngine.PlaySound(QoGSound.PaperClose, _editingPlayer.Center);
                        }
                    }
                }
                StopEdit();
            }

            if (Main.editSign) {
                if (_editingPaper != null && _editingPlayer != null) {
                    _lastSignText = Main.npcChatText;
                }
            }
        }
        private void StopEdit() {
            SignEditorHelper.CloseEditor(_signIndex);
            _isEditing = false;
            _editingPaper = null;
            _signIndex = -1;
            _editingPlayer = null;
        }
    }
}
