using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Runtime.InteropServices;
using System.Xml;

namespace Oxide.Plugins
{
    [Info("Stat", "Author", "1.0.0")]
    [Description("Плагин для отслеживания статистики игроков")]
    internal sealed class Stat : RustPlugin
    {
        [PluginReference] private Plugin? ImageLibrary;

        private Dictionary<ulong, PlayerStats> playerStats = new();
        internal PluginConfig config = null!;
        private readonly Dictionary<string, string> cachedUIs = new();
        internal Timer broadcastTimer = null!;

        private const string PERMISSION_USE = "stat.use";
        private const string UI_MAIN_PANEL = "Stat_MainPanel";
        private const string UI_RIGHT_PANEL = "Stat_RightPanel";
        private const string UI_PLAYER_LIST = "Stat_PlayerList";
        private const string UI_PAGE_INDICATOR = "Stat_PageIndicator";

        /// <summary>
        /// Константы для WinAPI
        /// </summary>
        private const int WS_EX_COMPOSITED = 0x02000000;

        /// <summary>
        /// Для предотвращения мерцания UI
        /// </summary>
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;

        #region Variables
        /// <summary>
        /// Переменные для работы с аватарами Steam
        /// </summary>
        private readonly Dictionary<ulong, string> avatarUrls = new();
        private const string defaultAvatarUrl = "https://avatars.akamai.steamstatic.com/fef49e7fa7e1997310d705b2a6158ff8dc1cdfeb_full.jpg";
        private bool isImageLibraryLoaded;

        /// <summary>
        /// Словарь для хранения текущей страницы для каждого игрока
        /// </summary>
        private readonly Dictionary<ulong, int> playerCurrentPage = new();

        /// <summary>
        /// Константа игроков на странице
        /// </summary>
        private const int PLAYERS_PER_PAGE = 10;
        #endregion Variables

        #region Classes

        private sealed class PlayerStats
        {
            public string Name { get; set; } = string.Empty;
            public int Points { get; set; }
            public int Kills { get; set; }
            public int Deaths { get; set; }
            public int Suicides { get; set; }
            public int ResourcesGathered { get; set; }
            public int HelicoptersDestroyed { get; set; }
            public int TanksDestroyed { get; set; }
            public DateTime LastSeen { get; set; }

            /// <summary>
            /// Ресурсы по типам
            /// </summary>
            public int WoodGathered { get; set; }
            public int StoneGathered { get; set; }
            public int MetalOreGathered { get; set; }
            public int SulfurOreGathered { get; set; }
            public int BarrelsDestroyed { get; set; }
            public int CratesLooted { get; set; }
        }

        internal sealed class PluginConfig
        {
            [JsonProperty("Команда для открытия топа")]
            public required string TopCommand { get; set; }

            [JsonProperty("Настройка начисления очков за добычу")]
            public required GatheringPointsConfig GatheringPoints { get; set; }

            [JsonProperty("Настройка начисления очков за уничтожение")]
            public required DestructionPointsConfig DestructionPoints { get; set; }

            [JsonProperty("Настройка начисления и отнимания очков за убийства и смерти")]
            public required KillDeathPointsConfig KillDeathPoints { get; set; }

            [JsonProperty("Настройка призов")]
            public required RewardsConfig Rewards { get; set; }

            [JsonProperty("Настройка оповещений в чате")]
            public required ChatNotificationsConfig ChatNotifications { get; set; }

            [JsonProperty("Config version")]
            public required string Version { get; set; }

            [JsonProperty("ИД магазина в сервисе")]
            public required string ShopId { get; set; }

            [JsonProperty("Секретный ключ (не распростраяйте его)")]
            public required string SecretKey { get; set; }

            [JsonProperty("Место в топе и выдаваемый баланс игроку")]
            public required Dictionary<int, string> RewardsByRank { get; set; }

            public sealed class GatheringPointsConfig
            {
                [JsonProperty("Сколько давать очков за дерево")]
                public int WoodPoints { get; set; }

                [JsonProperty("Сколько давать очков за каменный камень")]
                public int StonePoints { get; set; }

                [JsonProperty("Сколько давать очков за металический камень")]
                public int MetalOrePoints { get; set; }

                [JsonProperty("Сколько давать очков за серный камень")]
                public int SulfurPoints { get; set; }

                [JsonProperty("Сколько давать очков за уничтожение бочки | Лутание обычного ящика у дороги")]
                public int BarrelCratePoints { get; set; }
            }

            public sealed class DestructionPointsConfig
            {
                [JsonProperty("Сколько давать очков за уничтожение вертолета")]
                public int HelicopterPoints { get; set; }

                [JsonProperty("Сколько давать очков за уничтожение танка")]
                public int TankPoints { get; set; }
            }

            public sealed class KillDeathPointsConfig
            {
                [JsonProperty("Сколько давать очков за убийство игрока")]
                public int KillPoints { get; set; }

                [JsonProperty("Сколько давать очков за убийство NPC (бота)")]
                public int NPCKillPoints { get; set; }

                [JsonProperty("Сколько отнимать очков за смерть")]
                public int DeathPoints { get; set; }

                [JsonProperty("Сколько отнимать очков за суицид")]
                public int SuicidePoints { get; set; }
            }

            public sealed class RewardsConfig
            {
                [JsonProperty("Включить авто выдачу призов при вайпе сервера?")]
                public bool EnableAutoRewards { get; set; }

                [JsonProperty("ИД магазина в сервисе")]
                public required string ShopId { get; set; }

                [JsonProperty("Секретный ключ (не распростраяйте его)")]
                public required string SecretKey { get; set; }

                [JsonProperty("Место в топе и выдаваемый баланс игроку")]
                public required Dictionary<int, string> RewardsByRank { get; set; }
            }

            public sealed class ChatNotificationsConfig
            {
                [JsonProperty("Отправлять в чат сообщения с топ 5 игроками ?")]
                public bool EnableTopPlayersBroadcast { get; set; }

                [JsonProperty("Раз в сколько секунд будет отправлятся сообщение ?")]
                public int BroadcastInterval { get; set; }
            }
        }

        #endregion Classes

        #region Config

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig
            {
                TopCommand = "top",
                GatheringPoints = new PluginConfig.GatheringPointsConfig
                {
                    WoodPoints = 5,
                    StonePoints = 5,
                    MetalOrePoints = 5,
                    SulfurPoints = 5,
                    BarrelCratePoints = 5
                },
                DestructionPoints = new PluginConfig.DestructionPointsConfig
                {
                    HelicopterPoints = 1500,
                    TankPoints = 750
                },
                KillDeathPoints = new PluginConfig.KillDeathPointsConfig
                {
                    KillPoints = 40,
                    NPCKillPoints = 20,
                    DeathPoints = 15,
                    SuicidePoints = 15
                },
                Rewards = new PluginConfig.RewardsConfig
                {
                    EnableAutoRewards = true,
                    ShopId = "",
                    SecretKey = "",
                    RewardsByRank = new Dictionary<int, string>
                    {
                        { 1, "400.0" },
                        { 2, "250.0" },
                        { 3, "150.0" },
                        { 4, "100.0" },
                        { 5, "50.0" },
                        { 6, "50.0" },
                        { 7, "30.0" }
                    }
                },
                ChatNotifications = new PluginConfig.ChatNotificationsConfig
                {
                    EnableTopPlayersBroadcast = true,
                    BroadcastInterval = 1200
                },
                Version = "2.0.0",
                ShopId = "",
                SecretKey = "",
                RewardsByRank = new Dictionary<int, string>
                {
                    { 1, "400.0" },
                    { 2, "250.0" },
                    { 3, "150.0" },
                    { 4, "100.0" },
                    { 5, "50.0" },
                    { 6, "50.0" },
                    { 7, "30.0" }
                }
            };

            SaveConfig();
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
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion Config

        #region Oxide Hooks

        private void Init()
        {
            permission.RegisterPermission(PERMISSION_USE, this);
            cmd.AddChatCommand(config.TopCommand, this, nameof(CmdOpenTop));
            LoadData();

            if (config.ChatNotifications.EnableTopPlayersBroadcast)
            {
                StartBroadcastTimer();
            }
        }

        private void OnServerInitialized()
        {
            if (ImageLibrary == null)
            {
                PrintError("ImageLibrary plugin is not installed!");
            }
            // Проверяем наличие ImageLibrary
            Plugin? imageLibrary = plugins.Find("ImageLibrary");
            isImageLibraryLoaded = imageLibrary != null;

            if (!isImageLibraryLoaded)
            {
                PrintError("ImageLibrary не обнаружена! Аватарки не будут загружены.");
            }
            else
            {
                // Инициализируем ImageLibrary
                ImageLibrary = imageLibrary;
                PrintWarning("ImageLibrary обнаружена. Инициализация аватарок...");

                // Предзагружаем стандартную аватарку
                if (ImageLibrary != null)
                {
                    _ = ImageLibrary.Call("AddImage", defaultAvatarUrl, "default_avatar", 0UL);
                    PrintWarning("Стандартная аватарка загружена");
                }

                // Предзагружаем аватарки для существующих игроков
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    if (player?.IsNpc == false)
                    {
                        GetPlayerAvatar(player.userID, (avatarKey) => { });
                    }
                }
            }
        }

