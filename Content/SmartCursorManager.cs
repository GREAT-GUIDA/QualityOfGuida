using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using GuidaSharedCode;

namespace QualityOfGuida.Content.SmartCursor {
    public delegate bool ValidateTargetDelegate(int x, int y);

    public struct SmartCursorConfig {
        public ValidateTargetDelegate ValidateTarget;
        public bool RequireEmptyMousePosition;
        public bool RequireNoSolidMousePosition;

        public SmartCursorConfig(ValidateTargetDelegate validateTarget) {
            ValidateTarget = validateTarget;
            RequireEmptyMousePosition = false;
            RequireNoSolidMousePosition = false;
        }
    }

    public class SmartCursorManager : ModSystem {
        // 按玩家存储智能光标状态
        private static Dictionary<int, Point> _playerSmartTargets = new Dictionary<int, Point>();
        private static Dictionary<int, bool> _playerHasValidTarget = new Dictionary<int, bool>();

        public static void UpdateDraw(int mouseX, int mouseY, Player player, SmartCursorConfig config, int? searchRadius = null) {
            Point smartTarget = GetSmartTarget(mouseX, mouseY, player, config);
            bool hasValidTarget = smartTarget.X != -1 && smartTarget.Y != -1;

            // 更新对应玩家的状态
            _playerHasValidTarget[player.whoAmI] = hasValidTarget;

            if (hasValidTarget) {
                _playerSmartTargets[player.whoAmI] = smartTarget;
            }
        }

        public override void PostDrawInterface(SpriteBatch spriteBatch) {
            Player localPlayer = Main.LocalPlayer;

            // 只处理本地玩家的绘制
            if (!_playerHasValidTarget.TryGetValue(localPlayer.whoAmI, out bool hasValidTarget) || !hasValidTarget) {
                return;
            }

            // 重置状态，避免下一帧重复绘制
            _playerHasValidTarget[localPlayer.whoAmI] = false;

            if (!Main.SmartCursorIsUsed || Main.gameMenu) return;

            // 获取本地玩家的智能光标目标
            if (!_playerSmartTargets.TryGetValue(localPlayer.whoAmI, out Point currentSmartTarget)) {
                return;
            }

            spriteBatch.EndAndBegin(BlendState.NonPremultiplied, null, null, Main.GameViewMatrix.TransformationMatrix);

            Texture2D targetTexture = ModAsset.SmartCursorTarget.Value;
            Vector2 worldPos = new Vector2(currentSmartTarget.X * 16 - 2, currentSmartTarget.Y * 16 - 2);
            Vector2 screenPos = worldPos - Main.screenPosition;

            // 获取目标位置的光照颜色
            Color lightColor = Lighting.GetColor(currentSmartTarget.X, currentSmartTarget.Y);

            spriteBatch.Draw(targetTexture, screenPos, lightColor);
            spriteBatch.EndAndBegin(BlendState.AlphaBlend, null, null, Main.UIScaleMatrix);
        }

        // 清理离开玩家的数据
        public override void PostUpdatePlayers() {
            // 清理不存在的玩家数据
            var playersToRemove = new List<int>();

            foreach (int playerId in _playerSmartTargets.Keys) {
                if (playerId >= Main.maxPlayers || !Main.player[playerId].active) {
                    playersToRemove.Add(playerId);
                }
            }

            foreach (int playerId in playersToRemove) {
                _playerSmartTargets.Remove(playerId);
                _playerHasValidTarget.Remove(playerId);
            }
        }

        public static Point GetSmartTarget(int mouseX, int mouseY, Player player, SmartCursorConfig config, int? searchRadius = null) {
            // 检查鼠标位置限制
            if (config.RequireEmptyMousePosition && !IsEmptyTile(mouseX, mouseY)) {
                return new Point(-1, -1);
            }
            if (config.RequireNoSolidMousePosition && !IsNoSolidTile(mouseX, mouseY)) {
                return new Point(-1, -1);
            }

            // 检查鼠标位置本身是否有效
            if (config.ValidateTarget(mouseX, mouseY)) {
                return new Point(mouseX, mouseY);
            }

            // 搜索最近的有效目标
            Point bestTarget = new Point(-1, -1);
            float bestDistance = float.MaxValue;
            Vector2 mousePos = new Vector2(mouseX, mouseY);

            int tileRange = searchRadius ?? Player.tileRangeX;
            int playerTileX = (int)(player.Center.X / 16f);
            int playerTileY = (int)(player.Center.Y / 16f);

            for (int i = playerTileX - tileRange; i <= playerTileX + tileRange; i++) {
                for (int j = playerTileY - tileRange; j <= playerTileY + tileRange; j++) {
                    if (config.ValidateTarget(i, j)) {
                        float distance = Vector2.Distance(new Vector2(i, j), mousePos);
                        if (distance < bestDistance) {
                            bestDistance = distance;
                            bestTarget = new Point(i, j);
                        }
                    }
                }
            }

            return bestTarget;
        }

        public static Point GetUseTarget(int mouseX, int mouseY, Player player, SmartCursorConfig config) {
            Point targetPos = Main.SmartCursorIsUsed ?
                GetSmartTarget(mouseX, mouseY, player, config) :
                new Point(mouseX, mouseY);

            return targetPos.X == -1 || targetPos.Y == -1 ? new Point(mouseX, mouseY) : targetPos;
        }

        public static bool IsInRange(int x, int y, Player player) {
            int playerTileX = (int)(player.Center.X / 16f);
            int playerTileY = (int)(player.Center.Y / 16f);
            return Math.Abs(x - playerTileX) <= Player.tileRangeX &&
                   Math.Abs(y - playerTileY) <= Player.tileRangeY;
        }

        public static bool IsEmptyTile(int x, int y) {
            return WorldGen.InWorld(x, y) && !Main.tile[x, y].HasTile;
        }

        public static bool IsNoSolidTile(int x, int y) {
            if (!WorldGen.InWorld(x, y)) return false;
            Tile tile = Main.tile[x, y];
            return !tile.HasTile || !Main.tileSolid[tile.TileType];
        }

        public static bool HasOpenAdjacentSpace(int x, int y) {
            for (int dx = -1; dx <= 1; dx++) {
                for (int dy = -1; dy <= 1; dy++) {
                    if (dx == 0 && dy == 0) continue;
                    int checkX = x + dx;
                    int checkY = y + dy;
                    if (WorldGen.InWorld(checkX, checkY)) {
                        Tile tile = Main.tile[checkX, checkY];
                        if (!tile.HasTile || !Main.tileSolid[tile.TileType]) {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public static bool HasValidAdjacentTarget(int x, int y, ValidateTargetDelegate validateTarget) {
            for (int dx = -1; dx <= 1; dx++) {
                for (int dy = -1; dy <= 1; dy++) {
                    if (dx == 0 && dy == 0) continue;
                    int checkX = x + dx;
                    int checkY = y + dy;
                    if (WorldGen.InWorld(checkX, checkY) && validateTarget(checkX, checkY)) {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}