using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    /// <summary>
    /// Определение класса CuiRawImageComponent для использования изображений по URL
    /// </summary>
    public class CuiRawImageComponent : ICuiComponent, ICuiColor
    {
        public string Type => "UnityEngine.UI.RawImage";

        [JsonProperty("sprite")]
        public string? Sprite { get; set; }

        public string? Color { get; set; }

        [JsonProperty("material")]
        public string? Material { get; set; }

        [JsonProperty("url")]
        public string? Url { get; set; }

        [JsonProperty("png")]
        public string? Png { get; set; }

        [JsonProperty("fadeIn")]
        public float FadeIn { get; set; }

        [JsonProperty("fitToParent")]
        public bool FitToParent { get; set; }
    }

    [Info("TeleportMenu", "YourName", "1.0.0")]
    [Description("Provides a UI teleport menu for players to teleport to different locations")]
    public class TeleportMenu : RustPlugin
    {
        #region Fields

        private const string UIMainPanel = "TeleportMenu.MainPanel";
        private const string UICloseButton = "TeleportMenu.CloseButton";
        private const string UIHomePanel = "TeleportMenu.HomePanel";
        private const string UIFriendsPanel = "TeleportMenu.FriendsPanel";
        private const string UIHomeButtonsPanel = "TeleportMenu.HomeButtonsPanel";
        private const string UITownButton = "TeleportMenu.TownButton";
        private const string UICampButton = "TeleportMenu.CampButton";
        private const string UIAcceptButton = "TeleportMenu.AcceptButton";
        private const string UIDeclineButton = "TeleportMenu.DeclineButton";

        private readonly Dictionary<ulong, Timer> pendingTeleports = new();
        private readonly Dictionary<ulong, Dictionary<string, Vector3>> homes;
        private readonly Dictionary<ulong, List<TeleportRequest>> teleportRequests = new();
        private readonly Dictionary<ulong, TeleportData> teleportData = new();

        private PluginConfig config;

        /// <summary>
        /// Добавляем словарь для хранения координат монументов
        /// </summary>
        private readonly Dictionary<string, Vector3> monumentPositions = new();
        private static readonly string[] stringArray = new[] { "дом1" };
        private static readonly string[] stringArray0 = new[] { "дом2" };

        #endregion Fields

        #region Configuration

        private sealed class PluginConfig
        {
            public float TeleportDelay { get; set; } = 5.0f;
            public float TeleportCooldown { get; set; } = 60.0f;
            public int MaxHomesPerPlayer { get; set; } = 999;
            public bool EnableTPToTown { get; set; } = true;
            public bool EnableTPToCamp { get; set; } = true;
            public bool EnableTPToFriends { get; set; } = true;
            public Vector3 TownLocation { get; set; } = new Vector3(0, 0, 0);
            public Vector3 CampLocation { get; set; } = new Vector3(0, 0, 0);
            public string MainColor { get; set; } = "0.4 0.34 0.6 0.98";
            public string BackgroundColor { get; set; } = "0.22 0.22 0.29 0.9";
            public string ButtonColor { get; set; } = "0.42 0.42 0.55 0.8";
            public string TextColor { get; set; } = "1 1 1 0.9";
            public string AcceptColor { get; set; } = "0.2 0.6 0.2 0.9";
            public string DeclineColor { get; set; } = "0.6 0.2 0.2 0.9";
            public bool AutoDetectMonuments { get; set; } = true;

            /// <summary>
            /// Настройки привилегий по телепортациям
            /// </summary>
            public Dictionary<string, TeleportLimits> PermissionLimits { get; set; } = new Dictionary<string, TeleportLimits>
            {
                ["default"] = new TeleportLimits { Home = 20, Town = 15, Camp = 15, Friend = 25 },
                ["vip"] = new TeleportLimits { Home = 30, Town = 25, Camp = 25, Friend = 35 },
                ["premium"] = new TeleportLimits { Home = 50, Town = 50, Camp = 50, Friend = 50 }
            };
        }

        public class TeleportLimits
        {
            public int Home { get; set; }
            public int Town { get; set; }
            public int Camp { get; set; }
            public int Friend { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig();
            Config.WriteObject(config, true);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<PluginConfig>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error loading config: {ex.Message}");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion Configuration

        #region Data Models

        private sealed class TeleportData
        {
            public DateTime LastTeleport { get; set; } = DateTime.MinValue;
            public Dictionary<string, int> TeleportCounts { get; set; } = new Dictionary<string, int>
            {
                ["home"] = 0,
                ["town"] = 0,
                ["camp"] = 0,
                ["friend"] = 0
            };
            public int TeleportCount { get; set; }
        }

        private sealed class TeleportRequest
        {
            public ulong RequesterId { get; set; }
            public ulong TargetId { get; set; }
            public DateTime RequestTime { get; set; }
            public Timer? ExpiryTimer { get; set; }
        }

        #endregion Data Models

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TPHome"] = "ТП ДОМОЙ",
                ["TPToFriend"] = "ТП К ДРУГУ",
                ["Town"] = "В ГОРОД",
                ["Camp"] = "В ЛАГЕРЬ",
                ["Save"] = "Нажми, чтобы сохранить ДОМ",
                ["Empty"] = "ПУСТО",
                ["Accept"] = "ПРИНЯТЬ",
                ["Decline"] = "ОТКЛОНИТЬ",
                ["MenuTitle"] = "ТЕЛЕПОРТ МЕНЮ",
                ["CooldownMessage"] = "Подождите {0} секунд перед следующим телепортом",
                ["TeleportCountdown"] = "Телепортация через {0} секунд",
                ["TeleportSuccess"] = "Телепортация успешна!",
                ["TeleportFailed"] = "Телепортация отменена",
                ["NoPermission"] = "У вас нет разрешения на использование этой команды",
                ["HomeSet"] = "Дом {0} установлен",
                ["HomeLimitReached"] = "Вы достигли лимита домов ({0})",
                ["RequestSent"] = "Запрос на телепортацию отправлен игроку {0}",
                ["RequestReceived"] = "Игрок {0} хочет телепортироваться к вам",
                ["NoActiveRequests"] = "У вас нет активных запросов",
                ["TPRequestAccepted"] = "Запрос на телепортацию принят",
                ["TPRequestDeclined"] = "Запрос на телепортацию отклонен",
                ["RequestExpired"] = "Запрос на телепортацию истек",
                ["TooCloseToHostile"] = "Нельзя телепортироваться рядом с враждебной постройкой"
            }, this);
        }

        private string GetMsg(string key, string userId = null)
        {
            if (key == null)
            {
                return "MISSING_KEY";
            }

            string message = lang.GetMessage(key, this, userId);
            return message ?? key;
        }

        #endregion Localization

        #region Hooks

        private void Init()
        {
            LoadConfig();
            permission.RegisterPermission("teleportmenu.use", this);
            permission.RegisterPermission("teleportmenu.admin", this);
            permission.RegisterPermission("teleportmenu.default", this);
            permission.RegisterPermission("teleportmenu.vip", this);
            permission.RegisterPermission("teleportmenu.premium", this);
            cmd.AddChatCommand("tp", this, CmdTeleportMenu);
            cmd.AddChatCommand("home", this, CmdHome);
            cmd.AddChatCommand("town", this, CmdTown);
            cmd.AddChatCommand("camp", this, CmdCamp);
            cmd.AddChatCommand("tpr", this, CmdTeleportRequest);
            cmd.AddChatCommand("tpa", this, CmdAcceptTeleport);
            cmd.AddChatCommand("tpdecline", this, CmdDeclineTeleport);
            cmd.AddChatCommand("sethome", this, CmdSetHome);
            cmd.AddChatCommand("delhome", this, CmdDelHome);
            cmd.AddChatCommand("savehome", this, CmdSaveHome);
            cmd.AddChatCommand("savehome2", this, CmdSaveHome2);

            // Добавляем консольные команды для прямого вызова из UI без чата
            cmd.AddConsoleCommand("teleportmenu.sethome1", this, "ConSaveHome1");
            cmd.AddConsoleCommand("teleportmenu.sethome2", this, "ConSaveHome2");
            cmd.AddConsoleCommand("teleportmenu.sethome", this, "ConSetHome");
            cmd.AddConsoleCommand("teleportmenu.delhome", this, "ConDelHome");
            cmd.AddConsoleCommand("teleportmenu.home", this, "ConGoHome"); // Новая команда для телепортации к дому
            cmd.AddConsoleCommand("teleportmenu.close", this, "ConCloseMenu"); // Новая команда для закрытия интерфейса

            if (config.AutoDetectMonuments)
            {
                FindMonuments();
            }
        }

        private void OnServerSave()
        {
            SaveData();
        }

        private void Unload()
        {
            SaveData();
            foreach (BasePlayer? player in BasePlayer.activePlayerList)
            {
                DestroyUI(player);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            DestroyUI(player);
            if (pendingTeleports.ContainsKey(player.userID))
            {
                pendingTeleports[player.userID]?.Destroy();
                _ = pendingTeleports.Remove(player.userID);
            }
        }

        #endregion Hooks

        #region Data Management

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/homes", homes);
        }

        private void LoadData()
        {
            // Ничего не делаем, так как поле инициализируется в конструкторе
        }

        #endregion Data Management

        #region Commands

        private void CmdTeleportMenu(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "teleportmenu.use"))
            {
                player.ChatMessage(GetMsg("NoPermission", player.UserIDString));
                return;
            }

            ShowTeleportMenu(player);
        }

        private void CmdHome(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "teleportmenu.use"))
            {
                player.ChatMessage(GetMsg("NoPermission", player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                ShowTeleportMenu(player);
                return;
            }

            string homeName = args[0];
            if (!homes.ContainsKey(player.userID) || !homes[player.userID].TryGetValue(homeName, out Vector3 value))
            {
                player.ChatMessage($"Дом '{homeName}' не найден");
                return;
            }

            InitiateTeleport(player, value, "home");
        }

        private void CmdTown(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "teleportmenu.use"))
            {
                player.ChatMessage(GetMsg("NoPermission", player.UserIDString));
                return;
            }

            if (!config.EnableTPToTown)
            {
                player.ChatMessage("Телепортация в город отключена");
                return;
            }

            InitiateTeleport(player, config.TownLocation, "town");
        }

        private void CmdCamp(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "teleportmenu.use"))
            {
                player.ChatMessage(GetMsg("NoPermission", player.UserIDString));
                return;
            }

            if (!config.EnableTPToCamp)
            {
                player.ChatMessage("Телепортация в лагерь отключена");
                return;
            }

            InitiateTeleport(player, config.CampLocation, "camp");
        }

        private void CmdTeleportRequest(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "teleportmenu.use"))
            {
                player.ChatMessage(GetMsg("NoPermission", player.UserIDString));
                return;
            }

            if (!config.EnableTPToFriends)
            {
                player.ChatMessage("Телепортация к друзьям отключена");
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage("Укажите имя игрока или ID");
                return;
            }

            BasePlayer target = FindPlayer(args[0], player);
            if (target == null)
            {
                player.ChatMessage($"Игрок '{args[0]}' не найден");
                return;
            }

            if (target == player)
            {
                player.ChatMessage("Вы не можете телепортироваться к себе");
                return;
            }

            SendTeleportRequest(player, target);
        }

        private void CmdAcceptTeleport(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "teleportmenu.use"))
            {
                player.ChatMessage(GetMsg("NoPermission", player.UserIDString));
                return;
            }

            if (!teleportRequests.ContainsKey(player.userID) || teleportRequests[player.userID].Count == 0)
            {
                player.ChatMessage(GetMsg("NoActiveRequests", player.UserIDString));
                return;
            }

            TeleportRequest request = teleportRequests[player.userID][0];
            BasePlayer requester = BasePlayer.FindByID(request.RequesterId);
            if (requester == null)
            {
                player.ChatMessage("Игрок оффлайн");
                RemoveTeleportRequest(player.userID, request);
                return;
            }

            // Проверка лимитов телепортации для запрашивающего
            if (!CheckTeleportLimits(requester, "friend"))
            {
                requester.ChatMessage("Вы достигли лимита телепортаций к друзьям");
                player.ChatMessage($"Игрок {requester.displayName} достиг лимита телепортаций");
                RemoveTeleportRequest(player.userID, request);
                return;
            }

            player.ChatMessage(GetMsg("TPRequestAccepted", player.UserIDString));
            requester.ChatMessage(GetMsg("TPRequestAccepted", requester.UserIDString));

            InitiateTeleport(requester, player.transform.position, "friend");
            RemoveTeleportRequest(player.userID, request);
        }

        private void CmdDeclineTeleport(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "teleportmenu.use"))
            {
                player.ChatMessage(GetMsg("NoPermission", player.UserIDString));
                return;
            }

            if (!teleportRequests.ContainsKey(player.userID) || teleportRequests[player.userID].Count == 0)
            {
                player.ChatMessage(GetMsg("NoActiveRequests", player.UserIDString));
                return;
            }

            TeleportRequest request = teleportRequests[player.userID][0];
            BasePlayer requester = BasePlayer.FindByID(request.RequesterId);

            player.ChatMessage(GetMsg("TPRequestDeclined", player.UserIDString));
            requester?.ChatMessage(GetMsg("TPRequestDeclined", requester.UserIDString));

            RemoveTeleportRequest(player.userID, request);
        }

        private void CmdSetHome(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "teleportmenu.use"))
            {
                player.ChatMessage(GetMsg("NoPermission", player.UserIDString));
                return;
            }

            // Проверяем, указал ли игрок название дома
            if (args.Length == 0)
            {
                player.ChatMessage("Ошибка: Вы должны указать название дома. Использование: /sethome [название]");
                return;
            }

            string homeName = args[0];

            // Проверяем, находится ли игрок на фундаменте
            bool onFoundation = IsPlayerOnFoundation(player);
            if (!onFoundation)
            {
                player.ChatMessage("Дом можно сохранить только стоя на фундаменте!");
                return;
            }

            // Проверяем, находится ли игрок в зоне своего шкафа (имеет права строительства)
            bool hasBuildingPrivilege = HasBuildingPrivilege(player);
            if (!hasBuildingPrivilege)
            {
                player.ChatMessage("Дом можно сохранить только в зоне действия вашего шкафа!");
                return;
            }

            // Сохраняем дом
            if (!homes.ContainsKey(player.userID))
            {
                homes[player.userID] = new Dictionary<string, Vector3>();
            }

            // Сохраняем позицию дома
            homes[player.userID][homeName] = player.transform.position;
            SaveData();
            player.ChatMessage($"Дом '{homeName}' установлен");

            // Перезапускаем интерфейс, чтобы отобразить новый дом
            if (player.IsReceivingSnapshot)
            {
                // Показываем UI только если игрок не в процессе загрузки
                _ = timer.Once(0.5f, () => ShowTeleportMenu(player));
            }
            else
            {
                ShowTeleportMenu(player);
            }
        }

        /// <summary>
        /// Новая чат-команда для сохранения дома с именем "дом1"
        /// </summary>
        private void CmdSaveHome(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "teleportmenu.use"))
            {
                player.ChatMessage(GetMsg("NoPermission", player.UserIDString));
                return;
            }

            // Вызываем существующий метод с предопределенным именем дома
            CmdSetHome(player, "sethome", stringArray);
        }

        /// <summary>
        /// Команда для сохранения второго дома
        /// </summary>
        private void CmdSaveHome2(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "teleportmenu.use"))
            {
                player.ChatMessage(GetMsg("NoPermission", player.UserIDString));
                return;
            }

            // Вызываем существующий метод с именем для второго дома
            CmdSetHome(player, "sethome", stringArray0);
        }

        /// <summary>
        /// Команда для удаления дома по имени
        /// </summary>
        private void CmdDelHome(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "teleportmenu.use"))
            {
                player.ChatMessage(GetMsg("NoPermission", player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage("Укажите название дома для удаления. Использование: /delhome [название]");
                return;
            }

            string homeName = args[0];
            if (!homes.ContainsKey(player.userID) || !homes[player.userID].ContainsKey(homeName))
            {
                player.ChatMessage($"Дом '{homeName}' не найден");
                return;
            }

            _ = homes[player.userID].Remove(homeName);
            SaveData();
            player.ChatMessage($"Дом '{homeName}' удален");

            // Обновляем интерфейс после удаления дома
            ShowTeleportMenu(player);
        }

        #endregion Commands

        #region UI Methods

        private void ShowTeleportMenu(BasePlayer player)
        {
            DestroyUI(player);

            CuiElementContainer container = new();

            // Панель-подложка
            _ = container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.25 0.3", AnchorMax = "0.75 0.8" },
                Image = { Color = "0 0 0 0" },
                CursorEnabled = true
            }, "Overlay", UIMainPanel);

            // Фоновое изображение для главной панели
            container.Add(new CuiElement
            {
                Parent = UIMainPanel,
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Url = "https://i.imgur.com/qvVhltR.png",
                        Color = "1 1 1 1",
                        FitToParent = true
                    },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            // Кнопка закрытия интерфейса
            _ = container.Add(new CuiButton
            {
                Button = { Color = "0.0 0.0 0.0 0.0", Command = "teleportmenu.close" },
                RectTransform = { AnchorMin = "0.95 0.94", AnchorMax = "1 1" },
                Text = { Text = "", Align = TextAnchor.MiddleCenter, Color = config.TextColor, FontSize = 18 }
            }, UIMainPanel);

            // Добавляем кнопки принять/отклонить телепортацию в верхней части интерфейса
            _ = container.Add(new CuiButton
            {
                Button = { Color = "0.0 0.0 0.0 0.0", Command = "chat.say /tpa", FadeIn = 0.2f },
                RectTransform = { AnchorMin = "0.75 0.8", AnchorMax = "0.9 0.88" },
                Text = { Text = "", Align = TextAnchor.MiddleCenter, Color = config.TextColor, FontSize = 14 }
            }, UIMainPanel, "AcceptTpButton");

            _ = container.Add(new CuiButton
            {
                Button = { Color = "0.0 0.0 0.0 0.0", Command = "chat.say /tpdecline", FadeIn = 0.2f },
                RectTransform = { AnchorMin = "0.519 0.275", AnchorMax = "0.703 0.325" },
                Text = { Text = "", Align = TextAnchor.MiddleCenter, Color = config.TextColor, FontSize = 14 }
            }, UIMainPanel, "DeclineTpButton");

            // Пустая нижняя панель (только для сохранения структуры и корректной работы)
            _ = container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.1", AnchorMax = "1 0.63" },
                Image = { Color = "0 0 0 0" } // Полностью прозрачная
            }, UIMainPanel, "HomesPanel");

            // Получаем информацию о телепортациях для отображения в нижней части меню
            string townLimitText = GetTeleportLimitText(player, "town");
            string homeLimitText = GetTeleportLimitText(player, "home");
            string campLimitText = GetTeleportLimitText(player, "camp");
            string friendLimitText = GetTeleportLimitText(player, "friend");

            // Добавляем кнопку телепортации в город (Outpost)
            const string townPanelName = "TownButton";
            CuiPanel townPanel = new()
            {
                RectTransform = { AnchorMin = "0.295 0.60", AnchorMax = "0.485 0.65" },
                Image = { Color = "0 0 0 0" },
                CursorEnabled = true
            };
            _ = container.Add(townPanel, UIMainPanel, townPanelName);

            // Добавляем прозрачную кнопку для обработки клика в город с подсветкой
            _ = container.Add(new CuiButton
            {
                Button = { Color = "0.0 0.0 0.0 0.0", Command = "chat.say /town", FadeIn = 0.2f },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { Text = "", Align = TextAnchor.MiddleCenter, Color = config.TextColor, FontSize = 14 }
            }, townPanelName);

            // Добавляем кнопку телепортации в лагерь бандитов (Bandit Camp)
            const string campPanelName = "CampButton";
            CuiPanel campPanel = new()
            {
                RectTransform = { AnchorMin = "0.519 0.60", AnchorMax = "0.704 0.65" },
                Image = { Color = "0 0 0 0" },
                CursorEnabled = true
            };
            _ = container.Add(campPanel, UIMainPanel, campPanelName);

            // Добавляем прозрачную кнопку для обработки клика в лагерь с подсветкой
            _ = container.Add(new CuiButton
            {
                Button = { Color = "0.0 0.0 0.0 0.0", Command = "chat.say /camp", FadeIn = 0.2f },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { Text = "", Align = TextAnchor.MiddleCenter, Color = config.TextColor, FontSize = 14 }
            }, campPanelName);

            // Создаем кнопки быстрого доступа к домам с фиксированным положением
            if (homes.ContainsKey(player.userID) && homes[player.userID].Count > 0)
            {
                int count = 0;

                foreach (KeyValuePair<string, Vector3> home in homes[player.userID])
                {
                    if (count >= 10)
                    {
                        break; // Увеличиваем лимит с 3 до 10
                    }

                    // Используем фиксированные координаты для кнопок, расположенных вертикально
                    string anchorMin, anchorMax;
                    if (count == 0)
                    {
                        // Первая кнопка на месте "Сохранить текущее место как дом"
                        anchorMin = "0.06 0.70";
                        anchorMax = "0.265 0.77";
                    }
                    else if (count == 1)
                    {
                        // Вторая кнопка ниже первой
                        anchorMin = "0.06 0.62";
                        anchorMax = "0.265 0.69";
                    }
                    else if (count == 2)
                    {
                        // Третья кнопка еще ниже
                        anchorMin = "0.06 0.54";
                        anchorMax = "0.265 0.61";
                    }
                    else if (count == 3)
                    {
                        // Четвертая кнопка
                        anchorMin = "0.06 0.46";
                        anchorMax = "0.265 0.53";
                    }
                    else if (count == 4)
                    {
                        // Пятая кнопка
                        anchorMin = "0.06 0.38";
                        anchorMax = "0.265 0.45";
                    }
                    else if (count == 5)
                    {
                        // Шестая кнопка
                        anchorMin = "0.06 0.30";
                        anchorMax = "0.265 0.37";
                    }
                    else if (count == 6)
                    {
                        // Седьмая кнопка
                        anchorMin = "0.06 0.22";
                        anchorMax = "0.265 0.29";
                    }
                    else if (count == 7)
                    {
                        // Восьмая кнопка
                        anchorMin = "0.06 0.14";
                        anchorMax = "0.265 0.21";
                    }
                    else if (count == 8)
                    {
                        // Девятая кнопка
                        anchorMin = "0.39 0.70";
                        anchorMax = "0.59 0.77";
                    }
                    else // count == 9
                    {
                        // Десятая кнопка
                        anchorMin = "0.39 0.62";
                        anchorMax = "0.59 0.69";
                    }

                    string panelName = $"QuickHome_{count}";

                    // Создаем кнопку телепортации к дому
                    CuiPanel homePanel = new()
                    {
                        RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax },
                        Image = { Color = "0 0 0 0" }
                    };
                    _ = container.Add(homePanel, UIMainPanel, panelName);

                    // Добавляем фоновое изображение
                    container.Add(new CuiElement
                    {
                        Parent = panelName,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Url = "https://i.imgur.com/rNv8SmY.png",
                                Color = "1 1 1 1",
                                FitToParent = true
                            },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                        }
                    });

                    // Добавляем текст кнопки
                    container.Add(new CuiElement
                    {
                        Parent = panelName,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = $"Телепорт: {home.Key}",
                                Font = "robotocondensed-bold.ttf",
                                FontSize = 14,
                                Align = TextAnchor.MiddleCenter,
                                Color = config.TextColor
                            },
                            new CuiRectTransformComponent { AnchorMin = "0.05 0.1", AnchorMax = "0.95 0.9" }
                        }
                    });

                    // Добавляем прозрачную кнопку для обработки клика
                    _ = container.Add(new CuiButton
                    {
                        Button = { Color = "0 0 0 0", Command = $"teleportmenu.home {home.Key}" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Text = { Text = "" }
                    }, panelName);

                    // Создаем кнопку удаления дома справа от кнопки телепортации
                    string deleteAnchorMin, deleteAnchorMax;
                    if (count == 0)
                    {
                        deleteAnchorMin = "0.27 0.70";
                        deleteAnchorMax = "0.37 0.77";
                    }
                    else if (count == 1)
                    {
                        deleteAnchorMin = "0.27 0.62";
                        deleteAnchorMax = "0.37 0.69";
                    }
                    else if (count == 2)
                    {
                        deleteAnchorMin = "0.27 0.54";
                        deleteAnchorMax = "0.37 0.61";
                    }
                    else if (count == 3)
                    {
                        deleteAnchorMin = "0.27 0.46";
                        deleteAnchorMax = "0.37 0.53";
                    }
                    else if (count == 4)
                    {
                        deleteAnchorMin = "0.27 0.38";
                        deleteAnchorMax = "0.37 0.45";
                    }
                    else if (count == 5)
                    {
                        deleteAnchorMin = "0.27 0.30";
                        deleteAnchorMax = "0.37 0.37";
                    }
                    else if (count == 6)
                    {
                        deleteAnchorMin = "0.27 0.22";
                        deleteAnchorMax = "0.37 0.29";
                    }
                    else if (count == 7)
                    {
                        deleteAnchorMin = "0.27 0.14";
                        deleteAnchorMax = "0.37 0.21";
                    }
                    else if (count == 8)
                    {
                        deleteAnchorMin = "0.39 0.70";
                        deleteAnchorMax = "0.49 0.77";
                    }
                    else // count == 9
                    {
                        deleteAnchorMin = "0.39 0.62";
                        deleteAnchorMax = "0.49 0.69";
                    }

                    // Добавляем кнопку удаления
                    _ = container.Add(new CuiButton
                    {
                        Button = { Color = "0.6 0.2 0.2 0.8", Command = $"teleportmenu.delhome {home.Key}", FadeIn = 0.2f },
                        RectTransform = { AnchorMin = deleteAnchorMin, AnchorMax = deleteAnchorMax },
                        Text = { Text = "X", Align = TextAnchor.MiddleCenter, Color = config.TextColor, FontSize = 14 }
                    }, UIMainPanel, $"DeleteHome_{count}");

                    count++;
                }

                // Определяем позицию для кнопки создания дома - всегда ниже последней кнопки телепортации
                string saveButtonAnchorMin, saveButtonAnchorMax;

                // В зависимости от количества домов, смещаем вниз
                if (count == 0)
                {
                    saveButtonAnchorMin = "0.06 0.70";
                    saveButtonAnchorMax = "0.265 0.77";
                }
                else if (count == 1)
                {
                    saveButtonAnchorMin = "0.06 0.62";
                    saveButtonAnchorMax = "0.265 0.69";
                }
                else if (count == 2)
                {
                    saveButtonAnchorMin = "0.06 0.54";
                    saveButtonAnchorMax = "0.265 0.61";
                }
                else if (count == 3)
                {
                    saveButtonAnchorMin = "0.06 0.46";
                    saveButtonAnchorMax = "0.265 0.53";
                }
                else if (count == 4)
                {
                    saveButtonAnchorMin = "0.06 0.38";
                    saveButtonAnchorMax = "0.265 0.45";
                }
                else if (count == 5)
                {
                    saveButtonAnchorMin = "0.06 0.30";
                    saveButtonAnchorMax = "0.265 0.37";
                }
                else if (count == 6)
                {
                    saveButtonAnchorMin = "0.06 0.22";
                    saveButtonAnchorMax = "0.265 0.29";
                }
                else if (count == 7)
                {
                    saveButtonAnchorMin = "0.06 0.14";
                    saveButtonAnchorMax = "0.265 0.21";
                }
                else if (count == 8)
                {
                    saveButtonAnchorMin = "0.39 0.70";
                    saveButtonAnchorMax = "0.59 0.77";
                }
                else // count == 9
                {
                    saveButtonAnchorMin = "0.39 0.62";
                    saveButtonAnchorMax = "0.59 0.69";
                }

                // Создаем кнопку "Сохранить текущее место как дом" на новой позиции
                CuiPanel saveHomePanel = new()
                {
                    RectTransform = { AnchorMin = saveButtonAnchorMin, AnchorMax = saveButtonAnchorMax },
                    Image = { Color = "0 0 0 0" },
                    CursorEnabled = true
                };
                _ = container.Add(saveHomePanel, UIMainPanel, "SaveHomePanel");

                // Добавляем фоновое изображение для кнопки
                container.Add(new CuiElement
                {
                    Parent = "SaveHomePanel",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Url = "https://i.imgur.com/rNv8SmY.png",
                            Color = "1 1 1 1",
                            FitToParent = true
                        },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                    }
                });

                // Добавляем текст
                container.Add(new CuiElement
                {
                    Parent = "SaveHomePanel",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "Сохранить текущее место как дом",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 14,
                            Align = TextAnchor.MiddleCenter,
                            Color = config.TextColor
                        },
                        new CuiRectTransformComponent { AnchorMin = "0.05 0.1", AnchorMax = "0.95 0.9" }
                    }
                });

                // Генерируем уникальное имя для нового дома
                string newHomeName = $"дом_{DateTime.Now:HHmmss}";

                // Добавляем кнопку поверх (прозрачную, только для обработки клика)
                _ = container.Add(new CuiButton
                {
                    Button = { Color = "0.4 0.4 0.4 0.1", Command = $"teleportmenu.sethome {newHomeName}", FadeIn = 0.2f },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text = { Text = "" }
                }, "SaveHomePanel");
            }
            else
            {
                // Если нет домов, размещаем кнопку создания дома на стандартной позиции
                CuiPanel saveHomePanel = new()
                {
                    RectTransform = { AnchorMin = "0.06 0.70", AnchorMax = "0.265 0.77" },
                    Image = { Color = "0 0 0 0" }
                };
                _ = container.Add(saveHomePanel, UIMainPanel, "SaveHomePanel");

                // Добавляем фоновое изображение для кнопки
                container.Add(new CuiElement
                {
                    Parent = "SaveHomePanel",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Url = "https://i.imgur.com/rNv8SmY.png",
                            Color = "1 1 1 1",
                            FitToParent = true
                        },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                    }
                });

                // Добавляем текст
                container.Add(new CuiElement
                {
                    Parent = "SaveHomePanel",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "Сохранить текущее место как дом",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 14,
                            Align = TextAnchor.MiddleCenter,
                            Color = config.TextColor
                        },
                        new CuiRectTransformComponent { AnchorMin = "0.05 0.1", AnchorMax = "0.95 0.9" }
                    }
                });

                // Генерируем уникальное имя для нового дома
                string newHomeName = $"дом_{DateTime.Now:HHmmss}";

                // Добавляем кнопку поверх (прозрачную, только для обработки клика)
                _ = container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = $"teleportmenu.sethome {newHomeName}" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text = { Text = "" }
                }, "SaveHomePanel");
            }

            // Отображение запросов телепортации
            if (teleportRequests.ContainsKey(player.userID) && teleportRequests[player.userID].Count > 0)
            {
                TeleportRequest request = teleportRequests[player.userID][0];
                BasePlayer requester = BasePlayer.FindByID(request.RequesterId);
                if (requester != null)
                {
                    // Панель запроса - делаем полупрозрачной
                    _ = container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0.2 0.4", AnchorMax = "0.8 0.7" },
                        Image = { Color = "0.1 0.1 0.1 0.9" }
                    }, UIMainPanel, "RequestPanel");

                    // Текст запроса
                    container.Add(new CuiElement
                    {
                        Parent = "RequestPanel",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = $"Игрок {requester.displayName} хочет телепортироваться к вам",
                                Font = "robotocondensed-regular.ttf",
                                FontSize = 14,
                                Align = TextAnchor.MiddleCenter,
                                Color = config.TextColor
                            },
                            new CuiRectTransformComponent { AnchorMin = "0.1 0.6", AnchorMax = "0.9 0.9" }
                        }
                    });

                    // Кнопки принять/отклонить - делаем полупрозрачными
                    _ = container.Add(new CuiButton
                    {
                        Button = { Color = "0.2 0.6 0.2 0.8", Command = "tpa" },
                        RectTransform = { AnchorMin = "0.1 0.2", AnchorMax = "0.45 0.4" },
                        Text = { Text = "ПРИНЯТЬ", Align = TextAnchor.MiddleCenter, Color = config.TextColor, FontSize = 14 }
                    }, "RequestPanel", UIAcceptButton);

                    _ = container.Add(new CuiButton
                    {
                        Button = { Color = "0.6 0.2 0.2 0.8", Command = "tpdecline" },
                        RectTransform = { AnchorMin = "0.55 0.2", AnchorMax = "0.9 0.4" },
                        Text = { Text = "ОТКЛОНИТЬ", Align = TextAnchor.MiddleCenter, Color = config.TextColor, FontSize = 14 }
                    }, "RequestPanel", UIDeclineButton);
                }
            }

            // Добавляем информационную панель внизу меню с лимитами телепортаций
            // Создаем 4 панели для отображения лимитов телепортаций
            // Панель для лимита телепортаций в город
            const string townLimitPanelName = "TownLimitPanel";
            CuiPanel townLimitPanel = new()
            {
                RectTransform = { AnchorMin = "0.43 0.112", AnchorMax = "0.47 0.162" },
                Image = { Color = "0.0 0.0 0.0 0.0" }
            };
            _ = container.Add(townLimitPanel, UIMainPanel, townLimitPanelName);

            // Добавляем текст с информацией о лимитах телепортаций в город
            container.Add(new CuiElement
            {
                Parent = townLimitPanelName,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $" {townLimitText}",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 16,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 0.9"
                    },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            // Панель для лимита телепортаций домой
            const string homeLimitPanelName = "HomeLimitPanel";
            CuiPanel homeLimitPanel = new()
            {
                RectTransform = { AnchorMin = "0.625 0.112", AnchorMax = "0.675 0.162" },
                Image = { Color = "0.0 0.0 0.0 0.0" }
            };
            _ = container.Add(homeLimitPanel, UIMainPanel, homeLimitPanelName);

            // Добавляем текст с информацией о лимитах телепортаций домой
            container.Add(new CuiElement
            {
                Parent = homeLimitPanelName,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $" {homeLimitText}",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 16,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 0.9"
                    },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            // Панель для лимита телепортаций в лагерь
            const string campLimitPanelName = "CampLimitPanel";
            CuiPanel campLimitPanel = new()
            {
                RectTransform = { AnchorMin = "0.43 0.025", AnchorMax = "0.47 0.075" },
                Image = { Color = "0.0 0.0 0.0 0.0" }
            };
            _ = container.Add(campLimitPanel, UIMainPanel, campLimitPanelName);

            // Добавляем текст с информацией о лимитах телепортаций в лагерь
            container.Add(new CuiElement
            {
                Parent = campLimitPanelName,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $" {campLimitText}",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 16,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 0.9"
                    },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            // Панель для лимита телепортаций к другу
            const string friendLimitPanelName = "FriendLimitPanel";
            CuiPanel friendLimitPanel = new()
            {
                RectTransform = { AnchorMin = "0.625 0.025", AnchorMax = "0.675 0.075" },
                Image = { Color = "0.0 0.0 0.0 0.0" }
            };
            _ = container.Add(friendLimitPanel, UIMainPanel, friendLimitPanelName);

            // Добавляем текст с информацией о лимитах телепортаций к другу
            container.Add(new CuiElement
            {
                Parent = friendLimitPanelName,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $" {friendLimitText}",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 16,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 0.9"
                    },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            _ = CuiHelper.AddUi(player, container);
        }

        /// <summary>
        /// Метод для получения текста о лимите телепортаций в формате "использовано/максимум"
        /// </summary>
        private string GetTeleportLimitText(BasePlayer player, string teleportType)
        {
            if (!teleportData.ContainsKey(player.userID))
            {
                teleportData[player.userID] = new TeleportData();
            }

            TeleportData data = teleportData[player.userID];
            string permGroup = GetPlayerPermissionGroup(player);

            if (!config.PermissionLimits.ContainsKey(permGroup))
            {
                permGroup = "default";
            }

            TeleportLimits limits = config.PermissionLimits[permGroup];
            int currentCount = data.TeleportCounts.TryGetValue(teleportType, out int value) ? value : 0;
            int limit = GetLimitForType(limits, teleportType);

            return $"{currentCount}/{limit}";
        }

        /// <summary>
        /// Метод для получения оставшихся телепортаций
        /// </summary>
        private int GetRemainingTeleports(BasePlayer player, string teleportType)
        {
            if (!teleportData.ContainsKey(player.userID))
            {
                teleportData[player.userID] = new TeleportData();
            }

            TeleportData data = teleportData[player.userID];
            string permGroup = GetPlayerPermissionGroup(player);

            if (!config.PermissionLimits.ContainsKey(permGroup))
            {
                permGroup = "default";
            }

            TeleportLimits limits = config.PermissionLimits[permGroup];
            int currentCount = data.TeleportCounts.TryGetValue(teleportType, out int value) ? value : 0;
            int limit = GetLimitForType(limits, teleportType);

            return Math.Max(0, limit - currentCount);
        }

        /// <summary>
        /// Метод для получения использованных телепортаций
        /// </summary>
        private int GetUsedTPCount(TeleportData data, string teleportType)
        {
            return data.TeleportCounts.TryGetValue(teleportType, out int value) ? value : 0;
        }

        /// <summary>
        /// Метод для сброса счетчиков телепортаций
        /// </summary>
        private void ResetPlayerTeleportCounts(ulong playerId)
        {
            if (teleportData.ContainsKey(playerId))
            {
                if (!teleportData[playerId].TeleportCounts.ContainsKey("home"))
                {
                    teleportData[playerId].TeleportCounts["home"] = 0;
                }
                else
                {
                    teleportData[playerId].TeleportCounts["home"] = 0;
                }

                if (!teleportData[playerId].TeleportCounts.ContainsKey("town"))
                {
                    teleportData[playerId].TeleportCounts["town"] = 0;
                }
                else
                {
                    teleportData[playerId].TeleportCounts["town"] = 0;
                }

                if (!teleportData[playerId].TeleportCounts.ContainsKey("camp"))
                {
                    teleportData[playerId].TeleportCounts["camp"] = 0;
                }
                else
                {
                    teleportData[playerId].TeleportCounts["camp"] = 0;
                }

                if (!teleportData[playerId].TeleportCounts.ContainsKey("friend"))
                {
                    teleportData[playerId].TeleportCounts["friend"] = 0;
                }
                else
                {
                    teleportData[playerId].TeleportCounts["friend"] = 0;
                }
            }
        }

        /// <summary>
        /// Метод для сброса всех счетчиков
        /// </summary>
        private void ResetAllTeleportCounts()
        {
            foreach (KeyValuePair<ulong, TeleportData> entry in teleportData)
            {
                ResetPlayerTeleportCounts(entry.Key);
            }
            SaveTeleportData();
            Puts("Ежедневный сброс счетчиков телепортаций выполнен");
        }

        /// <summary>
        /// Консольная команда для сброса счетчиков телепортаций
        /// </summary>
        private void ConResetTeleportCounts(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
            {
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, "teleportmenu.admin"))
            {
                player.ChatMessage(GetMsg("NoPermission", player.UserIDString));
                return;
            }

            if (arg.HasArgs(1) && arg.GetString(0).IsSteamId())
            {
                ulong targetId = ulong.Parse(arg.GetString(0));
                ResetPlayerTeleportCounts(targetId);
                player.ChatMessage($"Счетчики телепортаций для игрока {targetId} сброшены.");
            }
            else if (arg.HasArgs(1) && arg.GetString(0).Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                foreach (KeyValuePair<ulong, TeleportData> entry in teleportData)
                {
                    ResetPlayerTeleportCounts(entry.Key);
                }
                player.ChatMessage("Счетчики телепортаций сброшены для всех игроков.");
            }
            else
            {
                ResetPlayerTeleportCounts(player.userID);
                player.ChatMessage("Ваши счетчики телепортаций сброшены.");
            }

            SaveTeleportData();
        }

        private void SaveTeleportData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/teleport_data", teleportData);
        }

        private void LoadTeleportData()
        {
            try
            {
                teleportData.Clear();
                Dictionary<ulong, TeleportData> savedData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, TeleportData>>($"{Name}/teleport_data");
                if (savedData != null)
                {
                    foreach (KeyValuePair<ulong, TeleportData> data in savedData)
                    {
                        teleportData[data.Key] = data.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                PrintError($"Ошибка загрузки данных телепортации: {ex.Message}");
            }
        }

        private void CreateHomeButtons(CuiElementContainer container, BasePlayer player)
        {
            _ = container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.05 0.1", AnchorMax = "0.95 0.9" },
                Image = { Color = "0 0 0 0" }
            }, UIHomePanel, UIHomeButtonsPanel);

            if (!homes.ContainsKey(player.userID))
            {
                homes[player.userID] = new Dictionary<string, Vector3>();
            }

            // Show existing homes
            int maxHomes = config.MaxHomesPerPlayer;
            for (int i = 0; i < maxHomes; i++)
            {
                string homeName = $"home{i + 1}";
                string buttonText;
                string buttonCommand;

                if (homes[player.userID].ContainsKey(homeName))
                {
                    buttonText = homeName;
                    buttonCommand = $"home {homeName}";
                }
                else
                {
                    buttonText = GetMsg("Save", player.UserIDString);
                    buttonCommand = $"teleportmenu.sethome {homeName}";
                }

                float yMin = 0.9f - ((i + 1) * 0.2f);
                float yMax = 0.9f - (i * 0.2f);

                _ = container.Add(new CuiButton
                {
                    Button = { Color = config.ButtonColor, Command = buttonCommand },
                    RectTransform = { AnchorMin = $"0.1 {yMin}", AnchorMax = $"0.9 {yMax}" },
                    Text = { Text = buttonText, Align = TextAnchor.MiddleCenter, Color = config.TextColor, FontSize = 16 }
                }, UIHomeButtonsPanel);
            }
        }

        private void DestroyUI(BasePlayer player)
        {
            _ = CuiHelper.DestroyUi(player, UIMainPanel);
        }

        #endregion UI Methods

        #region Teleportation Methods

        private void InitiateTeleport(BasePlayer player, Vector3 destination)
        {
            InitiateTeleport(player, destination, "");
        }

        private void InitiateTeleport(BasePlayer player, Vector3 destination, string teleportType)
        {
            if (!CanTeleport(player))
            {
                return;
            }

            // Проверяем лимиты телепортаций если тип задан
            if (!string.IsNullOrEmpty(teleportType) && !CheckTeleportLimits(player, teleportType))
            {
                return;
            }

            if (pendingTeleports.ContainsKey(player.userID))
            {
                pendingTeleports[player.userID]?.Destroy();
            }

            player.ChatMessage(string.Format(GetMsg("TeleportCountdown", player.UserIDString), config.TeleportDelay));
            DestroyUI(player);

            pendingTeleports[player.userID] = timer.Once(config.TeleportDelay, () =>
            {
                DoTeleport(player, destination, teleportType);
                _ = pendingTeleports.Remove(player.userID);
            });
        }

        private bool CanTeleport(BasePlayer player)
        {
            if (player == null)
            {
                return false;
            }

            // Проверяем активные разрешения игрока
            if (!teleportData.ContainsKey(player.userID))
            {
                teleportData[player.userID] = new TeleportData();
            }

            TeleportData data = teleportData[player.userID];
            double timeLeft = config.TeleportCooldown - (DateTime.Now - data.LastTeleport).TotalSeconds;

            if (timeLeft > 0)
            {
                player.ChatMessage(string.Format(GetMsg("CooldownMessage", player.UserIDString), Math.Ceiling(timeLeft)));
                return false;
            }

            return true;
        }

        private bool CheckTeleportLimits(BasePlayer player, string teleportType)
        {
            if (player == null)
            {
                return false;
            }

            // Если игрок админ или имеет админ-разрешение, не проверяем лимиты
            if (player.IsAdmin || permission.UserHasPermission(player.UserIDString, "teleportmenu.admin"))
            {
                return true;
            }

            if (!teleportData.ContainsKey(player.userID))
            {
                teleportData[player.userID] = new TeleportData();
            }

            TeleportData data = teleportData[player.userID];
            string permGroup = GetPlayerPermissionGroup(player);

            if (!config.PermissionLimits.ContainsKey(permGroup))
            {
                permGroup = "default"; // Если нет подходящей группы, используем default
            }

            TeleportLimits limits = config.PermissionLimits[permGroup];
            int currentCount = data.TeleportCounts.TryGetValue(teleportType, out int value) ? value : 0;
            int limit = GetLimitForType(limits, teleportType);

            if (currentCount >= limit)
            {
                player.ChatMessage($"Вы достигли лимита телепортаций ({limit}) для типа: {GetTeleportTypeName(teleportType)}");
                return false;
            }

            return true;
        }

        private string GetTeleportTypeName(string teleportType)
        {
            return teleportType switch
            {
                "home" => "домой",
                "town" => "в город",
                "camp" => "в лагерь бандитов",
                "friend" => "к другу",
                _ => teleportType,
            };
        }

        private int GetLimitForType(TeleportLimits limits, string teleportType)
        {
            return teleportType switch
            {
                "home" => limits.Home,
                "town" => limits.Town,
                "camp" => limits.Camp,
                "friend" => limits.Friend,
                _ => 0,
            };
        }

        private string GetPlayerPermissionGroup(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, "teleportmenu.premium"))
            {
                return "premium";
            }
            if (permission.UserHasPermission(player.UserIDString, "teleportmenu.vip"))
            {
                return "vip";
            }
            if (permission.UserHasPermission(player.UserIDString, "teleportmenu.default"))
            {
                return "default";
            }

            return "default"; // По умолчанию
        }

        private void DoTeleport(BasePlayer player, Vector3 destination)
        {
            DoTeleport(player, destination, "");
        }

        private void DoTeleport(BasePlayer player, Vector3 destination, string teleportType)
        {
            if (player?.IsConnected != true)
            {
                return;
            }

            // Получаем начальную позицию для логирования
            Vector3 startPos = player.transform.position;

            // Ищем безопасную позицию для телепортации (проверяем землю под точкой назначения)
            Vector3 safePosition = GetSafePosition(destination);

            // Логируем информацию о телепортации
            Puts($"Телепортация игрока {player.displayName} из {startPos} в {destination}, безопасная позиция: {safePosition}");

            player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.ClientRPCPlayer(null, player, "StartLoading");
            player.MovePosition(safePosition);

            // Первый таймер для перемещения игрока
            _ = timer.Once(0.1f, () =>
            {
                player.ClientRPCPlayer(null, player, "ForcePositionTo", safePosition);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, false);
                player.ClientRPCPlayer(null, player, "FinishLoading");
                player.ChatMessage(GetMsg("TeleportSuccess", player.UserIDString));

                if (!teleportData.ContainsKey(player.userID))
                {
                    teleportData[player.userID] = new TeleportData();
                }

                teleportData[player.userID].LastTeleport = DateTime.Now;
                teleportData[player.userID].TeleportCount++;

                // Увеличиваем счетчик для конкретного типа телепортации
                if (!string.IsNullOrEmpty(teleportType))
                {
                    if (!teleportData[player.userID].TeleportCounts.TryGetValue(teleportType, out int value))
                    {
                        value = 0;
                        teleportData[player.userID].TeleportCounts[teleportType] = value;
                    }
                    teleportData[player.userID].TeleportCounts[teleportType] = ++value;
                }

                // Запускаем дополнительную проверку для гарантии безопасной позиции на земле
                EnsurePlayerOnGround(player);
            });
        }

        /// <summary>
        /// Дополнительный метод проверки и коррекции позиции игрока для гарантии приземления
        /// </summary>
        /// <param name="player">Игрок для проверки</param>
        private void EnsurePlayerOnGround(BasePlayer player)
        {
            if (player?.IsConnected != true)
            {
                return;
            }

            // Подождем 0.5 секунды после телепортации, чтобы игровая физика обработала перемещение
            _ = timer.Once(0.5f, () =>
            {
                if (player?.IsConnected != true)
                {
                    return;
                }

                Vector3 playerPos = player.transform.position;
                // Проверяем, что игрок находится на земле, а не в воздухе
                bool isGrounded = false;

                // Используем различные методы проверки
                // 1. Проверка через IsOnGround метод BasePlayer
                if (player.IsOnGround())
                {
                    isGrounded = true;
                    Puts($"Проверка EnsurePlayerOnGround: игрок {player.displayName} на земле (IsOnGround)");
                }

                // 2. Проверка через raycast вниз на малое расстояние
                if (!isGrounded)
                {
                    if (Physics.Raycast(playerPos + new Vector3(0, 0.1f, 0), Vector3.down, out RaycastHit hit, 0.3f, LayerMask.GetMask("Terrain", "World", "Construction", "Deployed")))
                    {
                        isGrounded = true;
                        Puts($"Проверка EnsurePlayerOnGround: игрок {player.displayName} на земле (short raycast)");
                    }
                }

                // Если игрок не на земле, пробуем найти землю и переместить его на неё
                if (!isGrounded)
                {
                    Puts($"Обнаружено падение после телепортации для {player.displayName}, выполняем коррекцию");

                    // Используем raycast для поиска земли прямо под игроком
                    if (Physics.Raycast(playerPos, Vector3.down, out RaycastHit hit, 200f, LayerMask.GetMask("Terrain", "World", "Construction", "Deployed")))
                    {
                        // Нашли землю под игроком, переместим его туда плавно
                        Vector3 groundPos = hit.point + new Vector3(0, 0.2f, 0); // Чуть выше поверхности

                        Puts($"Найдена земля под игроком на расстоянии {hit.distance}, перемещаем на позицию {groundPos}");

                        // Отключим урон от падения на время перемещения
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);

                        // Переместим на безопасную позицию
                        player.MovePosition(groundPos);
                        player.ClientRPCPlayer(null, player, "ForcePositionTo", groundPos);

                        // Сбросим флаг через небольшую задержку
                        _ = timer.Once(0.2f, () =>
                        {
                            if (player?.IsConnected == true)
                            {
                                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, false);
                                Puts($"Коррекция позиции игрока {player.displayName} завершена успешно");
                            }
                        });
                    }
                    else
                    {
                        Puts($"Не удалось найти землю под игроком {player.displayName} для коррекции позиции");
                    }
                }
            });
        }

        /// <summary>
        /// Находит безопасную позицию для телепортации, проверяя наличие земли под указанной точкой
        /// </summary>
        /// <param name="destination">Желаемая точка назначения</param>
        /// <returns>Безопасная позиция для телепортации</returns>
        private Vector3 GetSafePosition(Vector3 destination)
        {
            Vector3 safePos = destination;

            // Вначале пытаемся получить высоту земли в точке назначения для определения безопасной высоты
            float terrainHeight = TerrainMeta.HeightMap.GetHeight(new Vector3(destination.x, 0, destination.z));
            if (terrainHeight > 0)
            {
                // Если нашли высоту земли, устанавливаем Y-координату чуть выше земли
                safePos.y = terrainHeight + 0.5f;
                Puts($"Определена высота по карте высот: {safePos.y}");
            }

            // Проверка на водную поверхность и подземную область
            if (WaterLevel.GetWaterDepth(destination, true, true, null) > 0.1f || destination.y < 0f)
            {
                // Пытаемся найти ближайшую сухую позицию
                for (float radius = 5f; radius <= 20f; radius += 5f)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * (360f / 8f);
                        Vector3 checkPos = destination + (new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad), 0f, Mathf.Cos(angle * Mathf.Deg2Rad)) * radius);
                        
                        // Проверяем высоту земли для этой позиции
                        float checkTerrainHeight = TerrainMeta.HeightMap.GetHeight(checkPos);
                        if (checkTerrainHeight > 0)
                        {
                            checkPos.y = checkTerrainHeight + 0.5f;
                        }
                        
                        // Проверяем, что в этой позиции нет воды
                        if (WaterLevel.GetWaterDepth(checkPos, true, true, null) < 0.1f && checkPos.y > 0f)
                        {
                            safePos = checkPos;
                            Puts($"Найдена безопасная позиция вне воды на расстоянии {radius}м от назначения");
                            return safePos;
                        }
                    }
                }
            }

            // Дополнительная проверка с raycast для определения высоты земли
            // Начинаем проверку с высоты 200 метров вниз
            if (Physics.Raycast(new Vector3(safePos.x, 200f, safePos.z), Vector3.down, out RaycastHit hitInfo, 400f, LayerMask.GetMask("Terrain", "World", "Construction", "Deployed")))
            {
                safePos.y = hitInfo.point.y + 0.5f; // Устанавливаем высоту немного выше земли
                Puts($"Скорректирована высота телепортации по поверхности: {safePos.y}");
            }
            else
            {
                // Пробуем еще один raycast с большей высоты, если первый не сработал
                if (Physics.Raycast(new Vector3(safePos.x, 500f, safePos.z), Vector3.down, out hitInfo, 1000f, LayerMask.GetMask("Terrain", "World")))
                {
                    safePos.y = hitInfo.point.y + 0.5f;
                    Puts($"Найдена земля с большой высоты: {safePos.y}");
                }
                else
                {
                    // Если все методы не сработали, принудительно устанавливаем позицию на уровне карты высот
                    // или минимальную высоту, если карта высот не дала результатов
                    float backupHeight = TerrainMeta.HeightMap.GetHeight(safePos);
                    if (backupHeight > 0)
                    {
                        safePos.y = backupHeight + 0.5f;
                        Puts($"Резервное определение высоты по карте высот: {safePos.y}");
                    }
                    else
                    {
                        // Используем высоту из базовой конфигурации, но не более 10 метров
                        safePos.y = Math.Min(Math.Max(safePos.y, 1f), 10f);
                        Puts($"Принудительная коррекция высоты в безопасном диапазоне: {safePos.y}");
                    }
                }
            }

            // Проверка на наличие препятствий в точке телепортации
            bool hasObstacle = Physics.CheckSphere(safePos + Vector3.up, 1f, LayerMask.GetMask("Construction", "Deployed", "World"));
            if (hasObstacle)
            {
                Puts("Обнаружено препятствие в точке телепортации, попытка найти безопасное место");
                // Пытаемся найти безопасное место рядом
                for (float radius = 2f; radius <= 10f; radius += 2f)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * (360f / 8f);
                        Vector3 checkPos = safePos + (new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad), 0f, Mathf.Cos(angle * Mathf.Deg2Rad)) * radius);
                        
                        // Получаем высоту земли
                        float checkTerrainHeight = TerrainMeta.HeightMap.GetHeight(checkPos);
                        if (checkTerrainHeight > 0)
                        {
                            checkPos.y = checkTerrainHeight + 0.5f;
                        }
                        else if (Physics.Raycast(checkPos + Vector3.up * 200f, Vector3.down, out RaycastHit hit, 400f, LayerMask.GetMask("Terrain", "World")))
                        {
                            checkPos.y = hit.point.y + 0.5f;
                        }
                        
                        // Проверяем, нет ли препятствий
                        if (!Physics.CheckSphere(checkPos + Vector3.up, 1f, LayerMask.GetMask("Construction", "Deployed", "World")))
                        {
                            safePos = checkPos;
                            Puts($"Найдено безопасное место на расстоянии {radius}м от назначения");
                            return safePos;
                        }
                    }
                }
            }
            
            // Если мы все еще под уровнем воды, принудительно поднимаем позицию
            if (safePos.y < 1f)
            {
                safePos.y = 1f;
                Puts("Аварийная коррекция высоты под ватерлинией");
            }

            // Ограничиваем максимальную высоту 10 метрами над уровнем земли
            float finalTerrainHeight = TerrainMeta.HeightMap.GetHeight(safePos);
            if (finalTerrainHeight > 0 && safePos.y > finalTerrainHeight + 10f)
            {
                safePos.y = finalTerrainHeight + 1f;
                Puts($"Принудительное ограничение высоты до {safePos.y}");
            }
            
            Puts($"Итоговая телепортационная точка: {safePos}");
            return safePos;
        }

        private void SendTeleportRequest(BasePlayer requester, BasePlayer target)
        {
            if (!teleportRequests.ContainsKey(target.userID))
            {
                teleportRequests[target.userID] = new List<TeleportRequest>();
            }

            // Check if there's already a request from this player
            foreach (TeleportRequest request in teleportRequests[target.userID])
            {
                if (request.RequesterId == requester.userID)
                {
                    requester.ChatMessage("У вас уже есть активный запрос к этому игроку");
                    return;
                }
            }

            // Создаем экземпляр запроса без использования nullable
            TeleportRequest teleportRequest = new()
            {
                RequesterId = requester.userID,
                TargetId = target.userID,
                RequestTime = DateTime.Now
            };

            // Теперь устанавливаем таймер и сохраняем его в переменную внутри объекта
            teleportRequest.ExpiryTimer = timer.Once(60f, () =>
            {
                if (teleportRequests.ContainsKey(target.userID))
                {
                    RemoveTeleportRequest(target.userID, teleportRequest);
                    requester.ChatMessage(GetMsg("RequestExpired", requester.UserIDString));
                }
            });

            teleportRequests[target.userID].Add(teleportRequest);
            requester.ChatMessage(string.Format(GetMsg("RequestSent", requester.UserIDString), target.displayName));
            target.ChatMessage(string.Format(GetMsg("RequestReceived", target.UserIDString), requester.displayName));
        }

        private void RemoveTeleportRequest(ulong targetId, TeleportRequest request)
        {
            if (teleportRequests.TryGetValue(targetId, out List<TeleportRequest>? value))
            {
                request.ExpiryTimer?.Destroy();
                _ = value.Remove(request);
            }
        }

        #endregion Teleportation Methods

        #region Helper Methods

        private BasePlayer FindPlayer(string nameOrId, BasePlayer player)
        {
            // Try to find by Steam ID
            if (ulong.TryParse(nameOrId, out ulong steamId))
            {
                BasePlayer foundPlayer = BasePlayer.FindByID(steamId);
                if (foundPlayer?.IsConnected == true)
                {
                    return foundPlayer;
                }
            }

            // Try to find by name
            foreach (BasePlayer? p in BasePlayer.activePlayerList)
            {
                if (p.displayName.Contains(nameOrId, StringComparison.CurrentCultureIgnoreCase))
                {
                    return p;
                }
            }

            return null;
        }

        /// <summary>
        /// Проверяет, стоит ли игрок на фундаменте
        /// </summary>
        /// <param name="player">Игрок для проверки</param>
        /// <returns>true если игрок стоит на фундаменте, иначе false</returns>
        private bool IsPlayerOnFoundation(BasePlayer player)
        {
            if (Physics.Raycast(player.transform.position, Vector3.down, out RaycastHit hit, 3f, Physics.DefaultRaycastLayers))
            {
                BuildingBlock block = hit.GetEntity() as BuildingBlock;
                if (block != null && block.grade != BuildingGrade.Enum.Twigs)
                {
                    // Проверяем, является ли блок фундаментом
                    return block.prefabID == 72949757; // ID фундамента
                }
            }
            return false;
        }

        /// <summary>
        /// Проверяет, имеет ли игрок привилегии строительства в данной точке
        /// </summary>
        /// <param name="player">Игрок для проверки</param>
        /// <returns>true если игрок имеет привилегии строительства, иначе false</returns>
        private bool HasBuildingPrivilege(BasePlayer player)
        {
            return player.CanBuild() && player.GetBuildingPrivilege() != null;
        }

        #endregion Helper Methods

        #region Monument Detection

        private void FindMonuments()
        {
            // Поиск всех монументов на карте
            Dictionary<string, Vector3> foundMonuments = new();

            // Найдем все монументы
            foreach (MonumentInfo monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
            {
                if (monument != null)
                {
                    string monumentName = monument.name.ToLower(System.Globalization.CultureInfo.CurrentCulture);
                    Vector3 position = monument.transform.position;

                    // Добавляем монумент в словарь
                    foundMonuments[monumentName] = position;

                    // Логируем найденный монумент для отладки
                    Puts($"Found monument: {monumentName} at position {position}");
                }
            }

            // Ищем Bandit Camp
            foreach (KeyValuePair<string, Vector3> monument in foundMonuments)
            {
                if (monument.Key.Contains("bandit") || monument.Key.Contains("bandit_town"))
                {
                    // Сохраняем базовую позицию монумента
                    monumentPositions["bandit_camp"] = monument.Value;

                    // Смещаем точку телепортации к входу в лагерь бандитов вместо вертолетной площадки
                    // Вход обычно находится с южной стороны лагеря
                    Vector3 entranceOffset = CalculateOffsetPosition(monument.Value, monument.Key,
                        new Vector3(0f, 1f, -30f), // Южное направление (-Z) с большим отступом от центра и низкой высотой
                        foundMonuments);

                    // Принудительно устанавливаем низкую высоту для избежания телепортации в воздухе
                    entranceOffset.y = Math.Max(1f, TerrainMeta.HeightMap.GetHeight(entranceOffset));

                    // Обновляем конфигурацию
                    config.CampLocation = entranceOffset;
                    Puts($"Automatically set Bandit Camp location to {entranceOffset} (near entrance)");
                    break;
                }
            }

            // Ищем Outpost (compound)
            foreach (KeyValuePair<string, Vector3> monument in foundMonuments)
            {
                if (monument.Key.Contains("compound") || monument.Key.Contains("outpost"))
                {
                    // Сохраняем базовую позицию монумента
                    monumentPositions["outpost"] = monument.Value;

                    // Смещаем точку телепортации к входу в Outpost вместо переработчика
                    // Вход обычно находится с южной стороны от центра Outpost
                    Vector3 entranceOffset = CalculateOffsetPosition(monument.Value, monument.Key,
                        new Vector3(0f, 3f, -25f), // Южное направление (0, -Z) с большим отступом от центра
                        foundMonuments);

                    // Обновляем конфигурацию
                    config.TownLocation = entranceOffset;
                    Puts($"Automatically set Outpost location to {entranceOffset} (near entrance)");
                    break;
                }
            }

            // Сохраняем изменения конфигурации
            SaveConfig();
        }

        /// <summary>
        /// Вычисляет позицию со смещением относительно центра монумента
        /// </summary>
        /// <param name="basePosition">Базовая позиция монумента</param>
        /// <param name="monumentName">Название монумента</param>
        /// <param name="offset">Вектор смещения от центра</param>
        /// <param name="allMonuments">Словарь всех найденных монументов</param>
        /// <returns>Новая позиция со смещением</returns>
        private Vector3 CalculateOffsetPosition(Vector3 basePosition, string monumentName, Vector3 offset, Dictionary<string, Vector3> allMonuments)
        {
            // Базовая позиция монумента + смещение
            Vector3 result = basePosition + offset;

            // Убедимся, что позиция находится выше уровня земли
            float terrainHeight = TerrainMeta.HeightMap.GetHeight(result);

            // Если нашли высоту земли, устанавливаем Y-координату чуть выше земли
            if (terrainHeight > 0)
            {
                result.y = terrainHeight + 0.5f;
            }
            else
            {
                // Пробуем использовать raycast для определения высоты
                if (Physics.Raycast(new Vector3(result.x, 500f, result.z), Vector3.down, out RaycastHit hit, 1000f, Physics.DefaultRaycastLayers))
                {
                    result.y = hit.point.y + 0.5f;
                }
                else
                {
                    // Если все методы не сработали, устанавливаем минимальную высоту
                    result.y = 1f;
                }
            }

            // Проверяем и логируем
            Puts($"Calculated offset position for {monumentName}: Base={basePosition}, Offset={offset}, Result={result}");

            return result;
        }

        private Vector3 GetMonumentPosition(string monumentName)
        {
            if (monumentPositions.TryGetValue(monumentName.ToLower(System.Globalization.CultureInfo.CurrentCulture), out Vector3 position))
            {
                return position;
            }

            return Vector3.zero;
        }

        #endregion Monument Detection

        /// <summary>
        /// Конструктор класса для инициализации readonly полей
        /// </summary>
        public TeleportMenu()
        {
            homes = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<string, Vector3>>>($"{Name}/homes")
                ?? new Dictionary<ulong, Dictionary<string, Vector3>>();
        }

        /// <summary>
        /// Консольная команда для сохранения первого дома
        /// </summary>
        private void ConSaveHome1(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
            {
                return;
            }

            // Проверка разрешений для использования команды
            if (!permission.UserHasPermission(player.UserIDString, "teleportmenu.use"))
            {
                player.ChatMessage(GetMsg("NoPermission", player.UserIDString));
                return;
            }

            CmdSetHome(player, "sethome", stringArray);
        }

        /// <summary>
        /// Консольная команда для сохранения второго дома
        /// </summary>
        private void ConSaveHome2(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
            {
                return;
            }

            // Проверка разрешений для использования команды
            if (!permission.UserHasPermission(player.UserIDString, "teleportmenu.use"))
            {
                player.ChatMessage(GetMsg("NoPermission", player.UserIDString));
                return;
            }

            CmdSetHome(player, "sethome", stringArray0);
        }

        /// <summary>
        /// Добавляем новую консольную команду для сохранения дома с произвольным названием
        /// </summary>
        private void ConSetHome(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
            {
                return;
            }

            // Проверка разрешений для использования команды
            if (!permission.UserHasPermission(player.UserIDString, "teleportmenu.use"))
            {
                player.ChatMessage(GetMsg("NoPermission", player.UserIDString));
                return;
            }

            string[] args = arg.HasArgs(1) ? new string[] { arg.GetString(0) } : Array.Empty<string>();
            CmdSetHome(player, "sethome", args);
        }

        /// <summary>
        /// Добавляем новую консольную команду для удаления дома
        /// </summary>
        private void ConDelHome(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
            {
                return;
            }

            // Проверка разрешений для использования команды
            if (!permission.UserHasPermission(player.UserIDString, "teleportmenu.use"))
            {
                player.ChatMessage(GetMsg("NoPermission", player.UserIDString));
                return;
            }

            string[] args = arg.HasArgs(1) ? new string[] { arg.GetString(0) } : Array.Empty<string>();
            CmdDelHome(player, "delhome", args);
        }

        /// <summary>
        /// Добавляем новую консольную команду для телепортации к дому
        /// </summary>
        private void ConGoHome(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
            {
                return;
            }

            // Проверка разрешений для использования команды
            if (!permission.UserHasPermission(player.UserIDString, "teleportmenu.use"))
            {
                player.ChatMessage(GetMsg("NoPermission", player.UserIDString));
                return;
            }

            if (!arg.HasArgs(1))
            {
                player.ChatMessage("Укажите название дома для телепортации");
                return;
            }

            string homeName = arg.GetString(0);
            Puts($"[DEBUG] Попытка телепортации к дому '{homeName}' игрока {player.displayName}");

            if (!homes.ContainsKey(player.userID))
            {
                Puts($"[DEBUG] У игрока {player.displayName} нет домов");
                player.ChatMessage("У вас нет сохраненных домов");
                return;
            }

            if (!homes[player.userID].TryGetValue(homeName, out Vector3 homePos))
            {
                Puts($"[DEBUG] Дом '{homeName}' для игрока {player.displayName} не найден");
                player.ChatMessage($"Дом '{homeName}' не найден");
                return;
            }

            Puts($"[DEBUG] Найден дом '{homeName}' игрока {player.displayName} по координатам {homePos}");

            if (!CanTeleport(player))
            {
                Puts($"[DEBUG] Игрок {player.displayName} не может телепортироваться (возможно кулдаун)");
                return;
            }

            Puts($"[DEBUG] Инициирую телепортацию игрока {player.displayName} к дому '{homeName}'");
            InitiateTeleport(player, homePos);
        }

        /// <summary>
        /// Консольная команда для закрытия интерфейса
        /// </summary>
        private void ConCloseMenu(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
            {
                return;
            }

            // Проверка разрешений для использования команды
            if (!permission.UserHasPermission(player.UserIDString, "teleportmenu.use"))
            {
                player.ChatMessage(GetMsg("NoPermission", player.UserIDString));
                return;
            }

            DestroyUI(player);
        }
    }
}