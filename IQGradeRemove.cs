using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using System.Linq;
using System.Text;
using System.Collections;
using System.IO;
using Oxide.Core;
using UnityEngine.Networking;
using System.Text.RegularExpressions;
using Object = System.Object;
using Physics = UnityEngine.Physics;
using Pool = Facepunch.Pool;
using Time = UnityEngine.Time;

namespace Oxide.Plugins
{
    [Info("IQGradeRemove", "Mercury", "2.34.45")]
    [Description("IQGradeRemove")]
    public class IQGradeRemove : RustPlugin
    {
        /// <summary>
        /// TODO: RaidBlock + radialMenu в VK от Андрей Чичеренко
        /// - Исправление после обновления игры
        /// </summary>
        
        #region Reference

        [PluginReference] Plugin IQTurret, Friends, Clans, IQRecycler, XBuildingSkinMenu, AutoBaseUpgrade, RaidBlock, NoEscape;
        
        #region RaidBlocked
        
        private Boolean IsRaidBlocked(BasePlayer player)
        {
            if (RaidBlock)
                return RaidBlock.Call<Boolean>("IsRaidBlocked", player);
            return NoEscape && NoEscape.Call<Boolean>("IsRaidBlocked", player);
        }
        
        #endregion

        #region Friends / Clans

        private Boolean IsFriends(BasePlayer player, UInt64 targetPlayerID)
        {
            List<UInt64> FriendList = GetFriendList(player);
            try
            {
                return FriendList != null && FriendList.Contains(targetPlayerID);
            }
            finally
            {
                Pool.FreeUnmanaged(ref FriendList);
            }
        }
        
        private List<UInt64> GetFriendList(BasePlayer targetPlayer)
        {
            List<UInt64> FriendList = Pool.Get<List<UInt64>>();
            if (Friends)
            {
                if (Friends?.Call("GetFriends", targetPlayer.userID.Get()) is UInt64[] frinedList)
                    FriendList.AddRange(frinedList);
            }
            
            if (Clans)
            {
                if (Clans?.Call("GetClanMembers", targetPlayer.UserIDString) is UInt64[] ClanMembers)
                    FriendList.AddRange(ClanMembers);
            }

            if(targetPlayer.Team != null)
                FriendList.AddRange(targetPlayer.Team.members);

            return FriendList;
        }
        
        #endregion

        #region XBuildingSkinMenu

        private UInt64 GetBuildingSkin(BasePlayer player, BuildingGrade.Enum grade)
        {
            if (XBuildingSkinMenu == null) return 0;
            if (XBuildingSkinMenu.Version < new Oxide.Core.VersionNumber(1, 1, 4))
            {
                PrintWarning(LanguageEn ? "You have an outdated version of the XBuildingSkinMenu plugin installed. For full functionality between IQGradeRemove and XBuildingSkinMenu, you need to update XBuildingSkinMenu to version 1.0.7 or higher!" : "У вас установлена устаревшая версия плагина XBuildingSkinMenu, для полноценного функционала между IQGradeRemove и XBuildingSkinMenu вам требуется обновить XBuildingSkinMenu до версии 1.0.7 или выше!");
                return 0;
            }
            return XBuildingSkinMenu?.Call<UInt64>("GetBuildingSkin", player, grade) ?? 0;
        }

        #endregion
        
        #endregion
        
        #region Vars
        private const Boolean LanguageEn = true;
        
        private static IQGradeRemove _;
        
        private static ImageUI _imageUI;
        private static InterfaceBuilder _interface;
        private Timer timerSaveData;
        private static Double CurrentTime() => Facepunch.Math.Epoch.Current;

        private const String PermissionsRemoveAdmin = "iqgraderemove.removeadmin";
        private const String PermissionsDistanceFunc = "iqgraderemove.distancefunc";
        private const String PermissionsAllObjects = "iqgraderemove.allobjects";
        private const String PermissionsAllObjectsRemove = "iqgraderemove.allobjectsremove";
        private const String PermissionsUpWood = "iqgraderemove.upwood";
        private const String PermissionsUpStone = "iqgraderemove.upstones";
        private const String PermissionsUpMetal = "iqgraderemove.upmetal";
        private const String PermissionsUpHqm = "iqgraderemove.uphmetal";
        private const String PermissionsRemove = "iqgraderemove.removeuse";
        private const String PermissionGRNoResource = "iqgraderemove.grusenorecource";
        private const String PermissionAllObjectsBack = "iqgraderemove.allobjectsback";
        
        private readonly Dictionary<String, String> PrefabNameNormalized = new Dictionary<String, String>()
        {
            ["mining_quarry"] = "mining.quarry",
            ["refinery_small"] = "small.oil.refinery",
            ["water_catcher_large"] = "water.catcher.large",
            ["water_catcher_small"] = "water.catcher.small",
            ["small_stash_deployed"] = "stash.small",
            ["stocking_small_deployed"] = "stocking.small",
            ["stocking_large_deployed"] = "stocking.large",
            ["landmine"] = "trap.landmine",
            ["survivalfishtrap.deployed"] = "fishtrap.small",
            ["survivalfishtrap.deployed"] = "fishtrap.small",
            ["waterpurifier.deployed"] = "water.purifier",
            ["electric.windmill.small"] = "generator.wind.scrap",
            ["wall.external.high.wood"] = "wall.external.high",
            ["barricade.cover.wood_double"] = "barricade.wood.cover",
        };
        
        private List<BasePlayer> playerAlerUp = new();
        private List<BasePlayer> playerAlerRemove = new();
        private ListDictionary<UInt64, Single> lastCheckTime = new ();

        #endregion
        
        #region Configuration

        private static Configuration config = new Configuration();

        private class Configuration
        {
            [JsonProperty(LanguageEn ? "Disable the connection of the plugin with the radial RUST menu (true - yes/false - no)" : "Отключить связь плагина с радиальным меню RUST (true - да/false - нет)")]
            public Boolean disableControllRadialMenu;
            [JsonProperty(LanguageEn ? "Use notifications with instructions when using a chat command to delete or improve" : "Использовать уведомления с инструкцией при использовании чат команды на удаление или улучшение")]
            public Boolean useInstructionAlert;
            [JsonProperty(LanguageEn ? "Upgrade settings" : "Настройка улучшения")]
            public UpgradePreset UpgradePresets = new UpgradePreset();

            [JsonProperty(LanguageEn ? "Remove settings" : "Настройка удаления")]
            public RemovePreset RemovePresets = new RemovePreset();

            [JsonProperty(LanguageEn ? "Allow remote upgrade/remove (just hit with a mallet next to the object) (grant rights)" : "Разрешить дистанционное улучшение/удаление (достаточно просто рядом с объектом ударить киянкой) (выдайте права)")]
            public Boolean UseDistanceFunc;
            
            [JsonProperty(LanguageEn ? "Setting Commands for functions" : "Настройка команд для функций")]
            public CommandPresets CommandsList = new CommandPresets();

            [JsonProperty(LanguageEn ? "Remove the UI when the player has passed all stages of improvement - or will be looped (there will be no looping if the player does not have rights to a particular element with the support of rights to elements enabled)" : "Удалять интерфейс когда игрок пройдет все этапы улучшения - либо будет зациклено (зацикливание не будет если у игрока нет прав на тот или иной элемент при включенной поддержке прав на элементы)")]
            public Boolean ConfigResetInterface;
            [JsonProperty(LanguageEn ? "Enable support for rights for each element separately (rights are issued separately for each variation)" : "Включить поддержку прав на каждый элемент отдельно (права выдаются отдельно под каждую вариацию)")]
            public Boolean IsCheckPermission;
            [JsonProperty(LanguageEn ? "Duration of the selected element (improvement/removal) in seconds" : "Время действия выбранного элемента (улучшени/удаления) в секундах")]
            public Int32 ConfigActiveGradeRemove;

            [JsonProperty(LanguageEn ? "Setting UI" : "Настройка UI")]
            public InterfaceController InterfaceControllers = new InterfaceController();
            
            internal class InterfaceController
            {
                [JsonProperty(LanguageEn ? "Setting up the main panel UI" : "Настройка UI главной панели")]
                public Coordinates StaticPanel = new Coordinates();
                [JsonProperty(LanguageEn ? "Setting the 'All' element" : "Настройка элемента 'All'")]
                public AllObjects AllObject = new AllObjects();
                [JsonProperty(LanguageEn ? "Setting up controls" : "Настройка элементов управления")]
                public TypeElements TypeElement = new TypeElements();
                [JsonProperty(LanguageEn ? "Setting up timers" : "Настройка таймера")]
                public TimerElement TimerElements = new TimerElement();

                [JsonProperty(LanguageEn ? "Enable the `Close UI` button (Don't forget to add its image)" : "Включить кнопку `Закрытия UI` (Не забудьте добавить ее изображение)")]
                public Boolean closeUIUsed;
                
                internal class AllObjects
                {
                    [JsonProperty(LanguageEn ? "Setting up the 'All' button" : "Настройка кнопки 'All'")]
                    public Coordinates AllObjectsButton = new Coordinates();
                    [JsonProperty(LanguageEn ? "Buttonn stroke size" : "Размер обводки кнопки")]
                    public String SizeSelectedElement; 
                }

                internal class TimerElement
                {
                    [JsonProperty(LanguageEn ? "Setting the timer position" : "Настройка расположения таймера")]
                    public Coordinates TimerPosition = new Coordinates();
                    [JsonProperty(LanguageEn ? "Timer text size" : "Размер текста таймера")]
                    public Int32 SizeTimer;
                }
                
                internal class TypeElements
                {
                    [JsonProperty(LanguageEn ? "Customize the user interface of the active type (wood/stone, etc.)" : "Настройка UI активных типов (дерево/камень и т.д)")]
                    public Coordinates TypesElements = new Coordinates();
                    [JsonProperty(LanguageEn ? "Spacing between elements" : "Отступы между элементами")]
                    public Single OffsetBetweenElements;
                    [JsonProperty(LanguageEn ? "Element stroke size" : "Размер обводки выбранного элемента")]
                    public String SizeSelectedElement; 
                }
                
                internal class Coordinates
                {
                    public String AnchorMin;
                    public String AnchorMax;
                    public String OffsetMin;
                    public String OffsetMax;
                }
            }
            
            internal class CommandPresets
            {
                [JsonProperty(LanguageEn ? "List of commands to upgrade" : "Список команд для улучшения")]
                public List<String> UpgradeCommands = new List<String>();

                [JsonProperty(LanguageEn ? "List of commands to remove" : "Список команд для удаления")]
                public List<String> RemoveCommands = new List<String>();
            }

            internal class UpgradePreset
            {
                [JsonProperty(LanguageEn
                    ? "Allow upgrade remove after recent damage"
                    : "Разрешить улучшение постройки после недавно нанесенного урона")]
                public Boolean UpgradeSecondsAttacks;
                
                [JsonProperty(LanguageEn
                    ? "Require the structure to be repaired before upgrading it if it does not have full durability"
                    : "Требовать починить строение перед его улучшением, если у строения не полное количество прочности")]
                public Boolean RepairToGrade;
                
                [JsonProperty(LanguageEn
                    ? "Require authorization in the cupboard before improving the building"
                        : "Требовать авторизацию в шкафу перед улучшением постройки")]
                public Boolean NoGradeWithoutCupboard;
                
                [JsonProperty(LanguageEn
                    ? "Disable upgrade during raid block"
                    : "Запретить улучшение во время рейдблока")]
                public Boolean NoGradeRaidBlock;

                [JsonProperty(LanguageEn
                    ? "Allow rolling back upgrade-level (Example : stone to wood)"
                    : "Разрешить откатывать улучшение назад (Например : камень в дерево)")]
                public Boolean BackUpgrade;
                
                [JsonProperty(LanguageEn
                    ? "Return resources when rolling back an upgrade (if metal - into wood - N% of metal resources will be returned)"
                    : "Возвращать ресурсы при откате улучшения назад (если металл - в дерево - будет возвращен N% ресурсов металла)")]
                public Boolean BackUpgradeReturnedItem;

                [JsonProperty(LanguageEn
                    ? "Indicate the percentage of resource return when rolling back the upgrade"
                    : "Укажите % возврата ресурсов при откате улучшения")]
                public Int32 BackUpgradeReturnedItemPercent;
                
                [JsonProperty(LanguageEn
                    ? "Cooldown settings before upgrade a new object"
                    : "Настройка перезарядки перед улучшением нового объекта")]
                public CooldownController CooldownUpgrade = new CooldownController();
            }

            internal class RemovePreset
            {
                [JsonProperty(LanguageEn
                    ? "Allow building remove after recent damage"
                    : "Разрешить удаление постройки после недавно нанесенного урона")]
                public Boolean RemoveSecondsAttacks;
                
                [JsonProperty(LanguageEn
                    ? "Disable remove during raid block"
                    : "Запретить удаление во время рейдблока")]
                public Boolean NoRemoveRaidBlock;