        private void Unload()
        {
            SaveData();
            CloseAllUIs();
            broadcastTimer?.Destroy();
        }

        private void OnServerSave()
        {
            SaveData();
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (entity is not BasePlayer player || item == null)
            {
                return;
            }

            int points;
            switch (item.info.shortname)
            {
                case "wood":
                    points = config.GatheringPoints.WoodPoints;
                    GetOrCreatePlayerStats(player.userID).WoodGathered += item.amount;
                    break;
                case "stones":
                    points = config.GatheringPoints.StonePoints;
                    GetOrCreatePlayerStats(player.userID).StoneGathered += item.amount;
                    break;
                case "metal.ore":
                    points = config.GatheringPoints.MetalOrePoints;
                    GetOrCreatePlayerStats(player.userID).MetalOreGathered += item.amount;
                    break;
                case "sulfur.ore":
                    points = config.GatheringPoints.SulfurPoints;
                    GetOrCreatePlayerStats(player.userID).SulfurOreGathered += item.amount;
                    break;
                default:
                    return;
            }

            AddPointsToPlayer(player.userID, points);
            GetOrCreatePlayerStats(player.userID).ResourcesGathered++;
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null)
            {
                return;
            }

            // Проверка на уничтожение бочки/ящика
            if (entity.ShortPrefabName.Contains("barrel") && info.Initiator is BasePlayer barrelPlayer)
            {
                AddPointsToPlayer(barrelPlayer.userID, config.GatheringPoints.BarrelCratePoints);
                GetOrCreatePlayerStats(barrelPlayer.userID).BarrelsDestroyed++;
                return;
            }

            if (entity.ShortPrefabName.Contains("crate_basic") && info.Initiator is BasePlayer cratePlayer)
            {
                AddPointsToPlayer(cratePlayer.userID, config.GatheringPoints.BarrelCratePoints);
                GetOrCreatePlayerStats(cratePlayer.userID).CratesLooted++;
                return;
            }

            // Проверка на уничтожение вертолета
            if (entity is PatrolHelicopter && info.Initiator is BasePlayer helicopterKiller)
            {
                AddPointsToPlayer(helicopterKiller.userID, config.DestructionPoints.HelicopterPoints);
                GetOrCreatePlayerStats(helicopterKiller.userID).HelicoptersDestroyed++;
                return;
            }

            // Проверка на уничтожение танка
            if (entity is BradleyAPC && info.Initiator is BasePlayer tankKiller)
            {
                AddPointsToPlayer(tankKiller.userID, config.DestructionPoints.TankPoints);
                GetOrCreatePlayerStats(tankKiller.userID).TanksDestroyed++;
                return;
            }

            // Проверка на убийство NPC (бота)
            if (entity is BasePlayer victim && !victim.userID.IsSteamId() && info.Initiator is BasePlayer npcKiller && npcKiller.userID.IsSteamId())
            {
                AddPointsToPlayer(npcKiller.userID, config.KillDeathPoints.NPCKillPoints);
                return;
            }

            // Проверка на убийство игрока
            if (entity is BasePlayer playerVictim && playerVictim.userID.IsSteamId() && info.Initiator != null)
            {
                // Отнимаем очки за смерть
                SubtractPointsFromPlayer(playerVictim.userID, config.KillDeathPoints.DeathPoints);
                GetOrCreatePlayerStats(playerVictim.userID).Deaths++;

                // Проверка на суицид
                if (info.Initiator == playerVictim)
                {
                    SubtractPointsFromPlayer(playerVictim.userID, config.KillDeathPoints.SuicidePoints);
                    GetOrCreatePlayerStats(playerVictim.userID).Suicides++;
                    return;
                }

                // Проверка на убийство игроком
                if (info.Initiator is BasePlayer killer && killer.userID.IsSteamId())
                {
                    AddPointsToPlayer(killer.userID, config.KillDeathPoints.KillPoints);
                    GetOrCreatePlayerStats(killer.userID).Kills++;
                }
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player?.userID.IsSteamId() != true)
            {
                return;
            }

            PlayerStats stats = GetOrCreatePlayerStats(player.userID);
            stats.Name = player.displayName;
            stats.LastSeen = DateTime.Now;

