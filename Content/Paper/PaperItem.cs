using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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

        public override string GetDynamicTexturePath() {
            return string.IsNullOrEmpty(paperContent) ?
                "QualityOfGuida/Content/Paper/PaperItem" :
                "QualityOfGuida/Content/Paper/PaperItem_1";
        }
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

            // 根据是否有内容决定堆叠数量
            UpdateStackAndProperties();
        }

        private void UpdateStackAndProperties() {
            Item.maxStack = string.IsNullOrEmpty(paperContent) ? Item.CommonMaxStack : 1;
        }

        public override bool CanStack(Item item2) {
            // 只有两个都是空白纸才能堆叠
            if (item2.ModItem is PaperItem otherPaper) {
                return string.IsNullOrEmpty(paperContent) && string.IsNullOrEmpty(otherPaper.paperContent);
            }
            return false;
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override bool? UseItem(Player player) {
            SoundEngine.PlaySound(ModAssets.PaperOpen, player.Center);
            OpenPaperEditor(player);
            return true;
        }

        private void OpenPaperEditor(Player player) {
            // 关闭其他界面
            Main.editChest = false;
            Main.SetNPCShopIndex(0);
            Main.playerInventory = false;
            Main.InGuideCraftMenu = false;
            player.SetTalkNPC(-1);

            // 设置告示牌编辑状态
            Main.editSign = true;
            Main.npcChatText = paperContent;

            // 找到安全的告示牌索引
            int signIndex = FindSafeSignIndex();
            player.sign = signIndex;

            // 初始化虚拟告示牌
            if (Main.sign[signIndex] == null)
                Main.sign[signIndex] = new Sign();

            // 寻找离玩家最近的空闲格子
            Point nearestEmptyTile = FindNearestEmptyTile(player);

            // 设置虚拟告示牌属性
            Main.sign[signIndex].x = nearestEmptyTile.X;
            Main.sign[signIndex].y = nearestEmptyTile.Y;
            Main.sign[signIndex].text = paperContent;

            // 开始监听编辑
            ModContent.GetInstance<PaperEditWatcher>().StartEdit(this, signIndex, player);
        }

        private Point FindNearestEmptyTile(Player player) {
            int playerTileX = (int)(player.position.X / 16);
            int playerTileY = (int)(player.position.Y / 16);

            // 首先检查玩家自己所在的格子
            if (!Main.tile[playerTileX, playerTileY].HasTile) {
                return new Point(playerTileX, playerTileY);
            }

            // 最大搜索半径
            int maxRadius = 10;

            // 从距离1开始向外搜索
            for (int radius = 1; radius <= maxRadius; radius++) {
                // 搜索当前半径的所有位置
                for (int dx = -radius; dx <= radius; dx++) {
                    for (int dy = -radius; dy <= radius; dy++) {
                        // 只检查当前半径边界上的点（优化性能）
                        if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                            continue;

                        int checkX = playerTileX + dx;
                        int checkY = playerTileY + dy;

                        // 检查坐标是否在世界范围内
                        if (checkX < 0 || checkX >= Main.maxTilesX || checkY < 0 || checkY >= Main.maxTilesY)
                            continue;

                        // 检查该位置是否为空闲格子
                        if (!Main.tile[checkX, checkY].HasTile) {
                            return new Point(checkX, checkY);
                        }
                    }
                }
            }

            // 如果没找到空闲格子，返回玩家位置作为备选
            return new Point(playerTileX, playerTileY);
        }

        private int FindSafeSignIndex() {
            // 从后往前找空槽位，避免与真实告示牌冲突
            for (int i = Main.sign.Length - 1; i >= Main.sign.Length - 50; i--) {
                if (Main.sign[i] == null || string.IsNullOrEmpty(Main.sign[i].text))
                    return i;
            }
            return Main.sign.Length - 1;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            List<TooltipLine> originalTooltips = new List<TooltipLine>(tooltips);
            tooltips.Clear();

            foreach (var tooltip in originalTooltips) {
                if (tooltip.Name == "ItemName") {
                    tooltips.Add(tooltip);
                }
            }

            // 2. 添加自定义内容
            if (!string.IsNullOrEmpty(paperContent)) {
                string[] lines = paperContent.Split('\n');
                for (int i = 0; i < Math.Min(lines.Length, 20); i++) {
                    string line = lines[i].Trim();
                    if (!string.IsNullOrEmpty(line)) {
                        tooltips.Add(new TooltipLine(Mod, $"Content{i}", line));
                    }
                }
            }

            // 3. 添加原有的tooltip（除了原ItemName）
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

        // 网络发送方法
        public override void NetSend(BinaryWriter writer) {
            writer.Write(paperContent ?? "");
        }

        // 网络接收方法
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
    // 简化的编辑监听系统
    public class PaperEditWatcher : ModSystem {
        private PaperItem _editingPaper;
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
                if (_editingPaper != null) {
                    // 选择贴图
                    string texturePath = string.IsNullOrEmpty(_editingPaper.GetContent()) ?
                        "QualityOfGuida/Content/Paper/PaperItem" :
                        "QualityOfGuida/Content/Paper/PaperItem_1";
                    sb.End();
                    sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, null, null, null,
                        Main.UIScaleMatrix);

                    Texture2D paperTexture = ModContent.Request<Texture2D>(texturePath).Value;

                    // 简单居中绘制，固定大小
                    Vector2 drawPos = panel.Location.ToVector2() + new Vector2(17 + 46, 18 + 47); // 中心位置
                    Vector2 origin = new Vector2(paperTexture.Width / 2, paperTexture.Height / 2);
                    float scale = 2.0f; // 固定缩放
                    sb.Draw(paperTexture, drawPos, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);

                    sb.End();
                    sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.SamplerStateForCursor, DepthStencilState.None,
                        RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
                }
            }
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

            // 编辑结束时保存
            if (!Main.editSign) {
                if (_editingPaper != null && _editingPlayer != null) {
                    string newContent = Main.npcChatText;

                    if (!string.IsNullOrEmpty(newContent) && _editingPaper.Item.stack > 1) {
                        // 分离逻辑保持不变...
                        _editingPaper.Item.stack -= 1;
                        Item writtenPaper = new Item();
                        writtenPaper.SetDefaults(_editingPaper.Item.type);
                        if (writtenPaper.ModItem is PaperItem paperItem) {
                            paperItem.SetContent(newContent);
                        }
                        writtenPaper.stack = 1;
                        _editingPlayer.QuickSpawnItem(_editingPlayer.GetSource_ItemUse(_editingPaper.Item), writtenPaper);
                        SoundEngine.PlaySound(ModAssets.PaperWrite, _editingPlayer.Center);
                    } else {
                        if (string.Equals(newContent, _lastSignText, StringComparison.Ordinal) &&
                            !string.Equals(newContent, _originalContent, StringComparison.Ordinal)) {

                            _editingPaper.SetContent(newContent);
                            SoundEngine.PlaySound(ModAssets.PaperWrite, _editingPlayer.Center);

                            // 简单同步：找到物品在库存中的位置
                            for (int i = 0; i < _editingPlayer.inventory.Length; i++) {
                                if (_editingPlayer.inventory[i]?.ModItem == _editingPaper) {
                                    PaperSyncSystem.SyncPaperToServer(_editingPlayer, i, newContent);
                                    break;
                                }
                            }
                        } else {
                            SoundEngine.PlaySound(ModAssets.PaperClose, _editingPlayer.Center);
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
            // 清理虚拟告示牌
            if (_signIndex >= 0 && _signIndex < Main.sign.Length && Main.sign[_signIndex] != null) {
                Main.sign[_signIndex].text = "";
            }
            _isEditing = false;
            _editingPaper = null;
            _signIndex = -1;
            _editingPlayer = null;
            Main.LocalPlayer.sign = -1;
            Main.npcChatText = "";
        }
    }
}