                [JsonProperty(LanguageEn
                    ? "Only friends can remove structures (otherwise, anyone who has access to the cupboard)"
                    : "Удалять постройки могут только друзья (Иначе все,кто есть в шкафу)")]
                public Boolean RemoveOnlyFriends;

                [JsonProperty(LanguageEn
                    ? "Items that cannot be removed (Shortname)"
                    : "Предметы, которые нельзя удалить (Shortname)")]
                public List<String> NoRemoveItems = new List<String>();

                [JsonProperty(LanguageEn
                    ? "Cooldown settings before removing a new object"
                    : "Настройка перезарядки перед удалением нового объекта")]
                public CooldownController CooldownRemove = new CooldownController();

                [JsonProperty(LanguageEn
                    ? "Temporary construction removal restriction (Exapmle : After placing the object, it won't be possible to remove it for a certain amount of time)"
                    : "Временный запрет на удаление постройки (Например : После установки объекта, его N количество времени нельзя будет удалить)")]
                public BlockRemoveBuilding TemporaryBlockBuildRemove = new BlockRemoveBuilding();

                [JsonProperty(LanguageEn
                    ? "Complete prohibition of object removal (For example: After 3 hours of placing the object, it cannot be removed at all)"
                    : "Полный запрет на удаление объекта (Например : Через 3 часа после установки объекта, его нельзя будет удалить вообще)")]
                public BlockRemoveBuilding FullBlockBuildRemove = new BlockRemoveBuilding();

                [JsonProperty(LanguageEn
                    ? "Resource and item return settings after deletion"
                    : "Настройка возврата ресурсов и предметов после удаления")]
                public ReturnedSettings ReturnedRemoveSettings = new ReturnedSettings();

                internal class BlockRemoveBuilding
                {
                    [JsonProperty(LanguageEn ? "Use lock function" : "Использовать функцию блокировки")]
                    public Boolean UseBlock;

                    [JsonProperty(LanguageEn ? "Time in seconds" : "Время в секундах")]
                    public Int32 TimeRemove;

                    [JsonProperty(LanguageEn
                        ? "Privilege-based configuration [iqgraderemove.name = time (in seconds)]"
                        : "Настройка по привилегиям [iqgraderemove.name = время (в секундах)]")]
                    public Dictionary<String, Int32> PermissionTime = new Dictionary<String, Int32>();
                }

                internal class ReturnedSettings
                {
                    [JsonProperty(LanguageEn ? "Return attached items to the item being removed (For example, a combination lock to the door) (Return of items or % of resources on deletion must be enabled)" : "Возвращать прикрепленные предметы к удаляемому предмету (Например кодовый замок к двери) (Должен быть включен возврат предметов или % ресурсов при удалении)")]
                    public Boolean returnedChildItems;
                    [JsonProperty(LanguageEn ? "Resource return settings for building deletion" : "Настройка возврата ресурсов за удаление строений")]
                    public Building BuildingSettings = new Building();

                    [JsonProperty(LanguageEn ? "Resource/item return settings for item deletion" : "Настройка возврата ресурсов/предметов за удаление предметов")]
                    public Items ItemsSettings = new Items();

                    internal class Building
                    {
                        [JsonProperty(LanguageEn
                            ? "Enable resource return for building deletion"
                            : "Возвращать ресурсы за удаление строений")]
                        public Boolean UseReturned;

                        [JsonProperty(LanguageEn
                            ? "Use return percentage based on building durability (disregards 'Resource return percentage for building deletion')"
                            : "Использовать процент возврата в зависимости от количество прочности строения (не будет учитываться пункт 'Процент возврата ресурсов за удаление строений')")]
                        public Boolean UsePercentHealts;

                        [JsonProperty(LanguageEn
                            ? "Resource return percentage for building deletion (regardless of building durability)"
                            : "Процент возврата ресурсов за удаление строений (вне зависимости от количества прочности строения)")]
                        public Int32 PercentReturn;
                    }

                    internal class Items
                    {
                        [JsonProperty(LanguageEn
                            ? "Return items after deletion, otherwise return % of item's resources (if craftable)"
                            : "Возвращать предметы после удаления, иначе будет возвращаться % ресурсов от предмета (если его возможно крафтить)")]
                        public Boolean UseReturnedItem;

                        [JsonProperty(LanguageEn
                            ? "Use return percentage based on item durability (disregards 'Resource return percentage for item deletion')"
                            : "Использовать процент возврата в зависимости от количество прочности строения (не будет учитываться пункт 'Процент возврата ресурсов за удаление предмета')")]
                        public Boolean UsePercentHealts;

                        [JsonProperty(LanguageEn
                            ? "Resource return percentage for item deletion (if percentage return is enabled)"
                            : "Процент возврата ресурсов за удаление предмета (если включен возврат процента)")]
                        public Int32 PercentReturn;

                        [JsonProperty(LanguageEn
                            ? "Reduce item condition upon return"
                            : "Снижать состояние предмета при возврате")]
                        public Boolean UseDamageReturned;

                        [JsonProperty(LanguageEn
                            ? "Items to be ignored after deletion - they will simply be deleted without any return of items or resources (Shortname)"
                            : "Предметы, которые игнорируются после удаления - они просто удалятся без возврата предмета или ресурсов (Shortname)")]
                        public List<String> ShortnameNoteReturned = new List<String>();
                    }
                }
            }

            internal class CooldownController
            {
                [JsonProperty(LanguageEn ? "Use cooldown before action" : "Использовать перезарядку перед действием")]
                public Boolean UseCooldown;

                [JsonProperty(LanguageEn ? "Time in seconds" : "Время в секундах")]
                public Single SecondCooldown;
            }

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    disableControllRadialMenu = false,
                    useInstructionAlert = false, 
                    UseDistanceFunc = false,
                    ConfigResetInterface = true,
                    ConfigActiveGradeRemove = 60,
                    IsCheckPermission = false,
                    InterfaceControllers = new InterfaceController()
                    {
                        StaticPanel = new InterfaceController.Coordinates()
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = "58.333 25",
                            OffsetMax = "335.667 73"
                        },
                        TypeElement = new InterfaceController.TypeElements() 
                        {
                            OffsetBetweenElements = -44.5f,
                            SizeSelectedElement = "21.333",
                            TypesElements = new InterfaceController.Coordinates()
                            {
                                AnchorMin = "0.5 0.5", 
                                AnchorMax = "0.5 0.5",
                                OffsetMin = "-132 -17.333",
                                OffsetMax = "-97 17.333",
                            }
                        },
                        AllObject = new InterfaceController.AllObjects
                        {
                            AllObjectsButton = new InterfaceController.Coordinates
                            {
                                AnchorMin = "0.5 0.5",
                                AnchorMax = "0.5 0.5",
                                OffsetMin = "-188.46712 -24",
                                OffsetMax = "-140.467 24"
                            },
                            SizeSelectedElement = "21.333", 
                        },
                        TimerElements = new InterfaceController.TimerElement()
                        {
                            TimerPosition = new InterfaceController.Coordinates()
                            {
                                AnchorMin = "0.5 0.5",
                                AnchorMax = "0.5 0.5",
                                OffsetMin = "90.878 -18.828",
                                OffsetMax = "137.255 18.828",
                            },
                            SizeTimer = 30,
                        }
                    },
                    CommandsList = new CommandPresets()
                    {
                        UpgradeCommands = new List<String>()
                        {
                            "up",
                            "upgrade",
                            "grade",
                            "bgrade"
                        },
                        RemoveCommands = new List<String>()
                        {
                            "remove",
                            "rem",
                        }
                    },
                    UpgradePresets = new UpgradePreset()
                    {
                        NoGradeWithoutCupboard = false,
                        UpgradeSecondsAttacks = false,
                        RepairToGrade = true,
                        NoGradeRaidBlock = true,
                        BackUpgrade = false,
                        BackUpgradeReturnedItem = false,
                        BackUpgradeReturnedItemPercent = 50,
                        CooldownUpgrade = new CooldownController()
                        {
                            UseCooldown = false,
                            SecondCooldown = 30,
                        }
                    },
                    RemovePresets = new RemovePreset()
                    {
                        RemoveSecondsAttacks = false,
                        NoRemoveRaidBlock = true,
                        RemoveOnlyFriends = false,
                        NoRemoveItems = new List<String>()
                        {
                            "shortname.example"
                        },
                        CooldownRemove = new CooldownController()
                        {
                            UseCooldown = false,
                            SecondCooldown = 30,
                        },
                        TemporaryBlockBuildRemove = new RemovePreset.BlockRemoveBuilding()
                        {
                            UseBlock = false,
                            TimeRemove = 600,
                            PermissionTime = new Dictionary<String, Int32>()
                            {
                                ["iqgraderemove.elite"] = 100,
                                ["iqgraderemove.vip"] = 300,
                            }
                        },
                        FullBlockBuildRemove = new RemovePreset.BlockRemoveBuilding()
                        {
                            UseBlock = false,
                            TimeRemove = 600,
                            PermissionTime = new Dictionary<String, Int32>()
                            {
                                ["iqgraderemove.elite"] = 1500,
                                ["iqgraderemove.vip"] = 1000,
                            }
                        },
                        ReturnedRemoveSettings = new RemovePreset.ReturnedSettings()
                        {
                            returnedChildItems = false,
                            ItemsSettings = new RemovePreset.ReturnedSettings.Items
                            {
                                UseReturnedItem = true,
                                UsePercentHealts = true,
                                PercentReturn = 100,
                                UseDamageReturned = true,
                                ShortnameNoteReturned = new List<String>()
                                {
                                    "shortname.example",
                                }
                            },
                            BuildingSettings = new RemovePreset.ReturnedSettings.Building()
                            {
                                UseReturned = true,
                                UsePercentHealts = true,
                                PercentReturn = 100,
                            }
                        }
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();

                if (config.InterfaceControllers == null)
                {
                    if (config.InterfaceControllers.StaticPanel == null)
                    {
                        config.InterfaceControllers = new Configuration.InterfaceController()
                        {
                            StaticPanel = new Configuration.InterfaceController.Coordinates()
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "0 0",
                                OffsetMin = "58.333 25",
                                OffsetMax = "335.667 73"
                            }
                        };
                    }

                    if (config.InterfaceControllers.AllObject == null)
                    {
                        config.InterfaceControllers.AllObject = new Configuration.InterfaceController.AllObjects()
                        {
                            AllObjectsButton = new Configuration.InterfaceController.Coordinates()
                            {
                                AnchorMin = "0.5 0.5",
                                AnchorMax = "0.5 0.5",
                                OffsetMin = "-188.46712 -24",
                                OffsetMax = "-140.467 24"
                            },
                            SizeSelectedElement = "21.333"
                        };
                    }

                    if (config.InterfaceControllers.TypeElement == null)
                    {
                        config.InterfaceControllers.TypeElement = new Configuration.InterfaceController.TypeElements()
                        {
                            SizeSelectedElement = "21.333",
                            OffsetBetweenElements = -44.5f,
                            TypesElements = new Configuration.InterfaceController.Coordinates()
                            {
                                AnchorMin = "0.5 0.5",
                                AnchorMax = "0.5 0.5",
                                OffsetMin = "-132 -17.333",
                                OffsetMax = "-97 17.333",
                            }
                        };
                    }

                    if (config.InterfaceControllers.TimerElements == null)
                    {
                        config.InterfaceControllers.TimerElements = new Configuration.InterfaceController.TimerElement()
                        {
                            TimerPosition = new Configuration.InterfaceController.Coordinates()
                            {
                                AnchorMin = "0.5 0.5",
                                AnchorMax = "0.5 0.5",
                                OffsetMin = "90.878 -18.828",
                                OffsetMax = "137.255 18.828",
                            },
                            SizeTimer = 30,
                        };
                    }
                }

                if (config.InterfaceControllers.StaticPanel.AnchorMin == null)
                    config.InterfaceControllers.StaticPanel.AnchorMin = "0 0";
                if (config.InterfaceControllers.StaticPanel.AnchorMax == null)
                    config.InterfaceControllers.StaticPanel.AnchorMax = "0 0";
                if (config.InterfaceControllers.StaticPanel.OffsetMin == null)
                    config.InterfaceControllers.StaticPanel.OffsetMin = "58.333 25";
                if (config.InterfaceControllers.StaticPanel.OffsetMax == null)
                    config.InterfaceControllers.StaticPanel.OffsetMax = "335.667 73";
                
