using System.Text;
using UnityEngine;
using System;
using Oxide.Core.Plugins;
using ConVar;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using System.Collections;
using System.Linq;
		   		 		  						  	   		  	  			  	  			  		  		   			
namespace Oxide.Plugins
{
    [Info("IQKits", "Mercury", "1.3.10")]
    [Description("Лучшие наборы из всех,которые есть")]
    internal class IQKits : RustPlugin
    {

        private Boolean API_IS_KIT_PLAYER(BasePlayer player, String KitKey)
        {
            if (player == null) return false;
            if (String.IsNullOrWhiteSpace(KitKey)) return false;
            if (!DataKitsUserList.ContainsKey(player.userID)) return false;
            if (!config.KitList.ContainsKey(KitKey)) return false;
            return DataKitsUserList[player.userID].InfoKitsList.ContainsKey(KitKey);
        }

        private Boolean API_IS_KIT(String KitKey)
        {
            if (String.IsNullOrWhiteSpace(KitKey)) return false;
            return config.KitList.ContainsKey(KitKey);
        }

                public void SendChat(BasePlayer player, String Message, Chat.ChatChannel channel = Chat.ChatChannel.Global)
        {
            Configuration.ReferenceSettings.IQChatSettings Chat = config.ReferenceSetting.IQChatSetting;
            if (IQChat)
                IQChat?.Call("API_ALERT_PLAYER", player, Message, Chat.CustomPrefix, Chat.CustomAvatar);
            else player.SendConsoleCommand("chat.add", channel, 0, Message);
        }

        private void OnServerShutdown() => Unload();

        private List<String> API_KIT_GET_ALL_KIT_LIST()
        {
            List<String> KitList = new List<String>();
            foreach (KeyValuePair<String, Configuration.Kits> Kit in config.KitList)
                KitList.Add(Kit.Key);

            return KitList;
        }
        
                private void Interface_Alert_Kits(BasePlayer player, String Message)
        {
            DestroyAlert(player);
            CuiElementContainer container = new CuiElementContainer();
            Configuration.InterfaceSettings Interface = config.InterfaceSetting;
            Single FadeIn = Interface.InterfaceFadeIn;
            Single FadeOut = Interface.InterfaceFadeOut;
            String AlertBackground = $"INFO_ALERT_BACKGROUND_{Interface.PNGAlert}";

            container.Add(new CuiElement
            {
                FadeOut = FadeOut,
                Parent = IQKITS_OVERLAY,
                Name = $"INFO_ALERT_BACKGROUND",
                Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn, Png = GetImage(AlertBackground), Color = HexToRustFormat(Interface.HEXBlock) },
                        new CuiRectTransformComponent{ AnchorMin = "0.3213542 0.01018518", AnchorMax = $"0.6958333 0.1101852"},
                    }
            });

            container.Add(new CuiLabel
            {
                FadeOut = FadeOut,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { FadeIn = FadeIn, Text = Message.ToUpper(), Color = HexToRustFormat(Interface.HEXLabels), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
            }, "INFO_ALERT_BACKGROUND", "TITLE_ALERT");

            CuiHelper.AddUi(player, container);

            timer.Once(2.5f, () => { DestroyAlert(player); });
        }
        public static DateTime RealTime = DateTime.Now.Date;

        private String GetRankName(String Key)
        {
            String Rank = String.Empty;
            if (!IQRankSystem) return Rank;
            return (String)IQRankSystem?.Call("API_GET_RANK_NAME", Key);
        }
        
                private void RegisteredPermissions()
        {
            Configuration.GeneralSettings GeneralSettings = config.GeneralSetting;
            Dictionary<String, Configuration.Kits> KitList = config.KitList;

            foreach (Configuration.GeneralSettings.AutoKit.KitSettings PermissionGeneral in GeneralSettings.AutoKitSettings.KitListRandom)
            {
                if (!permission.PermissionExists(PermissionGeneral.Permissions, this))
                    permission.RegisterPermission(PermissionGeneral.Permissions, this);
            }

            foreach (Configuration.GeneralSettings.AutoKit.KitSettings PermissionGeneral in GeneralSettings.AutoKitSettings.KitListPriority)
            {
                if (!permission.PermissionExists(PermissionGeneral.Permissions, this))
                    permission.RegisterPermission(PermissionGeneral.Permissions, this);
            }
		   		 		  						  	   		  	  			  	  			  		  		   			
            foreach (Configuration.GeneralSettings.AutoKit.BiomeKits PermissionGeneral in GeneralSettings.AutoKitSettings.BiomeStartedKitList)
            {
                if (!permission.PermissionExists(PermissionGeneral.Kits.Permissions, this))
                    permission.RegisterPermission(PermissionGeneral.Kits.Permissions, this);
            }

            foreach (KeyValuePair<String, Configuration.Kits> PermissionKits in KitList)
            {
                if (!permission.PermissionExists(PermissionKits.Value.Permission, this))
                    permission.RegisterPermission(PermissionKits.Value.Permission, this);
            }
        }

        private void DestroyInfoKits(BasePlayer player)
        {
            for (Int32 i = 0; i < 40; i++)
            {
                CuiHelper.DestroyUi(player, $"KIT_ITEM_AMOUNT_{i}");
                CuiHelper.DestroyUi(player, $"RARE_LABEL_{i}");
                CuiHelper.DestroyUi(player, $"RARE_BACKGROUND_{i}");
                CuiHelper.DestroyUi(player, $"KIT_ITEM_{i}");
                CuiHelper.DestroyUi(player, $"ITEM_{i}");
            }
            CuiHelper.DestroyUi(player, $"TITLE_KIT_INFO");
            CuiHelper.DestroyUi(player, $"HIDE_INFO_BTN");
            CuiHelper.DestroyUi(player, $"INFO_BACKGROUND");
        }

        private void Interface_IQ_Kits(BasePlayer player)
        {
            DestroyAll(player);
            player.SetFlag(BaseEntity.Flags.Reserved3, true);
            CuiElementContainer container = new CuiElementContainer();
            Configuration.InterfaceSettings Interface = config.InterfaceSetting;
            Single FadeIn = Interface.InterfaceFadeIn;
            Single FadeOut = Interface.InterfaceFadeOut;

            container.Add(new CuiPanel
            {
                FadeOut = FadeOut,
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HEXBackground), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
            }, "Overlay", IQKITS_OVERLAY);

            container.Add(new CuiLabel
            {
                FadeOut = FadeOut,
                RectTransform = { AnchorMin = "0 0.915", AnchorMax = "1 1" },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_TITLE", player.UserIDString), Color = HexToRustFormat(Interface.HEXLabels), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
            }, IQKITS_OVERLAY, "TITLE");

            container.Add(new CuiLabel
            {
                FadeOut = FadeOut,
                RectTransform = { AnchorMin = "0 0.8925912", AnchorMax = "1 0.9351871" },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_DESCRIPTION", player.UserIDString), Color = HexToRustFormat(Interface.HEXLabels), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
            }, IQKITS_OVERLAY, "DESCRIPTION");

            if (Interface.CloseType)
            {
                container.Add(new CuiButton
                {
                    FadeOut = FadeOut - 0.2f,
                    RectTransform = { AnchorMin = "0.871875 0.9388889", AnchorMax = "1 1" },
                    Button = { FadeIn = FadeIn, Command = $"kit_ui_func close.ui", Color = "0 0 0 0" },
                    Text = { FadeIn = FadeIn, Text = GetLang("UI_CLOSE_BTN", player.UserIDString), Color = HexToRustFormat(Interface.HEXLabels), Align = TextAnchor.MiddleCenter }
                }, IQKITS_OVERLAY, "CLOSE_BTN");
            }
            else
            {
                container.Add(new CuiButton
                {
                    FadeOut = FadeOut - 0.2f,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { FadeIn = FadeIn, Command = $"kit_ui_func close.ui", Color = "0 0 0 0" },
                    Text = { FadeIn = FadeIn, Text = "" }
                }, IQKITS_OVERLAY, "CLOSE_BTN");
            }


