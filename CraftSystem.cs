using System.Collections.Generic;
using System.Linq;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("CraftSystem", "rustmods.ru", "2.0.0")]
    [Description("Ui craft system")]
    class CraftSystem : RustPlugin
    {
        #region Вар
        string Layer = "Craft_UI";

        [PluginReference] Plugin ImageLibrary;
        #endregion

        #region Класс
        public class Settings
        {
            [JsonProperty("Название предмета")] public string DisplayName;
            [JsonProperty("Описание предмета")] public string Description;
            [JsonProperty("Верстак для крафта предмета")] public int Workbench;
            [JsonProperty("Короткое название предмета")] public string ShortName;
            [JsonProperty("Команда (если не предмет)")] public string Command;
            [JsonProperty("SkinID предмета")] public ulong SkinID;
            [JsonProperty("Изображение предмета (если используйте команду или скин)")] public string Url;
            [JsonProperty("Список предметов для крафта")] public List<Items> items;
        }

        public class Items
        {
            [JsonProperty("Короткое название предмета")] public string ShortName;
            [JsonProperty("Кол-во предмета")] public int Amount;
            [JsonProperty("SkinID предмета")] public ulong SkinID;
            [JsonProperty("Изображение (если используется скин)")] public string Url;
        }
        #endregion

        #region Конфиг
        Configuration config;
        class Configuration 
        {
            [JsonProperty("Список предметов")] public List<Settings> settings;
            public static Configuration GetNewConfig() 
            {
                return new Configuration
                {
                    settings = new List<Settings>()
                    {
                        new Settings
                        {
                            DisplayName = "Дерево",
                            Description = "Это дерево даст пиздюлей любому!",
                            Workbench = 1,
                            ShortName = "wood",
                            Command = null,
                            SkinID = 0,
                            Url = null,
                            items = new List<Items>()
                            {
                                new Items
                                {
                                    ShortName = "wood",
                                    Amount = 1000,
                                    SkinID = 0,
                                    Url = null
                                },
                                new Items
                                {
                                    ShortName = "burlap.gloves.new",
                                    Amount = 1,
                                    SkinID = 2358890751,
                                    Url = "https://imgur.com/ITJgzdK.png"
                                }
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
                if (config?.settings == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Ошибка чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfig();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Хуки
        void OnServerInitialized()
        {
			    PrintWarning("╔═══════════════════════════════════════════════╗");
                PrintWarning("║            Craft System Plugin                ║");
                PrintWarning("║                                               ║");
                PrintWarning("║            Author: root                       ║");
                PrintWarning($"║           Version: {Version}                      ║");
                PrintWarning("║                                               ║");
                PrintWarning("║                                               ║");
                PrintWarning("║                                               ║");
                PrintWarning("║         Plugin successfully initialized!      ║");
                PrintWarning("╚═══════════════════════════════════════════════╝");
            foreach (var check in config.settings)
            {
                if (check.Url != null)
                    ImageLibrary.Call("AddImage", check.Url, check.Url);
                if (check.ShortName != null)
                    ImageLibrary.Call("AddImage", $"https://rustlabs.com/img/items180/{check.ShortName}.png", check.ShortName);
                
                foreach (var item in check.items)
                {
                    if (item.Url != null)
                        ImageLibrary.Call("AddImage", item.Url, item.Url);
                    if (item.ShortName != null)
                        ImageLibrary.Call("AddImage", $"https://rustlabs.com/img/items180/{item.ShortName}.png", item.ShortName);
                }
            }
        }

        void Unload()
        {
            foreach (var check in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(check, Layer);
        }
        #endregion

        #region Команды
        [ChatCommand("craft")]
        void ChatCraft(BasePlayer player) => CraftUI(player);

        [ConsoleCommand("craft")]
        void ConsoleCraft(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player != null && args.HasArgs(1))
            {
                if (args.Args[0] == "item")
                {
                    ItemUI(player, int.Parse(args.Args[1]));
                }
                if (args.Args[0] == "craftitem")
                {
                    var check = config.settings[int.Parse(args.Args[1])];
                    if (player.currentCraftLevel < check.Workbench)
                    {
                        AlertUI(player, $"Нужен верстак {check.Workbench} уровня");
                        return;
                    }
                    foreach (var item in check.items)
                    {
                        var name = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(item.ShortName).itemid);
                        var haveItem = HaveItem(player, name.info.itemid, item.SkinID, item.Amount);
                        if (!haveItem) 
                        {
                            AlertUI(player, $"У вас не хватает ресурсов для крафта");
                            return;
                        }
                    }
                    foreach (var item in check.items)
                        player.inventory.Take(null, ItemManager.FindItemDefinition(item.ShortName).itemid, item.Amount);
                    if (check.Command != null)
                    {
                        Server.Command(check.Command.Replace("%STEAMID%", player.UserIDString));
                    }
                    if (check.ShortName != null)
                    {
                        var item = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(check.ShortName).itemid, 1, check.SkinID);
                        if (!player.inventory.GiveItem(item))
                        {
                            item.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity, new Quaternion());
                            return;
                        }
                    }
                    SendReply(player, $"Вы успешно скрафтили {check.DisplayName}");
                    CuiHelper.DestroyUi(player, Layer);
                }
                if (args.Args[0] == "skip")
                {
                    CraftUI(player, int.Parse(args.Args[1]));
                }
            }
        }
        #endregion

        #region Интерфейс
        void CraftUI(BasePlayer player, int page = 0)
        {
            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.9", Material = "assets/content/ui/uibackgroundblur.mat" },
            }, "Overlay", Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Close = Layer },
                Text = { Text = "" }
            }, Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.15 0.15", AnchorMax = "0.49 0.85", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0" }
            }, Layer, "Item");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.497 0.154", AnchorMax = "0.502 0.85", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.1" }
            }, Layer);

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"0.479 0.117", AnchorMax = $"0.499 0.15", OffsetMax = "0 0" },
                    Button = { Color = "1 1 1 0.1", Command = page >= 1 ? $"craft skip {page - 1} " : "" },
                    Text = { Text = "<", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 20, Font = "robotocondensed-regular.ttf" }
                }, Layer);

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"0.5 0.117", AnchorMax = $"0.52 0.15", OffsetMax = "0 0" },
                    Button = { Color = "1 1 1 0.1", Command = config.settings.Count() > (page + 1) * 9 ? $"craft skip {page + 1}" : "" },
                    Text = { Text = ">", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 20, Font = "robotocondensed-regular.ttf" }
                }, Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.51 0.15", AnchorMax = "0.85 0.85", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0" }
            }, Layer, "CraftItem");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.85", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.1" }
            }, "CraftItem", "Title");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04 0", AnchorMax = "0.96 1", OffsetMax = "0 0" },
                Text = { Text = $"<size=35><b>Крафт предметов</b></size>\nЗдесь вы можете скрафтить предметы, которые есть в списке(слева)!", Color = "1 1 1 0.5", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Title");

            float width = 1f, height = 0.1113f, startxBox = 0f, startyBox = 1.005f - height, xmin = startxBox, ymin = startyBox, z = 0;
            foreach (var check in config.settings.Skip(page * 9).Take(9))
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMin = "2 2", OffsetMax = "-2 -2" },
                    Image = { Color = "1 1 1 0" }
                }, "Item", "Items");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"0.137 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                    Button = { Color = "1 1 1 0.1", Command = $"craft item {z + page * 9}" },
                    Text = { Text = "", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "Items", "Text");

                var workbench = check.Workbench != 0 ? $"{check.Workbench} уровня!" : "Не требуется!";
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.02 0", AnchorMax = "0.96 1", OffsetMax = "0 0" },
                    Text = { Text = $"<size=20><b>{check.DisplayName}</b></size>\nДля крафта требуется {check.items.Count()} предметов! Верстак {workbench}", Color = "1 1 1 0.5", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "Text");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.13 1", OffsetMax = "0 0" },
                    Image = { Color = "1 1 1 0.1" }
                }, "Items", "Image");

                var image = check.Command != null ? check.Url : check.ShortName;
                container.Add(new CuiElement
                {
                    Parent = "Image",
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", image), Color = "1 1 1 0.8", FadeIn = 1f },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                    }
                });

                xmin += width;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height;
                }
                z++;
            }

            CuiHelper.AddUi(player, container);
            ItemUI(player, 0);
        }

        void ItemUI(BasePlayer player, int z)
        {
            DestroyUI(player);
            var container = new CuiElementContainer();

            var check = config.settings[z];

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.6", AnchorMax = "0.3 0.845", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.1" }
            }, "CraftItem", "ItemImage");

            var image = check.Url != null ? check.Url : check.ShortName;
            container.Add(new CuiElement
            {
                Parent = $"ItemImage",
                Components =
                {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", image) },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "10 10", OffsetMax = "-10 -10" }
                }
            });

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.307 0.6", AnchorMax = "1 0.845", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.1" }
            }, "CraftItem", "ItemDescription");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04 0.08", AnchorMax = "0.96 0.92", OffsetMax = "0 0" },
                Text = { Text = $"<size=20><b>Описание предмета</b></size>\n{check.Description}", Color = "1 1 1 0.5", Align = TextAnchor.UpperLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "ItemDescription");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.445", AnchorMax = "0.7 0.595", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.1" }
            }, "CraftItem", "ItemDesc");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04 0", AnchorMax = "0.96 1", OffsetMax = "0 0" },
                Text = { Text = $"<size=20><b>Что нужно для крафта</b></size>\nСнизу вы можете посмотреть предметы для крафта!", Color = "1 1 1 0.5", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "ItemDesc");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.707 0.445", AnchorMax = "1 0.595", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.1" }
            }, "CraftItem", "ItemCounts");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04 0", AnchorMax = "0.96 1", OffsetMax = "0 0" },
                Text = { Text = $"<size=22><b>{check.items.Count()}</b></size>\nПредметов для крафта", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "ItemCounts");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.707 0.153", AnchorMax = "1 0.438", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.1" }
            }, "CraftItem", "ItemWorkbench");

            var workbench = check.Workbench != 0 ? $"<size=20><b>Верстак</b></size>\nТребуется {check.Workbench} уровень!" : "<size=20><b>Верстак</b></size>\nНе требуется!";

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { Text = $"{workbench}", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "ItemWorkbench");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.707 0.011", AnchorMax = "1 0.145", OffsetMax = "0 0" },
                Button = { Color = "1 1 1 0.1", Command = $"craft craftitem {z}" },
                Text = { Text = "<size=20><b>Скрафтить</b></size>\nПредмет", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "CraftItem", "ItemCraft");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.705 0.442", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "CraftItem", "ItemCount");

            float width = 0.251f, height = 0.33f, startxBox = -0.006f, startyBox = 1f - height, xmin = startxBox, ymin = startyBox;
            for (int x = 0; x < 12; x++)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMin = "2 2", OffsetMax = "-2 -2" },
                    Image = { Color = "1 1 1 0.1" }
                }, "ItemCount", "Items");

                xmin += width;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height;
                }
            }

            float width1 = 0.251f, height1 = 0.33f, startxBox1 = -0.006f, startyBox1 = 1f - height1, xmin1 = startxBox1, ymin1 = startyBox1;
            foreach (var item in check.items)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xmin1} {ymin1}", AnchorMax = $"{xmin1 + width1} {ymin1 + height1 * 1}", OffsetMin = "2 2", OffsetMax = "-2 -2" },
                    Image = { Color = "0 0 0 0" }
                }, "ItemCount", "Items");

                var imagecraft = item.Url != null ? item.Url : item.ShortName;
                container.Add(new CuiElement
                {
                    Parent = "Items",
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", imagecraft), Color = "1 1 1 0.8", FadeIn = 1f },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.02 0.02", AnchorMax = "0.98 0.98", OffsetMax = "0 0" },
                    Text = { Text = $"{item.Amount} шт.", Color = "1 1 1 0.5", Align = TextAnchor.LowerRight, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "Items");

                xmin1 += width1;
                if (xmin1 + width1 >= 1)
                {
                    xmin1 = startxBox1;
                    ymin1 -= height1;
                }
            }

            CuiHelper.AddUi(player, container);
        }


        void AlertUI(BasePlayer player, string text)
        {
            CuiHelper.DestroyUi(player, "AlertUI");
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.2 0.07", AnchorMax = "0.8 0.115", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, "AlertUI");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { Text = $"{text}", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf", FadeIn = 1.5f }
            }, "AlertUI");

            CuiHelper.AddUi(player, container);
            timer.In(5f, () => {
                CuiHelper.DestroyUi(player, "AlertUI");
            });
        }
        #endregion

        #region Хелпер
        void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "ItemImage");
            CuiHelper.DestroyUi(player, "ItemDescription");
            CuiHelper.DestroyUi(player, "ItemDesc");
            CuiHelper.DestroyUi(player, "ItemWorkbench");
            CuiHelper.DestroyUi(player, "ItemCraft");
            CuiHelper.DestroyUi(player, "ItemCount");
            CuiHelper.DestroyUi(player, "ItemCounts");
        }

        public bool HaveItem(BasePlayer player, int itemID, ulong skinID, int amount)
        {
            if (skinID == 0U)
            {
                if (player.inventory.FindItemByItemID(itemID) != null &&
                    player.inventory.FindItemByItemID(itemID).amount >= amount) return true;
                return false;
            }
            else
            {

                List<Item> items = new List<Item>();

                items.AddRange(player.inventory.FindItemsByItemID(itemID));

                foreach (var item in items)
                {
                    if (item.skin == skinID && item.amount >= amount)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        #endregion
    }
}