                if (config.InterfaceControllers.AllObject.AllObjectsButton.AnchorMin == null)
                    config.InterfaceControllers.AllObject.AllObjectsButton.AnchorMin = "0.5 0.5";
                if (config.InterfaceControllers.AllObject.AllObjectsButton.AnchorMax == null)
                    config.InterfaceControllers.AllObject.AllObjectsButton.AnchorMax = "0.5 0.5";
                if (config.InterfaceControllers.AllObject.AllObjectsButton.OffsetMin == null)
                    config.InterfaceControllers.AllObject.AllObjectsButton.OffsetMin = "-188.46712 -24";
                if (config.InterfaceControllers.AllObject.AllObjectsButton.OffsetMax == null)
                    config.InterfaceControllers.AllObject.AllObjectsButton.OffsetMax = "-140.467 24";
                if (config.InterfaceControllers.AllObject.SizeSelectedElement == null)
                    config.InterfaceControllers.AllObject.SizeSelectedElement = "21.333";
                
                
                if (config.InterfaceControllers.TypeElement.TypesElements.AnchorMin == null)
                    config.InterfaceControllers.TypeElement.TypesElements.AnchorMin = "0.5 0.5";
                if (config.InterfaceControllers.TypeElement.TypesElements.AnchorMax == null)
                    config.InterfaceControllers.TypeElement.TypesElements.AnchorMax = "0.5 0.5";
                if (config.InterfaceControllers.TypeElement.TypesElements.OffsetMin == null)
                    config.InterfaceControllers.TypeElement.TypesElements.OffsetMin = "-132 -17.333";
                if (config.InterfaceControllers.TypeElement.TypesElements.OffsetMax == null)
                    config.InterfaceControllers.TypeElement.TypesElements.OffsetMax = "-97 17.333";
                if (config.InterfaceControllers.TypeElement.OffsetBetweenElements == 0)
                    config.InterfaceControllers.TypeElement.OffsetBetweenElements = -44.5f;
                if (config.InterfaceControllers.TypeElement.SizeSelectedElement == null)
                    config.InterfaceControllers.TypeElement.SizeSelectedElement = "21.333";
                
