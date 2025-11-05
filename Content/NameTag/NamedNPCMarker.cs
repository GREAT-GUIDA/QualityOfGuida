using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.GameContent;
using MonoMod.Cil;
namespace QualityOfGuida.Content.NameTag {
    public class SimpleNamedNPCMarker {
        public Vector2 Position;
        public int NPCType;
        public string CustomName;
        public int ExtraValue; // 金钱价值
        public float BaseValue; // 基础价值
        public bool SpawnedFromStatue; // 是否来自雕像

        public SimpleNamedNPCMarker(Vector2 position, int npcType, string customName, int extraValue = 0, float baseValue = 0f, bool spawnedFromStatue = false) {
            Position = position;
            NPCType = npcType;
            CustomName = customName;
            ExtraValue = extraValue;
            BaseValue = baseValue;
            SpawnedFromStatue = spawnedFromStatue;
        }

        public bool IsPlayerInRespawnArea() {
            Rectangle outerRect = Utils.CenteredRectangle(Position, new Vector2(120 * 16 - 32, (int)(67.5 * 16) - 32));
            for (int i = 0; i < Main.maxPlayers; i++) {
                if (Main.player[i].active && outerRect.Contains(Main.player[i].Center.ToPoint()))
                    return true;
            }
            return false;
        }

        public bool IsPlayerInNoRespawnArea() {
            Rectangle outerRect = Utils.CenteredRectangle(Position, new Vector2(1920, 1080));
            for (int i = 0; i < Main.maxPlayers; i++) {
                if (Main.player[i].active && outerRect.Contains(Main.player[i].Center.ToPoint()))
                    return true;
            }
            return false;
        }

