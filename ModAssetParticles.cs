using GuidaSharedCode;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;

namespace QualityOfGuida {
    internal static class ModAssetParticles {
        private static readonly Asset<Texture2D>[] GenericTextures = {
            ModAsset.Generic1,
            ModAsset.Generic2,
            ModAsset.Generic3,
            ModAsset.Generic4,
            ModAsset.Generic5,
            ModAsset.Generic6,
        };

        public static Texture2D RandomGeneric() => GenericTextures[Main.rand.Next(GenericTextures.Length)].Value;

        public static Texture2D Spotlight(int state) {
            return state switch {
                2 => ModAsset.Spotlight2.Value,
                _ => ModAsset.Spotlight1.Value,
            };
        }
    }
}