                if (config.InterfaceControllers.TimerElements.TimerPosition.AnchorMin == null)
                    config.InterfaceControllers.TimerElements.TimerPosition.AnchorMin = "0.5 0.5";
                if (config.InterfaceControllers.TimerElements.TimerPosition.AnchorMax == null)
                    config.InterfaceControllers.TimerElements.TimerPosition.AnchorMax = "0.5 0.5";
                if (config.InterfaceControllers.TimerElements.TimerPosition.OffsetMin == null)
                    config.InterfaceControllers.TimerElements.TimerPosition.OffsetMin = "90.878 -18.828";
                if (config.InterfaceControllers.TimerElements.TimerPosition.OffsetMax == null)
                    config.InterfaceControllers.TimerElements.TimerPosition.OffsetMax = "137.255 18.828";
                if (config.InterfaceControllers.TimerElements.SizeTimer == 0)
                    config.InterfaceControllers.TimerElements.SizeTimer = 30;
            }
            catch
            {
                PrintWarning(LanguageEn
                    ? $"Error reading #54327 configuration 'oxide/config/{Name}', creating a new configuration!!"
                    : $"Ошибка чтения #54327 конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion
        
        #region Data

        private enum TypeBlock
        {
            Temporary,
            Full,
        }
        
        private Dictionary<UInt64, Double> TemporaryBlockBuild = new Dictionary<UInt64, Double>();
        private Dictionary<UInt64, Double> FullBlockBuild = new Dictionary<UInt64, Double>();

        private Double GetTimeBlockBuild(TypeBlock typeBlock, UInt64 buildNetID)
        {
            Configuration.RemovePreset.BlockRemoveBuilding ConfigureBlock = typeBlock == TypeBlock.Temporary ? config.RemovePresets.TemporaryBlockBuildRemove : config.RemovePresets.FullBlockBuildRemove;
            if (!ConfigureBlock.UseBlock) return 0;
            
            Dictionary<UInt64, Double> BlockList = typeBlock == TypeBlock.Temporary ? TemporaryBlockBuild : FullBlockBuild;
            return BlockList.TryGetValue(buildNetID, out Double timeBlock) ? timeBlock - CurrentTime() : 0;
        }

        private void RemoveBlockBuild(String userID, TypeBlock typeBlock, UInt64 buildNetID)
        {
            Configuration.RemovePreset.BlockRemoveBuilding ConfigureBlock = typeBlock == TypeBlock.Temporary ? config.RemovePresets.TemporaryBlockBuildRemove : config.RemovePresets.FullBlockBuildRemove;
            if (!ConfigureBlock.UseBlock) return;
            
            Dictionary<UInt64, Double> BlockList = typeBlock == TypeBlock.Temporary ? TemporaryBlockBuild : FullBlockBuild;
            if (!BlockList.ContainsKey(buildNetID)) return;
            BlockList.Remove(buildNetID);
        }
        
        private void SetupBlockBuild(String userID, TypeBlock typeBlock, UInt64 buildNetID)
        {
            Configuration.RemovePreset.BlockRemoveBuilding ConfigureBlock = typeBlock == TypeBlock.Temporary ? config.RemovePresets.TemporaryBlockBuildRemove : config.RemovePresets.FullBlockBuildRemove;
            if (!ConfigureBlock.UseBlock) return;
            
            Dictionary<UInt64, Double> BlockList = typeBlock == TypeBlock.Temporary ? TemporaryBlockBuild : FullBlockBuild;
            if (!BlockList.ContainsKey(buildNetID))
                BlockList[buildNetID] = GetTimeBlock(userID, typeBlock);
        }

        private Double GetTimeBlock(String userID, TypeBlock typeBlock)
        {
            Configuration.RemovePreset.BlockRemoveBuilding ConfigureBlock = typeBlock == TypeBlock.Temporary ? config.RemovePresets.TemporaryBlockBuildRemove : config.RemovePresets.FullBlockBuildRemove;
            if (!ConfigureBlock.UseBlock) return 0;
            
            Int32 TimeBlock = ConfigureBlock.TimeRemove;
            
            foreach (KeyValuePair<String, Int32> permissionTimeBlock in ConfigureBlock.PermissionTime.OrderBy(x => x.Value))
            {
                if (permission.UserHasPermission(userID, permissionTimeBlock.Key))
                    TimeBlock = permissionTimeBlock.Value;
            }

            return TimeBlock + CurrentTime();
        }
        
        void ReadData()
        {
            if (config.RemovePresets.TemporaryBlockBuildRemove.UseBlock)
                TemporaryBlockBuild = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<UInt64, Double>>("IQSystem/IQGradeRemove/TemporaryBlockBuilding");
            
            if (config.RemovePresets.FullBlockBuildRemove.UseBlock)
                FullBlockBuild = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<UInt64, Double>>("IQSystem/IQGradeRemove/FullBlockBuilding");
        }
        void WriteData(Boolean skipCheck = false) 
        {
            if (config.RemovePresets.TemporaryBlockBuildRemove.UseBlock || skipCheck)
                Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQSystem/IQGradeRemove/TemporaryBlockBuilding", TemporaryBlockBuild);

            if (config.RemovePresets.FullBlockBuildRemove.UseBlock || skipCheck)
                Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQSystem/IQGradeRemove/FullBlockBuilding", FullBlockBuild);
        }
        
        #region LocalRepository
        
        private enum ActionType
        {
            None,
            Wood,
            Stone,
            Metal,
            Hqm,
            Remove,
            RemoveAdmin
        }
        
        private class LocalRepositoryUser
        {
            private Boolean isReset;
            public Int32 activityTime = 0;
            private Boolean isAllObject = false; 
            private Timer timerInfo;

            public ActionType selectedType = ActionType.None;

            #region Cooldown 
            
            private Double CooldownUpgrade;
            private Double CooldownRemove;

            private Boolean UseCooldown()
            {
                return selectedType switch
                {
                    ActionType.None => false,
                    ActionType.Remove => config.RemovePresets.CooldownRemove.UseCooldown,
                    _ => config.UpgradePresets.CooldownUpgrade.UseCooldown
                };
            }

            public void SetupCooldownData()
            {
                switch (selectedType)
                {
                    case ActionType.None:
                        return;
                    case ActionType.Remove:
                        CooldownRemove = CurrentTime() + config.RemovePresets.CooldownRemove.SecondCooldown;
                        break;
                    case ActionType.Wood:
                    case ActionType.Stone:
                    case ActionType.Metal:
                    case ActionType.Hqm:
                    default:
                        CooldownUpgrade = CurrentTime() + config.UpgradePresets.CooldownUpgrade.SecondCooldown;
                        break;
                }
            }

            public Boolean IsCooldown()
            {
                if (!UseCooldown()) return false;

                Double remainingCooldown = (selectedType == ActionType.Remove) 
                    ? CooldownRemove - CurrentTime()
                    : CooldownUpgrade - CurrentTime();

                return remainingCooldown > 0;
            }

            public Double GetCooldownTime()
            {
                if (!UseCooldown()) return 0;

                if (!IsCooldown())
                {
                    SetupCooldownData();
                    return 0;
                }

                if (selectedType == ActionType.Remove)
                    return CooldownRemove - CurrentTime();
    
                return CooldownUpgrade - CurrentTime();
            }

             
             #endregion
            
            #region All Objects
            
            public Boolean HasAllObjectsPermission(BasePlayer player) => _.permission.UserHasPermission(player.UserIDString, PermissionsAllObjects);
            public Boolean IsAllObjects(BasePlayer player) => HasAllObjectsPermission(player) && isAllObject && selectedType != ActionType.None;

            public void TurnedAllObjects(BasePlayer player)
            {
                isAllObject = !isAllObject;
                _.DrawUI_Button_AllObject(player, IsAllObjects(player));
            }

            #endregion
            
            public Int32 GetGradeLevel => selectedType is ActionType.Remove or ActionType.None ? -1 : (Int32)selectedType;
      
            public void ActionAssignment(BasePlayer player, Int32 customType = -1)
            {
                Int32 correctedCustomType = customType is > 5 or < 1 ? 1 : customType;
                Int32 nextTypeAsInt = (customType != -1) ? correctedCustomType : (Int32)selectedType + 1;
    
                if (customType != -1)
                {
                    if (!isReset && (Int32)selectedType == nextTypeAsInt)
                    {
                        isReset = true;
                        DeleteTimerUI(player);
                        return;
                    }
                    
                    isReset = false;
                }

                if (config.IsCheckPermission)
                {
                    Boolean anyPermissionFound = false;
                    Boolean sameTypeFound = false;
                    Boolean onlyOneTypeAvailable = true;  

                    while (nextTypeAsInt <= (Int32)ActionType.Remove)
                    {
                        if (HasPermissionTypes(player, nextTypeAsInt))
                        {
                            anyPermissionFound = true;
                            if (nextTypeAsInt != (Int32)selectedType)
                                onlyOneTypeAvailable = false;  
                            else sameTypeFound = true;
                            break;
                        }
                        nextTypeAsInt++;
                    }
                    
                    if (onlyOneTypeAvailable)  
                    {
                        if (sameTypeFound && timerInfo is { Destroyed: false })
                        {
                            isReset = true;
                            DeleteTimerUI(player);
                            return;
                        }
                        
                        if (!anyPermissionFound && (timerInfo == null || timerInfo.Destroyed))
                        {
                            _.MessageGameTipsError(player, _.GetLang("NO_PERMISSION", player.UserIDString));
                            return;
                        }
                        
                        DeleteTimerUI(player);
                        return;
                    }
                }

                if (nextTypeAsInt > (Int32)ActionType.Remove)
                {
                    if (isReset || !config.ConfigResetInterface)
                    {
                        nextTypeAsInt = (Int32)ActionType.Wood;
                        isReset = false;
                    }
                    else
                    {
                        isReset = true;
                        DeleteTimerUI(player);
                        return;
                    }
                }

                selectedType = (ActionType)nextTypeAsInt;
                _.DrawUI_StaticPanel(player);
                ResetTimer(player);
            }

            public Boolean HasPermissionTypes(BasePlayer player, Int32 actionType)
            {
                if (!config.IsCheckPermission) return true;
                return actionType switch
                {
                    (Int32)ActionType.Wood => _.permission.UserHasPermission(player.UserIDString, PermissionsUpWood),
                    (Int32)ActionType.Stone => _.permission.UserHasPermission(player.UserIDString, PermissionsUpStone),
                    (Int32)ActionType.Metal => _.permission.UserHasPermission(player.UserIDString, PermissionsUpMetal),
                    (Int32)ActionType.Hqm => _.permission.UserHasPermission(player.UserIDString, PermissionsUpHqm),
                    (Int32)ActionType.Remove => _.permission.UserHasPermission(player.UserIDString, PermissionsRemove),
                    _ => false
                };
            }

            public void ResetTimer(BasePlayer player)
            {
                timerInfo?.Destroy();
                activityTime = (Int32)CurrentTime() + config.ConfigActiveGradeRemove;
                _.DrawUI_Timer(player, (Int32)(activityTime - CurrentTime()));
                timerInfo = _.timer.Repeat(1f, config.ConfigActiveGradeRemove, () =>
                {
                    TimerController(player);
                });
            }

            private void TimerController(BasePlayer player)
            {
                if (CurrentTime() >= activityTime)
                    DeleteTimerUI(player);
                else _.DrawUI_Timer_Update(player, (Int32)(activityTime - CurrentTime()));
            }

            public void DeleteTimerUI(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, InterfaceBuilder.UI_GREADE_REMOVE_OVERLAY);
            
                timerInfo?.Destroy();

                selectedType = ActionType.None;
            }
        }

        private Dictionary<BasePlayer, LocalRepositoryUser> LocalRepository = new Dictionary<BasePlayer, LocalRepositoryUser>();

        #endregion

        private void RegisteredPlayer(BasePlayer player) => LocalRepository.TryAdd(player, new LocalRepositoryUser());

        #endregion

        #region Metods

        private void ControllerGradeRemove(BasePlayer player, String ArgNumber = "")
        {
            if (String.IsNullOrWhiteSpace(ArgNumber))
            {
                LocalRepository[player].ActionAssignment(player);
                return;
            }

            if (!Int32.TryParse(ArgNumber, out Int32 actionType))
                return;
            
            LocalRepository[player].ActionAssignment(player, actionType);
        }

        private void RegisteredPermissions()
        {
            if (!permission.PermissionExists(PermissionsAllObjects, this))
                permission.RegisterPermission(PermissionsAllObjects, this);
            
            if (!permission.PermissionExists(PermissionGRNoResource, this))
                permission.RegisterPermission(PermissionGRNoResource, this);
            
            if (!permission.PermissionExists(PermissionAllObjectsBack, this))
                permission.RegisterPermission(PermissionAllObjectsBack, this);
            
            if (!permission.PermissionExists(PermissionsUpWood, this))
                permission.RegisterPermission(PermissionsUpWood, this);
            
            if (!permission.PermissionExists(PermissionsUpStone, this))
                permission.RegisterPermission(PermissionsUpStone, this);
            
            if (!permission.PermissionExists(PermissionsUpMetal, this))
                permission.RegisterPermission(PermissionsUpMetal, this);
            
            if (!permission.PermissionExists(PermissionsUpHqm, this))
                permission.RegisterPermission(PermissionsUpHqm, this);
            
            if (!permission.PermissionExists(PermissionsRemove, this))
                permission.RegisterPermission(PermissionsRemove, this);
            
            if (!permission.PermissionExists(PermissionsDistanceFunc, this))
                permission.RegisterPermission(PermissionsDistanceFunc, this);   
            
            if (!permission.PermissionExists(PermissionsRemoveAdmin, this))
                permission.RegisterPermission(PermissionsRemoveAdmin, this); 
            
            if (!permission.PermissionExists(PermissionsAllObjectsRemove, this))
                permission.RegisterPermission(PermissionsAllObjectsRemove, this);

            foreach (KeyValuePair<String,Int32> fullBlockSettigns in config.RemovePresets.FullBlockBuildRemove.PermissionTime)
            {
                if (!permission.PermissionExists(fullBlockSettigns.Key, this))
                    permission.RegisterPermission(fullBlockSettigns.Key, this);
            }
            
            foreach (KeyValuePair<String,Int32> temporaryBlockSettigns in config.RemovePresets.TemporaryBlockBuildRemove.PermissionTime)
            {
                if (!permission.PermissionExists(temporaryBlockSettigns.Key, this))
                    permission.RegisterPermission(temporaryBlockSettigns.Key, this);
            }
        }

        #region Upgrade Core
   
        private void UpgradeHit(BasePlayer player, BaseEntity entity, LocalRepositoryUser repositoryUser)
        {
            BuildingBlock buildingBlock = entity as BuildingBlock;
            if (buildingBlock == null) return;
            
            String canUpgradeResult = CanUpgrade(player, buildingBlock, repositoryUser);
            if (!String.IsNullOrWhiteSpace(canUpgradeResult))
            {
                MessageGameTipsError(player, canUpgradeResult);
                return;
            }
            
            Int32 GradeLevel = repositoryUser.GetGradeLevel;
            if (GradeLevel == -1) return;

            BuildingGrade.Enum selectedGrade = (BuildingGrade.Enum)GradeLevel;
            
            if (selectedGrade == buildingBlock.grade) return;

            // Unsubscribe(nameof(OnStructureUpgrade));
            //
            // // if (Interface.Call("OnStructureUpgrade", buildingBlock, player, (BuildingGrade.Enum) selectedGrade) != null) 
            // //     return;
            // //
            // Subscribe(nameof(OnStructureUpgrade));
            
            if (!config.UpgradePresets.BackUpgrade)
            {
                if (GradeLevel <= (Int32)buildingBlock.grade)
                    return;
            }
            else if (config.UpgradePresets.BackUpgradeReturnedItem)
            {
                if (GradeLevel <= (Int32)buildingBlock.grade)
                {
                    foreach (ItemAmount itemAmount in buildingBlock.BuildCost())
                    {
                        Int32 amountItem = (Int32)(itemAmount.amount * (config.UpgradePresets.BackUpgradeReturnedItemPercent / 100.0f));
                        if(amountItem <= 0) continue;

                        Item backItem = ItemManager.CreateByName(itemAmount.itemDef.shortname, amountItem);
                        if (backItem == null) continue;
                        player.GiveItem(backItem);
                    }
                }
            }

            UInt64 SkinBuilding = GetBuildingSkin(player, selectedGrade);
            
            if (!permission.UserHasPermission(player.UserIDString, PermissionGRNoResource))
                buildingBlock.PayForUpgrade(buildingBlock.blockDefinition.GetGrade(selectedGrade, SkinBuilding), player);

            buildingBlock.SetHealthToMax();
            
            buildingBlock.ClientRPC<Int32, UInt64>((Network.Connection) null, "DoUpgradeEffect", GradeLevel, 0);
            
            buildingBlock.ChangeGradeAndSkin(selectedGrade, SkinBuilding, true, true);
            
            
            if(buildingBlock.grade == BuildingGrade.Enum.Metal && SkinBuilding != 0 && SkinBuilding == 10221)
            {
                UInt32 lastBlockColourID = GetShippingContainerBlockColourForPlayer(player);
                buildingBlock.SetCustomColour(lastBlockColourID == 0 ? buildingBlock.currentSkin.GetStartingDetailColour(0) : lastBlockColourID);
            }
            
            repositoryUser.ResetTimer(player);
        }
        
        #region GetSetContainerColor
    
        private UInt32 GetShippingContainerBlockColourForPlayer(BasePlayer player)
        {
            Int32 infoInt = player.GetInfoInt("client.SelectedShippingContainerBlockColour", 0);
      
            if (infoInt >= 0)
                return (UInt32)infoInt;
      
            return (UInt32)0;
        }
    
        private void SetShippingContainerBlockColourForPlayer(BasePlayer player, uint color) => player.SetInfo("client.SelectedShippingContainerBlockColour", color.ToString());
    
        #endregion

        private String CanUpgrade(BasePlayer player, BuildingBlock buildingBlock, LocalRepositoryUser repositoryUser, BuildingGrade.Enum grade = BuildingGrade.Enum.None)
        {
            if (buildingBlock.name.Contains("foundation") && DeployVolume.Check(buildingBlock.transform.position, buildingBlock.transform.rotation, PrefabAttribute.server.FindAll<DeployVolume>(buildingBlock.prefabID), ~(1 << buildingBlock.gameObject.layer)))
                return GetLang("UPGRADE_FOREIGN_OBJECT", player.UserIDString);
            
            if (!config.UpgradePresets.UpgradeSecondsAttacks && buildingBlock.SecondsSinceAttacked < 30)
                return GetLang("UPGRADE_SINCE_ATTACKED_BLOCK", player.UserIDString, FormatTime(TimeSpan.FromSeconds(30 - (Int32)buildingBlock.SecondsSinceAttacked), player.UserIDString));
                
            if(config.UpgradePresets.NoGradeWithoutCupboard)
                if (player.GetBuildingPrivilege() == null || !player.IsBuildingAuthed())
                    return GetLang("UPGRADE_NO_AUTH_BUILDING_CUPBOARD", player.UserIDString);
            
            if (!player.CanBuild())
                return GetLang("UPGRADE_NO_AUTH_BUILDING", player.UserIDString);

            Double CooldownTime = repositoryUser.GetCooldownTime();
            if (CooldownTime > 0)
                return GetLang("UPGRADE_TIME_EXECURE", player.UserIDString, FormatTime(TimeSpan.FromSeconds(CooldownTime), player.UserIDString));
            
            if (config.UpgradePresets.NoGradeRaidBlock && IsRaidBlocked(player))
                return GetLang("UPGRADE_NO_ESCAPE", player.UserIDString);

            if (config.UpgradePresets.RepairToGrade && buildingBlock.health < buildingBlock.MaxHealth() && buildingBlock.grade != BuildingGrade.Enum.Twigs)
                return GetLang("UPGRADE_REPAIR_OBJECTS", player.UserIDString);
            
            if (permission.UserHasPermission(player.UserIDString, PermissionGRNoResource)) return String.Empty;
            
            Int32 gradeLevel = grade == BuildingGrade.Enum.None ? repositoryUser.GetGradeLevel : (Int32)grade;
            return !buildingBlock.CanAffordUpgrade((BuildingGrade.Enum)gradeLevel, 0, player) ? GetLang("UPGRADE_NOT_ENOUGHT_RESOURCE", player.UserIDString) : String.Empty;
        }
        
        #endregion

        #region Remove Core

        private void RemoveHit(BasePlayer player, BaseEntity entity, LocalRepositoryUser repositoryUser)
        {
            String canRemoveResult = CanRemove(player, entity);
            if (!String.IsNullOrWhiteSpace(canRemoveResult))
            {
                MessageGameTipsError(player, canRemoveResult);
                return;
            }

            ReturnedRemoveItems(player, entity);

            NextTick(() => {
                {
                    StorageContainer container = entity as StorageContainer;
                    if (container != null) 
                        container.DropItems();

                    entity.Kill(BaseNetworkable.DestroyMode.Gib);
                }
            });
            
            repositoryUser.ResetTimer(player);
        }
        
        private String NormalizePrefabName(String prefabName)
        {
            foreach (KeyValuePair<String, String> replacement in PrefabNameNormalized)
                prefabName = prefabName.Replace(replacement.Key, replacement.Value);
            
            return Regex.Replace(prefabName, "\\.deployed|_deployed", "");
        }
        
        private void ReturnedRemoveItems(BasePlayer player, BaseEntity entity)
        {
            String NormalizePrefab = NormalizePrefabName(entity.ShortPrefabName);

            Configuration.RemovePreset.ReturnedSettings.Items ItemsSettings = config.RemovePresets.ReturnedRemoveSettings.ItemsSettings;
            Configuration.RemovePreset.ReturnedSettings.Building BuildingSettings = config.RemovePresets.ReturnedRemoveSettings.BuildingSettings;

            try
            {
                if (!ItemsSettings.ShortnameNoteReturned.Contains(NormalizePrefab))
                    if (HandleItemReturn(player, entity, NormalizePrefab, ItemsSettings))
                        return;
            }
            catch (Exception)
            {
                //PrintError(LanguageEn ? $"[NormalizePrefabName] Key name not found. Please notify the developer about this message and send them this text. Missing key normalizer for the prefab {entity.ShortPrefabName}" : $"[NormalizePrefabName] Not found key name. Сообщите разработчику об этом уведомлении и пришлите ему этот текст. Отсуствует ключ-нормализатор к префабу {entity.ShortPrefabName}");
                return;
            }

            if (BuildingSettings.UseReturned && entity is StabilityEntity buildingBlock)
                HandleBuildingReturn(player, buildingBlock, BuildingSettings);
        }

        private void HandleItemChildReturn(BaseEntity entity, BasePlayer player, Configuration.RemovePreset.ReturnedSettings.Items ItemsSettings)
        {
            if (!config.RemovePresets.ReturnedRemoveSettings.returnedChildItems) return;
            foreach (BaseEntity.Slot slot in Enum.GetValues(typeof(BaseEntity.Slot)))
            {
                if (!entity.HasSlot(slot)) continue;
                BaseEntity slotEntity = entity.GetSlot(slot);
                if (slotEntity == null) continue;
                
                String NormalizeSlot = NormalizePrefabName(slotEntity.ShortPrefabName);
                if (ItemsSettings.ShortnameNoteReturned.Contains(NormalizeSlot)) continue;
                
                Item itemSlotReturned = CreateItem(slotEntity, NormalizeSlot);
                if (itemSlotReturned != null)
                    player.GiveItem(itemSlotReturned);
            }
        }
        private Boolean HandleItemReturn(BasePlayer player, BaseEntity entity, String NormalizePrefab, Configuration.RemovePreset.ReturnedSettings.Items ItemsSettings)
        {
            if (entity is BuildingBlock) return false;
            Item ItemReturned = CreateItem(entity, NormalizePrefab);
            
            if (ItemsSettings.UseReturnedItem && ItemReturned != null)
            {
                if (ItemsSettings.UseDamageReturned && entity is BaseCombatEntity combatEntity)
                {
                    Single healthFraction = combatEntity.health / entity.MaxHealth();
                    ItemReturned.conditionNormalized = Mathf.Clamp01(healthFraction - combatEntity.pickup.subtractCondition);
                }

                HandleItemChildReturn(entity, player, ItemsSettings);
                
                player.GiveItem(ItemReturned);
                return true;
            }
            else if (entity is BaseCombatEntity combatEntity)
            {
                ItemDefinition itemDef = ItemManager.FindItemDefinition(ItemReturned.info.shortname);
                if (itemDef == null || itemDef.Blueprint == null || itemDef.Blueprint.ingredients == null)
                {
                    HandleItemChildReturn(entity, player, ItemsSettings);
                    player.GiveItem(ItemReturned);
                    return true;
                }
                
                Single PercentReturnResource = ItemsSettings.UsePercentHealts
                    ? combatEntity.health / combatEntity.MaxHealth()
                    : ItemsSettings.PercentReturn / 100.0f;

                foreach (ItemAmount CostReturned in itemDef.Blueprint.ingredients)
                {
                    Int32 Amount = Mathf.FloorToInt(CostReturned.amount * PercentReturnResource);
                    if (Amount <= 0 && CostReturned.amount == 1)
                    {
                        if (UnityEngine.Random.value > 0.5f)
                            Amount = 1;
                        else continue;
                    }
                    
                    HandleItemChildReturn(entity, player, ItemsSettings);
                    player.GiveItem(ItemManager.Create(CostReturned.itemDef, Amount));
                }

                return true;
            }

            if (IQRecycler?.Call<Boolean>("API_IsValidRecycler", entity) != true) return false;
            Item RecyclerReturned = IQRecycler.Call<Item>("API_GetItemRecyclerAfterRemove", entity, player);
            player.GiveItem(RecyclerReturned);

            return true;
        }

        private Item CreateItem(BaseEntity entity, String normalizePrefab)
        {
            var skinID = entity.skinID;

            return entity switch
            {
                ModularCarGarage modularCarGarage when modularCarGarage.ShortPrefabName.Contains("electrical.modularcarlift") => ItemManager.CreateByName("modularcarlift", 1, skinID),
                BaseCombatEntity baseCombatEntity => baseCombatEntity.pickup.itemTarget == null ? ItemManager.CreateByName(normalizePrefab, 1, skinID) : ItemManager.Create(baseCombatEntity.pickup.itemTarget, 1, skinID),
                CodeLock _ => ItemManager.CreateByName(normalizePrefab, 1, skinID),
                _ => null
            };
        }


        private void HandleBuildingReturn(BasePlayer player, StabilityEntity buildingBlock, Configuration.RemovePreset.ReturnedSettings.Building BuildingSettings)
        {
            Single PercentReturn = BuildingSettings.UsePercentHealts
                ? buildingBlock.health / buildingBlock.MaxHealth()
                : BuildingSettings.PercentReturn / 100.0f;

            foreach (ItemAmount CostReturned in buildingBlock.BuildCost())
            {
                Int32 amount = Mathf.FloorToInt(CostReturned.amount * PercentReturn);
                if (amount <= 0) continue;
                player.GiveItem(ItemManager.Create(CostReturned.itemDef, amount));
            }
        }
        String CanRemove(BasePlayer player, BaseEntity entity)
        {
            Configuration.RemovePreset RemoveConfig = config.RemovePresets;
            LocalRepositoryUser repository = LocalRepository[player];
        
            Object canRemove = Interface.Call("canRemove", player, entity);
            if (canRemove != null)
                return canRemove is String ? (String)canRemove : GetLang("REMOVE_NOT_REMOVE_OBJECT", player.UserIDString);
            
            if (permission.UserHasPermission(player.UserIDString, PermissionsRemoveAdmin))
                return RemoveConfig.NoRemoveItems.Contains(Regex.Replace(
                    entity.ShortPrefabName.Replace("mining_quarry", "mining.quarry"), "\\.deployed|_deployed", ""))
                    ? GetLang("REMOVE_NOT_REMOVE_OBJECT", player.UserIDString)
                    : "";
            
            if(entity.OwnerID == 0)
                return GetLang("REMOVE_NOT_REMOVE_ALIEN_OBJECT", player.UserIDString);
        
            if (RemoveConfig.NoRemoveRaidBlock && IsRaidBlocked(player))
                return GetLang("REMOVE_NO_ESCAPE", player.UserIDString);

            Double CooldownTime = repository.GetCooldownTime();
            if (CooldownTime > 0)
                return GetLang("REMOVE_TIME_EXECUTE", player.UserIDString, FormatTime(TimeSpan.FromSeconds(CooldownTime), player.UserIDString));
        
            if(IQTurret)
            {
                if(entity is ElectricSwitch)
                    if ((Boolean)IQTurret.CallHook("API_IS_TURRETLIST", entity))
                        return GetLang("REMOVE_IQTURRET_NO_DELETE_TUMBLER", player.UserIDString);
            }
        
            BuildingBlock buildingBlocks = entity as BuildingBlock;
            if (buildingBlocks != null)
            {
                if (!config.RemovePresets.RemoveSecondsAttacks && buildingBlocks.SecondsSinceAttacked < 30)
                    return GetLang("REMOVE_SINCE_ATTACKED_BLOCK", player.UserIDString, FormatTime(TimeSpan.FromSeconds(30 - (Int32)buildingBlocks.SecondsSinceAttacked), player.UserIDString));
            }
            BuildingPrivlidge privilege = player.GetBuildingPrivilege(player.WorldSpaceBounds());
            
            if (RemoveConfig.RemoveOnlyFriends && player.userID != entity.OwnerID)
            {
                if (!IsFriends(player, entity.OwnerID) || player.IsBuildingBlocked())
                    return GetLang("REMOVE_NOT_REMOVE_ALIEN_OBJECT", player.UserIDString);
            }
            else if (privilege != null && !player.IsBuildingAuthed())
                return GetLang("REMOVE_NOT_REMOVE_ALIEN_OBJECT", player.UserIDString);
            else if (privilege == null && entity.OwnerID != player.userID)
                return GetLang("REMOVE_NOT_REMOVE_ALIEN_OBJECT", player.UserIDString);
            
            Int32 TemporaryTimeBlock = Convert.ToInt32(GetTimeBlockBuild(TypeBlock.Temporary, entity.net.ID.Value));

            if (RemoveConfig.TemporaryBlockBuildRemove.UseBlock && TemporaryTimeBlock > 0)
                return GetLang("REMOVE_TIME_EXECUTE", player.UserIDString,
                    FormatTime(TimeSpan.FromSeconds(TemporaryTimeBlock), player.UserIDString));

            Int32 FullTimeBlock = Convert.ToInt32(GetTimeBlockBuild(TypeBlock.Full, entity.net.ID.Value));
            if (RemoveConfig.FullBlockBuildRemove.UseBlock && FullTimeBlock < 0)
                return GetLang("REMOVE_FULL_BLOCK_REMOVE", player.UserIDString);

            return RemoveConfig.NoRemoveItems.Contains(Regex.Replace(entity.ShortPrefabName.Replace("mining_quarry", "mining.quarry"), "\\.deployed|_deployed", "")) ? GetLang("REMOVE_NOT_REMOVE_OBJECT", player.UserIDString) : "";
        }

        #endregion
        
        #region Upgrade / Remove All Object

        private Dictionary<BasePlayer, Coroutine> RoutineUpgradeAllPlayers = new Dictionary<BasePlayer, Coroutine>();
        private void AllObjectAction(BasePlayer player, BaseEntity entity)
        {
            LocalRepositoryUser repository = LocalRepository[player];
            Boolean IsAllObjects = repository.IsAllObjects(player);
            if (!IsAllObjects) return;
            
            List<BuildingBlock> buildingBlocks = GetBlocks(player, entity);
            if (buildingBlocks == null)
                return;

            if (repository.selectedType == ActionType.Remove)
            {
                if (!permission.UserHasPermission(player.UserIDString, PermissionsAllObjectsRemove))
                {
                    MessageGameTipsError(player, GetLang("NO_PERMISSION", player.UserIDString));
                    return;
                }
                
                foreach (BuildingBlock Block in buildingBlocks)
                    Block.Kill();
            }
            else if (repository.selectedType != ActionType.None && repository.selectedType != ActionType.Remove)
            {
                BuildingGrade.Enum gradeType = (BuildingGrade.Enum)repository.GetGradeLevel;                                           
                List<BuildingBlock> blocksToChange = buildingBlocks.Where(block => block.grade != gradeType && (permission.UserHasPermission(player.UserIDString, PermissionAllObjectsBack) || config.UpgradePresets.BackUpgrade || repository.GetGradeLevel > (Int32)block.grade)).ToList();
            
                IEnumerator processUpgradeBlocks = ProcessUpgradeBlocks(blocksToChange, gradeType, player);
                
                if(RoutineUpgradeAllPlayers.ContainsKey(player))
                    if (RoutineUpgradeAllPlayers[player] != null)
                        ServerMgr.Instance.StopCoroutine(RoutineUpgradeAllPlayers[player]);
                    
                Coroutine upgradeAllRoutine = ServerMgr.Instance.StartCoroutine(processUpgradeBlocks);
                RoutineUpgradeAllPlayers[player] = upgradeAllRoutine;
                
                repository.ResetTimer(player);
            }

            Pool.FreeUnmanaged(ref buildingBlocks);
        }

        private IEnumerator ProcessUpgradeBlocks(List<BuildingBlock> blocksToChange, BuildingGrade.Enum gradeType,
            BasePlayer player)
        {
            Int32 tryUpObjectFinded = 0;
            for (Int32 i = 0; i < blocksToChange.Count; i++)
            {
                BuildingBlock Block = blocksToChange[i];
                if (!permission.UserHasPermission(player.UserIDString, PermissionGRNoResource) && 
                    !Block.CanAffordUpgrade(gradeType, Block.skinID, player))
                {
                    MessageGameTipsError(player, GetLang("UPGRADE_NOT_ENOUGHT_RESOURCE", player.UserIDString));
                    yield break;
                }
                
                if (Block.name.Contains("foundation") && DeployVolume.Check(Block.transform.position, Block.transform.rotation, PrefabAttribute.server.FindAll<DeployVolume>(Block.prefabID), ~(1 << Block.gameObject.layer)))
                {
                    tryUpObjectFinded++;
                    continue;
                }
                
                if (config.UpgradePresets.BackUpgradeReturnedItem)
                {
                    if ((Int32)gradeType <= (Int32)Block.grade)
                    {
                        foreach (ItemAmount itemAmount in Block.BuildCost())
                        {
                            Int32 amountItem = (Int32)(itemAmount.amount * (config.UpgradePresets.BackUpgradeReturnedItemPercent / 100.0f));
                            if(amountItem <= 0) continue;

                            Item backItem = ItemManager.CreateByName(itemAmount.itemDef.shortname, amountItem);
                            if (backItem == null) continue;
                            player.GiveItem(backItem);
                        }
                    }
                }

                ConstructionGrade grade = Block.blockDefinition.GetGrade(gradeType, 0);
                if (!permission.UserHasPermission(player.UserIDString, PermissionGRNoResource))
                    Block.PayForUpgrade(grade, player);

                Block.SetHealthToMax();

                UInt64 SkinBuilding = GetBuildingSkin(player, gradeType);
                Block.ChangeGradeAndSkin(gradeType, SkinBuilding, true, true);

                yield return new WaitForSeconds(0.05f);
            }

            if (tryUpObjectFinded != 0)
                MessageGameTipsError(player, GetLang("ALL_OBJECTS_FINDED_OBJECT", player.UserIDString));
        }

        private List<BuildingBlock> GetBlocks(BasePlayer player, BaseEntity buildingBlock)
        {
            if (buildingBlock.GetBuildingPrivilege() == null || !player.IsBuildingAuthed())
            {
                MessageGameTipsError(player, GetLang("ALL_OBJECTS_NO_AUTH", player.UserIDString));
                return null;
            }

            List<BuildingBlock> buildingBlocks = Pool.Get<List<BuildingBlock>>();
            
            foreach (BuildingBlock Block in buildingBlock.GetBuildingPrivilege().GetBuilding().buildingBlocks)
                if (!buildingBlocks.Contains(Block))
                    buildingBlocks.Add(Block);

            return buildingBlocks;
        }
        
        #endregion
        

        #region Help

        private void MessageGameTipsError(BasePlayer player, String Message)
        {
            player.SendConsoleCommand("gametip.showtoast", new Object[]{ "1", Message, "15" });
            Effect.server.Run("assets/bundled/prefabs/fx/invite_notice.prefab", player.GetNetworkPosition());
        }

        private String FormatTime(TimeSpan time, String userID)
        {
            StringBuilder result = new StringBuilder();

            if (time.Days > 0)
                result.Append(Format(time.Days, GetLang("FORMAT_TIME_DAY", userID)));

            if (time.Hours > 0)
                result.Append($" {Format(time.Hours, GetLang("FORMAT_TIME_HOURSE", userID))}");

            if (time.Minutes > 0)
                result.Append($" {Format(time.Minutes, GetLang("FORMAT_TIME_MINUTES", userID))}");

            if (time.Days == 0 && time.Hours == 0 && time.Minutes == 0 && time.Seconds > 0)
                result.Append($" {Format(time.Seconds, GetLang("FORMAT_TIME_SECONDS", userID))}");

            return result.ToString().Trim();
        }

        private String Format(Int32 units, String form)
        {
            Int32 tmp = units % 10;
    
            if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9)
                return $"{units}{form}";

            if (tmp >= 2 && tmp <= 4)
                return $"{units}{form}";

            return $"{units}{form}";
        }


