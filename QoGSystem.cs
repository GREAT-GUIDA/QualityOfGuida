using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.ModLoader;

namespace QualityOfGuida
{
    class QoGSystem : ModSystem {
        public override void PostDrawInterface(SpriteBatch spriteBatch) {

            if (Main.screenTarget.RenderTargetUsage != RenderTargetUsage.PreserveContents) {
                //NewScreenTarget();
            }
        }

        public static void NewScreenTarget() {
            GraphicsDevice device = Main.graphics.GraphicsDevice;
            int width = Main.screenTarget.Width;
            int height = Main.screenTarget.Height;

            Main.screenTarget.Dispose();
            Main.screenTarget = new RenderTarget2D(device, width, height, false,
                device.PresentationParameters.BackBufferFormat,
                DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        }

    }
}
