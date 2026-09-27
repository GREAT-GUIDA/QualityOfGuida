using GuidaSharedCode;
using Terraria.Audio;

namespace QualityOfGuida {
    internal static class QoGSound {
        public static SoundStyle PaperOpen => new(ModAsset.PaperOpen_Mod) { Volume = 0.6f, PitchVariance = 0.5f, MaxInstances = 3 };
        public static SoundStyle PaperClose => new(ModAsset.PaperClose_Mod) { Volume = 0.6f, PitchVariance = 0.5f, MaxInstances = 3 };
        public static SoundStyle PaperWrite => new(ModAsset.PaperWrite_Mod) { Volume = 1f, PitchVariance = 0.5f, MaxInstances = 3 };
        public static SoundStyle PortalArrive => new(ModAsset.PortalArrive_Mod) { Volume = 1f, PitchVariance = 0.1f, MaxInstances = 3 };
        public static SoundStyle PortalAmbience => new(ModAsset.PortalAmbience_Mod) { Volume = 1f, PitchVariance = 0.1f, MaxInstances = 10 };
        public static SoundStyle EggCrack => new(ModAsset.EggCrack_Mod) { Volume = 0.8f, PitchVariance = 0.5f, MaxInstances = 10 };
    }
}