            // Загружаем аватарку игрока при подключении
            if (isImageLibraryLoaded && ImageLibrary != null)
            {
                GetPlayerAvatar(player.userID, (avatarKey) => { });
            }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player?.userID.IsSteamId() != true)
            {
                return;
            }

            PlayerStats stats = GetOrCreatePlayerStats(player.userID);
            stats.LastSeen = DateTime.Now;
            DestroyUI(player, UI_MAIN_PANEL);
        }

        private void OnServerWipe()
        {
            if (config.Rewards.EnableAutoRewards)
            {
                DistributeRewards();
            }

            // Очищаем статистику после выдачи призов
            playerStats.Clear();
            SaveData();
        }

        #endregion Oxide Hooks

        #region Commands

        private void CmdOpenTop(BasePlayer player, string command, string[] args)
        {
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                return;
            }

            // Показываем UI с общей статистикой и статистикой игрока справа
            DisplayTopUI(player, player.userID);
        }

        [ChatCommand("mystats")]
        private void CmdChatMyStats(BasePlayer player, string command, string[] args)
        {
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                return;
            }

            // Показываем UI с личной статистикой игрока
            DisplayTopUI(player, player.userID);
        }

        [ConsoleCommand("stat.mystats")]
        private void CmdMyStats(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                return;
            }

            // Показываем UI с личной статистикой игрока
            DisplayTopUI(player, player.userID);
        }

        #endregion Commands

        #region UI

        /// <summary>
        /// Метод для включения композитного режима окна (устраняет мерцание)
        /// </summary>
        private void EnableCompositedUI(BasePlayer player)
        {
            // Этот код будет выполняться на клиенте для применения стиля WS_EX_COMPOSITED
            const string compositingCode = @"
            try {
                var mainWindow = UnityEngine.Display.main.GetGameView();
                if (mainWindow != null) {
                    var hwnd = mainWindow.GetNativeWindowHandle();
                    if (hwnd != System.IntPtr.Zero) {
                        int style = GetWindowLong(hwnd, -20);
                        SetWindowLong(hwnd, -20, style | 0x02000000);
                    }
                }
            } catch (Exception ex) {
                // Игнорируем ошибки
            }
            ";

            // Отправляем код для выполнения на клиенте
            player.SendConsoleCommand("clientrun", compositingCode);
        }

        private void DisplayTopUI(BasePlayer player, ulong selectedPlayerId = 0)
        {
            if (player == null)
            {
                return;
            }

            // Включаем композитный режим, чтобы избежать мерцания
            EnableCompositedUI(player);

            // Проверяем, есть ли у игрока текущая страница, если нет - устанавливаем 1
            if (!playerCurrentPage.ContainsKey(player.userID))
            {
                playerCurrentPage[player.userID] = 1;
            }

            // Получаем текущую страницу для игрока
            int currentPage = playerCurrentPage[player.userID];

            // Удаляем предыдущий UI если он был
            DestroyUI(player, UI_MAIN_PANEL);

            // Создаем контейнер для UI с композитным режимом
            CuiElementContainer container = new()
            {
                // Создаем главную панель с темным фоном и свойством предотвращения мерцания
                {
                    new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0.2 0.15", AnchorMax = "0.8 0.9" }, // Увеличиваем размер для лучшей видимости
                        Image = { Color = "0.1 0.11 0.15 0.97", Material = "assets/content/ui/uibackgroundblur.mat" }, // Более темный фон для контраста
                        CursorEnabled = true
                    },
                    "Overlay",
                    UI_MAIN_PANEL
                }
            };

            // Добавляем небольшую декоративную верхнюю панель
            _ = container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.95", AnchorMax = "1 1" },
                Image = { Color = "0.15 0.40 0.50 1", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
            }, UI_MAIN_PANEL, "HeaderBar");

            // Улучшенный заголовок "ТОП ИГРОКОВ" с тенью
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.01 0.951", AnchorMax = "0.99 0.999" },
                Text = { Text = "ТОП ИГРОКОВ", Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter, Color = "0 0 0 0.5" } // Тень текста
            }, UI_MAIN_PANEL);

            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.95", AnchorMax = "1 1" },
                Text = { Text = "ТОП ИГРОКОВ", Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, UI_MAIN_PANEL);

            // Кнопка "ЗАКРЫТЬ" в правом верхнем углу с улучшенным стилем
            _ = container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.92 0.95", AnchorMax = "0.99 0.99" },
                Button = {
                    Color = "0.7 0.2 0.2 0.8",
                    Command = "stat.close",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                Text = { Text = "✕", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, UI_MAIN_PANEL);

            // Основной контейнер для контента
            _ = container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.01 0.01", AnchorMax = "0.99 0.94" },
                Image = { Color = "0 0 0 0" }
            }, UI_MAIN_PANEL, "ContentContainer");

            // Правая панель с улучшенным стилем (для статистики игрока)
            _ = container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.56 0.02", AnchorMax = "0.98 0.93" },
                Image = {
                    Color = "0.12 0.15 0.18 0.95",
                    Material = "assets/content/ui/uibackgroundblur.mat"
                }
            }, "ContentContainer", "RightPanel");

            // Стильный заголовок правой панели
            _ = container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.93", AnchorMax = "1 1" },
                Image = { Color = "0.15 0.4 0.3 1" }
            }, "RightPanel", "RightPanelHeader");

            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.93", AnchorMax = "1 1" },
                Text = {
                    Text = "ИНФОРМАЦИЯ ИГРОКА",
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 16,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, "RightPanel");

            // Левая панель с улучшенным стилем (список игроков)
            _ = container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.02 0.02", AnchorMax = "0.54 0.93" },
                Image = {
                    Color = "0.12 0.15 0.18 0.95",
                    Material = "assets/content/ui/uibackgroundblur.mat"
                }
            }, "ContentContainer", "LeftPanel");

            // Стильный заголовок левой панели
            _ = container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.93", AnchorMax = "1 1" },
                Image = { Color = "0.15 0.4 0.3 1" }
            }, "LeftPanel", "LeftPanelHeader");

            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.93", AnchorMax = "1 1" },
                Text = {
                    Text = "СПИСОК ИГРОКОВ",
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 16,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, "LeftPanel");

            // Улучшенные заголовки таблицы в левой панели с подсветкой фона
            _ = container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.86", AnchorMax = "1 0.93" },
                Image = { Color = "0.15 0.2 0.25 0.9" }
            }, "LeftPanel", "TableHeader");

            // Заголовки таблицы с улучшенным форматированием
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.02 0.86", AnchorMax = "0.15 0.93" },
                Text = { Text = "МЕСТО", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 0.85 0.4 1" }
            }, "LeftPanel");

            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.16 0.86", AnchorMax = "0.45 0.93" },
                Text = { Text = "ИГРОК", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 0.85 0.4 1" }
            }, "LeftPanel");

            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.46 0.86", AnchorMax = "0.65 0.93" },
                Text = { Text = "СТАТУС", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 0.85 0.4 1" }
            }, "LeftPanel");

            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.66 0.86", AnchorMax = "0.8 0.93" },
                Text = { Text = "ОЧКИ", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 0.85 0.4 1" }
            }, "LeftPanel");

            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.81 0.86", AnchorMax = "0.98 0.93" },
                Text = { Text = "НАГРАДА", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 0.85 0.4 1" }
            }, "LeftPanel");

            // Контейнер для списка игроков
            _ = container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.07", AnchorMax = "1 0.86" },
                Image = { Color = "0 0 0 0" }
            }, "LeftPanel", "PlayerListContainer");

            // Получаем всех игроков, отсортированных по очкам
            List<KeyValuePair<ulong, PlayerStats>> allPlayersSorted = playerStats
                .OrderByDescending(p => p.Value.Points)
                .ToList();

            // Вычисляем общее количество страниц
            int totalPages = (int)Math.Ceiling(allPlayersSorted.Count / (float)PLAYERS_PER_PAGE);
            if (totalPages < 1)
            {
                totalPages = 1;
            }

            // Получаем игроков для текущей страницы
            List<KeyValuePair<ulong, PlayerStats>> pagePlayersWithIds = allPlayersSorted
                .Skip((currentPage - 1) * PLAYERS_PER_PAGE)
                .Take(PLAYERS_PER_PAGE)
                .ToList();

            // Выводим строки с игроками с улучшенным стилем
            for (int i = 0; i < pagePlayersWithIds.Count; i++)
            {
                ulong playerId = pagePlayersWithIds[i].Key;
                PlayerStats playerStat = pagePlayersWithIds[i].Value;

                // Улучшенное распределение строк для равномерного отображения
                const float rowHeight = 0.79f / PLAYERS_PER_PAGE;
                float yMin = 1f - ((i + 1) * rowHeight);
                float yMax = 1f - (i * rowHeight);

                // Более четкое чередование цветов строк с улучшенным контрастом
                string rowColor = i % 2 == 0 ? "0.14 0.17 0.22 0.7" : "0.16 0.19 0.24 0.7";

                // Выделение выбранного игрока с более ярким цветом
                string rowHighlight = playerId == selectedPlayerId ? "0.2 0.35 0.3 0.9" : rowColor;

                // Вычисляем глобальный ранг игрока
                int globalRank = allPlayersSorted.FindIndex(p => p.Key == playerId) + 1;

                // Фон строки с закругленными углами для лучшего визуального восприятия
                _ = container.Add(new CuiPanel
                {
                    RectTransform = {
                        AnchorMin = $"0.01 {yMin}",
                        AnchorMax = $"0.99 {yMax - 0.005f}" // Небольшой отступ между строками
                    },
                    Image = { Color = rowHighlight }
                }, "PlayerListContainer", $"PlayerRow_{playerId}");

                // Место с выделением цветом по позиции
                string rankColor = globalRank <= 3 ? GetRankColor(globalRank) : "1 1 1 1";
                _ = container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.02 0", AnchorMax = "0.15 1" },
                    Text = {
                        Text = $"#{globalRank}",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 14,
                        Align = TextAnchor.MiddleCenter,
                        Color = rankColor
                    }
                }, $"PlayerRow_{playerId}");

                // Имя игрока с улучшенным стилем
                _ = container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.16 0", AnchorMax = "0.45 1" },
                    Text = {
                        Text = playerStat.Name,
                        Font = globalRank <= 3 ? "robotocondensed-bold.ttf" : "robotocondensed-regular.ttf",
                        FontSize = 14,
                        Align = TextAnchor.MiddleLeft,
                        Color = globalRank <= 3 ? GetRankColor(globalRank) : "1 1 1 1"
                    }
                }, $"PlayerRow_{playerId}");

                // Статус с более выразительными цветами
                bool isOnline = BasePlayer.activePlayerList.Any(p => p.displayName == playerStat.Name);
                string statusText = isOnline ? "Online" : "Offline";
                string statusColor = isOnline ? "0.2 1 0.4 1" : "0.7 0.7 0.7 0.7";

                _ = container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.46 0", AnchorMax = "0.65 1" },
                    Text = {
                        Text = statusText,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 14,
                        Align = TextAnchor.MiddleCenter,
                        Color = statusColor
                    }
                }, $"PlayerRow_{playerId}");

                // Очки с выделением для лидеров
                _ = container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.66 0", AnchorMax = "0.8 1" },
                    Text = {
                        Text = playerStat.Points.ToString(),
                        Font = globalRank <= 3 ? "robotocondensed-bold.ttf" : "robotocondensed-regular.ttf",
                        FontSize = 14,
                        Align = TextAnchor.MiddleCenter,
                        Color = globalRank <= 3 ? GetRankColor(globalRank) : "1 1 1 1"
                    }
                }, $"PlayerRow_{playerId}");

                // Награда с улучшенным отображением
                string reward = "—";
                if (globalRank <= 7 && config.Rewards.RewardsByRank.TryGetValue(globalRank, out string rewardAmount))
                {
                    reward = rewardAmount;
                }

                _ = container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.81 0", AnchorMax = "0.98 1" },
                    Text = {
                        Text = reward,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 14,
                        Align = TextAnchor.MiddleCenter,
                        Color = globalRank <= 7 ? "1 0.9 0.3 1" : "0.7 0.7 0.7 0.5"
                    }
                }, $"PlayerRow_{playerId}");

                // Добавляем кнопку для выбора игрока
                _ = container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = {
                        Color = "0 0 0 0",
                        Command = $"stat.selectplayer {playerId}"
                    },
                    Text = { Text = "" }
                }, $"PlayerRow_{playerId}");
            }

            // Контейнер для кнопок пагинации
            _ = container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.25 0.01", AnchorMax = "0.75 0.06" },
                Image = { Color = "0.15 0.2 0.25 0.8" }
            }, "LeftPanel", "PaginationContainer");

            // Улучшенные кнопки пагинации
            _ = container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.25 0", AnchorMax = "0.4 1" },
                Button = {
                    Color = currentPage > 1 ? "0.2 0.5 0.7 0.9" : "0.2 0.2 0.3 0.5",
                    Command = "stat.prevpage"
                },
                Text = {
                    Text = "◀",
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 18,
                    Align = TextAnchor.MiddleCenter,
                    Color = currentPage > 1 ? "1 1 1 1" : "0.5 0.5 0.5 0.5"
                }
            }, "PaginationContainer");

            // Индикатор текущей страницы с более стильным видом
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.4 0", AnchorMax = "0.6 1" },
                Text = {
                    Text = $"{currentPage}/{totalPages}",
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 16,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, "PaginationContainer");

            _ = container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.6 0", AnchorMax = "0.75 1" },
                Button = {
                    Color = currentPage < totalPages ? "0.2 0.5 0.7 0.9" : "0.2 0.2 0.3 0.5",
                    Command = "stat.nextpage"
                },
                Text = {
                    Text = "▶",
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 18,
                    Align = TextAnchor.MiddleCenter,
                    Color = currentPage < totalPages ? "1 1 1 1" : "0.5 0.5 0.5 0.5"
                }
            }, "PaginationContainer");

            // Добавляем кнопку МОЯ СТАТИСТИКА с улучшенным стилем (уменьшенная)
            _ = container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.69 0.02", AnchorMax = "0.85 0.07" }, // Делаем кнопку еще меньше и немного сдвигаем вправо
                Image = { Color = "0.15 0.5 0.25 0.9" }
            }, "ContentContainer", "MyStatsButtonContainer");

            _ = container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = {
                    Color = "0 0 0 0",
                    Command = $"stat.selectplayer {player.userID}"
                },
                Text = {
                    Text = "МОЯ СТАТИСТИКА",
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 14, // Уменьшаем размер шрифта
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, "MyStatsButtonContainer");

            // Показываем статистику выбранного игрока или текущего игрока в правой панели
            ulong playerIdToShow = selectedPlayerId > 0 ? selectedPlayerId : player.userID;
            DisplayPlayerStats(container, playerIdToShow);

            // Применяем UI
            BasePlayer basePlayer = BasePlayer.FindByID(player.userID);
            if (basePlayer != null)
            {
                _ = CuiHelper.AddUi(basePlayer, container);
            }
        }

        /// <summary>
        /// Функция для получения цвета ранга (для топ-3 игроков)
        /// </summary>
        private string GetRankColor(int rank)
        {
            return rank switch
            {
                1 => "1 0.9 0.2 1",// Золото
                2 => "0.9 0.9 0.9 1",// Серебро
                3 => "0.8 0.5 0.2 1",// Бронза
                _ => "1 1 1 1",// Обычный цвет
            };
        }

        private void DisplayPlayerStats(CuiElementContainer container, ulong playerId)
        {
            if (!playerStats.TryGetValue(playerId, out PlayerStats? stats))
            {
                // Если статистики нет, выводим сообщение
                _ = container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.1 0.4", AnchorMax = "0.9 0.6" },
                    Text = { Text = "Нет данных для этого игрока", Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                }, "RightPanel");
                return;
            }

            // Очищаем предыдущее содержимое правой панели
            _ = CuiHelper.DestroyUi(BasePlayer.FindByID(playerId), "RightPanelContent");

            // Создаем новый контейнер для содержимого правой панели
            _ = container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "0.12 0.22 0.18 0.95", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, "RightPanel", "RightPanelContent");

            // Имя игрока в верхней части
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.05 0.92", AnchorMax = "0.95 1" },
                Text = { Text = stats.Name, Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, "RightPanelContent");

            // Проверяем, есть ли уже аватарка игрока в ImageLibrary
            string steamAvatarKey = $"steam_avatar_{playerId}";

            if (ImageLibrary?.Call<bool>("HasImage", steamAvatarKey, 0UL) == true)
            {
                // Если аватарка уже загружена, сразу отображаем
                string avatarImage = ImageLibrary.Call<string>("GetImage", steamAvatarKey, 0UL);

                // Добавляем элемент с изображением напрямую
                container.Add(new CuiElement
                {
                    Parent = "RightPanelContent",
                    Name = "AvatarImage",
                    Components =
                    {
                        new CuiRawImageComponent { Png = avatarImage, Color = "1 1 1 1" },
                        new CuiRectTransformComponent { AnchorMin = "0.35 0.75", AnchorMax = "0.65 0.9" }
                    }
                });

                PrintWarning($"Аватарка игрока {playerId} отображена напрямую");
            }
            else
            {
                // Если аватарки нет, создаем фоновую панель
                _ = container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.35 0.75", AnchorMax = "0.65 0.9" },
                    Image = { Color = "0.1 0.1 0.1 0.5" }
                }, "RightPanelContent", "AvatarBackground");

                // Убираем временную надпись "Загрузка", оставляем только фон

                // Запускаем асинхронную загрузку аватарки
                ulong currentPlayerId = playerId;  // Создаем копию переменной для использования в лямбда-выражении

                GetPlayerAvatar(currentPlayerId, (finalAvatarKey) =>
                {
                    BasePlayer targetPlayer = BasePlayer.FindByID(currentPlayerId);
                    if (targetPlayer?.IsConnected != true)
                    {
                        return;
                    }

                    // Создаем новый контейнер для обновления UI
                    CuiElementContainer updateContainer = new()
                    {
                        // Удаляем старую панель с фоном
                        new CuiElement
                        {
                            Name = "AvatarBackground",
                            DestroyUi = "AvatarBackground"
                        }
                    };

                    if (ImageLibrary?.Call<bool>("HasImage", finalAvatarKey, 0UL) ?? false)
                    {
                        // Добавляем элемент с аватаркой
                        updateContainer.Add(new CuiElement
                        {
                            Parent = "RightPanelContent",
                            Name = "AvatarImage",
                            Components =
                            {
                                new CuiRawImageComponent { Png = ImageLibrary.Call<string>("GetImage", finalAvatarKey, 0UL), Color = "1 1 1 1" },
                                new CuiRectTransformComponent { AnchorMin = "0.35 0.75", AnchorMax = "0.65 0.9" }
                            }
                        });

                        PrintWarning($"Аватарка игрока {currentPlayerId} загружена и отображена");
                    }
                    else
                    {
                        // Если аватарку не удалось загрузить, показываем сообщение об ошибке
                        string errorPanel = updateContainer.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0.35 0.75", AnchorMax = "0.65 0.9" },
                            Image = { Color = "0.1 0.1 0.1 0.5" }
                        }, "RightPanelContent", "AvatarError");

                        _ = updateContainer.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                            Text = { Text = "Нет\nаватара", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                        }, errorPanel);

                        PrintWarning($"Не удалось загрузить аватарку для игрока {currentPlayerId}");
                    }

                    // Отправляем обновленный UI игроку
                    _ = CuiHelper.AddUi(targetPlayer, updateContainer);
                });
            }

            // Левая сторона статистики (метки)
            const float leftLabelWidth = 0.45f;
            const float labelHeight = 0.04f;
            const float startY = 0.85f;
            const float labelSpacing = 0.05f;

            // МЕСТО В ТОПЕ
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.01 {startY}", AnchorMax = $"{leftLabelWidth} {startY + labelHeight}" },
                Text = { Text = "МЕСТО В ТОПЕ:", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" }
            }, "RightPanelContent");

            // ОЧКОВ
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.01 {startY - labelSpacing}", AnchorMax = $"{leftLabelWidth} {startY - labelSpacing + labelHeight}" },
                Text = { Text = "ОЧКОВ:", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" }
            }, "RightPanelContent");

            // АКТИВНОСТЬ
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.01 {startY - (labelSpacing * 2)}", AnchorMax = $"{leftLabelWidth} {startY - (labelSpacing * 2) + labelHeight}" },
                Text = { Text = "АКТИВНОСТЬ:", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" }
            }, "RightPanelContent");

            // УБИЙСТВ
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.01 {startY - (labelSpacing * 3)}", AnchorMax = $"{leftLabelWidth} {startY - (labelSpacing * 3) + labelHeight}" },
                Text = { Text = "УБИЙСТВ:", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" }
            }, "RightPanelContent");

            // СМЕРТЕЙ
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.01 {startY - (labelSpacing * 4)}", AnchorMax = $"{leftLabelWidth} {startY - (labelSpacing * 4) + labelHeight}" },
                Text = { Text = "СМЕРТЕЙ:", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" }
            }, "RightPanelContent");

            // К/Д
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.01 {startY - (labelSpacing * 5)}", AnchorMax = $"{leftLabelWidth} {startY - (labelSpacing * 5) + labelHeight}" },
                Text = { Text = "К/Д:", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" }
            }, "RightPanelContent");

            // Правая сторона статистики (значения)
            const float rightValueStart = 0.55f;
            const float rightValueWidth = 0.95f;

            // Значение для МЕСТО В ТОПЕ
            int rank = GetPlayerRank(playerId);
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"{rightValueStart} {startY}", AnchorMax = $"{rightValueWidth} {startY + labelHeight}" },
                Text = { Text = rank.ToString(), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleRight, Color = "1 1 1 1" }
            }, "RightPanelContent");

            // Значение для ОЧКОВ
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"{rightValueStart} {startY - labelSpacing}", AnchorMax = $"{rightValueWidth} {startY - labelSpacing + labelHeight}" },
                Text = { Text = stats.Points.ToString(), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleRight, Color = "1 1 1 1" }
            }, "RightPanelContent");

            // Значение для АКТИВНОСТЬ
            TimeSpan lastSeen = DateTime.Now - stats.LastSeen;
            string lastSeenText;
            if (lastSeen.TotalDays > 1)
            {
                lastSeenText = $"{(int)lastSeen.TotalDays}д.";
            }
            else if (lastSeen.TotalHours > 1)
            {
                lastSeenText = $"{(int)lastSeen.TotalHours}ч.";
            }
            else
            {
                lastSeenText = $"{(int)lastSeen.TotalMinutes}м.";
            }

            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"{rightValueStart} {startY - (labelSpacing * 2)}", AnchorMax = $"{rightValueWidth} {startY - (labelSpacing * 2) + labelHeight}" },
                Text = { Text = lastSeenText, Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleRight, Color = "1 1 1 1" }
            }, "RightPanelContent");

            // Значение для УБИЙСТВ
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"{rightValueStart} {startY - (labelSpacing * 3)}", AnchorMax = $"{rightValueWidth} {startY - (labelSpacing * 3) + labelHeight}" },
                Text = { Text = stats.Kills.ToString(), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleRight, Color = "1 1 1 1" }
            }, "RightPanelContent");

            // Значение для СМЕРТЕЙ
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"{rightValueStart} {startY - (labelSpacing * 4)}", AnchorMax = $"{rightValueWidth} {startY - (labelSpacing * 4) + labelHeight}" },
                Text = { Text = stats.Deaths.ToString(), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleRight, Color = "1 1 1 1" }
            }, "RightPanelContent");

            // Значение для К/Д
            float kd = stats.Deaths > 0 ? (float)stats.Kills / stats.Deaths : stats.Kills;
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"{rightValueStart} {startY - (labelSpacing * 5)}", AnchorMax = $"{rightValueWidth} {startY - (labelSpacing * 5) + labelHeight}" },
                Text = { Text = kd.ToString("F2"), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleRight, Color = "1 1 1 1" }
            }, "RightPanelContent");

            // Иконки ресурсов в один ряд
            const float iconSize = 0.07f;
            const float iconSpacing = 0.11f;
            const float iconRowY = 0.5f;
            const float iconRowHeight = 0.08f;

            // Массив ресурсов для отображения
            (string, int)[] resources = new[]
            {
                ("wood", stats.WoodGathered),
                ("stones", stats.StoneGathered),
                ("metal.ore", stats.MetalOreGathered),
                ("sulfur.ore", stats.SulfurOreGathered),
                ("hq.metal.ore", 0),
                ("cloth", 0),
                ("leather", 0),
                ("fat.animal", 0),
                ("barrel.oil", stats.BarrelsDestroyed)
            };

            // Отображаем иконки ресурсов
            for (int i = 0; i < resources.Length; i++)
            {
                float xPos = 0.03f + (i * iconSpacing);
                string iconName = resources[i].Item1;
                int amount = resources[i].Item2;

                // Контейнер для иконки
                string iconContainerId = $"ResourceIcon_{i}";
                _ = container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xPos} {iconRowY}", AnchorMax = $"{xPos + iconSize} {iconRowY + iconRowHeight}" },
                    Image = { Color = "0 0 0 0" }
                }, "RightPanelContent", iconContainerId);

                // Иконка ресурса
                if (ImageLibrary != null)
                {
                    string imageUrl = iconName;

                    // Специальная обработка для бочки
                    if (iconName == "barrel.oil")
                    {
                        imageUrl = "https://i.imgur.com/stMqbNy.png";
                    }
                    else
                    {
                        imageUrl = $"https://rustlabs.com/img/items180/{iconName}.png";
                    }

                    container.Add(new CuiElement
                    {
                        Parent = iconContainerId,
                        Components =
                        {
                            new CuiRawImageComponent { Url = imageUrl },
                            new CuiRectTransformComponent { AnchorMin = "0 0.3", AnchorMax = "1 1" }
                        }
                    });
                }

                // Количество ресурса
                _ = container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.3" },
                    Text = { Text = amount.ToString(), Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                }, iconContainerId);
            }

            // Секция "Получение очков"
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.05 0.45", AnchorMax = "0.95 0.49" },
                Text = { Text = "Получение очков", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.2 1 0.2 1" }
            }, "RightPanelContent");

            // Строки с получением очков
            AddPointRow(container, "RightPanelContent", "Сбитие вертолета", $"+{config.DestructionPoints.HelicopterPoints} очков", 0.41f, 0.03f, true);
            AddPointRow(container, "RightPanelContent", "Уничтожение танка", $"+{config.DestructionPoints.TankPoints} очков", 0.38f, 0.03f, true);
            AddPointRow(container, "RightPanelContent", "Убийство игрока", $"+{config.KillDeathPoints.KillPoints} очков", 0.35f, 0.03f, true);
            AddPointRow(container, "RightPanelContent", "Убийство NPC", $"+{config.KillDeathPoints.NPCKillPoints} очков", 0.32f, 0.03f, true);
            AddPointRow(container, "RightPanelContent", "Добыча камня", $"+{config.GatheringPoints.StonePoints} очков", 0.29f, 0.03f, true);
            AddPointRow(container, "RightPanelContent", "Добыча металла", $"+{config.GatheringPoints.MetalOrePoints} очков", 0.26f, 0.03f, true);
            AddPointRow(container, "RightPanelContent", "Добыча серы", $"+{config.GatheringPoints.SulfurPoints} очков", 0.23f, 0.03f, true);
            AddPointRow(container, "RightPanelContent", "Разрушение бочки", $"+{config.GatheringPoints.BarrelCratePoints} очков", 0.20f, 0.03f, true);

            // Секция "Лишение очков"
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.05 0.18", AnchorMax = "0.95 0.22" },
                Text = { Text = "Лишение очков", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 0.2 0.2 1" }
            }, "RightPanelContent");

            // Строки с лишением очков (поднятые выше)
            AddPointRow(container, "RightPanelContent", "Смерть", $"-{config.KillDeathPoints.DeathPoints} очков", 0.13f, 0.03f, false);
            AddPointRow(container, "RightPanelContent", "Самоубийство", $"-{config.KillDeathPoints.SuicidePoints} очков", 0.10f, 0.03f, false);

            // Добавим ресурсы (под основной статистикой)
            const float resourceY = startY - (labelSpacing * 7);
            const float resourceSpacing = 0.06f;

            // Создаем иконки для ресурсов
            const string resourceIconsId = "ResourceIcons";
            _ = container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.05 {resourceY - resourceSpacing}", AnchorMax = $"0.95 {resourceY - 0.02f}" },
                Image = { Color = "0 0 0 0" }
            }, "RightPanelContent", resourceIconsId);

            // Добавляем список действий и начисляемых очков
            AddPointRow(container, "RightPanelContent", "Сбитие вертолета", $"+{config.DestructionPoints.HelicopterPoints} очков", 0.41f, 0.03f, true);
            AddPointRow(container, "RightPanelContent", "Уничтожение танка", $"+{config.DestructionPoints.TankPoints} очков", 0.38f, 0.03f, true);
            AddPointRow(container, "RightPanelContent", "Убийство игрока", $"+{config.KillDeathPoints.KillPoints} очков", 0.35f, 0.03f, true);
            AddPointRow(container, "RightPanelContent", "Убийство NPC", $"+{config.KillDeathPoints.NPCKillPoints} очков", 0.32f, 0.03f, true);
            AddPointRow(container, "RightPanelContent", "Добыча камня", $"+{config.GatheringPoints.StonePoints} очков", 0.29f, 0.03f, true);
            AddPointRow(container, "RightPanelContent", "Добыча металла", $"+{config.GatheringPoints.MetalOrePoints} очков", 0.26f, 0.03f, true);
            AddPointRow(container, "RightPanelContent", "Добыча серы", $"+{config.GatheringPoints.SulfurPoints} очков", 0.23f, 0.03f, true);
            AddPointRow(container, "RightPanelContent", "Разрушение бочки", $"+{config.GatheringPoints.BarrelCratePoints} очков", 0.20f, 0.03f, true);

            // Загружаем аватарку при необходимости
            if (isImageLibraryLoaded && ImageLibrary != null)
            {
                PrintWarning($"[DEBUG] Загрузка аватара для игрока {playerId}, метод DisplayPlayerStats");
                // Создаем контейнер для аватарки - сохраняем его ID для обновления
                string avatarContainerId = container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.35 0.75", AnchorMax = "0.65 0.9" },
                    Image = { Color = "0.3 0.3 0.3 0.5" }
                }, "RightPanelContent", "PlayerAvatarContainer");

                // Загрузка аватарки с прямым обновлением UI
                string avatarKey = $"steam_avatar_{playerId}";

                // Проверяем, уже загружена ли аватарка
                bool hasImage = ImageLibrary?.Call<bool>("HasImage", avatarKey, 0UL) ?? false;
                PrintWarning($"[DEBUG] Проверка наличия аватара {avatarKey} в ImageLibrary: {hasImage}");

                if (hasImage)
                {
                    PrintWarning($"[DEBUG] Аватар {playerId} уже есть в ImageLibrary, отображаем напрямую без текста Загрузка");
                    // Создаем новый элемент UI прямо здесь, без промежуточных шагов
                    string png = ImageLibrary.Call<string>("GetImage", avatarKey, 0UL);
                    container.Add(new CuiElement
                    {
                        Parent = "RightPanelContent",
                        Name = "PlayerAvatarImage",
                        Components =
                        {
                            new CuiRawImageComponent { Png = png, Color = "1 1 1 1" },
                            new CuiRectTransformComponent { AnchorMin = "0.35 0.75", AnchorMax = "0.65 0.9" }
                        }
                    });
                }
                else
                {
                    PrintWarning($"[DEBUG] Аватар {playerId} не найден, запрашиваем без текста Загрузка");
                    RequestSteamInfo(playerId, (newAvatarKey) =>
                    {
                        PrintWarning($"[DEBUG] Получен ключ аватара: {newAvatarKey}, обновляем UI");
                        UpdatePlayerAvatar(playerId, newAvatarKey);
                    });
                }
            }
        }

        private void AddPointRow(CuiElementContainer container, string parent, string label, string value, float yPos, float height, bool isGain)
        {
            string color = isGain ? "0.2 1 0.2 1" : "1 0.2 0.2 1";

            // Метка действия
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.05 {yPos}", AnchorMax = $"0.7 {yPos + height}" },
                Text = { Text = label, Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.9 0.9 0.9 1" }
            }, parent);

            // Значение очков
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.7 {yPos}", AnchorMax = $"0.95 {yPos + height}" },
                Text = { Text = value, Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleRight, Color = color }
            }, parent);
        }

        private void DestroyUI(BasePlayer player, string uiName)
        {
            _ = CuiHelper.DestroyUi(player, uiName);
        }

        private void CloseAllUIs()
        {
            foreach (BasePlayer? player in BasePlayer.activePlayerList)
            {
                DestroyUI(player, UI_MAIN_PANEL);
            }
        }

        [ConsoleCommand("stat.close")]
        private void CmdCloseUI(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
            {
                return;
            }

            DestroyUI(player, UI_MAIN_PANEL);
        }

        [ConsoleCommand("stat.selectplayer")]
        private void CmdSelectPlayer(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
            {
                return;
            }

            if (arg.Args == null || arg.Args.Length == 0)
            {
                return;
            }

            // Применить композитный режим, чтобы избежать мерцания
            EnableCompositedUI(player);

            if (ulong.TryParse(arg.Args[0], out ulong selectedPlayerId))
            {
                // Проверяем, смотрит ли игрок свою статистику
                bool isPersonalStats = player.userID == selectedPlayerId;

                // Если игрок смотрит свою статистику, показываем персональный вид
                if (isPersonalStats)
                {
                    // Обновляем только правую панель для отображения персональной статистики
                    CuiElementContainer personalContainer = new();

                    // Получаем статистику игрока
                    if (playerStats.TryGetValue(selectedPlayerId, out _))
                    {
                        // Создаем и отображаем содержимое правой панели
                        DisplayPlayerStats(personalContainer, selectedPlayerId);
                        _ = CuiHelper.AddUi(player, personalContainer);
                    }
                }
                else
                {
                    // Если смотрит чужую статистику, показываем обычный вид
                    DisplayTopUI(player, selectedPlayerId);
                }
            }
        }

        [ConsoleCommand("stat.nextpage")]
        private void CmdNextPage(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
            {
                return;
            }

            // Получаем текущую страницу
            if (!playerCurrentPage.TryGetValue(player.userID, out int currentPage))
            {
                currentPage = 1;
            }

            // Получаем всех игроков, отсортированных по очкам, для расчета общего числа страниц
            Dictionary<ulong, PlayerStats> stats = playerStats ?? new Dictionary<ulong, PlayerStats>();
            List<KeyValuePair<ulong, PlayerStats>> allPlayersSorted = stats
                .OrderByDescending(p => p.Value.Points)
                .ToList();

            // Вычисляем общее количество страниц
            int totalPages = (int)Math.Ceiling(allPlayersSorted.Count / (float)PLAYERS_PER_PAGE);
            if (totalPages < 1)
            {
                totalPages = 1;
            }

            // Увеличиваем страницу, только если не достигли последней
            if (currentPage < totalPages)
            {
                currentPage++;
                playerCurrentPage[player.userID] = currentPage;
            }

            // Включаем композитный режим, чтобы избежать мерцания
            EnableCompositedUI(player);

            // Пере-отрисовываем UI с новой страницей
            DisplayTopUI(player, 0);
        }

        [ConsoleCommand("stat.prevpage")]
        private void CmdPrevPage(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
            {
                return;
            }

            // Получаем текущую страницу
            if (!playerCurrentPage.TryGetValue(player.userID, out int currentPage))
            {
                currentPage = 1;
            }

            // Уменьшаем страницу, только если не на первой
            if (currentPage > 1)
            {
                currentPage--;
                playerCurrentPage[player.userID] = currentPage;
            }

            // Включаем композитный режим, чтобы избежать мерцания
            EnableCompositedUI(player);

            // Пере-отрисовываем UI с новой страницей
            DisplayTopUI(player, 0);
        }

        private void DisplayPersonalStats(BasePlayer player)
        {
            if (player == null)
            {
                return;
            }

            // Сохраняем ссылку на player в локальной переменной для использования ниже

            // Удаляем предыдущий UI если он был
            DestroyUI(player, UI_MAIN_PANEL);

            // Создаем контейнер для UI
            CuiElementContainer container = new()
            {
                // Создаем главную панель с темным фоном как на скриншоте
                {
                    new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0.3 0.2", AnchorMax = "0.7 0.8" },
                        Image = { Color = "0.12 0.22 0.28 0.95" },
                        CursorEnabled = true
                    },
                    "Overlay",
                    UI_MAIN_PANEL
                },

                // Заголовок "ТОП ИГРОКОВ" в верхней части
                {
                    new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.05 0.92", AnchorMax = "0.95 1" },
                        Text = { Text = "ТОП ИГРОКОВ", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                    },
                    UI_MAIN_PANEL
                },

                // Кнопка "ЗАКРЫТЬ" в правом верхнем углу
                {
                    new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.835 0.96", AnchorMax = "0.98 0.98" },
                        Button = { Color = "0.4 0.4 0.4 0.8", Command = "stat.close" },
                        Text = { Text = "ЗАКРЫТЬ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                    },
                    UI_MAIN_PANEL
                },

                // Левая часть (таблица игроков) - заголовки колонок
                {
                    new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.07 0.88", AnchorMax = "0.17 0.92" },
                        Text = { Text = "Место", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                    },
                    UI_MAIN_PANEL
                },
                {
                    new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.17 0.88", AnchorMax = "0.37 0.92" },
                        Text = { Text = "Игрок", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                    },
                    UI_MAIN_PANEL
                },
                {
                    new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.37 0.88", AnchorMax = "0.47 0.92" },
                        Text = { Text = "Статус", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                    },
                    UI_MAIN_PANEL
                },
                {
                    new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.47 0.88", AnchorMax = "0.57 0.92" },
                        Text = { Text = "Награда", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                    },
                    UI_MAIN_PANEL
                },
                {
                    new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.57 0.88", AnchorMax = "0.67 0.92" },
                        Text = { Text = "Очки", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                    },
                    UI_MAIN_PANEL
                },

                // Строка для текущего игрока
                {
                    new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0.058 0.82", AnchorMax = "0.67 0.88" },
                        Image = { Color = "0.15 0.15 0.2 0.6" }
                    },
                    UI_MAIN_PANEL,
                    "PlayerRow"
                },

                // Данные игрока в строке
                {
                    new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "0.11 1" },
                        Text = { Text = "#1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                    },
                    "PlayerRow"
                },
                {
                    new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.11 0", AnchorMax = "0.4 1" },
                        Text = { Text = player.displayName, Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                    },
                    "PlayerRow"
                },
                {
                    new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.4 0", AnchorMax = "0.6 1" },
                        Text = { Text = "Online", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 1 0 1" }
                    },
                    "PlayerRow"
                },
                {
                    new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.6 0", AnchorMax = "0.8 1" },
                        Text = { Text = "400", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                    },
                    "PlayerRow"
                },

                // Прозрачная кнопка поверх строки игрока для клика на всю строку
                {
                    new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Button = { Color = "0 0 0 0", Command = $"stat.selectplayer {player.userID}" },
                        Text = { Text = "" }
                    },
                    "PlayerRow"
                }
            };

            // Пустые строки таблицы для заполнения места
            for (int i = 1; i < 7; i++)
            {
                string rowId = $"EmptyRow_{i}";

                // Создаем панель строки
                _ = container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"0.058 {0.82 - (i * 0.08)}", AnchorMax = $"0.67 {0.88 - (i * 0.08)}" },
                    Image = { Color = i % 2 == 0 ? "0.15 0.15 0.2 0.6" : "0.18 0.18 0.23 0.6" }
                }, UI_MAIN_PANEL, rowId);

                // Добавляем прозрачную кнопку поверх строки, чтобы она была кликабельной
                _ = container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Color = "0 0 0 0", Command = "stat.mystats" },
                    Text = { Text = "" }
                }, rowId);
            }

            // Кнопки пагинации
            _ = container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.31 0.19", AnchorMax = "0.37 0.23" },
                Button = { Color = "0.3 0.3 0.3 0.8", Command = "stat.prevpage" },
                Text = { Text = "<", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, UI_MAIN_PANEL);

            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.46 0.01", AnchorMax = "0.54 0.06" },
                Text = { Text = "1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, UI_MAIN_PANEL);

            _ = container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.55 0.01", AnchorMax = "0.65 0.06" },
                Button = { Color = "0.3 0.3 0.3 0.8", Command = "stat.nextpage" },
                Text = { Text = ">", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, UI_MAIN_PANEL);

            // === ПРАВАЯ ПАНЕЛЬ (со статистикой игрока) ===
            _ = container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.67 0.15", AnchorMax = "0.95 0.92" },
                Image = { Color = "0.12 0.22 0.18 0.95" }
            }, UI_MAIN_PANEL, "RightPanel");

            // Имя игрока вверху правой панели
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.1 0.92", AnchorMax = "0.9 1" },
                Text = { Text = player.displayName, Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, "RightPanel");

            // Аватар игрока по центру вверху
            if (ImageLibrary != null)
            {
                container.Add(new CuiElement
                {
                    Parent = "RightPanel",
                    Name = "PlayerAvatar",
                    Components =
                    {
                        new CuiRawImageComponent { Url = "https://steamcdn-a.akamaihd.net/steamcommunity/public/images/avatars/00/000000000000000000000000000000_full.jpg" },
                        new CuiRectTransformComponent { AnchorMin = "0.35 0.78", AnchorMax = "0.65 0.9" }
                    }
                });
            }

            // Статистика игрока слева от аватара и под ним
            // МЕСТО В ТОПЕ
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.05 0.72", AnchorMax = "0.5 0.76" },
                Text = { Text = "МЕСТО В ТОПЕ:", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" }
            }, "RightPanel");

            // ОЧКОВ
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.05 0.68", AnchorMax = "0.5 0.72" },
                Text = { Text = "ОЧКОВ:", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" }
            }, "RightPanel");

            // АКТИВНОСТЬ
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.05 0.64", AnchorMax = "0.5 0.68" },
                Text = { Text = "АКТИВНОСТЬ:", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" }
            }, "RightPanel");

            // УБИЙСТВ
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.05 0.6", AnchorMax = "0.5 0.64" },
                Text = { Text = "УБИЙСТВ:", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" }
            }, "RightPanel");

            // СМЕРТЕЙ
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.05 0.56", AnchorMax = "0.5 0.6" },
                Text = { Text = "СМЕРТЕЙ:", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" }
            }, "RightPanel");

            // К/Д
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.05 0.52", AnchorMax = "0.5 0.56" },
                Text = { Text = "К/Д:", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" }
            }, "RightPanel");

            // Значения статистики (как на скриншоте)
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.85 0.72", AnchorMax = "0.95 0.76" },
                Text = { Text = "1", Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleRight, Color = "1 1 1 1" }
            }, "RightPanel");

            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.85 0.68", AnchorMax = "0.95 0.72" },
                Text = { Text = "0", Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleRight, Color = "1 1 1 1" }
            }, "RightPanel");

            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.85 0.64", AnchorMax = "0.95 0.68" },
                Text = { Text = "6м.", Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleRight, Color = "1 1 1 1" }
            }, "RightPanel");

            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.85 0.6", AnchorMax = "0.95 0.64" },
                Text = { Text = "0", Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleRight, Color = "1 1 1 1" }
            }, "RightPanel");

            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.85 0.56", AnchorMax = "0.95 0.6" },
                Text = { Text = "0", Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleRight, Color = "1 1 1 1" }
            }, "RightPanel");

            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.85 0.52", AnchorMax = "0.95 0.56" },
                Text = { Text = "0", Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleRight, Color = "1 1 1 1" }
            }, "RightPanel");

            // Ряд иконок ресурсов
            string[] resources = { "wood", "stones", "cloth", "leather", "metal.refined", "lowgradefuel", "loot_barrel_1" };
            const float iconSize = 0.09f;
            const float iconMargin = 0.001f;
            float totalWidth = resources.Length * (iconSize + iconMargin);
            float startX = (1f - totalWidth) / 2f;
            const float iconY = 0.46f;

            for (int i = 0; i < resources.Length; i++)
            {
                float xPos = startX + (i * (iconSize + iconMargin));
                string iconId = $"ResourceIcon_{i}";

                // Контейнер для иконки
                _ = container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xPos} {iconY}", AnchorMax = $"{xPos + iconSize} {iconY + 0.06}" },
                    Image = { Color = "0 0 0 0" }
                }, "RightPanel", iconId);

                // Иконка ресурса
                if (ImageLibrary != null)
                {
                    container.Add(new CuiElement
                    {
                        Parent = iconId,
                        Components =
                        {
                            new CuiRawImageComponent { Url = $"https://rustlabs.com/img/items180/{resources[i]}.png" },
                            new CuiRectTransformComponent { AnchorMin = "0 0.3", AnchorMax = "1 1" }
                        }
                    });
                }

                // Количество ресурса (0)
                _ = container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.3" },
                    Text = { Text = "0", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                }, iconId);
            }

            // Строки с получением очков
            AddPointRow(container, "RightPanel", "Сбитие вертолета", $"+{config.DestructionPoints.HelicopterPoints} очков", 0.34f, 0.03f, true);
            AddPointRow(container, "RightPanel", "Уничтожение танка", $"+{config.DestructionPoints.TankPoints} очков", 0.31f, 0.03f, true);
            AddPointRow(container, "RightPanel", "Убийство игрока", $"+{config.KillDeathPoints.KillPoints} очков", 0.28f, 0.03f, true);
            AddPointRow(container, "RightPanel", "Убийство NPC", $"+{config.KillDeathPoints.NPCKillPoints} очков", 0.25f, 0.03f, true);
            AddPointRow(container, "RightPanel", "Добыча камня", $"+{config.GatheringPoints.StonePoints} очков", 0.22f, 0.03f, true);
            AddPointRow(container, "RightPanel", "Добыча металла", $"+{config.GatheringPoints.MetalOrePoints} очков", 0.19f, 0.03f, true);
            AddPointRow(container, "RightPanel", "Добыча серы", $"+{config.GatheringPoints.SulfurPoints} очков", 0.16f, 0.03f, true);
            AddPointRow(container, "RightPanel", "Разрушение бочки", $"+{config.GatheringPoints.BarrelCratePoints} очков", 0.13f, 0.03f, true);

            // Секция "Лишение очков"
            _ = container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.05 0.18", AnchorMax = "0.95 0.22" },
                Text = { Text = "Лишение очков", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 0.2 0.2 1" }
            }, "RightPanelContent");

            // Строки с лишением очков (поднятые выше)
            AddPointRow(container, "RightPanelContent", "Смерть", $"-{config.KillDeathPoints.DeathPoints} очков", 0.13f, 0.03f, false);
            AddPointRow(container, "RightPanelContent", "Самоубийство", $"-{config.KillDeathPoints.SuicidePoints} очков", 0.10f, 0.03f, false);

            // Временно закомментировать проблемный вызов 
            // CuiHelper.AddUi(localPlayer, container);

            // Аватар игрока - НЕ создаем временный контейнер с текстом "Загрузка"
            // Вместо этого просто создаем пустую область для аватара
            string avatarKey = $"steam_avatar_{player.userID}";

            // Проверяем наличие аватарки в ImageLibrary и сразу отображаем её
            if (ImageLibrary?.Call<bool>("HasImage", avatarKey, 0UL) ?? false)
            {
                PrintWarning($"Аватарка уже загружена, сразу отображаем: {avatarKey}");
                // Получаем изображение
                string png = ImageLibrary.Call<string>("GetImage", avatarKey, 0UL);

                // Добавляем напрямую элемент с изображением
                container.Add(new CuiElement
                {
                    Parent = "RightPanelContent",
                    Name = "PlayerAvatarImage",
                    Components =
                    {
                        new CuiRawImageComponent { Png = png, Color = "1 1 1 1" },
                        new CuiRectTransformComponent { AnchorMin = "0.35 0.75", AnchorMax = "0.65 0.9" }
                    }
                });

                // Отправляем обновление игроку
                _ = CuiHelper.AddUi(player, container);
                PrintWarning($"[Stat] Аватарка {player.userID} добавлена в интерфейс как PlayerAvatarImage");
            }
            else
            {
                // Если аватарки нет, запрашиваем загрузку и показываем плейсхолдер
                PrintWarning($"Аватарку {player.userID} нужно загрузить, делаем это через RequestSteamInfo");

                // Создаем пустой элемент, который будет заменен на аватарку
                container.Add(new CuiElement
                {
                    Parent = "RightPanelContent",
                    Name = "PlayerAvatarPlaceholder",
                    Components =
                    {
                        new CuiImageComponent { Color = "0.3 0.3 0.3 0.5" },
                        new CuiRectTransformComponent { AnchorMin = "0.35 0.75", AnchorMax = "0.65 0.9" }
                    }
                });

                // Отправляем UI с заглушкой
                _ = CuiHelper.AddUi(player, container);

                // Запрашиваем загрузку аватара
                RequestSteamInfo(player.userID, (avatarKey) => UpdatePlayerAvatar(player.userID, avatarKey));
            }
        }

        #endregion UI

        #region Data Management

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/player_stats", playerStats);
        }

        private void LoadData()
        {
            playerStats = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerStats>>($"{Name}/player_stats") ?? new Dictionary<ulong, PlayerStats>();
        }

        private PlayerStats GetOrCreatePlayerStats(ulong playerId)
        {
            if (!playerStats.TryGetValue(playerId, out PlayerStats? stats))
            {
                stats = new PlayerStats
                {
                    Name = "Unknown",
                    Points = 0,
                    Kills = 0,
                    Deaths = 0,
                    Suicides = 0,
                    ResourcesGathered = 0,
                    HelicoptersDestroyed = 0,
                    TanksDestroyed = 0,
                    LastSeen = DateTime.Now
                };
                playerStats[playerId] = stats;
            }
            return stats;
        }

        private void AddPointsToPlayer(ulong playerId, int points)
        {
            PlayerStats stats = GetOrCreatePlayerStats(playerId);
            stats.Points += points;

            BasePlayer player = BasePlayer.FindByID(playerId);
            if (player != null)
            {
                SendReply(player, $"Вы получили {points} очков. Всего: {stats.Points}");
            }
        }

        private void SubtractPointsFromPlayer(ulong playerId, int points)
        {
            PlayerStats stats = GetOrCreatePlayerStats(playerId);
            stats.Points = Math.Max(0, stats.Points - points);

            BasePlayer player = BasePlayer.FindByID(playerId);
            if (player != null)
            {
                SendReply(player, $"Вы потеряли {points} очков. Осталось: {stats.Points}");
            }
        }

        private int GetPlayerRank(ulong playerId)
        {
            List<PlayerStats> sortedPlayers = playerStats.Values.OrderByDescending(p => p.Points).ToList();
            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                if (playerStats.TryGetValue(playerId, out PlayerStats? stats) && stats.Points == sortedPlayers[i].Points)
                {
                    return i + 1;
                }
            }
            return sortedPlayers.Count + 1;
        }

        #endregion Data Management

        #region Utility Methods

        private void StartBroadcastTimer()
        {
            broadcastTimer?.Destroy();
            broadcastTimer = timer.Every(config.ChatNotifications.BroadcastInterval, BroadcastTopPlayers);
        }

        private void BroadcastTopPlayers()
        {
            if (!config.ChatNotifications.EnableTopPlayersBroadcast)
            {
                return;
            }

            List<PlayerStats> topPlayers = playerStats.Values
                .OrderByDescending(p => p.Points)
                .Take(5)
                .ToList();

            if (topPlayers.Count == 0)
            {
                return;
            }

            string message = "<color=#FFA500>Топ 5 игроков сервера:</color>\n";
            for (int i = 0; i < topPlayers.Count; i++)
            {
                message += $"<color=#FFFFFF>{i + 1}. {topPlayers[i].Name} - {topPlayers[i].Points} очков</color>\n";
            }
            message += $"<color=#FFA500>Используйте /{config.TopCommand} чтобы посмотреть полный топ</color>";

            Server.Broadcast(message);
        }

        private void DistributeRewards()
        {
            if (string.IsNullOrEmpty(config.Rewards.ShopId) || string.IsNullOrEmpty(config.Rewards.SecretKey))
            {
                PrintWarning("Невозможно выдать награды: не указан ID магазина или секретный ключ.");
                return;
            }

            List<KeyValuePair<ulong, PlayerStats>> topPlayers = playerStats.OrderByDescending(p => p.Value.Points)
                .Take(config.Rewards.RewardsByRank.Count)
                .ToList();

            for (int i = 0; i < topPlayers.Count; i++)
            {
                ulong playerId = topPlayers[i].Key;
                string rank = (i + 1).ToString();

                if (config.Rewards.RewardsByRank.TryGetValue(i + 1, out string rewardAmount))
                {
                    // Здесь должна быть интеграция с сервисом выдачи наград
                    // Это просто пример, в реальности нужно использовать API сервиса
                    PrintWarning($"Выдана награда игроку {topPlayers[i].Value.Name} (#{rank}) в размере {rewardAmount}");

                    // Отправка уведомления игроку
                    BasePlayer player = BasePlayer.FindByID(playerId);
                    if (player != null)
                    {
                        SendReply(player, $"Поздравляем! Вы заняли {rank} место в топе и получили награду: {rewardAmount}");
                    }
                }
            }
        }

        #endregion Utility Methods

        #region Steam Avatar Methods

        /// <summary>
        /// Обновляет аватарку игрока в интерфейсе
        /// </summary>
        private void UpdatePlayerAvatar(ulong playerId, string avatarKey)
        {
            // Найти игрока по ID
            BasePlayer player = BasePlayer.FindByID(playerId);
            if (player?.IsConnected != true)
            {
                return;
            }

            // Проверяем, есть ли аватарка
            bool hasImage = ImageLibrary?.Call<bool>("HasImage", avatarKey, 0UL) ?? false;
            if (!hasImage)
            {
                PrintWarning($"Не удалось найти аватарку {avatarKey} в ImageLibrary");
                return;
            }

            // Получаем URL изображения аватара
            string png = ImageLibrary.Call<string>("GetImage", avatarKey, 0UL);
            if (string.IsNullOrEmpty(png))
            {
                PrintWarning($"Получен пустой URL изображения для {avatarKey}");
                return;
            }

            PrintWarning($"Получен PNG для {avatarKey}, размер: {png.Length} символов");

            // Создаем элемент с аватаркой - новый подход
            CuiElementContainer container = new()
            {
                // Создаем новый CUI элемент прямо в контейнере
                new CuiElement
                {
                    Parent = "RightPanelContent",
                    Name = "PlayerAvatarImage",
                    Components =
                {
                    new CuiRawImageComponent { Png = png, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.35 0.75", AnchorMax = "0.65 0.9" }
                }
                }
            };

            // Отправляем обновление игроку
            _ = CuiHelper.AddUi(player, container);
            PrintWarning($"[Stat] Аватарка {playerId} добавлена в интерфейс как PlayerAvatarImage");
        }

        /// <summary>
        /// Запрос информации о Steam профиле пользователя, включая аватарку
        /// </summary>
        private void RequestSteamInfo(ulong steamId, Action<string> callback)
        {
            // Формируем URL для запроса XML данных профиля
            string steamProfileUrl = $"https://steamcommunity.com/profiles/{steamId}?xml=1";

            PrintWarning($"[DEBUG] Начинаем запрос аватара для {steamId}");

            // Сначала проверим, есть ли уже аватарка в ImageLibrary
            string avatarKey = $"steam_avatar_{steamId}";
            if (ImageLibrary?.Call<bool>("HasImage", avatarKey, 0UL) ?? false)
            {
                PrintWarning($"[DEBUG] Аватар {steamId} уже существует в ImageLibrary");
                callback?.Invoke(avatarKey);
                return;
            }

            // Выполняем веб-запрос только если аватарка еще не загружена
            webrequest.Enqueue(steamProfileUrl, null, (code, response) =>
            {
                if (code != 200 || string.IsNullOrEmpty(response))
                {
                    PrintError($"Не удалось загрузить данные Steam профиля: код {code}");
                    callback?.Invoke(defaultAvatarUrl);
                    return;
                }

                try
                {
                    // Загружаем ответ как XML
                    XmlDocument xmlDoc = new();
                    xmlDoc.LoadXml(response);

                    // Получаем URL аватарки
                    string? avatarUrl = xmlDoc.SelectSingleNode("//avatarFull")?.InnerText?.Trim();

                    if (string.IsNullOrEmpty(avatarUrl))
                    {
                        PrintError($"Не удалось найти аватарку в данных Steam для {steamId}");
                        callback?.Invoke(defaultAvatarUrl);
                        return;
                    }

                    PrintWarning($"[DEBUG] Получен URL аватарки: {avatarUrl}");

                    // Загружаем аватарку в ImageLibrary, если доступно
                    if (ImageLibrary != null && isImageLibraryLoaded)
                    {
                        PrintWarning($"[DEBUG] Загружаем аватарку {avatarKey} в ImageLibrary");

                        // Вызываем прямо с обратным вызовом, чтобы убедиться, что изображение полностью загружено
                        _ = ImageLibrary.Call("AddImage", avatarUrl, avatarKey, 0UL, () =>
                        {
                            PrintWarning($"[DEBUG] Аватарка {steamId} загружена в ImageLibrary, проверка: {ImageLibrary.Call<bool>("HasImage", avatarKey, 0UL)}");

                            // После успешной загрузки вызываем обратный вызов с ключом
                            callback?.Invoke(avatarKey);
                        });
                    }
                    else
                    {
                        // Если ImageLibrary недоступна, просто возвращаем URL
                        callback?.Invoke(avatarUrl);
                    }
                }
                catch (Exception ex)
                {
                    PrintError($"Ошибка при обработке данных Steam: {ex.Message}");
                    callback?.Invoke(defaultAvatarUrl);
                }
            }, this);
        }

        /// <summary>
        /// Получение аватарки игрока с загрузкой при необходимости
        /// </summary>
        private void GetPlayerAvatar(ulong playerId, Action<string> callback)
        {
            if (!isImageLibraryLoaded || ImageLibrary == null)
            {
                callback?.Invoke(defaultAvatarUrl);
                return;
            }

            string avatarKey = $"steam_avatar_{playerId}";

            // Проверяем, загружена ли уже аватарка
            if (avatarUrls.ContainsKey(playerId) && ImageLibrary.Call<bool>("HasImage", avatarKey, 0UL))
            {
                callback?.Invoke(avatarKey);
                return;
            }

            // Загружаем аватарку
            RequestSteamInfo(playerId, callback);
        }

        #endregion Steam Avatar Methods
    }
}