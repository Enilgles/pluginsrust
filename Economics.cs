//#define DEBUG

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using UnityEngine;

// TODO: Add SQLite and MySQL database support

namespace Oxide.Plugins
{
    [Info("Economics", "Enilgles", "1.0.0")]
    [Description("Basic economics system and economy API")]
    public class Economics : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        private sealed class Configuration
        {
            [JsonProperty("Allow negative balance for accounts")]
            public bool AllowNegativeBalance;

            [JsonProperty("Balance limit for accounts (0 to disable)")]
            public int BalanceLimit;

            [JsonProperty("Maximum balance for accounts (0 to disable)")] // TODO: From version 3.8.6; remove eventually
            private int BalanceLimitOld { set => BalanceLimit = value; }

            [JsonProperty("Negative balance limit for accounts (0 to disable)")]
            public int NegativeBalanceLimit;

            [JsonProperty("Remove unused accounts")]
            public bool RemoveUnused = true;

            [JsonProperty("Log transactions to file")]
            public bool LogTransactions;

            [JsonProperty("Starting account balance (0 or higher)")]
            public int StartingBalance = 1000;

            [JsonProperty("Starting money amount (0 or higher)")] // TODO: From version 3.8.6; remove eventually
            private int StartingBalanceOld { set => StartingBalance = value; }

            [JsonProperty("Wipe balances on new save file")]
            public bool WipeOnNewSave;

            [JsonProperty("Show balance in HUD")]
            public bool ShowBalanceHUD = true;

            [JsonProperty("HUD text color (RGBA format)")]
            public string HUDTextColor = "1 1 1 1";

            [JsonProperty("HUD background color (RGBA format)")]
            public string HUDBackgroundColor = "0 0 0 0.5";

            [JsonProperty("Currency name")]
            public string CurrencyName = "Coins";

            [JsonProperty("Настройка получение валюты за убийство игроков")]
            public CurrencySettings PlayerKillSettings = new()
            {
                Permission = "",
                Enabled = true,
                Rewards = new RewardSettings
                {
                    Chance = 35,
                    Amount = 10
                }
            };

            [JsonProperty("Настройка получение валюты за убийство животных")]
            public CurrencySettings AnimalKillSettings = new()
            {
                Permission = "",
                Enabled = true,
                Rewards = new RewardSettings
                {
                    Chance = 53,
                    Amount = 2
                }
            };

            [JsonProperty("Настройка получение валюты за убийство NPC")]
            public CurrencySettings NPCKillSettings = new()
            {
                Permission = "",
                Enabled = true,
                Rewards = new RewardSettings
                {
                    Chance = 10,
                    Amount = 15
                }
            };

            [JsonProperty("Настройка получение валюты за уничтожение танка")]
            public CurrencySettings BradleyKillSettings = new()
            {
                Permission = "",
                Enabled = true,
                Rewards = new RewardSettings
                {
                    Chance = 44,
                    Amount = 100
                }
            };

            [JsonProperty("Настройка получение валюты за уничтожение вертолета")]
            public CurrencySettings HelicopterKillSettings = new()
            {
                Permission = "",
                Enabled = true,
                Rewards = new RewardSettings
                {
                    Chance = 80,
                    Amount = 200
                }
            };

            [JsonProperty("Настройка получение валюты за уничтожение бочек")]
            public CurrencySettings BarrelDestroySettings = new()
            {
                Permission = "",
                Enabled = true,
                Rewards = new RewardSettings
                {
                    Chance = 23,
                    Amount = 5
                }
            };

            [JsonProperty("Настройка получение валюты за добычу ресурсов")]
            public ResourceCurrencySettings ResourceGatherSettings = new()
            {
                Permission = "",
                Enabled = true,
                ResourceRewards = new Dictionary<string, RewardSettings>
                {
                    ["sulfur.ore"] = new RewardSettings
                    {
                        Chance = 10,
                        Amount = 10
                    },
                    ["stones"] = new RewardSettings
                    {
                        Chance = 20,
                        Amount = 1
                    }
                }
            };

            [JsonProperty("Настройка получение валюты за поднятие ресурсов с земли (грибы, ягоды, дерево и т.д)")]
            public ResourceCurrencySettings ResourcePickupSettings = new()
            {
                Permission = "",
                Enabled = true,
                ResourceRewards = new Dictionary<string, RewardSettings>
                {
                    ["sulfur.ore"] = new RewardSettings
                    {
                        Chance = 10,
                        Amount = 10
                    },
                    ["stones"] = new RewardSettings
                    {
                        Chance = 20,
                        Amount = 3
                    }
                }
            };

            [JsonProperty("Настройка получение валюты за проведенное время на сервере")]
            public TimeCurrencySettings TimeOnServerSettings = new()
            {
                Permission = "",
                Enabled = true,
                TimeRequired = 3600,
                Amount = 10
            };

            [JsonProperty("IQSphereEvent : Настройка получение валюты за убийство NPC (Under - под сферой, Around - вокруг сферы, Tier1 - 1 этаж сферы, Tier2 - 2 этаж сферы, Tier3 - 3 этаж сферы)")]
            public IQSphereSettings SphereEventSettings = new()
            {
                Under = new CurrencySettings
                {
                    Permission = "",
                    Enabled = true,
                    Rewards = new RewardSettings
                    {
                        Chance = 50,
                        Amount = 5
                    }
                },
                Around = new CurrencySettings
                {
                    Permission = "",
                    Enabled = true,
                    Rewards = new RewardSettings
                    {
                        Chance = 30,
                        Amount = 5
                    }
                },
                Tier1 = new CurrencySettings
                {
                    Permission = "",
                    Enabled = true,
                    Rewards = new RewardSettings
                    {
                        Chance = 30,
                        Amount = 10
                    }
                },
                Tier2 = new CurrencySettings
                {
                    Permission = "",
                    Enabled = true,
                    Rewards = new RewardSettings
                    {
                        Chance = 45,
                        Amount = 12
                    }
                },
                Tier3 = new CurrencySettings
                {
                    Permission = "",
                    Enabled = true,
                    Rewards = new RewardSettings
                    {
                        Chance = 70,
                        Amount = 15
                    }
                }
            };

            public string ToJson()
            {
                return JsonConvert.SerializeObject(this);
            }

            public Dictionary<string, object> ToDictionary()
            {
                return JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
            }
        }

        [Serializable]
        public class CurrencySettings
        {
            [JsonProperty("Права для использования данной возможности (Если вам требуется сделать ее доступной всем по стандарту - оставьте поле пустым)")]
            public string Permission = "";

