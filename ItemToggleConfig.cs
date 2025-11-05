using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ModLoader.Config;

namespace QualityOfGuida
{
    public class ItemToggleConfig : ModConfig {
        public override ConfigScope Mode => ConfigScope.ServerSide;

        [DefaultValue(true)]
        [ReloadRequired]
        public bool EnablePaper { get; set; }

        [DefaultValue(true)]
        [ReloadRequired]
        public bool EnableFlint { get; set; }

        [DefaultValue(true)]
        [ReloadRequired]
        public bool EnableNameTag { get; set; }

        [DefaultValue(true)]
        [ReloadRequired]
        public bool EnableBoneMeal { get; set; }

        [DefaultValue(true)]
        [ReloadRequired]
        public bool EnableSpawnEgg { get; set; }

        [DefaultValue(true)]
        [ReloadRequired]
        public bool EnableTorcherino { get; set; }

        [DefaultValue(true)]
        [ReloadRequired]
        public bool EnableSpawner { get; set; }
    }
}
