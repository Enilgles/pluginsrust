/*
*  < ----- End-User License Agreement ----->
*  
*  You may not copy, modify, merge, publish, distribute, sublicense, or sell copies of this software without the developer’s consent.
*
*  THIS SOFTWARE IS PROVIDED BY IIIaKa AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, 
*  THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS 
*  BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
*  GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT 
*  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*
*  Developer: IIIaKa
*      https://t.me/iiiaka
*      Discord: @iiiaka
*      https://github.com/IIIaKa
*      https://umod.org/user/IIIaKa
*      https://codefling.com/iiiaka
*      https://www.patreon.com/iiiaka
*      https://boosty.to/iiiaka
*  Codefling plugin page: https://codefling.com/plugins/real-pve
*  Codefling license: https://codefling.com/plugins/real-pve?tab=downloads_field_4
*
*  Copyright © 2024 IIIaKa
*/

using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System.Globalization;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using Rust;
using Oxide.Plugins.ExtensionsRealPVE;
using UnityEngine.UI;
using Rust.Ai.Gen2;

namespace Oxide.Plugins
{
	[Info("RealPVE", "IIIaKa", "0.1.13")]
	[Description("Plugin for Real PvE servers, featuring damage prevention, anti-griefing measures, customizable PvP zones, an automatic loot queue in radtowns and raid zones, and much more.")]
	class RealPVE : RustPlugin
    {

		private void InitMonuments()
		{
			_harborEventMonument = string.Empty;
			_monumentsList.Clear();
			var pvpMonuments = new HashSet<string>();
			var monuments = (Dictionary<string, string>)(MonumentsWatcher?.Call("GetMonumentsTypeDictionary") ?? new Dictionary<string, string>());
			MonumentSettings monumentSettings;
			foreach (var kvp in monuments)
			{
				if (!_monumentsConfig.TrackedTypes.Contains(kvp.Value) || _monumentsConfig.IgnoredNames.Contains(kvp.Key)) continue;
				var monumentID = kvp.Key;
				if (monumentID.Contains("CargoShip"))
				{
					if (!_monumentsConfig.IgnoredNames.Contains("CargoShip") && IsMonumentCargoValid(monumentID))
					{
						monumentSettings = _monumentsConfig.MonumentsSettings["CargoShip"];
						_monumentsList[monumentID] = new MonumentData(monumentID, monumentSettings, true);
						if (monumentSettings.IsPvP)
							pvpMonuments.Add(GetMonumentName(monumentID));
					}
					continue;
				}
				if (!_monumentsConfig.MonumentsSettings.TryGetValue(monumentID, out monumentSettings) || monumentSettings == null)
					_monumentsConfig.MonumentsSettings[monumentID] = monumentSettings = new MonumentSettings(monumentID, kvp.Value);
				_monumentsList[monumentID] = new MonumentData(monumentID, monumentSettings);
				if (monumentSettings.IsPvP)
					pvpMonuments.Add(GetMonumentName(monumentID));
			}
			SaveConfig();
			LoadMonumentsImages();
			if (!_monumentsList.Any())
				return;
			if (pvpMonuments.Any())
				PrintWarning($"PvP flagged monuments: {string.Join(", ", pvpMonuments)}");
			foreach (var player in BasePlayer.activePlayerList)
			{
				if (player.userID.IsSteamId() && _monumentsList.TryGetValue(GetPlayerMonument(player.userID), out var monumentData))
					monumentData.OnPlayerEnter(player);
			}
			Subscribe(nameof(OnNpcTarget));
			Subscribe(nameof(OnEntityEnteredMonument));
			Subscribe(nameof(OnEntityExitedMonument));
			Subscribe(nameof(OnPlayerEnteredMonument));
			Subscribe(nameof(OnPlayerExitedMonument));
			Subscribe(nameof(OnCargoWatcherCreated));
			Subscribe(nameof(OnCargoWatcherDeleted));
			if (_monumentsConfig.CargoShip_HarborToPvP || _monumentsConfig.CargoShip_LargeHarborToPvP)
			{
				Subscribe(nameof(OnCargoShipHarborArrived));
				Subscribe(nameof(OnCargoShipHarborLeave));
			}
			Subscribe(nameof(OnHarborEventStart));
			Subscribe(nameof(OnHarborEventEnd));
			if (_monumentsList.ContainsKey("excavator_1"))
			{
				Subscribe(nameof(OnExcavatorResourceSet));
				Subscribe(nameof(OnExcavatorSuppliesRequest));
				Subscribe(nameof(OnExcavatorSuppliesRequested));
			}
			Subscribe(nameof(CanHackCrate));
			SaveMonumentsConfig();
		}

		object OnCrateLaptopAttack(HackableLockedCrate crate, HitInfo info)
		{
			if (info != null && info.InitiatorPlayer is BasePlayer attacker && _monumentsList.TryGetValue(GetEntityMonument(crate), out var monumentData))
				return monumentData.CanLoot(attacker);
			return false;
		}
		object CanLootEntity(BasePlayer player, Stocking stocking) => CanLootStorage(player, stocking);

        		private static RealPVE Instance { get; set; }
		
		void OnEntitySpawned(LootableCorpse corpse)
        {
			if (corpse.playerSteamID.IsSteamId()) return;
			var parentEnt = corpse.parentEnt;
            if (parentEnt != null && parentEnt.skinID != 0uL)
            {
                corpse.skinID = parentEnt.skinID;
                if (parentEnt.skinID == _rrPluginID && _config.RandomRaids_Enabled && _rrAllRaiders.TryGetValue(parentEnt.net.ID.Value, out var rrData))
                {
                    _rrAllRaiders.Add(corpse.net.ID.Value, rrData);
                    rrData.Raiders.Add(corpse.net.ID.Value);
                }
            }
		}
        private string GetNpcMonument(BasePlayer npcPlayer) => (string)(MonumentsWatcher?.Call(MonumentGetNpcMonument, npcPlayer.net.ID) ?? string.Empty);
		
        		private const string _uiVehiclePanel = "RealPVE_VehiclePanel", _dataVehiclesPath = @"RealPVE\Data\VehiclesData";
		private const string MonumentGetMonumentDisplayName = "GetMonumentDisplayName", MonumentGetMonumentType = "GetMonumentType", MonumentGetMonumentPlayers = "GetMonumentPlayers", MonumentGetMonumentEntities = "GetMonumentEntities", MonumentGetPlayerMonument = "GetPlayerMonument", MonumentGetNpcMonument = "GetNpcMonument", MonumentGetEntityMonument = "GetEntityMonument", MonumentGetMonumentPosition = "GetMonumentPosition", MonumentGetMonumentsByPos = "GetMonumentsByPos", MonumentIsPlayerInMonument = "IsPlayerInMonument";

		private object CanLootByOwnerID(BasePlayer player, BaseEntity entity)
		{
			if (entity.OwnerID.IsSteamId() && !_unrestrictedLooters.Contains(player.userID) && !IsEntityInPvP(player.userID, entity.net.ID.Value))
			{
				object result = player.TasirMumkin(entity.OwnerID);
				if (result != null)
					SendMessage(player, "MsgCantInteract");
				return result;
			}
			return null;
		}
		
		private void GiveDefaultItems(BasePlayer player)
        {
			if (player.isMounted) return;
			var inventory = player.inventory;
			bool canOverride = true;
            var bags = Pool.Get<List<SleepingBag>>();
            Vis.Entities(inventory.baseEntity.transform.position, 0.1f, bags);
            foreach (var bag in bags)
            {
                if (bag.deployerUserID == player.userID)
                {
                    canOverride = false;
                    break;
                }
            }
            Pool.FreeUnmanaged(ref bags);
			if (!canOverride)
				return;
			
			inventory.Strip();
            foreach (var rItem in _beachConfig.Respawn_Main)
            {
                var item = ItemManager.CreateByName(rItem.ShortName, rItem.Amount, rItem.SkinID);
                if (item != null)
                {
                    if (!string.IsNullOrWhiteSpace(rItem.Text))
                        item.text = lang.GetMessage(rItem.Text, this, player.UserIDString);
                    item.MoveToContainer(inventory.containerMain, rItem.Slot);
                }
            }
            foreach (var rItem in _beachConfig.Respawn_Belt)
            {
                var item = ItemManager.CreateByName(rItem.ShortName, rItem.Amount, rItem.SkinID);
                if (item != null)
                {
                    if (!string.IsNullOrWhiteSpace(rItem.Text))
                        item.text = lang.GetMessage(rItem.Text, this, player.UserIDString);
                    item.MoveToContainer(inventory.containerBelt, rItem.Slot);
                }
            }
            foreach (var rItem in _beachConfig.Respawn_Wear)
            {
                var item = ItemManager.CreateByName(rItem.ShortName, rItem.Amount, rItem.SkinID);
                if (item != null)
                {
                    if (!string.IsNullOrWhiteSpace(rItem.Text))
                        item.text = lang.GetMessage(rItem.Text, this, player.UserIDString);
                    item.MoveToContainer(inventory.containerWear, rItem.Slot);
                }
            }

            if (PlayerInventory.IsBirthday())
            {
                inventory.GiveItem(ItemManager.CreateByName("cakefiveyear", 1, 0uL), inventory.containerBelt);
                inventory.GiveItem(ItemManager.CreateByName("partyhat", 1, 0uL), inventory.containerWear);
            }
            if (PlayerInventory.IsChristmas())
            {
                inventory.GiveItem(ItemManager.CreateByName("snowball", 1, 0uL), inventory.containerBelt);
                inventory.GiveItem(ItemManager.CreateByName("snowball", 1, 0uL), inventory.containerBelt);
                inventory.GiveItem(ItemManager.CreateByName("snowball", 1, 0uL), inventory.containerBelt);
            }
        }
		
		private void DestroyUI(BasePlayer player, string uiName)
		{
			CuiHelper.DestroyUi(player, uiName);
			if (_playerUI.ContainsKey(player.userID))
				_playerUI[player.userID].Remove(uiName);
        }
		
		private void LoadMonumentsConfig()
        {
			List<CuiElement> uiList = null;
			if (Interface.Oxide.DataFileSystem.ExistsDatafile(_monumentsPath))
            {
				try
				{
					_monumentsConfig = Interface.Oxide.DataFileSystem.ReadObject<MonumentConfig>(_monumentsPath);
					uiList = Interface.Oxide.DataFileSystem.ReadObject<List<CuiElement>>(_monumentsUiOfferPath);
				}
				catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
			}
			
			if (_monumentsConfig == null || _monumentsConfig.Version < _monumentsVersion)
            {
                if (_monumentsConfig != null)
                {
                    string path = string.Format(_monumentsPathOld, _monumentsConfig.Version.ToString());
                    PrintWarning($@"Your settings version for monuments is outdated. The config file has been updated, and your old settings have been saved in \data\{path}");
                    SaveMonumentsConfig(path);
                }
				_monumentsConfig = new MonumentConfig() { Version = _monumentsVersion };
            }
			
			if (_monumentsConfig.TrackedTypes == null)
                _monumentsConfig.TrackedTypes = new string[] { "RadTown", "RadTownWater", "RadTownSmall", "TunnelStation", "Custom" };
            if (_monumentsConfig.IgnoredNames == null)
                _monumentsConfig.IgnoredNames = new string[] { "example" };
            if (_monumentsConfig.MonumentsSettings == null)
                _monumentsConfig.MonumentsSettings = new Dictionary<string, MonumentSettings>();
			else
            {
				foreach (var monumentSettings in _monumentsConfig.MonumentsSettings.Values)
                    monumentSettings.OfferTime = Math.Clamp(monumentSettings.OfferTime, 1f, 15f);
            }
			if (!_monumentsConfig.MonumentsSettings.ContainsKey("CargoShip"))
				_monumentsConfig.MonumentsSettings["CargoShip"] = new MonumentSettings("CargoShip", "RadTownWater");
			
			if (uiList == null || !uiList.Any())
            {
                uiList = GetDefaultClaimOffer();
                Interface.Oxide.DataFileSystem.WriteObject(_monumentsUiOfferPath, uiList);
            }
            _monumentsUiOffer = ReplacePlaceholders(CuiHelper.ToJson(uiList), MonumentOfferUI);
			
			SaveMonumentsConfig();
		}
		
		
		object OnCupboardAuthorize(VehiclePrivilege privilege, BasePlayer player)
        {
			object result = false;
            if (GetVehicleData(privilege.GetParentEntity(), out var vehicleData) && vehicleData.OwnerID != 0)
				result = vehicleData.CanLoot(player);
			if (result != null)
				SendMessage(player, "MsgVehicleTugboatAuthorization");
			return result;
		}
		void OnEntitySpawned(BuildingPrivlidge privlidge) => privlidge.lastNoiseTime = DateTimeOffset.UtcNow.Day;
		private HashSet<BaseEntity> GetMonumentEntities(string monumentID) => MonumentsWatcher?.Call(MonumentGetMonumentEntities, monumentID) as HashSet<BaseEntity>;
		private const string RBUI = "RealPVE_RaidableBases", RBLootUI = "RealPVE_RaidableBases_Loot", RBOfferUI = "RealPVE_RaidableBasesOffer", RBTextLootRemaining = "MsgRaidableBasesBarTextLootRemaining", RBTextLootCompleted = "MsgRaidableBasesBarTextLootCompleted";
		object CanDemolish(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade) => !block.OwnerID.IsSteamId() || IsEntityInPvP(player.userID, block.net.ID.Value) ? null : player.TasirMumkin(block.OwnerID);

		void OnCargoWatcherCreated(string monumentID, string type)
		{
			if (!_monumentsConfig.TrackedTypes.Contains(type) || _monumentsConfig.IgnoredNames.Contains("CargoShip")) return;
			NextTick(() =>
			{
				if (IsMonumentCargoValid(monumentID))
					_monumentsList[monumentID] = new MonumentData(monumentID, _monumentsConfig.MonumentsSettings["CargoShip"], true);
			});
		}

		void OnPlayerDisconnected(BasePlayer player, string reason)
		{
			_playerUI.Remove(player.userID);
			if (_config.AntiSleeper > 0f && player.BinoMumkin() != null)
				player.Invoke(Str_ScheduledDeath, _config.AntiSleeper);
			if (_monumentsList.TryGetValue(GetPlayerMonument(player.userID), out var monumentData))
				monumentData.OnPlayerExit(player, "disconnect");
		}
		
		private void Command_Admin(IPlayer player, string command, string[] args)
        {
			string replyKey = "CmdCommandFailed";
			string[] replyArgs = new string[5];
			bool isWarning = true;
			
			if (args != null && args.Length > 0 && (player.IsAdmin || permission.UserHasPermission(player.Id, PERMISSION_ADMIN)))
            {
				var bPlayer = player.Object as BasePlayer;
				ulong targetID = bPlayer != null ? bPlayer.userID : 0uL;
				if (args[0] == "loot")
                {
					if (args.Length < 2)
                    {
						if (player.IsServer)
							replyKey = "CmdCommandForPlayers";
						else if (_unrestrictedLooters.Remove(targetID))
							replyKey = "CmdAdminLootDisabled";
						else
						{
							_unrestrictedLooters.Add(targetID);
							replyKey = "CmdAdminLootEnabled";
							isWarning = false;
						}
					}
					else
                    {
                        if (args[1] == "clear")
                        {
                            replyArgs[0] = _unrestrictedLooters.Count.ToString();
                            _unrestrictedLooters.Clear();
                            SaveData(_dataLootersPath, _unrestrictedLooters);
                            replyKey = "CmdAdminLootClear";
                            isWarning = false;
                        }
                        else if (!TryGetPlayer(args[1], out var target) || !ulong.TryParse(target.Id, out targetID))
                        {
                            replyKey = "CmdAdminLootPlayerNotFound";
                            replyArgs[0] = args[1];
                        }
                        else
                        {
							replyArgs[0] = target.Name;
							if (_unrestrictedLooters.Remove(targetID))
                            {
								if (target.IsConnected)
									SendMessage(target, "CmdAdminLootDisabled");
								replyKey = "CmdAdminLootPlayerDisabled";
							}
							else
							{
								_unrestrictedLooters.Add(targetID);
								if (target.IsConnected)
									SendMessage(target, "CmdAdminLootEnabled", isWarning: false);
								replyKey = "CmdAdminLootPlayerEnabled";
								isWarning = false;
							}
                        }
                    }
				}
                else if (args[0] == "pickup")
                {
					replyKey = "CmdAdminPickupFailed";
					if (args.Length > 1 && args[1] == "clear")
                    {
						replyArgs[0] = _pickupPlayers.Count.ToString();
						_pickupPlayers.Clear();
						SaveData(_dataPickupsPath, _pickupPlayers);
						replyKey = "CmdAdminPickupClear";
						isWarning = false;
					}
				}
				else if (args[0] == "monument")
                {
					replyKey = "CmdAdminMonumentFailed";
					if (args.Length < 2) {}
					else if (!_monumentsList.TryGetValue(args[^1], out var monumentData) && (!targetID.IsSteamId() || !_monumentsList.TryGetValue(GetPlayerMonument(targetID), out monumentData)))
                    {
                        replyKey = "CmdAdminMonumentNotFound";
                        replyArgs[0] = args[^1];
                    }
                    else
                    {
						replyArgs[0] = GetMonumentName(monumentData.MonumentID, player.Id);
						if (args[1] == "pvp")
                        {
                            if (monumentData.OwnerID != targetID && monumentData.OwnerID.IsSteamId())
                                replyKey = "CmdAdminMonumentOcupied";
                            else
                            {
                                if (monumentData.IsPvP)
                                {
                                    monumentData.RemovePvP();
                                    replyKey = "CmdAdminMonumentPvPDisabled";
                                    isWarning = false;
                                }
                                else
                                {
                                    monumentData.SetAsPvP();
                                    replyKey = "CmdAdminMonumentPvPEnabled";
                                }
								monumentData.Settings.IsPvP = monumentData.IsPvP;
								SaveMonumentsConfig();
							}
                        }
                    }
                }
			}
			
			if (!string.IsNullOrWhiteSpace(replyKey))
                SendMessage(player, replyKey, replyArgs, isWarning);
		}
		
                private void SendCounterBar(BasePlayer player, RBData rbData)
        {
			if (!_statusIsLoaded) return;
			
			string text = string.Format(lang.GetMessage("MsgRaidableBasesBarText", this, player.UserIDString), lang.GetMessage(rbData.TextKey, this, player.UserIDString));
			Dictionary<int, object> parameters;
			if (rbData.Settings.UseProgress)
            {
				parameters = new Dictionary<int, object>(rbData.StatusProgressBar)
				{
					{ 15, text },
					{ 28, rbData.StartTime },
					{ 29, rbData.DespawnTime }
				};
			}
            else
			{
				parameters = new Dictionary<int, object>(rbData.StatusBar)
				{
					{ 15, text },
					{ 29, rbData.DespawnTime }
				};
				parameters[2] = BarTimeCounter;
			}
			
			AdvancedStatus?.Call(StatusCreateBar, player.userID.Get(), parameters);
        }
        
        		void OnPlayerEnterPVP(BasePlayer player, string zoneID)
        {
			if (!player.userID.IsSteamId()) return;
			if (!_pvpPlayers.TryGetValue(player.userID, out var playerPvP))
				_pvpPlayers[player.userID] = playerPvP = new PlayerPvP();
			
			if (playerPvP.ActiveZones.Contains(zoneID))
				return;
			
			playerPvP.ActiveZones.Add(zoneID);
			if (playerPvP.DelayEnd != 0d)
			{
				playerPvP.DelayEnd = 0d;
				Interface.CallHook(Hooks_OnPlayerPVPDelayRemoved, player);
			}
			if (player.IsConnected)
            {
				if (!string.IsNullOrWhiteSpace(playerPvP.LastZone))
				{
					string lastBar;
					if (_monumentsList.TryGetValue(playerPvP.LastZone, out var monumentData))
						lastBar = (string)monumentData.StatusBar[0];
					else if (_rbList.TryGetValue(playerPvP.LastZone, out var rbData))
						lastBar = (string)rbData.StatusBar[0];
					else
						lastBar = playerPvP.LastZone;
					DestroyBar(player.userID, lastBar);
				}
				
				SendPvPBar(player, zoneID);
				if (playerPvP.ActiveZones.Count == 1)
				{
					player.SendEffect();
					SendMessage(player, "MsgPvPEnter");
				}
            }
			playerPvP.LastZone = zoneID;
		}
		private static HashSet<ulong> _unrestrictedLooters, _pickupPlayers;
		public static Dictionary<ulong, RRData> _rrallPatrols = new Dictionary<ulong, RRData>();
		private readonly VersionNumber _monumentsVersion = new VersionNumber(0, 1, 3);
		object OnRackedWeaponSwap(Item item, WeaponRackSlot weaponAtIndex, BasePlayer player, WeaponRack rack) => CanLootWeaponRack(player, rack);
		private void SaveMonumentsConfig(string path = _monumentsPath) => Interface.Oxide.DataFileSystem.WriteObject(path, _monumentsConfig);

		void OnHarborEventEnd()
		{
			if (_watcherIsLoaded && !string.IsNullOrWhiteSpace(_harborEventMonument) && !_monumentsConfig.IgnoredNames.Contains(_harborEventMonument) &&
				_monumentsConfig.TrackedTypes.Contains(GetMonumentType(_harborEventMonument)))
			{
				if (!_monumentsList.TryGetValue(_harborEventMonument, out var monumentData))
                    _monumentsList[_harborEventMonument] = new MonumentData(_harborEventMonument, _monumentsConfig.MonumentsSettings[_harborEventMonument]);
                else
                    monumentData.RemovePvP();
			}
			_harborEventMonument = string.Empty;
		}
		
		private object AdminOpenLoot(BasePlayer player, StorageContainer container)
        {
			if (player.inventory.loot.StartLootingEntity(container, true))
            {
                container.SetFlag(BaseEntity.Flags.Open, b: true);
                container.AddContainers(player.inventory.loot);
                player.inventory.loot.SendImmediate();
                player.ClientRPC(RpcTarget.Player("RPC_OpenLootPanel", player), container.panelName);
                container.SendNetworkUpdate();
                return true;
            }
			return null;
		}
		
		object CanLootEntity(BasePlayer player, BaseRidableAnimal animal)
        {
			if (!IsEntityInPvP(player.userID, animal.net.ID.Value) && _vehiclesList.TryGetValue(animal.net.ID.Value, out var vehicleData))
            {
                object result = vehicleData.CanLoot(player);
                if (result != null)
                    SendMessage(player, "MsgVehicleCanNotInteract");
                return result;
            }
            return null;
        }
		object OnEntityTakeDamage(Tugboat tugboat, HitInfo info) => CanVehicleTakeDamage(tugboat.net?.ID.Value ?? 0uL, info);
		object OnEntityTakeDamage(Minicopter minicopter, HitInfo info) => CanVehicleTakeDamage(minicopter.net?.ID.Value ?? 0uL, info);
		
		object OnZoneStatusText(BasePlayer player, string zoneID)
		{
			switch (zoneID)
            {
                case "SurvivalArena":
                    return lang.GetMessage("MsgSurvivalArena", this, player.UserIDString);
                default:
                    return null;
            }
		}
		private readonly string[] HttpScheme = new string[2] { "http://", "https://" };
		
		private string _monumentsUiOffer;
		private static double _unixSeconds = 0d;
		
		void OnRaidableLootDestroyed(Vector3 pos, float radius, int lootRemain)
		{
			if (_rbList.TryGetValue(pos.ToString(), out var rbData))
				rbData.OnLootUpdated(lootRemain);
		}
		
                bool CanBypassQueue(Network.Connection connection) => ServerUsers.Is(connection.userid, ServerUsers.UserGroup.Owner) ? true : CanPlayerBypassQueue(connection.userid.ToString());

        private void RaidableBaseTimeUpdatedBar(BasePlayer player, RBData rbData)
        {
            if (!_statusIsLoaded) return;

            var parameters = new Dictionary<int, object>
            {
                { 0, rbData.RaidID },
                { 1, Name },
                { 29, rbData.DespawnTime }
            };

            AdvancedStatus?.Call(StatusCreateBar, player.userID.Get(), parameters);
        }
		
		object OnButtonPress(PressButton button, BasePlayer player)
        {
			if (_unrestrictedLooters.Contains(player.userID)) return null;
			
            object result = player.MumkinNol(button.OwnerID);
			if (result != null)
				SendMessage(player, "MsgCantInteract");
			return result;
		}
		
		
		object OnHotAirBalloonToggle(HotAirBalloon balloon, BasePlayer driver)
        {
			if (!IsEntityInPvP(driver.userID, balloon.net.ID.Value) && _vehiclesList.TryGetValue(balloon.net.ID.Value, out var vehicleData))
			{
                object result = vehicleData.CanInteract(driver);
                if (result != null)
                    SendMessage(driver, "MsgVehicleCanNotInteract");
                return result;
            }
            return null;
		}
		private void SaveVanillaEventsConfig(string path = _vanillaEventsPath) => Interface.Oxide.DataFileSystem.WriteObject(path, _vanillaEventsConfig);

        
		object CanBeTargeted(BasePlayer player, HelicopterTurret turret)
        {
			if (!player.userID.IsSteamId()) return null;
			var patrolHeli = turret._heliAI?.helicopterBase;
            if (patrolHeli != null && patrolHeli.net != null)
            {
				if (patrolHeli.skinID == _rrPluginID)
                {
					if (_config.RandomRaids_Enabled && _rrallPatrols.TryGetValue(patrolHeli.net.ID.Value, out var rrData) && !rrData.PlayersList.Contains(player.userID))
                        return false;
				}
                else if (_eventsList.TryGetValue(patrolHeli.net.ID.Value, out var patrolData))
                {
					if (patrolData.CanBeTargeted(player) != null)
                        return false;
				}
			}
			return null;
        }
		
		private void ShowEventOffer(BasePlayer player, EventData eventData)
		{
			DestroyUI(player, EventOfferUI);
			player.SendEffect();
			CuiHelper.AddUi(player, ReplacePlaceholders(_vanillaEventsUiOffer, null, (string)ImageLibrary?.Call("GetImage", EventOfferUI),
				string.Format(lang.GetMessage("MsgEventOfferTitle", this, player.UserIDString), new string[] { lang.GetMessage($"MsgEvent{eventData.Type}", this, player.UserIDString) }),
				string.Format(lang.GetMessage("MsgEventOfferDescription", this, player.UserIDString), new string[] { string.Format(_config.PriceFormat, eventData.Settings.Price.ToString()) }),
				$"{_commandUI} event pay {eventData.ID}"));
			_playerUI[player.userID].Add(EventOfferUI);
		}
		
		private class Configuration
        {
			[JsonProperty(PropertyName = "Chat admin command")]
            public string AdminCommand = "adminpve";
			
			[JsonProperty(PropertyName = "Chat command")]
			public string Command = "realpve";
			
			[JsonProperty(PropertyName = "Is it worth forcibly implementing PvE for a server?")]
			public bool Force_PvE = true;
			
			[JsonProperty(PropertyName = "Is it worth enabling GameTips for messages?")]
			public bool GameTips_Enabled = true;
			
			[JsonProperty(PropertyName = "Is it worth preventing the sending of 'Give' messages?")]
			public bool PreventGiveMessage = true;
			
			[JsonProperty(PropertyName = "Is it worth preventing resource gathering in someone else's building privilege area?")]
            public bool PreventResourceGathering = false;
			
			[JsonProperty(PropertyName = "Is it worth preventing the pickup of plants spawned by the server in someone else's building privilege zone?")]
            public bool PreventPickUpCollectibleEntity = false;
			
			[JsonProperty(PropertyName = "Is it worth assigning portals(Halloween and Christmas) to the first player?")]
			public bool AssignPortals = true;
			
			[JsonProperty(PropertyName = "Is it worth preventing players from handcuffing others?")]
            public bool PreventHandcuffing = true;
			
			[JsonProperty(PropertyName = "Is it worth preventing a backpack from dropping upon player death?")]
            public bool PreventBackpackDrop = true;
			
			[JsonProperty(PropertyName = "Is it worth preventing damage to the laptop of the Hackable Crate?")]
            public bool PreventLaptopAttack = true;
			
			[JsonProperty(PropertyName = "Is it worth removing the penalties for recyclers in safe zones?")]
            public bool RecyclerNoPenalties = true;
			
			[JsonProperty(PropertyName = "Which currency symbol and format will be utilized?")]
			public string PriceFormat = "${0}";
			
			[JsonProperty(PropertyName = "Vehicles - Time(in seconds) to display the marker when searching for a vehicle. A value of 0 disables the marker")]
            public float VehiclesMarkerTime = 15f;
			
			[JsonProperty(PropertyName = "Anti-Sleeper - Time in seconds after which a player will be killed if they disconnect while inside someone else's Building Privilege. Set to 0 to disable")]
			public float AntiSleeper = 1200f;

			[JsonProperty(PropertyName = "PatrolHelicopterAI - Monument Crash. If set to true, the helicopter will attempt to crash into the monument")]
			public bool PatrolHelicopterAI_MonumentCrash = false;

			[JsonProperty(PropertyName = "PatrolHelicopterAI - Use Danger Zones. If set to false, the helicopter will function as it did before the April update")]
			public bool PatrolHelicopterAI_UseDangerZones = false;

			[JsonProperty(PropertyName = "PatrolHelicopterAI - Flee Damage Percentage. A value of 1 or above will make the helicopter behave as it did before the April update")]
			public float PatrolHelicopterAI_FleeDamagePercentage = 1f;
			
			[JsonProperty(PropertyName = "Is Npc Random Raids enabled?")]
			public bool RandomRaids_Enabled = true;
			
			[JsonProperty(PropertyName = "PvP - Is friendly fire enabled by default when creating a team?")]
            public bool PvPTeamFF = false;
			
			[JsonProperty(PropertyName = "PvP - Is it worth adding map markers for PvP zones?")]
			public bool PvPMapMarkers = true;
			
			[JsonProperty(PropertyName = "PvP - Name of the map maker")]
            public string PvPMapMarkersName = "PvP Zone!";
			
			[JsonProperty(PropertyName = "PvP - Settings for the status bar")]
			public BarSettings BarPvP = null;
			
			[JsonProperty(PropertyName = "PvP - Settings for the progress status bar")]
            public ProgressBarSettings ProgressBarPvP = null;
			
			[JsonProperty(PropertyName = "Wipe ID")]
			public string WipeID = "";

			public Oxide.Core.VersionNumber Version;
		}
		
		void OnFriendAdded(string userID, string friendID) => OnFriendUpdated(userID, friendID);
		private int _defaultBeds = 15, _defaultShelters = 1, _defaultTurrets = 12;

		private bool GetLookEntity(BasePlayer player, out BaseEntity entity, float maxDistance = 10f)
		{
			entity = null;
			if (Physics.Raycast(player.eyes.HeadRay(), out RaycastHit hit, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
				entity = hit.GetEntity();
			return entity != null;
		}
		object CanAdministerVending(BasePlayer player, VendingMachine machine) => _unrestrictedLooters.Contains(player.userID) ? true : (!machine.OwnerID.IsSteamId() || IsEntityInPvP(player.userID, machine.net.ID.Value) ? null : player.TasirMumkin(machine.OwnerID));
		object CanLootEntity(BasePlayer player, BoxStorage storage) => CanLootStorage(player, storage);
		
		private void UpdatePvPBars()
        {
			if (_pvpBar == null) return;
			_pvpBar.Remove(10);
            _pvpBar.Remove(9);
            _pvpBar.Remove(8);
            if (!string.IsNullOrWhiteSpace(_config.BarPvP.Image_Sprite))
                _pvpBar.Add(10, _config.BarPvP.Image_Sprite);
            else if (!string.IsNullOrWhiteSpace(_config.BarPvP.Image_Local))
                _pvpBar.Add(9, _config.BarPvP.Image_Local);
            else
                _pvpBar.Add(8, _imgLibIsLoaded && _config.BarPvP.Image_Url.StartsWithAny(HttpScheme) ? Bar_PvP : _config.BarPvP.Image_Url);
			
			if (_pvpDelayBar != null)
				_pvpDelayBar.Clear();
			var progressBar = _config.ProgressBarPvP;
			_pvpDelayBar = new Dictionary<int, object>(_pvpBar)
			{
                { 32, progressBar.Progress_Reverse },
                { 33, progressBar.Progress_Color },
                { -33, progressBar.Progress_Transparency },
                { 34, progressBar.Progress_OffsetMin },
                { 35, progressBar.Progress_OffsetMax }
            };
			_pvpDelayBar[2] = "TimeProgressCounter";
			_pvpDelayBar[6] = progressBar.Main_Color;
			
			if (progressBar.Main_Color.StartsWith("#"))
				_pvpDelayBar[-6] = progressBar.Main_Transparency;
			else
				_pvpDelayBar.Remove(-6);
		}
				
		        private const ulong _bradleySkinId = 3074297551uL;

		private void LoadMonumentsImages()
		{
			if (_statusIsLoaded)
			{
				var imgList = new HashSet<string>();
				foreach (var monumentSettings in _monumentsConfig.MonumentsSettings.Values)
                {
                    if (!string.IsNullOrWhiteSpace(monumentSettings.Bar.Image_Local))
                        imgList.Add(monumentSettings.Bar.Image_Local);
                }
				if (imgList.Any())
					AdvancedStatus?.Call("LoadImages", imgList);
			}
			if (_imgLibIsLoaded)
			{
				var imgList = new Dictionary<string, string>();
				BarSettings barSettings;
				foreach (var kvp in _monumentsConfig.MonumentsSettings)
                {
                    barSettings = kvp.Value.Bar;
                    if (string.IsNullOrWhiteSpace(barSettings.Image_Sprite) && string.IsNullOrWhiteSpace(barSettings.Image_Local) && barSettings.Image_Url.StartsWithAny(HttpScheme))
                        imgList.Add($"{StatusBarID}{kvp.Key}", barSettings.Image_Url);
                }
				if (imgList.Any())
					ImageLibrary?.Call("ImportImageList", Name, imgList, 0uL, true);
			}
		}
		
		object CanLootEntity(BasePlayer player, ModularCarGarage garage)
		{
			if (garage.carOccupant == null)
			{
				SendMessage(player, "MsgVehicleCarGarageEmpty");
				return false;
			}
			return null;
		}
		object OnEntityTakeDamage(VehicleModuleCamper module, HitInfo info) => CanModuleTakeDamage(module, info);

		void OnPluginUnloaded(Plugin plugin)
		{
			if (plugin.Name == "ImageLibrary")
			{
				_imgLibIsLoaded = false;
				foreach (var monumentData in _monumentsList.Values)
					monumentData.UpdateBars();
				UpdatePvPBars();
				foreach (var rbData in _rbList.Values)
					rbData.UpdateBars();
			}
			else if (plugin.Name == "Economics")
				_economicsIsLoaded = false;
			else if (plugin.Name == "AdvancedStatus")
				_statusIsLoaded = false;
			else if (plugin.Name == "MonumentsWatcher")
			{
				_watcherIsLoaded = false;
				Unsubscribe(nameof(OnEntityEnteredMonument));
				Unsubscribe(nameof(OnEntityExitedMonument));
				Unsubscribe(nameof(OnPlayerEnteredMonument));
				Unsubscribe(nameof(OnPlayerExitedMonument));
				Unsubscribe(nameof(OnCargoWatcherCreated));
				Unsubscribe(nameof(OnCargoWatcherDeleted));
				Unsubscribe(nameof(OnCargoShipHarborArrived));
				Unsubscribe(nameof(OnCargoShipHarborLeave));
				Unsubscribe(nameof(OnHarborEventStart));
				Unsubscribe(nameof(OnHarborEventEnd));
				Unsubscribe(nameof(OnExcavatorResourceSet));
				Unsubscribe(nameof(OnExcavatorSuppliesRequest));
				Unsubscribe(nameof(OnExcavatorSuppliesRequested));
				Unsubscribe(nameof(CanHackCrate));
				foreach (var monumentData in _monumentsList.Values)
					monumentData.Destroy();
				_monumentsList.Clear();
				if (_statusIsLoaded)
					DestroyAllBars();
				_harborEventMonument = string.Empty;
			}
			else if (plugin.Name == "HarborEvent")
			{
				NextTick(() =>
				{
					if (!string.IsNullOrWhiteSpace(_harborEventMonument))
						OnHarborEventEnd();
				});
			}
			else if (plugin.Name == "RaidableBases")
			{
				Unsubscribe(nameof(OnPlayerEnteredRaidableBase));
				Unsubscribe(nameof(OnPlayerExitedRaidableBase));
				Unsubscribe(nameof(OnRaidableLootDestroyed));
				Unsubscribe(nameof(OnRaidableDespawnUpdate));
				Unsubscribe(nameof(OnRaidableBasePurchased));
				Unsubscribe(nameof(OnRaidableBaseStarted));
				Unsubscribe(nameof(OnRaidableBaseEnded));
				foreach (var rbData in _rbList.Values.ToList())
					rbData.Destroy();
			}
			else if (plugin.Name == "RandomRaids")
			{
				Unsubscribe(nameof(OnRandomRaidStart));
				Unsubscribe(nameof(RandomRaidEventEnd));
				Unsubscribe(nameof(OnRandomRaidRaiderSpawned));
				Unsubscribe(nameof(OnRandomRaidHeliSpawned));
				Unsubscribe(nameof(OnRandomRaidWin));
				
				Unsubscribe(nameof(OnBuildingSplit));
			}
			else if (plugin.Name == "DynamicPVP")
            {
				Unsubscribe(nameof(OnEntityEnterZone));
				Unsubscribe(nameof(OnEntityExitZone));
				Unsubscribe(nameof(OnCreateDynamicPVP));
				Unsubscribe(nameof(OnCreatedDynamicPVP));
				Unsubscribe(nameof(OnDeletedDynamicPVP));
			}
		}

		private float GetEventPriceMultiplier(string userID)
		{
			float result = float.MaxValue;
			foreach (var perm in _permissionsConfig.PermissionsList)
			{
				if (perm.Event_Multiplier < result && permission.UserHasPermission(userID, perm.Name))
					result = perm.Event_Multiplier;
			}
			return result == float.MaxValue ? _permissionsConfig.PermissionsList[0].Event_Multiplier : result;
		}
		private const string _monumentsPath = @"RealPVE\MonumentsConfig";
		
		void OnPlayerRespawned(BasePlayer player)
		{
			
			if (_respawnMessage.TryGetValue(player.userID, out var msg))
			{
				if (_config.GameTips_Enabled)
					player.SendConsoleCommand("gametip.showtoast", (int)GameTip.Styles.Blue_Long, msg, string.Empty);
				else
					player.ChatMessage(msg);
				_respawnMessage.Remove(player.userID);
			}
		}
		
		void OnEntityKill(ScientistNPC scientist)
        {
            if (scientist.skinID == _bradleySkinId)
                _eventScientistsList.Remove(scientist.net.ID);
        }
		object OnStructureRotate(BaseCombatEntity entity, BasePlayer player) => !entity.OwnerID.IsSteamId() || IsEntityInPvP(player.userID, entity.net.ID.Value) ? null : player.TasirMumkin(entity.OwnerID);
		private Dictionary<string, HashSet<string>> _pvpChangedMonuments = new Dictionary<string, HashSet<string>>();
        private readonly VersionNumber _permissionsVersion = new VersionNumber(0, 1, 1);
        private const string _vanillaEventsPath = @"RealPVE\VanillaEventsConfig";

        private CuiElementContainer GetVehicleCarPanel(string userID, VehicleData vehicleData)
        {
            string description, descriptionValue;
            bool notOwner = vehicleData.OwnerID != 0uL && !vehicleData.IsOwner(userID);
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "385 110", OffsetMax = "573 200" },
                Image = { Color = "0.35 0.35 0.35 1", Material = "assets/content/ui/ui.background.tiletex.psd" }
            }, "Overlay", _uiVehiclePanel);
            if (notOwner)
            {
                container.Add(new CuiElement
                {
                    Parent = _uiVehiclePanel,
                    Components =
                    {
                        new CuiImageComponent { Color = "0 0 0 0" },
                        new CuiRectTransformComponent { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-382 230", OffsetMax = "2 381" }
                    }
                });
            }
            container.Add(new CuiElement
            {
                Parent = _uiVehiclePanel,
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0 0" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-50 0", OffsetMax = "0 0" }
                }
            });
            string[] values = new string[5];
            values[0] = vehicleData.ID.ToString();
            values[1] = lang.GetMessage($"MsgVehicle{vehicleData.Type}", this, userID).FirstToUpper();
            values[2] = lang.GetMessage($"MsgVehicle{vehicleData.Category}", this, userID);
            values[3] = !string.IsNullOrWhiteSpace(vehicleData.RegistrationDate) ? $"{vehicleData.RegistrationDate}(UTC)" : lang.GetMessage("MsgNoDate", this, userID);
            if (notOwner)
            {
                var owner = BasePlayer.FindByID(vehicleData.OwnerID);
                values[4] = owner != null ? owner.displayName : vehicleData.OwnerID.ToString();
                description = lang.GetMessage("MsgVehicleCarDialogDescriptionNotOwner", this, userID);
                descriptionValue = string.Format(lang.GetMessage("MsgVehicleCarDialogDescriptionNotOwnerValue", this, userID), values);
            }
            else if (vehicleData.OwnerID == 0uL)
            {
                if (_economicsIsLoaded)
                {
                    var perm = GetVehiclePricePermission(userID, vehicleData.Type);
                    values[4] = $"{string.Format(_config.PriceFormat, $"{perm?.Allowed_Vehicles[vehicleData.Type].Price ?? 0}")}({string.Format(_config.PriceFormat, $"{Economics?.Call("Balance", userID) ?? 0}")})";
                }
                else
                    values[4] = lang.GetMessage("MsgFree", this, userID);
                description = lang.GetMessage("MsgVehicleCarDialogDescription", this, userID);
                descriptionValue = string.Format(lang.GetMessage("MsgVehicleCarDialogDescriptionValue", this, userID), values);
            }
            else
            {
                description = lang.GetMessage("MsgVehicleCarDialogDescriptionRegistered", this, userID);
                descriptionValue = string.Format(lang.GetMessage("MsgVehicleCarDialogDescriptionRegisteredValue", this, userID), values);
            }
            container.Add(new CuiLabel
            {
                Text =
                {
                    Text = lang.GetMessage("MsgVehicleDialogTitle", this, userID),
                    FontSize = 12,
                    Color = WhiteColor,
                    Align = TextAnchor.MiddleCenter
                },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 75", OffsetMax = "-5 92" }
            }, _uiVehiclePanel);
            container.Add(new CuiElement
            {
                Name = "Description",
                Parent = _uiVehiclePanel,
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0 0" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"5 {(notOwner ? "5" : "25")}", OffsetMax = "-5 -20" },
                    new CuiScrollViewComponent
                    {
                        ContentTransform = new CuiRectTransform { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"0 -{(11 *  description.Split('\n').Length * 1.2) - (notOwner ? 67 : 47)}", OffsetMax = "0 0" },
                        Vertical = true,
                        MovementType = ScrollRect.MovementType.Elastic,
                        ScrollSensitivity = 20f,
                        HorizontalScrollbar = null,
                        VerticalScrollbar = null
                    }
                }
            });
            container.Add(new CuiLabel
            {
                Text =
                {
                    Text = description,
                    Font = "RobotoCondensed-Regular.ttf",
                    FontSize = 11,
                    Color = WhiteColor,
                    Align = TextAnchor.MiddleLeft
                },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 0", OffsetMax = "0 0" }
            }, "Description");
            container.Add(new CuiLabel
            {
                Text =
                {
                    Text = descriptionValue,
                    Font = "RobotoCondensed-Regular.ttf",
                    FontSize = 11,
                    Color = WhiteColor,
                    Align = TextAnchor.MiddleRight
                },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "-5 0" }
            }, "Description");
            if (!notOwner)
            {
                container.Add(new CuiElement
                {
                    Name = "Button",
                    Parent = _uiVehiclePanel,
                    Components =
                    {
                        new CuiImageComponent { Color = "0.41 0.55 0.41 0.8" },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 22" }
                    }
                });
                if (vehicleData.OwnerID == 0uL)
                {
                    container.Add(new CuiLabel
                    {
                        Text =
                        {
                            Text = $"{lang.GetMessage("MsgVehicleDialogLink", this, userID)}:",
                            FontSize = 12,
                            Color = WhiteColor,
                            Align = TextAnchor.MiddleLeft
                        },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 0", OffsetMax = "-35 0" }
                    }, "Button");
                    container.Add(new CuiElement
                    {
                        Parent = "Button",
                        Components =
                        {
                            new CuiInputFieldComponent
                            {
                                FontSize = 12,
                                Align = TextAnchor.MiddleRight,
                                Color =  WhiteColor,
                                CharsLimit = 4,
                                Command = $"{_commandUI} vehicle link {vehicleData.ID}",
                                IsPassword = true
                            },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "-7 0" }
                        }
                    });
                }
                else
                {
                    container.Add(new CuiButton
                    {
                        Text =
                        {
                            Text = lang.GetMessage("MsgVehicleDialogUnLink", this, userID),
                            Font = "RobotoCondensed-Regular.ttf",
                            FontSize = 12,
                            Color = WhiteColor,
                            Align = TextAnchor.MiddleCenter
                        },
                        Button =
                        {
                            Command = $"{_commandUI} vehicle unlink {vehicleData.ID}",
                            Color = "1 0.4 0.4 0.8"
                        },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                    }, "Button");
                }

                container.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = lang.GetMessage(vehicleData.IsOwner(userID) ? "MsgVehicleDialogOwnerWarning" : "MsgVehicleDialogWarning", this, userID),
                        FontSize = 12,
                        Color = "1 0.4 0.4 0.8",
                        Align = TextAnchor.MiddleRight
                    },
                    RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-260 147", OffsetMax = "0 181" }
                }, _uiVehiclePanel);
            }
            return container;
        }

		private static bool GetVehicleData(BaseEntity vehicle, out VehicleData vehicleData)
		{
			ulong vehicleID = 0uL;
			if (vehicle is BaseVehicleModule module)
				vehicleID = module.VehicleParent()?.net.ID.Value ?? 0uL;
			else if (vehicle != null)
				vehicleID = vehicle.net.ID.Value;
			return _vehiclesList.TryGetValue(vehicleID, out vehicleData) && vehicleData != null;
		}
        private const string _vanillaEventsPathOld = @"RealPVE\_old_VanillaEventsConfig({0})";

        private void SendRBStrangerBar(BasePlayer player, RBData rbData)
        {
            if (!_statusIsLoaded) return;

            var parameters = new Dictionary<int, object>(rbData.StatusBar)
            {
                { 15, rbData.OwnerName },
                { 22, lang.GetMessage("MsgRaidableBasesBarNoAccess", this, player.UserIDString) }
            };

            AdvancedStatus?.Call(StatusCreateBar, player.userID.Get(), parameters);
        }
		object CanLootEntity(BasePlayer player, HuntingTrophy trophy) => CanLootStorage(player, trophy);
		void OnFriendRemoved(string userID, string friendID) => OnFriendUpdated(userID, friendID);
		private string _commandUI = string.Empty;
		private static bool _imgLibIsLoaded = false, _economicsIsLoaded = false, _watcherIsLoaded = false, _statusIsLoaded = false;
		private void DestroyAllBars() => AdvancedStatus?.Call(StatusDeleteAllPluginBars, Name);
		
		void OnLootEntity(BasePlayer player, StorageContainer container)
		{
			if (container.panelName != "fuelsmall" || container.GetParentEntity() is not BaseEntity parent || parent.net == null || parent is ModularCar ||
				!_vehiclesList.TryGetValue(parent.net.ID.Value, out var vehicleData)) return;
			ShowVehiclePanels(player, vehicleData);
		}

        object CanTakeCutting(BasePlayer player, GrowableEntity entity)
        {
			if (_config.PreventResourceGathering && player != null && !_unrestrictedLooters.Contains(player.userID) && !IsEntityInPvP(player.userID, entity.net.ID.Value))
			{
                object result = player.BinoMumkin();
                if (result != null)
                    SendMessage(player, "MsgCantGatherInBase");
                return result;
            }
            return null;
        }

        private void LoadPermissionsConfig()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(_permissionsPath))
            {
                try { _permissionsConfig = Interface.Oxide.DataFileSystem.ReadObject<PermissionConfig>(_permissionsPath); }
                catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
            }

            if (_permissionsConfig == null || _permissionsConfig.Version < _permissionsVersion)
            {
                if (_permissionsConfig != null)
                {
                    string path = string.Format(_permissionsPathOld, _permissionsConfig.Version.ToString());
                    PrintWarning($@"Your settings version for permissions is outdated. The config file has been updated, and your old settings have been saved in \data\{path}");
                    SavePermissionsConfig(path);
                }
                _permissionsConfig = new PermissionConfig() { Version = _permissionsVersion };
            }
			
			if (_permissionsConfig.PermissionsList == null || !_permissionsConfig.PermissionsList.Any())
                _permissionsConfig.PermissionsList = new List<PvEPermission>() { new PvEPermission("realpve.default", false, 15, 1, 12, 0f, 1f, 1f, 1, 1f, 1, 1f), new PvEPermission("realpve.vip", true, 20, 2, 15, 450f, 0.9f, 0.9f, 2, 0.9f, 5, 0.9f) };

            SavePermissionsConfig();
        }
				
		        private void Command_UI(IPlayer player, string command, string[] args)
        {
            if (args == null || args.Length < 3 || player.Object is not BasePlayer bPlayer || bPlayer == null) return;
            string replyKey = string.Empty, effectName = string.Empty;
			string[] replyArgs = new string[5];
			bool isWarning = false;
			object isPayed = null;
			if (args[0] == "vehicle")
            {
				if (ulong.TryParse(args[2], out var vehicleID) && _vehiclesList.TryGetValue(vehicleID, out var vehicleData) && BaseNetworkable.serverEntities.Find(new NetworkableId(vehicleID)) is BaseEntity entity)
                {
                    var car = entity as ModularCar;
					if (entity is BaseVehicle || entity is HotAirBalloon)
					{
						var privilege = entity.children.OfType<VehiclePrivilege>().FirstOrDefault();
						if (args[1] == "link")
						{
							if (car != null && !car.HasDriverMountPoints())
								effectName = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab";
							else if (car != null && (args.Length < 4 || !car.CarLock.IsValidLockCode(args[3])))
							{
								replyKey = "MsgVehicleDialogIncorrectPassword";
								isWarning = true;
								effectName = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab";
							}
							else
							{
								if (_economicsIsLoaded)
								{
									var vehType = vehicleData.Type;
									var perm = GetVehiclePricePermission(bPlayer.UserIDString, vehType);
									double regPrice = (perm?.Allowed_Vehicles[vehType].Price) ?? 0d;
									if (regPrice > 0d) isPayed = Economics?.Call("Withdraw", bPlayer.UserIDString, regPrice);
								}
								if (isPayed is bool && !(bool)isPayed)
								{
									replyKey = "MsgEconomicsNotEnough";
									isWarning = true;
									effectName = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab";
								}
								else if (vehicleData.AssignNewOwner(bPlayer) == null)
								{
									if (car != null)
										car.CarLock.TryAddALock(args[3], bPlayer.userID);
									else if (privilege != null)
									{
										privilege.authorizedPlayers.Clear();
										privilege.UpdateMaxAuthCapacity();
										privilege.SendNetworkUpdate();
										privilege.AddPlayer(bPlayer);
										privilege.SendNetworkUpdate();
									}
									effectName = "assets/prefabs/locks/keypad/effects/lock-code-deploy.prefab";
								}
							}
						}
						else if (args[1] == "unlink")
						{
							if (vehicleData.RemoveOwner(bPlayer) == null)
							{
								if (car != null)
								{
									if (car.CarLock != null)
									{
										car.CarLock.RemoveLock();
										foreach (var wId in car.CarLock.WhitelistPlayers.ToArray())
											car.CarLock.TryRemovePlayer(wId);
									}
								}
								else if (privilege != null)
								{
									privilege.authorizedPlayers.Clear();
									privilege.UpdateMaxAuthCapacity();
									privilege.SendNetworkUpdate();
								}
								effectName = "assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab";
							}
						}
						else if (args[1] == "rename")
						{
							if (!vehicleData.IsOwner(bPlayer.userID))
							{
								replyKey = "MsgVehicleNotOwner";
								isWarning = true;
							}
							else if (args.Length < 4 || args[3].Length > 16)
							{
								replyKey = "MsgVehicleWrongName";
								isWarning = true;
							}
							else
							{
								vehicleData.Name = args[3];
								replyKey = "MsgVehicleNewName";
								replyArgs[0] = vehicleData.Name;
							}
						}
					}
				}
            }
            else if (args[0] == "monument")
            {
                if (args[1] == "pay" && _monumentsList.TryGetValue(args[2], out var monumentData) && !monumentData.IsPvP)
                {
                    if (!monumentData.PlayersQueue.Contains(bPlayer))
                    {
                        replyKey = "MsgMonumentNotInQueue";
                        isWarning = true;
                    }
                    else
                    {
                        if (_economicsIsLoaded)
                        {
                            double lootPrice = monumentData.Settings.Price * GetMonumentPriceMultiplier(bPlayer.UserIDString);
                            if (lootPrice > 0d) isPayed = Economics?.Call("Withdraw", bPlayer.UserIDString, lootPrice);
                        }
                        if (isPayed is bool && !(bool)isPayed)
                        {
                            monumentData.OnOwnershipOfferExpired(bPlayer);
                            replyKey = "MsgEconomicsNotEnough";
                            isWarning = true;
                        }
                        else
                            monumentData.SetNewOwner(bPlayer);
                    }
                }
            }
            else if (args[0] == "event")
            {
                if (args[1] == "pay" && ulong.TryParse(args[2], out var eventID) && _eventsList.TryGetValue(eventID, out var eventData))
                {
                    if (eventData.OwnerID.IsSteamId())
                    {
                        replyKey = "MsgEventOccupied";
                        var eventOwner = BasePlayer.FindByID(eventData.OwnerID);
                        replyArgs[0] = eventOwner != null ? eventOwner.displayName : eventData.OwnerIDString;
                        isWarning = true;
                    }
                    else
                    {
                        if (_economicsIsLoaded)
                        {
                            double lootPrice = eventData.Settings.Price * GetEventPriceMultiplier(bPlayer.UserIDString);
                            if (lootPrice > 0d) isPayed = Economics?.Call("Withdraw", bPlayer.UserIDString, lootPrice);
                        }
                        if (isPayed is bool && !(bool)isPayed)
                        {
                            replyKey = "MsgEconomicsNotEnough";
                            isWarning = true;
                        }
                        else
                            eventData.SetNewOwner(bPlayer.userID);
                    }
                }
            }
            else if (args[0] == "rb")
            {
                if (args[1] == "pay" && args[2].TryParseVector3(out var pos) && _rbList.TryGetValue(pos.ToString(), out var rbData) && !rbData.IsPvP)
                {
                    if (rbData.OwnerID.IsSteamId())
                    {
                        replyKey = "MsgRaidableBasesOccupied";
                        var rbOwner = BasePlayer.FindByID(rbData.OwnerID);
                        replyArgs[0] = rbOwner != null ? rbOwner.displayName : rbData.OwnerIDString;
                        isWarning = true;
                    }
                    else if (rbData.IsPlayerInside(bPlayer.userID))
                    {
                        var limit = GetRaidableBasesLimit(bPlayer.UserIDString);
                        var total = CountRaids(bPlayer.UserIDString);
                        if (total >= limit)
                        {
                            replyKey = "MsgRaidableBasesLimit";
                            replyArgs[0] = total.ToString();
                            replyArgs[1] = limit.ToString();
                            isWarning = true;
                        }
                        else
                        {
                            double lootPrice = rbData.Settings.Price * GetRaidableBasesPriceMultiplier(bPlayer.UserIDString);
                            if (_economicsIsLoaded && lootPrice > 0d)
                                isPayed = Economics?.Call("Withdraw", bPlayer.UserIDString, lootPrice);
                            if (isPayed is bool && !(bool)isPayed)
                            {
                                replyKey = "MsgEconomicsNotEnough";
                                isWarning = true;
                            }
                            else
                            {
                                Instance.covalence.Server.Command($"{_rbsConfig.ConsoleCommand} setowner {bPlayer.UserIDString}");
                                replyKey = "MsgRaidableBasesPurchaseStart";
                                NextTick(() =>
                                {
                                    if (rbData.OwnerID != bPlayer.userID)
                                    {
                                        if (_economicsIsLoaded && lootPrice > 0d)
                                        {
                                            Economics?.Call("Deposit", bPlayer.UserIDString, lootPrice);
                                            SendMessage(player, "MsgRaidableBasesPurchaseFailed");
                                        }
                                    }
                                    else
                                        SendMessage(player, "MsgRaidableBasesPurchased", isWarning: false);
                                });
                            }
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(replyKey))
                SendMessage(player, replyKey, replyArgs, isWarning);
            if (!string.IsNullOrWhiteSpace(effectName))
                bPlayer.RunEffect(effectName);
        }
		private void OnUsedPortal(BasePlayer player, BasePortal portal)
        {
			if (portal.skinID == 0uL && portal.OwnerID == 0uL && !_unrestrictedLooters.Contains(player.userID))
				portal.OwnerID = player.userID;
		}

        private class PermissionConfig
        {
            [JsonProperty(PropertyName = "List of permissions. NOTE: The first permission will be used by default for those who do not have any permissions.")]
            public List<PvEPermission> PermissionsList = null;

            public Oxide.Core.VersionNumber Version;
        }
		
		private class VanillaEventsConfig
        {
			[JsonProperty(PropertyName = "Settings for the PatrolHelicopter events")]
            public EventSettings EventPatrolHelicopter = null;

            [JsonProperty(PropertyName = "Settings for the BradleyAPC events")]
            public EventSettings EventBradleyAPC = null;
			
			public Oxide.Core.VersionNumber Version;
		}
				
		        void Unload()
        {
			SaveData(_dataLootersPath, _unrestrictedLooters);
			SaveData(_dataPickupsPath, _pickupPlayers);
			SaveData(_dataVehiclesPath, _vehiclesList);
			SaveData(_dataTeamsPath, _teamsList);
			UnityEngine.Application.logMessageReceived += Facepunch.Output.LogHandler;
			UnityEngine.Application.logMessageReceived -= HookConflict;
			ConVar.Server.max_sleeping_bags = _defaultBeds;
			LegacyShelter.max_shelters = _defaultShelters;
			ConVar.Sentry.maxinterference = _defaultTurrets;
			PatrolHelicopterAI.monument_crash = _defaultPatrolCrash;
			PatrolHelicopterAI.use_danger_zones = _defaultPatrolDangerZone;
			PatrolHelicopterAI.flee_damage_percentage = _defaultPatrolEscapeDamage;
			foreach (var entity in BaseNetworkable.serverEntities)
			{
				if (entity is Recycler recycler)
					recycler.UpdateInSafeZone();
			}
			if (_updatesTimer != null)
                _updatesTimer.Destroy();
			foreach (var player in BasePlayer.activePlayerList)
			{
				if (!_playerUI.TryGetValue(player.userID, out var uiNames)) continue;
				foreach (var uiName in uiNames)
					CuiHelper.DestroyUi(player, uiName);
			}
			_playerUI = null;
			if (_watcherIsLoaded)
			{
				foreach (var monumentData in _monumentsList.Values)
					monumentData.Destroy();
				_monumentsList.Clear();
            }
			foreach (var markersPvP in _pvpMarkers.Values)
				markersPvP.Destroy();
			_pvpMarkers.Clear();
			if (RaidableBases != null && RaidableBases.IsLoaded)
			{
				foreach (var rbData in _rbList.Values.ToList())
					rbData.Destroy();
			}
			_pvpPlayers = null;
			_pvpEntities = null;
			_unrestrictedLooters = null;
            _pickupPlayers = null;
            _vehiclesList = null;
            _teamsList = null;
			Instance = null;
			_config = null;
			_permissionsConfig = null;
			_monumentsConfig = null;
			_vanillaEventsConfig = null;
			_vanillaEventsUiOffer = null;
			_rbsConfig = null;
			_rbsUiOffer = null;
			_beachConfig = null;
		}

		private float GetRaidableBasesPriceMultiplier(string userID)
		{
			float result = float.MaxValue;
			foreach (var perm in _permissionsConfig.PermissionsList)
			{
				if (perm.RB_Multiplier < result && permission.UserHasPermission(userID, perm.Name))
					result = perm.RB_Multiplier;
			}
			return result == float.MaxValue ? _permissionsConfig.PermissionsList[0].RB_Multiplier : result;
		}
		private Dictionary<string, RBData> _rbList = new Dictionary<string, RBData>();

		void OnCargoShipHarborLeave(CargoShip cargoShip)
		{
			if (cargoShip.skinID != 0uL) return;
			MonumentData cargoData = null;
			MonumentData harborData = null;
			string[] monuments = GetMonumentsByPos(cargoShip.transform.position);
			foreach (var monumentID in monuments)
			{
				if (!_monumentsList.ContainsKey(monumentID))
					continue;
				if (cargoData == null && monumentID.StartsWith("CargoShip"))
					cargoData = _monumentsList[monumentID];
				else if (harborData == null && monumentID.Contains("harbor"))
					harborData = _monumentsList[monumentID];
			}
			if (cargoData == null || !cargoData.IsPvP || harborData == null || harborData.Settings.IsPvP)
				return;
			harborData.RemovePvP();
		}
		private bool _defaultPatrolCrash = true, _defaultPatrolDangerZone = true;
		
		void OnPlayerEnteredMonument(string monumentID, BasePlayer player, string type, string oldMonumentID)
        {
			if (_monumentsConfig.TrackedTypes.Contains(type) && _monumentsList.TryGetValue(monumentID, out var monumentData))
            {
				MonumentData oldMonumentData = null;
				if (!string.IsNullOrWhiteSpace(oldMonumentID) && _monumentsList.TryGetValue(oldMonumentID, out oldMonumentData))
                    oldMonumentData.OnPlayerExit(player);
                monumentData.OnPlayerEnter(player);
            }
        }
        private const string _permissionsPathOld = @"RealPVE\_old_PermissionConfig({0})";
		
		object CanLootEntity(BasePlayer player, VendingMachine vending) => null;
		
		object OnHorseLead(BaseRidableAnimal animal, BasePlayer player)
        {
			if (!IsEntityInPvP(player.userID, animal.net.ID.Value) && !player.Uyda() && _vehiclesList.TryGetValue(animal.net.ID.Value, out var vehicleData))
            {
                object result = vehicleData.CanInteract(player);
                if (result != null)
                    SendMessage(player, "MsgVehicleCanNotInteract");
                return result;
            }
            return null;
		}
		void OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player) => OnTeamUpdated(player);

		private object CanLootByOwnerIDNol(BasePlayer player, BaseEntity entity)
        {
			if (!_unrestrictedLooters.Contains(player.userID) && !IsEntityInPvP(player.userID, entity.net.ID.Value))
			{
				object result = player.MumkinNol(entity.OwnerID);
				if (result != null)
					SendMessage(player, "MsgCantInteract");
				return result;
			}
			return null;
		}
		void OnPortalUsed(BasePlayer player, XmasDungeon xmas) => OnUsedPortal(player, xmas);
		
		object OnEntityEnter(TriggerMagnet trigger, ModularCar car)
        {
			if (GetVehicleData(car, out var vehicleData) && vehicleData.OwnerID != 0uL)
			{
				var driver = (trigger.GetComponentInParent<BaseMagnet>()?.entityOwner as MagnetCrane)?.GetDriver();
				if (driver != null && !vehicleData.IsOwner(driver.userID))
				{
					trigger.entityContents.Remove(car);
					return false;
				}
			}
			return null;
		}
		object CanLootEntity(BasePlayer player, SupplyDrop drop) => CanLootByOwnerIDNol(player, drop);
		
		private static void LoadData<T>(string filePath, out T result)
        {
            try { result = Interface.Oxide.DataFileSystem.ReadObject<T>(filePath); }
            catch (Exception ex) { UnityEngine.Debug.LogException(ex); result = default; }
            if (result == null)
                result = default;
        }

        object CanBeTargeted(BasePlayer target, GunTrap gunTrap)
        {
            if (target.userID.IsSteamId())
            {
				if (gunTrap.skinID == 0uL)
				{
					if (gunTrap.OwnerID.IsSteamId() && !IsEntityInPvP(target.userID, gunTrap.net.ID.Value))
						return false;
				}
				else if (gunTrap.skinID == _rbPluginID)
                {
					if (TryGetRaidBase(gunTrap.transform.position, out var rbData) && !rbData.CanInteractWithRaid(target.userID))
						return false;
				}
			}
            return null;
        }
		private string[] GetMonumentsByPos(Vector3 pos) => (string[])(MonumentsWatcher?.Call(MonumentGetMonumentsByPos, pos) ?? Array.Empty<string>());
		
		private void InitTeams()
        {
            var existedTeams = Pool.Get<List<ulong>>();
			foreach (var team in RelationshipManager.ServerInstance.teams.Values)
            {
				existedTeams.Add(team.teamID);
				if (!_teamsList.ContainsKey(team.teamID) || _teamsList[team.teamID] == null)
					_teamsList[team.teamID] = new TeamData(team.teamID, _config.PvPTeamFF);
            }
			foreach (var team in _teamsList.Values.ToList())
            {
				if (!existedTeams.Contains(team.TeamID))
					_teamsList.Remove(team.TeamID);
            }
			Pool.FreeUnmanaged(ref existedTeams);
		}
		
				private const string Bar_PvP = "RealPVE_PvP";
		
		void OnEventJoined(BasePlayer player, string zoneID) => OnPlayerEnterPVP(player, zoneID);
		private void OnTeamUpdated(BasePlayer player)
		{
			NextTick(() =>
            {
                if (_monumentsList.TryGetValue(GetPlayerMonument(player.userID), out var monumentData))
					monumentData.OnTeamUpdated(player);
                if (TryGetRaidBaseByUID(player.userID, out var rbData))
                    rbData.OnTeamUpdated(player);
            });
        }
		
		private void SendPvPDelayBar(BasePlayer player, string zoneID, double delay, double delayEnd)
        {
			if (!_statusIsLoaded) return;
			
			bool isCustomZone = false;
			Dictionary<int, object> bar;
			if (_monumentsList.TryGetValue(zoneID, out var monumentData))
				bar = monumentData.StatusProgressBar;
			else if (_rbList.TryGetValue(zoneID, out var rbData))
				bar = rbData.StatusProgressBar;
			else
			{
				bar = _pvpDelayBar;
				isCustomZone = true;
			}
			
			var parameters = new Dictionary<int, object>(bar)
            {
                { 15, lang.GetMessage("MsgPvPDelayBar", this, player.UserIDString) },
                { 28, _unixSeconds },
                { 29, delayEnd }
			};
			if (isCustomZone)
				parameters[0] = zoneID;
			
			AdvancedStatus?.Call(StatusCreateBar, player.userID.Get(), parameters);
			
			player.SendEffect();
			SendMessage(player, "MsgPvPDelay", new string[] { delay.ToString() });
		}
		private readonly VersionNumber _rbsVersion = new VersionNumber(0, 1, 1);
		void OnEntitySpawned(StabilityEntity stability) => UpdateBuildingBlock(stability);
		private static void SaveData<T>(string path, T obj) => Interface.Oxide.DataFileSystem.WriteObject(path, obj);
		object CanLock(BasePlayer player, BaseLock baseLock) => baseLock.OwnerID.IsSteamId() && (baseLock.net == null || !IsEntityInPvP(player.userID, baseLock.net.ID.Value)) ? player.TasirMumkin(baseLock.OwnerID) : null;

		private float GetHackableCrateSkip(string userID)
		{
			float result = float.MinValue;
			foreach (var perm in _permissionsConfig.PermissionsList)
			{
				if (perm.HackableCrateSkip > result && permission.UserHasPermission(userID, perm.Name))
					result = perm.HackableCrateSkip;
			}
			return result == float.MinValue ? _permissionsConfig.PermissionsList[0].HackableCrateSkip : result;
		}
		private const string _monumentsUiOfferPath = @"RealPVE\UI\MonumentsOffer";
		
		private static bool IsPlayerInPvP(ulong a, ulong b) => _pvpPlayers.ContainsKey(a) && _pvpPlayers.ContainsKey(b);
		private const string _rbsPath = @"RealPVE\RaidableBasesConfig";
		object OnRackedWeaponLoad(Item item, ItemDefinition itemDefinition, BasePlayer player, WeaponRack rack) => CanLootWeaponRack(player, rack);
		
		private void SendMessage(ulong userID, string replyKey, string[] replyArgs = null, bool isWarning = true) => SendMessage(covalence.Players.FindPlayerById(userID.ToString()), replyKey, replyArgs, isWarning);
		
		void OnLootEntityEnd(BasePlayer player, ModularCarGarage garage) => DestroyVehiclePanels(player);
		
		object OnPlayerPveDamage(BasePlayer attacker, HitInfo info, BuildingBlock block)
        {
			if (!IsEntityInPvP(attacker.userID, block.net.ID.Value))
			{
				if (block.OwnerID.IsSteamId())
                {
                    if (!attacker.userID.IsSteamId())
                    {
                        if (attacker.skinID == _rrPluginID)
                        {
                            if (_config.RandomRaids_Enabled && _rrAllRaiders.TryGetValue(attacker.net.ID.Value, out var rrData) && !rrData.PlayersList.Contains(block.OwnerID))
                                return false;
                        }
                    }
                    else if (!UrishMumkin(attacker, block.OwnerID))
                        return false;
                }
                else if (attacker.userID.IsSteamId())
                {
                    if (TryGetRaidBase(block.transform.position, out var rbData))
                    {
                        if (!rbData.CanInteractWithRaid(attacker.userID))
                            return false;
                    }
                }
			}
			
			Urish(block, info);
			return false;
		}
		
		private object CanPurchaseVehicle(BasePlayer player, VehicleType type, NPCTalking npcTalking)
		{
			object result = false;
			string replyKey;
			string[] replyArgs = new string[5];
			bool isWarning = true;
			var perm = GetVehicleLimitPermission(player.UserIDString, type);
			if (perm != null && perm.Allowed_Vehicles.TryGetValue(type, out var limit))
			{
				int totalCars = CountVehiclesByType(player.userID, type);
				if (limit.Limit == 0 || (limit.Limit > 0 && totalCars >= limit.Limit))
					replyKey = "MsgVehicleLimit";
				else
				{
					object isPayed = null;
					if (_economicsIsLoaded)
					{
						var permPrice = GetVehiclePricePermission(player.UserIDString, type);
						double regCost = (permPrice?.Allowed_Vehicles[type].Price) ?? 0d;
						isPayed = Economics?.Call("Withdraw", player.userID, regCost);
					}
					if (isPayed is bool && !(bool)isPayed)
					{
						replyKey = "MsgEconomicsNotEnough";
						player.RunEffect();
					}
					else
					{
						result = null;
						replyKey = "MsgVehicleLinked";
						isWarning = false;
					}
				}
				replyArgs[0] = lang.GetMessage($"MsgVehicle{type}", Instance, player.UserIDString);
				replyArgs[1] = result == null ? $"{totalCars++}" : totalCars.ToString();
				replyArgs[2] = limit.Limit < 0 ? "∞" : limit.Limit.ToString();
			}
			else
				replyKey = "MsgVehicleNoPermissions";

			if (!string.IsNullOrWhiteSpace(replyKey))
				SendMessage(player, replyKey, replyArgs, isWarning);
			if (result != null)
				npcTalking.ForceEndConversation(player);
			return result;
		}
		
		object OnInterferenceUpdate(AutoTurret turret)
		{
			if (!turret.OwnerID.IsSteamId()) return null;
			int num = 0;
			foreach (var nearbyTurret in turret.nearbyTurrets)
			{
				if (!nearbyTurret.isClient && nearbyTurret.IsValid() && nearbyTurret.gameObject.activeSelf && !nearbyTurret.EqualNetID(turret.net.ID) && nearbyTurret.IsOn() && !nearbyTurret.HasInterference())
					num++;
			}
			turret.SetFlag(BaseEntity.Flags.OnFire, num >= GetTurretsLimit(turret.OwnerID.ToString()));
			return false;
		}
		
		private string _rbsUiOffer;
		
		void OnRandomRaidWin(SupplyDrop drop, List<ulong> winners)
		{
			if (!winners.Any()) return;
			ulong ownerID = winners[0];
			NextTick(() => { drop.OwnerID = ownerID; });
		}

		private float GetMonumentPriceMultiplier(string userID)
		{
			float result = float.MaxValue;
			foreach (var perm in _permissionsConfig.PermissionsList)
			{
				if (perm.Monument_Multiplier < result && permission.UserHasPermission(userID, perm.Name))
					result = perm.Monument_Multiplier;
			}
			return result == float.MaxValue ? _permissionsConfig.PermissionsList[0].Monument_Multiplier : result;
		}

		private int GetTurretsLimit(string userID)
		{
			int result = int.MinValue;
			foreach (var perm in _permissionsConfig.PermissionsList)
			{
				if (perm.Turrets > result && permission.UserHasPermission(userID, perm.Name))
					result = perm.Turrets;
			}
			return result == int.MinValue ? _permissionsConfig.PermissionsList[0].Turrets : result;
		}
        private void SavePermissionsConfig(string path = _permissionsPath) => Interface.Oxide.DataFileSystem.WriteObject(path, _permissionsConfig);
		
		object OnEntityTakeDamage(BradleyAPC bradley, HitInfo info)
        {
			if (info == null || info.Initiator is not BasePlayer attacker || attacker == null || !attacker.userID.IsSteamId()) return null;
            if (bradley.skinID != 0uL) {}
			else if (_eventsList.TryGetValue(bradley.net.ID.Value, out var bradleyData))
            {
                if (bradleyData.OwnerID == 0uL)
                {
                    if (!_economicsIsLoaded || bradleyData.Settings.Price <= 0d || (bradleyData.Settings.Price * GetEventPriceMultiplier(attacker.UserIDString)) <= 0d)
                        bradleyData.SetNewOwner(attacker.userID);
                    else if (_playerUI.TryGetValue(attacker.userID, out var uiList) && !uiList.Contains(EventOfferUI))
                    {
                        ShowEventOffer(attacker, bradleyData);
                        timer.Once(bradleyData.Settings.OfferTime, () => { DestroyUI(attacker, EventOfferUI); });
					}
                    goto cancel;
                }
                else if (!bradleyData.CanBeAttackedBy(attacker))
                {
					SendMessage(attacker, "MsgEventOccupied", new string[] { lang.GetMessage("MsgEventBradleyAPC", this, attacker.UserIDString), bradleyData.OwnerName });
                    goto cancel;
                }
            }
            return null;
		
		cancel:
            info.Initiator = null;
            info.damageTypes.Clear();
            return null;
        }
		
                private HashSet<string> _dynamicPvPs;

        private void SendRaidableBasesLootBar(BasePlayer player, RBData rbData)
        {
            if (!_statusIsLoaded) return;

            var barSettings = rbData.Settings.Bar;
            var parameters = new Dictionary<int, object>(rbData.StatusBar)
            {
                { 15, lang.GetMessage(RBTextLootRemaining, this, player.UserIDString) },
                { 22, rbData.LootRemain > 0 ? rbData.LootRemain.ToString() : lang.GetMessage(RBTextLootCompleted, this, player.UserIDString) },
                { 29, _unixSeconds + 5 }
            };
            parameters[0] = RBLootUI;
            parameters[2] = BarTimed;
            parameters[4] = barSettings.Order + 1;

            AdvancedStatus?.Call(StatusCreateBar, player.userID.Get(), parameters);
        }

		object CanLootEntity(BasePlayer player, MixingTable table) => CanLootStorage(player, table);
		private Dictionary<ulong, EventData> _eventsList = new Dictionary<ulong, EventData>();

		object CanRenameBed(BasePlayer player, SleepingBag bed, string bedName) => !bed.deployerUserID.IsSteamId() || IsEntityInPvP(player.userID, bed.net.ID.Value) ? null : player.TasirMumkin(bed.deployerUserID);
		
		public class MonumentSettings
        {
			[JsonProperty(PropertyName = "Type(This parameter is just a hint. Changes won’t have any effect)")]
			public string Type { get; set; } = string.Empty;

			[JsonProperty(PropertyName = "Time in seconds(1-15) given to respond for purchasing monument looting")]
			public float OfferTime { get; set; } = 5f;

			public bool ShowSuffix { get; set; } = true;
			public bool Broadcast { get; set; } = true;

			[JsonProperty(PropertyName = "PvP - Is PvP enabled at this monument? If so, players will be able to kill each other, and loot will be publicly accessible")]
			public bool IsPvP { get; set; } = false;

			[JsonProperty(PropertyName = "PvP - Sets the delay in seconds that a player remains in PvP mode after leaving a PvP monument. 0 disables the delay")]
			public float PvPDelay { get; set; } = 10f;

			[JsonProperty(PropertyName = "PvP - Is it worth adding map markers for monuments if they are PvP zones?")]
			public bool PvPMapMarkers { get; set; } = true;

			public int LootingTime { get; set; } = 900;
			public double Price { get; set; } = 0d;

			[JsonProperty(PropertyName = "Is it worth using a progress bar for bars with a counter?")]
			public bool UseProgress { get; set; } = true;
			
			[JsonProperty(PropertyName = "Settings for the status bar")]
			public BarSettings Bar { get; set; }
			
			[JsonProperty(PropertyName = "Settings for the progress status bar")]
			public ProgressBarSettings ProgressBar { get; set; }
			
			public MonumentSettings() {}
			public MonumentSettings(string monumentID, string type)
			{
				Type = type;
				Bar = new BarSettings();
				ProgressBar = new ProgressBarSettings();
				switch (monumentID)
                {
					case "CargoShip":
						ShowSuffix = false;
						IsPvP = true;
						LootingTime = 3600;
						Price = 50d;
						Bar.Image_Url = "https://i.imgur.com/6FYEEqJ.png";
						Bar.Image_Local = "RealPVE_CargoShip";
						break;
					case "airfield_1":
						LootingTime = 1200;
						Price = 20d;
						Bar.Image_Url = "https://i.imgur.com/nMvE8pS.png";
						Bar.Image_Local = "RealPVE_airfield_1";
						break;
					case "arctic_research_base_a":
						LootingTime = 1200;
						Price = 20d;
						Bar.Image_Url = "https://i.imgur.com/kerlYFh.png";
						Bar.Image_Local = "RealPVE_arctic_research_base_a";
						break;
					case "bandit_town":
						Bar.Image_Url = "https://i.imgur.com/CwjNgXf.png";
						Bar.Image_Local = "RealPVE_bandit_town";
						break;
					case "compound":
						Bar.Image_Url = "https://i.imgur.com/KnGihg3.png";
						Bar.Image_Local = "RealPVE_compound";
						break;
					case var id when id.Contains("desert_military_base_"):
						LootingTime = 1200;
						Price = 20d;
						Bar.Image_Url = "https://i.imgur.com/F4fkg2W.png";
						Bar.Image_Local = "RealPVE_desert_military_base";
						break;
					case "excavator_1":
						LootingTime = 1800;
						Price = 15d;
						Bar.Image_Url = "https://i.imgur.com/3drbedg.png";
						Bar.Image_Local = "RealPVE_excavator_1";
						break;
					case "ferry_terminal_1":
						Price = 15d;
						Bar.Image_Url = "https://i.imgur.com/awUrIwA.png";
						Bar.Image_Local = "RealPVE_ferry_terminal_1";
						break;
					case var id when id.Contains("gas_station_1"):
						Broadcast = false;
						Bar.Image_Url = "https://i.imgur.com/aaSmHZE.png";
						Bar.Image_Local = "RealPVE_gas_station_1";
						break;
					case "harbor_1":
						LootingTime = 1800;
						Price = 15d;
						Bar.Image_Url = "https://i.imgur.com/oPZOaRC.png";
						Bar.Image_Local = "RealPVE_harbor_1";
						break;
					case "harbor_2":
						LootingTime = 1200;
						Price = 10d;
						Bar.Image_Url = "https://i.imgur.com/mc6rDqV.png";
						Bar.Image_Local = "RealPVE_harbor_2";
						break;
					case "junkyard_1":
						Price = 15d;
						Bar.Image_Url = "https://i.imgur.com/yvj6Yxj.png";
                        Bar.Image_Local = "RealPVE_junkyard_1";
                        break;
					case "launch_site_1":
						IsPvP = true;
						LootingTime = 1800;
						Price = 25d;
						Bar.Image_Url = "https://i.imgur.com/AEzabIG.png";
						Bar.Image_Local = "RealPVE_launch_site_1";
						break;
					case var id when id.Contains("lighthouse"):
						Broadcast = false;
						Bar.Image_Url = "https://i.imgur.com/YFEo2kX.png";
						Bar.Image_Local = "RealPVE_lighthouse";
						break;
					case "military_tunnel_1":
						LootingTime = 1800;
						Price = 25d;
						Bar.Image_Url = "https://i.imgur.com/71gSdrf.png";
						Bar.Image_Local = "RealPVE_military_tunnel_1";
						break;
					case "nuclear_missile_silo":
						LootingTime = 1200;
						Price = 20d;
						Bar.Image_Url = "https://i.imgur.com/QBW2i7O.png";
						Bar.Image_Local = "RealPVE_nuclear_missile_silo";
						break;
					case "oilrig_1":
                        LootingTime = 1800;
                        Price = 25d;
                        Bar.Image_Url = "https://i.imgur.com/iqWG4dk.png";
                        Bar.Image_Local = "RealPVE_oilrig_1";
                        break;
					case "oilrig_2":
						LootingTime = 1800;
						Price = 20d;
						Bar.Image_Url = "https://i.imgur.com/sZzZHHi.png";
						Bar.Image_Local = "RealPVE_oilrig_2";
						break;
					case "powerplant_1":
						LootingTime = 1200;
						Price = 20d;
						Bar.Image_Url = "https://i.imgur.com/PP1qXmZ.png";
						Bar.Image_Local = "RealPVE_powerplant_1";
						break;
					case "radtown_1":
                        Price = 15d;
						Bar.Image_Url = "https://i.imgur.com/HyXnrbu.png";
						Bar.Image_Local = "RealPVE_radtown_1";
						break;
					case "radtown_small_3":
						Price = 15d;
						Bar.Image_Url = "https://i.imgur.com/GHvhPNc.png";
						Bar.Image_Local = "RealPVE_radtown_small_3";
						break;
					case "satellite_dish":
						Price = 15d;
						Bar.Image_Url = "https://i.imgur.com/kJxFYH9.png";
						Bar.Image_Local = "RealPVE_satellite_dish";
						break;
					case "sphere_tank":
						Price = 20d;
						Bar.Image_Url = "https://i.imgur.com/V2b9pSm.png";
						Bar.Image_Local = "RealPVE_sphere_tank";
						break;
					case "stables_a":
						Bar.Image_Url = "https://i.imgur.com/D3aG1Tm.png";
						Bar.Image_Local = "RealPVE_stables_a";
						break;
					case "stables_b":
						Bar.Image_Url = "https://i.imgur.com/YbGhH89.png";
						Bar.Image_Local = "RealPVE_stables_b";
						break;
					case var id when id.Contains("station-"):
						Price = 10d;
						Bar.Image_Url = "https://i.imgur.com/33snptw.png";
						Bar.Image_Local = "RealPVE_Station";
						break;
					case var id when id.Contains("supermarket_1"):
						Broadcast = false;
						Bar.Image_Url = "https://i.imgur.com/160Wsti.png";
						Bar.Image_Local = "RealPVE_supermarket_1";
						break;
					case "trainyard_1":
						LootingTime = 1200;
						Price = 20d;
						Bar.Image_Url = "https://i.imgur.com/6QeCaNP.png";
						Bar.Image_Local = "RealPVE_trainyard_1";
						break;
					case var id when id.Contains("underwater_lab_"):
						LootingTime = 1200;
						Price = 20d;
						Bar.Image_Url = "https://i.imgur.com/4nEZryz.png";
						Bar.Image_Local = "RealPVE_underwater_lab";
						break;
					case var id when id.Contains("warehouse"):
						Broadcast = false;
						Bar.Image_Url = "https://i.imgur.com/8rEzWNP.png";
						Bar.Image_Local = "RealPVE_warehouse";
						break;
					case "water_treatment_plant_1":
						Price = 15d;
						Bar.Image_Url = "https://i.imgur.com/jmE44e8.png";
						Bar.Image_Local = "RealPVE_water_treatment_plant_1";
						break;
					default:
						Broadcast = false;
						break;
				}
			}
		}
        private const string _vanillaEventsUiOfferPath = @"RealPVE\UI\VanillaEventsOffer";

        object CanBradleyApcTarget(BradleyAPC bradley, BasePlayer player)
            => _eventsList.TryGetValue(bradley.net.ID.Value, out var patrolData) ? patrolData.CanBeTargeted(player) : null;
		protected override void LoadDefaultConfig() => _config = new Configuration() { Version = Version };

		object CanLootEntity(BasePlayer player, ResourceExtractorFuelStorage container)
        {
			if (_unrestrictedLooters.Contains(player.userID)) return AdminOpenLoot(player, container);
			if (IsEntityInPvP(player.userID, container.net.ID.Value)) return null;
			object result = null;
			var parent = container.GetParentEntity() as MiningQuarry;
			if (parent != null && !parent.isStatic && parent.OwnerID.IsSteamId())
				result = player.TasirMumkin(parent.OwnerID);
			else if (_monumentsList.TryGetValue(GetPlayerMonument(player.userID), out var monumentData))
				result = monumentData.CanLoot(player);
			if (result != null)
				SendMessage(player, "MsgCantInteract");
			return result;
		}

		public class VehicleProperties
		{
			public int Limit { get; set; }
			private double _price;
			public double Price
			{
				get { return _price; }
				set { _price = Math.Round(value, 2); }
			}
		}

		private bool IsMonumentCargoValid(string monumentID)
		{
			string[] parts = monumentID.Split('_');
			if (parts.Length > 0 && ulong.TryParse(parts[^1], out ulong cargoID) && BaseNetworkable.serverEntities.Find(new NetworkableId(cargoID)) is CargoShip cargoShip &&
				cargoShip != null && cargoShip.skinID != 0uL)
				return false;
			return true;
		}
        
        		void OnDefaultItemsReceive(PlayerInventory inventory)
        {
			if (inventory.baseEntity is BasePlayer player && !player.IsInTutorial && Interface.CallHook(Hooks_CanRedeemKit, player) == null)
				NextTick(() => GiveDefaultItems(player));
		}

		void OnLootEntity(BasePlayer player, ItemBasedFlowRestrictor restrictor)
		{
			if (!_unrestrictedLooters.Contains(player.userID) && _monumentsList.TryGetValue(GetPlayerMonument(player.userID), out var monumentData) && monumentData.CanLoot(player) != null)
			{
				ulong lastLooter = 0uL;
				ILootableEntity lootableEntity = restrictor as ILootableEntity;
				if (lootableEntity != null)
					lastLooter = lootableEntity.LastLootedBy;
				NextTick(() =>
				{
					restrictor.SetFlag(BaseEntity.Flags.Open, b: false);
					player.inventory.loot.RemoveContainer(restrictor.inventory);
					player.inventory.loot.SendImmediate();
					if (lootableEntity != null)
						lootableEntity.LastLootedBy = lastLooter;
					restrictor.SendNetworkUpdate();
					SendMessage(player, "MsgCantInteract");
				});
			}
		}
		
		private object CanInteractWithSeat(BasePlayer player, BaseMountable mount)
        {
			if (IsEntityInPvP(player.userID, mount.net.ID.Value)) return null;
			object result = null;
			string replyKey = "MsgVehicleCanNotInteract";
			if (mount.mountPose != PlayerModel.MountPoses.Sit_Crane)
			{
                var parent = mount.GetParentEntity();
                if (GetVehicleData(parent, out var vehicleData))
                {
                    result = vehicleData.CanInteract(player, false) == null ||
                        (parent is not VehicleModuleCamper && _allowSitVehicles.Contains(vehicleData.Type) && !_driverSit.Contains((int)mount.mountPose)) ? null : false;
                }
            }
			else if (_monumentsList.TryGetValue(GetPlayerMonument(player.userID), out var monumentData))
			{
				result = monumentData.CanLoot(player);
				replyKey = "MsgCantInteract";
			}
			
			if (result != null)
				SendMessage(player, replyKey);
			return result;
		}

		void OnCupboardDeauthorize(BuildingPrivlidge privilege, BasePlayer player)
		{
			if (_config.RandomRaids_Enabled && _randomRaidsList.TryGetValue(privilege.transform.position.ToString(), out var rrData))
			{
				NextTick(() =>
				{
					if (player != null && (privilege == null || !privilege.IsAuthed(player)))
					{
						_rrAllPlayers.Remove(player.userID);
						rrData.PlayersList.Remove(player.userID);
					}
				});
			}
		}
		void OnEventLeave(BasePlayer player, string zoneID) => OnPlayerExitPVP(player, zoneID);
		private string _harborEventMonument = string.Empty;
		
		object CanUnlock(BasePlayer player, ModularCarCodeLock carCodeLock, string password)
		{
			if (GetVehicleData(carCodeLock.owner, out var vehicleData))
            {
                object result = vehicleData.CanLoot(player);
                if (result != null)
                    SendMessage(player, "MsgVehicleCanNotInteract");
				return result;
            }
            return null;
		}
		
		void OnRaidableBaseEnded(Vector3 pos)
		{
			if (_rbList.TryGetValue(pos.ToString(), out var rbData) && rbData != null)
				rbData.Destroy();
			else
				_rbList.Remove(pos.ToString());
		}

		object OnConveyorFiltersChange(IOEntity entity, BasePlayer player)
        {
			if (_unrestrictedLooters.Contains(player.userID)) return null;
			object result = null;
			if (entity.OwnerID.IsSteamId())
				result = player.TasirMumkin(entity.OwnerID);
			else if (_monumentsList.TryGetValue(GetEntityMonument(entity), out var monumentData))
				result = monumentData.CanLoot(player);
			if (result != null)
				SendMessage(player, "MsgCantInteract");
			return result;
		}

		void OnPlayerConnected(BasePlayer player)
		{
			_playerUI[player.userID] = new HashSet<string>();
			player.CancelInvoke(Str_ScheduledDeath);
			if (_monumentsList.TryGetValue(GetPlayerMonument(player.userID), out var monumentData))
				monumentData.OnPlayerEnter(player);
			if (_pvpPlayers.TryGetValue(player.userID, out var playerPvP) && playerPvP.ActiveZones.Any())
            {
				SendPvPBar(player, playerPvP.ActiveZones[^1]);
				player.SendEffect();
				SendMessage(player, "MsgPvPEnter");
			}
		}
		
		object OnEntityTakeDamage(BasePlayer victim, HitInfo info)
        {
			if (info == null || info.Initiator is not BasePlayer attacker || attacker == null) return null;
			if (ConVar.Server.pve)
			{
				if (victim.userID.IsSteamId())
				{
					if (victim.IsGod()) return false;
					if (attacker.userID.IsSteamId() && !IsPlayerInPvP(attacker.userID, victim.userID) && !UrishMumkin(attacker, victim.userID))
						return false;
					if (info.PointStart != Vector3.zero && info.damageTypes.Total() >= 0f)
                    {
                        int arg = (int)info.damageTypes.GetMajorityDamageType();
                        if (info.Weapon != null && info.damageTypes.Has(DamageType.Bullet))
                        {
							var component = info.Weapon.GetComponent<BaseProjectile>();
							if (component != null && component.IsSilenced())
								arg = 12;
						}
						victim.ClientRPC(RpcTarget.PlayerAndSpectators("DirectionalDamage", victim), info.PointStart, arg, Mathf.CeilToInt(info.damageTypes.Total()));
					}
				}
				Urish(victim, info);
				return false;
			}
			else if (victim.userID.IsSteamId() && attacker.userID.IsSteamId() && !IsPlayerInPvP(attacker.userID, victim.userID) && !UrishMumkin(attacker, victim.userID))
			{
				info.Initiator = null;
				info.damageTypes.Clear();
			}
			return null;
		}
		
		void OnExplosiveThrown(BasePlayer player, BaseEntity entity, ThrownWeapon item) => entity.OwnerID = player.userID;
		
		void OnEntitySpawned(PatrolHelicopter patrol)
        {
			if (!_vanillaEventsConfig.EventPatrolHelicopter.IsEnabled) return;
			NextTick(() =>
			{
				if (patrol.skinID == 0uL)
					_eventsList[patrol.net.ID.Value] = new EventData(patrol.net.ID.Value, EventType.PatrolHelicopter, _vanillaEventsConfig.EventPatrolHelicopter); 
			});
        }
		
		private static MonumentConfig _monumentsConfig;
		
		private int CountRaids(string targetID)
		{
			int result = 0;
			foreach (var rbData in _rbList.Values)
			{
				if (rbData.OwnerIDString == targetID)
					result++;
			}
			return result;
		}
		private Dictionary<NetworkableId, EventData> _eventScientistsList = new Dictionary<NetworkableId, EventData>();
		
		private void InitVanillaEvents()
        {
			bool checkPatrol = _vanillaEventsConfig.EventPatrolHelicopter.IsEnabled;
			bool checkBradley = _vanillaEventsConfig.EventBradleyAPC.IsEnabled;
			foreach (var entity in BaseNetworkable.serverEntities)
			{
				if (entity is PatrolHelicopter patrol)
                {
					if (checkPatrol && patrol.skinID == 0uL)
						_eventsList[patrol.net.ID.Value] = new EventData(patrol.net.ID.Value, EventType.PatrolHelicopter, _vanillaEventsConfig.EventPatrolHelicopter);
				}
				else if (entity is BradleyAPC bradley)
				{
                    if (checkBradley && bradley.skinID == 0uL)
                        _eventsList[bradley.net.ID.Value] = new EventData(bradley.net.ID.Value, EventType.BradleyAPC, _vanillaEventsConfig.EventBradleyAPC);
                }
			}
			if (checkPatrol)
			{
				PatrolHelicopterAI.monument_crash = _config.PatrolHelicopterAI_MonumentCrash;
				PatrolHelicopterAI.use_danger_zones = _config.PatrolHelicopterAI_UseDangerZones;
				PatrolHelicopterAI.flee_damage_percentage = _config.PatrolHelicopterAI_FleeDamagePercentage;
			}
			if (checkBradley)
			{
				Subscribe(nameof(CanBradleyApcTarget));
				Subscribe(nameof(OnScientistInitialized));
				Subscribe(nameof(OnScientistRecalled));
			}
		}
		object CanDestroyLock(BasePlayer player, ModularCar modularCar, BaseVehicleModule carModule) => false;
		
		public class VehicleData
		{
			public ulong ID { get; set; }
			public string Name { get; set; } = "Unnamed Vehicle";
			public VehicleType Type { get; set; }
			public VehicleCategory Category { get; set; }
			public ulong OwnerID { get; set; }
			public string RegistrationDate { get; set; } = string.Empty;
			
			public VehicleData() {}
			public VehicleData(ulong id, VehicleType type, ulong ownerID = 0uL)
			{
				ID = id;
				Type = type;
				Category = GetCategory();
				if (ownerID != 0uL)
				{
					OwnerID = ownerID;
					RegistrationDate = DateTime.UtcNow.ToString(TimeFormat);
				}
			}

			public object AssignNewOwner(ulong userID) => AssignNewOwner(BasePlayer.FindByID(userID));
			public object AssignNewOwner(BasePlayer player)
			{
				object result = false;
				string replyKey;
				string[] replyArgs = new string[5];
				bool isWarning = true;
				if (OwnerID == 0uL || IsOwner(player.userID))
				{
					var perm = Instance?.GetVehicleLimitPermission(player.UserIDString, Type);
					if (perm != null && perm.Allowed_Vehicles.TryGetValue(Type, out var limit))
					{
						int totalCars = Instance?.CountVehiclesByType(player.userID, Type) ?? 0;
						if (limit.Limit == 0 || (OwnerID == 0 && limit.Limit > 0 && totalCars >= limit.Limit))
						{
							replyKey = "MsgVehicleLimit";
							player.RunEffect();
						}
						else
						{
							if (OwnerID == 0)
								totalCars++;
							OwnerID = player.userID;
							RegistrationDate = DateTime.UtcNow.ToString(TimeFormat);
							result = null;
							replyKey = "MsgVehicleLinked";
							isWarning = false;
							ShowButtons(player);
						}
						replyArgs[0] = Instance?.lang.GetMessage($"MsgVehicle{Type}", Instance, player.UserIDString) ?? $"MsgVehicle{Type}";
						replyArgs[1] = totalCars.ToString();
						replyArgs[2] = limit.Limit < 0 ? "∞" : limit.Limit.ToString();
					}
					else
						replyKey = "MsgVehicleNoPermissions";
				}
				else
					replyKey = "MsgVehicleNotOwner";

				Instance?.SendMessage(player, replyKey, replyArgs, isWarning);
				return result;
			}
			
			public object RemoveOwner(ulong userID, bool showButtons = true) => RemoveOwner(BasePlayer.FindByID(userID), showButtons);
			public object RemoveOwner(BasePlayer player, bool showButtons = true)
			{
				if (IsOwner(player.userID))
				{
					RemoveOwnerServerSide(player, showButtons);
					return null;
				}
				Instance?.SendMessage(player, "MsgVehicleNotOwner");
				return false;
			}
			
			public void RemoveOwnerServerSide(BasePlayer player = null, bool showButtons = true)
            {
				if (OwnerID == 0uL) return;
				if (player == null)
					player = BasePlayer.FindByID(OwnerID);
				OwnerID = 0uL;
				RegistrationDate = string.Empty;
				if (player != null)
				{
					if (showButtons)
						ShowButtons(player);
					Instance?.SendMessage(player, "MsgVehicleUnLinked", new string[] { Instance?.lang.GetMessage($"MsgVehicle{Type}", Instance, player.UserIDString) ?? $"MsgVehicle{Type}" }, false);
				}
			}
			
			public bool IsOwner(string userID) => OwnerID.ToString() == userID;
			public bool IsOwner(ulong userID) => OwnerID == userID;
			
			public bool CanBeNewOwner(BasePlayer player)
			{
				if (OwnerID == 0uL)
				{
					var perm = Instance?.GetVehicleLimitPermission(player.UserIDString, Type);
					if (perm != null && perm.Allowed_Vehicles.TryGetValue(Type, out var limit) && limit.Limit != 0)
					{
						if (limit.Limit < 0 || (Instance?.CountVehiclesByType(player.userID, Type) ?? 0) < limit.Limit)
							return true;
					}
				}
				return false;
			}
			
			public object CanInteract(BasePlayer player, bool sendMsg = true) => CanPerformAction(player, true, sendMsg);
			public object CanLoot(BasePlayer player) => CanPerformAction(player);
			private object CanPerformAction(BasePlayer player, bool checkFriends = false, bool sendMsg = true)
			{
				if (OwnerID == 0uL || player.userID == OwnerID || (player.Team != null && player.Team.members.Contains(OwnerID)) || (checkFriends && Instance.Friends != null && Instance.IsFriend(player.UserIDString, OwnerID.ToString())))
					return null;
				if (sendMsg) Instance?.SendMessage(player, "MsgVehicleCanNotInteract");
				return false;
			}
			
			public void OnDestroy()
			{
				if (OwnerID != 0 && BasePlayer.FindByID(OwnerID) is BasePlayer owner)
					Instance?.SendMessage(owner, "MsgVehicleDestroyed", new string[] { ID.ToString(), Type.ToString() });
				_vehiclesList.Remove(ID);
			}

			private void ShowButtons(BasePlayer player)
			{
				if (!_playerUI.ContainsKey(player.userID)) return;
				if (Type == VehicleType.Horse)
                    Instance?.ShowVehiclePanels(player, this);
                else if (Type == VehicleType.Car)
                    Instance?.ShowVehiclePanels(player, this);
                else
                    Instance?.ShowVehiclePanels(player, this);
            }

			private VehicleCategory GetCategory()
			{
				switch (Type)
				{
					case VehicleType.Horse:
					case VehicleType.Bike:
					case VehicleType.MotorBike:
					case VehicleType.Car:
						return VehicleCategory.LandVehicle;
					case VehicleType.Balloon:
					case VehicleType.Minicopter:
					case VehicleType.TransportHeli:
					case VehicleType.AttackHeli:
						return VehicleCategory.AirVehicle;
					case VehicleType.RowBoat:
					case VehicleType.RHIB:
					case VehicleType.TugBoat:
					case VehicleType.SubmarineOne:
					case VehicleType.SubmarineTwo:
						return VehicleCategory.WaterVehicle;
					case VehicleType.Snowmobile:
						return VehicleCategory.WinterVehicle;
					case VehicleType.Train:
						return VehicleCategory.TrainVehicle;
					default:
						return VehicleCategory.None;
				}
			}
		}

        private CuiElementContainer GetVehicleDefaultPanel(string userID, VehicleData vehicleData)
        {
            string description, descriptionValue;
            bool notOwner = vehicleData.OwnerID != 0 && !vehicleData.IsOwner(userID);
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "193 230", OffsetMax = "573 339" },
                Image = { Color = "0.969 0.922 0.882 0.035", Material = "assets/content/ui/ui.background.tiletex.psd" }
            }, "Overlay", _uiVehiclePanel);

            string[] values = new string[5];
            values[0] = vehicleData.ID.ToString();
            values[1] = lang.GetMessage($"MsgVehicle{vehicleData.Type}", this, userID).FirstToUpper();
            values[2] = lang.GetMessage($"MsgVehicle{vehicleData.Category}", this, userID);
            values[3] = !string.IsNullOrWhiteSpace(vehicleData.RegistrationDate) ? $"{vehicleData.RegistrationDate}(UTC)" : lang.GetMessage("MsgNoDate", this, userID);
            if (notOwner)
            {
                var owner = BasePlayer.FindByID(vehicleData.OwnerID);
                values[4] = owner != null ? owner.displayName : vehicleData.OwnerID.ToString();
                description = lang.GetMessage("MsgVehicleDialogDescriptionNotOwner", this, userID);
                descriptionValue = string.Format(lang.GetMessage("MsgVehicleDialogDescriptionNotOwnerValue", this, userID), values);
            }
            else if (vehicleData.OwnerID == 0)
            {
                if (_economicsIsLoaded)
                {
                    var perm = GetVehiclePricePermission(userID, vehicleData.Type);
                    values[4] = $"{string.Format(_config.PriceFormat, $"{perm?.Allowed_Vehicles[vehicleData.Type].Price ?? 0}")}({string.Format(_config.PriceFormat, $"{Economics?.Call("Balance", userID) ?? 0}")})";
                }
                else
                    values[4] = lang.GetMessage("MsgFree", this, userID);
                description = lang.GetMessage("MsgVehicleDialogDescription", this, userID);
                descriptionValue = string.Format(lang.GetMessage("MsgVehicleDialogDescriptionValue", this, userID), values);
            }

            else
            {
                description = lang.GetMessage("MsgVehicleDialogDescriptionRegistered", this, userID);
                descriptionValue = string.Format(lang.GetMessage("MsgVehicleDialogDescriptionRegisteredValue", this, userID), values);
            }
            container.Add(new CuiLabel
            {
                Text =
                {
                    Text = lang.GetMessage("MsgVehicleDialogTitle", this, userID),
                    FontSize = 12,
                    Color = WhiteColor,
                    Align = TextAnchor.UpperCenter
                },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 85", OffsetMax = "-5 104" }
            }, _uiVehiclePanel);
            container.Add(new CuiElement
            {
                Name = "Description",
                Parent = _uiVehiclePanel,
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0 0" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"5 {(notOwner ? "5" : "30")}", OffsetMax = "-5 -25" },
                    new CuiScrollViewComponent
                    {
                        ContentTransform = new CuiRectTransform { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"0 -{(12 *  description.Split('\n').Length * 1.2) - (notOwner ? 80 : 53)}", OffsetMax = "0 0" },
                        Vertical = true,
                        MovementType = ScrollRect.MovementType.Elastic,
                        ScrollSensitivity = 20f,
                        HorizontalScrollbar = null,
                        VerticalScrollbar = null
                    }
                }
            });
            container.Add(new CuiLabel
            {
                Text =
                {
                    Text = description,
                    Font = "RobotoCondensed-Regular.ttf",
                    FontSize = 12,
                    Color = WhiteColor,
                    Align = TextAnchor.MiddleLeft
                },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 0", OffsetMax = "0 0" }
            }, "Description");
            container.Add(new CuiLabel
            {
                Text =
                {
                    Text = descriptionValue,
                    Font = "RobotoCondensed-Regular.ttf",
                    FontSize = 12,
                    Color = WhiteColor,
                    Align = TextAnchor.MiddleRight
                },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "-5 0" }
            }, "Description");
            if (!notOwner)
            {
                container.Add(new CuiButton
                {
                    Text =
                    {
                        Text = lang.GetMessage(vehicleData.OwnerID == 0 ? "MsgVehicleDialogLink" : "MsgVehicleDialogUnLink", this, userID),
                        Font = "RobotoCondensed-Regular.ttf",
                        FontSize = 12,
                        Color = WhiteColor,
                        Align = TextAnchor.MiddleCenter
                    },
                    Button =
                    {
                        Command = $"{_commandUI} vehicle {(vehicleData.OwnerID == 0 ? "link" : "unlink")} {vehicleData.ID}",
                        Color = vehicleData.OwnerID == 0 ? "0.41 0.55 0.41 0.8" : "1 0.4 0.4 0.8"
                    },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 25" }
                }, _uiVehiclePanel);
            }
            return container;
        }
		object OnEntityTakeDamage(VehicleModuleSeating module, HitInfo info) => CanModuleTakeDamage(module, info);

		void OnCargoWatcherDeleted(string monumentID)
		{
			if (_monumentsList.TryGetValue(monumentID, out var monumentData))
				monumentData.Destroy(monumentID);
		}
		private const string _beachPathOld = @"RealPVE\_old_NewbieConfig({0})";

		public enum VehicleType
		{
			None,
			Horse,
			Bike,
            MotorBike,
            Car,
			Balloon,
			Minicopter,
			TransportHeli,
			AttackHeli,
			RowBoat,
			RHIB,
			TugBoat,
			SubmarineOne,
			SubmarineTwo,
			Snowmobile,
			Train
		}
		
		object CanAssignBed(BasePlayer player, SleepingBag bag, ulong targetPlayerId)
		{
			int limit = GetBedsLimit(targetPlayerId.ToString());
			int total = CountBeds(targetPlayerId);
			BasePlayer basePlayer = RelationshipManager.FindByID(targetPlayerId);
			if (total >= limit)
			{
				player.ShowToast(GameTip.Styles.Red_Normal, bag.cannotAssignBedPhrase, false, basePlayer?.displayName ?? "other player");
				return false;
			}
			NextTick(() => player.ShowToast(GameTip.Styles.Blue_Long, SleepingBag.bagLimitPhrase, false, CountBeds(player.userID).ToString(), limit.ToString()));
			return null;
		}
		void OnTeamKick(RelationshipManager.PlayerTeam team, BasePlayer player, ulong targetID)
        {
			if (BasePlayer.TryFindByID(targetID, out var tPlayer))
				OnTeamUpdated(tPlayer);
		}
		private void SendMessage(IPlayer player, string replyKey, string[] replyArgs = null, bool isWarning = true)
		{
			if (player == null) return;
			
			string text = lang.GetMessage(replyKey, this, player.Id);
			if (replyArgs != null)
				text = string.Format(text, replyArgs);
			
			if (!player.IsServer && _config.GameTips_Enabled)
				player.Command("gametip.showtoast", (int)(isWarning ? GameTip.Styles.Error : GameTip.Styles.Blue_Long), text, string.Empty);
			else
				player.Reply(text);
		}
		private readonly Dictionary<string, RBSettings> _defaultRaidableBasesConfig = new Dictionary<string, RBSettings>()
		{
			{ RaidableMode.Easy.ToString(), new RBSettings() { Price = 75d,
				Bar = new BarSettings()
				{
					Main_Color = "#60BF91",
					Main_Transparency = 0.7f,
					Main_Material = "assets/content/ui/uibackgroundblur.mat",
					Image_Url = "https://i.imgur.com/5lkjFih.png",
					Image_Local = "RealPVE_RaidableBases_Easy",
					Image_Color = "#94EDC2",
					Text_Color = "#94EDC2",
					SubText_Color = "#94EDC2"
				},
				ProgressBar = new ProgressBarSettings() { Progress_Color = "#60BF91" }
			}},
			{ RaidableMode.Medium.ToString(), new RBSettings() { Price = 150d,
				Bar = new BarSettings()
				{
					Main_Color = "#EFA287",
					Main_Transparency = 0.7f,
					Main_Material = "assets/content/ui/uibackgroundblur.mat",
					Image_Url = "https://i.imgur.com/5lkjFih.png",
					Image_Local = "RealPVE_RaidableBases_Medium",
					Image_Color = "#FAE197",
					Text_Color = "#FAE197",
					SubText_Color = "#FAE197"
				},
				ProgressBar = new ProgressBarSettings() { Progress_Color = "#EFA287" }
			}},
			{ RaidableMode.Hard.ToString(), new RBSettings() { Price = 225d,
				Bar = new BarSettings()
				{
					Main_Color = "#F75C5F",
					Main_Transparency = 0.7f,
					Main_Material = "assets/content/ui/uibackgroundblur.mat",
					Image_Url = "https://i.imgur.com/5lkjFih.png",
					Image_Local = "RealPVE_RaidableBases_Hard",
					Image_Color = "#FABBC4",
					Text_Color = "#FABBC4",
					SubText_Color = "#FABBC4"
				},
				ProgressBar = new ProgressBarSettings() { Progress_Color = "#F75C5F" }
			}},
			{ RaidableMode.Expert.ToString(), new RBSettings() { Price = 300d,
				Bar = new BarSettings()
				{
					Main_Color = "#E1402A",
					Main_Transparency = 0.7f,
					Main_Material = "assets/content/ui/uibackgroundblur.mat",
					Image_Url = "https://i.imgur.com/5lkjFih.png",
					Image_Local = "RealPVE_RaidableBases_Expert",
					Image_Color = "#FFD272",
					Text_Color = "#FFD272",
					SubText_Color = "#FFD272"
				},
				ProgressBar = new ProgressBarSettings() { Progress_Color = "#E1402A" }
			}},
			{ RaidableMode.Nightmare.ToString(), new RBSettings() { Price = 400d,
				Bar = new BarSettings()
				{
					Main_Color = "#D0B321",
					Main_Transparency = 0.7f,
					Main_Material = "assets/content/ui/uibackgroundblur.mat",
					Image_Url = "https://i.imgur.com/5lkjFih.png",
					Image_Local = "RealPVE_RaidableBases_Nightmare",
					Image_Color = "#FFEC5A",
					Text_Color = "#FFEC5A",
					SubText_Color = "#FFEC5A"
				},
				ProgressBar = new ProgressBarSettings() { Progress_Color = "#D0B321" }
			}}
		};
		
		void OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player) => OnTeamUpdated(player);

		private void MonumentBroadcast(string monumentID, string replyKey, string[] replyArgs = null, bool isWarning = false)
		{
			if (replyArgs == null)
				replyArgs = new string[1];
			foreach (var player in BasePlayer.activePlayerList)
			{
				if (!player.userID.IsSteamId()) continue;
				replyArgs[0] = GetMonumentName(monumentID, player.userID);
				SendMessage(player.IPlayer, replyKey, replyArgs, isWarning);
			}
		}
		private string GetMonumentByPos(Vector3 pos) { var a = GetMonumentsByPos(pos); return a.Length > 0 ? a[^1] : string.Empty; }
		
		object OnEntityTakeDamage(PlayerCorpse corpse, HitInfo info)
        {
			if (info == null || info.Initiator is not BasePlayer attacker || attacker == null || !attacker.userID.IsSteamId()) return null;
			if (corpse.playerSteamID.IsSteamId() && !IsEntityInPvP(attacker.userID, corpse.net.ID.Value) && !UrishMumkin(attacker, corpse.playerSteamID))
				goto cancel;
			return null;
		
		cancel:
			info.Initiator = null;
			info.damageTypes.Clear();
			return null;
		}
		
		private class NewbieConfig
		{
			[JsonProperty(PropertyName = "Is it worth changing the list of items given at spawn on the beach?")]
			public bool Respawn_Override = true;
			
			[JsonProperty(PropertyName = "List of items for the main inventory")]
			public HashSet<BeachItem> Respawn_Main = null;
			
			[JsonProperty(PropertyName = "List of items for the belt")]
			public HashSet<BeachItem> Respawn_Belt = null;
			
			[JsonProperty(PropertyName = "List of items for clothing")]
			public HashSet<BeachItem> Respawn_Wear = null;
			
			public Oxide.Core.VersionNumber Version;
		}
		object OnTurretAuthorize(AutoTurret turret, BasePlayer player) => !turret.OwnerID.IsSteamId() || IsEntityInPvP(player.userID, turret.net.ID.Value) ? null : player.TasirMumkin(turret.OwnerID);
		
		private static PermissionConfig _permissionsConfig;
		object OnEntityTakeDamage(ScrapTransportHelicopter scrapHeli, HitInfo info) => CanVehicleTakeDamage(scrapHeli.net?.ID.Value ?? 0uL, info);
		object CanUpdateSign(BasePlayer player, PhotoFrame photoFrame) => !photoFrame.OwnerID.IsSteamId() || IsEntityInPvP(player.userID, photoFrame.net.ID.Value) ? null : player.TasirMumkin(photoFrame.OwnerID);
		
		void OnScientistRecalled(BradleyAPC bradley, ScientistNPC scientist)
        {
			if (bradley.skinID == 0uL)
				_eventScientistsList.Remove(scientist.net.ID);
		}
		object OnEntityTakeDamage(SubmarineDuo submarine, HitInfo info) => CanVehicleTakeDamage(submarine.net?.ID.Value ?? 0uL, info);
		
		void OnEntityKill(BaseVehicle vehicle)
        {
			OnEntityExitPVP(vehicle);
			if (_vehiclesList.TryGetValue(vehicle.net.ID.Value, out var vehicleData))
				vehicleData.OnDestroy();
		}
		object OnPlayerDropActiveItem(BasePlayer player, Item item) => player.userID.IsSteamId() ? false : null;
		
		object OnSleepFrankenstein(FrankensteinTable table, BasePlayer player, FrankensteinPet pet)
        {
			if (_unrestrictedLooters.Contains(player.userID)) return null;
			object result = null;
			if (table.OwnerID.IsSteamId())
				result = player.TasirMumkin(table.OwnerID);
			if (result != null)
                SendMessage(player, "MsgCantInteract");
            return result;
        }
        private readonly VersionNumber _vanillaEventsVersion = new VersionNumber(0, 1, 0);
		
                private const ulong _rrPluginID = 8675309uL;

        object CanBeTargeted(BasePlayer target, FlameTurret flameTurret)
        {
            if (target.userID.IsSteamId())
            {
				if (flameTurret.skinID == 0uL)
				{
					if (flameTurret.OwnerID.IsSteamId() && !IsEntityInPvP(target.userID, flameTurret.net.ID.Value))
						return false;
				}
				else if (flameTurret.skinID == _rbPluginID)
                {
					if (TryGetRaidBase(flameTurret.transform.position, out var rbData) && !rbData.CanInteractWithRaid(target.userID))
						return false;
				}
			}
            return null;
        }
		
		void OnEntityDeath(BasePlayer player)
        {
			if (_pvpPlayers.TryGetValue(player.userID, out var playerPvP))
			{
				DestroyBar(player.userID, playerPvP.LastZone);
				playerPvP.ActiveZones.Clear();
			}
		}
		
		void OnPlayerExitPVP(BasePlayer player, string zoneID, float delay = 0f)
        {
			if (!_pvpPlayers.TryGetValue(player.userID, out var playerPvP)) return;
			int index = playerPvP.ActiveZones.IndexOf(zoneID);
			if (!playerPvP.ActiveZones.Remove(zoneID)) return;
			
			if (playerPvP.ActiveZones.Any())
				playerPvP.LastZone = playerPvP.ActiveZones[^1];
			else
			{
				delay = (float)(Interface.CallHook(Hooks_OnPlayerPVPDelay, player, delay, zoneID) ?? delay);
				if (delay > 0f)
                {
					playerPvP.DelayEnd = _unixSeconds + delay;
					Interface.CallHook(Hooks_OnPlayerPVPDelayed, player, delay, zoneID);
				}
			}
			
			if (!player.IsConnected || index != playerPvP.ActiveZones.Count) return;
			
			DestroyBar(player.userID, zoneID);
			if (playerPvP.ActiveZones.Any())
				SendPvPBar(player, playerPvP.LastZone);
			else if (delay > 0f)
				SendPvPDelayBar(player, zoneID, delay, playerPvP.DelayEnd);
		}

		void Init()
		{
			for (int i = 0; i < _defaultHooks.Length; i++)
				Unsubscribe(_defaultHooks[i]);
			Unsubscribe(nameof(OnServerMessage));
			Unsubscribe(nameof(OnPlayerHandcuff));
			Unsubscribe(nameof(OnPortalUse));
			Unsubscribe(nameof(OnPortalUsed));
			Unsubscribe(nameof(OnBackpackDrop));
			Unsubscribe(nameof(OnEntityEnteredMonument));
			Unsubscribe(nameof(OnEntityExitedMonument));
			Unsubscribe(nameof(OnPlayerEnteredMonument));
			Unsubscribe(nameof(OnPlayerExitedMonument));
			Unsubscribe(nameof(OnCargoWatcherCreated));
			Unsubscribe(nameof(OnCargoWatcherDeleted));
			Unsubscribe(nameof(OnCargoShipHarborArrived));
			Unsubscribe(nameof(OnCargoShipHarborLeave));
			Unsubscribe(nameof(OnHarborEventStart));
			Unsubscribe(nameof(OnHarborEventEnd));
			Unsubscribe(nameof(OnCrateLaptopAttack));
			Unsubscribe(nameof(OnExcavatorResourceSet));
			Unsubscribe(nameof(OnExcavatorSuppliesRequest));
			Unsubscribe(nameof(OnExcavatorSuppliesRequested));
			Unsubscribe(nameof(CanHackCrate));
			Unsubscribe(nameof(OnMonumentsWatcherLoaded));
			Unsubscribe(nameof(OnNpcTarget));
			Unsubscribe(nameof(CanBradleyApcTarget));
			Unsubscribe(nameof(OnEntityEnterZone));
			Unsubscribe(nameof(OnEntityExitZone));
			Unsubscribe(nameof(OnCreateDynamicPVP));
			Unsubscribe(nameof(OnCreatedDynamicPVP));
			Unsubscribe(nameof(OnDeletedDynamicPVP));
			Unsubscribe(nameof(OnScientistInitialized));
			Unsubscribe(nameof(OnScientistRecalled));
			Unsubscribe(nameof(OnPlayerEnteredRaidableBase));
			Unsubscribe(nameof(OnPlayerExitedRaidableBase));
			Unsubscribe(nameof(OnRaidableLootDestroyed));
			Unsubscribe(nameof(OnRaidableDespawnUpdate));
			Unsubscribe(nameof(OnRaidableBasePurchased));
			Unsubscribe(nameof(OnRaidableBaseStarted));
			Unsubscribe(nameof(OnRaidableBaseEnded));
			Unsubscribe(nameof(OnRandomRaidStart));
			Unsubscribe(nameof(RandomRaidEventEnd));
			Unsubscribe(nameof(OnRandomRaidRaiderSpawned));
			Unsubscribe(nameof(OnRandomRaidHeliSpawned));
			Unsubscribe(nameof(OnRandomRaidWin));
			Unsubscribe(nameof(OnBuildingSplit));
			Unsubscribe(nameof(OnDefaultItemsReceive));
			Unsubscribe(nameof(OnAdvancedStatusLoaded));
			
			Instance = this;
			permission.RegisterPermission(PERMISSION_ADMIN, this);
			_commandUI = $"{Name}_{Guid.NewGuid():N}";
			_playerUI = new Dictionary<ulong, HashSet<string>>();
			AddCovalenceCommand(_commandUI, nameof(Command_UI));
			AddCovalenceCommand(_config.AdminCommand, nameof(Command_Admin));
			AddCovalenceCommand(_config.Command, nameof(Command_RealPVE));
			LoadPermissionsConfig();
			LoadMonumentsConfig();
			LoadData(_dataLootersPath, out _unrestrictedLooters);
			LoadData(_dataPickupsPath, out _pickupPlayers);
			LoadData(_dataVehiclesPath, out _vehiclesList);
			LoadData(_dataTeamsPath, out _teamsList);
			LoadVanillaEventsConfig();
			LoadRBsConfig();
			LoadBeachConfig();
		}
		
		private void LoadVanillaEventsConfig()
        {
			List<CuiElement> uiList = null;
			if (Interface.Oxide.DataFileSystem.ExistsDatafile(_vanillaEventsPath))
            {
				try
				{
					_vanillaEventsConfig = Interface.Oxide.DataFileSystem.ReadObject<VanillaEventsConfig>(_vanillaEventsPath);
					uiList = Interface.Oxide.DataFileSystem.ReadObject<List<CuiElement>>(_vanillaEventsUiOfferPath);
				}
				catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
            }
			
			if (_vanillaEventsConfig == null || _vanillaEventsConfig.Version < _vanillaEventsVersion)
            {
                if (_vanillaEventsConfig != null)
                {
                    string path = string.Format(_vanillaEventsPathOld, _vanillaEventsConfig.Version.ToString());
                    PrintWarning($@"Your settings version for vanilla events is outdated. The config file has been updated, and your old settings have been saved in \data\{path}");
                    SaveVanillaEventsConfig(path);
                }
				_vanillaEventsConfig = new VanillaEventsConfig() { Version = _vanillaEventsVersion };
            }
			
			if (_vanillaEventsConfig.EventPatrolHelicopter == null)
				_vanillaEventsConfig.EventPatrolHelicopter = new EventSettings();
			else
				_vanillaEventsConfig.EventPatrolHelicopter.OfferTime = Math.Clamp(_vanillaEventsConfig.EventPatrolHelicopter.OfferTime, 1f, 15f);
			if (_vanillaEventsConfig.EventBradleyAPC == null)
                _vanillaEventsConfig.EventBradleyAPC = new EventSettings();
            else
				_vanillaEventsConfig.EventBradleyAPC.OfferTime = Math.Clamp(_vanillaEventsConfig.EventBradleyAPC.OfferTime, 1f, 15f);
			
			if (uiList == null || !uiList.Any())
            {
                uiList = GetDefaultClaimOffer();
                Interface.Oxide.DataFileSystem.WriteObject(_vanillaEventsUiOfferPath, uiList);
            }
			_vanillaEventsUiOffer = ReplacePlaceholders(CuiHelper.ToJson(uiList), EventOfferUI);
			
			SaveVanillaEventsConfig();
        }
		
		object CanBeTargeted(BasePlayer target, AutoTurret turret)
        {
			if (target.userID.IsSteamId())
            {
				if (turret.skinID == 0uL)
                {
					if (turret.OwnerID.IsSteamId() && !IsEntityInPvP(target.userID, turret.net.ID.Value))
						return false;
				}
				else if (turret.skinID == _rbPluginID)
				{
					if (TryGetRaidBase(turret.transform.position, out var rbData) && !rbData.CanInteractWithRaid(target.userID))
                        return false;
                }
			}
            return null;
        }
		private Dictionary<ulong, string> _respawnMessage = new Dictionary<ulong, string>();
		object CanLootEntity(BasePlayer player, ShopFront shopFront) => null;
		
		private void CheckForUpdates()
		{
			_unixSeconds = Network.TimeEx.currentTimestamp;
			foreach (var monumentData in _monumentsList.Values)
			{
				if (monumentData.IsMoveable)
					monumentData.MonumentPos = GetMonumentPosition(monumentData.MonumentID);
				if (monumentData.LootEndTime != 0d && _unixSeconds > monumentData.LootEndTime)
					monumentData.RemoveOwner();
				else if (monumentData.IsPvP)
					monumentData.UpdateCircleMapMarker();
			}
			
			var pvpToRemove = new HashSet<ulong>();
			PlayerPvP playerPvP;
			foreach (var kvp in _pvpPlayers)
			{
				playerPvP = kvp.Value;
				if (!playerPvP.ActiveZones.Any() && _unixSeconds > playerPvP.DelayEnd)
					pvpToRemove.Add(kvp.Key);
			}
			foreach (ulong userID in pvpToRemove)
				_pvpPlayers.Remove(userID);
		}
		
		void OnPlayerExitedRaidableBase(BasePlayer player, Vector3 pos)
		{
			if (_rbList.TryGetValue(pos.ToString(), out var rbData))
				rbData.OnPlayerExit(player);
		}
		
		public class MonumentData
		{
			public string MonumentID { get; private set; }
			public Vector3 MonumentPos { get; set; }
			public string BarID { get; private set; }
            public BasePlayer Owner { get; private set; }
			public ulong OwnerID { get; private set; }
			public string OwnerIDString { get; private set; }
			
			public MonumentSettings Settings { get; private set; }
			public Dictionary<int, object> StatusBar { get; private set; }
			public Dictionary<int, object> StatusProgressBar { get; private set; }
			
			public List<BasePlayer> PlayersQueue = Pool.Get<List<BasePlayer>>();
			public HashSet<BasePlayer> FriendsList = Pool.Get<HashSet<BasePlayer>>();
			
			private double LootStartTime { get; set; }
			public double LootEndTime { get; private set; }
			private double BroadcastLast { get; set; }
			private Timer MonumentTimer = null;
			
			public bool IsMoveable { get; private set; }
			public bool IsPvP { get; private set; }
			
			private VendingMachineMapMarker mainMapMarker;
			private MapMarkerGenericRadius circleMapMarker;
			
			public MonumentData(string monumentID, MonumentSettings monumentSettings, bool isMoveable = false)
			{
				MonumentID = monumentID;
				BarID = $"{StatusBarID}{MonumentID}";
				OwnerIDString = string.Empty;
				Settings = monumentSettings;
				MonumentPos = Instance.GetMonumentPosition(MonumentID);
				IsMoveable = isMoveable;
				IsPvP = Settings.IsPvP;
				UpdateMapMarkers();
				
				var barSettings = Settings.Bar;
				StatusBar = new Dictionary<int, object>
				{
					{ 0, BarID },
					{ 1, Instance.Name },
					{ 2, "Default" },
					{ 3, "Monuments" },
					{ 4, barSettings.Order },
					{ 5, barSettings.Height },
					{ 6, barSettings.Main_Color },
					{ 11, barSettings.Image_IsRawImage },
					{ 12, barSettings.Image_Color },
					{ 16, barSettings.Text_Size },
					{ 17, barSettings.Text_Color },
					{ 18, barSettings.Text_Font },
					{ 23, barSettings.SubText_Size },
					{ 24, barSettings.SubText_Color },
					{ 25, barSettings.SubText_Font }
				};
				
				if (barSettings.Main_Color.StartsWith("#"))
					StatusBar.Add(-6, barSettings.Main_Transparency);
				if (!string.IsNullOrWhiteSpace(barSettings.Main_Material))
					StatusBar.Add(7, barSettings.Main_Material);
				if (barSettings.Image_Color.StartsWith("#"))
                    StatusBar.Add(-12, barSettings.Image_Transparency);
				if (barSettings.Image_Outline_Enabled)
				{
					StatusBar.Add(13, barSettings.Image_Outline_Color);
					if (barSettings.Image_Outline_Color.StartsWith("#"))
						StatusBar.Add(-13, barSettings.Image_Outline_Transparency);
					StatusBar.Add(14, barSettings.Image_Outline_Distance);
				}
				if (barSettings.Text_Outline_Enabled)
                {
					StatusBar.Add(20, barSettings.Text_Outline_Color);
					if (barSettings.Text_Outline_Color.StartsWith("#"))
						StatusBar.Add(-20, barSettings.Text_Outline_Transparency);
					StatusBar.Add(21, barSettings.Text_Outline_Distance);
                }
				if (barSettings.SubText_Outline_Enabled)
                {
					StatusBar.Add(26, barSettings.SubText_Outline_Color);
					if (barSettings.SubText_Outline_Color.StartsWith("#"))
						StatusBar.Add(-26, barSettings.SubText_Outline_Transparency);
					StatusBar.Add(27, barSettings.SubText_Outline_Distance);
				}
				
				UpdateBars();
				
				var players = Instance.GetMonumentPlayers(MonumentID);
				if (players != null && players.Any())
				{
					foreach (var player in players)
						OnPlayerEnter(player);
				}
				if (IsPvP)
				{
					var entities = Instance.GetMonumentEntities(MonumentID);
                    if (entities != null && entities.Any())
                    {
						foreach (var entity in entities)
							OnEntityEnterPVP(entity);
					}
				}
			}

            public void OnPlayerEnter(BasePlayer player)
            {
				if (player == null) return;
				if (IsPvP)
                {
					Instance.OnPlayerEnterPVP(player, MonumentID);
					return;
				}
				if (PlayersQueue.Contains(player) || !player.IsConnected || player.IsDead()) return;
				if (OwnerID.IsSteamId())
                {
					if (OwnerID == player.userID)
						Instance.SendCounterBar(player, this, LootEndTime, Settings.UseProgress ? LootStartTime : 0d);
					else if (IsOwnerFriend(player))
                    {
						Instance.SendCounterBar(player, this, LootEndTime, Settings.UseProgress ? LootStartTime : 0d);
						FriendsList.Add(player);
                    }
					else
					{
						PlayersQueue.Add(player);
						if (_economicsIsLoaded)
						{
							double lootPrice = Settings.Price > 0d ? Settings.Price * Instance.GetMonumentPriceMultiplier(player.UserIDString) : Settings.Price;
							if (lootPrice > 0d)
								Instance.SendMessage(player, "MsgMonumentLootingNotFree", new string[] { string.Format(_config.PriceFormat, lootPrice) });
						}
						ShowQueueBarForAll();
					}
				}
                else
				{
					PlayersQueue.Add(player);
					if (PlayersQueue.IndexOf(player) == 0)
						OfferOwnership();
					ShowQueueBarForAll();
				}
            }
			
			public void OnPlayerExit(BasePlayer player, string reason = "leave")
            {
				if (player == null) return;
				Instance.DestroyBar(player.userID, BarID);
				if (IsPvP)
                {
					Instance.OnPlayerExitPVP(player, MonumentID, Settings.PvPDelay);
					return;
				}
				if (OwnerID == player.userID)
                {
                    if (reason == "death")
                        Instance._respawnMessage[player.userID] = string.Format(Instance.lang.GetMessage("MsgMonumentLooterDeath", Instance, player.UserIDString), new string[] { Instance.GetMonumentName(MonumentID, player.userID), $"{(int)(LootEndTime - _unixSeconds)}" });
                    else
                    {
						Instance.SendCounterBar(player, this, _unixSeconds + _monumentsConfig.TimeToComeBack, _unixSeconds);
						player.SendEffect();
                        Instance.SendMessage(player, "MsgMonumentLooterExit", new string[] { _monumentsConfig.TimeToComeBack.ToString() });
                        if (MonumentTimer != null)
                            MonumentTimer.Destroy();
                        MonumentTimer = Instance.timer.Once(_monumentsConfig.TimeToComeBack, () =>
                        {
							if (player != null && !Instance.IsPlayerInMonument(MonumentID, player))
                            {
                                Instance.SendMessage(player, "MsgMonumentLooterRemoved");
                                RemoveOwner();
                            }
                        });
                    }
                }
				else if (!FriendsList.Remove(player))
				{
					if (PlayersQueue.Remove(player))
						ShowQueueBarForAll();
					else
						Instance.DestroyUI(player, MonumentOfferUI);
				}
			}
			
			public void SetNewOwner(BasePlayer newOwner)
			{
				PlayersQueue.Remove(newOwner);
				if (OwnerID.IsSteamId())
					PlayersQueue.Add(Owner);
				Owner = newOwner;
				OwnerID = Owner.userID;
				OwnerIDString = OwnerID.ToString();
				LootStartTime = _unixSeconds;
				LootEndTime = LootStartTime + Settings.LootingTime;
				Instance.SendCounterBar(newOwner, this, LootEndTime, Settings.UseProgress ? LootStartTime : 0d);
				var friends = Pool.Get<List<BasePlayer>>();
				foreach (var player in PlayersQueue)
				{
					if (IsOwnerFriend(player))
						friends.Add(player);
				}
				FriendsList.Clear();
				foreach (var friend in friends)
					FromQueueToFriend(friend);
				Pool.FreeUnmanaged(ref friends);
				ShowQueueBarForAll();
				if (Settings.Broadcast && (_unixSeconds - BroadcastLast) >= 5)
					Instance.MonumentBroadcast(MonumentID, "MsgMonumentOccupied", new string[] { string.Empty, newOwner.displayName, $"{(LootEndTime - _unixSeconds) / 60}" }, true);
			}
			
			public void RemoveOwner()
            {
				if (OwnerID.IsSteamId())
				{
					if (Owner != null && Instance.IsPlayerInMonument(MonumentID, Owner))
						PlayersQueue.Add(Owner);
					foreach (var friend in FriendsList)
						PlayersQueue.Add(friend);
				}
				Owner = null;
				OwnerID = 0uL;
				OwnerIDString = string.Empty;
				FriendsList.Clear();
				LootStartTime = 0d;
				LootEndTime = 0d;
				if (Settings.Broadcast && (_unixSeconds - BroadcastLast) >= 5)
                {
					Instance.MonumentBroadcast(MonumentID, "MsgMonumentFree");
					BroadcastLast = _unixSeconds;
				}
				OfferOwnership();
				ShowQueueBarForAll();
			}
			
			public void OfferOwnership()
			{
				if (!PlayersQueue.Any()) return;
				var firstPlayer = PlayersQueue[0];
				if (!_economicsIsLoaded || Settings.Price <= 0d || (Settings.Price * Instance.GetMonumentPriceMultiplier(firstPlayer.UserIDString)) <= 0d)
					SetNewOwner(firstPlayer);
				else
                {
					Instance.ShowMonumentOffer(firstPlayer, this);
					if (MonumentTimer != null)
						MonumentTimer.Destroy();
					MonumentTimer = Instance.timer.Once(Settings.OfferTime, () => { if (this != null) OnOwnershipOfferExpired(firstPlayer); });
				}
			}
			
			public void OnOwnershipOfferExpired(BasePlayer player)
			{
				if (player == null || OwnerID.IsSteamId()) return;
				if (MonumentTimer != null)
					MonumentTimer.Destroy();
				Instance.DestroyUI(player, MonumentOfferUI);
				Instance.DestroyBar(player.userID, BarID);
				if (PlayersQueue.Any())
					PlayersQueue.RemoveAt(0);
				OfferOwnership();
				ShowQueueBarForAll();
				if (Instance.IsPlayerInMonument(MonumentID, player))
					Instance.SendMonumentsBar(player, this, displayTime: 5);
			}
			
			public void UpdateTimer(float newTime = 0f)
			{
				if (IsPvP || !OwnerID.IsSteamId()) return;
				double seconds = 0d, num = LootEndTime - _unixSeconds;
				if (newTime == 0)
					seconds = Settings.LootingTime;
				else if (num < newTime)
					seconds = (Settings.LootingTime > newTime ? Settings.LootingTime : newTime) - num;
				LootEndTime += seconds;
				Instance.SendCounterBar(Owner, this, LootEndTime, Settings.UseProgress ? LootStartTime : 0d);
				foreach (var friend in FriendsList)
					Instance.SendCounterBar(friend, this, LootEndTime, Settings.UseProgress ? LootStartTime : 0d);
			}
			
			public void ShowQueueBarForAll()
			{
				int queueTotal = PlayersQueue.Count;
				for (int i = 0; i < queueTotal; i++)
				{
					var player = PlayersQueue[i];
					Instance.SendMonumentsBar(player, this, $"{i + 1}/{queueTotal}");
				}
			}
			
			public void OnTeamUpdated(BasePlayer player)
            {
				if (IsPvP || !OwnerID.IsSteamId()) return;
				if (OwnerID == player.userID)
                {
					var players = Instance.GetMonumentPlayers(MonumentID);
					if (players != null && players.Any())
                    {
						foreach (var tPlayer in players)
						{
							if (tPlayer.userID != OwnerID)
								UpdateFriendList(tPlayer);
						}
					}
				}
				else
					UpdateFriendList(player);
			}
			
			public void OnFriendUpdated(BasePlayer player, BasePlayer friend)
            {
				if (IsPvP || !OwnerID.IsSteamId()) return;
				if (OwnerID == player.userID)
					UpdateFriendList(friend);
				else
					UpdateFriendList(player);
			}
			
			private void UpdateFriendList(BasePlayer player)
            {
				bool isFriend = IsOwnerFriend(player);
				if ((isFriend && !FriendsList.Contains(player)) || (!isFriend && FriendsList.Contains(player)))
                {
					OnPlayerExit(player);
                    OnPlayerEnter(player);
                }
			}
			
			private void FromQueueToFriend(BasePlayer target)
            {
                PlayersQueue.Remove(target);
                FriendsList.Add(target);
                Instance.SendCounterBar(target, this, LootEndTime, Settings.UseProgress ? LootStartTime : 0d);
            }
			
			public bool SetAsPvP(bool addMarkers = true)
            {
                if (IsPvP) return false;
				
				IsPvP = true;
                Owner = null;
				OwnerID = 0uL;
                OwnerIDString = string.Empty;
                FriendsList.Clear();
                PlayersQueue.Clear();
                LootStartTime = 0d;
                LootEndTime = 0d;
				var players = Instance.GetMonumentPlayers(MonumentID);
				if (players != null && players.Any())
				{
					foreach (var player in players)
					{
						Instance.DestroyBar(player.userID, BarID);
						OnPlayerEnter(player);
					}
				}
				var entities = Instance.GetMonumentEntities(MonumentID);
				if (entities != null && entities.Any())
                {
					foreach (var entity in entities)
						OnEntityEnterPVP(entity);
				}
				if (addMarkers)
                    UpdateMapMarkers();
                return true;
            }

            public bool RemovePvP()
            {
                if (!IsPvP) return false;
                IsPvP = false;
				var players = Instance.GetMonumentPlayers(MonumentID);
                if (players != null && players.Any())
				{
					foreach (var player in players)
					{
						OnPlayerExit(player);
						OnPlayerEnter(player);
					}
				}
				var entities = Instance.GetMonumentEntities(MonumentID);
				if (entities != null && entities.Any())
                {
                    foreach (var entity in entities)
						OnEntityExitPVP(entity);
				}
                UpdateMapMarkers();
				return true;
			}
			
			private void UpdateMapMarkers()
            {
				if (mainMapMarker != null)
					mainMapMarker.Kill();
				if (circleMapMarker != null)
					circleMapMarker.Kill();
				if (!IsPvP || !Settings.PvPMapMarkers) return;
				
				CargoShip parentEnt = null;
                if (IsMoveable)
                {
                    string[] parts = MonumentID.Split('_');
                    if (parts.Length > 0 && ulong.TryParse(parts[^1], out ulong cargoID))
                        parentEnt = BaseNetworkable.serverEntities.Find(new NetworkableId(cargoID)) as CargoShip;
                }

                mainMapMarker = GameManager.server.CreateEntity(StringPool.Get(3459945130u), MonumentPos) as VendingMachineMapMarker;
                if (mainMapMarker != null)
                {
                    mainMapMarker.markerShopName = $"PvP {Instance.GetMonumentName(MonumentID, showSuffix: false)}";
                    mainMapMarker.enabled = false;
                    mainMapMarker.Spawn();
                    if (parentEnt != null)
                    {
                        mainMapMarker.SetParent(parentEnt);
                        mainMapMarker.transform.localPosition = Vector3.zero;
                    }
                }

                circleMapMarker = GameManager.server.CreateEntity(StringPool.Get(2849728229u), MonumentPos) as MapMarkerGenericRadius;
                if (circleMapMarker != null)
                {
                    circleMapMarker.alpha = 0.75f;
                    circleMapMarker.color1 = Color.red;
                    circleMapMarker.color2 = Color.black;
                    circleMapMarker.radius = World.Size <= 3600 ? 0.5f : 0.25f;
                    circleMapMarker.Spawn();
                    if (parentEnt != null)
                    {
                        circleMapMarker.SetParent(parentEnt);
                        circleMapMarker.transform.localPosition = Vector3.zero;
                    }
                    circleMapMarker.SendUpdate();
                }
			}
			
			public void UpdateCircleMapMarker()
			{
				if (circleMapMarker != null)
					circleMapMarker.SendUpdate();
			}
			
			public object CanPickup(BasePlayer looter)
			{
				if (IsPvP || (OwnerID.IsSteamId() && (OwnerID == looter.userID || FriendsList.Contains(looter) || _pickupPlayers.Contains(OwnerID))))
					return null;
				return false;
			}
			
			public object CanLoot(BasePlayer looter)
            {
                if (IsPvP || (OwnerID.IsSteamId() && (OwnerID == looter.userID || FriendsList.Contains(looter))))
                    return null;
                return false;
			}
			
			public bool IsOwnerFriend(BasePlayer looter)
            {
				if ((looter.Team != null && looter.Team.members.Contains(OwnerID)) || (Instance.Friends != null && Instance.IsFriend(looter.UserIDString, OwnerIDString)))
					return true;
				return false;
			}
			
			public bool TryGetFriend(ulong targetID, out BasePlayer result)
            {
				result = null;
				foreach (var friend in FriendsList)
				{
					if (friend.userID == targetID)
					{
						result = friend;
						return true;
					}
                }
				return false;
			}
			
			private bool TryGetQueuedPlayer(ulong targetID, out BasePlayer result)
            {
                result = null;
                foreach (var qPlayer in PlayersQueue)
                {
                    if (qPlayer.userID == targetID)
                    {
                        result = qPlayer;
                        return true;
                    }
                }
                return false;
            }
			
			public void UpdateBars()
            {
				var barSettings = Settings.Bar;
				StatusBar.Remove(10);
                StatusBar.Remove(9);
                StatusBar.Remove(8);
                if (!string.IsNullOrWhiteSpace(barSettings.Image_Sprite))
                    StatusBar.Add(10, barSettings.Image_Sprite);
                else if (!string.IsNullOrWhiteSpace(barSettings.Image_Local))
                    StatusBar.Add(9, barSettings.Image_Local);
                else
                    StatusBar.Add(8, _imgLibIsLoaded && barSettings.Image_Url.StartsWithAny(Instance.HttpScheme) ? BarID : barSettings.Image_Url);
				
				var progressBar = Settings.ProgressBar;
                StatusProgressBar = new Dictionary<int, object>(StatusBar)
                {
                    { 32, progressBar.Progress_Reverse },
                    { 33, progressBar.Progress_Color },
                    { -33, progressBar.Progress_Transparency },
                    { 34, progressBar.Progress_OffsetMin },
                    { 35, progressBar.Progress_OffsetMax }
                };
                StatusProgressBar[2] = "TimeProgressCounter";
                StatusProgressBar[6] = progressBar.Main_Color;

                if (progressBar.Main_Color.StartsWith("#"))
                    StatusProgressBar[-6] = progressBar.Main_Transparency;
                else
                    StatusProgressBar.Remove(-6);
			}
			
			public void Destroy(string monumentID = "")
            {
				if (!string.IsNullOrWhiteSpace(monumentID))
					Instance._monumentsList.Remove(monumentID);
				
				if (mainMapMarker != null)
					mainMapMarker.Kill();
				if (circleMapMarker != null)
					circleMapMarker.Kill();
				
				if (_statusIsLoaded)
				{
					if (OwnerID.IsSteamId())
						Instance.DestroyBar(OwnerID, BarID);
					foreach (var friend in FriendsList)
						Instance.DestroyBar(friend.userID, BarID);
					foreach (var player in PlayersQueue)
						Instance.DestroyBar(player.userID, BarID);
                }
				FriendsList.Clear();
				PlayersQueue.Clear();
				Pool.FreeUnmanaged(ref FriendsList);
				Pool.FreeUnmanaged(ref PlayersQueue);
			}
		}
		
		object OnSamSiteTarget(SamSite samSite, BaseEntity targetEnt)
        {
			if (samSite.OwnerID == 0uL && (!_rbsConfig.IsEnabled || samSite.skinID != _rbPluginID)) return null;
			if (_pvpEntities.Contains(samSite.net.ID.Value) && _pvpEntities.Contains(targetEnt.net.ID.Value)) return null;
			object result = null;
			
			var mountedPlayers = Pool.Get<List<BasePlayer>>();
            mountedPlayers.AddRange(targetEnt.GetComponentsInChildren<BasePlayer>());
            if (targetEnt is BaseHelicopter heli)
            {
                foreach (var mountPoint in heli.allMountPoints)
                {
                    if (mountPoint != null && mountPoint.mountable != null && mountPoint.mountable.GetMounted() is BasePlayer passenger)
                        mountedPlayers.Add(passenger);
                }
            }
			
			if (mountedPlayers.Any())
            {
                if (samSite.OwnerID.IsSteamId())
                {
                    foreach (var passenger in mountedPlayers)
                    {
						if (passenger.userID.IsSteamId())
                        {
							result = false;
							break;
						}
					}
                }
				else if (samSite.skinID == _rbPluginID)
                {
					if (TryGetRaidBase(samSite.transform.position, out var rbData) && rbData != null)
                    {
						foreach (var passenger in mountedPlayers)
                        {
							if (passenger.userID.IsSteamId() && !rbData.CanInteractWithRaid(passenger.userID))
                            {
                                result = false;
                                break;
                            }
                        }
                    }
				}
			}
            Pool.FreeUnmanaged(ref mountedPlayers);
            return result;
        }
		
		void OnEntityKill(LootableCorpse corpse)
        {
			if (OnEntityExitPVP(corpse) && corpse.playerSteamID.IsSteamId())
			{
				if (!ConVar.Global.disableBagDropping && corpse.containers != null)
                {
					DroppedItemContainer container = ItemContainer.Drop("assets/prefabs/misc/item drop/item_drop_backpack.prefab", corpse.transform.position, Quaternion.identity, corpse.containers);
					if (container != null)
                    {
						container.playerName = corpse.playerName;
						container.playerSteamID = corpse.playerSteamID;
						OnEntityEnterPVP(container);
					}
				}
				corpse.blockBagDrop = true;
			}
			if (corpse.skinID == _bradleySkinId)
                _eventScientistsList.Remove(corpse.net.ID);
        }
		
		object OnEntityTakeDamage(NPCPlayerCorpse corpse, HitInfo info)
        {
			if (info == null || info.Initiator is not BasePlayer attacker || attacker == null || !attacker.userID.IsSteamId()) return null;
			if (corpse.skinID != 0uL)
            {
				if (corpse.skinID == _bradleySkinId)
                {
                    if (_eventScientistsList.TryGetValue(corpse.net.ID, out var eventData) && !eventData.CanBeAttackedBy(attacker))
                        goto cancel;
                }
                else if (corpse.skinID == _rbPluginID)
                {
                    if (TryGetRaidBase(corpse.transform.position, out var rbData) && !rbData.CanInteractWithRaid(attacker.userID))
                        goto cancel;
                }
                else if (corpse.skinID == _rrPluginID)
                {
                    if (_config.RandomRaids_Enabled && _rrAllRaiders.TryGetValue(corpse.net.ID.Value, out var rrData) && !rrData.PlayersList.Contains(attacker.userID))
                        goto cancel;
                }
            }
			else if (_monumentsList.TryGetValue(GetEntityMonument(corpse), out var monumentData))
			{
				if (monumentData.CanLoot(attacker) != null)
					goto cancel;
			}
			else if (_config.PreventResourceGathering && attacker.BinoMumkin() != null)
				goto cancel;
			return null;
		
		cancel:
			info.Initiator = null;
			info.damageTypes.Clear();
			return null;
		}
		
				void OnEntitySpawned(BuildingBlock block) => UpdateBuildingBlock(block);
		
		object OnPlayerHandcuff(BasePlayer victim, BasePlayer attacker)
        {
			if (victim.userID.IsSteamId() && !IsPlayerInPvP(attacker.userID, victim.userID) && !UrishMumkin(attacker, victim.userID))
            {
				SendMessage(attacker, "MsgCantHandcuffing");
				return false;
			}
			return null;
		}
		private string GetMonumentName(string monumentID, string userID, bool showSuffix = true) => (string)(MonumentsWatcher?.Call(MonumentGetMonumentDisplayName, monumentID, userID, showSuffix) ?? monumentID);
		
		public class EventData
		{
			public ulong ID { get; set; }
			public EventType Type { get; set; }
			
			private ulong _ownerID = 0uL;
			public ulong OwnerID
            {
                get => _ownerID;
                set
                {
                    _ownerID = value;
                    OwnerIDString = _ownerID.ToString();
                }
            }
			public string OwnerIDString { get; private set; } = string.Empty;
			
			public string OwnerName { get; private set; } = string.Empty;
			
			public EventSettings Settings { get; }
			public int DeathCounter { get; set; }
			
			public EventData() {}
			public EventData(ulong entID, EventType type, EventSettings settings)
            {
                ID = entID;
                Type = type;
                Settings = settings;
            }
			
			public void SetNewOwner(ulong userID, string name = "")
			{
				OwnerID = userID;
				OwnerName = !string.IsNullOrWhiteSpace(name) ? name : OwnerIDString;
				DeathCounter = 0;
				Instance.SendMessage(OwnerID, "MsgEventNewLooter", new string[] { Instance.lang.GetMessage($"MsgEvent{Type}", Instance, OwnerIDString), Settings.DeathLimit.ToString() }, false);
			}
			
			public bool CanBeAttackedBy(BasePlayer player) => OwnerID == player.userID || (player.Team != null && player.Team.members.Contains(OwnerID)) || (Instance.Friends != null && Instance.IsFriend(player.UserIDString, OwnerIDString));
			
			public object CanBeTargeted(BasePlayer player)
			{
				if (OwnerID == player.userID || (player.Team != null && player.Team.members.Contains(OwnerID)) || (Instance.Friends != null && Instance.IsFriend(player.UserIDString, OwnerIDString)))
					return null;
				return false;
			}
			
			public void OnLooterDeath()
            {
				if (Settings.DeathLimit < 1) return;
				DeathCounter++;
				if (DeathCounter > Settings.DeathLimit)
				{
					OwnerID = 0uL;
					OwnerName = string.Empty;
					Instance.SendMessage(OwnerID, "MsgEventDeathLimit", new string[] { Instance.lang.GetMessage($"MsgEvent{Type}", Instance, OwnerIDString) });
				}
			}
			
			private void StopFire(Vector3 pos)
            {
				var entList = Pool.Get<List<BaseEntity>>();
                Vis.Entities(pos, 5f, entList);
                if (entList.Any())
                {
					foreach (var entity in entList)
					{
						if (entity is LockedByEntCrate crate)
						{
							if (entity.OwnerID == 0uL)
								entity.OwnerID = OwnerID;
							var lockEnt = crate.lockingEnt.ToBaseEntity();
							if (lockEnt != null)
                                lockEnt.Kill();
                        }
						else if (entity is FireBall fireball)
							fireball.Extinguish();
						else if (entity is HelicopterDebris debris)
						{
							if (entity.OwnerID == 0uL)
								entity.OwnerID = OwnerID;
							debris.tooHotUntil = 0f;
						}
					}
                }
				Pool.FreeUnmanaged(ref entList);
			}
			
			public void OnParentDestroy(Vector3 pos)
            {
				if (BasePlayer.FindByID(OwnerID) is BasePlayer player && player.IsConnected)
				{
					Instance.SendMessage(player.IPlayer, "MsgEventComplete", new string[] { Instance.lang.GetMessage($"MsgEvent{Type}", Instance, OwnerIDString), pos.ToString() }, false);
					player.AddPingAtLocation(BasePlayer.PingType.Loot, pos, 15f, default);
				}
				if (Settings.StopFire)
                    StopFire(pos);
                else
				{
                    var crates = Pool.Get<List<LockedByEntCrate>>();
                    Vis.Entities(pos, 5f, crates);
                    if (crates.Any())
                    {
                        bool smoked = false;
                        foreach (var container in crates)
                        {
                            if (container.OwnerID != 0uL) continue;
							container.OwnerID = OwnerID;
							if (!smoked && container.lockingEnt != null)
                            {
                                Effect.server.Run("assets/bundled/prefabs/fx/smoke_signal_full.prefab", container.lockingEnt.ToBaseEntity(), 0, Vector3.zero, Vector3.zero, null, false);
                                smoked = true;
                            }
                        }
                    }
                    Pool.FreeUnmanaged(ref crates);
                    var debris = Pool.Get<List<HelicopterDebris>>();
                    Vis.Entities(pos, 5f, debris);
					foreach (var container in debris)
                        container.OwnerID = OwnerID;
					Pool.FreeUnmanaged(ref debris);
                }
                Instance._eventsList.Remove(ID);
            }
		}
		
		private void SendMonumentsBar(BasePlayer player, MonumentData monumentData, string subText = "", int displayTime = 0)
        {
            if (!_statusIsLoaded) return;
            if (string.IsNullOrWhiteSpace(subText))
                subText = lang.GetMessage("MsgMonumentNoAccess", this, player.UserIDString);

            var parameters = new Dictionary<int, object>(monumentData.StatusBar)
            {
                { 15, GetMonumentName(monumentData.MonumentID, player.userID, monumentData.Settings.ShowSuffix) },
                { 22, subText }
            };
            if (displayTime > 0)
            {
                parameters[2] = BarTimed;
                parameters.Add(29, _unixSeconds + displayTime);
            }

            AdvancedStatus?.Call(StatusCreateBar, player.userID.Get(), parameters);
        }
		object CanLootEntity(BasePlayer player, FrankensteinTable table) => CanLootStorage(player, table);
		
		private bool TryGetRaidBase(Vector3 pos, out RBData result)
        {
			result = null;
			if (!_rbsConfig.IsEnabled) return false;
			float distance = float.MaxValue;
			foreach (var rbData in _rbList.Values)
            {
				float newDistance = (pos - rbData.Position).sqrMagnitude;
				if (newDistance < distance && newDistance <= rbData.RadiusSquared)
				{
					distance = newDistance;
					result = rbData;
				}
            }
			return result != null;
		}
		private static Dictionary<ulong, VehicleData> _vehiclesList;

		void OnExcavatorSuppliesRequested(ExcavatorSignalComputer computer, BasePlayer player, BaseEntity cargoPlane)
		{
			if (player != null && cargoPlane != null && _monumentsList.TryGetValue(GetPlayerMonument(player.userID), out var monumentData) && !monumentData.IsPvP)
				cargoPlane.OwnerID = player.userID;
		}
		object CanLootEntity(BasePlayer player, Mailbox box) => null;
		
		void OnEntityKill(PatrolHelicopter patrol)
        {
			if (patrol.skinID == _rrPluginID)
            {
				if (_config.RandomRaids_Enabled && _rrallPatrols.TryGetValue(patrol.net.ID.Value, out var rrData))
					rrData.OnPatrolDestroy(patrol.transform.position);
            }
			else if (_eventsList.TryGetValue(patrol.net.ID.Value, out var eventData))
				eventData.OnParentDestroy(patrol.transform.position);
		}
		private const string _rbsUiOfferPath = @"RealPVE\UI\RaidableBasesOffer";
		
		private CuiElementContainer GetDefaultClaimOffer()
        {
            var result = new CuiElementContainer();
            result.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "30 -100", OffsetMax = "360 -40" },
                Image = { Color = "0 0 0 0" }
            }, "Overlay", "{0}");
            result.Add(new CuiElement
            {
                Parent = "{0}",
                Name = "{0}_Image",
                Components =
                {
                    new CuiImageComponent { Color = "0.8 0.9 0.6 0.8", Png = "{1}" },
                    new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "7 -17.5", OffsetMax = "42 17.5" }
                }
            });
            result.Add(new CuiLabel
            {
                Text =
                {
                    Text = "{2}",
                    Font = "RobotoCondensed-Bold.ttf",
                    FontSize = 14,
                    Color = WhiteColor,
                    Align = TextAnchor.UpperLeft,
                    FadeIn = 1f
                },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "50 7", OffsetMax = "-5 -7" }
            }, "{0}", "{0}_Title");
            result.Add(new CuiLabel
            {
                Text =
                {
                    Text = "{3}",
                    Font = "RobotoCondensed-Regular.ttf",
                    FontSize = 12,
                    Color = WhiteColor,
                    Align = TextAnchor.LowerLeft,
                    FadeIn = 1f
                },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "50 7", OffsetMax = "-5 -7" }
            }, "{0}", "{0}_Description");
            result.Add(new CuiButton
            {
                Button =
                {
                    Close = "{0}",
                    Command = "{4}",
                    Color = "0 0 0 0"
                },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
            }, "{0}", "{0}_Button");
			
			return result;
		}
		
		private static RBConfig _rbsConfig;
		
		public class BeachItem
		{
			public string ShortName { get; set; }
			public int Slot { get; set; }
			public int Amount { get; set; }
			public ulong SkinID { get; set; }
			public string Text { get; set; }
			
			public BeachItem() {}
			public BeachItem(string name, int slot = 0, int amount = 1, ulong skinID = 0uL, string text = "")
			{
				ShortName = name;
				Slot = slot;
				Amount = amount;
				SkinID = skinID;
				Text = text;
			}
		}
		private string[] _defaultHooks = new string[] { "CanBypassQueue", "OnPlayerConnected", "OnPlayerDisconnected", "OnPlayerDeath", "OnEntityDeath",
			"OnPlayerRespawned", "OnButtonPress", "OnSwitchToggle", "OnConveyorFiltersChange", "OnExplosiveThrown",
			"OnRocketLaunched", "OnCargoPlaneSignaled", "OnSupplyDropDropped", "OnWakeFrankenstein", "OnSleepFrankenstein",
			"OnPluginLoaded", "OnPluginUnloaded", "OnEntitySpawned", "OnEntityKill", "OnCupboardAuthorize",
			"OnCupboardDeauthorize", "OnCupboardClearList", "CanBuild", "OnStructureUpgrade", "OnStructureRotate",
			"CanDemolish", "CanLock", "CanUnlock", "CanChangeCode", "CanUseLockedEntity",
			"CanAssignBed", "CanRenameBed", "CanAdministerVending", "CanUpdateSign", "OnTurretAuthorize",
			"OnInterferenceUpdate", "OnEntityTakeDamage", "OnDispenserGather", "OnCollectiblePickup", "OnGrowableGather",
			"CanTakeCutting", "OnQuarryToggle", "OnItemPickup", "CanLootPlayer", "CanLootEntity",
			"OnOvenToggle", "OnMixingTableToggle", "OnPlayerDropActiveItem", "OnPlayerDrink", "OnRackedWeaponMount",
			"OnRackedWeaponSwap", "OnRackedWeaponTake", "OnRackedWeaponLoad", "OnRackedWeaponUnload", "OnCrateHack",
			"OnLootEntity", "OnNpcConversationRespond", "CanMountEntity", "CanSwapToSeat", "OnEngineStart",
			"OnVehiclePush", "CanPushBoat", "OnLootEntityEnd", "OnRidableAnimalClaim", "OnHorseLead",
			"OnPlayerLootEnd", "OnVehicleLockRequest", "OnCodeChange", "OnLockRemove", "CanDestroyLock",
			"OnVehicleModuleMove", "OnEntityEnter", "OnHotAirBalloonToggle", "OnTeamCreated", "OnTeamDisbanded",
			"OnTeamAcceptInvite", "OnTeamLeave", "OnTeamKick", "OnFriendAdded", "OnFriendRemoved",
			"CanBeTargeted", "OnSamSiteTarget", "OnNpcTargetSense", "OnPlayerPveDamage", "OnPlayerCorpseSpawned",
			"OnPlayerEnterPVP", "OnPlayerExitPVP", "OnEntityEnterPVP", "OnEntityExitPVP", "OnEventJoin",
			"OnEventJoined", "OnEventLeave", "OnZoneStatusText", "CreatePVPMapMarker", "DeletePVPMapMarker" };
		
		private void InitRaidableBases()
		{
			if (!_rbsConfig.IsEnabled) return;
			
			LoadRBsImages();
			var rbsList = RaidableBases?.Call("GetAllEvents") as List<(Vector3 pos, int mode, bool allowPVP, string a, float b, float c, float loadTime, ulong ownerID, BasePlayer owner, List<BasePlayer> raiders, List<BasePlayer> intruders, HashSet<BaseEntity> entities, string baseName, DateTime spawnDateTime, DateTime despawnDateTime, float radius, int lootRemain)>;
			if (rbsList != null && rbsList.Any())
            {
				string raidID;
				foreach (var rbInfo in rbsList)
				{
					if (rbInfo.mode != (int)RaidableMode.Disabled)
					{
						raidID = rbInfo.pos.ToString();
						_rbList[raidID] = new RBData(raidID, rbInfo.pos, rbInfo.mode, rbInfo.allowPVP, rbInfo.radius, rbInfo.ownerID, rbInfo.despawnDateTime, rbInfo.lootRemain, rbInfo.intruders);
					}
                }
			}
			Subscribe(nameof(OnPlayerEnteredRaidableBase));
            Subscribe(nameof(OnPlayerExitedRaidableBase));
			Subscribe(nameof(OnRaidableLootDestroyed));
			Subscribe(nameof(OnRaidableDespawnUpdate));
			Subscribe(nameof(OnRaidableBasePurchased));
            Subscribe(nameof(OnRaidableBaseStarted));
            Subscribe(nameof(OnRaidableBaseEnded));
		}
		private float _defaultPatrolEscapeDamage = 0.35f;
		
		void OnPortalUsed(BasePlayer player, HalloweenDungeon halloween) => OnUsedPortal(player, halloween);
		
                private void LoadLibImages()
		{
			var imgList = new Dictionary<string, string>
			{
				{ MonumentOfferUI, "https://i.imgur.com/4Adzkb8.png" },
				{ EventOfferUI, "https://i.imgur.com/4Adzkb8.png" },
				{ RBOfferUI, "https://i.imgur.com/4Adzkb8.png" }
			};
			
			BarSettings barSettings = _config.BarPvP;
			if (string.IsNullOrWhiteSpace(barSettings.Image_Sprite) && string.IsNullOrWhiteSpace(barSettings.Image_Local) && barSettings.Image_Url.StartsWithAny(HttpScheme))
				imgList.Add(Bar_PvP, barSettings.Image_Url);
			foreach (var kvp in _monumentsConfig.MonumentsSettings)
            {
				barSettings = kvp.Value.Bar;
				if (string.IsNullOrWhiteSpace(barSettings.Image_Sprite) && string.IsNullOrWhiteSpace(barSettings.Image_Local) && barSettings.Image_Url.StartsWithAny(HttpScheme))
					imgList.Add($"{StatusBarID}{kvp.Key}", barSettings.Image_Url);
			}
			foreach (var kvp in _rbsConfig.Settings)
			{
				barSettings = kvp.Value.Bar;
				if (string.IsNullOrWhiteSpace(barSettings.Image_Sprite) && string.IsNullOrWhiteSpace(barSettings.Image_Local) && barSettings.Image_Url.StartsWithAny(HttpScheme))
					imgList.Add($"{RBUI}_{kvp.Key}", barSettings.Image_Url);
			}
			
			ImageLibrary?.Call("ImportImageList", Name, imgList, 0uL, true);
		}

        
		object OnRidableAnimalClaim(BaseRidableAnimal animal, BasePlayer player)
		{
			NextTick(() => { DestroyVehiclePanels(player); });
			return _vehiclesList[animal.net.ID.Value].AssignNewOwner(player);
		}
		
		public class PlayerPvP
        {
			public double DelayEnd { get; set; } = 0d;
			public List<string> ActiveZones { get; set; } = new List<string>();
			public string LastZone { get; set; } = string.Empty;
		}
		public static Dictionary<ulong, RRData> _rrAllRaiders = new Dictionary<ulong, RRData>();

		private void UpdateBuildingBlock(StabilityEntity stability)
		{
			if (stability != null && stability.OwnerID.IsSteamId())
			{
				stability.CancelInvoke(stability.StopBeingDemolishable);
				stability.SetFlag(BaseEntity.Flags.Reserved2, true);
			}
		}
		
		void OnScientistInitialized(BradleyAPC bradley, ScientistNPC scientist, Vector3 spawnPos)
        {
			if (bradley.skinID == 0uL && _eventsList.TryGetValue(bradley.net.ID.Value, out var eventData))
			{
				_eventScientistsList[scientist.net.ID] = eventData;
				scientist.skinID = _bradleySkinId;
			}
		}
				
				private const string StatusBarID = "RealPVE_Bar_", BarTimed = "Timed", BarTimeCounter = "TimeCounter", StatusCreateBar = "CreateBar", StatusDeleteBar = "DeleteBar", StatusDeleteAllPluginBars = "DeleteAllPluginBars";

        void OnCreatedDynamicPVP(string zoneID, string eventName, Vector3 pos, float duration)
        {
			_dynamicPvPs.Add(zoneID);
			
			var entities = ZoneManager?.Call("GetEntitiesInZone", zoneID) as List<BaseEntity>;
			if (entities != null && entities.Any())
            {
				foreach (var entity in entities)
                {
                    if (entity is not BasePlayer)
                        OnEntityEnterPVP(entity);
                }
            }
			
			string monumentID = GetMonumentByPos(pos);
            if (_monumentsList.TryGetValue(monumentID, out var monumentData) && monumentData.SetAsPvP(false))
            {
				if (!_pvpChangedMonuments.TryGetValue(monumentID, out var zones))
                    _pvpChangedMonuments[monumentID] = zones = new HashSet<string>();
                zones.Add(zoneID);
            }
			
			if (_config.PvPMapMarkers)
				CreatePVPMapMarker(zoneID, pos, (float)(ZoneManager?.Call("GetZoneRadius", zoneID) ?? 0.25f), _config.PvPMapMarkersName);
		}
		
		public enum EventType
        {
			PatrolHelicopter,
			BradleyAPC
		}
		object OnEntityTakeDamage(BaseVehicleModule module, HitInfo info) => CanModuleTakeDamage(module, info);
		void OnCargoPlaneSignaled(CargoPlane plane, SupplySignal signal) => plane.OwnerID = signal.OwnerID;
		
		
		
		void OnBuildingSplit(BuildingManager.Building building, uint newBuildingId)
		{
			var oldID = building.ID;
			foreach (var rrData in _randomRaidsList.Values)
            {
				if (rrData.BuildingIDs.Contains(oldID))
                {
					rrData.BuildingIDs.Add(newBuildingId);
					break;
				}
			}
		}
		object OnPortalUse(BasePlayer player, XmasDungeon xmas) => CanUsePortal(player, xmas);

		object CanLootEntity(BasePlayer player, StorageContainer container)
        {
			if (_unrestrictedLooters.Contains(player.userID)) return AdminOpenLoot(player, container);
			if (IsEntityInPvP(player.userID, container.net.ID.Value)) return null;

			
			

			var parentEnt = container.GetParentEntity();
			ulong parentID = 0uL;
			if (parentEnt is BaseVehicleModule module)
				parentID = module.VehicleParent()?.net.ID.Value ?? parentID;
			else if (parentEnt != null)
				parentID = parentEnt.net.ID.Value;
			if (_vehiclesList.TryGetValue(parentID, out var vehicleData))
			{
				object result = vehicleData.CanLoot(player);
				if (result != null)
					SendMessage(player, "MsgVehicleCanNotInteract");
				return result;
			}
			return CanLootStorage(player, container, true);
		}
		
		private void LoadRBsImages()
		{
			if (_statusIsLoaded)
			{
				var imgList = new HashSet<string>();
				foreach (var rbSettings in _rbsConfig.Settings.Values)
                {
					if (!string.IsNullOrWhiteSpace(rbSettings.Bar.Image_Local))
						imgList.Add(rbSettings.Bar.Image_Local);
				}
				if (imgList.Any())
					AdvancedStatus?.Call("LoadImages", imgList);
			}
			if (_imgLibIsLoaded)
			{
				var imgList = new Dictionary<string, string>();
				BarSettings barSettings;
				foreach (var kvp in _rbsConfig.Settings)
                {
                    barSettings = kvp.Value.Bar;
                    if (string.IsNullOrWhiteSpace(barSettings.Image_Sprite) && string.IsNullOrWhiteSpace(barSettings.Image_Local) && barSettings.Image_Url.StartsWithAny(HttpScheme))
                        imgList.Add($"{RBUI}_{kvp.Key}", barSettings.Image_Url);
                }
				if (imgList.Any())
					ImageLibrary?.Call("ImportImageList", Name, imgList, 0uL, true);
			}
		}
		
		object OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
			if (dispenser?.baseEntity is HelicopterDebris debris && debris != null && !_unrestrictedLooters.Contains(player.userID) && !IsEntityInPvP(player.userID, debris.net.ID.Value))
			{
                object result = player.MumkinNol(debris.OwnerID);
                if (result != null && Friends != null && IsFriend(player.UserIDString, debris.OwnerID.ToString()))
                    result = null;
                if (result != null)
                    SendMessage(player, "MsgCantInteract");
                return result;
            }
            return null;
        }

		object CanLootEntity(BasePlayer player, BuildingPrivlidge privlidge) => CanLootStorage(player, privlidge);
		
		void OnLootEntity(BasePlayer player, ModularCarGarage garage)
		{
			if (GetVehicleData(garage.carOccupant, out var vehicleData))
				ShowVehiclePanels(player, vehicleData);
		}

		private object CanLootWeaponRack(BasePlayer player, WeaponRack rack)
		{
			if (!_unrestrictedLooters.Contains(player.userID) && !IsEntityInPvP(player.userID, rack.net.ID.Value))
			{
				object result = player.TasirMumkin(rack.OwnerID);
				if (result != null)
					SendMessage(player, "MsgCantInteractWeaponRack");
				return result;
			}
			return null;
		}
		
		static bool OnEntityEnterPVP(BaseEntity entity, string zoneID = "")
        {
			if (entity != null && entity.net != null)
				return _pvpEntities.Add(entity.net.ID.Value);
			return false;
		}
		private bool IsPlayerInMonument(string monumentID, BasePlayer player) => (bool)(MonumentsWatcher?.Call(MonumentIsPlayerInMonument, monumentID, player) ?? false);

		object OnExcavatorResourceSet(ExcavatorArm arm, string resourceName, BasePlayer player)
		{
			if (player != null && !_unrestrictedLooters.Contains(player.userID) && _monumentsList.TryGetValue(GetPlayerMonument(player.userID), out var monumentData))
			{
				object result = monumentData.CanLoot(player);
				if (result != null)
					SendMessage(player, "MsgCantInteract");
				return result;
			}
			return null;
		}
		object OnCodeChange(ModularCar modularCar, BasePlayer player, string newPass) => false;
		
		public class EventSettings
        {
			public bool IsEnabled { get; set; } = true;
			
			[JsonProperty(PropertyName = "Time in seconds (1-15) given to respond for purchasing this event. Note: This is shown to everyone who deals damage, and the first person to buy it will claim it")]
            public float OfferTime { get; set; } = 5f;
			
			[JsonProperty(PropertyName = "Is it worth removing fire from crates?")]
			public bool StopFire { get; set; } = true;
			
			[JsonProperty(PropertyName = "The price to capture the event. 0 means the event is free")]
			public double Price { get; set; } = 50d;
			
			[JsonProperty(PropertyName = "The number of deaths after which the event becomes public")]
			public int DeathLimit { get; set; } = 5;
		}
		
		void OnEntityExitZone(string zoneID, BaseEntity entity)
        {
			if (_dynamicPvPs.Contains(zoneID))
				OnEntityExitPVP(entity);
		}
		object OnEntityTakeDamage(VehicleModuleTaxi module, HitInfo info) => CanModuleTakeDamage(module, info);
		
                object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
		{
			if (info == null || info.damageTypes.GetMajorityDamageType() == DamageType.Decay || info.Initiator is not BasePlayer attacker || attacker == null ||
				IsEntityInPvP(attacker.userID, entity.net.ID.Value)) return null;
			if (entity.OwnerID.IsSteamId())
			{
				if (!attacker.userID.IsSteamId())
				{
					if (attacker.skinID == _rrPluginID)
					{
						if (_config.RandomRaids_Enabled && _rrAllRaiders.TryGetValue(attacker.net.ID.Value, out var rrData) && !rrData.PlayersList.Contains(entity.OwnerID))
							goto cancel;
					}
				}
				else if (!UrishMumkin(attacker, entity.OwnerID))
					goto cancel;
			}
			else if (attacker.userID.IsSteamId())
			{
				if (TryGetRaidBase(entity.transform.position, out var rbData))
				{
					if (!rbData.CanInteractWithRaid(attacker.userID))
						goto cancel;
				}
				else if (_monumentsList.TryGetValue(GetEntityMonument(entity), out var monumentData))
				{
					if (monumentData.CanLoot(attacker) != null)
						goto cancel;
				}
			}
			return null;
		
		cancel:
			info.Initiator = null;
			info.damageTypes.Clear();
			return null;
		}
		
				private void InitPermissions()
		{
			foreach (var perm in _permissionsConfig.PermissionsList)
				permission.RegisterPermission(perm.Name, this);
			ConVar.Server.max_sleeping_bags = _permissionsConfig.PermissionsList.Max(p => p.Beds);
			LegacyShelter.max_shelters = _permissionsConfig.PermissionsList.Max(p => p.Shelters);
			ConVar.Sentry.maxinterference = _permissionsConfig.PermissionsList.Max(p => p.Turrets);
		}
		object OnRackedWeaponTake(Item item, BasePlayer player, WeaponRack rack) => CanLootWeaponRack(player, rack);
		
		object OnWakeFrankenstein(FrankensteinTable table, BasePlayer player)
        {
			if (_unrestrictedLooters.Contains(player.userID)) return null;
			object result = null;
			if (table.OwnerID.IsSteamId())
				result = player.TasirMumkin(table.OwnerID);
			if (result != null)
                SendMessage(player, "MsgCantInteract");
            return result;
        }
		
		private int CountVehiclesByType(ulong userID, VehicleType type)
        {
			int result = 0;
			foreach (var vehicle in _vehiclesList.Values)
            {
				if (vehicle.OwnerID == userID && vehicle.Type == type)
					result++;
			}
			return result;
		}
		
		void OnRandomRaidStart(string waveType, Vector3 pos)
		{
			var tcList = Pool.Get<List<BuildingPrivlidge>>();
			Vis.Entities(pos, 1f, tcList);
			if (tcList.Any())
                _randomRaidsList[pos.ToString()] = new RRData(tcList[0]);
			Pool.FreeUnmanaged(ref tcList);
		}

		object CanLootEntity(BasePlayer player, Workbench workbench) => null;
		
		
		private static void Urish(BaseCombatEntity entity, HitInfo info)
		{
			if (entity == null || info == null || entity.IsDead()) return;
			using (TimeWarning.New("Hurt( HitInfo )", 50))
			{
				float originalHealth = entity.health;
				entity.ScaleDamage(info);
				if (info.PointStart != Vector3.zero)
				{
					for (int i = 0; i < entity.propDirection.Length; i++)
					{
						if (!(entity.propDirection[i].extraProtection == null) && !entity.propDirection[i].IsWeakspot(entity.transform, info))
							entity.propDirection[i].extraProtection.Scale(info.damageTypes);
					}
				}

				info.damageTypes.Scale(DamageType.Arrow, ConVar.Server.arrowdamage);
				info.damageTypes.Scale(DamageType.Bullet, ConVar.Server.bulletdamage);
				info.damageTypes.Scale(DamageType.Slash, ConVar.Server.meleedamage);
				info.damageTypes.Scale(DamageType.Blunt, ConVar.Server.meleedamage);
				info.damageTypes.Scale(DamageType.Stab, ConVar.Server.meleedamage);
				info.damageTypes.Scale(DamageType.Bleeding, ConVar.Server.bleedingdamage);
				if (entity is not BasePlayer)
					info.damageTypes.Scale(DamageType.Fun_Water, 0f);
				
				float damageAmount = info.damageTypes.Total();
				entity.SetHealth(originalHealth - info.damageTypes.Total());
				if (ConVar.Global.developer > 1)
					Debug.Log("[Combat]".PadRight(10) + entity.gameObject.name + " hurt " + info.damageTypes.GetMajorityDamageType().ToString() + "/" + damageAmount + " - " + entity.health.ToString("0") + " health left");
				
				entity.lastDamage = info.damageTypes.GetMajorityDamageType();
				entity.lastAttacker = info.Initiator;
				if (entity.lastAttacker != null)
				{
					var baseCombatEntity = entity.lastAttacker as BaseCombatEntity;
					if (baseCombatEntity != null)
					{
						baseCombatEntity.lastDealtDamageTime = UnityEngine.Time.time;
						baseCombatEntity.lastDealtDamageTo = entity;
					}
					if (entity.IsValid())
						(entity.lastAttacker as BasePlayer)?.ProcessMissionEvent(BaseMission.MissionEventType.HURT_ENTITY, entity.net.ID, damageAmount);
				}
				
				var baseCombatEntity2 = entity.lastAttacker as BaseCombatEntity;
				if (entity.markAttackerHostile && baseCombatEntity2 != null && baseCombatEntity2 != entity)
					baseCombatEntity2.MarkHostileFor();
				
				if (entity.lastDamage.IsConsideredAnAttack())
				{
					entity.SetJustAttacked();
					if (entity.lastAttacker != null)
						entity.LastAttackedDir = (entity.lastAttacker.transform.position - entity.transform.position).normalized;
				}

				bool flag = entity.Health() <= 0f;
				Facepunch.Rust.Analytics.Azure.OnEntityTakeDamage(info, flag);
				if (flag)
					entity.Die(info);

				var initiatorPlayer = info.InitiatorPlayer;
				if (initiatorPlayer != null)
				{
					if (entity.IsDead())
						initiatorPlayer.stats.combat.LogAttack(info, "killed", originalHealth);
					else
						initiatorPlayer.stats.combat.LogAttack(info, string.Empty, originalHealth);
				}
			}
		}

		void OnServerInitialized(bool initial)
		{
			_unixSeconds = Network.TimeEx.currentTimestamp;
			UnityEngine.Application.logMessageReceived += HookConflict;
			UnityEngine.Application.logMessageReceived -= Facepunch.Output.LogHandler;
			_defaultBeds = ConVar.Server.max_sleeping_bags;
			_defaultShelters = LegacyShelter.max_shelters;
			_defaultTurrets = ConVar.Sentry.maxinterference;
			_defaultPatrolCrash = PatrolHelicopterAI.monument_crash;
			_defaultPatrolDangerZone = PatrolHelicopterAI.use_danger_zones;
			_defaultPatrolEscapeDamage = PatrolHelicopterAI.flee_damage_percentage;
			if (string.IsNullOrWhiteSpace(_config.WipeID) || _config.WipeID != SaveRestore.WipeId)
			{
				_config.WipeID = SaveRestore.WipeId;
				_vehiclesList.Clear();
				_teamsList.Clear();
				PrintWarning("Wipe detected! Stored data was reset!");
				SaveConfig();
				SaveData(_dataVehiclesPath, _vehiclesList);
				SaveData(_dataTeamsPath, _teamsList);
			}
			_pvpPlayers = new Dictionary<ulong, PlayerPvP>();
			_pvpEntities = new HashSet<ulong>();
			InitPermissions();
			foreach (var player in BasePlayer.activePlayerList)
			{
				if (player.userID.IsSteamId())
					_playerUI[player.userID] = new HashSet<string>();
			}
			if (_config.Force_PvE && !ConVar.Server.pve)
			{
				ConVar.Server.pve = true;
				PrintWarning("The PVE settings were forcibly enabled.");
			}
			_imgLibIsLoaded = ImageLibrary != null && ImageLibrary.IsLoaded;
			if (_imgLibIsLoaded)
				LoadLibImages();
			if (AdvancedStatus != null && AdvancedStatus?.Call("IsReady") != null)
				OnAdvancedStatusLoaded();
			Subscribe(nameof(OnAdvancedStatusLoaded));
			_economicsIsLoaded = Economics != null && Economics.IsLoaded;
			if (MonumentsWatcher != null && MonumentsWatcher.IsLoaded)
				OnMonumentsWatcherLoaded();
			Subscribe(nameof(OnMonumentsWatcherLoaded));
			InitVehicles();
			InitTeams();
			InitVanillaEvents();
			
			UpdatePvPBars();
			
			if (RaidableBases != null && RaidableBases.IsLoaded)
				InitRaidableBases();
			
			foreach (var entity in BaseNetworkable.serverEntities)
			{
				if (entity is BuildingBlock block)
					UpdateBuildingBlock(block);
				else if (entity is StabilityEntity stability)
					UpdateBuildingBlock(stability);
				else if (_config.RecyclerNoPenalties && entity is Recycler recycler)
					recycler.SetFlag(BaseEntity.Flags.Reserved9, false);
            }
			if (_config.PreventGiveMessage)
				Subscribe(nameof(OnServerMessage));
			if (_config.PreventHandcuffing)
				Subscribe(nameof(OnPlayerHandcuff));
			if (_config.AssignPortals)
			{
				Subscribe(nameof(OnPortalUse));
				Subscribe(nameof(OnPortalUsed));
			}
			if (_beachConfig.Respawn_Override)
				Subscribe(nameof(OnDefaultItemsReceive));
			if (_config.RandomRaids_Enabled && RandomRaids)
			{
				Subscribe(nameof(OnRandomRaidStart));
				Subscribe(nameof(RandomRaidEventEnd));
				Subscribe(nameof(OnRandomRaidRaiderSpawned));
				Subscribe(nameof(OnRandomRaidHeliSpawned));
				Subscribe(nameof(OnRandomRaidWin));
				
				Subscribe(nameof(OnBuildingSplit));
			}
			if (DynamicPVP != null && DynamicPVP.IsLoaded)
				InitDynamicPVP();
			if (_config.PreventBackpackDrop)
				Subscribe(nameof(OnBackpackDrop));
			if (_config.PreventLaptopAttack)
				Subscribe(nameof(OnCrateLaptopAttack));
			
			for (int i = 0; i < _defaultHooks.Length; i++)
				Subscribe(_defaultHooks[i]);
			_defaultHooks = null;
			
			_updatesTimer = timer.Every(1f, CheckForUpdates);
			
			if (!_economicsIsLoaded)
				PrintWarning("Economy plugin not found! For enhanced functionality, it is recommended to install it!\nhttps://umod.org/plugins/economics");
			if (!_watcherIsLoaded)
				PrintWarning("MonumentsWatcher plugin not found! MonumentsWatcher is required to work with monuments!\nhttps://codefling.com/plugins/monuments-watcher");
			if (!_statusIsLoaded)
			{
				if (initial && AdvancedStatus != null)
					PrintWarning("AdvancedStatus plugin found, but not ready yet. Waiting for it to load...");
				else
					PrintWarning("AdvancedStatus plugin not found! AdvancedStatus is required to work with status bars!\nhttps://codefling.com/plugins/advanced-status");
			}
			PrintError($"{Title} has been successfully loaded! If you encounter any issues, please create a thread in the support section on the plugin's page.");
		}
		
		object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
			var player = target.player;
			if (player != null)
            {
				if (!_pvpPlayers.ContainsKey(player.userID) && player.BinoMumkin() != null && (!TryGetRaidBase(target.entity.transform.position, out var rbData) || (!rbData.CanInteractWithRaid(player.userID) && prefab.prefabID != 2150203378u)))
                {
					player.ShowToast(GameTip.Styles.Error, ConstructionErrors.NoPermission, false);
                    return false;
                }
				
				if (prefab.isSleepingBag)
                {
                    int limit = GetBedsLimit(player.UserIDString);
					int total = CountBeds(player.userID);
                    if (total >= limit)
                    {
                        player.ShowToast(GameTip.Styles.Error, SleepingBag.bagLimitReachedPhrase, false);
                        return false;
                    }
					NextTick(() => { CheckIfPlaced(player, total, limit, true); });
				}
                else if (prefab.prefabID == 2243018404u)
                {
                    int limit = GetSheltersLimit(player.UserIDString);
					int total = LegacyShelter.GetShelterCount(player.userID);
                    if (total >= limit)
                    {
                        player.ShowToast(GameTip.Styles.Error, LegacyShelter.shelterLimitReachedPhrase, false);
                        return false;
                    }
					NextTick(() => { CheckIfPlaced(player, total, limit, false); });
				}
			}
			return null;
		}
		
		public class ProgressBarSettings
        {
			[JsonProperty(PropertyName = "Main_Color(Hex or RGBA)")]
            public string Main_Color { get; set; } = "1 1 1 0.15";
			
			public float Main_Transparency { get; set; } = 0.15f;
			public bool Progress_Reverse { get; set; } = true;
			public string Progress_Color { get; set; } = "#FFBF99";
            public float Progress_Transparency { get; set; } = 0.7f;
            public string Progress_OffsetMin { get; set; } = "0 0";
            public string Progress_OffsetMax { get; set; } = "0 0";
        }
		
		
		private object CanModuleTakeDamage(BaseVehicleModule module, HitInfo info)
		{
			ulong moduleID = module.VehicleParent()?.net?.ID.Value ?? 0uL;
			if (moduleID == 0uL)
				moduleID = module.net?.ID.Value ?? 0uL;
			return CanVehicleTakeDamage(moduleID, info);
		}
		
		void OnEntityEnteredMonument(string monumentID, BaseEntity entity, string type, string oldMonumentID)
        {
			if (_monumentsConfig.TrackedTypes.Contains(type) && _monumentsList.TryGetValue(monumentID, out var monumentData) && monumentData.IsPvP)
				OnEntityEnterPVP(entity);
		}
		
		object OnNpcTargetSense(NPCPlayer npc, BasePlayer target, AIBrainSenses npcBrain)
        {
			if (npc.skinID == _rbPluginID)
			{
				if (TryGetRaidBase(npc.transform.position, out var rbData) && !rbData.CanInteractWithRaid(target.userID))
					return false;
			}
			else if (npc.skinID == _rrPluginID)
			{
				if (_config.RandomRaids_Enabled && _rrAllRaiders.TryGetValue(npc.net.ID.Value, out var rrData) && !rrData.PlayersList.Contains(target.userID))
					return false;
			}
			return null;
		}

		private string GetMonumentType(string monumentID) => (string)(MonumentsWatcher?.Call(MonumentGetMonumentType, monumentID) ?? string.Empty);

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try { _config = Config.ReadObject<Configuration>(); }
			catch (Exception ex) { PrintError($"{ex.Message}\n\n[{Title}] Your configuration file contains an error."); }
			if (_config == null || _config.Version == new VersionNumber())
			{
				PrintWarning("The configuration file is not found or contains errors. Creating a new one...");
				LoadDefaultConfig();
			}
			else if (_config.Version < Version)
			{
				PrintWarning($"Your configuration file version({_config.Version}) is outdated. Updating it to {Version}.");
				_config.Version = Version;
				PrintWarning($"The configuration file has been successfully updated to version {_config.Version}!");
            }
			
			if (_config.BarPvP == null)
			{
				_config.BarPvP = new BarSettings()
                {
					Order = 9,
					Main_Color = "1 0.39 0.28 0.7",
					Image_Url = "https://i.imgur.com/oi5vIkk.png",
					Image_Local = "RealPVE_PvP",
					Image_Color = "1 0.39 0.28 1"
				};
			}
			
			if (_config.ProgressBarPvP == null)
				_config.ProgressBarPvP = new ProgressBarSettings() { Progress_Color = "#FF6347" };
			
			var barSettings = _config.BarPvP;
			_pvpBar = new Dictionary<int, object>
			{
				{ 1, Name },
				{ 2, "Default" },
				{ 3, "PvP" },
                { 4, barSettings.Order },
                { 5, barSettings.Height },
                { 6, barSettings.Main_Color },
				{ 11, barSettings.Image_IsRawImage },
                { 12, barSettings.Image_Color },
				{ 16, barSettings.Text_Size },
                { 17, barSettings.Text_Color },
                { 18, barSettings.Text_Font },
				{ 23, barSettings.SubText_Size },
                { 24, barSettings.SubText_Color },
                { 25, barSettings.SubText_Font }
            };
			if (barSettings.Main_Color.StartsWith("#"))
				_pvpBar.Add(-6, barSettings.Main_Transparency);
			if (!string.IsNullOrWhiteSpace(barSettings.Main_Material))
				_pvpBar.Add(7, barSettings.Main_Material);
			if (barSettings.Image_Color.StartsWith("#"))
				_pvpBar.Add(-12, barSettings.Image_Transparency);
			if (barSettings.Image_Outline_Enabled)
            {
				_pvpBar.Add(13, barSettings.Image_Outline_Color);
				if (barSettings.Image_Outline_Color.StartsWith("#"))
					_pvpBar.Add(-13, barSettings.Image_Outline_Transparency);
				_pvpBar.Add(14, barSettings.Image_Outline_Distance);
            }
            if (barSettings.Text_Outline_Enabled)
            {
				_pvpBar.Add(20, barSettings.Text_Outline_Color);
				if (barSettings.Text_Outline_Color.StartsWith("#"))
					_pvpBar.Add(-20, barSettings.Text_Outline_Transparency);
				_pvpBar.Add(21, barSettings.Text_Outline_Distance);
            }
            if (barSettings.SubText_Outline_Enabled)
            {
				_pvpBar.Add(26, barSettings.SubText_Outline_Color);
				if (barSettings.SubText_Outline_Color.StartsWith("#"))
					_pvpBar.Add(-26, barSettings.SubText_Outline_Transparency);
				_pvpBar.Add(27, barSettings.SubText_Outline_Distance);
            }
			
			SaveConfig();
		}
		object CanUpdateSign(BasePlayer player, Signage sign) => !sign.OwnerID.IsSteamId() || IsEntityInPvP(player.userID, sign.net.ID.Value) ? null : player.TasirMumkin(sign.OwnerID);
		void OnTeamDisbanded(RelationshipManager.PlayerTeam team) => _teamsList.Remove(team.teamID);

		private int GetBedsLimit(string userID)
		{
			int result = int.MinValue;
			foreach (var perm in _permissionsConfig.PermissionsList)
			{
				if (perm.Beds > result && permission.UserHasPermission(userID, perm.Name))
					result = perm.Beds;
			}
			return result == int.MinValue ? _permissionsConfig.PermissionsList[0].Beds : result;
		}
		
		private void ShowMonumentOffer(BasePlayer player, MonumentData monumentData)
		{
			DestroyUI(player, MonumentOfferUI);
			player.SendEffect();
			CuiHelper.AddUi(player, ReplacePlaceholders(_monumentsUiOffer, null, (string)ImageLibrary?.Call("GetImage", MonumentOfferUI),
				string.Format(lang.GetMessage("MsgMonumentOfferTitle", this, player.UserIDString), new string[] { GetMonumentName(monumentData.MonumentID, player.userID) }),
				string.Format(lang.GetMessage("MsgMonumentOfferDescription", this, player.UserIDString), new string[] { string.Format(_config.PriceFormat, monumentData.Settings.Price.ToString()) }),
				$"{_commandUI} monument pay {monumentData.MonumentID}"));
			_playerUI[player.userID].Add(MonumentOfferUI);
		}

		void OnPluginLoaded(Plugin plugin)
		{
			if (plugin == ImageLibrary)
			{
				_imgLibIsLoaded = true;
				LoadLibImages();
				foreach (var monumentData in _monumentsList.Values)
					monumentData.UpdateBars();
				UpdatePvPBars();
				foreach (var rbData in _rbList.Values)
					rbData.UpdateBars();
			}
			else if (plugin == Economics)
				_economicsIsLoaded = Economics != null && Economics.IsLoaded;
			else if (plugin == RaidableBases)
				InitRaidableBases();
			else if (plugin == RandomRaids)
			{
				if (!_config.RandomRaids_Enabled) return;
				Subscribe(nameof(OnRandomRaidStart));
				Subscribe(nameof(RandomRaidEventEnd));
				Subscribe(nameof(OnRandomRaidRaiderSpawned));
				Subscribe(nameof(OnRandomRaidHeliSpawned));
				Subscribe(nameof(OnRandomRaidWin));
				
				Subscribe(nameof(OnBuildingSplit));
			}
			else if (plugin == DynamicPVP)
				InitDynamicPVP();
		}
		object CanLootEntity(BasePlayer player, FlameTurret flameTurret) => CanLootStorage(player, flameTurret);
		private const string EventOfferUI = "RealPVE_EventOffer";

		private object CanLootCombatEntity(BasePlayer player, BaseCombatEntity combatEntity, ulong playerSteamID = 0uL)
        {
			if (!_unrestrictedLooters.Contains(player.userID) && !IsEntityInPvP(player.userID, combatEntity.net.ID.Value))
			{
				object result = null;
				if (playerSteamID.IsSteamId())
					result = player.TasirMumkin(playerSteamID);
				else if (combatEntity.skinID == _rbPluginID)
				{
					if (TryGetRaidBase(combatEntity.transform.position, out var rbData) && !rbData.CanInteractWithRaid(player.userID))
						result = false;
				}
				else if (combatEntity.skinID == _rrPluginID)
				{
					if (_config.RandomRaids_Enabled && _rrAllRaiders.TryGetValue(combatEntity.net.ID.Value, out var rrData) && !rrData.PlayersList.Contains(player.userID))
						result = false;
				}
				else if (_monumentsList.TryGetValue(GetPlayerMonument(player.userID), out var monumentData))
					result = monumentData.CanLoot(player);
				if (result != null)
					SendMessage(player, "MsgCantInteract");
				return result;
			}
			return null;
		}

		private int CountBeds(ulong targetID)
		{
			int result = 0;
			foreach (var sleepingBag in SleepingBag.sleepingBags)
			{
				if (sleepingBag.deployerUserID == targetID)
					result++;
			}
			return result;
		}
		
		
		object OnVehicleModuleMove(BaseVehicleModule moduleForItem, ModularCar car, BasePlayer player)
		{
			if (GetVehicleData(car, out var vehicleData) && vehicleData.OwnerID != 0uL)
			{
				if (!vehicleData.IsOwner(player.userID))
					return false;
				NextTick(() => { if (!car.HasDriverMountPoints()) vehicleData.RemoveOwnerServerSide(showButtons: true); });
			}
			return null;
		}
		
		void OnLootEntity(BasePlayer player, RidableHorse horse)
		{
			if (horse.HasSaddle() && _vehiclesList.ContainsKey(horse.net.ID.Value))
				ShowVehiclePanels(player, _vehiclesList[horse.net.ID.Value]);
		}
		object CanLootEntity(BasePlayer player, IOEntity entity) => CanLootByOwnerID(player, entity);
		object CanLootEntity(BasePlayer player, HitchTrough hitch) => CanLootStorage(player, hitch);
		
		void OnEntityKill(HotAirBalloon balloon)
        {
			OnEntityExitPVP(balloon);
			if (_vehiclesList.TryGetValue(balloon.net.ID.Value, out var vehicleData))
				vehicleData.OnDestroy();
		}
		object CanLootEntity(BasePlayer player, ContainerIOEntity container) => CanLootByOwnerID(player, container);
        private const string _permissionsPath = @"RealPVE\PermissionConfig";
		
		public class RRData
		{
			public BuildingPrivlidge Privlidge { get; set; }
			public HashSet<uint> BuildingIDs = Pool.Get<HashSet<uint>>();
			public HashSet<ulong> Raiders = Pool.Get<HashSet<ulong>>();
			public ulong PatrolID = 0;
			public List<ulong> PlayersList = Pool.Get<List<ulong>>();
			
			public RRData(BuildingPrivlidge privlidge)
			{
				Privlidge = privlidge;
				UpdateBuildings();
				foreach (var nameID in privlidge.authorizedPlayers)
                {
					_rrAllPlayers[nameID.userid] = this;
					PlayersList.Add(nameID.userid);
				}
			}
			
			public void UpdateBuildings()
			{
				BuildingIDs.Clear();
				BuildingIDs.Add(Privlidge.buildingID);
				var build = Privlidge.GetBuilding();
				if (build == null || build.buildingBlocks == null) return;
				var fList = Pool.Get<List<BuildingBlock>>();
				foreach (var block in build.buildingBlocks)
				{
					if (block.ShortPrefabName != "foundation") continue;
					Vis.Entities(block.transform.position, 18f, fList);
					foreach (var subBlock in fList)
					{
						if (subBlock.ShortPrefabName == "foundation")
							BuildingIDs.Add(subBlock.buildingID);
					}
					fList.Clear();
				}
				Pool.FreeUnmanaged(ref fList);
			}
			
			public void OnPatrolDestroy(Vector3 pos)
			{
				ulong ownerID = PlayersList.Any() ? PlayersList[0] : 0uL;
				var crates = Pool.Get<List<LockedByEntCrate>>();
				Vis.Entities(pos, 5f, crates);
				if (crates.Any())
				{
					foreach (var container in crates)
					{
						if (container.OwnerID == 0uL)
							container.OwnerID = ownerID;
					}
				}
				Pool.FreeUnmanaged(ref crates);
				var debris = Pool.Get<List<HelicopterDebris>>();
				Vis.Entities(pos, 5f, debris);
				foreach (var helicopterDebris in debris)
					helicopterDebris.OwnerID = ownerID;
				Pool.FreeUnmanaged(ref debris);
				_rrallPatrols.Remove(PatrolID);
				PatrolID = 0;
			}
			
			public void Destroy()
			{
				foreach (var netID in Raiders)
					_rrAllRaiders.Remove(netID);
				_rrallPatrols.Remove(PatrolID);
				foreach (var userID in PlayersList)
					_rrAllPlayers.Remove(userID);
				Pool.FreeUnmanaged(ref BuildingIDs);
				Pool.FreeUnmanaged(ref Raiders);
				Pool.FreeUnmanaged(ref PlayersList);
			}
		}
		private const string _monumentsPathOld = @"RealPVE\_old_MonumentsConfig({0})";
		
		object OnEntityTakeDamage(NPCPlayer victimNPC, HitInfo info)
        {
			if (info == null || info.Initiator is not BasePlayer attacker || attacker == null || !attacker.userID.IsSteamId()) return null;
			if (victimNPC.skinID != 0uL)
            {
				if (victimNPC.skinID == _bradleySkinId)
                {
                    if (_eventScientistsList.TryGetValue(victimNPC.net.ID, out var eventData) && !eventData.CanBeAttackedBy(attacker))
                        goto cancel;
                }
				else if (victimNPC.skinID == _rbPluginID)
                {
                    if (TryGetRaidBase(victimNPC.transform.position, out var rbData) && !rbData.CanInteractWithRaid(attacker.userID))
                        goto cancel;
                }
                else if (victimNPC.skinID == _rrPluginID)
                {
                    if (_config.RandomRaids_Enabled && _rrAllRaiders.TryGetValue(victimNPC.net.ID.Value, out var rrData) && !rrData.PlayersList.Contains(attacker.userID))
                        goto cancel;
                }
			}
			else if (_monumentsList.TryGetValue(GetNpcMonument(victimNPC), out var monumentData) && monumentData.CanLoot(attacker) != null)
				goto cancel;
			return null;
		
		cancel:
			info.Initiator = null;
			info.damageTypes.Clear();
			return null;
		}
		object CanUnlock(BasePlayer player, BaseLock baseLock) => !baseLock.OwnerID.IsSteamId() || IsEntityInPvP(player.userID, baseLock.net.ID.Value) ? null : player.TasirMumkin(baseLock.OwnerID);
		private void SaveRBsConfig(string path = _rbsPath) => Interface.Oxide.DataFileSystem.WriteObject(path, _rbsConfig);
		object OnEntityTakeDamage(AttackHelicopter attackHeli, HitInfo info) => CanVehicleTakeDamage(attackHeli.net?.ID.Value ?? 0uL, info);
		
		object CanLootEntity(BasePlayer player, Rust.Modular.EngineStorage storage)
		{
			if (_unrestrictedLooters.Contains(player.userID)) return AdminOpenLoot(player, storage);
			if (!IsEntityInPvP(player.userID, storage.net.ID.Value) && storage.GetParentEntity() is BaseVehicleModule module)
				CanLootCar(player, module);
			return null;
		}
        
        		private const string _dataLootersPath = @"RealPVE\Data\CachedLooters", _dataPickupsPath = @"RealPVE\Data\CachedPickups";
		
		object OnStructureUpgrade(BaseCombatEntity entity, BasePlayer player, BuildingGrade.Enum grade) => !entity.OwnerID.IsSteamId() || IsEntityInPvP(player.userID, entity.net.ID.Value) ? null : player.TasirMumkin(entity.OwnerID);
		
		void OnRaidableBaseStarted(Vector3 pos, int mode, bool allowPVP, string raidID, float f1, float f2, float loadTime, ulong ownerID, BasePlayer player, List<BasePlayer> raiders, List<BasePlayer> intruders, List<BaseEntity> entities, string baseName, DateTime spawnDateTime, DateTime despawnDateTime, float radius, int lootRemain)
		{
			raidID = pos.ToString();
			_rbList[raidID] = new RBData(raidID, pos, mode, allowPVP, radius, ownerID, despawnDateTime, lootRemain, intruders);
		}
		
		object OnCreateDynamicPVP(string eventName, BaseEntity entity)
        {
            switch (eventName)
            {
                case "Bradley":
                case "Helicopter":
                    if (entity.skinID == 0uL && _eventsList.ContainsKey(entity.net.ID.Value))
                        return false;
                    break;
                case "SupplyDrop":
                case "SupplySignal":
                    entity.OwnerID = 0uL;
                    break;
                default:
                    break;
            }
            return null;
        }
		object CanLootEntity(BasePlayer player, RepairBench repairBench) => CanLootStorage(player, repairBench);
		
		private object CanLootStorage(BasePlayer player, StorageContainer container, bool isPreChecked = false)
        {
			if (!isPreChecked)
            {
				if (_unrestrictedLooters.Contains(player.userID)) return AdminOpenLoot(player, container);
				if (IsEntityInPvP(player.userID, container.net.ID.Value)) return null;
			}
			object result = null;
            if (container.OwnerID.IsSteamId())
                result = player.TasirMumkin(container.OwnerID);
            else if (_monumentsList.TryGetValue(GetEntityMonument(container), out var monumentData))
                result = monumentData.CanLoot(player);
            else if (TryGetRaidBase(container.transform.position, out var rbData) && !rbData.CanInteractWithRaid(player.userID))
                result = false;
            if (result != null)
                SendMessage(player, "MsgCantInteract");
            return result;
		}
				
				private const string WhiteColor = "1 1 1 1";
		
		void OnRaidableBasePurchased(string playerID, Vector3 pos)
        {
			if (ulong.TryParse(playerID, out var userID) && _rbList.TryGetValue(pos.ToString(), out var rbData))
				rbData.SetNewOwner(userID);
		}
		
		void OnEntityKill(BaseEntity entity) => OnEntityExitPVP(entity);
		public static Dictionary<ulong, RRData> _rrAllPlayers = new Dictionary<ulong, RRData>();

		private PvEPermission GetVehicleLimitPermission(string userID, VehicleType type)
		{
			PvEPermission result = null;
			foreach (var perm in _permissionsConfig.PermissionsList)
			{
				if (permission.UserHasPermission(userID, perm.Name))
				{
					if (result == null)
						result = perm;
					else if (result.Allowed_Vehicles[type].Limit < 0 || perm.Allowed_Vehicles[type].Limit < 0)
					{
						if (perm.Allowed_Vehicles[type].Limit < result.Allowed_Vehicles[type].Limit)
							result = perm;
					}
					else if (perm.Allowed_Vehicles[type].Limit > result.Allowed_Vehicles[type].Limit)
						result = perm;
				}
			}
			return result ?? _permissionsConfig.PermissionsList[0];
		}
		
		void OnLootEntityEnd(BasePlayer player, StorageContainer container)
		{
			if (container.panelName == "fuelsmall")
				DestroyVehiclePanels(player);
		}
		private static Dictionary<ulong, HashSet<string>> _playerUI;
		
		object CanSwapToSeat(BasePlayer player, BaseMountable mountable) => CanInteractWithSeat(player, mountable);
		
		void RandomRaidEventEnd(Vector3 pos)
		{
			string posStr = pos.ToString();
			if (_randomRaidsList.TryGetValue(posStr, out var rrData))
				rrData.Destroy();
			_randomRaidsList.Remove(posStr);
		}
		
		void OnRandomRaidHeliSpawned(Vector3 pos, PatrolHelicopter patrol)
        {
			string rrPos = new Vector3(pos.x, pos.y - 100f, pos.z).ToString();
			if (_randomRaidsList.TryGetValue(rrPos.ToString(), out var rrData))
			{
				_rrallPatrols[patrol.net.ID.Value] = rrData;
				rrData.PatrolID = patrol.net.ID.Value;
			}
		}
		
		private void LoadBeachConfig()
		{
			if (Interface.Oxide.DataFileSystem.ExistsDatafile(_beachPath))
			{
				try { _beachConfig = Interface.Oxide.DataFileSystem.ReadObject<NewbieConfig>(_beachPath); }
				catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
			}
			
			if (_beachConfig == null || _beachConfig.Version < _beachVersion)
			{
				if (_beachConfig != null)
				{
					string path = string.Format(_beachPathOld, _beachConfig.Version.ToString());
					PrintWarning($@"Your settings version for starter kits is outdated. The config file has been updated, and your old settings have been saved in \data\{path}");
					SaveBeachConfig(path);
				}
				_beachConfig = new NewbieConfig() { Version = _beachVersion };
			}
			
			if (_beachConfig.Respawn_Main == null)
				_beachConfig.Respawn_Main = new HashSet<BeachItem>() { new BeachItem("note", 0, text: "MsgNoteText") };
			else if (_beachConfig.Respawn_Main.Count > 24)
				_beachConfig.Respawn_Main = _beachConfig.Respawn_Main.Take(24).ToHashSet();
			if (_beachConfig.Respawn_Belt == null)
				_beachConfig.Respawn_Belt = new HashSet<BeachItem>() { new BeachItem("rock", 0, skinID: 3270017356uL), new BeachItem("torch.torch.skull", 1) };
			else if (_beachConfig.Respawn_Belt.Count > 6)
				_beachConfig.Respawn_Belt = _beachConfig.Respawn_Belt.Take(6).ToHashSet();
			if (_beachConfig.Respawn_Wear == null)
				_beachConfig.Respawn_Wear = new HashSet<BeachItem>() { new BeachItem("santahat", 0), new BeachItem("twitchsunglasses", 1), new BeachItem("santabeard", 2), new BeachItem("hoodie", 3, skinID: 1587744366uL), new BeachItem("pants", 4, skinID: 1587846022uL), new BeachItem("attire.hide.boots", 5, skinID: 1230633097uL) };
			else if (_beachConfig.Respawn_Wear.Count > 7)
				_beachConfig.Respawn_Wear = _beachConfig.Respawn_Wear.Take(7).ToHashSet();
			
			SaveBeachConfig();
		}
		
		object OnNpcTarget(BaseAnimalNPC animal, BasePlayer target)
        {
            if (target.userID.IsSteamId())
            {
                if (_monumentsList.TryGetValue(GetEntityMonument(animal), out var monumentData))
                    return monumentData.CanLoot(target);
                if (_monumentsList.TryGetValue(GetPlayerMonument(target.userID), out monumentData))
                    return monumentData.CanLoot(target);
            }
            return null;
        }
		
		void OnPlayerEnteredRaidableBase(BasePlayer player, Vector3 pos, bool allowPVP, int mode)
        {
			if (mode < 0 || !_rbList.TryGetValue(pos.ToString(), out var rbData))
            {
				SendMessage(player, "MsgRaidableBasesDisabled");
				return;
            }
			rbData.OnPlayerEnter(player);
			if (!rbData.IsPvP && !rbData.OwnerID.IsSteamId())
			{
				ShowRaidableBasesOffer(player, rbData);
				timer.Once(rbData.Settings.OfferTime, () => { DestroyUI(player, RBOfferUI); });
			}
		}
		private Timer _updatesTimer;
		object OnEntityTakeDamage(MotorRowboat motorBoat, HitInfo info) => CanVehicleTakeDamage(motorBoat.net?.ID.Value ?? 0uL, info);
		
		public static string ReplacePlaceholders(string str, params string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
				if (args[i] != null)
					str = str.Replace($"{{{i}}}", args[i]);
			}
            return str;
        }
		private Dictionary<int, object> _pvpBar, _pvpDelayBar;
		private Dictionary<string, MonumentData> _monumentsList = new Dictionary<string, MonumentData>();
		
		object CanPushBoat(BasePlayer player, MotorRowboat boat)
        {
			if (!IsEntityInPvP(player.userID, boat.net.ID.Value) && _vehiclesList.TryGetValue(boat.net.ID.Value, out var vehicleData) && !player.Uyda())
			{
                object result = vehicleData.CanInteract(player);
                if (result != null)
                    SendMessage(player, "MsgVehicleCanNotInteract");
                return result;
            }
            return null;
        }
		
		static bool OnEntityExitPVP(BaseEntity entity, string zoneID = "", float delay = 0f)
        {
			if (entity != null && entity.net != null)
				return _pvpEntities.Remove(entity.net.ID.Value);
			return false;
		}
        		
		        protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["MsgNoteText"] = "Welcome to our PvE server!\nThis server utilizes the RealPVE plugin.\nYou can find more details about the plugin at the following link: https://codefling.com/plugins/real-pve",
				["CmdAdminLootClear"] = "{0} players' admin looting rights have been revoked!",
				["CmdAdminLootEnabled"] = "Admin looting rights have been granted to you!",
				["CmdAdminLootDisabled"] = "Admin looting rights have been revoked from you!",
				["CmdAdminLootPlayerNotFound"] = "Player {0} not found! You must provide the player's name or ID.",
				["CmdAdminLootPlayerEnabled"] = "Admin looting rights have been granted to player {0}!",
				["CmdAdminLootPlayerDisabled"] = "Admin looting rights have been revoked from player {0}!",
				["CmdAdminPickupFailed"] = "Not enough arguments for this command!\n<size=12><color=#9A9A9A>/adminpve pickup clear</color></size>",
				["CmdAdminPickupClear"] = "{0} players have been removed from the list allowing everyone to pick up their items!",
				["CmdAdminMonumentFailed"] = "Not enough arguments for this command!\n<size=12><color=#9A9A9A>/adminpve monument pvp *monumentID*</color></size>",
				["CmdAdminMonumentNotFound"] = "Monument {0} not found! You must specify the monument's ID as the last parameter or be inside it.",
				["CmdAdminMonumentOcupied"] = "Monument {0} is occupied! You can only apply such changes to unoccupied monuments.",
				["CmdAdminMonumentPvPEnabled"] = "Monument {0} now has the PvP flag!",
				["CmdAdminMonumentPvPDisabled"] = "Monument {0} no longer has the PvP flag!",
				["CmdCommandFailed"] = "Incorrect command or you don't have permissions!",
				["CmdCommandForPlayers"] = "This command is only available to players!",
				["MsgCantInteract"] = "You can't interact with others' belongings!",
				["MsgCantInteractPlayer"] = "You can't interact with other players, only your friends!",
				["MsgCantGatherInBase"] = "You can't gather resources in others' bases!",
				["MsgCantPickup"] = "You can't pick up others' items!",
				["MsgCantInteractWeaponRack"] = "You can't interact with others' weapon racks!",
				["MsgCantHandcuffing"] = "You cannot handcuff other players outside the PvP zone!",
				["MsgPrivlidgeClear"] = "{0} players have been removed from the Building Privilege.",
				["MsgPrivlidgeClearEmpty"] = "Only you are authorized in the Building Privilege.",
				["MsgFree"] = "Free",
				["MsgNoDate"] = "null",
				["MsgEconomicsNotEnough"] = "Not enough funds!",
				["CmdPickupEnabled"] = "You have allowed all players to pick up your items!",
				["CmdPickupDisabled"] = "You have forbidden all players from picking up your items!",
				["CmdTeamNotFound"] = "To use this command, you must be in a group!",
				["CmdTeamNotLeader"] = "To use this command, you must be the group leader!",
				["CmdTeamFireEnabled"] = "Friendly fire enabled by {0}!",
				["CmdTeamFireDisabled"] = "Friendly fire disabled by {0}!",
				["MsgPvPEnter"] = "You have entered the PvP zone! You can be killed and looted here!",
				["MsgPvPBar"] = "PvP Zone!",
				["MsgPvPDelay"] = "You have left the PvP zone, but PvP will remain active for {0} seconds!",
				["MsgPvPDelayBar"] = "PvP ends in:",
				["MsgMonumentOccupied"] = "{1} occupied {0} in {2} minutes.",
				["MsgMonumentFree"] = "{0} is available for looting!",
				["MsgMonumentOfferTitle"] = "Unlock Treasures of {0}!",
				["MsgMonumentOfferDescription"] = "Tap the notification to pay {0}.\nAnd unlock access to undiscovered riches!",
				["MsgMonumentCantPickup"] = "You can't pick up items in others' monuments!",
				["MsgMonumentLooterDeath"] = "You died while looting {0}. You have {1} seconds.",
				["MsgMonumentLooterExit"] = "You have left the monument. You have {0} seconds to return!",
				["MsgMonumentLooterRemoved"] = "Time's up! You have been removed from the monument!",
				["MsgMonumentLootingNotFree"] = "You have been added to the loot queue. Loot cost: {0}",
				["MsgMonumentNotInQueue"] = "You are not in the queue! You need to re-enter the monument!",
				["MsgMonumentIsPvP"] = "PvP Zone!",
				["MsgMonumentNoAccess"] = "no access",
				["CmdVehicleFailed"] = "Not enough arguments for this command!\n<size=12><color=#9A9A9A>/realpve vehicle find *vehicleID*</color></size>",
				["CmdVehicleNotFound"] = "Vehicle not found!",
				["CmdVehicleFind"] = "Your vehicle {0} is located in grid {1}!",
				["CmdVehicleClearEmpty"] = "No vehicles found for removal!",
				["CmdVehicleClear"] = "Removed {0} vehicles!",
				["MsgVehicleDialogTitle"] = "Department of Motor Vehicles",
				["MsgVehicleDialogDescription"] = "ID: \nType: \nRegistration fee: \nCategory: ",
				["MsgVehicleDialogDescriptionValue"] = "<b>{0}</b>\n<b>{1}</b>\n<b>{4}</b>\n<b>{2}</b>",
				["MsgVehicleDialogDescriptionRegistered"] = "ID: \nType: \nRegistration date: \nCategory: ",
				["MsgVehicleDialogDescriptionRegisteredValue"] = "<b>{0}</b>\n<b>{1}</b>\n<b>{3}</b>\n<b>{2}</b>",
				["MsgVehicleDialogDescriptionNotOwner"] = "ID: \nOwner: \nRegistration date: \nType: \nCategory: ",
				["MsgVehicleDialogDescriptionNotOwnerValue"] = "<b>{0}</b>\n<b>{4}</b>\n<b>{3}</b>\n<b>{1}</b>\n<b>{2}</b>",
				["MsgVehicleCarDialogDescription"] = "ID: \nType: \nRegistration fee: \nCategory: ",
				["MsgVehicleCarDialogDescriptionValue"] = "<b>{0}</b>\n<b>{1}</b>\n<b>{4}</b>\n<b>{2}</b>",
				["MsgVehicleCarDialogDescriptionRegistered"] = "ID: \nType: \nReg date: \nCategory: ",
				["MsgVehicleCarDialogDescriptionRegisteredValue"] = "<b>{0}</b>\n<b>{1}</b>\n<b>{3}</b>\n<b>{2}</b>",
				["MsgVehicleCarDialogDescriptionNotOwner"] = "ID: \nOwner: \nReg date: \nType: \nCategory: ",
				["MsgVehicleCarDialogDescriptionNotOwnerValue"] = "<b>{0}</b>\n<b>{4}</b>\n<b>{3}</b>\n<b>{1}</b>\n<b>{2}</b>",
				["MsgVehicleCarGarageEmpty"] = "The car lift is empty!",
				["MsgVehicleDialogLink"] = "Register Vehicle",
				["MsgVehicleDialogUnLink"] = "Cancel registration",
				["MsgVehicleDialogOwnerWarning"] = "Removing all driver modules will result in the cancellation of registration!",
				["MsgVehicleDialogWarning"] = "Registration is only possible with a driver module present!",
				["MsgVehicleDialogIncorrectPassword"] = "The password must consist of 4 digits!",
				["MsgVehicleNotOwner"] = "You are not the owner!",
				["MsgVehicleWrongName"] = "Invalid name format for the vehicle!",
				["MsgVehicleNewName"] = "Your vehicle has been renamed to: {0}!",
				["MsgVehicleCanNotInteract"] = "You are not the owner or their friend!",
				["MsgVehicleNoPermissions"] = "You do not have permissions for this action!",
				["MsgVehicleLinked"] = "The {0} has been successfully linked! You have {1} out of {2} available.",
				["MsgVehicleUnLinked"] = "The {0} has been successfully unlinked!",
				["MsgVehicleFailedDeauthorize"] = "You can only deauthorize by unlinking the vehicle from you.",
				["MsgVehicleLimit"] = "Limit exceeded! You have used {1} out of {2} registrations.",
				["MsgVehicleDestroyed"] = "Your vehicle {0}({1}) has been destroyed!",
				["MsgVehicleTugboatAuthorization"] = "To authorize in the tugboat, it must be claim!",
				["MsgVehicleLandVehicle"] = "Land",
				["MsgVehicleAirVehicle"] = "Air",
				["MsgVehicleWaterVehicle"] = "Water",
				["MsgVehicleWinterVehicle"] = "Winter",
				["MsgVehicleTrainVehicle"] = "Train",
				["MsgVehicleHorse"] = "horse",
				["MsgVehicleBike"] = "bike",
				["MsgVehicleMotorBike"] = "motor bike",
				["MsgVehicleCar"] = "car",
				["MsgVehicleBalloon"] = "air balloon",
				["MsgVehicleMinicopter"] = "minicopter",
				["MsgVehicleTransportHeli"] = "transportHeli",
				["MsgVehicleAttackHeli"] = "attack heli",
				["MsgVehicleRowBoat"] = "row boat",
				["MsgVehicleRHIB"] = "RHIB",
				["MsgVehicleTugBoat"] = "tugboat",
				["MsgVehicleSubmarineOne"] = "small submarine",
				["MsgVehicleSubmarineTwo"] = "submarine",
				["MsgVehicleSnowmobile"] = "snowmobile",
				["MsgVehicleTrain"] = "train",
				["MsgEventOccupied"] = "{0} is already occupied by {1}!",
				["MsgEventOfferTitle"] = "Claim {0}!",
				["MsgEventOfferDescription"] = "Tap the notification to pay {0}.\nAnd unlock access to undiscovered riches!",
				["MsgEventNewLooter"] = "You have claimed {0}. You have {1} death for your team.",
				["MsgEventDeathLimit"] = "{0} is no longer yours! You have exceeded your death limit!",
				["MsgEventComplete"] = "{0} destroyed at coordinates: {1}!",
				["MsgEventPatrolHelicopter"] = "Patrol Helicopter",
				["MsgEventBradleyAPC"] = "Bradley",
				["MsgRaidableBasesDisabled"] = "This Raidable Base is either disabled or not found!",
				["MsgRaidableBasesOccupied"] = "The Raidable Base is already occupied by {0}!",
				["MsgRaidableBasesLimit"] = "Limit exceeded! You have {0} out of {1} available Raidable Bases.",
				["MsgRaidableBasesPurchaseStart"] = "Payment successful! Please wait...",
				["MsgRaidableBasesPurchased"] = "You have successfully purchased the Raidable Base!",
				["MsgRaidableBasesPurchaseFailed"] = "You were unable to purchase the Raidable Base! Funds refunded.",
				["MsgRaidableBasesOfferTitle"] = "Claim {0} Raidable Base!",
				["MsgRaidableBasesOfferDescription"] = "Tap the notification to pay {0}.\nAnd unlock access to undiscovered riches!",
				["MsgRaidableBasesBarText"] = "{0} Base",
				["MsgRaidableBasesBarTextLootRemaining"] = "Loot Remaining",
				["MsgRaidableBasesBarTextLootCompleted"] = "Completed",
				["MsgRaidableBasesBarNoAccess"] = "no access",
				["MsgRaidableBasesEasy"] = "Easy",
				["MsgRaidableBasesMedium"] = "Medium",
				["MsgRaidableBasesHard"] = "Hard",
				["MsgRaidableBasesExpert"] = "Expert",
				["MsgRaidableBasesNightmare"] = "Nightmare",
				["MsgRaidableBasesIsPvP"] = "PvP Zone!",
				["MsgSurvivalArena"] = "Survival Arena",
				["MsgArenaWhilePvP"] = "You cannot enter the arena while you have an active PvP!"
			}, this);
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["MsgNoteText"] = "Добро пожаловать на наш PvE сервер!\nДанный сервер использует RealPVE плагин.\nПодробней о плагине можно узнать по ссылке: https://codefling.com/plugins/real-pve",
				["CmdAdminLootClear"] = "У {0} игроков было отозвано лутание с правами администатора!",
				["CmdAdminLootEnabled"] = "Вам были выданы администраторские права на лутание!",
				["CmdAdminLootDisabled"] = "У вас были отозваны администраторские права на лутание!",
				["CmdAdminLootPlayerNotFound"] = "Игрок {0} не найден! Вы должны указать имя или ID игрока.",
				["CmdAdminLootPlayerEnabled"] = "Игроку {0} были выданы администраторские права на лутание!",
				["CmdAdminLootPlayerDisabled"] = "У игрока {0} были отозваны администраторские права на лутание!",
				["CmdAdminPickupFailed"] = "Не достаточно аргументов для этой команды!\n<size=12><color=#9A9A9A>/adminpve pickup clear</color></size>",
				["CmdAdminPickupClear"] = "Удалено {0} игроков из списка, позволяющего всем поднимать их вещи!",
				["CmdAdminMonumentFailed"] = "Не достаточно аргументов для этой команды!\n<size=12><color=#9A9A9A>/adminpve monument pvp *monumentID*</color></size>",
				["CmdAdminMonumentNotFound"] = "Монумент {0} не найден! Вы должны указать ID монумента в качестве последнего параметра, либо находится внутри него.",
				["CmdAdminMonumentOcupied"] = "Монумент {0} занят! Вы можете применять подобные изменения только у свободных монументов.",
				["CmdAdminMonumentPvPEnabled"] = "Монумент {0} теперь имеет флаг ПвП!",
				["CmdAdminMonumentPvPDisabled"] = "Монумент {0} теперь не имеет флага ПвП!",
				["CmdCommandFailed"] = "Неправильная команда либо у вас недостаточно прав!",
				["CmdCommandForPlayers"] = "Эта команда доступна только для игроков!",
				["MsgCantInteract"] = "Вы не можете взаимодействовать с чужими вещами!",
				["MsgCantInteractPlayer"] = "Вы не можете взаимодействовать с другими игроками, только с друзьями!",
				["MsgCantGatherInBase"] = "Вы не можете собирать ресурсы в чужих базах!",
				["MsgCantPickup"] = "Вы не можете подбирать чужие вещи!",
				["MsgCantInteractWeaponRack"] = "Вы не можете взаимодействовать с чужими оружейными стойками!",
				["MsgCantHandcuffing"] = "Вы не можете заковывать других игроков в наручники за пределами ПвП зоны!",
				["MsgPrivlidgeClear"] = "Из шкафа выписано {0} ироков.",
				["MsgPrivlidgeClearEmpty"] = "Кроме вас в шкафу ни кто не авторизован.",
				["MsgFree"] = "Бесплатно",
				["MsgNoDate"] = "пусто",
				["MsgEconomicsNotEnough"] = "Не достаточно средств!",
				["CmdPickupEnabled"] = "Вы разрешили поднятие ваших предметов для всех игркоов!",
				["CmdPickupDisabled"] = "Вы запретили поднятие ваших предметов для всех игркоов!",
				["CmdTeamNotFound"] = "Для использования этой команды вы должны быть в группе!",
				["CmdTeamNotLeader"] = "Для использования этой команды вы должны быть лидером группы!",
				["CmdTeamFireEnabled"] = "{0} включил дружественный огонь!",
				["CmdTeamFireDisabled"] = "{0} выключил дружественный огонь!",
				["MsgPvPEnter"] = "Вы вошли в ПвП зону! Здесь вас могут убить и залутать!",
				["MsgPvPBar"] = "Зона ПвП!",
				["MsgPvPDelay"] = "Вы покинули ПвП зону, но ПвП останется активным еще {0} секунд!",
				["MsgPvPDelayBar"] = "ПвП еще активно:",
				["MsgMonumentOccupied"] = "{1} занял {0} на {2} минут.",
				["MsgMonumentFree"] = "{0} можно лутать!",
				["MsgMonumentOfferTitle"] = "Откройте сокровища {0}!",
				["MsgMonumentOfferDescription"] = "Нажми на уведомление для оплаты {0}.\nИ разблокируй доступ к неизведанным богатствам!",
				["MsgMonumentCantPickup"] = "Вы не можете подбирать вещи в чужих монументах!",
				["MsgMonumentLooterDeath"] = "Вы умерли во время лутания {0}. У вас есть {1} секунд.",
				["MsgMonumentLooterExit"] = "Вы покинули монумент. У вас есть {0} секунд на возвращение!",
				["MsgMonumentLooterRemoved"] = "Время вышло! Вы были удалены из монумента!",
				["MsgMonumentLootingNotFree"] = "Вас добавили в очередь на лутание. Стоимость лутания: {0}",
				["MsgMonumentNotInQueue"] = "Вас нет в очереди! Вам необходимо перезайти в монумент!",
				["MsgMonumentIsPvP"] = "Зона ПвП!",
				["MsgMonumentNoAccess"] = "нет доступа",
				["CmdVehicleFailed"] = "Не достаточно аргументов для этой команды!\n<size=12><color=#9A9A9A>/realpve vehicle find *vehicleID*</color></size>",
				["CmdVehicleNotFound"] = "Транспортное средство не найдено!",
				["CmdVehicleFind"] = "Ваше транспортное средство {0} находится в квадрате {1}!",
				["CmdVehicleClearEmpty"] = "Транспортные средства для удаления не найдены!",
				["CmdVehicleClear"] = "Удалено {0} транспортных средств!",
				["MsgVehicleDialogTitle"] = "ГИБДД",
				["MsgVehicleDialogDescription"] = "ID: \nТип: \nСтоимость регистрации: \nКатегория: ",
				["MsgVehicleDialogDescriptionValue"] = "<b>{0}</b>\n<b>{1}</b>\n<b>{4}</b>\n<b>{2}</b>",
				["MsgVehicleDialogDescriptionRegistered"] = "ID: \nТип: \nДата регистрации: \nКатегория: ",
				["MsgVehicleDialogDescriptionRegisteredValue"] = "<b>{0}</b>\n<b>{1}</b>\n<b>{3}</b>\n<b>{2}</b>",
				["MsgVehicleDialogDescriptionNotOwner"] = "ID: \nВладелец: \nДата регистрации: \nТип: \nКатегория: ",
				["MsgVehicleDialogDescriptionNotOwnerValue"] = "<b>{0}</b>\n<b>{4}</b>\n<b>{3}</b>\n<b>{1}</b>\n<b>{2}</b>",
				["MsgVehicleCarDialogDescription"] = "ID: \nТип: \nСтоимость регистрации: \nКатегория: ",
				["MsgVehicleCarDialogDescriptionValue"] = "<b>{0}</b>\n<b>{1}</b>\n<b>{4}</b>\n<b>{2}</b>",
				["MsgVehicleCarDialogDescriptionRegistered"] = "ID: \nТип: \nДата: \nКатегория: ",
				["MsgVehicleCarDialogDescriptionRegisteredValue"] = "<b>{0}</b>\n<b>{1}</b>\n<b>{3}</b>\n<b>{2}</b>",
				["MsgVehicleCarDialogDescriptionNotOwner"] = "ID: \nВладелец: \nДата: \nТип: \nКатегория: ",
				["MsgVehicleCarDialogDescriptionNotOwnerValue"] = "<b>{0}</b>\n<b>{4}</b>\n<b>{3}</b>\n<b>{1}</b>\n<b>{2}</b>",
				["MsgVehicleCarGarageEmpty"] = "Подъемник пустой!",
				["MsgVehicleDialogLink"] = "Поставить на учет",
				["MsgVehicleDialogUnLink"] = "Снять с учета",
				["MsgVehicleDialogOwnerWarning"] = "Снятие всех водительских модулей приведет к отмене регистрации!",
				["MsgVehicleDialogWarning"] = "Регистрация возможна только при наличии водительского модуля!",
				["MsgVehicleDialogIncorrectPassword"] = "Пароль должен состоять из 4-х цифр!",
				["MsgVehicleNotOwner"] = "Вы не являетесь владельцем!",
				["MsgVehicleWrongName"] = "Не верный формат имени для транспортного средства!",
				["MsgVehicleNewName"] = "Ваше транспортное средство переименовано в: {0}!",
				["MsgVehicleCanNotInteract"] = "Вы не являетесь владелецем или его другом!",
				["MsgVehicleNoPermissions"] = "У вас нет прав для этого действия!",
				["MsgVehicleLinked"] = "{0} успешно привязан(а)! У вас {1} из {2} доступных.",
				["MsgVehicleUnLinked"] = "{0} успешно отвязан(а)!",
				["MsgVehicleFailedDeauthorize"] = "Вы можете выписаться только при отвязки транспорта от вас.",
				["MsgVehicleLimit"] = "Лимит превышен! Вы использовали {1} из {2} доступных регистрации.",
				["MsgVehicleDestroyed"] = "Ваше транспортное средство {0}({1}) было уничтожено!",
				["MsgVehicleTugboatAuthorization"] = "Для авторизации в буксире, его необходимо поставить на учет!",
				["MsgVehicleLandVehicle"] = "Наземный",
				["MsgVehicleAirVehicle"] = "Воздушный",
				["MsgVehicleWaterVehicle"] = "Водный",
				["MsgVehicleWinterVehicle"] = "Зимний",
				["MsgVehicleTrainVehicle"] = "ЖД",
				["MsgVehicleHorse"] = "Лошадь",
				["MsgVehicleBike"] = "Велосипед",
				["MsgVehicleMotorBike"] = "Мотоцикл",
				["MsgVehicleCar"] = "Машина",
				["MsgVehicleBalloon"] = "Воздушный шар",
				["MsgVehicleMinicopter"] = "Мини коптер",
				["MsgVehicleTransportHeli"] = "Корова",
				["MsgVehicleAttackHeli"] = "Боевой вертолет",
				["MsgVehicleRowBoat"] = "Лодка",
				["MsgVehicleRHIB"] = "Патрульная лодка",
				["MsgVehicleTugBoat"] = "Буксир",
				["MsgVehicleSubmarineOne"] = "Маленькая подлодка",
				["MsgVehicleSubmarineTwo"] = "Подлодка",
				["MsgVehicleSnowmobile"] = "Снегоход",
				["MsgVehicleTrain"] = "Поезд",
				["MsgEventOccupied"] = "{0} уже занят игроком {1}!",
				["MsgEventOfferTitle"] = "Займите {0}!",
				["MsgEventOfferDescription"] = "Нажми на уведомление для оплаты {0}.\nИ разблокируй доступ к неизведанным богатствам!",
				["MsgEventNewLooter"] = "Вы заняли {0}. У вас на команду есть {1} жизней.",
				["MsgEventDeathLimit"] = "{0} больше не ваше! Вы исчерпали свой лимит жизней!",
				["MsgEventComplete"] = "{0} уничтожен в координатах: {1}!",
				["MsgEventPatrolHelicopter"] = "Патрульный вертолет",
				["MsgEventBradleyAPC"] = "Танк",
				["MsgRaidableBasesDisabled"] = "Эта Рейд база выключена или не найдена!",
				["MsgRaidableBasesOccupied"] = "Эта Рейд база уже занята игроком {0}!",
				["MsgRaidableBasesLimit"] = "Лимит превышен! У вас {0} из {1} доступных Рейд баз.",
				["MsgRaidableBasesPurchaseStart"] = "Оплата прошла! Ожидайте...",
				["MsgRaidableBasesPurchased"] = "Вы успешно приобрели Рейд базу!",
				["MsgRaidableBasesPurchaseFailed"] = "Вам не удалось приобрести Рейд базу! Деньги возвращены.",
				["MsgRaidableBasesOfferTitle"] = "Займите Рейд базу уровня: {0}!",
				["MsgRaidableBasesOfferDescription"] = "Нажми на уведомление для оплаты {0}.\nИ разблокируй доступ к неизведанным богатствам!",
				["MsgRaidableBasesBarText"] = "Уровень: {0}",
				["MsgRaidableBasesBarTextLootRemaining"] = "Осталось лута",
				["MsgRaidableBasesBarTextLootCompleted"] = "Выполнено",
				["MsgRaidableBasesBarNoAccess"] = "нет доступа",
				["MsgRaidableBasesEasy"] = "Легко",
				["MsgRaidableBasesMedium"] = "Средне",
				["MsgRaidableBasesHard"] = "Сложно",
				["MsgRaidableBasesExpert"] = "Эксперт",
				["MsgRaidableBasesNightmare"] = "Кошмар",
				["MsgRaidableBasesIsPvP"] = "Зона ПвП!",
				["MsgSurvivalArena"] = "Арена",
				["MsgArenaWhilePvP"] = "Вы не можете попасть на арену пока у вас имеется активное ПвП!"
			}, this, "ru");
		}
		private readonly VersionNumber _beachVersion = new VersionNumber(0, 1, 0);
		object OnLockRemove(ModularCar modularCar, BasePlayer player) => false;
		
		private bool TryGetRaidBaseByUID(ulong userID, out RBData result)
        {
            result = null;
            if (!_rbsConfig.IsEnabled) return false;
			foreach (var rbData in _rbList.Values)
            {
				if (rbData.IsPlayerInside(userID))
                {
					result = rbData;
					break;
				}
			}
            return result != null;
        }
		
        
        


        
        
        

        




        
        
        

        
        
        



        
        

        



        
        


        
        
        

        
        

        

        
        


        
        

        
        


        
        
        
        

        



        
        
        
        
        

        
        
        


        
        

        
        


        
        

        


        
        
        

        
        
        
        


        [PluginReference]
		private Plugin ImageLibrary, ZoneManager, Economics, RaidableBases, RandomRaids, Friends, DynamicPVP, AdvancedStatus, MonumentsWatcher, ServerPanels;

		object OnCupboardClearList(VehiclePrivilege privilege, BasePlayer player)
		{
			if (GetVehicleData(privilege.GetParentEntity(), out var vehicleData))
			{
				if (vehicleData.IsOwner(player.userID))
				{
					int totalPlayers = privilege.authorizedPlayers.Count() - 1;
					if (totalPlayers < 1)
						SendMessage(player, "MsgPrivlidgeClearEmpty", isWarning: false);
					else
					{
						privilege.authorizedPlayers.Clear();
						privilege.UpdateMaxAuthCapacity();
						privilege.SendNetworkUpdate();
						privilege.AddPlayer(player);
						privilege.UpdateMaxAuthCapacity();
						privilege.SendNetworkUpdate();
						SendMessage(player, "MsgPrivlidgeClear", new string[] { totalPlayers.ToString() }, false);
					}
				}
				else if (vehicleData.OwnerID == 0)
					return null;
			}
			return false;
		}
		private Dictionary<string, MarkersPvP> _pvpMarkers = new Dictionary<string, MarkersPvP>();
		
		private object CanVehicleTakeDamage(ulong vehicleID, HitInfo info)
		{
			if (info != null && info.damageTypes.GetMajorityDamageType() != DamageType.Decay &&
				info.InitiatorPlayer is BasePlayer attacker && attacker.userID.IsSteamId() && !IsEntityInPvP(attacker.userID, vehicleID) &&
				_vehiclesList.TryGetValue(vehicleID, out var vehicleData) && vehicleData.CanLoot(attacker) != null)
			{
				info.Initiator = null;
				info.damageTypes.Clear();
			}
			return null;
		}

		void OnCrateHack(HackableLockedCrate crate)
		{
			NextTick(() =>
            {
				if (_monumentsList.TryGetValue(GetEntityMonument(crate), out var monumentData) && monumentData.IsPvP)
					return;
				ulong hackerID = crate.originalHackerPlayerId;
				if (hackerID.IsSteamId())
					crate.hackSeconds += GetHackableCrateSkip(hackerID.ToString());
				if (monumentData != null && crate.hackSeconds < 900f)
					monumentData.UpdateTimer(crate.hackSeconds - HackableLockedCrate.requiredHackSeconds);
				crate.OwnerID = hackerID;
			});
		}
		
		private static NewbieConfig _beachConfig;

		void OnPlayerExitedMonument(string monumentID, BasePlayer player, string type, string reason, string newMonumentID)
		{
			if (_monumentsConfig.TrackedTypes.Contains(type) && _monumentsList.TryGetValue(monumentID, out var monumentData))
			{
				MonumentData newMonumentData = null;
				if (!string.IsNullOrWhiteSpace(newMonumentID))
					_monumentsList.TryGetValue(newMonumentID, out newMonumentData);
				monumentData.OnPlayerExit(player, reason);
				if (newMonumentData != null)
					newMonumentData.OnPlayerEnter(player);
			}
		}
		object OnEntityTakeDamage(VehicleModuleEngine module, HitInfo info) => CanModuleTakeDamage(module, info);
		object OnOvenToggle(BaseOven oven, BasePlayer player) => player == null ? null : CanLootStorage(player, oven);
		
		object OnEntityTakeDamage(HotAirBalloon balloon, HitInfo info)
		{
			if (info != null && info.damageTypes.GetMajorityDamageType() != DamageType.Decay &&
				info.InitiatorPlayer is BasePlayer attacker && attacker.userID.IsSteamId() && !IsEntityInPvP(attacker.userID, balloon.net.ID.Value))
			{
				ulong targetID = balloon.net?.ID.Value ?? 0uL;
				if (_vehiclesList.TryGetValue(targetID, out var vehicleData) && vehicleData.CanLoot(attacker) != null)
				{
					info.Initiator = null;
					info.damageTypes.Clear();
				}
			}
			return null;
		}
		
		object OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
			if (player == null || _unrestrictedLooters.Contains(player.userID) || IsEntityInPvP(player.userID, collectible.net.ID.Value)) return null;
			
			object result = null;
			
			if (collectible.prefabID == 1388355532u)
            {
				if (_monumentsList.TryGetValue(GetEntityMonument(collectible), out var monumentData))
					result = monumentData.CanPickup(player);
			}
            else if (_config.PreventPickUpCollectibleEntity)
				result = player.BinoMumkin();
			
			if (result != null)
				SendMessage(player, "MsgCantInteract");
			return result;
		}
		private const string PERMISSION_ADMIN = "realpve.admin", TimeFormat = "yyyy-MM-dd HH:mm:ss", Str_ScheduledDeath = "ScheduledDeath", Str_IsFriend = "IsFriend",
			Hooks_OnPlayerPVPDelay = "OnPlayerPVPDelay", Hooks_OnPlayerPVPDelayed = "OnPlayerPVPDelayed", Hooks_OnPlayerPVPDelayRemoved = "OnPlayerPVPDelayRemoved", Hooks_OnZoneStatusText = "OnZoneStatusText", Hooks_CanRedeemKit = "CanRedeemKit";
		object CanLootEntity(BasePlayer player, ResearchTable table) => CanLootStorage(player, table);
		
		object OnEntityTakeDamage(RidableHorse horse, HitInfo info) => CanVehicleTakeDamage(horse.net?.ID.Value ?? 0uL, info);

		object OnSwitchToggle(IOEntity entity, BasePlayer player)
        {
			if (_unrestrictedLooters.Contains(player.userID)) return null;
			object result = null;
			if (entity.OwnerID.IsSteamId())
				result = player.TasirMumkin(entity.OwnerID);
			else if (_monumentsList.TryGetValue(GetEntityMonument(entity), out var monumentData))
				result = monumentData.CanLoot(player);
			if (result != null)
				SendMessage(player, "MsgCantInteract");
			return result;
		}
		object OnMixingTableToggle(MixingTable table, BasePlayer player) => player == null ? null : CanLootStorage(player, table);
		private object CanUsePortal(BasePlayer player, BasePortal portal)
        {
			if (portal.skinID != 0uL || _unrestrictedLooters.Contains(player.userID)) return null;
			object result = player.MumkinNol(portal.OwnerID) == null || (Friends != null && IsFriend(player.UserIDString, portal.OwnerID.ToString())) ? null : false;
			if (result != null)
				SendMessage(player, "MsgCantInteract");
			return result;
		}
		
		void OnEntityEnterZone(string zoneID, BaseEntity entity)
        {
			if (entity is not BasePlayer && _dynamicPvPs.Contains(zoneID))
				OnEntityEnterPVP(entity);
		}
		object CanChangeCode(BasePlayer player, CodeLock codeLock, string newCode, bool isGuestCode) => !codeLock.OwnerID.IsSteamId() || IsEntityInPvP(player.userID, codeLock.net.ID.Value) ? null : player.TasirMumkin(codeLock.OwnerID);
		private Vector3 GetMonumentPosition(string monumentID) => (Vector3)(MonumentsWatcher?.Call(MonumentGetMonumentPosition, monumentID) ?? Vector3.zero);
		
		void OnPlayerCorpseSpawned(BasePlayer player, PlayerCorpse corpse)
        {
			if (_pvpPlayers.ContainsKey(player.userID))
                OnEntityEnterPVP(corpse);
            if (corpse.containers != null)
            {
                foreach (var container in corpse.containers)
                    container.containerVolume = 28;
            }
        }
		
		private object RRNpc(ulong netID, BuildingBlock block)
		{
			if (block != null && _rrAllRaiders.TryGetValue(netID, out var rrData))
			{
				var buildID = block.GetBuilding()?.ID ?? 0;
				if (rrData.BuildingIDs.Contains(buildID) || rrData.PlayersList.Contains(block.OwnerID))
					return null;
			}
			return false;
		}
		
		void DeletePVPMapMarker(string zoneID)
		{
			if (_pvpMarkers.TryGetValue(zoneID, out var markersPvP))
			{
				markersPvP.Destroy();
				_pvpMarkers.Remove(zoneID);
			}
		}

		private int GetSheltersLimit(string userID)
		{
			int result = int.MinValue;
			foreach (var perm in _permissionsConfig.PermissionsList)
			{
				if (perm.Shelters > result && permission.UserHasPermission(userID, perm.Name))
					result = perm.Shelters;
			}
			return result == int.MinValue ? _permissionsConfig.PermissionsList[0].Shelters : result;
		}
		public static readonly HashSet<VehicleType> _allowSitVehicles = new HashSet<VehicleType>() { VehicleType.MotorBike, VehicleType.Car, VehicleType.Minicopter, VehicleType.TransportHeli, VehicleType.RowBoat, VehicleType.RHIB, VehicleType.Snowmobile };
		object CanLootEntity(BasePlayer player, StashContainer stash) => CanLootStorage(player, stash);
		
		void OnRaidableDespawnUpdate(Vector3 pos, int mode, bool allowPVP, string raidID, float f1, float f2, float loadTime, ulong ownerID, BasePlayer player, List<BasePlayer> raiders, List<BasePlayer> intruders, List<BaseEntity> entities, string baseName, DateTime spawnDateTime, DateTime despawnDateTime, float radius, int lootRemain)
        {
			if (_rbList.TryGetValue(pos.ToString(), out var rbData))
				rbData.DespawnTimeUpdated(despawnDateTime);
		}
        		
				private void ShowVehiclePanels(BasePlayer player, VehicleData vehicleData)
        {
            CuiElementContainer container;
            switch (vehicleData.Type)
            {
                case VehicleType.Horse:
                    container = GetVehicleHorsePanel(player.UserIDString, vehicleData);
                    break;
                case VehicleType.Car:
                    container = GetVehicleCarPanel(player.UserIDString, vehicleData);
                    break;
                default:
                    container = GetVehicleDefaultPanel(player.UserIDString, vehicleData);
                    break;
            }
            DestroyUI(player, _uiVehiclePanel);
            CuiHelper.AddUi(player, container);
            _playerUI[player.userID].Add(_uiVehiclePanel);
        }
		object CanLootEntity(BasePlayer player, Composter composter) => CanLootStorage(player, composter);
		public static readonly int[] _driverSit = new int[5] { 1, 5, 9, 11, 26 };

		private bool GetEntityFromArg(BasePlayer player, string arg, out BaseEntity entity)
		{
			entity = null;
			if (player != null)
			{
				if (ulong.TryParse(arg, out var entID))
					entity = BaseNetworkable.serverEntities.Find(new NetworkableId(entID)) as BaseEntity;
				if (entity == null)
					GetLookEntity(player, out entity);
			}
			return entity != null;
		}
		
		void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
			if (player.userID.IsSteamId() && info != null && info.Initiator is BaseEntity killerEnt)
			{
				if (killerEnt is ScientistNPC)
				{
					if (killerEnt.skinID == _bradleySkinId && _eventScientistsList.TryGetValue(killerEnt.net.ID, out var eventData) && eventData.CanBeAttackedBy(player))
						eventData.OnLooterDeath();
				}
				else if (killerEnt is BradleyAPC)
				{
					if (_eventsList.TryGetValue(killerEnt.net.ID.Value, out var eventData) && eventData.CanBeAttackedBy(player))
						eventData.OnLooterDeath();
				}
			}
		}

		object CanHackCrate(BasePlayer player, HackableLockedCrate crate)
		{
			if (!_unrestrictedLooters.Contains(player.userID) && _monumentsList.TryGetValue(GetEntityMonument(crate), out var monumentData))
			{
				object result = monumentData.CanLoot(player);
				if (result != null)
					SendMessage(player, "MsgCantInteract");
				return result;
			}
			return null;
		}

		private void InitVehicle(BaseEntity vehicle)
		{
			if (vehicle == null || vehicle.net == null) return;
			VehicleType type;
			switch (vehicle)
			{
				case RidableHorse:
					type = VehicleType.Horse;
					break;
				case Bike bike:
					if (bike.poweredBy == Bike.PoweredBy.Fuel)
						type = VehicleType.MotorBike;
					else
						type = VehicleType.Bike;
					break;
				case ModularCar:
					type = VehicleType.Car;
					break;
				case HotAirBalloon:
					type = VehicleType.Balloon;
					break;
				case Minicopter:
					type = VehicleType.Minicopter;
					break;
				case ScrapTransportHelicopter:
					type = VehicleType.TransportHeli;
					break;
				case AttackHelicopter:
					type = VehicleType.AttackHeli;
					break;
				case RHIB:
					type = VehicleType.RHIB;
					break;
				case Tugboat:
					type = VehicleType.TugBoat;
					break;
				case MotorRowboat:
					type = VehicleType.RowBoat;
					break;
				case SubmarineDuo:
					type = VehicleType.SubmarineTwo;
					break;
				case BaseSubmarine:
					type = VehicleType.SubmarineOne;
					break;
				case Snowmobile:
					type = VehicleType.Snowmobile;
					break;
				default:
					type = VehicleType.None;
					break;
			}
			if (type == VehicleType.None) return;
			var vehicleID = vehicle.net.ID.Value;
			ulong creatorID = vehicle.creatorEntity is BasePlayer ownerPlayer ? ownerPlayer.userID : 0;
			if (!_vehiclesList.ContainsKey(vehicleID) || _vehiclesList[vehicleID] == null)
				_vehiclesList[vehicleID] = new VehicleData(vehicleID, type, creatorID);
			if (vehicle is ModularCar car && car.CarLock != null && car.CarLock.WhitelistPlayers.Any())
			{
				var vehicleData = _vehiclesList[vehicleID];
				if (vehicleData.OwnerID == 0 || !car.CarLock.WhitelistPlayers.Contains(vehicleData.OwnerID))
                {
					vehicleData.OwnerID = car.CarLock.WhitelistPlayers[0];
					vehicleData.RegistrationDate = DateTime.UtcNow.ToString(TimeFormat);
				}
			}
		}
		private string GetMonumentName(string monumentID, ulong userID = 0uL, bool showSuffix = true) => (string)(MonumentsWatcher?.Call(MonumentGetMonumentDisplayName, monumentID, userID, showSuffix) ?? monumentID);
        		
		        object OnEntityTakeDamage(ResourceEntity resource, HitInfo info)
        {
            if (_config.PreventResourceGathering && info != null && info.InitiatorPlayer is BasePlayer attacker && attacker.userID.IsSteamId() &&
				!_unrestrictedLooters.Contains(attacker.userID) && !IsEntityInPvP(attacker.userID, resource.net.ID.Value))
			{
				object result = attacker.BinoMumkin();
                if (result != null)
                    SendMessage(attacker, "MsgCantGatherInBase");
                return result;
            }
            return null;
        }

		object CanLootEntity(BasePlayer player, BaseOven oven)
		{
			if (_unrestrictedLooters.Contains(player.userID)) return AdminOpenLoot(player, oven);
			if (IsEntityInPvP(player.userID, oven.net.ID.Value)) return null;
			if (oven.GetParentEntity() is BaseVehicleModule module)
				return CanLootCar(player, module);
			return CanLootStorage(player, oven, true);
		}
		private Dictionary<string, RRData> _randomRaidsList = new Dictionary<string, RRData>();
		object CanLootEntity(BasePlayer player, DroppedItemContainer container) => CanLootCombatEntity(player, container, container.playerSteamID);
		object CanLootEntity(BasePlayer player, LootableCorpse corpse) => CanLootCombatEntity(player, corpse, corpse.playerSteamID);
		
		void InitDynamicPVP()
		{
			if (_dynamicPvPs == null)
				_dynamicPvPs = new HashSet<string>();
			else
				_dynamicPvPs.Clear();
			
			var array = DynamicPVP.Call("AllDynamicPVPZones") as string[];
			if (array != null && array.Any())
            {
				string zoneID;
				for (int i = 0; i < array.Length; i++)
				{
					zoneID = array[i];
					OnCreatedDynamicPVP(zoneID, string.Empty, (Vector3)(ZoneManager?.Call("GetZoneLocation", zoneID) ?? Vector3.zero), 0f);
					
					var players = ZoneManager?.Call("GetPlayersInZone", zoneID) as List<BasePlayer>;
					if (players == null || !players.Any()) continue;
					foreach (var player in players)
						OnPlayerEnterPVP(player, zoneID);
				}
            }
			
			Subscribe(nameof(OnEntityEnterZone));
            Subscribe(nameof(OnEntityExitZone));
			Subscribe(nameof(OnCreateDynamicPVP));
            Subscribe(nameof(OnCreatedDynamicPVP));
            Subscribe(nameof(OnDeletedDynamicPVP));
		}
		
		protected override void SaveConfig() => Config.WriteObject(_config);
		object CanLootEntity(BasePlayer player, FishMount fishMount) => CanLootStorage(player, fishMount);
		
		public class TeamData
        {
			public ulong TeamID { get; set; }
			public bool FriendlyFire { get; set; }
			
			public TeamData() {}
			public TeamData(ulong id, bool fFire = false)
			{
				TeamID = id;
				FriendlyFire = fFire;
			}
		}
		
		private class RBConfig
        {
			[JsonProperty(PropertyName = "Is RaidableBases enabled?")]
			public bool IsEnabled = true;
			
			[JsonProperty(PropertyName = "RaidableBases Console command")]
			public string ConsoleCommand = "rbevent";
			
			[JsonProperty(PropertyName = "Settings for the RaidableBases")]
			public Dictionary<string, RBSettings> Settings = null;
			
			public Oxide.Core.VersionNumber Version;
		}
		
		private void ShowRaidableBasesOffer(BasePlayer player, RBData rbData)
		{
			DestroyUI(player, RBOfferUI);
			player.SendEffect();
			CuiHelper.AddUi(player, ReplacePlaceholders(_rbsUiOffer, null, (string)ImageLibrary?.Call("GetImage", RBOfferUI),
				string.Format(lang.GetMessage("MsgRaidableBasesOfferTitle", this, player.UserIDString), new string[] { lang.GetMessage($"MsgRaidableBases{rbData.Mode}", this, player.UserIDString) }),
				string.Format(lang.GetMessage("MsgRaidableBasesOfferDescription", this, player.UserIDString), new string[] { string.Format(_config.PriceFormat, rbData.Settings.Price.ToString()) }),
				$"{_commandUI} rb pay {rbData.RaidID.Replace(" ", "")}"));
			_playerUI[player.userID].Add(RBOfferUI);
		}
		
		private object CanLootCar(BasePlayer player, BaseVehicleModule module)
		{
			if (_vehiclesList.TryGetValue(module.VehicleParent()?.net.ID.Value ?? 0, out var vehicleData))
			{
				object result = vehicleData.CanLoot(player);
				if (result != null)
					SendMessage(player, "MsgVehicleCanNotInteract");
				return result;
			}
			return null;
		}
		private void SendMessage(BasePlayer player, string replyKey, string[] replyArgs = null, bool isWarning = true) => SendMessage(player.IPlayer, replyKey, replyArgs, isWarning);
        		
		        private const string MonumentOfferUI = "RealPVE_MonumentOffer";
		
		object OnEntityTakeDamage(HelicopterDebris debris, HitInfo info)
        {
			if (info == null || info.Initiator is not BasePlayer attacker || attacker == null || !attacker.userID.IsSteamId() || _unrestrictedLooters.Contains(attacker.userID)) return null;
            if (attacker.MumkinNol(debris.OwnerID) != null)
            {
				if (Friends != null && IsFriend(attacker.UserIDString, debris.OwnerID.ToString()))
					return null;
				info.Initiator = null;
				info.damageTypes.Clear();
			}
			return null;
        }
		private const string _beachPath = @"RealPVE\NewbieConfig";
		object OnRackedWeaponMount(Item item, BasePlayer player, WeaponRack rack) => CanLootWeaponRack(player, rack);
		object OnEntityTakeDamage(Bike bike, HitInfo info) => CanVehicleTakeDamage(bike.net?.ID.Value ?? 0uL, info);
		void OnEntitySpawned(HotAirBalloon balloon) => NextTick(() => { InitVehicle(balloon); });
		
		public class MarkersPvP
        {
			private VendingMachineMapMarker MainMarker;
			private MapMarkerGenericRadius CircleMarker;
			
			public MarkersPvP(Vector3 pos, string title, float radius = 0f, BaseEntity parentEntity = null)
            {
				if (radius <= 0f)
					radius = World.Size <= 3600 ? 0.5f : 0.25f;
				else
				{
					radius = radius / 135;
					if (radius > 1.5f)
						radius = 1.5f;
                }
				
				MainMarker = GameManager.server.CreateEntity(StringPool.Get(3459945130), pos) as VendingMachineMapMarker;
                if (MainMarker != null)
                {
                    MainMarker.markerShopName = title;
                    MainMarker.enabled = false;
                    MainMarker.Spawn();
                    if (parentEntity != null)
                    {
                        MainMarker.SetParent(parentEntity);
                        MainMarker.transform.localPosition = Vector3.zero;
                    }
                }
				
				CircleMarker = GameManager.server.CreateEntity(StringPool.Get(2849728229), pos) as MapMarkerGenericRadius;
                if (CircleMarker != null)
                {
                    CircleMarker.alpha = 0.75f;
                    CircleMarker.color1 = Color.red;
                    CircleMarker.color2 = Color.black;
					CircleMarker.radius = radius;
                    CircleMarker.Spawn();
                    if (parentEntity != null)
                    {
                        CircleMarker.SetParent(parentEntity);
                        CircleMarker.transform.localPosition = Vector3.zero;
                    }
                    CircleMarker.SendUpdate();
                }
            }
			
			public void Destroy()
            {
				if (MainMarker != null)
					MainMarker.Kill();
				if (CircleMarker != null)
					CircleMarker.Kill();
			}
        }
        
        		private void SendPvPBar(BasePlayer player, string zoneID)
        {
			if (!_statusIsLoaded) return;
			
			Dictionary<int, object> parameters;
			if (_monumentsList.TryGetValue(zoneID, out var monumentData))
			{
				parameters = new Dictionary<int, object>(monumentData.StatusBar)
				{
					{ 15, GetMonumentName(monumentData.MonumentID, player.userID, monumentData.Settings.ShowSuffix) },
					{ 22, lang.GetMessage("MsgMonumentIsPvP", this, player.UserIDString) }
				};
			}
			else if (_rbList.TryGetValue(zoneID, out var rbData))
			{
				parameters = new Dictionary<int, object>(rbData.StatusBar)
                {
                    { 15, string.Format(lang.GetMessage("MsgRaidableBasesBarText", this, player.UserIDString), lang.GetMessage(rbData.TextKey, this, player.UserIDString)) },
                    { 22, lang.GetMessage("MsgRaidableBasesIsPvP", Instance, player.UserIDString) }
                };
            }
			else
            {
				parameters = new Dictionary<int, object>(_pvpBar)
				{
					{ 0, zoneID }
				};
				
				string text = lang.GetMessage("MsgPvPBar", this, player.UserIDString);
				var text2 = Interface.CallHook(Hooks_OnZoneStatusText, player, zoneID) as string;
				if (text2 != null)
				{
					parameters[15] = text2;
					parameters[22] = text;
				}
				else
					parameters[15] = text;
			}

            AdvancedStatus?.Call(StatusCreateBar, player.userID.Get(), parameters);
		}

		void OnCargoShipHarborArrived(CargoShip cargoShip)
		{
			if (cargoShip.skinID != 0uL) return;
			MonumentData cargoData = null;
			MonumentData harborData = null;
			string[] monuments = GetMonumentsByPos(cargoShip.transform.position);
			foreach (var monumentID in monuments)
			{
				if (!_monumentsList.ContainsKey(monumentID))
					continue;
				if (cargoData == null && monumentID.StartsWith("CargoShip"))
					cargoData = _monumentsList[monumentID];
				else if (harborData == null && monumentID.Contains("harbor"))
					harborData = _monumentsList[monumentID];
			}
			if (cargoData == null || !cargoData.IsPvP || harborData == null || harborData.IsPvP ||
				!(harborData.MonumentID.Contains("harbor_1") ? _monumentsConfig.CargoShip_HarborToPvP : _monumentsConfig.CargoShip_LargeHarborToPvP))
				return;
			harborData.SetAsPvP();
		}
		object CanLootEntity(BasePlayer player, PlanterBox planter) => CanLootStorage(player, planter);
		private HashSet<BasePlayer> GetMonumentPlayers(string monumentID) => MonumentsWatcher?.Call(MonumentGetMonumentPlayers, monumentID) as HashSet<BasePlayer>;
		
                private void SendCounterBar(BasePlayer player, MonumentData monumentData, double endTime, double startTime = 0d)
        {
			if (!_statusIsLoaded) return;
			
			string monumentName = GetMonumentName(monumentData.MonumentID, player.userID, monumentData.Settings.ShowSuffix);
			Dictionary<int, object> parameters;
			if (startTime > 0d && endTime > startTime)
			{
				parameters = new Dictionary<int, object>(monumentData.StatusProgressBar)
				{
					{ 15, monumentName },
					{ 28, startTime },
					{ 29, endTime }
				};
			}
			else
			{
				parameters = new Dictionary<int, object>(monumentData.StatusBar)
				{
					{ 15, monumentName },
					{ 29, endTime }
				};
				parameters[2] = BarTimeCounter;
			}
			
			AdvancedStatus?.Call(StatusCreateBar, player.userID.Get(), parameters);
		}

        object OnGrowableGather(GrowableEntity plant, BasePlayer player)
        {
			if (_config.PreventResourceGathering && player != null && !_unrestrictedLooters.Contains(player.userID) && !IsEntityInPvP(player.userID, plant.net.ID.Value))
			{
                object result = player.BinoMumkin();
                if (result != null)
                    SendMessage(player, "MsgCantGatherInBase");
                return result;
            }
            return null;
        }

        object OnQuarryToggle(MiningQuarry mining, BasePlayer player)
        {
			if (player != null && !_unrestrictedLooters.Contains(player.userID) && !IsEntityInPvP(player.userID, mining.net.ID.Value))
			{
                object result = null;
                if (!mining.isStatic && mining.OwnerID.IsSteamId())
					result = player.TasirMumkin(mining.OwnerID);
				else if (_monumentsList.TryGetValue(GetPlayerMonument(player.userID), out var monumentData))
                    result = monumentData.CanLoot(player);
                if (result != null)
                    SendMessage(player, "MsgCantInteract");
                return result;
            }
            return null;
        }
		
		private string _vanillaEventsUiOffer;
		object OnEntityTakeDamage(VehicleModuleStorage module, HitInfo info) => CanModuleTakeDamage(module, info);
		private static Dictionary<ulong, PlayerPvP> _pvpPlayers;
		object OnBackpackDrop(Item backpack, PlayerInventory inventory) => false;
		
		object OnEntityTakeDamage(PatrolHelicopter patrol, HitInfo info)
        {
			if (info == null || info.Initiator is not BasePlayer attacker || attacker == null || !attacker.userID.IsSteamId()) return null;
			if (patrol.skinID != 0uL)
			{
				if (patrol.skinID == _rrPluginID)
                {
                    if (_config.RandomRaids_Enabled && _rrallPatrols.TryGetValue(patrol.net.ID.Value, out var rrData) && !rrData.PlayersList.Contains(attacker.userID))
                        goto cancel;
                }
			}
			else if (_eventsList.TryGetValue(patrol.net.ID.Value, out var patrolData))
            {
                if (patrolData.OwnerID == 0uL)
                {
                    if (!_economicsIsLoaded || patrolData.Settings.Price <= 0d || (patrolData.Settings.Price * GetEventPriceMultiplier(attacker.UserIDString)) <= 0d)
                        patrolData.SetNewOwner(attacker.userID);
                    else if (_playerUI.TryGetValue(attacker.userID, out var uiList) && !uiList.Contains(EventOfferUI))
                    {
                        ShowEventOffer(attacker, patrolData);
                        timer.Once(patrolData.Settings.OfferTime, () => { DestroyUI(attacker, EventOfferUI); });
					}
                    goto cancel;
                }
                else if (!patrolData.CanBeAttackedBy(attacker))
                {
					SendMessage(attacker, "MsgEventOccupied", new string[] { lang.GetMessage("MsgEventPatrolHelicopter", this, attacker.UserIDString), patrolData.OwnerName });
					goto cancel;
                }
            }
			return null;
		
		cancel:
            info.Initiator = null;
            info.damageTypes.Clear();
            return null;
        }

		public enum VehicleCategory
		{
			None,
			LandVehicle,
			AirVehicle,
			WaterVehicle,
			WinterVehicle,
			TrainVehicle
		}
		
		public class RBData
        {
			public string RaidID { get; private set; }
			public Vector3 Position { get; private set; } = Vector3.zero;
			public float RadiusSquared { get; private set; } = 2500f;
			
			private ulong _ownerID = 0uL;
			public ulong OwnerID
			{
				get => _ownerID;
				set
				{
					_ownerID = value;
					OwnerIDString = _ownerID.ToString();
					var owner = BasePlayer.FindAwakeOrSleepingByID(_ownerID);
					OwnerName = owner != null ? owner.displayName : (_ownerID == 0 ? "None" : OwnerIDString);
				}
			}
			public string OwnerIDString { get; private set; } = string.Empty;
			public string OwnerName { get; private set; } = string.Empty;
			
			public RaidableMode Mode { get; private set; } = RaidableMode.Disabled;
			public double StartTime { get; set; }
			public double DespawnTime { get; set; }
			public Dictionary<ulong, BasePlayer> ParticipantsList = Pool.Get<Dictionary<ulong, BasePlayer>>();
			public Dictionary<ulong, BasePlayer> PlayersList = Pool.Get<Dictionary<ulong, BasePlayer>>();
			public int LootRemain { get; set; }
			public RBSettings Settings { get; set; }
			public Dictionary<int, object> StatusBar { get; private set; }
			public Dictionary<int, object> StatusProgressBar { get; private set; }
			public string TextKey = string.Empty;
			
			public bool IsPvP { get; private set; }
			
			public RBData(string raidID, Vector3 pos, int mode, bool isPVP, float radius, ulong ownerID, DateTime despawnTime, int lootRemain, List<BasePlayer> intruders = null)
			{
				RaidID = raidID;
				Position = pos;
				RadiusSquared = radius * radius;
				OwnerID = ownerID;
				Mode = (RaidableMode)mode;
				if (_rbsConfig.Settings.TryGetValue(Mode.ToString(), out var rbSettings))
					Settings = rbSettings;
				else
				{
					Settings = Instance._defaultRaidableBasesConfig["Easy"];
					Instance.PrintWarning($"Settings for the mode '{Mode}' not found. Defaulting to 'Easy' settings.");
                }
				StartTime = _unixSeconds;
				DespawnTime = StartTime + despawnTime.Subtract(DateTime.Now).TotalSeconds;
				LootRemain = lootRemain;
				TextKey = $"MsgRaidableBases{Mode}";
				IsPvP = isPVP;
				
				var barSettings = Settings.Bar;
				StatusBar = new Dictionary<int, object>
                {
                    { 0, RaidID },
                    { 1, Instance.Name },
                    { 2, "Default" },
                    { 3, "RaidableBases" },
                    { 4, barSettings.Order },
                    { 5, barSettings.Height },
                    { 6, barSettings.Main_Color },
					{ 11, barSettings.Image_IsRawImage },
                    { 12, barSettings.Image_Color },
					{ 16, barSettings.Text_Size },
                    { 17, barSettings.Text_Color },
                    { 18, barSettings.Text_Font },
                    { 23, barSettings.SubText_Size },
                    { 24, barSettings.SubText_Color },
                    { 25, barSettings.SubText_Font }
                };
				
				if (barSettings.Main_Color.StartsWith("#"))
					StatusBar.Add(-6, barSettings.Main_Transparency);
				if (!string.IsNullOrWhiteSpace(barSettings.Main_Material))
					StatusBar.Add(7, barSettings.Main_Material);
				if (barSettings.Image_Color.StartsWith("#"))
                    StatusBar.Add(-12, barSettings.Image_Transparency);
				if (barSettings.Image_Outline_Enabled)
                {
					StatusBar.Add(13, barSettings.Image_Outline_Color);
					if (barSettings.Image_Outline_Color.StartsWith("#"))
						StatusBar.Add(-13, barSettings.Image_Outline_Transparency);
					StatusBar.Add(14, barSettings.Image_Outline_Distance);
                }
                if (barSettings.Text_Outline_Enabled)
                {
					StatusBar.Add(20, barSettings.Text_Outline_Color);
					if (barSettings.Text_Outline_Color.StartsWith("#"))
						StatusBar.Add(-20, barSettings.Text_Outline_Transparency);
					StatusBar.Add(21, barSettings.Text_Outline_Distance);
                }
                if (barSettings.SubText_Outline_Enabled)
                {
					StatusBar.Add(26, barSettings.SubText_Outline_Color);
					if (barSettings.SubText_Outline_Color.StartsWith("#"))
						StatusBar.Add(-26, barSettings.SubText_Outline_Transparency);
					StatusBar.Add(27, barSettings.SubText_Outline_Distance);
                }
				
				UpdateBars();

                if (intruders != null)
                {
                    foreach (var player in intruders)
                        OnPlayerEnter(player);
                }
            }
			
			public void OnPlayerEnter(BasePlayer player)
			{
				if (IsPvP || IsOwnerOrFriend(player))
                {
					ParticipantsList[player.userID] = player;
					Instance.SendRaidableBasesLootBar(player, this);
					if (IsPvP)
						Instance.OnPlayerEnterPVP(player, RaidID);
					else
						Instance.SendCounterBar(player, this);
				}
                else
				{
					PlayersList[player.userID] = player;
					Instance.SendRBStrangerBar(player, this);
                }
			}
			
			public void OnPlayerExit(BasePlayer player)
            {
				Instance.DestroyBar(player.userID, RaidID);
				if (ParticipantsList.Remove(player.userID))
					Instance.DestroyBar(player.userID, RBLootUI);
				else
					PlayersList.Remove(player.userID);
				
				if (IsPvP && _pvpPlayers.TryGetValue(player.userID, out var playerPvP))
					Instance.OnPlayerExitPVP(player, RaidID, Settings.PvPDelay);
			}
			
			public void OnLootUpdated(int lootRemain)
            {
				if (LootRemain == lootRemain) return;
				LootRemain = lootRemain;
				foreach (var player in ParticipantsList.Values)
					Instance.SendRaidableBasesLootBar(player, this);
			}
			
			public void DespawnTimeUpdated(DateTime despawnTime)
            {
				var newStamp = _unixSeconds + despawnTime.Subtract(DateTime.Now).TotalSeconds;
				if (DespawnTime == newStamp) return;
				DespawnTime = newStamp;
				if (IsPvP) return;
				foreach (var player in ParticipantsList.Values)
					Instance.RaidableBaseTimeUpdatedBar(player, this);
			}
			
			public bool IsPlayerInside(ulong userID) => ParticipantsList.ContainsKey(userID) || PlayersList.ContainsKey(userID);
			
			public void SetNewOwner(ulong userID)
            {
				if (IsPvP) return;
				OwnerID = userID;
                var playersList = ParticipantsList.Values.ToList();
                playersList.AddRange(PlayersList.Values);
                foreach (var rPlayer in playersList)
                {
                    OnPlayerExit(rPlayer);
                    OnPlayerEnter(rPlayer);
                }
			}
			
			public void OnTeamUpdated(BasePlayer player)
            {
				if (IsPvP || !OwnerID.IsSteamId()) return;
				if (OwnerID == player.userID)
                {
                    var playersList = ParticipantsList.Values.ToList();
                    playersList.AddRange(PlayersList.Values);
                    foreach (var rPlayer in playersList)
                    {
						OnPlayerExit(rPlayer);
						OnPlayerEnter(rPlayer);
                    }
				}
                else
				{
					OnPlayerExit(player);
					OnPlayerEnter(player);
				}
            }
			
			public void OnFriendUpdated(BasePlayer player, BasePlayer friend)
            {
				if (IsPvP || !OwnerID.IsSteamId()) return;
				if (OwnerID == player.userID)
				{
					OnPlayerExit(friend);
					OnPlayerEnter(friend);
				}
				else if (OwnerID == friend.userID)
				{
					OnPlayerExit(player);
					OnPlayerEnter(player);
				}
			}
			
			public bool IsOwnerOrFriend(BasePlayer looter)
            {
                if (looter.userID == OwnerID || (looter.Team != null && looter.Team.members.Contains(OwnerID)) || (Instance.Friends != null && Instance.IsFriend(looter.UserIDString, OwnerIDString)))
                    return true;
                return false;
            }
			
			public bool CanInteractWithRaid(ulong userID) => OwnerID == userID || ParticipantsList.ContainsKey(userID);
			
			public void UpdateBars()
            {
				var barSettings = Settings.Bar;
                StatusBar.Remove(10);
                StatusBar.Remove(9);
                StatusBar.Remove(8);
                if (!string.IsNullOrWhiteSpace(barSettings.Image_Sprite))
                    StatusBar.Add(10, barSettings.Image_Sprite);
                else if (!string.IsNullOrWhiteSpace(barSettings.Image_Local))
                    StatusBar.Add(9, barSettings.Image_Local);
                else
                    StatusBar.Add(8, _imgLibIsLoaded && barSettings.Image_Url.StartsWithAny(Instance.HttpScheme) ? $"{RBUI}_{Mode}" : barSettings.Image_Url);
				
				var progressBar = Settings.ProgressBar;
                StatusProgressBar = new Dictionary<int, object>(StatusBar)
                {
                    { 32, progressBar.Progress_Reverse },
                    { 33, progressBar.Progress_Color },
                    { -33, progressBar.Progress_Transparency },
                    { 34, progressBar.Progress_OffsetMin },
                    { 35, progressBar.Progress_OffsetMax }
                };
                StatusProgressBar[2] = "TimeProgressCounter";
                StatusProgressBar[6] = progressBar.Main_Color;

                if (progressBar.Main_Color.StartsWith("#"))
                    StatusProgressBar[-6] = progressBar.Main_Transparency;
                else
                    StatusProgressBar.Remove(-6);
			}
			
			public void Destroy()
			{
				foreach (var player in ParticipantsList.Values)
					Instance.DestroyBar(player.userID, RaidID);
				ParticipantsList.Clear();
				Pool.FreeUnmanaged(ref ParticipantsList);
				foreach (var player in PlayersList.Values)
					Instance.DestroyBar(player.userID, RaidID);
				PlayersList.Clear();
				Pool.FreeUnmanaged(ref PlayersList);
				Instance._rbList.Remove(RaidID);
			}
		}
		void OnRocketLaunched(BasePlayer player, BaseEntity entity) => entity.OwnerID = player.userID;
		
		private void DestroyBar(ulong userID, string barID) => AdvancedStatus?.Call(StatusDeleteBar, userID, barID, Name);

		void OnHarborEventStart(Vector3 pos, float radius)
		{
			_harborEventMonument = GetMonumentByPos(pos);
			if (!string.IsNullOrWhiteSpace(_harborEventMonument) && _monumentsList.TryGetValue(_harborEventMonument, out var monumentData))
			{
				if (!_monumentsConfig.HarborEvent_HarborsToPvP)
					monumentData.Destroy(_harborEventMonument);
				else if (!monumentData.IsPvP)
					monumentData.SetAsPvP();
				else
					_harborEventMonument = string.Empty;
			}
			else
				_harborEventMonument = string.Empty;
		}
		
		void OnAdvancedStatusLoaded()
		{
			_statusIsLoaded = true;
			var imgList = new HashSet<string>();
			
			if (!string.IsNullOrWhiteSpace(_config.BarPvP.Image_Local))
				imgList.Add(_config.BarPvP.Image_Local);
			foreach (var monumentSettings in _monumentsConfig.MonumentsSettings.Values)
			{
				if (!string.IsNullOrWhiteSpace(monumentSettings.Bar.Image_Local))
					imgList.Add(monumentSettings.Bar.Image_Local);
			}
			foreach (var rbSettings in _rbsConfig.Settings.Values)
			{
				if (!string.IsNullOrWhiteSpace(rbSettings.Bar.Image_Local))
					imgList.Add(rbSettings.Bar.Image_Local);
			}
			if (imgList.Any())
				AdvancedStatus?.Call("LoadImages", imgList);
			
			foreach (var monumentData in _monumentsList.Values)
				monumentData.UpdateBars();
			UpdatePvPBars();
			foreach (var rbData in _rbList.Values)
				rbData.UpdateBars();
		}
		void OnSupplyDropDropped(SupplyDrop supplyDrop, CargoPlane plane) => supplyDrop.OwnerID = plane.OwnerID;

		void OnEntityKill(BuildingPrivlidge privlidge)
		{
			OnEntityExitPVP(privlidge);
			if (_config.AntiSleeper > 0f && privlidge.lastNoiseTime != DateTimeOffset.UtcNow.Day)
			{
				var players = Pool.Get<List<BasePlayer>>();
				Vis.Entities(privlidge.transform.position, 20f, players);
				foreach (var sleeper in players)
				{
					if (sleeper.IsNpc || sleeper.IsConnected || sleeper.IsAdmin) continue;
					sleeper.Invoke(Str_ScheduledDeath, _config.AntiSleeper);
				}
				Pool.FreeUnmanaged(ref players);
			}
		}
		
		
        object OnVehicleLockRequest(ModularCarGarage garage, BasePlayer player, string password) => false;
		private static bool IsEntityInPvP(ulong a, ulong b) => _pvpPlayers.ContainsKey(a) && _pvpEntities.Contains(b);

		object OnExcavatorSuppliesRequest(ExcavatorSignalComputer computer, BasePlayer player)
		{
			if (player != null && !_unrestrictedLooters.Contains(player.userID) && _monumentsList.TryGetValue(GetPlayerMonument(player.userID), out var monumentData))
			{
				object result = monumentData.CanLoot(player);
				if (result != null)
					SendMessage(player, "MsgCantInteract");
				return result;
			}
			return null;
		}
		object CanLootEntity(BasePlayer player, TorchDeployableLightSource torchDeployable) => CanLootStorage(player, torchDeployable);
		
		private void Command_RealPVE(IPlayer player, string command, string[] args)
        {
			string replyKey = "CmdCommandFailed";
			string[] replyArgs = new string[5];
			bool isWarning = true;
			var bPlayer = player.Object as BasePlayer;
			
			if (bPlayer == null)
				replyKey = "CmdCommandForPlayers";
			else if (args != null && args.Length > 0)
			{
				string effectName = string.Empty;
                if (args[0] == "pickup")
                {
                    if (_pickupPlayers.Remove(bPlayer.userID))
                        replyKey = "CmdPickupDisabled";
                    else
                    {
                        _pickupPlayers.Add(bPlayer.userID);
                        replyKey = "CmdPickupEnabled";
                        isWarning = false;
                    }
                }
                else if (args[0] == "vehicle")
                {
					replyKey = "CmdVehicleFailed";
					BaseEntity targetEntity;
                    if (args[1] == "clear")
                    {
                        int counter = 0;
                        foreach (var vehicleData in _vehiclesList.Values)
                        {
                            if (vehicleData.OwnerID != bPlayer.userID) continue;
                            targetEntity = BaseNetworkable.serverEntities.Find(new NetworkableId(vehicleData.ID)) as BaseEntity;
                            if (vehicleData.RemoveOwner(bPlayer, false) == null && targetEntity != null)
                            {
                                var car = targetEntity as ModularCar;
                                var privilege = targetEntity.children.OfType<VehiclePrivilege>().FirstOrDefault();
                                if (car != null)
                                    car.CarLock.RemoveLock();
                                if (privilege != null)
                                {
                                    privilege.authorizedPlayers.Clear();
                                    privilege.UpdateMaxAuthCapacity();
                                    privilege.SendNetworkUpdate();
                                }
                                counter++;
                            }
                        }
                        if (counter > 0)
                        {
                            replyKey = "CmdVehicleClear";
                            replyArgs[0] = counter.ToString();
                        }
                        else
                            replyKey = "CmdVehicleClearEmpty";
                        isWarning = false;
                    }
                    else if (GetEntityFromArg(bPlayer, args.Length > 2 ? args[2] : string.Empty, out targetEntity) && _vehiclesList.TryGetValue(targetEntity.net.ID.Value, out var vehicleData))
                    {
                        if (vehicleData.OwnerID == bPlayer.userID)
                        {
                            var car = targetEntity as ModularCar;
                            var privilege = targetEntity.children.OfType<VehiclePrivilege>().FirstOrDefault();
                            if (args[1] == "find")
                            {
                                replyKey = "CmdVehicleFind";
                                replyArgs[0] = vehicleData.Name;
                                replyArgs[1] = targetEntity.transform.position.GetGrid();
                                if (_config.VehiclesMarkerTime > 0f)
                                    bPlayer.AddPingAtLocation(BasePlayer.PingType.GoTo, targetEntity.transform.position + targetEntity.transform.up * 1f, _config.VehiclesMarkerTime, targetEntity.net.ID);
                            }
                            else if (args[1] == "unlink")
                            {
                                replyKey = string.Empty;
                                if (vehicleData.RemoveOwner(bPlayer) == null)
                                {
                                    if (car != null)
                                        car.CarLock.RemoveLock();
                                    effectName = "assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab";

                                    if (privilege != null)
                                    {
                                        privilege.authorizedPlayers.Clear();
                                        privilege.UpdateMaxAuthCapacity();
                                        privilege.SendNetworkUpdate();
                                    }
                                }
                            }
                            isWarning = false;
                        }
                        else
                            replyKey = "MsgVehicleNotOwner";
                    }
                    else
                        replyKey = "CmdVehicleNotFound";
                }
                else if (args[0] == "team")
                {
                    var pTeam = bPlayer.Team;
                    if (pTeam == null || !_teamsList.TryGetValue(pTeam.teamID, out var teamData))
                        replyKey = "CmdTeamNotFound";
                    else if (pTeam.teamLeader != bPlayer.userID)
                        replyKey = "CmdTeamNotLeader";
                    else if (args[1] == "ff")
                    {
                        replyKey = string.Empty;
                        string replyKey2;
                        string[] replyArgs2 = new string[1] { bPlayer.displayName };
                        teamData.FriendlyFire = !teamData.FriendlyFire;
                        if (teamData.FriendlyFire)
                            replyKey2 = "CmdTeamFireEnabled";
                        else
                        {
                            replyKey2 = "CmdTeamFireDisabled";
                            isWarning = false;
                        }
                        foreach (var mateID in pTeam.members)
                        {
                            var mate = RelationshipManager.FindByID(mateID);
                            if (mate != null && mate.IsConnected)
                                SendMessage(mate, replyKey2, replyArgs2, isWarning);
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(effectName))
                    bPlayer.RunEffect(effectName);
			}
			
			if (!string.IsNullOrWhiteSpace(replyKey))
				SendMessage(player, replyKey, replyArgs, isWarning);
		}
		
		private VehicleType GetVehicleActionType(string action)
		{
			switch (action)
			{
				case "habbuy":
					return VehicleType.Balloon;
				case "minicopterbuy":
					return VehicleType.Minicopter;
				case "transportbuy":
					return VehicleType.TransportHeli;
				case "attackbuy":
					return VehicleType.AttackHeli;
				case "pay_rowboat":
					return VehicleType.RowBoat;
				case "pay_rhib":
					return VehicleType.RHIB;
				case "pay_sub":
					return VehicleType.SubmarineOne;
				case "pay_duosub":
					return VehicleType.SubmarineTwo;
				default:
					return VehicleType.None;
			}
		}
		private void SaveBeachConfig(string path = _beachPath) => Interface.Oxide.DataFileSystem.WriteObject(path, _beachConfig);

		private PvEPermission GetVehiclePricePermission(string userID, VehicleType type)
		{
			PvEPermission result = null;
			foreach (var perm in _permissionsConfig.PermissionsList)
			{
				if (permission.UserHasPermission(userID, perm.Name))
				{
					if (result == null)
						result = perm;
					else if (perm.Allowed_Vehicles[type].Price < result.Allowed_Vehicles[type].Price)
						result = perm;
				}
			}
			return result ?? _permissionsConfig.PermissionsList[0];
		}
		
		object OnItemPickup(Item item, BasePlayer player)
        {
			if (item.GetWorldEntity() is not DroppedItem dropped || dropped == null || _unrestrictedLooters.Contains(player.userID) || IsEntityInPvP(player.userID, dropped.net.ID.Value)) return null;
			
			object result = null;
			string replyKey = string.Empty;
			if (dropped.DroppedBy.IsSteamId())
			{
				if (!_pickupPlayers.Contains(dropped.DroppedBy))
				{
					result = player.TasirMumkin(dropped.DroppedBy);
					replyKey = "MsgCantPickup";
				}
			}
			else if (_monumentsConfig.OnlyOwnerPickup && _monumentsList.TryGetValue(GetPlayerMonument(player.userID), out var monumentData))
            {
				result = monumentData.CanPickup(player);
				replyKey = "MsgMonumentCantPickup";
			}
			
			if (result != null)
				SendMessage(player, replyKey);
			return result;
		}
		
		object OnVehiclePush(BaseVehicle vehicle, BasePlayer player)
        {
			if (!IsEntityInPvP(player.userID, vehicle.net.ID.Value) && _vehiclesList.TryGetValue(vehicle.net.ID.Value, out var vehicleData) && !player.Uyda())
            {
				object result = vehicleData.CanInteract(player);
				if (result != null && _monumentsList.TryGetValue(GetPlayerMonument(player.userID), out var monumentData))
					result = monumentData.CanLoot(player);
				if (result != null)
					SendMessage(player, "MsgVehicleCanNotInteract");
				return result;
			}
			return null;
		}
		
		void OnEntityKill(BradleyAPC bradley)
		{
			if (_eventsList.TryGetValue(bradley.net.ID.Value, out var eventData))
				eventData.OnParentDestroy(bradley.transform.position);
		}
		
		private void InitVehicles()
		{
			var existList = Pool.Get<List<ulong>>();
			foreach (var entity in BaseNetworkable.serverEntities)
			{
				if (entity is BaseVehicle vehicle)
					InitVehicle(vehicle);
				else if (entity is HotAirBalloon balloon)
					InitVehicle(balloon);
				else
					continue;
				if (entity.net != null)
					existList.Add(entity.net.ID.Value);
			}
			foreach (var vehID in _vehiclesList.Keys.ToList())
            {
				if (!existList.Contains(vehID))
					_vehiclesList.Remove(vehID);
            }
			Pool.FreeUnmanaged(ref existList);
		}
		private readonly List<string> _hooksConflict = new List<string>() { "Calling hook OnEntityTakeDamage resulted in a conflict", "Calling hook CanBeTargeted resulted in a conflict", "Calling hook OnNpcTarget resulted in a conflict", "Calling hook OnPlayerDropActiveItem resulted in a conflict", "Hook conflict while calling" };
		
		object OnEntityTakeDamage(BuildingBlock block, HitInfo info)
        {
			if (ConVar.Server.pve || info == null || info.damageTypes.GetMajorityDamageType() == DamageType.Decay || info.Initiator is not BasePlayer attacker || attacker == null ||
				IsEntityInPvP(attacker.userID, block.net.ID.Value)) return null;
			if (block.OwnerID.IsSteamId())
            {
                if (!attacker.userID.IsSteamId())
                {
                    if (attacker.skinID == _rrPluginID)
                    {
                        if (_config.RandomRaids_Enabled && _rrAllRaiders.TryGetValue(attacker.net.ID.Value, out var rrData) && !rrData.PlayersList.Contains(block.OwnerID))
                            goto cancel;
                    }
                }
                else if (!UrishMumkin(attacker, block.OwnerID))
                    goto cancel;
            }
            else if (attacker.userID.IsSteamId())
            {
                if (TryGetRaidBase(block.transform.position, out var rbData) && !rbData.CanInteractWithRaid(attacker.userID))
                    goto cancel;
            }
            return null;

        cancel:
            info.Initiator = null;
            info.damageTypes.Clear();
            return null;
        }
		object OnEntityTakeDamage(BaseSubmarine submarine, HitInfo info) => CanVehicleTakeDamage(submarine.net?.ID.Value ?? 0uL, info);
		
		void OnEntitySpawned(BradleyAPC bradley)
		{
			if (!_vanillaEventsConfig.EventBradleyAPC.IsEnabled) return;
			NextTick(() =>
            {
                if (bradley.skinID == 0uL)
                    _eventsList[bradley.net.ID.Value] = new EventData(bradley.net.ID.Value, EventType.BradleyAPC, _vanillaEventsConfig.EventBradleyAPC);
            });
		}
		object OnPlayerDrink(BasePlayer player, LiquidContainer container) => CanLootByOwnerID(player, container);
				
		        object OnEventJoin(BasePlayer player)
        {
			
			
			if (_pvpPlayers.ContainsKey(player.userID))
				return lang.GetMessage("MsgArenaWhilePvP", this, player.UserIDString);
			return null;
		}

		private bool CanPlayerBypassQueue(string userID)
		{
			foreach (var perm in _permissionsConfig.PermissionsList)
			{
				if (perm.BypassQueue && permission.UserHasPermission(userID, perm.Name))
					return true;
			}
			return _permissionsConfig.PermissionsList[0].BypassQueue;
		}
		
                private const ulong _rbPluginID = 14922524uL;
		
		private class MonumentConfig
        {
			[JsonProperty(PropertyName = "Time in seconds to return to the monument if the owner has left its boundaries")]
            public float TimeToComeBack = 15f;
			
			[JsonProperty(PropertyName = "Is it worth prohibiting players without access from picking up items in occupied monuments?")]
            public bool OnlyOwnerPickup = true;
			
			[JsonProperty(PropertyName = "Harbor — Is it worth making the Harbor monument a PvP zone when the PvP Cargo Ship(Vanilla) is docked?")]
			public bool CargoShip_HarborToPvP = true;
			
			[JsonProperty(PropertyName = "Large Harbor — Is it worth making the Large Harbor monument a PvP zone when the PvP Cargo Ship(Vanilla) is docked?")]
			public bool CargoShip_LargeHarborToPvP = true;
			
			[JsonProperty(PropertyName = "HarborEvent - Is it worth making the Harbors a PvP zone during the event?")]
            public bool HarborEvent_HarborsToPvP = true;
			
			[JsonProperty(PropertyName = "List of tracked types of monuments")]
			public string[] TrackedTypes = null;

			[JsonProperty(PropertyName = "List of IGNORED monument names. Example: powerplant_1")]
			public string[] IgnoredNames = null;
			
			[JsonProperty(PropertyName = "Settings for each monument")]
            public Dictionary<string, MonumentSettings> MonumentsSettings = null;
			
			public Oxide.Core.VersionNumber Version;
		}

        private void DestroyVehiclePanels(BasePlayer player)
        {
            if (player == null) return;
            DestroyUI(player, _uiVehiclePanel);
        }
		
		object OnPortalUse(BasePlayer player, HalloweenDungeon halloween) => CanUsePortal(player, halloween);
		
		public class RBSettings
        {
			[JsonProperty(PropertyName = "Time in seconds (1-15) given to respond for purchasing a Raidable Base. Note: This is shown to everyone who enters, and the first person to buy it will claim it")]
			public float OfferTime { get; set; } = 5f;
			
			public double Price { get; set; }
			
			[JsonProperty(PropertyName = "Is it worth using a progress bar for bars with a counter?")]
			public bool UseProgress { get; set; } = true;

			[JsonProperty(PropertyName = "PvP - Sets the delay in seconds that a player remains in PvP mode after leaving a PvP RaidableBase. 0 disables the delay")]
			public float PvPDelay { get; set; } = 10f;
			
			[JsonProperty(PropertyName = "Settings for the status bar")]
			public BarSettings Bar { get; set; }
			
			[JsonProperty(PropertyName = "Settings for the progress status bar")]
			public ProgressBarSettings ProgressBar { get; set; }
		}
		
		object OnNpcConversationRespond(NPCTalking npcTalking, BasePlayer player, ConversationData conversationData, ConversationData.ResponseNode responseNode)
		{
			if (responseNode.resultingSpeechNode.StartsWith("pay_") || (responseNode.resultingSpeechNode != "prebuy" && responseNode.resultingSpeechNode.EndsWith("buy")))
			{
				var type = GetVehicleActionType(responseNode.resultingSpeechNode);
				if (type != VehicleType.None)
					return CanPurchaseVehicle(player, type, npcTalking);
			}
			return null;
		}
		
		private static bool UrishMumkin(BasePlayer a, ulong b) => b == a.userID || (a.Team != null && a.Team.members.Contains(b) && _teamsList.TryGetValue(a.currentTeam, out var teamData) && teamData.FriendlyFire);
		
		object OnEntityTakeDamage(BaseCorpse corpse, HitInfo info)
        {
			if (corpse.parentEnt is BaseAnimalNPC || info == null || info.Initiator is not BasePlayer attacker || attacker == null || !attacker.userID.IsSteamId()) return null;

			if (TryGetRaidBase(corpse.transform.position, out var rbData))
			{
				if (!rbData.CanInteractWithRaid(attacker.userID))
					goto cancel;
			}
			else if (_monumentsList.TryGetValue(GetEntityMonument(corpse), out var monumentData))
			{
				if (monumentData.CanLoot(attacker) != null)
					goto cancel;
			}
			else if (_config.PreventResourceGathering && attacker.BinoMumkin() != null)
				goto cancel;
			return null;
		
		cancel:
			info.Initiator = null;
			info.damageTypes.Clear();
			return null;
		}
		private string GetPlayerMonument(ulong userID) => (string)(MonumentsWatcher?.Call(MonumentGetPlayerMonument, userID) ?? string.Empty);
		
		object CanMountEntity(BasePlayer player, BaseMountable mountable) => CanInteractWithSeat(player, mountable);
				
				private static Configuration _config;
		
		object OnEntityTakeDamage(BaseNPC2 npc, HitInfo info) => null;
		
        		object OnNpcTarget(global::HumanNPC npc, BasePlayer target)
        {
            if (target.userID.IsSteamId())
            {
				if (npc.skinID == 0uL)
                {
                    if (_monumentsList.TryGetValue(GetNpcMonument(npc), out var monumentData))
                        return monumentData.CanLoot(target);
                    if (_monumentsList.TryGetValue(GetPlayerMonument(target.userID), out monumentData))
                        return monumentData.CanLoot(target);
                }
                else if (npc.skinID == _bradleySkinId)
                {
					if (_eventScientistsList.TryGetValue(npc.net.ID, out var eventData))
                        return eventData.CanBeTargeted(target);
                }
            }
            return null;
        }

		object CanLootEntity(BasePlayer player, Locker locker)
		{
			if (_unrestrictedLooters.Contains(player.userID)) return AdminOpenLoot(player, locker);
			if (IsEntityInPvP(player.userID, locker.net.ID.Value)) return null;
			if (locker.GetParentEntity() is BaseVehicleModule module)
				return CanLootCar(player, module);
			return CanLootStorage(player, locker, true);
		}

		object OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
			if (IsEntityInPvP(player.userID, privilege.net.ID.Value)) return null;
			object result = player.TasirMumkin(privilege.OwnerID);
			if (_config.RandomRaids_Enabled && result == null && _randomRaidsList.TryGetValue(privilege.transform.position.ToString(), out var rrData))
			{
				NextTick(() =>
				{
					if (player != null && privilege.IsAuthed(player))
					{
						_rrAllPlayers[player.userID] = rrData;
						rrData.PlayersList.Add(player.userID);
					}
				});
			}
			if (result != null)
            {
				if (_unrestrictedLooters.Contains(player.userID))
					AdminOpenLoot(player, privilege);
				else
					SendMessage(player, "MsgCantInteract");
			}
			return result;
		}
		
        		private const string _dataTeamsPath = @"RealPVE\Data\TeamsData";
		private string GetEntityMonument(BaseEntity entity) => (string)(MonumentsWatcher?.Call(MonumentGetEntityMonument, entity.net.ID) ?? string.Empty);
		
		object OnEngineStart(BaseVehicle vehicle, BasePlayer driver)
        {
			if (!IsEntityInPvP(driver.userID, vehicle.net.ID.Value) && GetVehicleData(vehicle, out var vehicleData))
            {
                object result = vehicleData.CanInteract(driver);
                if (result != null)
                    SendMessage(driver, "MsgVehicleCanNotInteract");
                return result;
            }
            return null;
		}
		
		void OnEntityExitedMonument(string monumentID, BaseEntity entity, string type, string reason, string newMonumentID)
        {
            if (_monumentsConfig.TrackedTypes.Contains(type) && _monumentsList.TryGetValue(monumentID, out var monumentData) && monumentData.IsPvP &&
				(!_monumentsList.TryGetValue(newMonumentID, out var newMonumentData) || !newMonumentData.IsPvP))
				OnEntityExitPVP(entity);
		}
		
		
		void OnPlayerLootEnd(PlayerLoot inventory)
		{
			if (inventory.entitySource is RidableHorse && inventory.baseEntity is BasePlayer player)
				DestroyVehiclePanels(player);
		}
		
		private void LoadRBsConfig()
        {
			List<CuiElement> uiList = null;
			if (Interface.Oxide.DataFileSystem.ExistsDatafile(_rbsPath))
			{
				try
				{
					_rbsConfig = Interface.Oxide.DataFileSystem.ReadObject<RBConfig>(_rbsPath);
					uiList = Interface.Oxide.DataFileSystem.ReadObject<List<CuiElement>>(_rbsUiOfferPath);
				}
				catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
			}
			
			if (_rbsConfig == null || _rbsConfig.Version < _rbsVersion)
            {
                if (_rbsConfig != null)
                {
                    string path = string.Format(_rbsPathOld, _rbsConfig.Version.ToString());
                    PrintWarning($@"Your settings version for raidable bases is outdated. The config file has been updated, and your old settings have been saved in \data\{path}");
                    SaveRBsConfig(path);
                }
				_rbsConfig = new RBConfig() { Version = _rbsVersion };
			}
			
			if (_rbsConfig.Settings == null || !_rbsConfig.Settings.Any())
				_rbsConfig.Settings = new Dictionary<string, RBSettings>(_defaultRaidableBasesConfig);
			
			if (uiList == null || !uiList.Any())
            {
                uiList = GetDefaultClaimOffer();
                Interface.Oxide.DataFileSystem.WriteObject(_rbsUiOfferPath, uiList);
            }
            _rbsUiOffer = ReplacePlaceholders(CuiHelper.ToJson(uiList), RBOfferUI);
			
			SaveRBsConfig();
		}
		object CanUpdateSign(BasePlayer player, CarvablePumpkin pumpkin) => !pumpkin.OwnerID.IsSteamId() || IsEntityInPvP(player.userID, pumpkin.net.ID.Value) ? null : player.TasirMumkin(pumpkin.OwnerID);

        private CuiElementContainer GetVehicleHorsePanel(string userID, VehicleData vehicleData)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "400 176", OffsetMax = "572 196" },
                Image = { Color = "0 0 0 0" }
            }, "Overlay", _uiVehiclePanel);
            if (vehicleData.OwnerID == 0 || vehicleData.IsOwner(userID))
            {
                container.Add(new CuiButton
                {
                    Text =
                    {
                        Text = lang.GetMessage(vehicleData.OwnerID == 0 ? "MsgVehicleDialogLink" : "MsgVehicleDialogUnLink", this,  userID),
                        Font = "RobotoCondensed-Regular.ttf",
                        FontSize = 12,
                        Color = WhiteColor,
                        Align = TextAnchor.MiddleCenter
                    },
                    Button =
                    {
                        Command = $"{_commandUI} vehicle {(vehicleData.OwnerID == 0 ? "link" : "unlink")} {vehicleData.ID}",
                        Color = vehicleData.OwnerID == 0 ? "0.41 0.55 0.41 0.8" : "1 0.4 0.4 0.8"
                    },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 2", OffsetMax = "-2 -2" }
                }, _uiVehiclePanel);
            }
            return container;
        }

		void OnMonumentsWatcherLoaded()
		{
			_watcherIsLoaded = true;
			InitMonuments();
		}
		object CanLootEntity(BasePlayer player, ChristmasTree tree) => CanLootStorage(player, tree);
		
		private static VanillaEventsConfig _vanillaEventsConfig;

        void OnDeletedDynamicPVP(string zoneID, string eventName)
        {
			_dynamicPvPs.Remove(zoneID);
			DeletePVPMapMarker(zoneID);
			
			string monumentID;
            HashSet<string> zones;
            var remMonuments = new List<string>();
            foreach (var kvp in _pvpChangedMonuments)
            {
                monumentID = kvp.Key;
                zones = kvp.Value;
                if (zones.Contains(zoneID))
                {
                    zones.Remove(zoneID);
                    if (!zones.Any())
                    {
                        remMonuments.Add(monumentID);
                        if (_monumentsList.TryGetValue(monumentID, out var monumentData))
                            monumentData.RemovePvP();
                    }
                }
            }
            foreach (var id in remMonuments)
                _pvpChangedMonuments.Remove(id);
		}
		object CanLootEntity(BasePlayer player, GunTrap gunTrap) => CanLootStorage(player, gunTrap);

		private void UpdateBuildingBlock(BuildingBlock block)
		{
			if (block != null && block.OwnerID.IsSteamId())
			{
				block.CancelInvoke(block.StopBeingRotatable);
				block.SetFlag(BaseEntity.Flags.Reserved1, true);

				block.CancelInvoke(block.StopBeingDemolishable);
				block.SetFlag(BaseEntity.Flags.Reserved2, true);
			}
		}
		private void CheckIfPlaced(BasePlayer player, int oldTotal, int limit, bool bag)
		{
			int newTotal = bag ? CountBeds(player.userID) : LegacyShelter.GetShelterCount(player.userID);
			if (oldTotal < newTotal)
				player.ShowToast(GameTip.Styles.Blue_Long, bag ? SleepingBag.bagLimitPhrase : LegacyShelter.shelterLimitPhrase, false, newTotal.ToString(), limit.ToString());
		}
		object OnEntityTakeDamage(RHIB rhib, HitInfo info) => CanVehicleTakeDamage(rhib.net?.ID.Value ?? 0uL, info);
		
		public enum RaidableMode { Disabled = -1, Easy = 0, Medium = 1, Hard = 2, Expert = 3, Nightmare = 4, Points = 8888, Random = 9999 }
		object OnEntityTakeDamage(Snowmobile snowmobile, HitInfo info) => CanVehicleTakeDamage(snowmobile.net?.ID.Value ?? 0uL, info);

		private bool IsFriend(string playerID, string friendID) => (bool)(Friends.Call(Str_IsFriend, playerID, friendID) ?? false);
		object CanLootEntity(BasePlayer player, PhotoFrame frame) => CanLootStorage(player, frame);
		object CanLootEntity(BasePlayer player, LockedByEntCrate crate) => CanLootByOwnerIDNol(player, crate);

		public class BarSettings
		{
			public int Order { get; set; } = 10;
			public int Height { get; set; } = 26;
			
			[JsonProperty(PropertyName = "Main_Color(Hex or RGBA)")]
			public string Main_Color { get; set; } = "#FFBF99";

			public float Main_Transparency { get; set; } = 0.8f;
			public string Main_Material { get; set; } = string.Empty;
			public string Image_Url { get; set; } = "https://i.imgur.com/mn8reWg.png";
			
			[JsonProperty(PropertyName = "Image_Local(Leave empty to use Image_Url)")]
			public string Image_Local { get; set; } = "RealPVE_Default";
			
			[JsonProperty(PropertyName = "Image_Sprite(Leave empty to use Image_Local or Image_Url)")]
			public string Image_Sprite { get; set; } = string.Empty;
			
			public bool Image_IsRawImage { get; set; }
			
			[JsonProperty(PropertyName = "Image_Color(Hex or RGBA)")]
			public string Image_Color { get; set; } = "#FFDCB6";
			
			public float Image_Transparency { get; set; } = 1f;
			
			[JsonProperty(PropertyName = "Is it worth enabling an outline for the image?")]
			public bool Image_Outline_Enabled { get; set; }
			
			[JsonProperty(PropertyName = "Image_Outline_Color(Hex or RGBA)")]
            public string Image_Outline_Color { get; set; } = "0.1 0.3 0.8 0.9";
			
			public float Image_Outline_Transparency { get; set; }
			public string Image_Outline_Distance { get; set; } = "0.75 0.75";
			public int Text_Size { get; set; } = 12;
			
			[JsonProperty(PropertyName = "Text_Color(Hex or RGBA)")]
			public string Text_Color { get; set; } = "1 1 1 1";
			
			[JsonProperty(PropertyName = "Text_Font(https://umod.org/guides/rust/basic-concepts-of-gui#fonts)")]
			public string Text_Font { get; set; } = "RobotoCondensed-Bold.ttf";
			
			[JsonProperty(PropertyName = "Is it worth enabling an outline for the text?")]
			public bool Text_Outline_Enabled { get; set; }
			
			[JsonProperty(PropertyName = "Text_Outline_Color(Hex or RGBA)")]
			public string Text_Outline_Color { get; set; } = "#000000";
			
			public float Text_Outline_Transparency { get; set; } = 1f;
			public string Text_Outline_Distance { get; set; } = "0.75 0.75";
			public int SubText_Size { get; set; } = 12;
			
			[JsonProperty(PropertyName = "SubText_Color(Hex or RGBA)")]
			public string SubText_Color { get; set; } = "1 1 1 1";
			public string SubText_Font { get; set; } = "RobotoCondensed-Bold.ttf";
			
			[JsonProperty(PropertyName = "Is it worth enabling an outline for the sub text?")]
			public bool SubText_Outline_Enabled { get; set; }
			
			[JsonProperty(PropertyName = "SubText_Outline_Color(Hex or RGBA)")]
			public string SubText_Outline_Color { get; set; } = "0.5 0.6 0.7 0.5";
			
			public float SubText_Outline_Transparency { get; set; }
			public string SubText_Outline_Distance { get; set; } = "0.75 0.75";
		}
		
		void OnTeamCreated(BasePlayer player, RelationshipManager.PlayerTeam team) => _teamsList[team.teamID] = new TeamData(team.teamID, _config.PvPTeamFF);

		object OnCupboardClearList(BuildingPrivlidge privilege, BasePlayer player)
        {
			if (IsEntityInPvP(player.userID, privilege.net.ID.Value)) return null;
			object result = player.TasirMumkin(privilege.OwnerID);
			if (_config.RandomRaids_Enabled && result == null && _randomRaidsList.TryGetValue(privilege.transform.position.ToString(), out var rrData))
			{
				List<ulong> list = privilege.authorizedPlayers.Select(p => p.userid).ToList();
				NextTick(() =>
				{
					foreach (var userID in list)
					{
						if (!privilege.IsAuthed(userID))
						{
							_rrAllPlayers.Remove(userID);
							rrData.PlayersList.Remove(userID);
						}
					}
					list.Clear();
				});
			}
			if (result != null)
				SendMessage(player, "MsgCantInteract");
			return result;
		}
		object OnRackedWeaponUnload(Item item, BasePlayer player, WeaponRack rack) => CanLootWeaponRack(player, rack);
		
		private bool TryGetPlayer(string nameOrId, out IPlayer result)
        {
            result = null;
            if (nameOrId.IsSteamId())
            {
                foreach (var player in covalence.Players.All)
                {
                    if (!player.IsServer && player.Id == nameOrId)
                    {
                        result = player;
                        break;
                    }
                }
            }
            else
            {
                nameOrId = nameOrId.ToLower();
                foreach (var player in covalence.Players.All)
                {
                    if (!player.IsServer && player.Name.ToLower() == nameOrId)
                    {
                        result = player;
                        break;
                    }
                }
            }
            return result != null;
        }
		private const string _rbsPathOld = @"RealPVE\_old_RaidableBasesConfig({0})";
		private static HashSet<ulong> _pvpEntities;
		private static Dictionary<ulong, TeamData> _teamsList;
		
		void CreatePVPMapMarker(string zoneID, Vector3 pos, float radius, string displayName = "", BaseEntity entity = null) =>
			_pvpMarkers[zoneID] = new MarkersPvP(pos, !string.IsNullOrWhiteSpace(displayName) ? displayName : _config.PvPMapMarkersName, radius, entity);

		private void HookConflict(string message, string stackTrace, UnityEngine.LogType type)
		{
			if (!string.IsNullOrEmpty(message) && !_hooksConflict.Any(message.Contains))
				Facepunch.Output.LogHandler(message, stackTrace, type);
		}
		
		object OnServerMessage(string message, string name)
		{
			if (name == "SERVER")
			{
				var words = message.Split(' ');
				if (words.Length > 1 && words[1] == "gave")
					return true;
			}
			return null;
		}

		private int GetRaidableBasesLimit(string userID)
		{
			int result = int.MinValue;
			foreach (var perm in _permissionsConfig.PermissionsList)
			{
				if (perm.RB_Limit > result && permission.UserHasPermission(userID, perm.Name))
					result = perm.RB_Limit;
			}
			return result == int.MinValue ? _permissionsConfig.PermissionsList[0].RB_Limit : result;
		}
		
		object CanUseLockedEntity(BasePlayer player, BaseLock baseLock)
        {
			if (_unrestrictedLooters.Contains(player.userID)) return true;
			if (baseLock.OwnerID.IsSteamId() && !IsEntityInPvP(player.userID, baseLock.net.ID.Value))
				return player.TasirMumkin(baseLock.OwnerID);
			return null;
		}
		
		void OnEntitySpawned(BaseVehicle vehicle) => NextTick(() => { InitVehicle(vehicle); });

		object OnCupboardDeauthorize(VehiclePrivilege privilege, BasePlayer player)
		{
			if (GetVehicleData(privilege.GetParentEntity(), out var vehicleData) && vehicleData.IsOwner(player.userID))
			{
				SendMessage(player, "MsgVehicleFailedDeauthorize");
				return false;
			}
			return null;
		}
		
		void OnEntitySpawned(DroppedItemContainer container)
        {
            
            if (!container.playerSteamID.IsSteamId() && TryGetRaidBase(container.transform.position, out var rbData))
                container.skinID = _rbPluginID;
        }
		private void OnFriendUpdated(string userID, string friendID)
		{
			var player = BasePlayer.Find(userID);
			if (player == null) return;
			var friend = BasePlayer.Find(friendID);
			if (friend == null) return;
			
			if (_monumentsList.TryGetValue(GetPlayerMonument(player.userID), out var monumentData))
				monumentData.OnFriendUpdated(player, friend);
			if (TryGetRaidBaseByUID(player.userID, out var rbData))
				rbData.OnFriendUpdated(player, friend);
		}
		
		object CanLootPlayer(BasePlayer target, BasePlayer looter)
		{
			if (_unrestrictedLooters.Contains(looter.userID)) return true;
			if (target.userID.IsSteamId() && !IsPlayerInPvP(looter.userID, target.userID))
			{
				object result = looter.TasirMumkin(target.userID);
				if (result != null)
					SendMessage(looter, "MsgCantInteractPlayer");
				return result;
			}
			return null;
		}

		public class PvEPermission
		{
			private int _beds;
			private int _shelters;
			private int _turrets;
			private float _hackableCrateSkip;
			private float _monumentMultiplier;
			private float _eventMultiplier;
			private int _rbLimit;
			private float _rbMultiplier;

			[JsonProperty(PropertyName = "Permission Name")]
			public string Name { get; set; }

			[JsonProperty(PropertyName = "Bypass Queue")]
			public bool BypassQueue { get; set; }

			[JsonProperty(PropertyName = "Limit of beds")]
			public int Beds
			{
				get => _beds;
				set => _beds = value >= 0 ? value : 0;
			}

			[JsonProperty(PropertyName = "Limit of shelters")]
			public int Shelters
			{
				get => _shelters;
				set => _shelters = value >= 0 ? value : 0;
			}

			[JsonProperty(PropertyName = "Limit of auto turrets")]
			public int Turrets
			{
				get => _turrets;
				set => _turrets = value >= 0 ? value : 0;
			}

			[JsonProperty(PropertyName = "Seconds that will be skipped when opening HackableLockedCrate. Range from 0 to 900")]
			public float HackableCrateSkip
			{
				get => _hackableCrateSkip;
				set => _hackableCrateSkip = value >= 0 ? (value > 900 ? 900 : value) : 0;
			}

			[JsonProperty(PropertyName = "Monuments price multiplier")]
			public float Monument_Multiplier
			{
				get => _monumentMultiplier;
				set => _monumentMultiplier = value >= 0 ? value : 0f;
			}

			[JsonProperty(PropertyName = "Events price multiplier")]
			public float Event_Multiplier
			{
				get => _eventMultiplier;
				set => _eventMultiplier = value >= 0 ? value : 0f;
			}

			[JsonProperty(PropertyName = "Limit of RaidableBases(at the time)")]
			public int RB_Limit
			{
				get => _rbLimit;
				set => _rbLimit = value >= 0 ? value : 0;
			}

			[JsonProperty(PropertyName = "RaidableBases price multiplier")]
			public float RB_Multiplier
			{
				get => _rbMultiplier;
				set => _rbMultiplier = value >= 0 ? value : 0f;
			}

			[JsonProperty(PropertyName = "Vehicles settings")]
			public Dictionary<VehicleType, VehicleProperties> Allowed_Vehicles { get; set; }

			public PvEPermission()
			{
				Name = string.Empty;
				Allowed_Vehicles = InitVehicleLimits();
			}

			public PvEPermission(string name, bool bypass = false, int beds = 0, int shelters = 0, int turrets = 0, float hackableCrateSkip = 0f, float monuments = 0f, float events = 0f, int rb = 0, float rb_multiplier = 0f, int veh_limit = 0, float veh_price = 1f)
			{
				Name = name;
				BypassQueue = bypass;
				Beds = beds;
				Shelters = shelters;
				Turrets = turrets;
				HackableCrateSkip = hackableCrateSkip;
				Monument_Multiplier = monuments;
				Event_Multiplier = events;
				RB_Limit = rb;
				RB_Multiplier = rb_multiplier;
				Allowed_Vehicles = InitVehicleLimits(veh_limit, veh_price);
			}

			public Dictionary<VehicleType, VehicleProperties> InitVehicleLimits(int veh_limit = 0, float veh_price = 1f)
			{
				var result = new Dictionary<VehicleType, VehicleProperties>();
				foreach (VehicleType type in Enum.GetValues(typeof(VehicleType)))
				{
					switch (type)
					{
						case VehicleType.Horse:
							result.Add(type, new VehicleProperties() { Limit = veh_limit, Price = 10 * veh_price });
							break;
						case VehicleType.Bike:
							result.Add(type, new VehicleProperties() { Limit = veh_limit, Price = 5 * veh_price });
							break;
						case VehicleType.MotorBike:
							result.Add(type, new VehicleProperties() { Limit = veh_limit, Price = 20 * veh_price });
							break;
						case VehicleType.Car:
							result.Add(type, new VehicleProperties() { Limit = veh_limit, Price = 25 * veh_price });
							break;
						case VehicleType.Balloon:
							result.Add(type, new VehicleProperties() { Limit = veh_limit, Price = 20 * veh_price });
							break;
						case VehicleType.Minicopter:
							result.Add(type, new VehicleProperties() { Limit = veh_limit, Price = 25 * veh_price });
							break;
						case VehicleType.TransportHeli:
							result.Add(type, new VehicleProperties() { Limit = veh_limit, Price = 30 * veh_price });
							break;
						case VehicleType.AttackHeli:
							result.Add(type, new VehicleProperties() { Limit = veh_limit, Price = 30 * veh_price });
							break;
						case VehicleType.RHIB:
							result.Add(type, new VehicleProperties() { Limit = veh_limit, Price = 25 * veh_price });
							break;
						case VehicleType.TugBoat:
							result.Add(type, new VehicleProperties() { Limit = veh_limit, Price = 50 * veh_price });
							break;
						case VehicleType.RowBoat:
							result.Add(type, new VehicleProperties() { Limit = veh_limit, Price = 20 * veh_price });
							break;
						case VehicleType.SubmarineTwo:
							result.Add(type, new VehicleProperties() { Limit = veh_limit, Price = 30 * veh_price });
							break;
						case VehicleType.SubmarineOne:
							result.Add(type, new VehicleProperties() { Limit = veh_limit, Price = 25 * veh_price });
							break;
						case VehicleType.Snowmobile:
							result.Add(type, new VehicleProperties() { Limit = veh_limit, Price = 20 * veh_price });
							break;
						default:
							continue;
					}
				}
				return result;
			}
		}
		
		void OnRandomRaidRaiderSpawned(Vector3 pos, NPCPlayer raider)
		{
			if (_randomRaidsList.TryGetValue(pos.ToString(), out var rrData))
			{
				_rrAllRaiders[raider.net.ID.Value] = rrData;
				rrData.Raiders.Add(raider.net.ID.Value);
				_rrAllRaiders[raider.userID] = rrData;
				rrData.Raiders.Add(raider.userID);
			}
		}
			}
}

namespace Oxide.Plugins.ExtensionsRealPVE
{
	public static class ExtensionMethods
	{
		public static string GetGrid(this Vector3 a) { var b = TerrainMeta.Size.x / 1024f; var c = 7f; var d = new Vector2(TerrainMeta.NormalizeX(a.x), TerrainMeta.NormalizeZ(a.z)) * b * c; var e = Mathf.Floor(d.x) + 1f; return $"{(e / 26f > 1f ? (char)(64 + (int)(e / 26f)) : "")}{(char)(64 + (int)((e - 1) % 26 + 1))}{Mathf.Floor(b * c - d.y)}"; }
		public static Vector3 ToVector3(this string a) { try { a = a.Replace("(", "").Replace(")", "").Replace(" ", ""); var b = a.Split(','); return new Vector3(float.Parse(b[0]), float.Parse(b[1]), float.Parse(b[2])); } catch { return Vector3.zero; } }
		public static bool TryParseVector3(this string a, out Vector3 b) { b = a.ToVector3(); return b != Vector3.zero; }
		public static string FirstToUpper(this string a) => !string.IsNullOrWhiteSpace(a) ? char.ToUpper(a[0]) + a.Substring(1) : a;
		public static object TasirMumkin(this BasePlayer a, ulong b) => b == a.userID || (a.Team != null && a.Team.members.Contains(b)) ? null : false;
		public static object MumkinNol(this BasePlayer a, ulong b) => b == 0 || b == a.userID || (a.Team != null && a.Team.members.Contains(b)) ? null : false;
		public static object BinoMumkin(this BasePlayer a) { var b = a.GetBuildingPrivilege(); return b == null || b.IsAuthed(a) ? null : false; }
		public static bool Uyda(this BasePlayer a) { var b = a.GetBuildingPrivilege(); return b != null && b.IsAuthed(a); }
		public static void SendEffect(this BasePlayer a, string b = "assets/bundled/prefabs/fx/invite_notice.prefab") => EffectNetwork.Send(new Effect(b, a.transform.position, Vector3.zero), a.Connection);
		public static void RunEffect(this BaseEntity a, string b = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab") => Effect.server.Run(b, a, 0u, Vector3.zero, Vector3.zero);
	}
}