            [JsonProperty("Использовать эту возможность получения валюты")]
            public bool Enabled = true;

            [JsonProperty("Настройка получения валюты")]
            public RewardSettings Rewards = new();
        }

        [Serializable]
        public class RewardSettings
        {
            [JsonProperty("Шанс получить валюту")]
            public int Chance = 100;

            [JsonProperty("Сколько выдавать валюты")]
            public int Amount = 10;
        }

        [Serializable]
        public class ResourceCurrencySettings
        {
            [JsonProperty("Права для использования данной возможности (Если вам требуется сделать ее доступной всем по стандарту - оставьте поле пустым)")]
            public string Permission = "";

            [JsonProperty("Использовать эту возможность получения валюты")]
            public bool Enabled = true;

            [JsonProperty("Сколько начислять валюты за ресурсы ( [за какой ресурс давать] = { остальная настройка }")]
            public Dictionary<string, RewardSettings> ResourceRewards = new();
        }

        [Serializable]
        public class TimeCurrencySettings
        {
            [JsonProperty("Права для использования данной возможности (Если вам требуется сделать ее доступной всем по стандарту - оставьте поле пустым)")]
            public string Permission = "";

            [JsonProperty("Использовать эту возможность получения валюты")]
            public bool Enabled = true;

            [JsonProperty("Сколько нужно провести времени,чтобы выдали награду")]
            public int TimeRequired = 3600;

            [JsonProperty("Сколько начислять валюты за проведенное время на сервере")]
            public int Amount = 10;
        }

        [Serializable]
        public class IQSphereSettings
        {
            [JsonProperty("Under")]
            public CurrencySettings Under = new();

            [JsonProperty("Around")]
            public CurrencySettings Around = new();

            [JsonProperty("Tier1")]
            public CurrencySettings Tier1 = new();

            [JsonProperty("Tier2")]
            public CurrencySettings Tier2 = new();

            [JsonProperty("Tier3")]
            public CurrencySettings Tier3 = new();
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            LogWarning($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion Configuration

        #region Stored Data

        private DynamicConfigFile data;
        private StoredData storedData;
        private bool changed;

        private sealed class StoredData
        {
            public readonly Dictionary<string, double> Balances = new();
        }

        private void SaveData()
        {
            if (changed)
            {
                Puts("Saving balances for players...");
                Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
            }
        }

        private void OnServerSave()
        {
            SaveData();
        }

        private void Unload()
        {
            SaveData();

            // Destroy all UI elements for all players
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player?.IsConnected != true)
                {
                    continue;
                }

                // Force destroy all UI elements with this plugin's prefix
                DestroyUI(player);
                _ = CuiHelper.DestroyUi(player, HUD_PANEL_NAME); // Direct call as a fallback
            }
        }

