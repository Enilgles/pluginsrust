using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("NPCGifts", "Mabel", "1.0.2")]
    class NPCGifts : RustPlugin
    {
        private class ContainerData
        {
            public string Prefab { get; set; }
            public float SpawnChance { get; set; }
            public bool IsEnabled { get; set; }
            public string Permission { get; set; }
        }

        private class CooldownData
        {
            public ulong PlayerID { get; set; }
            public DateTime LastSpawnTime { get; set; }
        }

        private List<ContainerData> containerList;
        private Dictionary<ulong, CooldownData> cooldowns = new Dictionary<ulong, CooldownData>();
        private List<BaseEntity> spawnedContainers = new List<BaseEntity>();
        private float cooldownDurationMinutes = 60;
        private System.Random random;

        private string containerReceivedMessage;

        protected override void LoadDefaultConfig()
        {
            Config["Containers"] = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["Prefab"] = "assets/prefabs/misc/xmas/sleigh/presentdrop.prefab",
                    ["SpawnChance"] = 0.5f,
                    ["IsEnabled"] = true,
                    ["Permission"] = "npcgifts.example1"
                },
                new Dictionary<string, object>
                {
                    ["Prefab"] = "assets/prefabs/missions/portal/proceduraldungeon/xmastunnels/loot/xmastunnellootbox.prefab",
                    ["SpawnChance"] = 0.5f,
                    ["IsEnabled"] = true,
                    ["Permission"] = "npcgifts.example2"
                },
                new Dictionary<string, object>
                {
                    ["Prefab"] = "assets/prefabs/misc/xmas/giftbox/giftbox_loot.prefab",
                    ["SpawnChance"] = 0.5f,
                    ["IsEnabled"] = true,
                    ["Permission"] = "npcgifts.example3"
                }
            };
            Config["CooldownDurationMinutes"] = cooldownDurationMinutes;

            containerReceivedMessage = Config.Get("ContainerReceivedMessage", " :santahat: Merry Christmas <color=#4A95CC>{player.name}</color> from <color=#4A95CC>{server.name}</color> :santahat:") as string;
            Config["ContainerReceivedMessage"] = containerReceivedMessage;

            SaveConfig();
        }

        void Init()
        {
            LoadConfigValues();
            cooldownDurationMinutes = Convert.ToSingle(Config["CooldownDurationMinutes"]);
            random = new System.Random();

            CheckAndCreatePermissions();

            timer.Once(300f, () => CleanupContainers());
        }

        private void CheckAndCreatePermissions()
        {
            foreach (var containerData in containerList)
            {
                if (!string.IsNullOrEmpty(containerData.Permission))
                {
                    if (!permission.PermissionExists(containerData.Permission, this))
                    {
                        permission.RegisterPermission(containerData.Permission, this);
                    }
                }
            }
        }

        void LoadConfigValues()
        {
            object configObject = Config["Containers"];
            if (configObject is List<object>)
            {
                containerList = (configObject as List<object>).Select(x =>
                {
                    var containerData = new ContainerData
                    {
                        Prefab = Convert.ToString(((Dictionary<string, object>)x).GetValueOrDefault("Prefab", string.Empty)),
                        SpawnChance = Convert.ToSingle(((Dictionary<string, object>)x).GetValueOrDefault("SpawnChance", 0.0f)),
                        IsEnabled = Convert.ToBoolean(((Dictionary<string, object>)x).GetValueOrDefault("IsEnabled", false)),
                        Permission = Convert.ToString(((Dictionary<string, object>)x).GetValueOrDefault("Permission", string.Empty))
                    };

                    return containerData;
                }).ToList();

                if (containerList.Any(c => string.IsNullOrEmpty(c.Permission)))
                {
                    foreach (var containerData in containerList)
                    {
                        if (string.IsNullOrEmpty(containerData.Permission))
                        {
                            containerData.Permission = $"npcgifts.example{containerList.IndexOf(containerData) + 1}";
                        }
                    }

                    Config["Containers"] = containerList.Cast<object>().ToList();
                    SaveConfig();
                    Puts("Added missing permissions to config.");
                }
            }
            else
            {
                PrintWarning("Failed to load the configuration file. Using default values.");
                LoadDefaultConfig();
                SaveConfig();
                LoadConfigValues();
            }

            containerReceivedMessage = Config.Get("ContainerReceivedMessage") as string;
            if (containerReceivedMessage == null)
            {
                containerReceivedMessage = " :santahat: Merry Christmas <color=#4A95CC>{player.name}</color> from <color=#4A95CC>{server.name}</color> :santahat:";
                Config["ContainerReceivedMessage"] = containerReceivedMessage;
                SaveConfig();
            }	
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null || hitInfo == null) return;

            BaseEntity killer = hitInfo.Initiator as BaseEntity;
            BasePlayer player = killer as BasePlayer;

            if (player != null && entity.GetComponent<HumanNPC>() != null)
            {
                if (!HasCooldown(player.userID))
                {
                    SpawnRandomContainer(entity.transform.position, player.userID);
                    SetCooldown(player.userID);
                }
            }
        }

        private void SpawnRandomContainer(Vector3 position, ulong playerID)
        {
            if (containerList == null || containerList.Count == 0) return;

            random = new System.Random();

            foreach (var containerData in containerList.Where(c => c.IsEnabled))
            {
                if (random.NextDouble() <= containerData.SpawnChance)
                {
                    if (string.IsNullOrEmpty(containerData.Permission) || permission.UserHasPermission(playerID.ToString(), containerData.Permission))
                    {
                        BaseEntity container = GameManager.server.CreateEntity(containerData.Prefab, position);

                        if (container != null)
                        {
                            container.Spawn();
                            spawnedContainers.Add(container);
                            BasePlayer player = BasePlayer.FindByID(playerID);
                            if (player != null)
                            {
                                SendChatMessage(player, ReplacePlaceholders(containerReceivedMessage, player));
                            }
                        }
                    }

                    break;
                }
            }
        }

        private void SendChatMessage(BasePlayer player, string message)
        {
            if (player == null || string.IsNullOrEmpty(message)) return;
            player.ChatMessage(message);
        }

        private bool HasCooldown(ulong playerID)
        {
            if (cooldowns.ContainsKey(playerID))
            {
                var cooldownData = cooldowns[playerID];
                var elapsedTime = DateTime.Now - cooldownData.LastSpawnTime;
                return elapsedTime.TotalMinutes < cooldownDurationMinutes;
            }
            return false;
        }

        private void SetCooldown(ulong playerID)
        {
            if (cooldowns.ContainsKey(playerID))
            {
                cooldowns[playerID].LastSpawnTime = DateTime.Now;
            }
            else
            {
                cooldowns[playerID] = new CooldownData
                {
                    PlayerID = playerID,
                    LastSpawnTime = DateTime.Now
                };
            }
        }

        private string ReplacePlaceholders(string message, BasePlayer player)
        {
            if (player == null || string.IsNullOrEmpty(message)) return message;

            string playerName = player.displayName;
            string serverName = ConVar.Server.hostname;

            return message.Replace("{player.name}", playerName).Replace("{server.name}", serverName);
        }

        private void CleanupContainers()
        {
            foreach (var container in spawnedContainers)
            {
                if (container != null && !container.IsDestroyed)
                {
                    container.Kill();
                }
            }
        }

        void Unload()
        {
            CleanupContainers();
        }
    }
}