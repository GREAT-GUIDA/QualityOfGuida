using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;
using Terraria.DataStructures;
using Terraria.Audio;
using QualityOfGuida.Content.SpawnEgg;
using Terraria.GameContent.ObjectInteractions;
using System.Collections.Generic;

namespace QualityOfGuida.Content.Spawner {
    public class SpawnerItem : ModItem {
        public override bool IsLoadingEnabled(Mod mod) {
            return ModContent.GetInstance<ItemToggleConfig>().EnableSpawner;
        }
        public override void SetDefaults() {
            Item.DefaultToPlaceableTile(ModContent.TileType<SpawnerTile>());
            Item.width = 32;
            Item.height = 32;
            Item.value = Item.buyPrice(gold: 1);
            Item.rare = ItemRarityID.Blue;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Terrarium, 1)
                .AddIngredient(ItemID.ChlorophyteBar, 6)
                .AddIngredient(ItemID.Ectoplasm, 3)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }
}