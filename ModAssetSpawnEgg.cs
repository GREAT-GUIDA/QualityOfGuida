using GuidaSharedCode;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.ModLoader;

namespace QualityOfGuida {
    /// <summary>Dynamic SpawnEgg texture names (banner-specific filled eggs) anchored on ModAsset.SpawnEggItem_Mod.</summary>
    internal static class ModAssetSpawnEgg {
        public static string Path(string fileNameWithoutExtension) {
            string basePath = ModAsset.SpawnEggItem_Mod;
            int slash = basePath.LastIndexOf('/');
            return slash >= 0 ? basePath.Substring(0, slash + 1) + fileNameWithoutExtension : fileNameWithoutExtension;
        }

        public static bool Has(string fileNameWithoutExtension) => ModContent.HasAsset(Path(fileNameWithoutExtension));

        public static Texture2D GetTexture(string fileNameWithoutExtension) =>
            ModContent.Request<Texture2D>(Path(fileNameWithoutExtension), AssetRequestMode.ImmediateLoad).Value;
    }
}