            CuiHelper.AddUi(player, container);
            Interface_Loaded_Kits(player);
            PlayersRoutine[player] = ServerMgr.Instance.StartCoroutine(UI_UpdateCooldown(player));
        }

        
        
                public Coroutine DownloadImage = null;
        private Dictionary<BasePlayer, Coroutine> PlayersRoutine = new Dictionary<BasePlayer, Coroutine>();

        private Int32 API_KIT_PLAYER_GET_COOLDOWN(BasePlayer player, String KitKey)
        {
            if (player == null) return 0;
            if (String.IsNullOrWhiteSpace(KitKey)) return 0;
            if (!DataKitsUserList.ContainsKey(player.userID))
            {
                PrintError($"API_KIT_PLAYER_GET_COOLDOWN : Такого игрока не существует в дата-файле");
                return 0;
            }
            if (!config.KitList.ContainsKey(KitKey))
            {
                PrintError($"API_KIT_PLAYER_GET_COOLDOWN : Ключа {KitKey} не существует");
                return 0;
            }
            if (!DataKitsUserList[player.userID].InfoKitsList.ContainsKey(KitKey))
            {
                PrintError($"API_KIT_PLAYER_GET_COOLDOWN : У игрока нет данного набора {KitKey}");
                return 0;
            }
            return DataKitsUserList[player.userID].InfoKitsList[KitKey].Cooldown;
        }

        private List<String> API_KIT_GET_AUTO_KIT_LIST()
        {
            List<String> KitList = new List<String>();
            for (Int32 i = 0; i < config.GeneralSetting.AutoKitSettings.KitListRandom.Count; i++)
                KitList.Add(config.GeneralSetting.AutoKitSettings.KitListRandom[i].StartKitKey);
            return KitList;
        }

        public Boolean AddImage(String url, String shortname, UInt64 skin = 0)
        {
            return (Boolean)ImageLibrary?.Call("AddImage", url, shortname, skin);
        }

        public Boolean HasImage(String imageName)
        {
            return (Boolean)ImageLibrary?.Call("HasImage", imageName);
        }
        private void GiveItem(BasePlayer player, Item item, ItemContainer cont = null)
        {
            if (item == null) return;
            PlayerInventory inv = player.inventory;

            Boolean MovedContainer = item.MoveToContainer(cont) || item.MoveToContainer(inv.containerMain);
            if (!MovedContainer)
            {
                if (cont == inv.containerBelt)
                    MovedContainer = item.MoveToContainer(inv.containerWear);
                if (cont == inv.containerWear)
                    MovedContainer = item.MoveToContainer(inv.containerBelt);
            }

            if (!MovedContainer)
                item.Drop(player.GetCenter(), player.GetDropVelocity());
        }

        public void SendImage(BasePlayer player, String imageName, UInt64 imageId = 0)
        {
            ImageLibrary?.Call("SendImage", player, imageName, imageId);
        }

        
        private IEnumerable<KeyValuePair<String, Configuration.Kits>> GetKits(BasePlayer player)
        {
            IEnumerable<KeyValuePair<String, Configuration.Kits>> Kits = config.KitList.Where(k => ((k.Value.TypeKit != TypeKits.Started && WipeTime >= k.Value.WipeOpened)
                                                     && (((k.Value.TypeKit == TypeKits.Amount || k.Value.TypeKit == TypeKits.AmountCooldown) && (DataKitsUserList[player.userID].InfoKitsList.ContainsKey(k.Key) && DataKitsUserList[player.userID].InfoKitsList[k.Key].Amount > 0)) || k.Value.TypeKit == TypeKits.Cooldown)
                                                     && (String.IsNullOrWhiteSpace(k.Value.Permission) || permission.UserHasPermission(player.UserIDString, k.Value.Permission))
                                                     && !IsRaidBlocked(player, k.Value.UseRaidBlock)
                                                     && IsRank(player.userID, k.Value.RankUser)
                                                     ));
		   		 		  						  	   		  	  			  	  			  		  		   			
            return Kits;
        }
        public String GetLang(String LangKey, String userID = null, params System.Object[] args)
        {
            sb.Clear();
            if (args != null)
            {
                sb.AppendFormat(lang.GetMessage(LangKey, this, userID), args);
                return sb.ToString();
            }
            return lang.GetMessage(LangKey, this, userID);
        }
        
                private void DestroyAll(BasePlayer player)
        {
            DestroyAlert(player);
            DestroyInfoKits(player);
            DestroyKits(player);
            CuiHelper.DestroyUi(player, "CLOSE_BTN");
            CuiHelper.DestroyUi(player, "DESCRIPTION");
            CuiHelper.DestroyUi(player, "TITLE");
            CuiHelper.DestroyUi(player, IQKITS_OVERLAY);
            player.SetFlag(BaseEntity.Flags.Reserved3, false);
		   		 		  						  	   		  	  			  	  			  		  		   			
            if (PlayersRoutine != null)
                if (PlayersRoutine.ContainsKey(player) && PlayersRoutine[player] != null)
                {
                    ServerMgr.Instance.StopCoroutine(PlayersRoutine[player]);
                    PlayersRoutine[player] = null;
                }
            
        }

        private String API_KIT_GET_NAME(String KitKey)
        {
            if (String.IsNullOrWhiteSpace(KitKey)) return "NONE";
            if (!config.KitList.ContainsKey(KitKey)) return "NONE";
            return config.KitList[KitKey].DisplayName;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private enum TypeContent
        {
            Ammo,
            Contents
        }
        
                private String GetImage(String fileName, UInt64 skin = 0)
        {
            String imageId = (String)plugins.Find("ImageLibrary").CallHook("GetImage", fileName, skin);
            if (!String.IsNullOrEmpty(imageId))
                return imageId;
            return String.Empty;
        }

        
        
        private void API_KIT_GIVE(BasePlayer player, String KitKey)
        {
            if (player == null) return;
            if (!config.KitList.ContainsKey(KitKey))
            {
                PrintError($"Ключа {KitKey} не существует, набор не выдан!");
                return;
            }
            ParseAndGive(player, KitKey);
        }

        private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        
                private void CheckKit()
        {
            foreach (KeyValuePair<UInt64, DataKitsUser> Data in DataKitsUserList)
            {
                UInt64 PlayerID = Data.Key;

                foreach (KeyValuePair<String, Configuration.Kits> kitList in config.KitList.Where(k => DataKitsUserList[PlayerID].InfoKitsList.ContainsKey(k.Key)))
                {
                    String KitKey = kitList.Key;
                    DataKitsUser.InfoKits DataPlayer = Data.Value.InfoKitsList[KitKey];

                    if (!permission.UserHasPermission(PlayerID.ToString(), kitList.Value.Permission))
                        DataKitsUserList[PlayerID].InfoKitsList.Remove(KitKey);
                }
            }
        }
		   		 		  						  	   		  	  			  	  			  		  		   			
        private void OnPlayerDisconnected(BasePlayer player, String reason)
        {
            player.SetFlag(BaseEntity.Flags.Reserved3, false);

            if (PlayersRoutine.ContainsKey(player))
                PlayersRoutine.Remove(player);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            CachingImage(player);
            RegisteredDataUser(player);

            if (!PlayersRoutine.ContainsKey(player))
                PlayersRoutine.Add(player, null);
        }
        private void LoadedImage()
        {
            Configuration.InterfaceSettings Interface = config.InterfaceSetting;
            foreach (KeyValuePair<String, Configuration.Kits> Kit in config.KitList.Where(k => !String.IsNullOrWhiteSpace(k.Value.PNG)))
            {
                if (!HasImage($"KIT_{Kit.Value.PNG}"))
                    AddImage(Kit.Value.PNG, $"KIT_{Kit.Value.PNG}");

                foreach (Configuration.Kits.ItemsKit img in Kit.Value.ItemKits.Where(i => !String.IsNullOrWhiteSpace(i.PNG)))
                {
                    if (!HasImage($"ITEM_KIT_PNG_{img.PNG}"))
                        AddImage(img.PNG, $"ITEM_KIT_PNG_{img.PNG}");
                }
            }
            if (!HasImage($"INFO_BACKGROUND_{Interface.PNGInfoPanel}"))
                AddImage(Interface.PNGInfoPanel, $"INFO_BACKGROUND_{Interface.PNGInfoPanel}");

            if (!HasImage($"INFO_ALERT_BACKGROUND_{Interface.PNGAlert}"))
                AddImage(Interface.PNGAlert, $"INFO_ALERT_BACKGROUND_{Interface.PNGAlert}");

            DownloadImage = ServerMgr.Instance.StartCoroutine(DownloadImages());
        }
        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                DestroyAll(player);

            if (DownloadImage != null)
                ServerMgr.Instance.StopCoroutine(DownloadImage);
		   		 		  						  	   		  	  			  	  			  		  		   			
            CheckKit();
            WriteData();

            PlayersRoutine = null;
            DownloadImage = null;
        }

        public static String FormatTime(TimeSpan time)
        {
            String result = String.Empty;
            if (time.Days != 0)
                result = $"{Format(time.Days, "дней", "дня", "день")}";

            if (time.Hours != 0 && time.Days == 0)
                result = $"{Format(time.Hours, "часов", "часа", "час")}";

            if (time.Minutes != 0 && time.Hours == 0 && time.Days == 0)
                result = $"{Format(time.Minutes, "минут", "минуты", "минута")}";
		   		 		  						  	   		  	  			  	  			  		  		   			
            if (time.Seconds != 0 && time.Days == 0 && time.Minutes == 0 && time.Hours == 0)
                result = $"{Format(time.Seconds, "секунд", "секунды", "секунда")}";

            return result;
        }
        
                private Boolean IsRank(UInt64 userID, String Key)
        {
            if (!IQRankSystem) return true;
            if (String.IsNullOrWhiteSpace(Key)) return true;
            return (Boolean)IQRankSystem?.Call("API_GET_AVAILABILITY_RANK_USER", userID, Key);
        }

        private void ParseAndGive(BasePlayer player, String KitKey)
        {
            Configuration.Kits Kit = config.KitList[KitKey];
            List<Configuration.Kits.ItemsKit> ItemKit = Kit.ItemKits;
            foreach (Configuration.Kits.ItemsKit Item in ItemKit)
            {
                if (Item.Rare != 0)
                {
                    Int32 Rare = IQPlagueSkill ? IS_SKILL_RARE(player) ? Item.Rare + GET_SKILL_RARE_PERCENT() : Item.Rare : Item.Rare;
                    if (!IsRareDrop(Rare)) continue;
                }

                if (!String.IsNullOrWhiteSpace(Item.Command))
                {
                    rust.RunServerCommand(Item.Command.Replace("%STEAMID%", player.UserIDString));
                }
                else
                {
                    Int32 Amount = Item.RandomDropSettings.UseRandomItems ? UnityEngine.Random.Range(Item.RandomDropSettings.MinAmount, Item.RandomDropSettings.MaxAmount) : (Item.Amount > 1 ? Item.Amount : 1);
                    Item item = ItemManager.CreateByName(Item.Shortname, Amount, Item.SkinID);
                    if (!String.IsNullOrWhiteSpace(Item.DisplayName))
                        item.name = Item.DisplayName;

                    foreach (Configuration.Kits.ItemsKit.ItemContents Content in Item.ContentsItem)
                    {
                        Item ItemContent = ItemManager.CreateByName(Content.Shortname, Content.Amount);
                        ItemContent.condition = Content.Condition;
                        switch (Content.ContentType)
                        {
                            case TypeContent.Contents:
                                {
                                    ItemContent.MoveToContainer(item.contents);
                                    break;
                                }
                            case TypeContent.Ammo:
                                {
                                    BaseProjectile Weapon = item.GetHeldEntity() as BaseProjectile;
                                    if (Weapon != null)
                                    {
                                        Weapon.primaryMagazine.contents = ItemContent.amount;
                                        Weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(Content.Shortname);
                                    }
                                    break;
                                }
                        }
                    }
		   		 		  						  	   		  	  			  	  			  		  		   			
                    GiveItem(player, item, Item.ContainerItemType == ContainerItem.containerBelt ? player.inventory.containerBelt : Item.ContainerItemType == ContainerItem.containerWear ? player.inventory.containerWear : player.inventory.containerMain);
                }
            }
        }

        private List<Configuration.Kits.ItemsKit> GetPlayerItems(BasePlayer player)
        {
            List<Configuration.Kits.ItemsKit> kititems = new List<Configuration.Kits.ItemsKit>();
            foreach (Item item in player.inventory.containerWear.itemList)
            {
                if (item != null)
                {
                    Configuration.Kits.ItemsKit iteminfo = ItemToKit(item, ContainerItem.containerWear);
                    kititems.Add(iteminfo);
                }
            }
            foreach (Item item in player.inventory.containerMain.itemList)
            {
                if (item != null)
                {
                    Configuration.Kits.ItemsKit iteminfo = ItemToKit(item, ContainerItem.containerMain);
                    kititems.Add(iteminfo);
                }
            }
            foreach (Item item in player.inventory.containerBelt.itemList)
            {
                if (item != null)
                {
                    Configuration.Kits.ItemsKit iteminfo = ItemToKit(item, ContainerItem.containerBelt);
                    kititems.Add(iteminfo);
                }
            }
            return kititems;
        }

        private void ReadData()
        {
            if ((Oxide.Core.Interface.Oxide.DataFileSystem.GetFile("IQKits/KitsData") != null && Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Hash<UInt64, DataKitsUser>>("IQKits/KitsData").Count > 0)
            && (Oxide.Core.Interface.Oxide.DataFileSystem.GetFile("IQSystem/IQKits/KitsData") == null || Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Hash<UInt64, DataKitsUser>>("IQSystem/IQKits/KitsData").Count == 0))
            {
                DataKitsUserList = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Hash<UInt64, DataKitsUser>>("IQKits/KitsData");
                PrintWarning($"Миграция дата-файла..");
                NextTick(() =>
                {
                    Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQSystem/IQKits/KitsData", DataKitsUserList);
                    DataKitsUserList = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Hash<UInt64, DataKitsUser>>("IQSystem/IQKits/KitsData");
                    PrintWarning($"Миграция дата-файла завершена!\nПеренесено {DataKitsUserList.Count} записей");
                });
            }
            else DataKitsUserList = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Hash<UInt64, DataKitsUser>>("IQSystem/IQKits/KitsData");
        }

        private System.Object OnPlayerRespawned(BasePlayer player)
        {
            player.inventory.Strip();
            AutoKitGive(player);
            return null;
        }

        
                private Boolean IS_SKILL_COOLDOWN(BasePlayer player)
        {
            return (Boolean)IQPlagueSkill?.CallHook("API_IS_COOLDOWN_SKILL_KITS", player);
        }

        private Int32 API_KIT_GET_MAX_COOLDOWN(String KitKey)
        {
            if (String.IsNullOrWhiteSpace(KitKey)) return 0;
            if (!config.KitList.ContainsKey(KitKey))
            {
                PrintError($"API_KIT_GET_MAX_COOLDOWN : Ключа {KitKey} не существует!");
                return 0;
            }
            return config.KitList[KitKey].CoolDown;
        }
        
        [ChatCommand("kit")]
        private void IQKITS_ChatCommand(BasePlayer player, String cmd, String[] arg)
        {
            if (player.HasFlag(BaseEntity.Flags.Reserved3))
                return;
            
            if (arg.Length == 1)
            {
                String KitKey = arg[0];
                if (String.IsNullOrWhiteSpace(KitKey))
                {
                    SendChat(player, GetLang("CHAT_ALERT_PLAYER_KIT_COMMAND_EMPTY_KEY", player.UserIDString));
                    return;
                }

                if (!DataKitsUserList[player.userID].InfoKitsList.ContainsKey(KitKey))
                {
                    String KistList = String.Empty;

                    foreach (KeyValuePair<String, DataKitsUser.InfoKits> keyValuePair in DataKitsUserList[player.userID].InfoKitsList.OrderBy(x => x.Value.Cooldown >= CurrentTime()).Where(k => config.KitList.ContainsKey(k.Key) && ((config.KitList[k.Key].TypeKit != TypeKits.Started && WipeTime >= config.KitList[k.Key].WipeOpened) && (((config.KitList[k.Key].TypeKit == TypeKits.Amount || config.KitList[k.Key].TypeKit == TypeKits.AmountCooldown) && (DataKitsUserList[player.userID].InfoKitsList.ContainsKey(k.Key) && DataKitsUserList[player.userID].InfoKitsList[k.Key].Amount > 0)) || config.KitList[k.Key].TypeKit == TypeKits.Cooldown))))
                    {
                        Boolean IsCooldown = keyValuePair.Value.Cooldown >= CurrentTime();
                        String InfoAmountAndCooldown = IsCooldown ? GetLang("CHAT_ALERT_PLAYER_INFO_KIT_LIST_KIT_FORMAT_COOLDOWN", player.UserIDString, keyValuePair.Key, FormatTime(TimeSpan.FromSeconds(keyValuePair.Value.Cooldown - CurrentTime()))) : keyValuePair.Value.Amount != 0 ? GetLang("CHAT_ALERT_PLAYER_INFO_KIT_LIST_KIT_FORMAT_AMOUNT", player.UserIDString, keyValuePair.Key, keyValuePair.Value.Amount) : GetLang("CHAT_ALERT_PLAYER_INFO_KIT_LIST_KIT_FORMAT_YES", player.UserIDString, keyValuePair.Key);

                        KistList += InfoAmountAndCooldown;
                    }
                    SendChat(player, GetLang("CHAT_ALERT_PLAYER_INFO_KIT_LIST", player.UserIDString, KistList));
                    return;
                }
                
                TakeKit(player, KitKey, true);
                return;
            }
            
            if (arg.Length < 2)
            {
                PagePlayers[player] = 0;
                Interface_IQ_Kits(player);
                return;
            }

            switch (arg[0])
            {
                case "create":
                case "createkit":
                case "add":
                case "new":
                    {
                        if (!player.IsAdmin) return;
                        String NameKit = arg[1];
                        if (String.IsNullOrWhiteSpace(NameKit))
                        {
                            SendChat(player, "Введите корректное название!");
                            return;
                        }
                        CreateNewKit(player, NameKit);
                        break;
                    }
                case "remove":
                case "delete":
                case "revoke":
                    {
                        if (!player.IsAdmin) return;
                        String NameKit = arg[1];
                        if (String.IsNullOrWhiteSpace(NameKit))
                        {
                            SendChat(player, "Введите корректное название!");
                            return;
                        }
                        KitRemove(player, NameKit);
                        break;
                    }
                case "copy":
                case "edit":
                    {
                        if (!player.IsAdmin) return;
                        String NameKit = arg[1];
                        if (String.IsNullOrWhiteSpace(NameKit))
                        {
                            SendChat(player, "Введите корректное название!");
                            return;
                        }
                        KitEdit(player, NameKit);
                        break;
                    }
                case "give":
                    {
                        if (!player.IsAdmin) return;
                        String IDarName = arg[1];
                        if (String.IsNullOrWhiteSpace(IDarName))
                        {
                            SendChat(player, "Введите корректное имя или ID");
                            return;
                        }
                        BasePlayer TargetUser = BasePlayer.Find(IDarName);
                        if (TargetUser == null)
                        {
                            SendChat(player, "Такого игрока нет на сервере");
                            return;
                        }
                        String KitKey = arg[2];
                        if (String.IsNullOrWhiteSpace(KitKey))
                        {
                            SendChat(player, "Введите корректный ключ набора");
                            return;
                        }
                        if (!config.KitList.ContainsKey(KitKey))
                        {
                            SendChat(player, "Набора с данным ключем не существует");
                            return;
                        }
                        ParseAndGive(TargetUser, KitKey);
                        break;
                    }
            }
        }

        private void Init()
        {
            ReadData();
        }
        
                private void Interface_Info_Kits(BasePlayer player, String KitKey)
        {
            DestroyInfoKits(player);
            CuiElementContainer container = new CuiElementContainer();
            Configuration.InterfaceSettings Interface = config.InterfaceSetting;
            Single FadeIn = Interface.InterfaceFadeIn;
            Single FadeOut = Interface.InterfaceFadeOut;
            Configuration.Kits Kit = config.KitList[KitKey];
            container.Add(new CuiElement
            {
                FadeOut = FadeOut,
                Parent = IQKITS_OVERLAY,
                Name = $"INFO_BACKGROUND",
                Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn, Png = GetImage($"INFO_BACKGROUND_{Interface.PNGInfoPanel}"),Color = HexToRustFormat(Interface.HEXBlock) },
                        new CuiRectTransformComponent{ AnchorMin = "0.4005208 0.2416667", AnchorMax = $"0.5958334 0.825"},
                    }
            });

            container.Add(new CuiButton
            {
                FadeOut = FadeOut - 0.2f,
                RectTransform = { AnchorMin = "0.02933349 0.01269239", AnchorMax = "0.9706663 0.1111111" },
                Button = { FadeIn = FadeIn, Command = $"kit_ui_func hide.info", Color = HexToRustFormat(Interface.HEXInfoItemButton) },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_HIDE_BTN", player.UserIDString), Color = HexToRustFormat(Interface.HEXLabelsInfoItemButton), Align = TextAnchor.MiddleCenter }
            }, $"INFO_BACKGROUND", $"HIDE_INFO_BTN");

            container.Add(new CuiLabel
            {
                FadeOut = FadeOut,
                RectTransform = { AnchorMin = "0.3916669 0.1444444", AnchorMax = "0.60625 0.237037" },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_WHAT_INFO_TITLE", player.UserIDString, Kit.DisplayName.ToUpper()), Color = HexToRustFormat(Interface.HEXLabels), Font = "robotocondensed-bold.ttf", Align = TextAnchor.UpperCenter }
            }, IQKITS_OVERLAY, $"TITLE_KIT_INFO");

                        Int32 ItemCount = 0;
            Single itemMinPosition = 219f;
            Single itemWidth = 0.413646f - 0.24f; /// Ширина
            Single itemMargin = 0.439895f - 0.415f; /// Расстояние между 
            Int32 itemCount = Kit.ItemKits.Count;
            Single itemMinHeight = 0.89f; // Сдвиг по вертикали
            Single itemHeight = 0.1f; /// Высота
            Int32 ItemTarget = 5;

            if (itemCount > ItemTarget)
            {
                itemMinPosition = 0.5f - ItemTarget / 2f * itemWidth - (ItemTarget - 1) / 2f * itemMargin;
                itemCount -= ItemTarget;
            }
            else
            {
                itemMinPosition = 0.5f - itemCount / 2f * itemWidth - (itemCount - 1) / 2f * itemMargin;
            }
            
            Int32 i = 0;
            foreach (Configuration.Kits.ItemsKit Item in Kit.ItemKits.Take(35))
            {
                container.Add(new CuiElement
                {
                    FadeOut = FadeOut,
                    Parent = "INFO_BACKGROUND",
                    Name = $"KIT_ITEM_{i}",
                    Components = // debug
                    {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat("#37353E77") },
                        new CuiRectTransformComponent { AnchorMin = $"{itemMinPosition} {itemMinHeight}", AnchorMax = $"{itemMinPosition + itemWidth} {itemMinHeight + itemHeight}" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HEXBlockItemInfo), Distance = "0 -1.5", UseGraphicAlpha = true }
                    }
                });
		   		 		  						  	   		  	  			  	  			  		  		   			
                if (String.IsNullOrWhiteSpace(Item.Sprite))
                {
                    CuiRawImageComponent ComponentAvatar = !String.IsNullOrWhiteSpace(Item.PNG) ? new CuiRawImageComponent { FadeIn = FadeIn, Png = GetImage($"ITEM_KIT_PNG_{Item.PNG}") } : new CuiRawImageComponent { FadeIn = FadeIn, Png = GetImage($"{Item.Shortname}_128px") };
                    container.Add(new CuiElement
                    {
                        FadeOut = FadeOut,
                        Parent = $"KIT_ITEM_{i}",
                        Name = $"ITEM_{i}",
                        Components =
                    {
                        ComponentAvatar,
                        new CuiRectTransformComponent{ AnchorMin = "0 0", AnchorMax = $"1 1"},
                    }
                    });
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        FadeOut = FadeOut,
                        Parent = $"KIT_ITEM_{i}",
                        Name = $"ITEM_{i}",
                        Components =
                    {
                        new CuiImageComponent { FadeIn = FadeIn, Sprite = Item.Sprite },
                        new CuiRectTransformComponent{ AnchorMin = "0 0", AnchorMax = $"1 1"},
                    }
                    });
                }

                if (Item.Rare != 0)
                {

                    Int32 Rare = IQPlagueSkill ? IS_SKILL_RARE(player) ? Item.Rare + GET_SKILL_RARE_PERCENT() : Item.Rare : Item.Rare;
                    if (Rare >= 100) continue;
                    container.Add(new CuiPanel
                    {
                        FadeOut = FadeOut,
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HEXBackground) }
                    }, $"KIT_ITEM_{i}", $"RARE_BACKGROUND_{i}");

                    container.Add(new CuiLabel
                    {
                        FadeOut = FadeOut,
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Text = { FadeIn = FadeIn, Text = $"{Rare}%", FontSize = 10, Color = HexToRustFormat(Interface.HEXLabels), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
                    }, $"RARE_BACKGROUND_{i}", $"RARE_LABEL_{i}");
                }

                if (Item.RandomDropSettings.UseRandomItems)
                {
                    string amount = $"x{Item.RandomDropSettings.MinAmount}-{Item.RandomDropSettings.MaxAmount}";
                    container.Add(new CuiLabel
                    {
                        FadeOut = FadeOut,
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "0.930693 0.2688163" },
                        Text = { FadeIn = FadeIn, Text = amount, FontSize = amount.Length > 7 ? 8 : amount.Length > 9 ? 7 : 10, Color = HexToRustFormat(Interface.HEXLabels), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleRight }
                    }, $"KIT_ITEM_{i}", $"KIT_ITEM_AMOUNT_{i}");
                }
                else
                {
                    container.Add(new CuiLabel
                    {
                        FadeOut = FadeOut,
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "0.930693 0.2688163" },
                        Text = { FadeIn = FadeIn, Text = $"x{Item.Amount}", FontSize = 10, Color = HexToRustFormat(Interface.HEXLabels), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleRight }
                    }, $"KIT_ITEM_{i}", $"KIT_ITEM_AMOUNT_{i}");
                }

                                i++;
                ItemCount++;
                itemMinPosition += (itemWidth + itemMargin);
                if (ItemCount % ItemTarget == 0)
                {
                    itemMinHeight -= (itemHeight + (itemMargin * 1f));
                    if (itemCount > ItemTarget)
                    {
                        itemMinPosition = 0.5f - ItemTarget / 2f * itemWidth - (ItemTarget - 1) / 2f * itemMargin;
                        itemCount -= ItemTarget;
                    }
                    else
                    {
                        itemMinPosition = 0.5f - itemCount / 2f * itemWidth - (itemCount - 1) / 2f * itemMargin;
                    }
                }
                            }
		   		 		  						  	   		  	  			  	  			  		  		   			
            CuiHelper.AddUi(player, container);
        }

        private enum TypeAutoKit
        {
            Single,
            List,
            PriorityList,
            BiomeList,
        }

        private Int32 GET_SKILL_RARE_PERCENT()
        {
            return (Int32)IQPlagueSkill?.CallHook("API_GET_RARE_IQKITS");
        }
        
                private void KitRemove(BasePlayer player, String NameKit)
        {
            if (!player.IsAdmin) return;

            if (!config.KitList.ContainsKey(NameKit))
            {
                SendChat(player, "Набора с таким ключем не существует!");
                return;
            }

            config.KitList.Remove(NameKit);
            SaveConfig();
            SendChat(player, $"Набора с ключем {NameKit} успешно удален");
        }
		   		 		  						  	   		  	  			  	  			  		  		   			
        private void DestroyAlert(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, $"TITLE_ALERT");
            CuiHelper.DestroyUi(player, $"INFO_ALERT_BACKGROUND");
        }

        public class DataKitsUser
        {
            [JsonProperty("Информация о наборах игрока")]
            public Dictionary<String, InfoKits> InfoKitsList = new Dictionary<String, InfoKits>();
            internal class InfoKits
            {
                public Int32 Amount;
                public Int32 Cooldown;
            }
        }

        private enum TypeKits
        {
            Cooldown,
            Amount,
            Started,
            AmountCooldown,
        }

        private void OnServerInitialized()
        {
            if(ImageLibrary == null)
            {
                NextTick(() => {
                    PrintError("Для корректной работы плагина требуется установить плагин ImageLibrary (https://umod.org/plugins/image-library)");
                    Interface.Oxide.UnloadPlugin(Name);
                });
            }
            RegisteredPermissions();
            LoadedImage();

            foreach (BasePlayer p in BasePlayer.activePlayerList)
                OnPlayerConnected(p);
        }
		   		 		  						  	   		  	  			  	  			  		  		   			
        private void Interface_Loaded_Kits(BasePlayer player)
        {
            RegisteredDataUser(player);
            IEnumerable<KeyValuePair<String, Configuration.Kits>> Kits = GetKits(player);
            if (Kits == null || Kits.Skip(4 * (PagePlayers[player])).Take(4).Count() == 0) return;

            CuiElementContainer container = new CuiElementContainer();
            Configuration.InterfaceSettings Interface = config.InterfaceSetting;
            Single FadeIn = Interface.InterfaceFadeIn;
            Single FadeOut = Interface.InterfaceFadeOut;
            Int32 CountKitPage = Kits.Skip(4 * (PagePlayers[player] + 1)).Take(4).Count();

            Int32 x = 0, y = 0, i = 0;
            foreach (KeyValuePair<String, Configuration.Kits> Kit in Kits.Skip(4 * (PagePlayers[player])).Take(4))
            {
                DataKitsUser.InfoKits Data = DataKitsUserList[player.userID].InfoKitsList[Kit.Key];
                Boolean IsCooldown = Data.Cooldown >= CurrentTime();
		   		 		  						  	   		  	  			  	  			  		  		   			
                container.Add(new CuiPanel
                {
                    FadeOut = FadeOut,
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = $"{0.08385417 + (x * 0.52)} {0.5842593 - (y * 0.342)}", AnchorMax = $"{0.3916667 + (x * 0.52)} {0.8231534 - (y * 0.342)}" },
                    Image = { FadeIn = FadeIn, Color = "0 0 0 0" }
                }, IQKITS_OVERLAY, $"KIT_PANEL_{i}");

                                container.Add(new CuiPanel
                {
                    FadeOut = FadeOut,
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.43654976 1" }, 
                    Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HEXBlock) }
                }, $"KIT_PANEL_{i}", $"AVATAR_PANEL_{i}");

                if (String.IsNullOrWhiteSpace(Kit.Value.Sprite))
                {
                    CuiRawImageComponent ComponentAvatar = !String.IsNullOrWhiteSpace(Kit.Value.PNG) ? new CuiRawImageComponent { FadeIn = FadeIn, Png = GetImage($"KIT_{Kit.Value.PNG}") } : new CuiRawImageComponent { FadeIn = FadeIn, Png = GetImage(Kit.Value.Shortname) };
                    container.Add(new CuiElement
                    {
                        FadeOut = FadeOut,
                        Parent = $"AVATAR_PANEL_{i}",
                        Name = $"AVATAR_{i}",
                        Components =
                    {
                        ComponentAvatar,
                        new CuiRectTransformComponent{ AnchorMin = "0.0775194 0.07364181", AnchorMax = $"0.9224806 0.9185845"},
                    }
                    });
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        FadeOut = FadeOut,
                        Parent = $"AVATAR_PANEL_{i}",
                        Name = $"AVATAR_{i}",
                        Components =
                    {
                        new CuiImageComponent { FadeIn = FadeIn, Sprite = Kit.Value.Sprite },
                        new CuiRectTransformComponent{ AnchorMin = "0.0775194 0.07364181", AnchorMax = $"0.9224806 0.9185845"},
                    }
                    });
                }
                
                
                container.Add(new CuiPanel
                {
                    FadeOut = FadeOut,
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = $"0.4602368 0.6472726", AnchorMax = $"1 1" },
                    Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HEXBlock) }
                }, $"KIT_PANEL_{i}", $"DISPLAY_NAME_PANEL_{i}");

                container.Add(new CuiLabel
                {
                    FadeOut = FadeOut,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0.965517 0.9449963" },
                    Text = { FadeIn = FadeIn, Text = GetLang("UI_DISPLAY_NAME_KIT", player.UserIDString, Kit.Value.DisplayName.ToUpper()), Color = HexToRustFormat(Interface.HEXLabels), Font = "robotocondensed-bold.ttf", Align = TextAnchor.UpperRight }

                }, $"DISPLAY_NAME_PANEL_{i}", $"TITLE_KIT_{i}");
                
                                container.Add(new CuiPanel
                {
                    FadeOut = FadeOut,
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = $"0.4602368 0.2519316", AnchorMax = $"1 0.6046609" },
                    Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HEXBlock) }
                }, $"KIT_PANEL_{i}", $"COOLDOWN_PANEL_{i}");

                Double XMax = IsCooldown ? (Double)((Data.Cooldown - CurrentTime()) * Math.Pow(Kit.Value.CoolDown, -1)) : Data.Amount != 0 ? (Double)((Data.Amount) * Math.Pow(Kit.Value.Amount, -1)) : 1;
                container.Add(new CuiPanel
                {
                    FadeOut = FadeOut,
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"{XMax} 1", OffsetMin = "1 1", OffsetMax = "-2 -1" },
                    Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HEXCooldowns) }
                }, $"COOLDOWN_PANEL_{i}", $"COOLDOWN_LINE_{i}");

                String InfoAmountAndCooldown = IsCooldown ? GetLang("UI_COOLDONW_KIT", player.UserIDString, FormatTime(TimeSpan.FromSeconds(Data.Cooldown - CurrentTime()))) : Data.Amount != 0 ? GetLang("UI_AMOUNT_KIT", player.UserIDString, Data.Amount) : GetLang("UI_COOLDONW_KIT_NO", player.UserIDString);
                container.Add(new CuiLabel
                {
                    FadeOut = FadeOut,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0.965517 0.9449963" },
                    Text = { FadeIn = FadeIn, Text = InfoAmountAndCooldown, Color = HexToRustFormat(Interface.HEXLabels), Font = "robotocondensed-bold.ttf", Align = TextAnchor.UpperRight }
                }, $"COOLDOWN_PANEL_{i}", $"COOLDOWN_TITLE{i}");
                
                
                container.Add(new CuiButton
                {
                    FadeOut = FadeOut - 0.2f,
                    RectTransform = { AnchorMin = "0.465313 0", AnchorMax = "0.72584976 0.21704976" }, 
                    Button = { FadeIn = FadeIn, Command = $"kit_ui_func information {Kit.Key}", Color = HexToRustFormat(Interface.HEXInfoItemButton), },
                    Text = { FadeIn = FadeIn, Text = GetLang("UI_BTN_WHAT_INFO", player.UserIDString), Color = HexToRustFormat(Interface.HEXLabelsInfoItemButton), Align = TextAnchor.MiddleCenter }
                }, $"KIT_PANEL_{i}", $"WHAT_INFO_{i}");

                String KeyLangTake = IsCooldown ? GetLang("UI_BTN_TAKE_KIT_BLOCK", player.UserIDString) : Data.Amount != 0 ? GetLang("UI_BTN_TAKE_KIT", player.UserIDString, Data.Amount) : GetLang("UI_BTN_TAKE_KIT", player.UserIDString);
                String HexButtonTake = IsCooldown ? Interface.HEXInfoItemButton : Data.Amount != 0 ? Interface.HEXAccesButton : Interface.HEXAccesButton;
                String HexButtonLabelTake = IsCooldown ? Interface.HEXLabelsInfoItemButton : Data.Amount != 0 ? Interface.HEXLabelsAccesButton : Interface.HEXLabelsAccesButton;
                String CommandButtonTake = IsCooldown ? "" : Data.Amount != 0 ? $"kit_ui_func take.kit {Kit.Key}" : $"kit_ui_func take.kit {Kit.Key}";
                container.Add(new CuiButton
                {
                    FadeOut = FadeOut - 0.2f,
                    RectTransform = { AnchorMin = "0.7394261 0", AnchorMax = "1 0.2170733" },
                    Button = { FadeIn = FadeIn, Command = CommandButtonTake, Color = HexToRustFormat(HexButtonTake) },
                    Text = { FadeIn = FadeIn, Text = KeyLangTake, Color = HexToRustFormat(HexButtonLabelTake), Align = TextAnchor.MiddleCenter }
                }, $"KIT_PANEL_{i}", $"TAKE_KIT_{i}");

                
                x++;
                if (x >= 2)
                {
                    x = 0;
                    y++;
                }
                i++;
                if (x == 2 && y == 1) break;
            }

            if (PagePlayers[player] != 0)
            {
                container.Add(new CuiButton
                {
                    FadeOut = FadeOut - 0.2f,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0.1015625 0.05462963" },
                    Button = { FadeIn = FadeIn, Command = "kit_ui_func back.page", Color = "0 0 0 0" },
                    Text = { FadeIn = FadeIn, Text = GetLang("UI_BACK_BTN", player.UserIDString), Color = HexToRustFormat(Interface.HEXLabels), Align = TextAnchor.MiddleCenter }
                }, IQKITS_OVERLAY, $"BTN_BACK_BUTTON");
            }
            if (CountKitPage != 0)
            {
                container.Add(new CuiButton
                {
                    FadeOut = FadeOut - 0.2f,
                    RectTransform = { AnchorMin = "0.89895 0", AnchorMax = "1 0.05462963" },
                    Button = { FadeIn = FadeIn, Command = $"kit_ui_func next.page", Color = "0 0 0 0" },
                    Text = { FadeIn = FadeIn, Text = GetLang("UI_NEXT_BTN", player.UserIDString), Color = HexToRustFormat(Interface.HEXLabels), Align = TextAnchor.MiddleCenter }
                }, IQKITS_OVERLAY, $"BTN_NEXT_BUTTON");
            }

            CuiHelper.AddUi(player, container);
        }
		   		 		  						  	   		  	  			  	  			  		  		   			
        private Int32 GET_SKILL_COOLDOWN_PERCENT()
        {
            return (Int32)IQPlagueSkill?.CallHook("API_GET_COOLDOWN_IQKITS");
        }
        
                public Boolean IsRaidBlocked(BasePlayer player, Boolean Skipped)
        {
            if (Skipped) return false;

            String ret = Interface.Call("CanTeleport", player) as String;
            if (ret != null)
                return true;
            else return false;
        }
		   		 		  						  	   		  	  			  	  			  		  		   			
        private Int32 API_KIT_PLAYER_GET_AMOUNT(BasePlayer player, String KitKey)
        {
            if (player == null) return 0;
            if (String.IsNullOrWhiteSpace(KitKey)) return 0;
            if (!DataKitsUserList.ContainsKey(player.userID))
            {
                PrintError($"API_KIT_PLAYER_GET_AMOUNT : Такого игрока не существует в дата-файле");
                return 0;
            }
            if (!config.KitList.ContainsKey(KitKey))
            {
                PrintError($"API_KIT_PLAYER_GET_AMOUNT : Ключа {KitKey} не существует");
                return 0;
            }
            if (!DataKitsUserList[player.userID].InfoKitsList.ContainsKey(KitKey))
            {
                PrintError($"API_KIT_PLAYER_GET_AMOUNT : У игрока нет данного набора {KitKey}");
                return 0;
            }
            return DataKitsUserList[player.userID].InfoKitsList[KitKey].Amount;
        }

        public static StringBuilder sb = new StringBuilder();

        public static String IQKITS_OVERLAY = "IQKITS_OVERLAY";
        
        
                private void CreateNewKit(BasePlayer player, String NameKit)
        {
            if (!player.IsAdmin) return;
		   		 		  						  	   		  	  			  	  			  		  		   			
            if (config.KitList.ContainsKey(NameKit))
            {
                SendChat(player, "Ключ данного набора уже существует!");
                return;
            }

            config.KitList.Add(NameKit, new Configuration.Kits
            {
                Amount = 0,
                CoolDown = 300,
                DisplayName = NameKit,
                Permission = "iqkits.setting",
                PNG = "",
                Shortname = "",
                UseRaidBlock = false,
                RankUser = "",
                Sprite = "assets/icons/gear.png",
                TypeKit = TypeKits.Cooldown,
                ItemKits = GetPlayerItems(player),
                WipeOpened = 0,
            });

            SaveConfig();
            SendChat(player, $"Набор с ключем {NameKit} успешно создан");
        }
        /// <summary>
        /// Обновление 1.3.10
        /// Нововведения :
        /// - Добавлена возможность брать наборы через команду (поддерживает консольные (F1) и чат (/) команды), с возможностью бинда
        /// - Добавлена чат-команда для просмотра всех доступных наборов - /kitlist
        /// Исправления :
        /// - Исправлено отображения диапазона выпадаемых предметов в наборе
        /// /// </summary>

                [PluginReference] private readonly Plugin ImageLibrary, IQPlagueSkill, IQChat, IQRankSystem;
        private IEnumerator DownloadImages()
        {
            Puts("AddImages SkyPlugins.ru...");
            foreach (KeyValuePair<String, Configuration.Kits> Kit in config.KitList)
            {
                foreach (Configuration.Kits.ItemsKit img in Kit.Value.ItemKits.Where(i => !String.IsNullOrWhiteSpace(i.Shortname)))
                {
                    if (!HasImage($"{img.Shortname}_128px"))
                        AddImage($"https://api.skyplugins.ru/api/getimage/{img.Shortname}/128", $"{img.Shortname}_128px");
                }
            }

            yield return new WaitForSeconds(0.04f);
            Puts("AddImages SkyPlugins.ru - completed..");
        }

        private void DestroyKits(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, $"BTN_BACK_BUTTON");
            CuiHelper.DestroyUi(player, $"BTN_NEXT_BUTTON");

            for (Int32 i = 0; i < 4; i++)
            {
                CuiHelper.DestroyUi(player, $"WHAT_INFO_{i}");
                CuiHelper.DestroyUi(player, $"TAKE_KIT_{i}");
                CuiHelper.DestroyUi(player, $"COOLDOWN_LINE_{i}");
                CuiHelper.DestroyUi(player, $"COOLDOWN_TITLE{i}");
                CuiHelper.DestroyUi(player, $"COOLDOWN_PANEL_{i}");
                CuiHelper.DestroyUi(player, $"TITLE_KIT_{i}");
                CuiHelper.DestroyUi(player, $"DISPLAY_NAME_PANEL_{i}");
                CuiHelper.DestroyUi(player, $"AVATAR_{i}");
                CuiHelper.DestroyUi(player, $"AVATAR_PANEL_{i}");
                CuiHelper.DestroyUi(player, $"KIT_PANEL_{i}");
            }
        }
        
        [ConsoleCommand("kit")]
        private void IQKITS_ConsoleCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (player != null)
            {
                if (player.HasFlag(BaseEntity.Flags.Reserved3))
                    return;
                
                Boolean result;
                if (arg.Args == null || String.IsNullOrWhiteSpace(arg.Args[0]) ||
                    Boolean.TryParse(arg.Args[0], out result))
                {
                    PagePlayers[player] = 0;
                    Interface_IQ_Kits(player);
                    return;
                }
                
                if (arg.Args.Length <= 2 && !NoTriggerCommandRepository.Contains(arg.Args[0]))
                {
                    String KitKey = arg.Args[0];
                    if (String.IsNullOrWhiteSpace(KitKey))
                    {
                        SendChat(player, GetLang("CHAT_ALERT_PLAYER_KIT_COMMAND_EMPTY_KEY", player.UserIDString));
                        return;
                    }

                    if (!DataKitsUserList[player.userID].InfoKitsList.ContainsKey(KitKey))
                    {
                        String KistList = String.Empty;

                        foreach (KeyValuePair<String, DataKitsUser.InfoKits> keyValuePair in DataKitsUserList[player.userID].InfoKitsList.OrderBy(x => x.Value.Cooldown >= CurrentTime()).Where(k => config.KitList.ContainsKey(k.Key) && ((config.KitList[k.Key].TypeKit != TypeKits.Started && WipeTime >= config.KitList[k.Key].WipeOpened) && (((config.KitList[k.Key].TypeKit == TypeKits.Amount || config.KitList[k.Key].TypeKit == TypeKits.AmountCooldown) && (DataKitsUserList[player.userID].InfoKitsList.ContainsKey(k.Key) && DataKitsUserList[player.userID].InfoKitsList[k.Key].Amount > 0)) || config.KitList[k.Key].TypeKit == TypeKits.Cooldown))))
                        {
                            Boolean IsCooldown = keyValuePair.Value.Cooldown >= CurrentTime();
                            String InfoAmountAndCooldown = 
                                IsCooldown ? GetLang("CHAT_ALERT_PLAYER_INFO_KIT_LIST_KIT_FORMAT_COOLDOWN", player.UserIDString, keyValuePair.Key, FormatTime(TimeSpan.FromSeconds(keyValuePair.Value.Cooldown - CurrentTime())))
                                : keyValuePair.Value.Amount != 0 ? GetLang("CHAT_ALERT_PLAYER_INFO_KIT_LIST_KIT_FORMAT_AMOUNT", player.UserIDString, keyValuePair.Key, keyValuePair.Value.Amount)
                                : GetLang("CHAT_ALERT_PLAYER_INFO_KIT_LIST_KIT_FORMAT_YES", player.UserIDString, keyValuePair.Key);

                            KistList += InfoAmountAndCooldown;
                        }

                        SendChat(player, GetLang("CHAT_ALERT_PLAYER_INFO_KIT_LIST", player.UserIDString, KistList));
                        return;
                    }

                    TakeKit(player, KitKey, true);
                    return;
                }
            }

            if (player != null && !player.IsAdmin) return;
            
            switch (arg.Args[0])
            {
                case "create":
                case "createkit":
                case "add":
                case "new":
                    {
                        if (player == null || !player.IsAdmin) return;
                        String NameKit = arg.Args[1];
                        if (String.IsNullOrWhiteSpace(NameKit))
                        {
                            PrintToConsole(player, "Введите корректное название!");
                            return;
                        }
                        CreateNewKit(player, NameKit);
                        break;
                    }
                case "remove":
                case "delete":
                case "revoke":
                    {
                        if (player == null || !player.IsAdmin) return;
                        String NameKit = arg.Args[1];
                        if (String.IsNullOrWhiteSpace(NameKit))
                        {
                            PrintToConsole(player, "Введите корректное название!");
                            return;
                        }
                        KitRemove(player, NameKit);
                        break;
                    }
                case "give":
                    {
                        if (player != null && !player.IsAdmin) return;

                        String IDarName = arg.Args[1];
                        if (String.IsNullOrWhiteSpace(IDarName))
                        {
                            if (player != null)
                                PrintToConsole(player, "Введите корректное имя или ID");
                            PrintError("Введите корректное имя или ID");
                            return;
                        }
                        BasePlayer TargetUser = BasePlayer.Find(IDarName);
                        if (TargetUser == null)
                        {
                            if (player != null)
                                PrintToConsole(player, "Такого игрока нет на сервере");
                            PrintError("Такого игрока нет на сервере");
                            return;
                        }
                        String KitKey = arg.Args[2];
                        if (String.IsNullOrWhiteSpace(KitKey))
                        {
                            if (player != null)
                                PrintToConsole(player, "Введите корректный ключ набора");
                            PrintError("Введите корректный ключ набора");
                            return;
                        }
                        if (!config.KitList.ContainsKey(KitKey))
                        {
                            if (player != null)
                                PrintToConsole(player, "Набора с данным ключем не существует");
                            PrintError("Набора с данным ключем не существует");
                            return;
                        }
                        ParseAndGive(TargetUser, KitKey);
                        if (player != null)
                            PrintToConsole(player, "Успешно выдан набор");
                        break;
                    }
            }
        }

        protected override void LoadDefaultConfig()
        {
            config = Configuration.GetNewConfiguration();
        }
        
                private static Configuration config = new Configuration();
        [ConsoleCommand("kit_ui_func")]
        private void IQKITS_UI_Func(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
            {
                PrintWarning("Это консольная команда должна отыгрываться от игрока!");
                return;
            }
            String Key = arg.Args[0];
            switch (Key)
            {
                case "information":
                    {
                        String KitKey = arg.Args[1];
                        Interface_Info_Kits(player, KitKey);
                        break;
                    }
                case "take.kit":
                    {
                        String KitKey = arg.Args[1];
                        TakeKit(player, KitKey);
                        break;
                    }
                case "close.ui":
                    {
                        DestroyAll(player);
                        break;
                    }
                case "hide.info":
                    {
                        DestroyInfoKits(player);
                        break;
                    }
                case "next.page":
                    {
                        DestroyKits(player);
                        PagePlayers[player]++;
                        Interface_Loaded_Kits(player);
                        break;
                    }
                case "back.page":
                    {
                        DestroyKits(player);
                        PagePlayers[player]--;
                        Interface_Loaded_Kits(player);
                        break;
                    }
            }

        }
        private static String Format(Int32 units, String form1, String form2, String form3)
        {
            Int32 tmp = units % 10;

            if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9)
                return $"{units} {form1}";

            if (tmp >= 2 && tmp <= 4)
                return $"{units} {form2}";
		   		 		  						  	   		  	  			  	  			  		  		   			
            return $"{units} {form3}";
        }
        private void ClearData()
        {
            if (!config.GeneralSetting.AutoWipeClearKits) return;
            DataKitsUserList.Clear();
            WriteData();
        }

        private String API_KIT_GET_ITEMS(String KitKey)
        {
            Configuration.Kits Kit = config.KitList[KitKey];
            if (Kit == null) return String.Empty;
            List<ShortInfoKit> ShortKitList = new List<ShortInfoKit>();
		   		 		  						  	   		  	  			  	  			  		  		   			
            foreach (Configuration.Kits.ItemsKit KitItems in Kit.ItemKits.Where(x => !String.IsNullOrWhiteSpace(x.Shortname)))
                ShortKitList.Add(new ShortInfoKit { Shortname = KitItems.Shortname, Amount = KitItems.Amount, SkinID = KitItems.SkinID });

            return JsonConvert.SerializeObject(ShortKitList);
        }
        
        
        public Boolean IsRareDrop(Int32 Rare)
        {
            return UnityEngine.Random.Range(0, 100) >= (100 - (Rare > 100 ? 100 : Rare));
        }

        private BiomeType GetBiome(BasePlayer player)
        {
            if (TerrainMeta.BiomeMap.GetBiome(player.transform.position, 1) > 0.5) return BiomeType.Arid;
            if (TerrainMeta.BiomeMap.GetBiome(player.transform.position, 2) > 0.5) return BiomeType.Temperate;
            if (TerrainMeta.BiomeMap.GetBiome(player.transform.position, 4) > 0.5) return BiomeType.Tundra;
            if (TerrainMeta.BiomeMap.GetBiome(player.transform.position, 8) > 0.5) return BiomeType.Arctic;
            return BiomeType.None;
        }
        
                private void AutoKitGive(BasePlayer player)
        {
            if (player == null) return;
            Configuration.GeneralSettings.AutoKit AutoKit = config.GeneralSetting.AutoKitSettings;
            Dictionary<String, Configuration.Kits> KitList = config.KitList;

            switch (AutoKit.TypeAuto)
            {
                case TypeAutoKit.Single:
                    {
                        if (String.IsNullOrWhiteSpace(AutoKit.StartKitKey) || !KitList.ContainsKey(AutoKit.StartKitKey))
                        {
                            PrintWarning("У вас не верно указан стартовый ключ, такого набора не существует! Игрок не получил его автоматически");
                            return;
                        }
                        ParseAndGive(player, AutoKit.StartKitKey);
                        break;
                    }
                case TypeAutoKit.List:
                    {
                        Configuration.GeneralSettings.AutoKit.KitSettings RandomKit = AutoKit.KitListRandom.Where(k => permission.UserHasPermission(player.UserIDString, k.Permissions) && KitList.ContainsKey(k.StartKitKey) && WipeTime >= KitList[k.StartKitKey].WipeOpened).ToList().GetRandom();
                        if (RandomKit == null) return;
                        ParseAndGive(player, RandomKit.StartKitKey);
                        break;
                    }
                case TypeAutoKit.PriorityList:
                    {
                        Configuration.GeneralSettings.AutoKit.KitSettings Kit = AutoKit.KitListPriority.FirstOrDefault(k => permission.UserHasPermission(player.UserIDString, k.Permissions) && KitList.ContainsKey(k.StartKitKey) && WipeTime >= KitList[k.StartKitKey].WipeOpened);
                        if (Kit == null) return;
                        ParseAndGive(player, Kit.StartKitKey);
                        break;
                    }
                case TypeAutoKit.BiomeList:
                    {
                        Configuration.GeneralSettings.AutoKit.BiomeKits BiomeKit = AutoKit.BiomeStartedKitList.FirstOrDefault(k => GetBiome(player) == k.biomeType && permission.UserHasPermission(player.UserIDString, k.Kits.Permissions) && KitList.ContainsKey(k.Kits.StartKitKey) && WipeTime >= KitList[k.Kits.StartKitKey].WipeOpened);
                        if (BiomeKit == null) return;
                        ParseAndGive(player, BiomeKit.Kits.StartKitKey);
                        break;
                    }
            }
        }

        private List<Configuration.Kits.ItemsKit.ItemContents> GetContentItem(Item Item)
        {
            List<Configuration.Kits.ItemsKit.ItemContents> Contents = new List<Configuration.Kits.ItemsKit.ItemContents>();

            if (Item.contents != null)
            {
                foreach (Item Content in Item.contents.itemList)
                {
                    Configuration.Kits.ItemsKit.ItemContents ContentItem = new Configuration.Kits.ItemsKit.ItemContents
                    {
                        ContentType = TypeContent.Contents,
                        Shortname = Content.info.shortname,
                        Amount = Content.amount,
                        Condition = Content.condition
                    };
                    Contents.Add(ContentItem);
                }
            }

            BaseProjectile Weapon = Item.GetHeldEntity() as BaseProjectile;
            if (Weapon != null)
            {
                Configuration.Kits.ItemsKit.ItemContents ContentItem = new Configuration.Kits.ItemsKit.ItemContents
                {
                    ContentType = TypeContent.Ammo,
                    Shortname = Weapon.primaryMagazine.ammoType.shortname,
                    Amount = Weapon.primaryMagazine.contents == 0 ? 1 : Weapon.primaryMagazine.contents,
                    Condition = Weapon.primaryMagazine.ammoType.condition.max
                };
                Contents.Add(ContentItem);
            }

            return Contents;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Ошибка чтения #57 конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }
        
        private List<String> NoTriggerCommandRepository = new List<String>()
        {
            "create",
            "createkit",
            "add",
            "new",
            "remove",
            "delete",
            "revoke",
            "give",
        };

        private void WriteData() => Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQSystem/IQKits/KitsData", DataKitsUserList);
        
                private void OnNewSave(String filename)
        {
            ClearData();
        }
        
        private static String HexToRustFormat(String hex)
        {
            Color color;
            ColorUtility.TryParseHtmlString(hex, out color);
            sb.Clear();
            return sb.AppendFormat("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a).ToString();
        }
        private class Configuration
        {

            internal class GeneralSettings
            {
                [JsonProperty("Настройки автоматических китов")]
                public AutoKit AutoKitSettings = new AutoKit();
                [JsonProperty("Автоматическая очистка наборов у игроков после вайпа (true - включено/false - выключено)")]
                public Boolean AutoWipeClearKits;
                internal class AutoKit
                {
                    [JsonProperty("Тип автокитов : 0 - Единый ключ, 1 - Случаный список, 2 - Приоритетный список, 3 - Биомный список")]
                    public TypeAutoKit TypeAuto;
                    [JsonProperty("Ключ набора (Тип 0 - Единый ключ)")]
                    public String StartKitKey;
                    [JsonProperty("Список ключей набора (Тип 1 - Случайный список). Дается один из случайных автокитов доступных игроку")]
                    public List<KitSettings> KitListRandom = new List<KitSettings>();
                    [JsonProperty("Список ключей набора (Тип 2 - Приоритетный список). Дается доступный набор игроку, который выше других")]
                    public List<KitSettings> KitListPriority = new List<KitSettings>();
                    [JsonProperty("Список ключей наборов по биомам (Тип 3 - Биомный список). Дается доступный набор игроку, в зависимости от биома")]
                    public List<BiomeKits> BiomeStartedKitList = new List<BiomeKits>();

                    internal class BiomeKits
                    {
                        [JsonProperty("Настройка набора")]
                        public KitSettings Kits = new KitSettings();
                        [JsonProperty("Номер биома в котором будет даваться набор ( 1 - Arid, 2 - Temperate, 3 - Tundra, 4 - Arctic )")]
                        public BiomeType biomeType;
                    }

                    internal class KitSettings
                    {
                        [JsonProperty("Ключ набора")]
                        public String StartKitKey;
                        [JsonProperty("Права для набора(не оставляйте это поле пустым)")]
                        public String Permissions;
                    }
                }
            }
            [JsonProperty("Настройки интерфейса")]
            public InterfaceSettings InterfaceSetting = new InterfaceSettings();
            [JsonProperty("Настройки плагинов совместимости")]
            public ReferenceSettings ReferenceSetting = new ReferenceSettings();
            internal class InterfaceSettings
            {
                [JsonProperty("Использовать кнопку ЗАКРЫТЬ в UI (true - да/false - нет). Если установлено false - ui будет закрываться при нажатии в любом месте")]
                public Boolean CloseType;
                [JsonProperty("Закрывать интерфейс после выбора набора")]
                public Boolean CloseUiTakeKit;
                [JsonProperty("HEX: Цвет заднего фона")]
                public String HEXBackground;
                [JsonProperty("HEX: Цвет текста")]
                public String HEXLabels;
                [JsonProperty("HEX: Кнопки с информацией")]
                public String HEXInfoItemButton;
                [JsonProperty("HEX: Цвет текста на кнопке с информацией")]
                public String HEXLabelsInfoItemButton;
                [JsonProperty("HEX: Цвет кнопки забрать")]
                public String HEXAccesButton;
                [JsonProperty("HEX: Цвет текста на кнопке забрать")]
                public String HEXLabelsAccesButton;
                [JsonProperty("HEX: Цвет полосы перезарядки")]
                public String HEXCooldowns;
                [JsonProperty("HEX: Цвет блоков с информацией")]
                public String HEXBlock;
                [JsonProperty("HEX: Цвет блоков на которых будут лежать предметы")]
                public String HEXBlockItemInfo;
                [JsonProperty("Время появления интерфейса(его плавность)")]
                public Single InterfaceFadeOut;
                [JsonProperty("Время исчезновения интерфейса(его плавность)")]
                public Single InterfaceFadeIn;
                [JsonProperty("PNG заднего фона с информацией о том,что находится в наборе")]
                public String PNGInfoPanel;
                [JsonProperty("PNG заднего фона уведомления")]
                public String PNGAlert;
            }
            [JsonProperty("Настройка наборов")]
            public Dictionary<String, Kits> KitList = new Dictionary<String, Kits>();
            internal class ReferenceSettings
            {
                internal class IQChatSettings
                {
                    [JsonProperty("IQChat : Кастомный аватар в чате(Если требуется)")]
                    public String CustomAvatar;
                    [JsonProperty("IQChat : Кастомный префикс в чате")]
                    public String CustomPrefix;
                }
                [JsonProperty("Настройки IQChat")]
                public IQChatSettings IQChatSetting = new IQChatSettings();
            }
            [JsonProperty("Общие настройки")]
            public GeneralSettings GeneralSetting = new GeneralSettings();
            internal class Kits
            {
                [JsonProperty("Тип набора(0 - С перезарядкой, 1 - Лимитированый, 2 - Стартовый(АвтоКит), 3 - Лимитированый с перезарядкой)")]
                public TypeKits TypeKit;
                [JsonProperty("Отображаемое имя")]
                public String DisplayName;
                [JsonProperty("Через сколько дней вайпа будет доступен набор")]
                public Int32 WipeOpened;
                [JsonProperty("Разрешить использовать этот набор во время рейдблока (true - да/false - нет)")]
                public Boolean UseRaidBlock = true;
                [JsonProperty("IQRankSystem : Разрешить использовать этот набор только по рангу (Впишите ключ с рангом). Если вам это не нужно - оставьте поле пустым")]
                public String RankUser = "";
                [JsonProperty("Права")]
                public String Permission;
                [JsonProperty("PNG(128x128)")]
                public String PNG;
                [JsonProperty("Sprite(Установится если отсутствует PNG)")]
                public String Sprite;
                [JsonProperty("Shortname(Установится если отсутствует PNG и Sprite)")]
                public String Shortname;
                [JsonProperty("Время перезарядки набора")]
                public Int32 CoolDown;
                [JsonProperty("Количество сколько наборов можно взять")]
                public Int32 Amount;
                [JsonProperty("Предметы , которые будут даваться в данном наборе")]
                public List<ItemsKit> ItemKits = new List<ItemsKit>();

                internal class ItemsKit
                {
                    [JsonProperty("Выберите контейнер в который будет перенесен предмет(0 - Одежда, 1 - Панель быстрого доступа, 2 - Рюкзак)")]
                    public ContainerItem ContainerItemType;
                    [JsonProperty("Название предмета")]
                    public String DisplayName;
                    [JsonProperty("Shortname предмета")]
                    public String Shortname;
                    [JsonProperty("Количество(Если это команда,так-же указывайте число)")]
                    public Int32 Amount;
                    [JsonProperty("Настройки случайного количества выпадения предмета")]
                    public RandomingDrop RandomDropSettings = new RandomingDrop();
                    [JsonProperty("Шанс на выпадения предмета(Оставьте 0 - если не нужен шанс)")]
                    public Int32 Rare;
                    [JsonProperty("SkinID предмета")]
                    public UInt64 SkinID;
                    [JsonProperty("PNG предмета(если установлена команда)")]
                    public String PNG;
                    [JsonProperty("Sprite(Если установлена команда и не установлен PNG)")]
                    public String Sprite;
                    [JsonProperty("Команда(%STEAMID% заменится на ID пользователя)")]
                    public String Command;
                    [JsonProperty("Содержимое внутри предмета (Пример : Вода в бутылке) не корректируйте эти значения, если не знаете для чего они. Используйте встроенные команды")]
                    public List<ItemContents> ContentsItem = new List<ItemContents>();
                    internal class ItemContents
                    {
                        [JsonProperty("Тип : 0 - Патроны | 1 - Контент")]
                        public TypeContent ContentType;
                        [JsonProperty("Shortname предмета")]
                        public String Shortname = "";
                        [JsonProperty("Количество предметов")]
                        public Int32 Amount = 0;
                        [JsonProperty("Целостность предмета")]
                        public Single Condition = 0;
                    }

                    internal class RandomingDrop
                    {
                        [JsonProperty("Использовать случайное выпадение предмета(Действует только на предметы)")]
                        public Boolean UseRandomItems;
                        [JsonProperty("Минимальное количество")]
                        public Int32 MinAmount;
                        [JsonProperty("Максимальное количество")]
                        public Int32 MaxAmount;
                    }
                }
            }

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    ReferenceSetting = new ReferenceSettings
                    {
                        IQChatSetting = new ReferenceSettings.IQChatSettings
                        {
                            CustomAvatar = "",
                            CustomPrefix = "",
                        },
                    },
                    KitList = new Dictionary<String, Kits>
                    {
                                                ["start1"] = new Kits
                        {
                            TypeKit = TypeKits.Started,
                            Amount = 0,
                            CoolDown = 0,
                            WipeOpened = 0,
                            DisplayName = "Новичок",
                            Permission = "iqkits.start1",
                            RankUser = "",
                            UseRaidBlock = false,
                            PNG = "",
                            Shortname = "",
                            Sprite = "",
                            ItemKits = new List<Kits.ItemsKit>
                            {
                                new Kits.ItemsKit
                                {
                                    ContainerItemType = ContainerItem.containerMain,
                                    RandomDropSettings = new Kits.ItemsKit.RandomingDrop
                                    {
                                        UseRandomItems = false,
                                        MinAmount = 1,
                                        MaxAmount = 1
                                    },
                                    DisplayName = "Ak103",
                                    Amount = 1,
                                    Command = "",
                                    PNG = "",
                                    Shortname = "rifle.ak",
                                    Rare = 0,
                                    Sprite = "",
                                    SkinID = 0,
                                },
                                new Kits.ItemsKit
                                {
                                    ContainerItemType = ContainerItem.containerMain,
                                    RandomDropSettings = new Kits.ItemsKit.RandomingDrop
                                    {
                                        UseRandomItems = false,
                                        MinAmount = 1,
                                        MaxAmount = 1
                                    },
                                    DisplayName = "Ak104",
                                    Amount = 1,
                                    Command = "",
                                    PNG = "",
                                    Shortname = "rifle.ak",
                                    Rare = 0,
                                    Sprite = "",
                                    SkinID = 0,
                                },
                            },
                        },
                        ["start2"] = new Kits
                        {
                            TypeKit = TypeKits.Started,
                            Amount = 0,
                            CoolDown = 0,
                            WipeOpened = 0,
                            DisplayName = "Новичок #2",
                            Permission = "iqkits.start2",
                            RankUser = "",
                            UseRaidBlock = false,
                            PNG = "",
                            Shortname = "",
                            Sprite = "",
                            ItemKits = new List<Kits.ItemsKit>
                            {
                                new Kits.ItemsKit
                                {
                                    ContainerItemType = ContainerItem.containerMain,
                                    RandomDropSettings = new Kits.ItemsKit.RandomingDrop
                                    {
                                        UseRandomItems = false,
                                        MinAmount = 1,
                                        MaxAmount = 1
                                    },
                                    DisplayName = "Ak103",
                                    Amount = 1,
                                    Command = "",
                                    PNG = "",
                                    Shortname = "rifle.ak",
                                    Rare = 0,
                                    Sprite = "",
                                    SkinID = 0,
                                },
                                new Kits.ItemsKit
                                {
                                    ContainerItemType = ContainerItem.containerMain,
                                    RandomDropSettings = new Kits.ItemsKit.RandomingDrop
                                    {
                                        UseRandomItems = false,
                                        MinAmount = 1,
                                        MaxAmount = 1
                                    },
                                    DisplayName = "Ak104",
                                    Amount = 1,
                                    Command = "",
                                    PNG = "",
                                    Shortname = "rifle.ak",
                                    Rare = 0,
                                    Sprite = "",
                                    SkinID = 0,
                                },
                            },
                        },
                        ["start3Premium"] = new Kits
                        {
                            TypeKit = TypeKits.Started,
                            Amount = 0,
                            CoolDown = 0,
                            WipeOpened = 0,
                            DisplayName = "Премиум новичок",
                            Permission = "iqkits.premium",
                            RankUser = "",
                            UseRaidBlock = false,
                            PNG = "",
                            Shortname = "",
                            Sprite = "",
                            ItemKits = new List<Kits.ItemsKit>
                            {
                                new Kits.ItemsKit
                                {
                                    ContainerItemType = ContainerItem.containerMain,
                                    RandomDropSettings = new Kits.ItemsKit.RandomingDrop
                                    {
                                        UseRandomItems = false,
                                        MinAmount = 1,
                                        MaxAmount = 1
                                    },
                                    DisplayName = "Ak103",
                                    Amount = 1,
                                    Command = "",
                                    PNG = "",
                                    Shortname = "rifle.ak",
                                    Rare = 0,
                                    Sprite = "",
                                    SkinID = 0,
                                },
                                new Kits.ItemsKit
                                {
                                    ContainerItemType = ContainerItem.containerMain,
                                    RandomDropSettings = new Kits.ItemsKit.RandomingDrop
                                    {
                                        UseRandomItems = false,
                                        MinAmount = 1,
                                        MaxAmount = 1
                                    },
                                    DisplayName = "Ak104",
                                    Amount = 1,
                                    Command = "",
                                    PNG = "",
                                    Shortname = "rifle.ak",
                                    Rare = 0,
                                    Sprite = "",
                                    SkinID = 0,
                                },
                            },
                        },
                        
                                                ["hunter"] = new Kits
                        {
                            TypeKit = TypeKits.Cooldown,
                            Amount = 0,
                            CoolDown = 300,
                            WipeOpened = 2,
                            DisplayName = "Охотник",
                            Permission = "iqkits.default",
                            RankUser = "",
                            UseRaidBlock = false,
                            PNG = "",
                            Shortname = "rifle.ak",
                            Sprite = "",
                            ItemKits = new List<Kits.ItemsKit>
                            {
                                new Kits.ItemsKit
                                {
                                    ContainerItemType = ContainerItem.containerMain,
                                    RandomDropSettings = new Kits.ItemsKit.RandomingDrop
                                    {
                                        UseRandomItems = false,
                                        MinAmount = 1,
                                        MaxAmount = 1
                                    },
                                    DisplayName = "Ak103",
                                    Amount = 1,
                                    Command = "",
                                    PNG = "",
                                    Shortname = "rifle.ak",
                                    Rare = 0,
                                    Sprite = "",
                                    SkinID = 0,
                                    ContentsItem = new List<Kits.ItemsKit.ItemContents> { }
                                },
                                new Kits.ItemsKit
                                {
                                    ContainerItemType = ContainerItem.containerMain,
                                    RandomDropSettings = new Kits.ItemsKit.RandomingDrop
                                    {
                                        UseRandomItems = false,
                                        MinAmount = 1,
                                        MaxAmount = 1
                                    },
                                    DisplayName = "Ak104",
                                    Amount = 1,
                                    Command = "",
                                    PNG = "",
                                    Shortname = "rifle.ak",
                                    Rare = 0,
                                    Sprite = "",
                                    SkinID = 0,
                                    ContentsItem = new List<Kits.ItemsKit.ItemContents> { }
                                },
                            },
                        },
                        ["med"] = new Kits
                        {
                            TypeKit = TypeKits.Amount,
                            Amount = 10,
                            CoolDown = 0,
                            WipeOpened = 1,
                            DisplayName = "Медик",
                            Permission = "iqkits.default",
                            RankUser = "",
                            UseRaidBlock = false,
                            PNG = "",
                            Shortname = "",
                            Sprite = "assets/icons/broadcast.png",
                            ItemKits = new List<Kits.ItemsKit>
                            {
                                new Kits.ItemsKit
                                {
                                    ContainerItemType = ContainerItem.containerMain,
                                    RandomDropSettings = new Kits.ItemsKit.RandomingDrop
                                    {
                                        UseRandomItems = false,
                                        MinAmount = 1,
                                        MaxAmount = 1
                                    },
                                    DisplayName = "Ak103",
                                    Amount = 1,
                                    Command = "",
                                    PNG = "",
                                    Shortname = "rifle.ak",
                                    Rare = 0,
                                    Sprite = "",
                                    SkinID = 0,
                                    ContentsItem = new List<Kits.ItemsKit.ItemContents> { }
                                },
                                new Kits.ItemsKit
                                {
                                    ContainerItemType = ContainerItem.containerMain,
                                    RandomDropSettings = new Kits.ItemsKit.RandomingDrop
                                    {
                                        UseRandomItems = false,
                                        MinAmount = 1,
                                        MaxAmount = 1
                                    },
                                    DisplayName = "Ak104",
                                    Amount = 1,
                                    Command = "",
                                    PNG = "",
                                    Shortname = "rifle.ak",
                                    Rare = 0,
                                    Sprite = "",
                                    SkinID = 0,
                                    ContentsItem = new List<Kits.ItemsKit.ItemContents> { }
                                },
                            },
                        },
                        ["food"] = new Kits
                        {
                            TypeKit = TypeKits.AmountCooldown,
                            Amount = 10,
                            CoolDown = 300,
                            DisplayName = "Еда",
                            WipeOpened = 2,
                            Permission = "iqkits.default",
                            RankUser = "",
                            UseRaidBlock = false,
                            PNG = "https://i.imgur.com/rSWlSlN.png",
                            Shortname = "",
                            Sprite = "",
                            ItemKits = new List<Kits.ItemsKit>
                            {
                                new Kits.ItemsKit
                                {
                                    ContainerItemType = ContainerItem.containerMain,
                                    RandomDropSettings = new Kits.ItemsKit.RandomingDrop
                                    {
                                        UseRandomItems = false,
                                        MinAmount = 1,
                                        MaxAmount = 1
                                    },
                                    DisplayName = "Ak103",
                                    Amount = 1,
                                    Command = "",
                                    PNG = "",
                                    Shortname = "rifle.ak",
                                    Rare = 0,
                                    Sprite = "",
                                    SkinID = 0,
                                    ContentsItem = new List<Kits.ItemsKit.ItemContents> { }
                                },
                                new Kits.ItemsKit
                                {
                                    ContainerItemType = ContainerItem.containerMain,
                                    RandomDropSettings = new Kits.ItemsKit.RandomingDrop
                                    {
                                        UseRandomItems = false,
                                        MinAmount = 1,
                                        MaxAmount = 1
                                    },
                                    DisplayName = "Ak104",
                                    Amount = 1,
                                    Command = "",
                                    PNG = "",
                                    Shortname = "rifle.ak",
                                    Rare = 0,
                                    Sprite = "",
                                    SkinID = 0,
                                    ContentsItem = new List<Kits.ItemsKit.ItemContents> { }
                                },
                            },
                        },
                                            },
                    GeneralSetting = new GeneralSettings
                    {
                        AutoWipeClearKits = true,
                        AutoKitSettings = new GeneralSettings.AutoKit
                        {
                            TypeAuto = TypeAutoKit.Single,
                            StartKitKey = "start1",
                            KitListRandom = new List<GeneralSettings.AutoKit.KitSettings>
                            {
                                new GeneralSettings.AutoKit.KitSettings
                                {
                                    Permissions = "iqkits.vip",
                                    StartKitKey = "start1"
                                },
                                new GeneralSettings.AutoKit.KitSettings
                                {
                                    Permissions = "iqkits.hunter",
                                    StartKitKey = "food"
                                },
                            },
                            KitListPriority = new List<GeneralSettings.AutoKit.KitSettings>
                            {
                                new GeneralSettings.AutoKit.KitSettings
                                {
                                    Permissions = "iqkits.vip",
                                    StartKitKey = "start1"
                                },
                                new GeneralSettings.AutoKit.KitSettings
                                {
                                    Permissions = "iqkits.hunter",
                                    StartKitKey = "food"
                                },
                            },
                            BiomeStartedKitList = new List<GeneralSettings.AutoKit.BiomeKits>
                            {
                                   new GeneralSettings.AutoKit.BiomeKits
                                   {
                                       biomeType = BiomeType.Arctic,
                                       Kits = new GeneralSettings.AutoKit.KitSettings
                                       {
                                           Permissions = "iqkits.default",
                                           StartKitKey = "start1"
                                       }
                                   },
                                   new GeneralSettings.AutoKit.BiomeKits
                                   {
                                       biomeType = BiomeType.Arid,
                                       Kits = new GeneralSettings.AutoKit.KitSettings
                                       {
                                           Permissions = "iqkits.default",
                                           StartKitKey = "start1"
                                       }
                                   },
                                   new GeneralSettings.AutoKit.BiomeKits
                                   {
                                       biomeType = BiomeType.None,
                                       Kits = new GeneralSettings.AutoKit.KitSettings
                                       {
                                           Permissions = "iqkits.default",
                                           StartKitKey = "start1"
                                       }
                                   },
                                   new GeneralSettings.AutoKit.BiomeKits
                                   {
                                       biomeType = BiomeType.Temperate,
                                       Kits = new GeneralSettings.AutoKit.KitSettings
                                       {
                                           Permissions = "iqkits.default",
                                           StartKitKey = "start1"
                                       }
                                   },
                                   new GeneralSettings.AutoKit.BiomeKits
                                   {
                                       biomeType = BiomeType.Tundra,
                                       Kits = new GeneralSettings.AutoKit.KitSettings
                                       {
                                           Permissions = "iqkits.default",
                                           StartKitKey = "start1"
                                       }
                                   },
                            },
                        },
                    },
                    InterfaceSetting = new InterfaceSettings
                    {
                        CloseType = true,
                        CloseUiTakeKit = false,
                        HEXBackground = "#0000006A",
                        HEXBlock = "#646361A6",
                        HEXAccesButton = "#708a47",
                        HEXBlockItemInfo = "#3D492837",
                        HEXInfoItemButton = "#8a6347",
                        HEXCooldowns = "#708A47D8",
                        HEXLabels = "#FFFFFFFF",
                        HEXLabelsAccesButton = "#C9E39FFF",
                        HEXLabelsInfoItemButton = "#C9E39FFF",
                        InterfaceFadeIn = 0.35f,
                        InterfaceFadeOut = 0.35f,
                        PNGAlert = "https://i.imgur.com/g4Mzn9a.png",
                        PNGInfoPanel = "https://i.imgur.com/9kbOqHK.png",
                    }
                };
            }
        }
        
                private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<String, String>
            {
                ["UI_TITLE"] = "<size=45><b>KITS MENU</b></size>",
                ["UI_DESCRIPTION"] = "<size=25><b>Your available kits are displayed here</b></size>",

                ["UI_DISPLAY_NAME_KIT"] = "<size=12><b>DISPLAY NAME KIT</b></size>\n <size=30><b>{0}</b></size>",
                ["UI_COOLDONW_KIT_NO"] = "<size=12><b>COOLDOWN</b></size>\n <size=25><b>KIT AVAILABLE</b></size>",
                ["UI_COOLDONW_KIT"] = "<size=12><b>COOLDOWN</b></size>\n <size=30><b>{0}</b></size>",
                ["UI_AMOUNT_KIT"] = "<size=12><b>AMOUNT</b></size>\n <size=30><b>{0}</b></size>",
                ["UI_BTN_WHAT_INFO"] = "<size=12><b>WHAT IS INSIDE?</b></size>",
                ["UI_BTN_TAKE_KIT"] = "<size=12><b>PICK UP</b></size>",
                ["UI_BTN_TAKE_KIT_BLOCK"] = "<size=12><b>WAIT</b></size>",
                ["UI_WHAT_INFO_TITLE"] = "<size=25><b>ITEMS IN THE {0} SET</b></size>",
                ["UI_CLOSE_BTN"] = "<size=30><b>CLOSE</b></size>",
                ["UI_HIDE_BTN"] = "<size=30><b>HIDE</b></size>",
                ["UI_NEXT_BTN"] = "<size=30><b>NEXT</b></size>",
                ["UI_BACK_BTN"] = "<size=30><b>BACK</b></size>",
                ["UI_ALERT_ACCES_KIT"] = "<size=20><b>YOU SUCCESSFULLY RECEIVED THE KIT {0}</b></size>",
                ["UI_ALERT_FULL_INVENTORY"] = "<size=20><b>YOU CANNOT TAKE THE KIT, THE INVENTORY IS OVERFULL</b></size>",
                
                ["CHAT_ALERT_PLAYER_COLDOWN_KIT"] = "Cooldown : <color=#E69765>{0}</color>",
                ["CHAT_ALERT_PLAYER_AMOUNT_KIT"] = "You have fully used the available amount of this set",
                ["CHAT_ALERT_PLAYER_FULL_INVENTORY"] = "Your inventory does not have enough space for this set!",
                ["CHAT_ALERT_PLAYER_TAKE_KIT"] = "You have successfully received a kit",
                ["CHAT_ALERT_PLAYER_INFO_KIT_LIST"] = "You don't have an available set with this name!\n\nAvailable kits : {0}\n\nChat example: /kit name\nUsage example via bind : bind x kit name",
                ["CHAT_ALERT_PLAYER_INFO_KIT_LIST_SHOW"] = "Available kits : {0}\n\nChat example: /kit name\nUsage example via bind : bind x kit name",
                ["CHAT_ALERT_PLAYER_INFO_KIT_LIST_KIT_FORMAT_YES"] = "\n- {0} : <color=#90E665>available</color>",
                ["CHAT_ALERT_PLAYER_INFO_KIT_LIST_KIT_FORMAT_COOLDOWN"] = "\n- {0} : through <color=#E69765>{1}</color>",
                ["CHAT_ALERT_PLAYER_INFO_KIT_LIST_KIT_FORMAT_AMOUNT"] = "\n- {0} : amount <color=#90E665>{1}</color>",
                ["CHAT_ALERT_PLAYER_KIT_COMMAND_EMPTY_KEY"] = "You did not enter a dial key",
            }, this);

            lang.RegisterMessages(new Dictionary<String, String>
            {
                ["UI_TITLE"] = "<size=45><b>НАБОРЫ</b></size>",
                ["UI_DESCRIPTION"] = "<size=25><b>Здесь отображены ваши доступные наборы</b></size>",

                ["UI_DISPLAY_NAME_KIT"] = "<size=12><b>НАЗВАНИЕ НАБОРА</b></size>\n <size=30><b>{0}</b></size>",
                ["UI_COOLDONW_KIT"] = "<size=12><b>ПЕРЕЗАРЯДКА</b></size>\n <size=30><b>{0}</b></size>",
                ["UI_AMOUNT_KIT"] = "<size=12><b>КОЛИЧЕСТВО</b></size>\n <size=30><b>{0}</b></size>",
                ["UI_COOLDONW_KIT_NO"] = "<size=12><b>ПЕРЕЗАРЯДКА</b></size>\n <size=25><b>НАБОР ДОСТУПЕН</b></size>",
                ["UI_BTN_WHAT_INFO"] = "<size=12><b>ЧТО ВНУТРИ?</b></size>",
                ["UI_BTN_TAKE_KIT"] = "<size=12><b>ЗАБРАТЬ</b></size>",
                ["UI_BTN_TAKE_KIT_BLOCK"] = "<size=12><b>ОЖИДАЙТЕ</b></size>",
                ["UI_WHAT_INFO_TITLE"] = "<size=25><b>ПРЕДМЕТЫ В НАБОРЕ {0}</b></size>",
                ["UI_CLOSE_BTN"] = "<size=30><b>ЗАКРЫТЬ</b></size>",
                ["UI_HIDE_BTN"] = "<size=30><b>СКРЫТЬ</b></size>",
                ["UI_NEXT_BTN"] = "<size=30><b>ВПЕРЕД</b></size>",
                ["UI_BACK_BTN"] = "<size=30><b>НАЗАД</b></size>",
                ["UI_ALERT_ACCES_KIT"] = "<size=20><b>ВЫ УСПЕШНО ПОЛУЧИЛИ НАБОР {0}</b></size>",
                ["UI_ALERT_FULL_INVENTORY"] = "<size=20><b>ВЫ НЕ МОЖЕТЕ ВЗЯТЬ НАБОР, ИНВЕНТАРЬ ПЕРЕПОЛНЕН</b></size>",
                
                ["CHAT_ALERT_PLAYER_COLDOWN_KIT"] = "Данный набор перезаряжается : <color=#E69765>{0}</color>",
                ["CHAT_ALERT_PLAYER_AMOUNT_KIT"] = "Вы полностью использовали доступное количество данного набора",
                ["CHAT_ALERT_PLAYER_FULL_INVENTORY"] = "В вашем инвентаре недостаточно места для данного набора!",
                ["CHAT_ALERT_PLAYER_TAKE_KIT"] = "Вы успешно получили набор",
                ["CHAT_ALERT_PLAYER_INFO_KIT_LIST"] = "У вас нет доступного набора с таким названием!\n\nДоступные наборы : {0}\n\nПример использования через чат : /kit name\nПример использования через бинд : bind x kit name",
                ["CHAT_ALERT_PLAYER_INFO_KIT_LIST_SHOW"] = "Доступные наборы : {0}\n\nПример использования через чат : /kit name\nПример использования через бинд : bind x kit name",
                ["CHAT_ALERT_PLAYER_INFO_KIT_LIST_KIT_FORMAT_YES"] = "\n- {0} : <color=#90E665>доступен</color>",
                ["CHAT_ALERT_PLAYER_INFO_KIT_LIST_KIT_FORMAT_COOLDOWN"] = "\n- {0} : через <color=#E69765>{1}</color>",
                ["CHAT_ALERT_PLAYER_INFO_KIT_LIST_KIT_FORMAT_AMOUNT"] = "\n- {0} : осталось <color=#90E665>{1}</color>",
                ["CHAT_ALERT_PLAYER_KIT_COMMAND_EMPTY_KEY"] = "Вы не ввели ключ набора",

            }, this, "ru");
            Puts("Языковой файл загружен успешно");
        }

        internal class ShortInfoKit
        {
            public String Shortname;
            public Int32 Amount;
            public UInt64 SkinID;
        }
        public Dictionary<BasePlayer, Int32> PagePlayers = new Dictionary<BasePlayer, Int32>();
        public static Int32 WipeTime = RealTime.Subtract(TimeCreatedSave).Days;
        
                private IEnumerator UI_UpdateCooldown(BasePlayer player)
        {
            Configuration.InterfaceSettings Interface = config.InterfaceSetting;

            IEnumerable<KeyValuePair<String, Configuration.Kits>> Kits = GetKits(player);
            while (player.HasFlag(BaseEntity.Flags.Reserved3))
            {
                Int32 i = 0;
                foreach (KeyValuePair<String, Configuration.Kits> Kit in Kits.Skip(4 * (PagePlayers[player])).Take(4))
                {
                    CuiElementContainer container = new CuiElementContainer();

                    CuiHelper.DestroyUi(player, $"COOLDOWN_LINE_{i}");
                    CuiHelper.DestroyUi(player, $"COOLDOWN_TITLE{i}");
		   		 		  						  	   		  	  			  	  			  		  		   			
                    DataKitsUser.InfoKits Data = DataKitsUserList[player.userID].InfoKitsList[Kit.Key];
                    Boolean IsCooldown = Data.Cooldown >= CurrentTime();

                    Double XMax = IsCooldown ? (Double)((Data.Cooldown - CurrentTime()) * Math.Pow(Kit.Value.CoolDown, -1)) : Data.Amount != 0 ? (Double)((Data.Amount) * Math.Pow(Kit.Value.Amount, -1)) : 1;
                    container.Add(new CuiPanel
                    {
                        CursorEnabled = true,
                        RectTransform = { AnchorMin = $"0 0", AnchorMax = $"{XMax} 1", OffsetMin = "1 1", OffsetMax = "-2 -1" },
                        Image = { Color = HexToRustFormat(Interface.HEXCooldowns) }
                    }, $"COOLDOWN_PANEL_{i}", $"COOLDOWN_LINE_{i}");
		   		 		  						  	   		  	  			  	  			  		  		   			
                    String InfoAmountAndCooldown = IsCooldown ? GetLang("UI_COOLDONW_KIT", player.UserIDString, FormatTime(TimeSpan.FromSeconds(Data.Cooldown - CurrentTime()))) : Data.Amount != 0 ? GetLang("UI_AMOUNT_KIT", player.UserIDString, Data.Amount) : GetLang("UI_COOLDONW_KIT_NO", player.UserIDString);
                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "0.965517 0.9449963" },
                        Text = { Text = InfoAmountAndCooldown, Color = HexToRustFormat(Interface.HEXLabels), Font = "robotocondensed-bold.ttf", Align = TextAnchor.UpperRight }
                    }, $"COOLDOWN_PANEL_{i}", $"COOLDOWN_TITLE{i}");
		   		 		  						  	   		  	  			  	  			  		  		   			
                    i++;
                    CuiHelper.AddUi(player, container);
                }
                yield return new WaitForSeconds(1);
            }
        }
        
        
        [ChatCommand("kitlist")]
        private void ShowKitList(BasePlayer player)
        {
            String KistList = String.Empty;

            foreach (KeyValuePair<String, DataKitsUser.InfoKits> keyValuePair in DataKitsUserList[player.userID].InfoKitsList.OrderBy(x => x.Value.Cooldown >= CurrentTime()).Where(k => config.KitList.ContainsKey(k.Key) && ((config.KitList[k.Key].TypeKit != TypeKits.Started && WipeTime >= config.KitList[k.Key].WipeOpened) && (((config.KitList[k.Key].TypeKit == TypeKits.Amount || config.KitList[k.Key].TypeKit == TypeKits.AmountCooldown) && (DataKitsUserList[player.userID].InfoKitsList.ContainsKey(k.Key) && DataKitsUserList[player.userID].InfoKitsList[k.Key].Amount > 0)) || config.KitList[k.Key].TypeKit == TypeKits.Cooldown))))
            {
                Boolean IsCooldown = keyValuePair.Value.Cooldown >= CurrentTime();
                String InfoAmountAndCooldown = IsCooldown ? GetLang("CHAT_ALERT_PLAYER_INFO_KIT_LIST_KIT_FORMAT_COOLDOWN", player.UserIDString, keyValuePair.Key, FormatTime(TimeSpan.FromSeconds(keyValuePair.Value.Cooldown - CurrentTime()))) : keyValuePair.Value.Amount != 0 ? GetLang("CHAT_ALERT_PLAYER_INFO_KIT_LIST_KIT_FORMAT_AMOUNT", player.UserIDString, keyValuePair.Key, keyValuePair.Value.Amount) : GetLang("CHAT_ALERT_PLAYER_INFO_KIT_LIST_KIT_FORMAT_YES", player.UserIDString, keyValuePair.Key);

                KistList += InfoAmountAndCooldown;
            }
            SendChat(player, GetLang("CHAT_ALERT_PLAYER_INFO_KIT_LIST_SHOW", player.UserIDString, KistList));
        }
        
        
        
        private String API_KIT_GET_AUTO_KIT()
        {
            return config.GeneralSetting.AutoKitSettings.StartKitKey;
        }

        private Boolean IS_SKILL_RARE(BasePlayer player)
        {
            return (Boolean)IQPlagueSkill?.CallHook("API_IS_RARE_SKILL_KITS", player);
        }

        
                private void KitEdit(BasePlayer player, String NameKit)
        {
            if (!player.IsAdmin) return;

            if (!config.KitList.ContainsKey(NameKit))
            {
                SendChat(player, "Ключ данного набора не существует!");
                return;
            }

            Configuration.Kits Kit = config.KitList[NameKit];
            Kit.ItemKits = GetPlayerItems(player);

            SaveConfig();
            SendChat(player, $"Предметы набора с ключем {NameKit} успешно изменены, настройки сохранены");
        }
        private Configuration.Kits.ItemsKit ItemToKit(Item item, ContainerItem containerItem)
        {
            Configuration.Kits.ItemsKit ItemsKit = new Configuration.Kits.ItemsKit
            {
                Amount = item.amount,
                ContainerItemType = containerItem,
                Shortname = item.info.shortname,
                SkinID = item.skin,
                Rare = 0,
                PNG = "",
                Sprite = "",
                Command = "",
                DisplayName = ""
            };
            ItemsKit.RandomDropSettings = new Configuration.Kits.ItemsKit.RandomingDrop
            {
                MinAmount = 0,
                MaxAmount = 0,
                UseRandomItems = false,
            };
            ItemsKit.ContentsItem = GetContentItem(item);

            return ItemsKit;
        }
        
                [JsonProperty("Дата с информацией о игроках")]
        public Hash<UInt64, DataKitsUser> DataKitsUserList = new Hash<UInt64, DataKitsUser>();

        private Int32 API_KIT_GET_MAX_AMOUNT(String KitKey)
        {
            if (String.IsNullOrWhiteSpace(KitKey)) return 0;
            if (!config.KitList.ContainsKey(KitKey))
            {
                PrintError($"API_KIT_GET_MAX_AMOUNT : Ключа {KitKey} не существует!");
                return 0;
            }
            return config.KitList[KitKey].Amount;
        }
        
                private void TakeKit(BasePlayer player, String KitKey, Boolean IsManual = false)
        {
            Configuration.Kits Kit = config.KitList[KitKey];
            List<Configuration.Kits.ItemsKit> ItemKit = Kit.ItemKits;
            DataKitsUser.InfoKits Data = DataKitsUserList[player.userID].InfoKitsList[KitKey];

            if(Kit.TypeKit == TypeKits.Cooldown || Kit.TypeKit == TypeKits.AmountCooldown)
                if (Data.Cooldown >= CurrentTime())
                {
                    if (IsManual)
                        SendChat(player,
                            GetLang("CHAT_ALERT_PLAYER_COLDOWN_KIT", player.UserIDString,
                                FormatTime(TimeSpan.FromSeconds(Data.Cooldown - CurrentTime()))));
                    return;
                }

            if(Kit.TypeKit == TypeKits.Amount || Kit.TypeKit == TypeKits.AmountCooldown)
                if (Data.Amount <= 0)
                {
                    if (IsManual)
                        SendChat(player,
                            GetLang("CHAT_ALERT_PLAYER_AMOUNT_KIT", player.UserIDString,
                                FormatTime(TimeSpan.FromSeconds(Data.Cooldown - CurrentTime()))));
                    return;
                }

            Int32 BeltAmount = ItemKit.Count(i => i.ContainerItemType == ContainerItem.containerBelt);
            Int32 WearAmount = ItemKit.Count(i => i.ContainerItemType == ContainerItem.containerWear);
            Int32 MainAmount = ItemKit.Count(i => i.ContainerItemType == ContainerItem.containerMain);

            Int32 Total = BeltAmount + WearAmount + MainAmount;
            if ((player.inventory.containerBelt.capacity - player.inventory.containerBelt.itemList.Count) < BeltAmount
            || (player.inventory.containerWear.capacity - player.inventory.containerWear.itemList.Count) < WearAmount
            || (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count) < MainAmount)
            {
                if (Total > (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count))
                {
                    if(IsManual)
                        SendChat(player, GetLang("CHAT_ALERT_PLAYER_FULL_INVENTORY", player.UserIDString));
                    else Interface_Alert_Kits(player, GetLang("UI_ALERT_FULL_INVENTORY", player.UserIDString));
                    return;
                }
            }

            switch (Kit.TypeKit)
            {
                case TypeKits.Cooldown:
                    {
                        Int32 Cooldown = IQPlagueSkill ? IS_SKILL_COOLDOWN(player) ? (Kit.CoolDown - (Kit.CoolDown / 100 * GET_SKILL_COOLDOWN_PERCENT())) : Kit.CoolDown : Kit.CoolDown;
                        Data.Cooldown = Convert.ToInt32(CurrentTime() + Cooldown);
                        break;
                    }
                case TypeKits.Amount:
                    {
                        Data.Amount--;
                        break;
                    }
                case TypeKits.AmountCooldown:
                    {
                        Int32 Cooldown = IQPlagueSkill ? IS_SKILL_COOLDOWN(player) ? (Kit.CoolDown - (Kit.CoolDown / 100 * GET_SKILL_COOLDOWN_PERCENT())) : Kit.CoolDown : Kit.CoolDown;
                        Data.Amount--;
                        Data.Cooldown = Convert.ToInt32(CurrentTime() + Cooldown); break;
                    }
            }
            ParseAndGive(player, KitKey);
            if (IsManual)
            {
                SendChat(player, GetLang("CHAT_ALERT_PLAYER_TAKE_KIT", player.UserIDString));
                return;
            }
            
            if (!config.InterfaceSetting.CloseUiTakeKit)
            {
                DestroyKits(player);
                Interface_Loaded_Kits(player);
                Interface_Alert_Kits(player, GetLang("UI_ALERT_ACCES_KIT", player.UserIDString, Kit.DisplayName));
            }
            else
            {
                DestroyAll(player);
            }
        }

        private void CachingImage(BasePlayer player)
        {
            Configuration.InterfaceSettings Interface = config.InterfaceSetting;
            foreach (KeyValuePair<String, Configuration.Kits> Kit in config.KitList.Where(k => !String.IsNullOrWhiteSpace(k.Value.PNG)))
            {
                SendImage(player, $"KIT_{Kit.Value.PNG}");

                foreach (Configuration.Kits.ItemsKit ItemKit in Kit.Value.ItemKits.Where(ik => !String.IsNullOrWhiteSpace(ik.Shortname)))
                    SendImage(player, $"{ItemKit.Shortname}_128px");

                foreach (Configuration.Kits.ItemsKit img in Kit.Value.ItemKits.Where(i => !String.IsNullOrWhiteSpace(i.PNG)))
                {
                    if (!HasImage($"ITEM_KIT_PNG_{img.PNG}"))
                        AddImage(img.PNG, $"ITEM_KIT_PNG_{img.PNG}");
                }
            }
            SendImage(player, $"INFO_BACKGROUND_{Interface.PNGInfoPanel}");
            SendImage(player, $"INFO_ALERT_BACKGROUND_{Interface.PNGAlert}");
        }

        private static Double CurrentTime()
        {
            return DateTime.UtcNow.Subtract(epoch).TotalSeconds;
        }
        private enum BiomeType
        {
            None,
            Arid,
            Temperate,
            Tundra,
            Arctic
        }

        private void RegisteredDataUser(BasePlayer player)
        {
            if (!DataKitsUserList.ContainsKey(player.userID))
            {
                DataKitsUserList.Add(player.userID, new DataKitsUser
                {
                    InfoKitsList = new Dictionary<String, DataKitsUser.InfoKits> { }
                });
            }

            foreach (KeyValuePair<String, Configuration.Kits> Kit in config.KitList.Where(x => !DataKitsUserList[player.userID].InfoKitsList.ContainsKey(x.Key) && (String.IsNullOrWhiteSpace(x.Value.Permission) || permission.UserHasPermission(player.UserIDString, x.Value.Permission))))
                DataKitsUserList[player.userID].InfoKitsList.Add(Kit.Key, new DataKitsUser.InfoKits { Amount = Kit.Value.Amount, Cooldown = 0 });
        }
        
        
        
        public static DateTime TimeCreatedSave = SaveRestore.SaveCreatedTime.Date;
		   		 		  						  	   		  	  			  	  			  		  		   			
        private enum ContainerItem
        {
            containerWear,
            containerBelt,
            containerMain
        }

        
            }
}
