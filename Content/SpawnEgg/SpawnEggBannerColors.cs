using GuidaSharedCode;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace QualityOfGuida.Content.SpawnEgg {
    public static class SpawnEggBannerColors {
        private static readonly Dictionary<int, ColorPair> BannerColorCache = new();

        public struct ColorPair {
            public Color Primary;
            public Color Secondary;
            public bool HasSecondary;

            public ColorPair(Color primary, Color secondary = default, bool hasSecondary = false) {
                Primary = primary;
                Secondary = secondary;
                HasSecondary = hasSecondary;
            }
        }

        public static ColorPair GetColorPair(int bannerID) {
            if (!BannerColorCache.TryGetValue(bannerID, out ColorPair colorPair)) {
                colorPair = ExtractThemeColorsFromBanner(bannerID);
            }

            return colorPair;
        }

        public static void ClearCache() {
            BannerColorCache.Clear();
        }

        private static ColorPair ExtractThemeColorsFromBanner(int bannerID) {
            int itemID = Item.BannerToItem(bannerID);

            Main.instance.LoadItem(itemID);
            Texture2D bannerTexture = TextureAssets.Item[itemID].Value;

            Color[] pixels = new Color[bannerTexture.Width * bannerTexture.Height];
            bannerTexture.GetData(pixels);

            List<Color> validPixels = new();
            for (int y = 0; y < bannerTexture.Height; y += 2) {
                for (int x = 0; x < bannerTexture.Width; x += 2) {
                    Color pixel = pixels[y * bannerTexture.Width + x];
                    if (pixel.A > 0) {
                        validPixels.Add(pixel);
                    }
                }
            }

            ColorPair colorPair = PerformColorClustering(validPixels);
            BannerColorCache[bannerID] = colorPair;
            return colorPair;
        }

        private static ColorPair PerformColorClustering(List<Color> pixels) {
            if (pixels.Count == 0) {
                throw new InvalidOperationException("No valid pixels found for banner color extraction.");
            }

            if (pixels.Count == 1) {
                return new ColorPair(pixels[0]);
            }

            int clusterCount = Math.Min(4, pixels.Count);
            Random random = new(pixels.Count);

            List<Vector3> centers = Enumerable.Range(0, clusterCount)
                .Select(_ => {
                    Color pixel = pixels[random.Next(pixels.Count)];
                    return new Vector3(pixel.R, pixel.G, pixel.B);
                })
                .ToList();

            List<List<Color>> clusters = new();

            for (int iteration = 0; iteration < 10; iteration++) {
                clusters.Clear();
                for (int i = 0; i < clusterCount; i++) {
                    clusters.Add(new List<Color>());
                }

                foreach (Color pixel in pixels) {
                    Vector3 pixelVec = new(pixel.R, pixel.G, pixel.B);
                    int closest = 0;
                    float minDistance = Vector3.DistanceSquared(pixelVec, centers[0]);

                    for (int i = 1; i < centers.Count; i++) {
                        float distance = Vector3.DistanceSquared(pixelVec, centers[i]);
                        if (distance < minDistance) {
                            minDistance = distance;
                            closest = i;
                        }
                    }

                    clusters[closest].Add(pixel);
                }

                bool converged = true;
                for (int i = 0; i < centers.Count; i++) {
                    if (clusters[i].Count <= 0) {
                        continue;
                    }

                    float avgR = (float)clusters[i].Average(p => (double)p.R);
                    float avgG = (float)clusters[i].Average(p => (double)p.G);
                    float avgB = (float)clusters[i].Average(p => (double)p.B);
                    Vector3 newCenter = new(avgR, avgG, avgB);

                    if (Vector3.DistanceSquared(centers[i], newCenter) > 1f) {
                        converged = false;
                    }

                    centers[i] = newCenter;
                }

                if (converged) {
                    break;
                }
            }

            List<ClusterInfo> validClusters = new();
            for (int i = 0; i < clusters.Count; i++) {
                if (clusters[i].Count > 0) {
                    validClusters.Add(new ClusterInfo {
                        Center = centers[i],
                        Count = clusters[i].Count,
                        Brightness = 0.299f * centers[i].X + 0.587f * centers[i].Y + 0.114f * centers[i].Z,
                        Color = VectorToColor(centers[i])
                    });
                }
            }

            validClusters = validClusters.OrderByDescending(c => c.Count).ToList();

            if (validClusters.Count <= 1) {
                return validClusters.Count == 1 ? new ColorPair(validClusters[0].Color) : new ColorPair(Color.White);
            }

            if (validClusters.Count > 2) {
                ClusterInfo darkest = validClusters.OrderBy(c => c.Brightness).First();
                validClusters.Remove(darkest);
            }

            if (validClusters.Count > 2) {
                ClusterInfo least = validClusters.OrderBy(c => c.Count).First();
                validClusters.Remove(least);
            }

            if (validClusters.Count == 0) {
                return new ColorPair(Color.White);
            }

            if (validClusters.Count == 1) {
                return new ColorPair(validClusters[0].Color);
            }

            ClusterInfo color1 = validClusters[0];
            ClusterInfo color2 = validClusters[1];
            float colorDistance = Vector3.Distance(color1.Center, color2.Center);
            float brightnessDiff = Math.Abs(color1.Brightness - color2.Brightness);

            if (colorDistance < 50f && brightnessDiff < 30f) {
                int totalWeight = color1.Count + color2.Count;
                Vector3 weightedSum = (color1.Center * color1.Count + color2.Center * color2.Count) / totalWeight;
                return new ColorPair(VectorToColor(weightedSum));
            }

            if (brightnessDiff <= 80f) {
                ClusterInfo brighter = color1.Brightness > color2.Brightness ? color1 : color2;
                ClusterInfo darker = color1.Brightness <= color2.Brightness ? color1 : color2;
                Vector3 enhancedBrighter = EnhanceBrightness(brighter.Center, 1.2f);
                Vector3 enhancedDarker = EnhanceBrightness(darker.Center, 0.8f);
                return new ColorPair(VectorToColor(enhancedBrighter), VectorToColor(enhancedDarker), true);
            }

            return new ColorPair(color1.Color, color2.Color, true);
        }

        private static Vector3 EnhanceBrightness(Vector3 color, float factor) {
            Color rgbColor = VectorToColor(color);
            Vector3 rgb = rgbColor.ToVector3();
            float lightness = (Math.Max(rgb.X, Math.Max(rgb.Y, rgb.Z)) + Math.Min(rgb.X, Math.Min(rgb.Y, rgb.Z))) / 2f;
            float lOffset = lightness * (factor - 1f);
            Color newColor = rgbColor.OffsetHSL(0f, 0f, lOffset);
            return new Vector3(newColor.R, newColor.G, newColor.B);
        }

        private sealed class ClusterInfo {
            public Vector3 Center { get; set; }
            public int Count { get; set; }
            public float Brightness { get; set; }
            public Color Color { get; set; }
        }

        private static Color VectorToColor(Vector3 vector) => new(
            (int)MathHelper.Clamp(vector.X, 0, 255),
            (int)MathHelper.Clamp(vector.Y, 0, 255),
            (int)MathHelper.Clamp(vector.Z, 0, 255));
    }
}