        #endregion Stored Data

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "У вас нет прав на использование команды {0}",
                ["UsageBalance"] = "Использование: {0} <игрок>",
                ["UsageBalanceOthers"] = "Использование: {0} <игрок>",
                ["UsageDeposit"] = "Использование: {0} <игрок> <сумма>",
                ["UsageSetBalance"] = "Использование: {0} <игрок> <сумма>",
                ["UsageTransfer"] = "Использование: {0} <игрок> <сумма>",
                ["UsageWithdraw"] = "Использование: {0} <игрок> <сумма>",
                ["UsageWipe"] = "Использование: {0}",
                ["ZeroAmount"] = "Сумма должна быть больше 0",
                ["NegativeBalance"] = "Баланс не может быть отрицательным",
                ["PlayerBalance"] = "Баланс игрока {0}: {1} {2}",
                ["YourBalance"] = "Ваш баланс: {0} {1}",
                ["DepositedToAll"] = "Внесено {0} {1} на счета {2} игроков",
                ["SetBalanceForAll"] = "Установлен баланс {0} {1} для {2} игроков",
                ["TransferedToAll"] = "Переведено {0} {1} на счета {2} игроков",
                ["WithdrawnForAll"] = "Снято {0} {1} со счета {2} игрока",
                ["TransactionFailed"] = "Не удалось выполнить транзакцию для игрока {0}",
                ["YouLackMoney"] = "У вас недостаточно средств",
                ["TransferToSelf"] = "Вы не можете перевести деньги самому себе",
                ["TransferredTo"] = "Переведено {0} {1} игроку {2}",
                ["ReceivedFrom"] = "Вы получили {0} {1} от игрока {2}",
                ["DataWiped"] = "Все данные экономики были удалены",
                ["PlayersFound"] = "Найдено несколько игроков: {0}",
                ["NoPlayersFound"] = "Игрок {0} не найден",
                ["YouReceivedMoney"] = "Вы получили: {0} монет",
                ["YouReceivedMoneyForKill"] = "Вы получили: {0} монет за убийство игрока",
                ["YouReceivedMoneyForAnimal"] = "Вы получили: {0} монет за убийство животного",
                ["YouReceivedMoneyForNPC"] = "Вы получили: {0} монет за убийство NPC",
                ["YouReceivedMoneyForBradley"] = "Вы получили: {0} монет за уничтожение танка",
                ["YouReceivedMoneyForHeli"] = "Вы получили: {0} монет за уничтожение вертолета",
                ["YouReceivedMoneyForBarrel"] = "Вы получили: {0} монет за уничтожение бочки",
                ["YouReceivedMoneyForResource"] = "Вы получили: {0} монет за добычу ресурса {2}",
                ["YouReceivedMoneyForPickup"] = "Вы получили: {0} монет за подбор {2}",
                ["YouReceivedMoneyForTime"] = "Вы получили: {0} монет за время на сервере",
                ["YouReceivedMoneyForIQSphere"] = "Вы получили: {0} монет за убийство NPC в {2}",
                ["LogDeposit"] = "Внесение: {0} {1} на счет игрока {2}",
                ["LogSetBalance"] = "Установка баланса: {0} {1} для игрока {2}",
                ["LogTransfer"] = "Перевод: {0} {1} от игрока {2} игроку {3}",
                ["LogWithdrawl"] = "Снятие: {0} {1} со счета игрока {2}"
            }, this);
        }

        #endregion Localization

        #region Initialization

        private const string permissionBalance = "economics.balance";
        private const string permissionDeposit = "economics.deposit";
        private const string permissionDepositAll = "economics.depositall";
        private const string permissionSetBalance = "economics.setbalance";
        private const string permissionSetBalanceAll = "economics.setbalanceall";
        private const string permissionTransfer = "economics.transfer";
        private const string permissionTransferAll = "economics.transferall";
        private const string permissionWithdraw = "economics.withdraw";
        private const string permissionWithdrawAll = "economics.withdrawall";
        private const string permissionWipe = "economics.wipe";

        /// <summary>
        /// UI constants
        /// </summary>
        private const string HUD_PANEL_NAME = "EconomicsHUD";
        private const string BALANCE_BUTTON_NAME = "EconomicsBalanceButton";
        private readonly Dictionary<BasePlayer, string> uiElements = new();
        private readonly string BalanceIconUrl = "https://i.imgur.com/5ALJG2t.png";

        /// <summary>
        /// Таймеры для времени на сервере
        /// </summary>
        private readonly Dictionary<string, Timer> playerTimeRewardTimers = new();
        /// <summary>
        /// Информация о последних убийствах для предотвращения спама
        /// </summary>
        private readonly Dictionary<string, Dictionary<string, float>> lastPlayerKills = new();
        /// <summary>
        /// 5 минут кулдаун между убийствами одного и того же игрока
        /// </summary>
        private const float PlayerKillCooldown = 300f;

        private void Init()
        {
            // Register universal chat/console commands
            AddLocalizedCommand(nameof(CommandBalance));
            AddLocalizedCommand(nameof(CommandDeposit));
            AddLocalizedCommand(nameof(CommandSetBalance));
            AddLocalizedCommand(nameof(CommandTransfer));
            AddLocalizedCommand(nameof(CommandWithdraw));
            AddLocalizedCommand(nameof(CommandWipe));

            // Register permissions for commands
            permission.RegisterPermission(permissionBalance, this);
            permission.RegisterPermission(permissionDeposit, this);
            permission.RegisterPermission(permissionDepositAll, this);
            permission.RegisterPermission(permissionSetBalance, this);
            permission.RegisterPermission(permissionSetBalanceAll, this);
            permission.RegisterPermission(permissionTransfer, this);
            permission.RegisterPermission(permissionTransferAll, this);
            permission.RegisterPermission(permissionWithdraw, this);
            permission.RegisterPermission(permissionWithdrawAll, this);
            permission.RegisterPermission(permissionWipe, this);

            // Set default language to Russian
            lang.SetServerLanguage("ru");

            // Load existing data and migrate old data format
            data = Interface.Oxide.DataFileSystem.GetFile(Name);
            try
            {
                Dictionary<ulong, double> temp = data.ReadObject<Dictionary<ulong, double>>();
                try
                {
                    storedData = new StoredData();
                    foreach (KeyValuePair<ulong, double> old in temp)
                    {
                        if (!storedData.Balances.ContainsKey(old.Key.ToString()))
                        {
                            storedData.Balances.Add(old.Key.ToString(), old.Value);
                        }
                    }
                    changed = true;
                }
                catch
                {
                    // Ignored
                }
            }
            catch
            {
                storedData = data.ReadObject<StoredData>();
                changed = true;
            }

            List<string> playerData = new(storedData.Balances.Keys);

            // Check for and set any balances over maximum allowed
            if (config.BalanceLimit > 0)
            {
                foreach (string p in playerData)
                {
                    if (storedData.Balances[p] > config.BalanceLimit)
                    {
                        storedData.Balances[p] = config.BalanceLimit;
                        changed = true;
                    }
                }
            }

            // Check for and remove any inactive player balance data
            if (config.RemoveUnused)
            {
                foreach (string p in playerData)
                {
                    if (storedData.Balances[p].Equals(config.StartingBalance))
                    {
                        _ = storedData.Balances.Remove(p);
                        changed = true;
                    }
                }
            }

            // Регистрируем пермишены для получения валюты
            if (!string.IsNullOrEmpty(config.PlayerKillSettings.Permission))
            {
                permission.RegisterPermission(config.PlayerKillSettings.Permission, this);
            }

            if (!string.IsNullOrEmpty(config.AnimalKillSettings.Permission))
            {
                permission.RegisterPermission(config.AnimalKillSettings.Permission, this);
            }

            if (!string.IsNullOrEmpty(config.NPCKillSettings.Permission))
            {
                permission.RegisterPermission(config.NPCKillSettings.Permission, this);
            }

            if (!string.IsNullOrEmpty(config.BradleyKillSettings.Permission))
            {
                permission.RegisterPermission(config.BradleyKillSettings.Permission, this);
            }

            if (!string.IsNullOrEmpty(config.HelicopterKillSettings.Permission))
            {
                permission.RegisterPermission(config.HelicopterKillSettings.Permission, this);
            }

            if (!string.IsNullOrEmpty(config.BarrelDestroySettings.Permission))
            {
                permission.RegisterPermission(config.BarrelDestroySettings.Permission, this);
            }

            if (!string.IsNullOrEmpty(config.ResourceGatherSettings.Permission))
            {
                permission.RegisterPermission(config.ResourceGatherSettings.Permission, this);
            }

            if (!string.IsNullOrEmpty(config.ResourcePickupSettings.Permission))
            {
                permission.RegisterPermission(config.ResourcePickupSettings.Permission, this);
            }

            if (!string.IsNullOrEmpty(config.TimeOnServerSettings.Permission))
            {
                permission.RegisterPermission(config.TimeOnServerSettings.Permission, this);
            }

            if (!string.IsNullOrEmpty(config.SphereEventSettings.Under.Permission))
            {
                permission.RegisterPermission(config.SphereEventSettings.Under.Permission, this);
            }

            if (!string.IsNullOrEmpty(config.SphereEventSettings.Around.Permission))
            {
                permission.RegisterPermission(config.SphereEventSettings.Around.Permission, this);
            }

            if (!string.IsNullOrEmpty(config.SphereEventSettings.Tier1.Permission))
            {
                permission.RegisterPermission(config.SphereEventSettings.Tier1.Permission, this);
            }

            if (!string.IsNullOrEmpty(config.SphereEventSettings.Tier2.Permission))
            {
                permission.RegisterPermission(config.SphereEventSettings.Tier2.Permission, this);
            }

            if (!string.IsNullOrEmpty(config.SphereEventSettings.Tier3.Permission))
            {
                permission.RegisterPermission(config.SphereEventSettings.Tier3.Permission, this);
            }
        }

        private void OnServerInitialized()
        {
            // Clean up any existing UI elements that might be leftover from a plugin reload
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player?.IsConnected == true)
                {
                    // Try to destroy any existing UI elements
                    DestroyUI(player);
                    _ = CuiHelper.DestroyUi(player, HUD_PANEL_NAME);
                    _ = CuiHelper.DestroyUi(player, BALANCE_BUTTON_NAME);
                }
            }

            // Create UI for all active players
            _ = timer.Once(0.5f, () =>
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    if (player?.IsConnected == true)
                    {
                        if (config.ShowBalanceHUD)
                        {
                            CreateBalanceHUD(player);
                        }

                        // Всегда создаем кнопку
                        CreateBalanceButton(player);

                        // Перезапускаем таймер начисления валюты за время на сервере
                        StartTimeRewardTimer(player.UserIDString);
                    }
                }
            });
        }

        private void OnNewSave()
        {
            if (config.WipeOnNewSave)
            {
                storedData.Balances.Clear();
                changed = true;
                _ = Interface.Call("OnEconomicsDataWiped");
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (config.ShowBalanceHUD)
            {
                CreateBalanceHUD(player);
            }

            // Всегда создаем кнопку, независимо от настроек HUD
            CreateBalanceButton(player);

            // Запускаем таймер начисления валюты за время на сервере
            StartTimeRewardTimer(player.UserIDString);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (uiElements.ContainsKey(player))
            {
                DestroyUI(player);
                _ = uiElements.Remove(player);
            }

            // Останавливаем таймер начисления валюты за время на сервере
            StopTimeRewardTimer(player.UserIDString);
        }

        #endregion Initialization

        #region API Methods

        private double Balance(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                LogWarning("Balance method called without a valid player ID");
                return 0.0;
            }

            return storedData.Balances.TryGetValue(playerId, out double playerData) ? playerData : config.StartingBalance;
        }

        private double Balance(object playerId)
        {
            return Balance(GetUserId(playerId));
        }

        private string GetUserId(object playerId)
        {
            if (playerId is ulong)
            {
                return playerId.ToString();
            }

            // Support Rusts BasePlayer.userID type
            string userId = playerId.ToString();
            if (string.IsNullOrEmpty(userId) || !userId.IsSteamId())
            {
                throw new ArgumentException("Invalid player ID, playerId must be a valid SteamID of type ulong or string");
            }

            return userId;
        }

        private bool Deposit(string playerId, double amount)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                LogWarning("Deposit method called without a valid player ID");
                return false;
            }

            if (amount > 0 && SetBalance(playerId, amount + Balance(playerId)))
            {
                _ = Interface.Call("OnEconomicsDeposit", playerId, amount);

                if (config.LogTransactions)
                {
                    LogToFile("transactions", $"[{DateTime.Now}] {GetLang("LogDeposit", null, amount, playerId)}", this);
                }

                return true;
            }

            return false;
        }

        private bool Deposit(object playerId, double amount)
        {
            return Deposit(GetUserId(playerId), amount);
        }

        private bool SetBalance(string playerId, double amount)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                LogWarning("SetBalance method called without a valid player ID");
                return false;
            }

            if (amount >= 0 || config.AllowNegativeBalance)
            {
                amount = Math.Round(amount, 2);
                if (config.BalanceLimit > 0 && amount > config.BalanceLimit)
                {
                    amount = config.BalanceLimit;
                }
                else if (config.AllowNegativeBalance && config.NegativeBalanceLimit < 0 && amount < config.NegativeBalanceLimit)
                {
                    amount = config.NegativeBalanceLimit;
                }

                storedData.Balances[playerId] = amount;
                changed = true;

                _ = Interface.Call("OnEconomicsBalanceUpdated", playerId, amount);
                _ = Interface.CallDeprecatedHook("OnBalanceChanged", "OnEconomicsBalanceUpdated", new DateTime(2022, 7, 1), playerId, amount);

                if (config.LogTransactions)
                {
                    LogToFile("transactions", $"[{DateTime.Now}] {GetLang("LogSetBalance", null, amount, playerId)}", this);
                }

                return true;
            }

            return false;
        }

        private bool SetBalance(object playerId, double amount)
        {
            return SetBalance(GetUserId(playerId), amount);
        }

        private bool Transfer(string playerId, string targetId, double amount)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                LogWarning("Transfer method called without a valid player ID");
                return false;
            }

            if (Withdraw(playerId, amount) && Deposit(targetId, amount))
            {
                _ = Interface.Call("OnEconomicsTransfer", playerId, targetId, amount);

                if (config.LogTransactions)
                {
                    LogToFile("transactions", $"[{DateTime.Now}] {GetLang("LogTransfer", null, amount, targetId, playerId)}", this);
                }

                return true;
            }

            return false;
        }

        private bool Transfer(object playerId, ulong targetId, double amount)
        {
            return Transfer(GetUserId(playerId), targetId.ToString(), amount);
        }

        private bool Withdraw(string playerId, double amount)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                LogWarning("Withdraw method called without a valid player ID");
                return false;
            }

            if (amount >= 0 || config.AllowNegativeBalance)
            {
                double balance = Balance(playerId);
                if ((balance >= amount || (config.AllowNegativeBalance && balance + amount > config.NegativeBalanceLimit)) && SetBalance(playerId, balance - amount))
                {
                    _ = Interface.Call("OnEconomicsWithdrawl", playerId, amount);

                    if (config.LogTransactions)
                    {
                        LogToFile("transactions", $"[{DateTime.Now}] {GetLang("LogWithdrawl", null, amount, playerId)}", this);
                    }

                    return true;
                }
            }

            return false;
        }

        private bool Withdraw(object playerId, double amount)
        {
            return Withdraw(GetUserId(playerId), amount);
        }

        #endregion API Methods

        #region Commands

        #region Balance Command

        private void CommandBalance(IPlayer player, string command, string[] args)
        {
            if (args?.Length > 0)
            {
                if (!player.HasPermission(permissionBalance))
                {
                    Message(player, "NotAllowed", command);
                    return;
                }

                IPlayer target = FindPlayer(args[0], player);
                if (target == null)
                {
                    Message(player, "UsageBalance", command);
                    return;
                }

                Message(player, "PlayerBalance", target.Name, Balance(target.Id));
                return;
            }

            if (player.IsServer)
            {
                Message(player, "UsageBalanceOthers", command);
            }
            else
            {
                Message(player, "YourBalance", Balance(player.Id));
            }
        }

        #endregion Balance Command

        #region Deposit Command

        private void CommandDeposit(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permissionDeposit))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args == null || args.Length <= 1)
            {
                Message(player, "UsageDeposit", command);
                return;
            }

            _ = double.TryParse(args[1], out double amount);
            if (amount <= 0)
            {
                Message(player, "ZeroAmount");
                return;
            }

            if (args[0] == "*")
            {
                if (!player.HasPermission(permissionDepositAll))
                {
                    Message(player, "NotAllowed", command);
                    return;
                }

                int receivers = 0;
                foreach (string targetId in storedData.Balances.Keys.ToList())
                {
                    if (Deposit(targetId, amount))
                    {
                        receivers++;
                    }
                }
                Message(player, "DepositedToAll", amount * receivers, amount, receivers);
            }
            else
            {
                IPlayer target = FindPlayer(args[0], player);
                if (target == null)
                {
                    return;
                }

                if (Deposit(target.Id, amount))
                {
                    Message(player, "PlayerBalance", target.Name, Balance(target.Id));
                }
                else
                {
                    Message(player, "TransactionFailed", target.Name);
                }
            }
        }

        #endregion Deposit Command

        #region Set Balance Command

        private void CommandSetBalance(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permissionSetBalance))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args == null || args.Length <= 1)
            {
                Message(player, "UsageSetBalance", command);
                return;
            }

            _ = double.TryParse(args[1], out double amount);

            if (amount < 0)
            {
                Message(player, "NegativeBalance");
                return;
            }

            if (args[0] == "*")
            {
                if (!player.HasPermission(permissionSetBalanceAll))
                {
                    Message(player, "NotAllowed", command);
                    return;
                }

                int receivers = 0;
                foreach (string targetId in storedData.Balances.Keys.ToList())
                {
                    if (SetBalance(targetId, amount))
                    {
                        receivers++;
                    }
                }
                Message(player, "SetBalanceForAll", amount, receivers);
            }
            else
            {
                IPlayer target = FindPlayer(args[0], player);
                if (target == null)
                {
                    return;
                }

                if (SetBalance(target.Id, amount))
                {
                    Message(player, "PlayerBalance", target.Name, Balance(target.Id));
                }
                else
                {
                    Message(player, "TransactionFailed", target.Name);
                }
            }
        }

        #endregion Set Balance Command

        #region Transfer Command

        private void CommandTransfer(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permissionTransfer))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args == null || args.Length <= 1)
            {
                Message(player, "UsageTransfer", command);
                return;
            }

            _ = double.TryParse(args[1], out double amount);

            if (amount <= 0)
            {
                Message(player, "ZeroAmount");
                return;
            }

            if (args[0] == "*")
            {
                if (!player.HasPermission(permissionTransferAll))
                {
                    Message(player, "NotAllowed", command);
                    return;
                }

                if (!Withdraw(player.Id, amount))
                {
                    Message(player, "YouLackMoney");
                    return;
                }

                int receivers = players.Connected.Count();
                double splitAmount = amount /= receivers;

                foreach (IPlayer target in players.Connected)
                {
                    if (Deposit(target.Id, splitAmount) && target.IsConnected)
                    {
                        Message(target, "ReceivedFrom", splitAmount, player.Name);
                    }
                }
                Message(player, "TransferedToAll", amount, splitAmount, receivers);
            }
            else
            {
                IPlayer target = FindPlayer(args[0], player);
                if (target == null)
                {
                    return;
                }

                if (target.Equals(player))
                {
                    Message(player, "TransferToSelf");
                    return;
                }

                if (!Withdraw(player.Id, amount))
                {
                    Message(player, "YouLackMoney");
                    return;
                }

                if (Deposit(target.Id, amount))
                {
                    Message(player, "TransferredTo", amount, target.Name);
                    Message(target, "ReceivedFrom", amount, player.Name);
                }
                else
                {
                    Message(player, "TransactionFailed", target.Name);
                }
            }
        }

        #endregion Transfer Command

        #region Withdraw Command

        private void CommandWithdraw(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permissionWithdraw))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args == null || args.Length <= 1)
            {
                Message(player, "UsageWithdraw", command);
                return;
            }

            _ = double.TryParse(args[1], out double amount);

            if (amount <= 0)
            {
                Message(player, "ZeroAmount");
                return;
            }

            if (args[0] == "*")
            {
                if (!player.HasPermission(permissionWithdrawAll))
                {
                    Message(player, "NotAllowed", command);
                    return;
                }

                int receivers = 0;
                foreach (string targetId in storedData.Balances.Keys.ToList())
                {
                    if (Withdraw(targetId, amount))
                    {
                        receivers++;
                    }
                }
                Message(player, "WithdrawnForAll", amount * receivers, amount, receivers);
            }
            else
            {
                IPlayer target = FindPlayer(args[0], player);
                if (target == null)
                {
                    return;
                }

                if (Withdraw(target.Id, amount))
                {
                    Message(player, "PlayerBalance", target.Name, Balance(target.Id));
                }
                else
                {
                    Message(player, "YouLackMoney", target.Name);
                }
            }
        }

        #endregion Withdraw Command

        #region Wipe Command

        private void CommandWipe(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permissionWipe))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            storedData = new StoredData();
            changed = true;
            SaveData();

            Message(player, "DataWiped");
            _ = Interface.Call("OnEconomicsDataWiped", player);
        }

        #endregion Wipe Command

        #endregion Commands

        #region Helpers

        private IPlayer FindPlayer(string playerNameOrId, IPlayer player)
        {
            IPlayer[] foundPlayers = players.FindPlayers(playerNameOrId).ToArray();
            if (foundPlayers.Length > 1)
            {
                Message(player, "PlayersFound", string.Join(", ", foundPlayers.Select(p => p.Name).Take(10).ToArray()).Truncate(60));
                return null;
            }

            IPlayer target = foundPlayers.Length == 1 ? foundPlayers[0] : null;
            if (target == null)
            {
                Message(player, "NoPlayersFound", playerNameOrId);
                return null;
            }

            return target;
        }

        private void AddLocalizedCommand(string command)
        {
            foreach (string language in lang.GetLanguages(this))
            {
                foreach (KeyValuePair<string, string> message in (Dictionary<string, string>)lang.GetMessages(language, this))
                {
                    if (message.Key.Equals(command, StringComparison.Ordinal) && !string.IsNullOrEmpty(message.Value))
                    {
                        AddCovalenceCommand(message.Value, command);
                    }
                }
            }
        }

        private string GetLang(string langKey, string playerId = null, params object[] args)
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, lang.GetMessage(langKey, this, playerId), args);
        }

        private void Message(IPlayer player, string textOrLang, params object[] args)
        {
            if (player.IsConnected)
            {
                string message = GetLang(textOrLang, player.Id, args);
                player.Reply(message != textOrLang ? message : textOrLang);
            }
        }

        #endregion Helpers

        #region UI

        private void CreateBalanceButton(BasePlayer player)
        {
            if (player == null)
            {
                return;
            }

            // Не вызываем DestroyUI, чтобы не убрать панель баланса
            // Уничтожаем только кнопку, если она существует
            _ = CuiHelper.DestroyUi(player, BALANCE_BUTTON_NAME);

            CuiElementContainer container = new();

            // Создаем фоновую круглую панель с прозрачностью
            _ = container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.86 0.0462",
                    AnchorMax = "0.8765 0.071"
                },
                Image =
                {
                    // Используем темный фон с прозрачностью для круглой кнопки
                    Color = "0.42 0.42 0.42 0.5",
                    // Используем скругленный фон
                    Sprite = "assets/content/ui/ui.background.transparent.radial.psd"
                }
            }, "Hud", BALANCE_BUTTON_NAME);

            // Добавляем кнопку над фоном для обработки клика
            string buttonName = container.Add(new CuiButton
            {
                Button =
                {
                    Command = "economics.showbalance",
                    Color = "0 0 0 0" // Полностью прозрачный цвет для кнопки
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                },
                Text =
                {
                    Text = ""
                }
            }, BALANCE_BUTTON_NAME);

            // Добавляем иконку с немного меньшим размером для красивого отображения
            container.Add(new CuiElement
            {
                Parent = buttonName,
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Url = BalanceIconUrl,
                        Color = "1 1 1 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.01 0.01",
                        AnchorMax = "0.9 0.9"
                    }
                }
            });

            _ = CuiHelper.AddUi(player, container);
            // Не обновляем uiElements, чтобы не перезаписать текущий активный элемент
        }

        private void CreateBalanceHUD(BasePlayer player)
        {
            if (player == null)
            {
                return;
            }

            // Уничтожаем только панель баланса, но не кнопку
            _ = CuiHelper.DestroyUi(player, HUD_PANEL_NAME);

            double balance = Balance(player.UserIDString);
            string formattedBalance = GetLang("BalanceHUD", player.UserIDString, balance);

            CuiElementContainer container = new();
            _ = container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.802 0.0462", AnchorMax = "0.859 0.071", OffsetMin = "0 0", OffsetMax = "0 0" },
                Image = { Color = "0.42 0.42 0.42 0.5" }
            }, "Hud", HUD_PANEL_NAME);

            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.77 1" },
                Text = { Text = formattedBalance, FontSize = 14, Align = TextAnchor.MiddleCenter, Color = config.HUDTextColor }
            }, HUD_PANEL_NAME);

            _ = CuiHelper.AddUi(player, container);
            uiElements[player] = HUD_PANEL_NAME;
        }

        [Command("economics.showbalance")]
        private void CommandShowBalance(IPlayer player, string command, string[] args)
        {
            if (player?.Object == null)
            {
                return;
            }

            if (player.Object is not BasePlayer basePlayer)
            {
                return;
            }

            // Проверяем, показана ли панель баланса в данный момент
            if (uiElements.TryGetValue(basePlayer, out string uiName) && uiName == HUD_PANEL_NAME)
            {
                // Если панель баланса уже показана, скрываем её
                // Уничтожаем только панель баланса, но не кнопку
                _ = CuiHelper.DestroyUi(basePlayer, HUD_PANEL_NAME);
                _ = uiElements.Remove(basePlayer);
            }
            else
            {
                // Если панель баланса не показана, показываем её
                CreateBalanceHUD(basePlayer);
            }

            // Убедимся, что кнопка всегда видна
            CreateBalanceButton(basePlayer);
        }

        private void DestroyUI(BasePlayer player)
        {
            if (player == null)
            {
                return;
            }

            if (uiElements.TryGetValue(player, out string uiName))
            {
                _ = CuiHelper.DestroyUi(player, uiName);
                // Удаляем запись из словаря uiElements, чтобы система знала, что UI больше не показан
                _ = uiElements.Remove(player);
            }

            // Always try to destroy both UI elements
            _ = CuiHelper.DestroyUi(player, HUD_PANEL_NAME);
            _ = CuiHelper.DestroyUi(player, BALANCE_BUTTON_NAME);
        }

        private void UpdateAllHUDs()
        {
            if (!config.ShowBalanceHUD)
            {
                return;
            }

            foreach (BasePlayer? player in BasePlayer.activePlayerList)
            {
                if (player?.IsConnected == true)
                {
                    if (config.ShowBalanceHUD)
                    {
                        CreateBalanceHUD(player);
                    }
                    else
                    {
                        CreateBalanceButton(player);
                    }
                }
            }
        }

        private void OnEconomicsBalanceUpdated(string playerId, double amount)
        {
            BasePlayer player = BasePlayer.Find(playerId);
            if (player?.IsConnected == true && uiElements.TryGetValue(player, out string uiName) && uiName == HUD_PANEL_NAME)
            {
                CreateBalanceHUD(player);
            }
        }

        #endregion UI

        #region Reward Hooks

        /// <summary>
        /// Хук для обработки убийства игрока другим игроком
        /// </summary>
        /// <param name="player"></param>
        /// <param name="info"></param>
        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null || info.InitiatorPlayer == null ||
                player.IsNpc || info.InitiatorPlayer.IsNpc ||
                player.UserIDString == info.InitiatorPlayer.UserIDString)
            {
                return;
            }

            if (!config.PlayerKillSettings.Enabled)
            {
                return;
            }

            string killerID = info.InitiatorPlayer.UserIDString;
            string victimID = player.UserIDString;

            // Проверяем права доступа
            if (!string.IsNullOrEmpty(config.PlayerKillSettings.Permission) &&
                !permission.UserHasPermission(killerID, config.PlayerKillSettings.Permission))
            {
                return;
            }

            // Проверяем кулдаун между убийствами одного и того же игрока
            bool onCooldown = false;
            if (lastPlayerKills.TryGetValue(killerID, out Dictionary<string, float>? killTimes) && killTimes.TryGetValue(victimID, out float lastKillTime) && Time.realtimeSinceStartup - lastKillTime < PlayerKillCooldown)
            {
                onCooldown = true;
            }

            if (onCooldown)
            {
                return;
            }

            // Проверяем шанс
            if (UnityEngine.Random.Range(1, 101) > config.PlayerKillSettings.Rewards.Chance)
            {
                return;
            }

            // Выдаем валюту
            if (Deposit(killerID, config.PlayerKillSettings.Rewards.Amount))
            {
                // Обновляем время последнего убийства
                if (!lastPlayerKills.TryGetValue(killerID, out Dictionary<string, float> playerKillTimes))
                {
                    playerKillTimes = new Dictionary<string, float>();
                    lastPlayerKills[killerID] = playerKillTimes;
                }

                playerKillTimes[victimID] = Time.realtimeSinceStartup;

                // Отправляем сообщение игроку
                IPlayer iplayer = players.FindPlayerById(killerID);
                if (iplayer?.IsConnected == true)
                {
                    SendRewardMessage(iplayer, "YouReceivedMoney", config.PlayerKillSettings.Rewards.Amount);
                }
            }
        }

        /// <summary>
        /// Хук для обработки убийства животного
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="info"></param>
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null || info.InitiatorPlayer == null)
            {
                return;
            }

            BasePlayer player = info.InitiatorPlayer;
            if (player.IsNpc)
            {
                return;
            }

            string playerID = player.UserIDString;

            // Обработка убийства животного
            if (entity is BaseAnimalNPC && config.AnimalKillSettings.Enabled)
            {
                // Проверяем права доступа
                if (!string.IsNullOrEmpty(config.AnimalKillSettings.Permission) &&
                    !permission.UserHasPermission(playerID, config.AnimalKillSettings.Permission))
                {
                    return;
                }

                // Проверяем шанс
                if (UnityEngine.Random.Range(1, 101) > config.AnimalKillSettings.Rewards.Chance)
                {
                    return;
                }

                // Выдаем валюту
                if (Deposit(playerID, config.AnimalKillSettings.Rewards.Amount))
                {
                    // Отправляем сообщение игроку
                    IPlayer iplayer = players.FindPlayerById(playerID);
                    if (iplayer?.IsConnected == true)
                    {
                        SendRewardMessage(iplayer, "YouReceivedMoney", config.AnimalKillSettings.Rewards.Amount);
                    }
                }
            }
            // Обработка убийства NPC
            else if (entity is BasePlayer npcPlayer && npcPlayer.IsNpc && config.NPCKillSettings.Enabled)
            {
                // Проверяем права доступа
                if (!string.IsNullOrEmpty(config.NPCKillSettings.Permission) &&
                    !permission.UserHasPermission(playerID, config.NPCKillSettings.Permission))
                {
                    return;
                }

                // Проверяем шанс
                if (UnityEngine.Random.Range(1, 101) > config.NPCKillSettings.Rewards.Chance)
                {
                    return;
                }

                // Выдаем валюту
                if (Deposit(playerID, config.NPCKillSettings.Rewards.Amount))
                {
                    // Отправляем сообщение игроку
                    IPlayer iplayer = players.FindPlayerById(playerID);
                    if (iplayer?.IsConnected == true)
                    {
                        SendRewardMessage(iplayer, "YouReceivedMoney", config.NPCKillSettings.Rewards.Amount);
                    }
                }
            }
            // Обработка уничтожения танка (Bradley APC)
            else if (entity is BradleyAPC && config.BradleyKillSettings.Enabled)
            {
                // Проверяем права доступа
                if (!string.IsNullOrEmpty(config.BradleyKillSettings.Permission) &&
                    !permission.UserHasPermission(playerID, config.BradleyKillSettings.Permission))
                {
                    return;
                }

                // Проверяем шанс
                if (UnityEngine.Random.Range(1, 101) > config.BradleyKillSettings.Rewards.Chance)
                {
                    return;
                }

                // Выдаем валюту
                if (Deposit(playerID, config.BradleyKillSettings.Rewards.Amount))
                {
                    // Отправляем сообщение игроку
                    IPlayer iplayer = players.FindPlayerById(playerID);
                    if (iplayer?.IsConnected == true)
                    {
                        SendRewardMessage(iplayer, "YouReceivedMoney", config.BradleyKillSettings.Rewards.Amount);
                    }
                }
            }
            // Обработка уничтожения вертолета
            else if (entity is PatrolHelicopter && config.HelicopterKillSettings.Enabled)
            {
                // Проверяем права доступа
                if (!string.IsNullOrEmpty(config.HelicopterKillSettings.Permission) &&
                    !permission.UserHasPermission(playerID, config.HelicopterKillSettings.Permission))
                {
                    return;
                }

                // Проверяем шанс
                if (UnityEngine.Random.Range(1, 101) > config.HelicopterKillSettings.Rewards.Chance)
                {
                    return;
                }

                // Выдаем валюту
                if (Deposit(playerID, config.HelicopterKillSettings.Rewards.Amount))
                {
                    // Отправляем сообщение игроку
                    IPlayer iplayer = players.FindPlayerById(playerID);
                    if (iplayer?.IsConnected == true)
                    {
                        SendRewardMessage(iplayer, "YouReceivedMoney", config.HelicopterKillSettings.Rewards.Amount);
                    }
                }
            }
            // Обработка уничтожения бочек
            else if (entity is LootContainer lootContainer && lootContainer.ShortPrefabName.Contains("barrel") && config.BarrelDestroySettings.Enabled)
            {
                // Проверяем права доступа
                if (!string.IsNullOrEmpty(config.BarrelDestroySettings.Permission) &&
                    !permission.UserHasPermission(playerID, config.BarrelDestroySettings.Permission))
                {
                    return;
                }

                // Проверяем шанс
                if (UnityEngine.Random.Range(1, 101) > config.BarrelDestroySettings.Rewards.Chance)
                {
                    return;
                }

                // Выдаем валюту
                if (Deposit(playerID, config.BarrelDestroySettings.Rewards.Amount))
                {
                    // Отправляем сообщение игроку
                    IPlayer iplayer = players.FindPlayerById(playerID);
                    if (iplayer?.IsConnected == true)
                    {
                        SendRewardMessage(iplayer, "YouReceivedMoney", config.BarrelDestroySettings.Rewards.Amount);
                    }
                }
            }
        }

        /// <summary>
        /// Хук для обработки добычи ресурсов
        /// </summary>
        /// <param name="dispenser"></param>
        /// <param name="entity"></param>
        /// <param name="item"></param>
        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (!config.ResourceGatherSettings.Enabled || entity == null || item == null || entity is not BasePlayer)
            {
                return;
            }

            BasePlayer player = entity as BasePlayer;
            if (player.IsNpc)
            {
                return;
            }

            string playerID = player.UserIDString;
            string itemShortname = item.info.shortname;

            // Проверяем права доступа
            if (!string.IsNullOrEmpty(config.ResourceGatherSettings.Permission) &&
                !permission.UserHasPermission(playerID, config.ResourceGatherSettings.Permission))
            {
                return;
            }

            // Проверяем, есть ли настройки для данного ресурса
            if (!config.ResourceGatherSettings.ResourceRewards.TryGetValue(itemShortname, out RewardSettings? resourceReward))
            {
                return;
            }

            // Проверяем шанс
            if (UnityEngine.Random.Range(1, 101) > resourceReward.Chance)
            {
                return;
            }

            // Выдаем валюту
            if (Deposit(playerID, resourceReward.Amount))
            {
                // Отправляем сообщение игроку
                IPlayer iplayer = players.FindPlayerById(playerID);
                if (iplayer?.IsConnected == true)
                {
                    SendRewardMessage(iplayer, "YouReceivedMoney", resourceReward.Amount);
                }
            }
        }

        /// <summary>
        /// Хук для обработки поднятия ресурсов с земли
        /// </summary>
        /// <param name="item"></param>
        /// <param name="player"></param>
        private void OnCollectiblePickup(Item item, BasePlayer player)
        {
            if (!config.ResourcePickupSettings.Enabled || player == null || item == null || player.IsNpc)
            {
                return;
            }

            string playerID = player.UserIDString;
            string itemShortname = item.info.shortname;

            // Проверяем права доступа
            if (!string.IsNullOrEmpty(config.ResourcePickupSettings.Permission) &&
                !permission.UserHasPermission(playerID, config.ResourcePickupSettings.Permission))
            {
                return;
            }

            // Проверяем, есть ли настройки для данного ресурса
            if (!config.ResourcePickupSettings.ResourceRewards.TryGetValue(itemShortname, out RewardSettings? resourceReward))
            {
                return;
            }

            // Проверяем шанс
            if (UnityEngine.Random.Range(1, 101) > resourceReward.Chance)
            {
                return;
            }

            // Выдаем валюту
            if (Deposit(playerID, resourceReward.Amount))
            {
                // Отправляем сообщение игроку
                IPlayer iplayer = players.FindPlayerById(playerID);
                if (iplayer?.IsConnected == true)
                {
                    SendRewardMessage(iplayer, "YouReceivedMoney", resourceReward.Amount);
                }
            }
        }

        /// <summary>
        /// Методы для начисления валюты за время на сервере
        /// </summary>
        /// <param name="playerID"></param>
        private void StartTimeRewardTimer(string playerID)
        {
            if (!config.TimeOnServerSettings.Enabled)
            {
                return;
            }

            // Проверяем права доступа
            if (!string.IsNullOrEmpty(config.TimeOnServerSettings.Permission) &&
                !permission.UserHasPermission(playerID, config.TimeOnServerSettings.Permission))
            {
                return;
            }

            // Останавливаем предыдущий таймер, если он есть
            StopTimeRewardTimer(playerID);

            // Создаем новый таймер

            // Сохраняем таймер
            playerTimeRewardTimers[playerID] = timer.Every(config.TimeOnServerSettings.TimeRequired, () =>
            {
                // Выдаем валюту
                if (Deposit(playerID, config.TimeOnServerSettings.Amount))
                {
                    // Отправляем сообщение игроку
                    IPlayer iplayer = players.FindPlayerById(playerID);
                    if (iplayer?.IsConnected == true)
                    {
                        SendRewardMessage(iplayer, "YouReceivedMoney", config.TimeOnServerSettings.Amount);
                    }
                }
            });
        }

        private void StopTimeRewardTimer(string playerID)
        {
            if (playerTimeRewardTimers.TryGetValue(playerID, out Timer? value))
            {
                value?.Destroy();
                _ = playerTimeRewardTimers.Remove(playerID);
            }
        }

        /// <summary>
        /// Хук для IQSphereEvent
        /// </summary>
        /// <param name="tier"></param>
        /// <param name="player"></param>
        private void OnIQSphereNpcKilled(string tier, BasePlayer player)
        {
            if (player?.IsNpc != false)
            {
                return;
            }

            string playerID = player.UserIDString;
            CurrencySettings settings;
            switch (tier.ToLower(System.Globalization.CultureInfo.CurrentCulture))
            {
                case "under":
                    settings = config.SphereEventSettings.Under;
                    break;
                case "around":
                    settings = config.SphereEventSettings.Around;
                    break;
                case "tier1":
                    settings = config.SphereEventSettings.Tier1;
                    break;
                case "tier2":
                    settings = config.SphereEventSettings.Tier2;
                    break;
                case "tier3":
                    settings = config.SphereEventSettings.Tier3;
                    break;
                default:
                    return;
            }

            if (settings?.Enabled != true)
            {
                return;
            }

            // Проверяем права доступа
            if (!string.IsNullOrEmpty(settings.Permission) &&
                !permission.UserHasPermission(playerID, settings.Permission))
            {
                return;
            }

            // Проверяем шанс
            if (UnityEngine.Random.Range(1, 101) > settings.Rewards.Chance)
            {
                return;
            }

            // Выдаем валюту
            if (Deposit(playerID, settings.Rewards.Amount))
            {
                // Отправляем сообщение игроку
                IPlayer iplayer = players.FindPlayerById(playerID);
                if (iplayer?.IsConnected == true)
                {
                    SendRewardMessage(iplayer, "YouReceivedMoney", settings.Rewards.Amount);
                }
            }
        }

        #endregion Reward Hooks

        private void SendRewardMessage(IPlayer player, string key, params object[] args)
        {
            if (player?.IsConnected != true)
            {
                return;
            }

            // Для сообщений о получении валюты используем специальный формат
            if (key == "YouReceivedMoney" && args.Length > 0 && args[0] is int amount)
            {
                // Отправляем сообщение в фиксированном формате "Вы получили: X монет"
                player.Reply($"Вы получили: {amount} монет");
            }
            else
            {
                // Для всех остальных сообщений используем стандартный механизм локализации
                string message = GetLang(key, player.Id, args);
                if (string.IsNullOrEmpty(message))
                {
                    return;
                }

                // Отправляем сообщение в чат
                player.Reply(message);
            }
        }
    }
}

#region Extension Methods

namespace Oxide.Plugins.EconomicsExtensionMethods
{
    public static class ExtensionMethods
    {
        public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0)
            {
                return min;
            }
            else if (val.CompareTo(max) > 0)
            {
                return max;
            }
            else
            {
                return val;
            }
        }
    }
}

#endregion Extension Methods
