using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace QualityOfGuida.Content {
    public record TileActionConfig {
        public Func<int, int, bool> CustomPreprocess { get; set; } = null;

        public bool ClearWall { get; set; } = false;
        public int PlaceWallType { get; set; } = 0;
        public int WallPaintColor { get; set; } = 0;
        public bool ClearTile { get; set; } = false;
        public int PlaceTileType { get; set; } = 0;
        public int PlaceTileStyle { get; set; } = 0;
        public byte TileSlope { get; set; } = 0;

        public bool IsHalfBrick { get; set; } = false;

        public class ObjectPlaceData {
            public int Type { get; set; } = 0;
            public int Style { get; set; } = 0;
            public int Alternate { get; set; } = 0;
            public int Random { get; set; } = -1;
            public int Direction { get; set; } = -1;
        }

        public ObjectPlaceData PlaceObjectData { get; set; } = null;

        public int TilePaintColor { get; set; } = 0;

        public Action<int, int> CustomPostprocess { get; set; } = null;

        public int LiquidType { get; set; } = -1;

        public byte LiquidAmount { get; set; } = 255;

        public bool Mute { get; set; } = true;

        public bool Forced { get; set; } = false;


        public static TileActionConfig Tile(bool clear, int tileType = 0, int tileStyle = 0, byte slopeType = 0, bool halfBrick = false, int paintColor = 0) {
            return new TileActionConfig {
                ClearTile = true,
                PlaceTileType = tileType,
                PlaceTileStyle = tileStyle,
                TileSlope = slopeType,
                IsHalfBrick = halfBrick,
                TilePaintColor = paintColor
            };
        }
        public static TileActionConfig Wall(bool clear, int wallType = 0, int paintColor = 0) {
            return new TileActionConfig {
                ClearWall = true,
                PlaceWallType = wallType,
                WallPaintColor = paintColor
            };
        }
        public static TileActionConfig Liquid(int liquidType, byte amount = 255) {
            return new TileActionConfig {
                LiquidType = liquidType,
                LiquidAmount = amount
            };
        }

        public static TileActionConfig Object(int type, int style = 0, int alternate = 0, int random = -1, int direction = -1) {
            return new TileActionConfig {
                ClearTile = true,
                PlaceObjectData = new ObjectPlaceData {
                    Type = type,
                    Style = style,
                    Alternate = alternate,
                    Random = random,
                    Direction = direction
                }
            };
        }

        public static TileActionConfig TilePaint(int paintColor) {
            return new TileActionConfig {
                TilePaintColor = paintColor
            };
        }

        public static TileActionConfig WallPaint(int paintColor) {
            return new TileActionConfig {
                WallPaintColor = paintColor
            };
        }
    }

    public class ImageToTileConverter {
        private Dictionary<Color, TileActionConfig> colorMappings;
        private Color[,] imageData;
        private int imageWidth;
        private int imageHeight;
        private bool imageLoaded;

        public ImageToTileConverter() {
            colorMappings = new Dictionary<Color, TileActionConfig>();
            imageLoaded = false;
        }

        public bool LoadImageFromTexture(Texture2D texture) {
            if (texture == null) {
                return false;
            }
                
            imageWidth = texture.Width;
            imageHeight = texture.Height;
            imageData = new Color[imageWidth, imageHeight];
            // 获取纹理的颜色数据
            Color[] colorData = new Color[imageWidth * imageHeight];
            bool dataReady = false;

            Main.RunOnMainThread(() => {
                texture.GetData(colorData);
                dataReady = true;  // 标记数据已准备好
            });

            // 等待数据准备完成
            while (!dataReady) {
                System.Threading.Thread.Sleep(1);
            }
            // 转换为二维数组
            for (int y = 0; y < imageHeight; y++) {
                for (int x = 0; x < imageWidth; x++) {
                    imageData[x, y] = colorData[y * imageWidth + x];
                }
            }

            imageLoaded = true;
            return true;
        }


        public bool LoadImageFromTextureDirect(Texture2D texture) {
            if (texture == null) {
                return false;
            }

            imageWidth = texture.Width;
            imageHeight = texture.Height;
            imageData = new Color[imageWidth, imageHeight];
            // 获取纹理的颜色数据
            Color[] colorData = new Color[imageWidth * imageHeight];

            texture.GetData(colorData);

            for (int y = 0; y < imageHeight; y++) {
                for (int x = 0; x < imageWidth; x++) {
                    imageData[x, y] = colorData[y * imageWidth + x];
                }
            }

            imageLoaded = true;
            return true;
        }

        public void SetColorMapping(Color color, TileActionConfig config) {
            colorMappings[color] = config;
        }
        public void ClearColorMapping() {
            colorMappings.Clear();
        }

        public bool GenerateTiles(int startX, int startY, bool flipHorizontally = false, bool flipVertically = false) {
            if (!imageLoaded) {
                return false;
            }

            var processingData = new List<(int x, int y, TileActionConfig config)>();

            for (int imageX = 0; imageX < imageWidth; imageX++) {
                for (int imageY = 0; imageY < imageHeight; imageY++) {
                    int actualImageX = flipHorizontally ? imageWidth - 1 - imageX : imageX;
                    int actualImageY = flipVertically ? imageHeight - 1 - imageY : imageY;

                    Color pixelColor = imageData[actualImageX, actualImageY];

                    if (!colorMappings.ContainsKey(pixelColor)) {
                        continue;
                    }

                    TileActionConfig config = colorMappings[pixelColor];

                    int worldX = startX + imageX;
                    int worldY = startY + imageY;

                    if (!WorldGen.InWorld(worldX, worldY)) {
                        continue;
                    }

                    processingData.Add((worldX, worldY, config));
                }
            }

            // 执行预处理阶段，并过滤掉返回false的格子
            var validProcessingData = ExecutePreprocessPhase("自定义预处理", processingData);

            // 按阶段处理有效的格子
            ExecutePhase("清除墙壁", validProcessingData, (x, y, config) => { if (config.ClearWall) WorldGen.KillWall(x, y, fail: false); });
            ExecutePhase("放置墙壁", validProcessingData, (x, y, config) => { if (config.PlaceWallType > 0) WorldGen.PlaceWall(x, y, config.PlaceWallType, mute: config.Mute); });
            ExecutePhase("墙壁刷漆", validProcessingData, (x, y, config) => { if (config.WallPaintColor > 0 && config.WallPaintColor <= 31) WorldGen.paintWall(x, y, (byte)config.WallPaintColor); });
            ExecutePhase("清除物块", validProcessingData, (x, y, config) => { if (config.ClearTile) WorldGen.KillTile(x, y, fail: false, effectOnly: false, noItem: true); });
            ExecutePhase("放置物块", validProcessingData, (x, y, config) => { if (config.PlaceTileType > 0) WorldGen.PlaceTile(x, y, config.PlaceTileType, mute: config.Mute, forced: config.Forced, -1, style: config.PlaceTileStyle); });
            ExecutePhase("设置物块状态", validProcessingData, (x, y, config) => SetTileState(x, y, config));
            ExecutePhase("放置物体", validProcessingData, (x, y, config) => PlaceObjectIfNeeded(x, y, config));
            ExecutePhase("物块刷漆", validProcessingData, (x, y, config) => { if (config.TilePaintColor > 0 && config.TilePaintColor <= 31) WorldGen.paintTile(x, y, (byte)config.TilePaintColor); });
            ExecutePhase("放置液体", validProcessingData, (x, y, config) => PlaceLiquidIfNeeded(x, y, config));
            ExecutePhase("自定义后处理", validProcessingData, (x, y, config) => config.CustomPostprocess?.Invoke(x, y));

            return processingData.Count > 0;
        }

        /// <summary>
        /// 执行预处理阶段，返回通过预处理检查的格子列表
        /// </summary>
        private List<(int x, int y, TileActionConfig config)> ExecutePreprocessPhase(string phaseName, List<(int x, int y, TileActionConfig config)> processingData) {
            var validData = new List<(int x, int y, TileActionConfig config)>();

            foreach (var (x, y, config) in processingData) {
                // 如果没有预处理函数，默认通过检查
                if (config.CustomPreprocess == null) {
                    validData.Add((x, y, config));
                    continue;
                }

                // 执行预处理函数，如果返回true则添加到有效列表中
                try {
                    if (config.CustomPreprocess.Invoke(x, y)) {
                        validData.Add((x, y, config));
                    }
                    // 如果返回false，则该格子不会被添加到validData中，后续操作会被跳过
                } catch (Exception ex) {
                    // 预处理发生异常时，可以选择跳过该格子或记录错误
                    // 这里选择跳过该格子
                    continue;
                }
            }

            return validData;
        }

        private void ExecutePhase(string phaseName, List<(int x, int y, TileActionConfig config)> processingData, Action<int, int, TileActionConfig> action) {
            foreach (var (x, y, config) in processingData) {
                action(x, y, config);
            }
        }

        private void SetTileState(int x, int y, TileActionConfig config) {
            if (config.TileSlope > 0 || config.IsHalfBrick) {
                Tile tile = Main.tile[x, y];
                if (tile != null && tile.HasTile) {
                    tile.Slope = (SlopeType)config.TileSlope;
                    tile.IsHalfBlock = config.IsHalfBrick;
                }
            }
        }

        private void PlaceObjectIfNeeded(int x, int y, TileActionConfig config) {
            if (config.PlaceObjectData != null) {
                var data = config.PlaceObjectData;
                if (data.Type == TileID.Containers) {
                    WorldGen.PlaceChest(x, y, (ushort)data.Type, false, data.Style);
                } else {
                    WorldGen.PlaceObject(x, y, data.Type, config.Mute, data.Style, data.Alternate, data.Random, data.Direction);
                }
            }
        }

        private void PlaceLiquidIfNeeded(int x, int y, TileActionConfig config) {
            if (config.LiquidType >= 0 && config.LiquidAmount > 0) {
                Tile tile = Main.tile[x, y];
                tile.LiquidAmount = config.LiquidAmount;
                tile.LiquidType = config.LiquidType;
            }
        }
    }


    public static class TilePresetMappings {

        public static void TilePreset(this ImageToTileConverter converter) {
            converter.SetColorMapping(Color.White, TileActionConfig.Tile(clear: true));
        }
        public static void WallPreset(this ImageToTileConverter converter) {
            converter.SetColorMapping(Color.White, TileActionConfig.Wall(clear: true));
        }
        public static void TilePaintPreset(this ImageToTileConverter converter) {
            // 基础颜色 (1-12)
            converter.SetColorMapping(new Color(255, 0, 0), TileActionConfig.TilePaint(1));     // 红漆
            converter.SetColorMapping(new Color(255, 165, 0), TileActionConfig.TilePaint(2));   // 橙漆
            converter.SetColorMapping(new Color(255, 255, 0), TileActionConfig.TilePaint(3));   // 黄漆
            converter.SetColorMapping(new Color(173, 255, 47), TileActionConfig.TilePaint(4));  // 橙绿漆
            converter.SetColorMapping(new Color(0, 255, 0), TileActionConfig.TilePaint(5));     // 绿漆
            converter.SetColorMapping(new Color(50, 205, 50), TileActionConfig.TilePaint(6));   // 青绿漆
            converter.SetColorMapping(new Color(0, 255, 255), TileActionConfig.TilePaint(7));   // 青漆
            converter.SetColorMapping(new Color(135, 206, 235), TileActionConfig.TilePaint(8)); // 天蓝漆
            converter.SetColorMapping(new Color(0, 0, 255), TileActionConfig.TilePaint(9));     // 蓝漆
            converter.SetColorMapping(new Color(128, 0, 128), TileActionConfig.TilePaint(10));  // 紫漆
            converter.SetColorMapping(new Color(138, 43, 226), TileActionConfig.TilePaint(11)); // 蓝紫漆
            converter.SetColorMapping(new Color(255, 192, 203), TileActionConfig.TilePaint(12)); // 粉漆

            // 深色系 (13-24)
            converter.SetColorMapping(new Color(139, 0, 0), TileActionConfig.TilePaint(13));    // 深红漆
            converter.SetColorMapping(new Color(255, 140, 0), TileActionConfig.TilePaint(14));  // 深橙漆
            converter.SetColorMapping(new Color(218, 165, 32), TileActionConfig.TilePaint(15)); // 深黄漆
            converter.SetColorMapping(new Color(107, 142, 35), TileActionConfig.TilePaint(16)); // 深橙绿漆
            converter.SetColorMapping(new Color(0, 100, 0), TileActionConfig.TilePaint(17));    // 深绿漆
            converter.SetColorMapping(new Color(0, 139, 139), TileActionConfig.TilePaint(18));  // 深青绿漆
            converter.SetColorMapping(new Color(0, 206, 209), TileActionConfig.TilePaint(19));  // 深青漆
            converter.SetColorMapping(new Color(70, 130, 180), TileActionConfig.TilePaint(20)); // 深天蓝漆
            converter.SetColorMapping(new Color(0, 0, 139), TileActionConfig.TilePaint(21));    // 深蓝漆
            converter.SetColorMapping(new Color(75, 0, 130), TileActionConfig.TilePaint(22));   // 深紫漆
            converter.SetColorMapping(new Color(147, 112, 219), TileActionConfig.TilePaint(23)); // 深蓝紫漆
            converter.SetColorMapping(new Color(255, 20, 147), TileActionConfig.TilePaint(24)); // 深粉漆

            // 特殊颜色 (25-30)
            converter.SetColorMapping(new Color(0, 0, 0), TileActionConfig.TilePaint(25));      // 黑漆
            converter.SetColorMapping(new Color(255, 255, 255), TileActionConfig.TilePaint(26)); // 白漆
            converter.SetColorMapping(new Color(128, 128, 128), TileActionConfig.TilePaint(27)); // 灰漆
            converter.SetColorMapping(new Color(165, 42, 42), TileActionConfig.TilePaint(28));  // 棕漆
            converter.SetColorMapping(new Color(64, 64, 64), TileActionConfig.TilePaint(29));   // 暗影漆
            converter.SetColorMapping(new Color(128, 255, 128), TileActionConfig.TilePaint(30)); // 反色漆
        }

        public static void WallPaintPreset(this ImageToTileConverter converter) {
            // 基础颜色 (1-12)
            converter.SetColorMapping(new Color(255, 0, 0), TileActionConfig.WallPaint(1));     // 红漆
            converter.SetColorMapping(new Color(255, 165, 0), TileActionConfig.WallPaint(2));   // 橙漆
            converter.SetColorMapping(new Color(255, 255, 0), TileActionConfig.WallPaint(3));   // 黄漆
            converter.SetColorMapping(new Color(173, 255, 47), TileActionConfig.WallPaint(4));  // 橙绿漆
            converter.SetColorMapping(new Color(0, 255, 0), TileActionConfig.WallPaint(5));     // 绿漆
            converter.SetColorMapping(new Color(50, 205, 50), TileActionConfig.WallPaint(6));   // 青绿漆
            converter.SetColorMapping(new Color(0, 255, 255), TileActionConfig.WallPaint(7));   // 青漆
            converter.SetColorMapping(new Color(135, 206, 235), TileActionConfig.WallPaint(8)); // 天蓝漆
            converter.SetColorMapping(new Color(0, 0, 255), TileActionConfig.WallPaint(9));     // 蓝漆
            converter.SetColorMapping(new Color(128, 0, 128), TileActionConfig.WallPaint(10));  // 紫漆
            converter.SetColorMapping(new Color(138, 43, 226), TileActionConfig.WallPaint(11)); // 蓝紫漆
            converter.SetColorMapping(new Color(255, 192, 203), TileActionConfig.WallPaint(12)); // 粉漆

            // 深色系 (13-24)
            converter.SetColorMapping(new Color(139, 0, 0), TileActionConfig.WallPaint(13));    // 深红漆
            converter.SetColorMapping(new Color(255, 140, 0), TileActionConfig.WallPaint(14));  // 深橙漆
            converter.SetColorMapping(new Color(218, 165, 32), TileActionConfig.WallPaint(15)); // 深黄漆
            converter.SetColorMapping(new Color(107, 142, 35), TileActionConfig.WallPaint(16)); // 深橙绿漆
            converter.SetColorMapping(new Color(0, 100, 0), TileActionConfig.WallPaint(17));    // 深绿漆
            converter.SetColorMapping(new Color(0, 139, 139), TileActionConfig.WallPaint(18));  // 深青绿漆
            converter.SetColorMapping(new Color(0, 206, 209), TileActionConfig.WallPaint(19));  // 深青漆
            converter.SetColorMapping(new Color(70, 130, 180), TileActionConfig.WallPaint(20)); // 深天蓝漆
            converter.SetColorMapping(new Color(0, 0, 139), TileActionConfig.WallPaint(21));    // 深蓝漆
            converter.SetColorMapping(new Color(75, 0, 130), TileActionConfig.WallPaint(22));   // 深紫漆
            converter.SetColorMapping(new Color(147, 112, 219), TileActionConfig.WallPaint(23)); // 深蓝紫漆
            converter.SetColorMapping(new Color(255, 20, 147), TileActionConfig.WallPaint(24)); // 深粉漆

            // 特殊颜色 (25-30)
            converter.SetColorMapping(new Color(0, 0, 0), TileActionConfig.WallPaint(25));      // 黑漆
            converter.SetColorMapping(new Color(255, 255, 255), TileActionConfig.WallPaint(26)); // 白漆
            converter.SetColorMapping(new Color(128, 128, 128), TileActionConfig.WallPaint(27)); // 灰漆
            converter.SetColorMapping(new Color(165, 42, 42), TileActionConfig.WallPaint(28));  // 棕漆
            converter.SetColorMapping(new Color(64, 64, 64), TileActionConfig.WallPaint(29));   // 暗影漆
            converter.SetColorMapping(new Color(128, 255, 128), TileActionConfig.WallPaint(30)); // 反色漆
        }
    }

    public static class ImageConverterHelper {
        public static Action<int, int> RandomReplacement(int replacementTileType, int chance = 20) {
            return (x, y) => {
                if (Main.rand.Next(100) < chance) {
                    Tile tile = Main.tile[x, y];
                    if (tile != null && tile.HasTile) {
                        tile.TileType = (ushort)replacementTileType;
                    }
                }
            };
        }
        public static Action<int, int> RandomRemoval(int chance = 15) {
            return (x, y) => {
                if (Main.rand.Next(100) < chance) {
                    WorldGen.KillTile(x, y, noItem: true);
                }
            };
        }

        public static bool AddItemToChestRandomSlot(Chest chest, Item item) {
            if (chest == null) return false;

            var emptySlots = new List<int>();
            for (int i = 0; i < Chest.maxItems; i++) {
                if (chest.item[i] == null || chest.item[i].IsAir) {
                    emptySlots.Add(i);
                }
            }

            if (emptySlots.Count == 0) return false;

            int randomSlot = emptySlots[Main.rand.Next(emptySlots.Count)];
            chest.item[randomSlot] = item;

            return true;
        }

        public static bool AddItemToChest(Chest chest, Item item) {
            if (chest == null) return false;
            for (int i = 0; i < Chest.maxItems; i++) {
                if (chest.item[i] == null || chest.item[i].IsAir) {
                    chest.item[i] = item;
                    return true;
                }
            }
            return false;
        }
    }
}