        #endregion
        
        #endregion

        #region Commands

        [ConsoleCommand("close.ui.func")]
        private void ClosedUI(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            LocalRepository[player].DeleteTimerUI(player);
        }

        [ConsoleCommand("upgrade.func")]
        private void UpgradeFuncCMD(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            
            LocalRepository[player].ActionAssignment(player, Convert.ToInt32(arg.Args[0]));
        }
        
        private void ChatCMDUp(BasePlayer player, String cmd, String[] args)
        {
            if (args.Length >= 1)
                ControllerGradeRemove(player, args[0]);
            else ControllerGradeRemove(player);

            if (!config.useInstructionAlert) return;
            if (playerAlerUp.Contains(player)) return;
            MessageGameTipsError(player, GetLang("UPGRADE_UP_USE_INSTRUCTION", player.UserIDString));
            playerAlerUp.Add(player);
        }

        private void ChatCMDRemove(BasePlayer player)
        {
            ControllerGradeRemove(player, "5");

            if (!config.useInstructionAlert) return;
            if (playerAlerRemove.Contains(player)) return;
            MessageGameTipsError(player, GetLang("UPGRADE_REMOVE_USE_INSTRUCTION", player.UserIDString));
            playerAlerRemove.Add(player);
        }

        private void ConsoleCMDUp(ConsoleSystem.Arg arg) 
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            ControllerGradeRemove(player);
        }

        private void ConsoleCMDRemove(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            
            ControllerGradeRemove(player, "5");
        }
        
        [ConsoleCommand("func.turn.all.objects")]
        private void FuncCommandAllObjects(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            LocalRepositoryUser localRepositoryUser = LocalRepository[player];
            if (!localRepositoryUser.HasAllObjectsPermission(player)) return;
            
            localRepositoryUser.TurnedAllObjects(player);
        }

        #endregion

        #region Hooks

        #region Core Hooks
        private Int32 GetFilteredCollidersCount(Collider[] colliders, int count)
        {
            return colliders.Take(count).Count(x =>
                x.name.Contains("assets") &&
                !x.name.Contains("building") &&
                !x.name.Contains("player"));
        }

        
        private Boolean CheckNearbyCollider(BuildingBlock block)
        {
            Collider[] colliders = new Collider[30];
            Int32 colliderCount = Physics.OverlapSphereNonAlloc(block.CenterPoint() + new Vector3(0f, 0f, 0f), 2f, colliders);
            Int32 FilteredCountFloor = GetFilteredCollidersCount(colliders, colliderCount);
  
            return FilteredCountFloor < 1;
        }
        
        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!input.IsDown(BUTTON.FIRE_PRIMARY))
                return;
            
            Single currentTime = Time.time;
            if (lastCheckTime.TryGetValue(player.userID, out Single lastTime))
            {
                if (currentTime - lastTime < 1f) 
                    return;
            }
            else lastCheckTime.Add(player.userID, 0);

            lastCheckTime[player.userID] = currentTime;

            if (!(player.GetHeldEntity() is Hammer))
                return;
            
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 3.0f, LayerMask.GetMask("Construction")))
                return;

            BuildingBlock block = hit.GetEntity() as BuildingBlock;
            if (block == null)
                return;

            if (!CheckNearbyCollider(block)) return;

            LocalRepositoryUser repositoryUser = LocalRepository[player];
            if (repositoryUser.IsAllObjects(player))
            {
                AllObjectAction(player, block);
                return;
            }

            if (repositoryUser.selectedType == ActionType.Remove)
                RemoveHit(player, block, repositoryUser);
            else UpgradeHit(player, block, repositoryUser);
        }
        
        void OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (info == null || player == null || info.HitEntity == null) return;
            LocalRepositoryUser repositoryUser = LocalRepository[player];
            BaseEntity hitEntity = info.HitEntity;

            if (repositoryUser.IsAllObjects(player))
            {
                AllObjectAction(player, hitEntity);
                return;
            }

            if (repositoryUser.selectedType == ActionType.Remove)
                RemoveHit(player, hitEntity, repositoryUser);
            else if(repositoryUser.selectedType != ActionType.Remove && repositoryUser.selectedType != ActionType.None) 
                UpgradeHit(player, hitEntity, repositoryUser);
        }
        
        Object OnStructureRepair(BaseCombatEntity entity, BasePlayer player)
        {
            if (entity == null || player == null) return null;
            
            if (LocalRepository[player].selectedType != ActionType.Remove && LocalRepository[player].selectedType != ActionType.None && !config.UpgradePresets.RepairToGrade)
                return false;
            
            if (LocalRepository[player].selectedType == ActionType.Remove)
                return false;
            
            return null;
        }
        
        object OnPayForUpgrade(BasePlayer player, BuildingBlock block, ConstructionGrade gradeTarget)
        {
            if (permission.UserHasPermission(player.UserIDString, PermissionGRNoResource))
                return false;
            return null;
        }
        
        Object OnStructureUpgrade(BaseCombatEntity entity, BasePlayer player, BuildingGrade.Enum grade)
        {
            BuildingBlock buildingBlock = entity as BuildingBlock;
            if (buildingBlock == null) return null;
            if(entity.prefabID is 1340890323 or 34089032) return null;
            
            Boolean isAutoBaseUpgrade = AutoBaseUpgrade != null && AutoBaseUpgrade.Call<Boolean>("IsBaseUpgradeProcess", entity.GetBuildingPrivilege());
            String canUpgrade = config.disableControllRadialMenu || isAutoBaseUpgrade ? String.Empty : CanUpgrade(player, buildingBlock, LocalRepository[player], grade);
            
            if (String.IsNullOrWhiteSpace(canUpgrade))
            {
                UInt64 SkinBuilding = GetBuildingSkin(player, grade);
                
                buildingBlock.SetHealthToMax();
        
                if (grade == BuildingGrade.Enum.Metal && SkinBuilding == 10221)
                    buildingBlock.SetCustomColour(GetShippingContainerBlockColourForPlayer(player)); 
                
                return null;
            }
            
            MessageGameTipsError(player, canUpgrade);
            return true;
        }
        
        object OnPayForPlacement(BasePlayer player, Planner planner, Construction construction)
        {
            if (planner.isTypeDeployable)
                return null;
            
            if (!permission.UserHasPermission(player.UserIDString, PermissionGRNoResource))
                return null;

            return false;
        }
        
        private void OnEntityKill(BaseEntity entity)
        {
            if (entity == null) return;
            if (entity.prefabID is 1340890323 or 34089032) return;
            if (entity.net == null) return;
            if (!entity.OwnerID.IsSteamId()) return;
            String ownerID = entity.OwnerID.ToString();
            
            RemoveBlockBuild(ownerID, TypeBlock.Temporary, entity.net.ID.Value);
            RemoveBlockBuild(ownerID, TypeBlock.Full, entity.net.ID.Value);
        }
        
        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (plan == null) return;
            BasePlayer player = plan.GetOwnerPlayer();
            if (player == null || go == null) return;

            BaseEntity entity = go.ToBaseEntity();
            if (entity == null) return;
            if (entity.prefabID is 1340890323 or 34089032) return;
            if (entity.net == null) return;
      
            SetupBlockBuild(player.UserIDString, TypeBlock.Temporary, entity.net.ID.Value);
            SetupBlockBuild(player.UserIDString, TypeBlock.Full, entity.net.ID.Value);

            BuildingBlock buildingBlock = entity as BuildingBlock;
            if (buildingBlock == null) return;
            LocalRepositoryUser repository = LocalRepository[player];

            if (repository.selectedType != ActionType.None && repository.selectedType != ActionType.Remove)
                UpgradeHit(player, buildingBlock, repository);
        }

        #endregion
        
        private void Init()
        {
            ReadData();
            _ = this;
        }

        private void OnServerInitialized()
        {
            if (AutoBaseUpgrade && AutoBaseUpgrade.Version < new VersionNumber(1, 2, 2))
            {
                NextTick(() =>
                {
                    PrintError(LanguageEn
                        ? "You have an outdated version of AutoBaseUpgrade installed. Please update to version 1.2.2 or higher"
                        : "У вас установлена устаревшая версия AutoBaseUpgrade, обновитесь до версии выше 1.2.2");
                    Interface.Oxide.UnloadPlugin(Name);
                });
            }
            if (config.CommandsList.UpgradeCommands.Count == 0)
            {
                PrintWarning(LanguageEn ? "You don't have upgrade commands, the plugin can't work" : "У вас не указаны команды для улучшения, плагин не может работать");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }
             
            if (config.CommandsList.UpgradeCommands.Count == 0)
            {
                PrintWarning(LanguageEn ? "You don't have remove commands, the plugin can't work" : "У вас не указаны команды для удаления, плагин не может работать");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }
            
            _imageUI = new ImageUI();
            _imageUI.DownloadImage();

            if (!config.UseDistanceFunc)
                Unsubscribe("OnPlayerInput");
            
            RegisteredPermissions();

            foreach (String upgradeCommand in config.CommandsList.UpgradeCommands)
            {
                cmd.AddChatCommand(upgradeCommand, this, nameof(ChatCMDUp));
                cmd.AddConsoleCommand(upgradeCommand, this, nameof(ConsoleCMDUp));
            }
            foreach (String removeCommand in config.CommandsList.RemoveCommands)
            {
                cmd.AddChatCommand(removeCommand, this, nameof(ChatCMDRemove));
                cmd.AddConsoleCommand(removeCommand, this, nameof(ConsoleCMDRemove));
            }

            if (config.RemovePresets.TemporaryBlockBuildRemove.UseBlock || config.RemovePresets.FullBlockBuildRemove.UseBlock)
                timerSaveData = timer.Every(900f, () => WriteData());
            else Unsubscribe(nameof(OnEntityKill));
            
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
        }

        private void OnNewSave(string filename)
        {
            if (FullBlockBuild != null && FullBlockBuild.Count != 0)
                FullBlockBuild.Clear();
            
            if (TemporaryBlockBuild != null && TemporaryBlockBuild.Count != 0)
                TemporaryBlockBuild.Clear();
            
            WriteData(true);
        }
        
        private void OnServerShutdown() => Unload();
        
        private void Unload()
        {
            if (_ == null) return;
            
            InterfaceBuilder.DestroyAll();

            WriteData();

            if (_imageUI != null)
            {
                _imageUI.UnloadImages();
                _imageUI = null;
            }
            
            if(FullBlockBuild != null && FullBlockBuild.Count != 0)
                FullBlockBuild.Clear();
            
            if(TemporaryBlockBuild != null && TemporaryBlockBuild.Count != 0)
                TemporaryBlockBuild.Clear();
            
            if(RoutineUpgradeAllPlayers != null && RoutineUpgradeAllPlayers.Count != 0)
                foreach (KeyValuePair<BasePlayer,Coroutine> routineUpgradeAllPlayer in RoutineUpgradeAllPlayers)
                {
                    if(routineUpgradeAllPlayer.Value != null)
                        ServerMgr.Instance.StopCoroutine(routineUpgradeAllPlayer.Value);
                }
            
            _ = null;
        }

        private void OnPlayerConnected(BasePlayer player) => RegisteredPlayer(player);

        #endregion

        #region Interface

        #region DrawUI
        
        private void DrawUI_StaticPanel(BasePlayer player)
        {
            if (_interface == null) return;
            CuiHelper.DestroyUi(player, InterfaceBuilder.UI_GREADE_REMOVE_OVERLAY);

            String Interface = InterfaceBuilder.GetInterface("UI_Static_Panel");
            if (Interface == null) return;
            
            CuiHelper.AddUi(player, Interface);
            
            LocalRepositoryUser localRepositoryUser = LocalRepository[player];

            for (Int32 i = 1; i < 6; i++)
                DrawUI_Types(player, i, localRepositoryUser);

            if (_.permission.UserHasPermission(player.UserIDString, PermissionsAllObjects))
                DrawUI_Button_AllObject(player, localRepositoryUser.IsAllObjects(player));
        }
        
        private void DrawUI_Types(BasePlayer player, Int32 Index, LocalRepositoryUser repositoryUser)
        {
            if (_interface == null) return;

            String Interface = InterfaceBuilder.GetInterface("UI_TypeElement");
            if (Interface == null) return;

            ActionType type = (ActionType)Index;
            String NameType = type.ToString();
            Boolean isPermissionType = LocalRepository[player].HasPermissionTypes(player, Index);

            String cmdType = isPermissionType ? type != ActionType.Remove ? $"upgrade.func {Index}" : $"{config.CommandsList.RemoveCommands[0]}" : String.Empty;
            
            String iconType = isPermissionType ? 
                (type switch
                {
                    ActionType.Wood => "WOOD",
                    ActionType.Stone => "STONE",
                    ActionType.Metal => "METAL",
                    ActionType.Hqm => "HQM",
                    _ => "REMOVE"
                }) : "NO_PERMISSION";

            GenerateOffsets(config.InterfaceControllers.TypeElement.TypesElements.OffsetMin, config.InterfaceControllers.TypeElement.TypesElements.OffsetMax, config.InterfaceControllers.TypeElement.OffsetBetweenElements, Index, out String newOffsetMin, out String newOffsetMax);

            Interface = Interface.Replace("%NAME_TYPE%", NameType);
            Interface = Interface.Replace("%TYPE_ICON%", _imageUI.GetImage(iconType));
            Interface = Interface.Replace("%COMMAND_TYPE%", cmdType);
            Interface = Interface.Replace("%OFFSET_MIN%", newOffsetMin);
            Interface = Interface.Replace("%OFFSET_MAX%", newOffsetMax);

            CuiHelper.AddUi(player, Interface);
            
            if (type == repositoryUser.selectedType)
                DrawUI_Types_Selected(player, NameType);
        }
        
        void GenerateOffsets(String OffsetMin, String OffsetMax, Single OffsetBetween, Int32 Index, out String newOffsetMin, out String newOffsetMax)
        {
            String[] minParts = OffsetMin.Split(' ');
            String[] maxParts = OffsetMax.Split(' ');

            if (minParts.Length != 2 || maxParts.Length != 2)
            {
                throw new ArgumentException(LanguageEn ? "OffsetMin and OffsetMax must have exactly two parts separated by a space" : "OffsetMin и OffsetMax должны состоять ровно из двух частей, разделенных пробелом");
            }

            Single min0 = float.Parse(minParts[0]);
            Single min1 = float.Parse(minParts[1]);
        
            Single max0 = float.Parse(maxParts[0]);
            Single max1 = float.Parse(maxParts[1]);

            newOffsetMin = $"{min0 - (OffsetBetween * (Index - 1))} {min1}";
            newOffsetMax = $"{max0 - (OffsetBetween * (Index - 1))} {max1}";
        }
        
        private void DrawUI_Types_Selected(BasePlayer player, String NameType)
        {
            if (_interface == null) return;

            String Interface = InterfaceBuilder.GetInterface("UI_Selectel");
            if (Interface == null) return;
            
            Interface = Interface.Replace("%NAME_TYPE%", NameType);

            CuiHelper.AddUi(player, Interface);
        }  
        
        private void DrawUI_Timer(BasePlayer player, Int32 TimeTick)
        {
            if (_interface == null) return;

            String Interface = InterfaceBuilder.GetInterface("UI_Timer_Label");
            if (Interface == null) return;
            
            Interface = Interface.Replace("%TIMER_TEXT%", TimeTick.ToString());

            CuiHelper.AddUi(player, Interface);
        }
        
        private void DrawUI_Timer_Update(BasePlayer player, Int32 TimeTick)
        {
            if (_interface == null) return;

            String Interface = InterfaceBuilder.GetInterface("UI_Timer_Label_Update");
            if (Interface == null) return;
            
            Interface = Interface.Replace("%TIMER_TEXT%", TimeTick.ToString());

            CuiHelper.AddUi(player, Interface);
        } 
        
        private void DrawUI_Button_AllObject(BasePlayer player, Boolean TurnedAllObject)
        {
            if (_interface == null) return;

            String Interface = InterfaceBuilder.GetInterface("UI_AllObject_Button");
            if (Interface == null) return;
            
            Interface = Interface.Replace("%ALL_BUTTON%", "func.turn.all.objects");

            CuiHelper.AddUi(player, Interface);
            
            if(TurnedAllObject)
                DrawUI_Button_AllObject_Selectel(player);
        }

        private void DrawUI_Button_AllObject_Selectel(BasePlayer player)
        {
            if (_interface == null) return;

            String Interface = InterfaceBuilder.GetInterface("UI_Selectel_AllObject");
            if (Interface == null) return;
            
            Interface = Interface.Replace("%ALL_BUTTON%", "func.turn.all.objects");

            CuiHelper.AddUi(player, Interface);
        }
        
        #endregion

        private class InterfaceBuilder
        {
            #region Vars

            public static InterfaceBuilder Instance;
            public const String UI_GREADE_REMOVE_OVERLAY = "UI_GREADE_REMOVE_OVERLAY";
            public Dictionary<String, String> Interfaces;

            #endregion

            #region Main

            public InterfaceBuilder()
            {
                Instance = this;
                Interfaces = new Dictionary<String, String>();

                Building_Static_Panel();
                Building_TypeElement();
                Building_Selectel();
                Building_Timer();
                Building_AllObjectButton();
                Building_SelectelAllObjects();
                Building_Timer_Update();
            }

            public static void AddInterface(String name, String json)
            {
                if (Instance.Interfaces.ContainsKey(name))
                {
                    _.PrintError($"Error! Tried to add existing cui elements! -> {name}");
                    return;
                }

                Instance.Interfaces.Add(name, json);
            }

            public static String GetInterface(String name)
            {
                if (Instance.Interfaces.TryGetValue(name, out String json) == false)
                    _.PrintWarning($"Warning! UI elements not found by name! -> {name}");

                return json;
            }

            public static void DestroyAll()
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    CuiHelper.DestroyUi(player, UI_GREADE_REMOVE_OVERLAY);
            }

            #endregion

            private void Building_Static_Panel()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Name = UI_GREADE_REMOVE_OVERLAY,
                    Parent = "Overlay",
                    DestroyUi = UI_GREADE_REMOVE_OVERLAY,
                    Components =
                    {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("HUDPANEL") },
                        new CuiRectTransformComponent
                            { AnchorMin = config.InterfaceControllers.StaticPanel.AnchorMin, AnchorMax = config.InterfaceControllers.StaticPanel.AnchorMax, OffsetMin = config.InterfaceControllers.StaticPanel.OffsetMin, OffsetMax = config.InterfaceControllers.StaticPanel.OffsetMax }
                    }
                });

                if (config.InterfaceControllers.closeUIUsed)
                {
                    container.Add(new CuiElement
                    {
                        Name = "CLOSE_BUTTON_IMG",
                        Parent = UI_GREADE_REMOVE_OVERLAY,
                        DestroyUi = "CLOSE_BUTTON_IMG",
                        Components =
                        {
                            new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("CLOSE_BUTTON") },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "138.267 -25.333",
                                OffsetMax = "188.933 25.333"
                            }
                        }
                    });

                    container.Add(new CuiButton
                    {
                        Button = { Color = "0 0 0 0", Command = "close.ui.func" },
                        Text =
                        {
                            Text = "", Font = "robotocondensed-regular.ttf", FontSize = 14,
                            Align = TextAnchor.MiddleCenter, Color = "0 0 0 0"
                        },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
                    }, "CLOSE_BUTTON_IMG", "CLOSE_BUTTON", "CLOSE_BUTTON");
                }

                AddInterface("UI_Static_Panel", container.ToJson());
            }
     
            private void Building_Timer()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Name = "TimerInfo",
                    Parent = UI_GREADE_REMOVE_OVERLAY,
                    DestroyUi = "TimerInfo",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "%TIMER_TEXT%", Font = "robotocondensed-bold.ttf", FontSize = config.InterfaceControllers.TimerElements.SizeTimer,
                            Align = TextAnchor.MiddleCenter, Color = "1 1 1 1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = config.InterfaceControllers.TimerElements.TimerPosition.AnchorMin, AnchorMax = config.InterfaceControllers.TimerElements.TimerPosition.AnchorMax, OffsetMin = config.InterfaceControllers.TimerElements.TimerPosition.OffsetMin,
                            OffsetMax = config.InterfaceControllers.TimerElements.TimerPosition.OffsetMax
                        }
                    }
                });
                
                AddInterface("UI_Timer_Label", container.ToJson());
            }
            
            private void Building_Timer_Update()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Name = "TimerInfo",
                    Parent = UI_GREADE_REMOVE_OVERLAY,
                    Update = true,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "%TIMER_TEXT%", Font = "robotocondensed-bold.ttf", FontSize = config.InterfaceControllers.TimerElements.SizeTimer,
                            Align = TextAnchor.MiddleCenter, Color = "1 1 1 1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = config.InterfaceControllers.TimerElements.TimerPosition.AnchorMin, AnchorMax = config.InterfaceControllers.TimerElements.TimerPosition.AnchorMax, OffsetMin = config.InterfaceControllers.TimerElements.TimerPosition.OffsetMin,
                            OffsetMax = config.InterfaceControllers.TimerElements.TimerPosition.OffsetMax
                        }
                    }
                });
                
                AddInterface("UI_Timer_Label_Update", container.ToJson());
            }
            
            private void Building_TypeElement()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Name = "%NAME_TYPE%",
                    Parent = UI_GREADE_REMOVE_OVERLAY,
                    DestroyUi = "%NAME_TYPE%",
                    Components =
                    {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = "%TYPE_ICON%" },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = config.InterfaceControllers.TypeElement.TypesElements.AnchorMin, AnchorMax = config.InterfaceControllers.TypeElement.TypesElements.AnchorMax, OffsetMin = "%OFFSET_MIN%",
                            OffsetMax = "%OFFSET_MAX%"
                        }
                    }
                });
                
                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = "%COMMAND_TYPE%"},
                    Text = { Text = "", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 0" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-4 -4", OffsetMax = "4 4" }
                },"%NAME_TYPE%","ButtonType_%NAME_TYPE%", "ButtonType_%NAME_TYPE%");
                
                AddInterface("UI_TypeElement", container.ToJson());
            }
            
            private void Building_Selectel()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Name = "SelectedElement",
                    Parent = "%NAME_TYPE%",
                    DestroyUi = "SelectedElement",
                    Components = {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("SELECTED_ELEMENT")},
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-{config.InterfaceControllers.TypeElement.SizeSelectedElement} -{config.InterfaceControllers.TypeElement.SizeSelectedElement}", OffsetMax = 
                            $"{config.InterfaceControllers.TypeElement.SizeSelectedElement} {config.InterfaceControllers.TypeElement.SizeSelectedElement}" }
                    }
                });
                
                AddInterface("UI_Selectel", container.ToJson());
            }
            
            private void Building_SelectelAllObjects()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Name = "SelectedElementAllObject",
                    Parent = "AllType",
                    DestroyUi = "SelectedElementAllObject",
                    Components = {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("SELECTED_ELEMENT")},
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-{config.InterfaceControllers.AllObject.SizeSelectedElement} -{config.InterfaceControllers.AllObject.SizeSelectedElement}", OffsetMax = $"{config.InterfaceControllers.AllObject.SizeSelectedElement} {config.InterfaceControllers.AllObject.SizeSelectedElement}" }
                    }
                });
                
                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = "%ALL_BUTTON%"},
                    Text = { Text = "", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 0" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-4 -4", OffsetMax = "4 4" }
                },"SelectedElementAllObject","AllType_Button", "AllType_Button");
                
                AddInterface("UI_Selectel_AllObject", container.ToJson());
            }

            private void Building_AllObjectButton()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Name = "AdminAllObjects",
                    Parent = UI_GREADE_REMOVE_OVERLAY,
                    DestroyUi = "AdminAllObjects",
                    Components =
                    {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("ALL_BUTTON") },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = config.InterfaceControllers.AllObject.AllObjectsButton.AnchorMin, AnchorMax = config.InterfaceControllers.AllObject.AllObjectsButton.AnchorMax, OffsetMin = config.InterfaceControllers.AllObject.AllObjectsButton.OffsetMin,
                            OffsetMax = config.InterfaceControllers.AllObject.AllObjectsButton.OffsetMax
                        }
                    }
                });
                
                container.Add(new CuiElement
                {
                    Name = "AllType",
                    Parent = "AdminAllObjects",
                    DestroyUi = "AllType",
                    Components =
                    {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("ALL") },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-17.33321 -17.33408904",
                            OffsetMax = "17.333 17.333"
                        }
                    }
                });
                
                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = "%ALL_BUTTON%"},
                    Text = { Text = "", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 0" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-4 -4", OffsetMax = "4 4" }
                },"AllType","AllType_Button", "AllType_Button");

                
                AddInterface("UI_AllObject_Button", container.ToJson());
            }
        }


        #region ImageLoader

        private class ImageUI
        {
            private const String _path = "IQSystem/IQGradeRemove/Images/";
            private const String _printPath = "data/" + _path;
            private readonly Dictionary<String, ImageData> _images = new()
            {
                { "HUDPANEL", new ImageData() },
                { "SELECTED_ELEMENT", new ImageData() },
                { "NO_PERMISSION", new ImageData() },
                { "ALL_BUTTON", new ImageData() },
                { "ALL", new ImageData() },
                { "WOOD", new ImageData() },
                { "STONE", new ImageData() },
                { "METAL", new ImageData() },
                { "HQM", new ImageData() },
                { "REMOVE", new ImageData() },
            };

            private enum ImageStatus
            {
                NotLoaded,
                Loaded,
                Failed
            }

            private class ImageData
            {
                public ImageStatus Status = ImageStatus.NotLoaded;
                public string Id { get; set; }
            }

            public string GetImage(string name)
            {
                ImageData image;
                if (_images.TryGetValue(name, out image) && image.Status == ImageStatus.Loaded)
                    return image.Id;
                return null;
            }

            public void DownloadImage()
            {
                if (config.InterfaceControllers.closeUIUsed)
                    _images.TryAdd("CLOSE_BUTTON", new ImageData());

                KeyValuePair<string, ImageData>? image = null;
                foreach (KeyValuePair<string, ImageData> img in _images)
                {
                    if (img.Value.Status == ImageStatus.NotLoaded)
                    {
                        image = img;
                        break;
                    }
                }

                if (image != null)
                {
                    ServerMgr.Instance.StartCoroutine(ProcessDownloadImage(image.Value));
                }
                else
                {
                    List<String> failedImages = new List<string>();

                    foreach (KeyValuePair<String, ImageData> img in _images)
                    {
                        if (img.Value.Status == ImageStatus.Failed)
                        {
                            failedImages.Add(img.Key);
                        }
                    }

                    if (failedImages.Count > 0)
                    {
                        String images = String.Join(", ", failedImages);
                        _.PrintError(LanguageEn
                            ? $"Failed to load the following images: {images}. Perhaps you did not upload them to the '{_printPath}' folder. Images - https://drive.google.com/drive/folders/1MYmWgN-KHWQhfFxNiweQo9h3SfPwsxDS"
                            : $"Не удалось загрузить следующие изображения: {images}. Возможно, вы не загрузили их в папку '{_printPath}'. Картинки - https://drive.google.com/drive/folders/1MYmWgN-KHWQhfFxNiweQo9h3SfPwsxDS");
                        Interface.Oxide.UnloadPlugin(_.Name);
                    }
                    else
                    {
                        _.Puts(LanguageEn
                            ? $"{_images.Count} images downloaded successfully!"
                            : $"{_images.Count} изображений успешно загружено!");
                        
                        _interface = new InterfaceBuilder();
                    }
                }
            }
            
            public void UnloadImages()
            {
                foreach (KeyValuePair<string, ImageData> item in _images)
                    if(item.Value.Status == ImageStatus.Loaded)
                        if (item.Value?.Id != null)
                            FileStorage.server.Remove(uint.Parse(item.Value.Id), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);

                _images?.Clear();
            }

            private IEnumerator ProcessDownloadImage(KeyValuePair<string, ImageData> image)
            {
                string url = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + _path + image.Key + ".png";

                using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
                {
                    yield return www.SendWebRequest();

                    if (www.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError)
                    {
                        image.Value.Status = ImageStatus.Failed;
                    }
                    else
                    {
                        Texture2D tex = DownloadHandlerTexture.GetContent(www);
                        image.Value.Id = FileStorage.server.Store(tex.EncodeToPNG(), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
                        image.Value.Status = ImageStatus.Loaded;
                        UnityEngine.Object.DestroyImmediate(tex);
                    }

                    DownloadImage();
                }
            }
        }

        #endregion
        
        #endregion
        
        #region Lang
        
        private static StringBuilder sb = new StringBuilder();

        private String GetLang(String LangKey, String userID = null, params Object[] args)
        {
            sb.Clear();
            if (args == null) return lang.GetMessage(LangKey, this, userID);
            sb.AppendFormat(lang.GetMessage(LangKey, this, userID), args);
            return sb.ToString();
        }
        
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<String, String>
            {
                ["UPGRADE_UP_USE_INSTRUCTION"] = "TO IMPROVE THE STRUCTURE, HIT IT WITH A HAMMER",
                ["UPGRADE_REMOVE_USE_INSTRUCTION"] = "TO REMOVE A BUILDING, HIT IT WITH A HAMMER",
                ["UPGRADE_FOREIGN_OBJECT"] = "ITEM INSIDE THE OBJECT",
                ["UPGRADE_SINCE_ATTACKED_BLOCK"] = "OBJECT RECENTLY ATTACKED - CAN BE UPGRADED IN {0}",
                ["UPGRADE_NO_AUTH_BUILDING"] = "CAN'T UPGRADE BUILDINGS ON FOREIGN TERRITORY",
                ["UPGRADE_NO_AUTH_BUILDING_CUPBOARD"] = "THE IMPROVEMENT IS AVAILABLE ONLY WITH THE PRESENCE OF A CUPBOARD",
                ["UPGRADE_NO_ESCAPE"] = "CAN'T UPGRADE BUILDINGS DURING RAID BLOCK",
                ["UPGRADE_NOT_ENOUGHT_RESOURCE"] = "INSUFFICIENT RESOURCES TO UPGRADE",
                ["UPGRADE_REPAIR_OBJECTS"] = "REPAIR OBJECT BEFORE UPGRADING",
                ["UPGRADE_TIME_EXECURE"] = "YOU CAN UPGRADE THE OBJECT IN: {0}",

                ["REMOVE_IQTURRET_NO_DELETE_TUMBLER"] = "CAN'T DELETE THIS ITEM",
                ["REMOVE_NOT_REMOVE_OBJECT"] = "YOU CAN'T DELETE THIS OBJECT",
                ["REMOVE_NOT_REMOVE_ALIEN_OBJECT"] = "YOU CAN'T DELETE FOREIGN OBJECT",
                ["REMOVE_NO_ESCAPE"] = "YOU CAN'T REMOVE BUILDINGS DURING RAID BLOCK",
                ["REMOVE_TIME_EXECUTE"] = "YOU CAN REMOVE THE OBJECT IN: {0}",
                ["REMOVE_FULL_BLOCK_REMOVE"] = "YOU CAN NO LONGER DELETE THIS OBJECT",
                ["REMOVE_SINCE_ATTACKED_BLOCK"] = "THE OBJECT WAS RECENTLY ATTACKED - IT WILL BE POSSIBLE TO DELETE IT AFTER {0}",

                ["ALL_OBJECTS_NO_AUTH"] = "YOU MUST BE AUTHORIZED IN THE CABINET TO PERFORM THIS ACTION",
                ["ALL_OBJECTS_FINDED_OBJECT"] = "AN OBJECT WAS DISCOVERED IN SOME BUILDINGS",

                ["NO_PERMISSION"] = "INSUFFICIENT PERMISSIONS FOR THIS ACTION",

                ["FORMAT_TIME_DAY"] = "D",
                ["FORMAT_TIME_HOURSE"] = "H",
                ["FORMAT_TIME_MINUTES"] = "M",
                ["FORMAT_TIME_SECONDS"] = "S",
            }, this);

            lang.RegisterMessages(new Dictionary<String, String>
            {
                ["UPGRADE_UP_USE_INSTRUCTION"] = "ЧТОБЫ УЛУЧШИТЬ СТРОЕНИЕ - УДАРЬТЕ ПО НЕМУ МОЛОТКОМ",
                ["UPGRADE_REMOVE_USE_INSTRUCTION"] = "ЧТОБЫ УДАЛИТЬ СТРОЕНИЕ - УДАРЬТЕ ПО НЕМУ МОЛОТКОМ",
                ["UPGRADE_FOREIGN_OBJECT"] = "В ОБЪЕКТЕ НАХОДИТСЯ ПРЕДМЕТ",
                ["UPGRADE_SINCE_ATTACKED_BLOCK"] = "ОБЪЕКТ НЕДАВНО АТАКОВАН - УЛУЧШИТЬ МОЖНО БУДЕТ ЧЕРЕЗ {0}",
                ["UPGRADE_NO_AUTH_BUILDING"] = "НЕЛЬЗЯ УЛУЧШАТЬ ПОСТРОЙКИ НА ЧУЖОЙ ТЕРРИТОРИИ",
                ["UPGRADE_NO_AUTH_BUILDING_CUPBOARD"] = "УЛУЧШЕНИЕ ДОСТУПНО ТОЛЬКО С НАЛИЧИЕМ ШКАФА",
                ["UPGRADE_NO_ESCAPE"] = "ВЫ НЕ МОЖЕТЕ УЛУЧШАТЬ ПОСТРОЙКИ ВО ВРЕМЯ РЕЙДБЛОКА",
                ["UPGRADE_NOT_ENOUGHT_RESOURCE"] = "НЕДОСТАТОЧНО РЕСУРСОВ ДЛЯ УЛУЧШЕНИЯ",
                ["UPGRADE_REPAIR_OBJECTS"] = "ПОЧИНИТЕ ОБЪЕКТ ПЕРЕД УЛУЧШЕНИЕМ",
                ["UPGRADE_TIME_EXECURE"] = "ВЫ СМОЖЕТЕ УЛУЧШИТЬ ОБЪЕКТ ЧЕРЕЗ : {0}",

                ["REMOVE_IQTURRET_NO_DELETE_TUMBLER"] = "НЕЛЬЗЯ УДАЛИТЬ ЭТОТ ПРЕДМЕТ",
                ["REMOVE_NOT_REMOVE_OBJECT"] = "ВЫ НЕ МОЖЕТЕ УДАЛИТЬ ЭТОТ ОБЪЕКТ",
                ["REMOVE_NOT_REMOVE_ALIEN_OBJECT"] = "ВЫ НЕ МОЖЕТЕ УДАЛИТЬ ЧУЖОЙ ОБЪЕКТ",
                ["REMOVE_NO_ESCAPE"] = "ВЫ НЕ МОЖЕТЕ УДАЛЯТЬ ПОСТРОЙКИ ВО ВРЕМЯ РЕЙДБЛОКА",
                ["REMOVE_SINCE_ATTACKED_BLOCK"] = "ОБЪЕКТ НЕДАВНО АТАКОВАН - УДАЛИТЬ МОЖНО БУДЕТ ЧЕРЕЗ {0}",
                ["REMOVE_TIME_EXECUTE"] = "ВЫ СМОЖЕТЕ УДАЛИТЬ ОБЪЕКТ ЧЕРЕЗ : {0}",
                ["REMOVE_FULL_BLOCK_REMOVE"] = "ВЫ БОЛЬШЕ НЕ МОЖЕТЕ УДАЛИТЬ ЭТОТ ОБЪЕКТ",

                ["ALL_OBJECTS_NO_AUTH"] = "ВЫ ДОЛЖНЫ БЫТЬ АВТОРИЗИРОВАНЫ В ШКАФУ ДЛЯ ЭТОГО ДЕЙСТВИЯ",
                ["ALL_OBJECTS_FINDED_OBJECT"] = "В НЕКОТОРЫХ СТРОЕНИЯХ БЫЛ ОБНАРУЖЕН ОБЪЕКТ",
                
                ["NO_PERMISSION"] = "НЕДОСТАТОЧНО ПРАВ ДЛЯ ЭТОГО ДЕЙСТВИЯ",
                
                ["FORMAT_TIME_DAY"] = "Д",
                ["FORMAT_TIME_HOURSE"] = "Ч",
                ["FORMAT_TIME_MINUTES"] = "М",
                ["FORMAT_TIME_SECONDS"] = "С",
            }, this, "ru");
            
            PrintWarning(LanguageEn ? "The language file has been successfully uploaded" : "Языковой файл загружен успешно");
        }
        #endregion
        
        #region API

        Int32 API_GET_GRADE_LEVEL_PLAYER(BasePlayer player)
        {
            if (player == null)
                return 0;

            if (!LocalRepository.ContainsKey(player))
                return 0;

            return (Int32)LocalRepository[player].selectedType;
        }
        
        Int32 API_GET_GRADE_TIME_PLAYER(BasePlayer player)
        {
            if (player == null)
                return 0;

            if (!LocalRepository.ContainsKey(player))
                return 0;

            return LocalRepository[player].selectedType == ActionType.None ? 0 : LocalRepository[player].activityTime - CurrentTime() <= 0 ? 0 : Convert.ToInt32(LocalRepository[player].activityTime - CurrentTime());
        }

        Boolean API_IS_REPAIR_TO_GRADE() => config.UpgradePresets.RepairToGrade;

        #endregion
    }
}