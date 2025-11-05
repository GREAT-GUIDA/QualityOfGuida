using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Reflection;
using Terraria.Audio;
using Terraria.ModLoader;

namespace QualityOfGuida {
    // 自定义属性
    [AttributeUsage(AttributeTargets.Field)]
    public class AssetAttribute : Attribute {
        public string Path { get; }
        public AssetAttribute(string path) {
            Path = path;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class SoundAssetAttribute : AssetAttribute {
        public float Volume { get; set; } = 1f;
        public float PitchVariance { get; set; } = 0f;
        public int MaxInstances { get; set; } = 1;

        public SoundAssetAttribute(string path) : base(path) { }
    }

    public static class ModAssets {
        public static string AssetDir = $"{nameof(QualityOfGuida)}/Assets";
        public static string ContentDir = $"{nameof(QualityOfGuida)}/Content";
        public static string EmptyTextureDir = $"{nameof(QualityOfGuida)}/Assets/Texture/Empty";

        // 音效
        [SoundAsset("PaperOpen", Volume = 0.6f, PitchVariance = 0.5f, MaxInstances = 3)]
        public static SoundStyle PaperOpen;

        [SoundAsset("PaperClose", Volume = 0.6f, PitchVariance = 0.5f, MaxInstances = 3)]
        public static SoundStyle PaperClose;

        [SoundAsset("PaperWrite", Volume = 1f, PitchVariance = 0.5f, MaxInstances = 3)]
        public static SoundStyle PaperWrite;

        [SoundAsset("PortalArrive", Volume = 1f, PitchVariance = 0.1f, MaxInstances = 3)]
        public static SoundStyle PortalArrive;

        [SoundAsset("PortalAmbience", Volume = 1f, PitchVariance = 0.1f, MaxInstances = 10)]
        public static SoundStyle PortalAmbience;

        [SoundAsset("EggCrack", Volume = 0.8f, PitchVariance = 0.5f, MaxInstances = 10)]
        public static SoundStyle EggCrack;

        internal static void Load(Mod mod) {
            LoadAssetsByAttributes();
        }

        private static void LoadAssetsByAttributes() {
            var type = typeof(ModAssets);
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);

            foreach (var field in fields) {
                // 处理普通资源
                var assetAttr = field.GetCustomAttribute<AssetAttribute>();
                if (assetAttr != null && !(assetAttr is SoundAssetAttribute)) {
                    var fullPath = $"{AssetDir}/{assetAttr.Path}";

                    if (field.FieldType == typeof(Effect)) {
                        field.SetValue(null, LoadAsset<Effect>(fullPath));
                    } else if (field.FieldType == typeof(Texture2D)) {
                        field.SetValue(null, LoadAsset<Texture2D>(fullPath));
                    }
                }

                // 处理音效资源
                var soundAttr = field.GetCustomAttribute<SoundAssetAttribute>();
                if (soundAttr != null) {
                    var fullPath = $"{AssetDir}/Sounds/{soundAttr.Path}";
                    var soundStyle = new SoundStyle(fullPath) {
                        Volume = soundAttr.Volume,
                        PitchVariance = soundAttr.PitchVariance,
                        MaxInstances = soundAttr.MaxInstances
                    };
                    field.SetValue(null, soundStyle);
                }
            }
        }

        private static T LoadAsset<T>(string path) where T : class {
            return ModContent.Request<T>(path, AssetRequestMode.ImmediateLoad).Value;
        }

        internal static void Unload() {
        }
    }
}
