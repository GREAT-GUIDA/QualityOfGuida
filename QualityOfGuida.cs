using GuidaSharedCode;
using Microsoft.Xna.Framework;
using QualityOfGuida.Content.SpawnEgg;
using QualityOfGuida.Content.Spawner;
using QualityOfGuida.Content.Torcherino;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace QualityOfGuida {
    public class QualityOfGuida : Mod {
        public static int KingBossHeadIndex { get; private set; } = -1;
        public override void Load() {
            KingBossHeadIndex = AddBossHeadTexture(ModAsset.KingBossHead_Mod);
        }

        public override void Unload() {
            NetworkManager.Clear();
            SpawnEggBannerColors.ClearCache();
            TorcherinoSystem.ClearStaticState();
        }
        public enum MessageType : byte {
            SyncMousePosition,
            SyncPlayerRange,
            Custom,
        }

        public override void HandlePacket(BinaryReader reader, int whoAmI) {
            MessageType msgType = (MessageType)reader.ReadByte();

            switch (msgType) {
                case MessageType.SyncMousePosition:
                    HandleMouseSync(reader);
                    break;
                case MessageType.SyncPlayerRange:
                    HandleRangeSync(reader);
                    break;
                case MessageType.Custom:
                    NetworkManager.HandleMessage(reader, whoAmI);
                    break;
            }
        }

        private void HandleMouseSync(BinaryReader reader) {
            int playerIndex = reader.ReadInt32();
            float mouseX = reader.ReadSingle();
            float mouseY = reader.ReadSingle();

            if (Main.netMode == NetmodeID.Server && IsValidPlayerIndex(playerIndex)) {
                var modPlayer = Main.player[playerIndex].GetModPlayer<MouseSyncPlayer>();
                modPlayer.SyncedMouseWorld = new Vector2(mouseX, mouseY);
                modPlayer.HasSyncedMouse = true;
            }
        }

        private void HandleRangeSync(BinaryReader reader) {
            int playerIndex = reader.ReadInt32();
            int rangeX = reader.ReadInt32();
            int rangeY = reader.ReadInt32();

            if (Main.netMode == NetmodeID.Server && IsValidPlayerIndex(playerIndex)) {
                var modPlayer = Main.player[playerIndex].GetModPlayer<MouseSyncPlayer>();
                modPlayer.SyncedTileRangeX = rangeX;
                modPlayer.SyncedTileRangeY = rangeY;
                modPlayer.HasSyncedRange = true;
            }
        }

        private bool IsValidPlayerIndex(int index) {
            return index >= 0 && index < Main.maxPlayers && Main.player[index]?.active == true;
        }
    }

    public static class NetworkManager {
        private static Dictionary<string, Action<BinaryReader, int>> handlers = new Dictionary<string, Action<BinaryReader, int>>();

        public static void RegisterHandler(string messageType, Action<BinaryReader, int> handler) {
            handlers[messageType] = handler;
        }

        public static void SendMessage(string messageType, Action<BinaryWriter> writeData, int toClient = -1, int ignoreClient = -1) {
            if (ModContent.GetInstance<QualityOfGuida>() is QualityOfGuida mod) {
                ModPacket packet = mod.GetPacket();
                packet.Write((byte)QualityOfGuida.MessageType.Custom);
                packet.Write(messageType);
                writeData(packet);
                packet.Send(toClient, ignoreClient);
            }
        }

        public static void HandleMessage(BinaryReader reader, int whoAmI) {
            string messageType = reader.ReadString();
            if (handlers.TryGetValue(messageType, out var handler)) {
                handler(reader, whoAmI);
            }
        }

        public static void Clear() {
            handlers.Clear();
        }
    }

    public class MouseSyncPlayer : ModPlayer {
        public Vector2 SyncedMouseWorld = Vector2.Zero;
        public bool HasSyncedMouse = false;

        public int SyncedTileRangeX = Player.tileRangeX;
        public int SyncedTileRangeY = Player.tileRangeY;
        public bool HasSyncedRange = false;

        private Vector2 lastMouseWorld = Vector2.Zero;
        private int lastTileRangeX = 0;
        private int lastTileRangeY = 0;

        private int mouseSyncCooldown = 0;
        private int rangeSyncCooldown = 0;

        private const int MOUSE_SYNC_INTERVAL = 2;
        private const int RANGE_SYNC_INTERVAL = 30;

        public Vector2 GetMouseWorld() {
            if (Main.netMode == NetmodeID.Server && HasSyncedMouse) {
                return SyncedMouseWorld;
            }
            return Main.MouseWorld;
        }

        public int GetTileRangeX() {
            if (Main.netMode == NetmodeID.Server && HasSyncedRange) {
                return SyncedTileRangeX;
            }
            return Player.tileRangeX;
        }

        public int GetTileRangeY() {
            if (Main.netMode == NetmodeID.Server && HasSyncedRange) {
                return SyncedTileRangeY;
            }
            return Player.tileRangeY;
        }

        public override void PostUpdate() {
            if (Main.netMode == NetmodeID.MultiplayerClient && ShouldSyncPlayerState() && Player.whoAmI == Main.myPlayer) {
                SyncMouse();
                SyncRange();
            }
        }

        private void SyncMouse() {
            mouseSyncCooldown--;
            if (mouseSyncCooldown <= 0) {
                Vector2 currentMouse = Main.MouseWorld;

                if (Vector2.DistanceSquared(currentMouse, lastMouseWorld) > 16f) {
                    SendMouseSync();
                    lastMouseWorld = currentMouse;
                }

                mouseSyncCooldown = MOUSE_SYNC_INTERVAL;
            }
        }

        private void SyncRange() {
            rangeSyncCooldown--;
            if (rangeSyncCooldown <= 0 || Player.tileRangeX != lastTileRangeX || Player.tileRangeY != lastTileRangeY) {
                SendRangeSync();
                lastTileRangeX = Player.tileRangeX;
                lastTileRangeY = Player.tileRangeY;

                rangeSyncCooldown = RANGE_SYNC_INTERVAL;
            }
        }

        private bool ShouldSyncPlayerState() {
            var heldItem = Player.HeldItem;
            return heldItem?.ModItem is SpawnEggItem;
        }

        private void SendMouseSync() {
            ModPacket packet = Mod.GetPacket();
            packet.Write((byte)QualityOfGuida.MessageType.SyncMousePosition);
            packet.Write(Player.whoAmI);
            packet.Write(Main.MouseWorld.X);
            packet.Write(Main.MouseWorld.Y);
            packet.Send();
        }

        private void SendRangeSync() {
            ModPacket packet = Mod.GetPacket();
            packet.Write((byte)QualityOfGuida.MessageType.SyncPlayerRange);
            packet.Write(Player.whoAmI);
            packet.Write(Player.tileRangeX);
            packet.Write(Player.tileRangeY);
            packet.Send();
        }

        public void ClearSyncedData() {
            HasSyncedMouse = false;
            HasSyncedRange = false;
            SyncedMouseWorld = Vector2.Zero;
            SyncedTileRangeX = Player.tileRangeX;
            SyncedTileRangeY = Player.tileRangeY;
        }
    }
}