        public void SpawnNPC() {
            // 只在服务端执行NPC生成
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            if (!SimpleNamedNPCRevengeSystem.CanRespawn(NPCType)) return;

            int respawnType = NPCID.Sets.RespawnEnemyID.ContainsKey(NPCType) ? NPCID.Sets.RespawnEnemyID[NPCType] : NPCType;
            int npcIndex = NPC.NewNPC(Entity.GetSource_NaturalSpawn(), (int)Position.X, (int)Position.Y, respawnType);

            if (npcIndex < Main.maxNPCs) {
                NPC npc = Main.npc[npcIndex];
                npc.timeLeft += 3600;
                npc.Center = Position;

                // 恢复金钱属性
                npc.extraValue = ExtraValue;
                npc.value = BaseValue;
                npc.SpawnedFromStatue = SpawnedFromStatue;

                // 在服务端设置名字和应用特效
                NPCNamingSystem.SetNPCName(npc, CustomName);

                if (Main.netMode == NetmodeID.Server) {
                    // 使用完整的状态广播，确保客户端获得所有参数
                    NameTagSyncSystem.BroadcastNPCNameUpdate(npc, CustomName);
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npcIndex);
                }
            }
        }
    }

    public class SimpleNamedNPCRevengeSystem : ModSystem {
        public List<SimpleNamedNPCMarker> markers;
        public Dictionary<int, string> trackedNPCs;
        private int updateTimer = 0;

        public override void PostSetupContent() {
            // 注册网络消息处理
            NetworkManager.RegisterHandler("CreateNPCMarker", HandleCreateMarker);
            NetworkManager.RegisterHandler("NPCRespawned", HandleNPCRespawned);

            // 钩住原版复仇系统的CacheEnemy方法
            On_CoinLossRevengeSystem.CacheEnemy += On_CoinLossRevengeSystem_CacheEnemy;

            markers = new List<SimpleNamedNPCMarker>();
            trackedNPCs = new Dictionary<int, string>();
        }

        // 拦截原版复仇系统，阻止有名字的NPC进入复仇系统
        private void On_CoinLossRevengeSystem_CacheEnemy(On_CoinLossRevengeSystem.orig_CacheEnemy orig, CoinLossRevengeSystem self, NPC npc) {
            // 如果NPC有自定义名字，不让它进入原版复仇系统
            var globalNPC = npc.GetGlobalNPC<NPCNamingSystem>();
            if (globalNPC?.HasCustomName() ?? false) {
                return; // 直接返回，不调用原方法
            }

            // 否则正常调用原版方法
            orig(self, npc);
        }

        public override void ClearWorld() {
            markers?.Clear();
            trackedNPCs?.Clear();
        }

        public void StartTracking(int whoAmI, string customName) {
            if (!trackedNPCs.ContainsKey(whoAmI))
                trackedNPCs[whoAmI] = customName;
        }

        public void StopTracking(int whoAmI) => trackedNPCs.Remove(whoAmI);

        public override void PostUpdateWorld() {
            // 只在服务端执行核心逻辑
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            updateTimer++;
            if (updateTimer == 1) RestoreTracking();
            CheckForDisappearedNPCs();
            CheckForRespawns();
        }

        private void RestoreTracking() {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && NPCNamingSystem.NPCHasCustomName(npc)) {
                    string customName = NPCNamingSystem.GetNPCName(npc);
                    if (!string.IsNullOrEmpty(customName))
                        StartTracking(npc.whoAmI, customName);
                }
            }
        }

        private void CheckForDisappearedNPCs() {
            var toRemove = new List<int>();
            foreach (var kvp in trackedNPCs) {
                int whoAmI = kvp.Key;
                if (whoAmI >= Main.maxNPCs || !Main.npc[whoAmI].active ||
                    Main.npc[whoAmI].type == 0 || !NPCNamingSystem.NPCHasCustomName(Main.npc[whoAmI])) {
                    if (whoAmI < Main.maxNPCs && Main.npc[whoAmI].type != 0 &&
                        ShouldCache(Main.npc[whoAmI].Center) && CanRespawn(Main.npc[whoAmI].type) && !Main.npc[whoAmI].townNPC) {

                        NPC npc = Main.npc[whoAmI];
                        var newMarker = new SimpleNamedNPCMarker(
                            npc.Center,
                            npc.netID,
                            kvp.Value,
                            npc.extraValue,
                            npc.value,
                            npc.SpawnedFromStatue);

                        if (!newMarker.IsPlayerInNoRespawnArea())
                            CreateMarker(npc.Center, npc.netID, kvp.Value, npc.extraValue, npc.value, npc.SpawnedFromStatue);
                    }
                    toRemove.Add(whoAmI);
                }
            }
            toRemove.ForEach(whoAmI => trackedNPCs.Remove(whoAmI));
        }

        private bool ShouldCache(Vector2 position) =>
            position.X > 100 && position.X < (Main.maxTilesX * 16 - 100) &&
            position.Y > 100 && position.Y < (Main.maxTilesY * 16 - 100);

        public static bool CanRespawn(int npcType) {
            int respawnType = NPCID.Sets.RespawnEnemyID.ContainsKey(npcType) ? NPCID.Sets.RespawnEnemyID[npcType] : npcType;
            return respawnType != 0;
        }

        private void CreateMarker(Vector2 position, int npcType, string customName, int extraValue = 0, float baseValue = 0f, bool spawnedFromStatue = false) {
            var newMarker = new SimpleNamedNPCMarker(position, npcType, customName, extraValue, baseValue, spawnedFromStatue);
            markers.Add(newMarker);

            // 在多人游戏中同步marker创建到客户端
            if (Main.netMode == NetmodeID.Server) {
                NetworkManager.SendMessage("CreateNPCMarker", writer => {
                    writer.Write(position.X);
                    writer.Write(position.Y);
                    writer.Write(npcType);
                    writer.Write(customName ?? "");
                    writer.Write(extraValue);
                    writer.Write(baseValue);
                    writer.Write(spawnedFromStatue);
                });
            }
        }

        private void CheckForRespawns() {
            for (int i = markers.Count - 1; i >= 0; i--) {
                if (markers[i].IsPlayerInRespawnArea()) {
                    var markerToRemove = markers[i];

                    // 重生NPC（这会广播NPC状态到客户端）
                    if (markers[i].IsPlayerInRespawnArea()) markerToRemove.SpawnNPC();

                    // 从服务端移除marker
                    markers.RemoveAt(i);

                    // 告诉客户端删除对应的marker
                    if (Main.netMode == NetmodeID.Server) {
                        NetworkManager.SendMessage("NPCRespawned", writer => {
                            writer.Write(markerToRemove.Position.X);
                            writer.Write(markerToRemove.Position.Y);
                            writer.Write(markerToRemove.NPCType);
                            writer.Write(markerToRemove.CustomName ?? "");
                            writer.Write(markerToRemove.ExtraValue);
                            writer.Write(markerToRemove.BaseValue);
                            writer.Write(markerToRemove.SpawnedFromStatue);
                        });
                    }
                }
            }
        }

        // 处理客户端接收marker创建
        private static void HandleCreateMarker(BinaryReader reader, int whoAmI) {
            if (Main.netMode != NetmodeID.MultiplayerClient) return;

            float posX = reader.ReadSingle();
            float posY = reader.ReadSingle();
            int npcType = reader.ReadInt32();
            string customName = reader.ReadString();
            int extraValue = reader.ReadInt32();
            float baseValue = reader.ReadSingle();
            bool spawnedFromStatue = reader.ReadBoolean();

            var system = ModContent.GetInstance<SimpleNamedNPCRevengeSystem>();
            system.markers.Add(new SimpleNamedNPCMarker(new Vector2(posX, posY), npcType, customName, extraValue, baseValue, spawnedFromStatue));
        }

        // 处理NPC重生完成，删除客户端对应的marker
        private static void HandleNPCRespawned(BinaryReader reader, int whoAmI) {
            if (Main.netMode != NetmodeID.MultiplayerClient) return;

            float posX = reader.ReadSingle();
            float posY = reader.ReadSingle();
            int npcType = reader.ReadInt32();
            string customName = reader.ReadString();
            int extraValue = reader.ReadInt32();
            float baseValue = reader.ReadSingle();
            bool spawnedFromStatue = reader.ReadBoolean();

            var system = ModContent.GetInstance<SimpleNamedNPCRevengeSystem>();
            Vector2 targetPos = new Vector2(posX, posY);

            for (int i = system.markers.Count - 1; i >= 0; i--) {
                var marker = system.markers[i];
                if (Vector2.Distance(marker.Position, targetPos) < 10f &&
                    marker.NPCType == npcType &&
                    marker.CustomName == customName) {
                    system.markers.RemoveAt(i);
                    break;
                }
            }
        }

        public override void SaveWorldData(TagCompound tag) {
            // 只在服务端或单人游戏中保存数据
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            var markerData = new List<TagCompound>();

            foreach (var marker in markers) {
                markerData.Add(new TagCompound {
                    ["posX"] = marker.Position.X,
                    ["posY"] = marker.Position.Y,
                    ["npcType"] = marker.NPCType,
                    ["customName"] = marker.CustomName,
                    ["extraValue"] = marker.ExtraValue,
                    ["baseValue"] = marker.BaseValue,
                    ["spawnedFromStatue"] = marker.SpawnedFromStatue
                });
            }

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && NPCNamingSystem.NPCHasCustomName(npc)) {
                    string customName = NPCNamingSystem.GetNPCName(npc);
                    if (!string.IsNullOrEmpty(customName) && ShouldCache(npc.Center) && CanRespawn(npc.type) && !npc.townNPC) {
                        markerData.Add(new TagCompound {
                            ["posX"] = npc.Center.X,
                            ["posY"] = npc.Center.Y,
                            ["npcType"] = npc.type,
                            ["customName"] = customName,
                            ["extraValue"] = npc.extraValue,
                            ["baseValue"] = npc.value,
                            ["spawnedFromStatue"] = npc.SpawnedFromStatue
                        });
                    }
                }
            }
            tag["markers"] = markerData;
        }

        public override void LoadWorldData(TagCompound tag) {
            markers.Clear();
            trackedNPCs.Clear();

            var markerData = tag.Get<List<TagCompound>>("markers");
            if (markerData != null) {
                foreach (var markerTag in markerData) {
                    markers.Add(new SimpleNamedNPCMarker(
                        new Vector2(markerTag.GetFloat("posX"), markerTag.GetFloat("posY")),
                        markerTag.GetInt("npcType"),
                        markerTag.GetString("customName"),
                        markerTag.GetInt("extraValue"),
                        markerTag.GetFloat("baseValue"),
                        markerTag.GetBool("spawnedFromStatue")
                    ));
                }
            }
        }
    }
}