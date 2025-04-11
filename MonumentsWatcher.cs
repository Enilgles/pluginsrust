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
*      https://lone.design/vendor/iiiaka/
*      https://www.patreon.com/iiiaka
*      https://boosty.to/iiiaka
*  GitHub repository page: https://github.com/IIIaKa/MonumentsWatcher
*  
*  uMod plugin page: https://umod.org/plugins/monuments-watcher
*  uMod license: https://umod.org/plugins/monuments-watcher#license
*  
*  Codefling plugin page: https://codefling.com/plugins/monuments-watcher
*  Codefling license: https://codefling.com/plugins/monuments-watcher?tab=downloads_field_4
*  
*  Lone.Design plugin page: https://lone.design/product/monuments-watcher/
*
*  Copyright © 2024-2025 IIIaKa
*/

using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using Rust;
using Facepunch;
using Newtonsoft.Json;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
	[Info("Monuments Watcher", "IIIaKa", "0.1.7")]
	[Description("A plugin that allows other plugins to interact with players and entities in monuments via API.")]
	class MonumentsWatcher : RustPlugin
    {
		#region ~Variables~
        private static MonumentsWatcher Instance { get; set; }
		private const string PERMISSION_ADMIN = "monumentswatcher.admin", Str_Leave = "leave", Str_Death = "death", Str_ClearList = "clearlist", Str_CargoShip = "CargoShip",
			Hooks_OnLoaded = "OnMonumentsWatcherLoaded", Hooks_OnCargoWatcherCreated = "OnCargoWatcherCreated", Hooks_OnCargoWatcherDeleted = "OnCargoWatcherDeleted",
			Hooks_OnPlayerEnteredMonument = "OnPlayerEnteredMonument", Hooks_OnNpcEnteredMonument = "OnNpcEnteredMonument", Hooks_OnEntityEnteredMonument = "OnEntityEnteredMonument",
			Hooks_OnPlayerExitedMonument = "OnPlayerExitedMonument", Hooks_OnNpcExitedMonument = "OnNpcExitedMonument", Hooks_OnEntityExitedMonument = "OnEntityExitedMonument";
        private static Hash<string, MonumentWatcher> _monumentsList;
		private static Hash<ulong, List<MonumentWatcher>> _playersInMonuments;
        private static Hash<NetworkableId, List<MonumentWatcher>> _npcsInMonuments;
        private static Hash<NetworkableId, List<MonumentWatcher>> _entitiesInMonuments;
        private BoundsValues _cargoBounds;
		#endregion

		#region ~Configuration~
		private static Configuration _config;
		
		private class Configuration
		{
			[JsonProperty(PropertyName = "Chat command")]
			public string Command = string.Empty;
			
			[JsonProperty(PropertyName = "Is it worth enabling GameTips for messages?")]
			public bool GameTips_Enabled = true;
			
			[JsonProperty(PropertyName = "Is it worth recreating boundaries(excluding custom monuments) upon detecting a wipe?")]
			public bool RecreateOnWipe = true;
			
			[JsonProperty(PropertyName = "List of tracked categories of monuments. Leave blank to track all")]
			public HashSet<MonumentCategory> TrackedCategories = null;
			
			[JsonProperty(PropertyName = "Wipe ID")]
			public string WipeID = string.Empty;
			
			public Oxide.Core.VersionNumber Version;
		}
		
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
			
			if (string.IsNullOrWhiteSpace(_config.Command))
				_config.Command = "monument";
			if (_config.TrackedCategories == null)
				_config.TrackedCategories = new HashSet<MonumentCategory>();
			
			SaveConfig();
		}
		
		protected override void SaveConfig() => Config.WriteObject(_config);
		protected override void LoadDefaultConfig() => _config = new Configuration() { Version = Version };
		#endregion

		#region ~Language~
		private Dictionary<string, string> _enLang = new Dictionary<string, string>
		{
			["CmdMain"] = string.Join("\n", new string[]
			{
				"Available commands:\n",
				"<color=#D1CBCB>/monument</color> <color=#D1AB9A>show *monumentID*(optional) *floatValue*(optional)</color> - Display the boundary of the monument you are in or specified. The display will last for the specified time or 30 seconds",
                "<color=#D1CBCB>/monument</color> <color=#D1AB9A>list</color> - List of available monuments",
                "<color=#D1CBCB>/monument</color> <color=#D1AB9A>rotate *monumentID*(optional) *floatValue*(optional)</color> - Rotate the monument you are in or specified, either in the direction you are looking or in the specified direction",
				"<color=#D1CBCB>/monument</color> <color=#D1AB9A>recreate custom/all(optional)</color> - Recreate the boundaries of vanilla/custom/all monuments",
				"\n<color=#D1CBCB>Note:</color> Instead of a monument ID, you can leave it empty, but you must be inside a monument. You can also use the word 'closest' to select the nearest monument to you",
				"\n--------------------------------------------------"
			}),
			["CmdMainShowNotFound"] = "Monument not found! You must be inside a monument, or specify the name or ID of the monument",
			["CmdMainShow"] = "Monument '{0}' is located at coordinates: {1}",
			["CmdMainShowList"] = "{1} monuments found and displayed for the key '{0}'",
			["CmdMainList"] = string.Join("\n", new string[]
			{
				"List of available monuments:\n",
				"{0}",
				"\n--------------------------------------------------"
			}),
			["CmdMainRotateNotFound"] = "You must be inside a monument and looking in the correct direction, or specify the name or ID of the monument along with the Y-coordinate for direction",
			["CmdMainRotated"] = "Successful rotation of the {0} by Y-coordinate({1})!",
			["CmdMainRecreated"] = "The boundaries of the monuments have been successfully recreated!",
			["CargoShip"] = "CargoShip",
			["airfield_1"] = "Airfield",
			["airfield_1_station"] = "Airfield Station",
			["arctic_research_base_a"] = "Arctic Research Base",
			["arctic_research_base_a_station"] = "Arctic Station",
			["bandit_town"] = "Bandit Camp",
			["bandit_town_station"] = "Bandit Station",
			["compound"] = "Outpost",
			["compound_station"] = "Outpost Station",
			["desert_military_base_a"] = "Abandoned Military Base",
			["desert_military_base_a_station"] = "Dune Station",
			["desert_military_base_b"] = "Abandoned Military Base",
			["desert_military_base_b_station"] = "Dune Station",
			["desert_military_base_c"] = "Abandoned Military Base",
			["desert_military_base_c_station"] = "Dune Station",
			["desert_military_base_d"] = "Abandoned Military Base",
			["desert_military_base_d_station"] = "Dune Station",
			["excavator_1"] = "Giant Excavator Pit",
			["excavator_1_station"] = "Excavator Station",
			["ferry_terminal_1"] = "Ferry Terminal",
			["ferry_terminal_1_station"] = "Ferry Terminal Station",
			["fishing_village_a"] = "Large Fishing Village",
			["fishing_village_a_station"] = "Large Fishing Station",
			["fishing_village_b"] = "Fishing Village",
			["fishing_village_b_station"] = "Fishing Station",
			["fishing_village_c"] = "Fishing Village",
			["fishing_village_c_station"] = "Fishing Station",
			["gas_station_1"] = "Oxum's Gas Station",
			["harbor_1"] = "Large Harbor",
			["harbor_1_station"] = "Large Harbor Station",
			["harbor_2"] = "Small Harbor",
			["harbor_2_station"] = "Harbor Station",
			["junkyard_1"] = "Junkyard",
			["junkyard_1_station"] = "Junkyard Station",
			["launch_site_1"] = "Launch Site",
			["launch_site_1_station"] = "Launch Site Station",
			["lighthouse"] = "Lighthouse",
			["military_tunnel_1"] = "Military Tunnel",
			["military_tunnel_1_station"] = "Military Tunnel Station",
			["mining_quarry_a"] = "Sulfur Quarry",
			["mining_quarry_b"] = "Stone Quarry",
			["mining_quarry_c"] = "HQM Quarry",
			["nuclear_missile_silo"] = "Missile Silo",
			["nuclear_missile_silo_station"] = "Silo Station",
			["oilrig_1"] = "Large Oil Rig",
			["oilrig_2"] = "Oil Rig",
			["powerplant_1"] = "Power Plant",
			["powerplant_1_station"] = "Power Plant Station",
			["radtown_1"] = "Toxic Village",
            ["radtown_1_station"] = "Toxic Village Station",
			["radtown_small_3"] = "Sewer Branch",
			["radtown_small_3_station"] = "Sewer Branch Station",
			["satellite_dish"] = "Satellite Dish",
			["satellite_dish_station"] = "Satellite Station",
			["sphere_tank"] = "The Dome",
			["sphere_tank_station"] = "The Dome Station",
			["stables_a"] = "Ranch",
			["stables_a_station"] = "Ranch Station",
			["stables_b"] = "Large Barn",
			["stables_b_station"] = "Barn Station",
			["station-sn-0"] = "Tunnel Station",
			["station-sn-1"] = "Tunnel Station",
			["station-sn-2"] = "Tunnel Station",
			["station-sn-3"] = "Tunnel Station",
			["station-we-0"] = "Tunnel Station",
			["station-we-1"] = "Tunnel Station",
			["station-we-2"] = "Tunnel Station",
			["station-we-3"] = "Tunnel Station",
			["supermarket_1"] = "Abandoned Supermarket",
			["swamp_a"] = "Wild Swamp",
			["swamp_b"] = "Wild Swamp",
			["swamp_c"] = "Abandoned Cabins",
			["trainyard_1"] = "Train Yard",
			["trainyard_1_station"] = "Train Yard Station",
			["underwater_lab_a"] = "Underwater Lab",
			["underwater_lab_b"] = "Underwater Lab",
			["underwater_lab_c"] = "Underwater Lab",
			["underwater_lab_d"] = "Underwater Lab",
			["warehouse"] = "Mining Outpost",
			["water_treatment_plant_1"] = "Water Treatment Plant",
			["water_treatment_plant_1_station"] = "Water Treatment Station",
			["entrance_bunker_a"] = "Bunker Entrance",
			["entrance_bunker_b"] = "Bunker Entrance",
			["entrance_bunker_c"] = "Bunker Entrance",
			["entrance_bunker_d"] = "Bunker Entrance",
			["cave_small_easy"] = "Small Cave",
			["cave_small_medium"] = "Medium Cave",
			["cave_small_hard"] = "Medium Cave",
			["cave_medium_easy"] = "Medium Cave",
			["cave_medium_medium"] = "Medium Cave",
			["cave_medium_hard"] = "Medium Cave",
			["cave_large_medium"] = "Medium Cave",
			["cave_large_hard"] = "Medium Cave",
			["cave_large_sewers_hard"] = "Large Cave",
			["ice_lake_1"] = "Ice Lake",
			["ice_lake_2"] = "Ice Lake",
			["ice_lake_3"] = "Large Ice Lake",
			["ice_lake_4"] = "Small Ice Lake",
			["power_sub_small_1"] = "Substation",
			["power_sub_small_2"] = "Substation",
			["power_sub_big_1"] = "Large Substation",
			["power_sub_big_2"] = "Large Substation",
			["water_well_a"] = "Water Well",
			["water_well_b"] = "Water Well",
			["water_well_c"] = "Water Well",
			["water_well_d"] = "Water Well",
			["water_well_e"] = "Water Well"
		};
		
		private Dictionary<string, string> _ruLang = new Dictionary<string, string>
		{
			["CmdMain"] = string.Join("\n", new string[]
			{
				"Доступные команды:\n",
				"<color=#D1CBCB>/monument</color> <color=#D1AB9A>show *айдиМонумента*(опционально) *дробноеЗначение*(опционально)</color> - Отобразить границу монумента, в котором вы находитесь или указали. Отображение будет в течении указанного времени или 30 секунд",
                "<color=#D1CBCB>/monument</color> <color=#D1AB9A>list</color> - Список доступных монументов",
                "<color=#D1CBCB>/monument</color> <color=#D1AB9A>rotate *айдиМонумента*(опционально) *дробноеЗначение*(опционально)</color> - Повернуть монумент, в котором вы находитесь или указали, в направлении вашего взгляда либо в указанном направлении",
                "<color=#D1CBCB>/monument</color> <color=#D1AB9A>recreate custom/all(опционально)</color> - Пересоздать границы ванильных/кастомных/всех монументов",
				"\n<color=#D1CBCB>Примечание:</color> Вместо айди монумента вы можете ничего не указывать, но должны находиться внутри монумента. Также можно указать слово 'closest', чтобы выбрать ближайший к вам монумент",
				"\n--------------------------------------------------"
			}),
			["CmdMainShowNotFound"] = "Монумент не найден! Вы должны находиться в монументе либо указать имя или ID монумента",
			["CmdMainShow"] = "Монумент '{0}' расположен по координатам: {1}",
			["CmdMainShowList"] = "По ключу '{0}' найдено и отображено {1} монументов",
			["CmdMainList"] = string.Join("\n", new string[]
            {
                "Список доступных монументов:\n",
                "{0}",
                "\n--------------------------------------------------"
            }),
			["CmdMainRotateNotFound"] = "Вы должны находиться в монументе и смотреть в нужном направлении, либо указать имя или ID монумента и Y-координату для направления",
			["CmdMainRotated"] = "Успешный поворот у {0} по Y координате({1})!",
			["CmdMainRecreated"] = "Границы монументов успешно пересозданы!",
			["CargoShip"] = "Грузовой корабль",
			["airfield_1"] = "Аэропорт",
			["airfield_1_station"] = "Станция Аэропорт",
			["arctic_research_base_a"] = "Арктическая база",
			["arctic_research_base_a_station"] = "Станция Арктическая",
			["bandit_town"] = "Лагерь бандитов",
			["bandit_town_station"] = "Станция бандитов",
			["compound"] = "Город",
			["compound_station"] = "Станция Город",
			["desert_military_base_a"] = "Заброшенная военная база",
			["desert_military_base_a_station"] = "Станция Дюна",
			["desert_military_base_b"] = "Заброшенная военная база",
			["desert_military_base_b_station"] = "Станция Дюна",
			["desert_military_base_c"] = "Заброшенная военная база",
			["desert_military_base_c_station"] = "Станция Дюна",
			["desert_military_base_d"] = "Заброшенная военная база",
			["desert_military_base_d_station"] = "Станция Дюна",
			["excavator_1"] = "Гигантский экскаватор",
			["excavator_1_station"] = "Станция Экскаваторная",
			["ferry_terminal_1"] = "Паромный терминал",
			["ferry_terminal_1_station"] = "Станция Паромщиков",
			["fishing_village_a"] = "Большая рыбацкая деревня",
			["fishing_village_a_station"] = "Станция Рыбаков",
			["fishing_village_b"] = "Рыбацкая деревня",
			["fishing_village_b_station"] = "Станция Рыбаков",
			["fishing_village_c"] = "Рыбацкая деревня",
			["fishing_village_c_station"] = "Станция Рыбаков",
			["gas_station_1"] = "Заправка",
			["harbor_1"] = "Большой порт",
			["harbor_1_station"] = "Станция Моряков",
			["harbor_2"] = "Порт",
			["harbor_2_station"] = "Станция Моряков",
			["junkyard_1"] = "Свалка",
			["junkyard_1_station"] = "Станция Мусорщиков",
			["launch_site_1"] = "Космодром",
			["launch_site_1_station"] = "Станция Космонавтов",
			["lighthouse"] = "Маяк",
			["military_tunnel_1"] = "Военные туннели",
			["military_tunnel_1_station"] = "Станция Туннельная",
			["mining_quarry_a"] = "Серный карьер",
			["mining_quarry_b"] = "Каменный карьер",
			["mining_quarry_c"] = "МВК карьер",
			["nuclear_missile_silo"] = "Ракетная пусковая шахта",
			["nuclear_missile_silo_station"] = "Станция Ракетная",
			["oilrig_1"] = "Большая нефтяная вышка",
			["oilrig_2"] = "Нефтяная вышка",
			["powerplant_1"] = "Электростанция",
			["powerplant_1_station"] = "Станция Электриков",
			["radtown_1"] = "Токсичная деревня",
            ["radtown_1_station"] = "Станция Легаси",
			["radtown_small_3"] = "Канализационный отвод",
			["radtown_small_3_station"] = "Станция Отвод",
			["satellite_dish"] = "Спутниковая тарелка",
			["satellite_dish_station"] = "Станция Связистов",
			["sphere_tank"] = "Сфера",
			["sphere_tank_station"] = "Станция Сфера",
			["stables_a"] = "Ранчо",
			["stables_a_station"] = "Станция Ранчо",
			["stables_b"] = "Большой амбар",
			["stables_b_station"] = "Станция Амбарная",
			["station-sn-0"] = "Станция метро",
			["station-sn-1"] = "Станция метро",
			["station-sn-2"] = "Станция метро",
			["station-sn-3"] = "Станция метро",
			["station-we-0"] = "Станция метро",
			["station-we-1"] = "Станция метро",
			["station-we-2"] = "Станция метро",
			["station-we-3"] = "Станция метро",
			["supermarket_1"] = "Супермаркет",
			["swamp_a"] = "Болото",
			["swamp_b"] = "Болото",
			["swamp_c"] = "Заброшенные хижины",
			["trainyard_1"] = "Железнодорожное депо",
			["trainyard_1_station"] = "Станция Железнодорожников",
			["underwater_lab_a"] = "Подводная лаборатория",
			["underwater_lab_b"] = "Подводная лаборатория",
			["underwater_lab_c"] = "Подводная лаборатория",
			["underwater_lab_d"] = "Подводная лаборатория",
			["warehouse"] = "Склад",
			["water_treatment_plant_1"] = "Очистные сооружения",
			["water_treatment_plant_1_station"] = "Станция Очистная",
			["entrance_bunker_a"] = "Вход в бункер",
			["entrance_bunker_b"] = "Вход в бункер",
			["entrance_bunker_c"] = "Вход в бункер",
			["entrance_bunker_d"] = "Вход в бункер",
			["cave_small_easy"] = "Маленькая пещера",
			["cave_small_medium"] = "Средняя пещера",
			["cave_small_hard"] = "Средняя пещера",
			["cave_medium_easy"] = "Средняя пещера",
			["cave_medium_medium"] = "Средняя пещера",
			["cave_medium_hard"] = "Средняя пещера",
			["cave_large_medium"] = "Средняя пещера",
			["cave_large_hard"] = "Средняя пещера",
			["cave_large_sewers_hard"] = "Большая пещера",
			["ice_lake_1"] = "Замерзшее озеро",
			["ice_lake_2"] = "Замерзшее озеро",
			["ice_lake_3"] = "Большое замерзшее озеро",
			["ice_lake_4"] = "Маленькое замерзшее озеро",
			["power_sub_small_1"] = "Подстанция",
			["power_sub_small_2"] = "Подстанция",
			["power_sub_big_1"] = "Большая подстанция",
			["power_sub_big_2"] = "Большая подстанция",
			["water_well_a"] = "Колодец с водой",
			["water_well_b"] = "Колодец с водой",
			["water_well_c"] = "Колодец с водой",
			["water_well_d"] = "Колодец с водой",
			["water_well_e"] = "Колодец с водой"
		};
		#endregion

		#region ~Methods~
		private void InitMonuments()
        {
			LoadDefaultBounds();
			LoadBoundsConfig(_monumentsBoundsPath, out _monumentsBounds);
			LoadBoundsConfig(_customMonumentsBoundsPath, out _customMonumentsBounds);
			ClearWatchers();
			foreach (var entity in BaseNetworkable.serverEntities)
            {
				if (entity is CargoShip cargoShip)
					CreateCargoWatcher(cargoShip);
            }
			string monumentKey, prefab;
			int miningoutpost = 0, lighthouse = 0, gasstation = 0, supermarket = 0, tunnel = 0, bunker = 0, cave = 0, icelake = 0, power = 0, waterwell = 0;
			foreach (var monument in TerrainMeta.Path.Monuments)
            {
				prefab = monument.name.ToLower();
				if (prefab.Contains("monument_marker.prefab", StringComparison.OrdinalIgnoreCase))
                {
					monumentKey = monument.transform.root.name.ToLower();
					monumentKey = System.Text.RegularExpressions.Regex.Replace(monumentKey, @"[^\w\d]", string.Empty);
					CreateCustomWatcher(monumentKey, monument.transform, prefab, monument.transform.root.name);
					continue;
                }
				monumentKey = ClearMonumentName(prefab);
				if (!_defaultBounds.ContainsKey(monumentKey)) continue;
				if (monument.IsSafeZone)
                {
					CreateWatcher(monumentKey, MonumentCategory.SafeZone, monument.transform, prefab);
					continue;
                }
				switch (monumentKey)
                {
					case "oilrig_1":
					case "oilrig_2":
					case "underwater_lab_a":
					case "underwater_lab_b":
					case "underwater_lab_c":
					case "underwater_lab_d":
						CreateWatcher(monumentKey, MonumentCategory.RadTownWater, monument.transform, prefab);
						break;
					case "lighthouse":
						lighthouse++;
						CreateWatcher(monumentKey, MonumentCategory.RadTownSmall, monument.transform, prefab, $"_{lighthouse}", $"#{lighthouse}");
						break;
					case "gas_station_1":
						gasstation++;
						CreateWatcher(monumentKey, MonumentCategory.RadTownSmall, monument.transform, prefab, $"_{gasstation}", $"#{gasstation}");
						break;
					case "supermarket_1":
						supermarket++;
						CreateWatcher(monumentKey, MonumentCategory.RadTownSmall, monument.transform, prefab, $"_{supermarket}", $"#{supermarket}");
						break;
					case "warehouse":
						miningoutpost++;
						CreateWatcher(monumentKey, MonumentCategory.RadTownSmall, monument.transform, prefab, $"_{miningoutpost}", $"#{miningoutpost}");
						break;
					case "mining_quarry_a":
					case "mining_quarry_b":
					case "mining_quarry_c":
						CreateWatcher(monumentKey, MonumentCategory.MiningQuarry, monument.transform, prefab);
						break;
					case "swamp_a":
					case "swamp_b":
					case "swamp_c":
						CreateWatcher(monumentKey, MonumentCategory.Swamp, monument.transform, prefab);
						break;
					case "entrance_bunker_a":
					case "entrance_bunker_b":
					case "entrance_bunker_c":
					case "entrance_bunker_d":
						bunker++;
						CreateWatcher(monumentKey, MonumentCategory.BunkerEntrance, monument.transform, prefab, $"_{bunker}", $"#{bunker}");
						break;
					case "cave_small_easy":
					case "cave_small_medium":
					case "cave_small_hard":
					case "cave_medium_easy":
					case "cave_medium_medium":
					case "cave_medium_hard":
					case "cave_large_medium":
					case "cave_large_hard":
					case "cave_large_sewers_hard":
						cave++;
						CreateWatcher(monumentKey, MonumentCategory.Cave, monument.transform, prefab, $"_{cave}", $"#{cave}");
						break;
					case "ice_lake_1":
					case "ice_lake_2":
					case "ice_lake_3":
					case "ice_lake_4":
						icelake++;
						CreateWatcher(monumentKey, MonumentCategory.IceLake, monument.transform, prefab, $"_{icelake}", $"#{icelake}");
						break;
					case "power_sub_small_1":
					case "power_sub_small_2":
					case "power_sub_big_1":
					case "power_sub_big_2":
						power++;
						CreateWatcher(monumentKey, MonumentCategory.PowerSubstation, monument.transform, prefab, $"_{power}", $"#{power}");
						break;
					case "water_well_a":
					case "water_well_b":
					case "water_well_c":
					case "water_well_d":
					case "water_well_e":
						waterwell++;
						CreateWatcher(monumentKey, MonumentCategory.WaterWell, monument.transform, prefab, $"_{waterwell}", $"#{waterwell}");
						break;
					default:
						CreateWatcher(monumentKey, MonumentCategory.RadTown, monument.transform, prefab);
						break;
                }
			}
			float stationDistance = 100f;
			foreach (var station in TerrainMeta.Path.DungeonGridCells)
            {
				if (!station.name.Contains("/tunnel-station/station-", StringComparison.OrdinalIgnoreCase)) continue;
				prefab = station.name.ToLower();
				monumentKey = ClearMonumentName(prefab);
				MonumentWatcher parentWatcher = null;
				var groundPos = new Vector3(station.transform.position.x, TerrainMeta.HeightMap.GetHeight(station.transform.position), station.transform.position.z);
				foreach (var monument in _monumentsList.Values)
                {
					if (monument.Category != MonumentCategory.SafeZone && monument.Category != MonumentCategory.RadTown) continue;
					if (Vector3.Distance(monument.boxCollider.ClosestPointOnBounds(groundPos), groundPos) <= stationDistance)
						parentWatcher = monument;
                }
				
				if (parentWatcher != null)
                {
					CreateWatcher(monumentKey, MonumentCategory.TunnelStation, station.transform, prefab, $"_{parentWatcher.ID}");
					if (_monumentsList.TryGetValue($"{monumentKey}_{parentWatcher.ID}", out var stationWatcher))
						stationWatcher.LangKey = $"{parentWatcher.ID}_station";
					continue;
				}
				
				tunnel++;
				CreateWatcher(monumentKey, MonumentCategory.TunnelStation, station.transform, prefab, $"_{tunnel}", $"#{tunnel}");
			}
			Subscribe(nameof(OnEntitySpawned));
			Subscribe(nameof(OnEntityDeath));
			Subscribe(nameof(OnEntityKill));
			Subscribe(nameof(OnPlayerTeleported));
			
			SaveBoundsConfig(_defaultBoundsPath, _defaultBounds);
			SaveBoundsConfig(_monumentsBoundsPath, _monumentsBounds);
			SaveBoundsConfig(_customMonumentsBoundsPath, _customMonumentsBounds);
			FreeBoundsConfig();
		}
		
		private void CreateCargoWatcher(CargoShip cargoShip)
        {
			if (!cargoShip.IsValid() || (_config.TrackedCategories.Any() && !_config.TrackedCategories.Contains(MonumentCategory.RadTownWater))) return;
			ulong cargoID = cargoShip.net.ID.Value;
			string monumentID = $"CargoShip_{cargoID}";
			
			var watcher = new GameObject().gameObject.AddComponent<MonumentWatcher>();
			watcher.InitializeProperties(monumentID, MonumentCategory.RadTownWater, cargoShip.name, Str_CargoShip, $"#{cargoID}");
			watcher.InitializeBounds(_cargoBounds.CenterOffset, _cargoBounds.Size, Quaternion.identity, cargoShip.transform);
			watcher.transform.parent = cargoShip.transform;
			_monumentsList[monumentID] = watcher;
			Interface.CallHook(Hooks_OnCargoWatcherCreated, monumentID, watcher.CategoryString, cargoShip);
		}
		
		private void CreateCustomWatcher(string monumentID, Transform transform, string prefab, string displayName)
        {
            _enLang[$"custom_{monumentID}"] = displayName;
            _ruLang[$"custom_{monumentID}"] = displayName;
            if (!_customMonumentsBounds.TryGetValue(monumentID, out var bounds) || bounds == null)
            {
                var rotation = transform.rotation.eulerAngles;
                var colArray = new Collider[5];
				for (var i = 0; i < Physics.OverlapSphereNonAlloc(transform.position, 1f, colArray, Rust.Layers.Mask.Prevent_Building, QueryTriggerInteraction.Ignore); i++)
                {
					var collider = colArray[i];
                    if (collider != null && collider.name.Contains("prevent_building", StringComparison.OrdinalIgnoreCase))
                    {
                        rotation = collider.transform.rotation.eulerAngles;
                        break;
                    }
                }
                _customMonumentsBounds[monumentID] = bounds = new CustomMonumentBounds(_defaultBounds["monument_marker"], transform.position, rotation, MonumentCategory.Custom);
            }
			
			if (bounds.MonumentCategory != MonumentCategory.Custom && _config.TrackedCategories.Any() && !_config.TrackedCategories.Contains(bounds.MonumentCategory)) return;
			var watcher = new GameObject().AddComponent<MonumentWatcher>();
			watcher.InitializeProperties(monumentID, bounds.MonumentCategory, prefab, $"custom_{monumentID}", isCustom: true);
			watcher.InitializeBounds(bounds.Center + (Quaternion.Euler(bounds.Rotation) * bounds.CenterOffset), bounds.Size, Quaternion.Euler(bounds.Rotation));
			_monumentsList[monumentID] = watcher;
		}
		
		private void CreateWatcher(string monumentKey, MonumentCategory category, Transform transform, string prefab, string idSuffix = "", string suffix = "")
        {
			if (_config.TrackedCategories.Any() && !_config.TrackedCategories.Contains(category)) return;
			string monumentID = $"{monumentKey}{(!string.IsNullOrWhiteSpace(idSuffix) ? idSuffix : string.Empty)}";
			if (!_monumentsBounds.TryGetValue(monumentID, out var bounds) || bounds == null)
				_monumentsBounds[monumentID] = bounds = new MonumentBounds(_defaultBounds[monumentKey], transform.position, transform.rotation.eulerAngles);
			
			var watcher = new GameObject().AddComponent<MonumentWatcher>();
			watcher.InitializeProperties(monumentID, category, prefab, monumentKey, suffix);
			watcher.InitializeBounds(bounds.Center + (Quaternion.Euler(bounds.Rotation) * bounds.CenterOffset), bounds.Size, Quaternion.Euler(bounds.Rotation));
			_monumentsList[monumentID] = watcher;
		}
		
		private void ClearWatchers()
        {
			Unsubscribe(nameof(OnEntitySpawned));
			Unsubscribe(nameof(OnEntityDeath));
			Unsubscribe(nameof(OnEntityKill));
			Unsubscribe(nameof(OnPlayerTeleported));
			foreach (var watcher in _monumentsList.Values.ToArray())
				UnityEngine.Object.DestroyImmediate(watcher.gameObject);
			_monumentsList.Clear();
			_playersInMonuments.Clear();
            _npcsInMonuments.Clear();
            _entitiesInMonuments.Clear();
		}
		
		private void HandleLanguageFile(Dictionary<string, string> langFile, string langKey = "en")
		{
			var existFile = lang.GetMessages(langKey, this);
			if (existFile == null || !existFile.Any())
			{
				if (!Directory.Exists(Path.Combine(Interface.Oxide.LangDirectory, langKey)))
					Directory.CreateDirectory(Path.Combine(Interface.Oxide.LangDirectory, langKey));
				File.WriteAllText(Path.Combine(Interface.Oxide.LangDirectory, $"{langKey}{Path.DirectorySeparatorChar}{this.Name}.json"), JsonConvert.SerializeObject(langFile, Formatting.Indented));
			}
			lang.RegisterMessages(langFile, this, langKey);
		}
		
		private bool TryGetPlayerWatcher(BasePlayer player, out MonumentWatcher result, bool closest = true)
        {
			result = null;
			if (_playersInMonuments.TryGetValue(player.userID, out var watchers) && watchers.Any())
				result = watchers[^1];
			else if (closest)
			{
				float minDistance = float.MaxValue;
				var pos = player.transform.position;
                foreach (var watcher in _monumentsList.Values)
                {
                    float distance = (pos - watcher.transform.position).sqrMagnitude;
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        result = watcher;
                    }
                }
			}
            return result != null;
        }
		
		private void ShowBounds(MonumentWatcher watcher, BasePlayer player, float duration = 20f)
        {
            if (watcher != null && player != null)
            {
                bool isAdmin = player.IsAdmin;
                try
                {
                    if (!isAdmin) UpdateFlag(player, BasePlayer.PlayerFlags.IsAdmin, true);
					
					//TEXT
                    player.SendConsoleCommand("ddraw.text", duration, Color.magenta, watcher.transform.position, watcher.ID);
					
					//CENTER
                    player.SendConsoleCommand("ddraw.sphere", duration, Color.green, watcher.transform.position, 1f);

                    //CORNERS
					Vector3 size = watcher.boxCollider.size * 0.5f,
						center = watcher.boxCollider.center;
					var transform = watcher.boxCollider.transform;
					Vector3[] corners = new Vector3[8],
                        offsets = new Vector3[8]
                        {
                            new Vector3(-size.x, -size.y, -size.z),
                            new Vector3(size.x, -size.y, -size.z),
                            new Vector3(size.x, -size.y, size.z),
                            new Vector3(-size.x, -size.y, size.z),
                            new Vector3(-size.x, size.y, -size.z),
                            new Vector3(size.x, size.y, -size.z),
                            new Vector3(size.x, size.y, size.z),
                            new Vector3(-size.x, size.y, size.z)
                        };
					for (int i = 0; i < offsets.Length; i++)
					{
						var corner = corners[i] = transform.TransformPoint(center + offsets[i]);
						player.SendConsoleCommand("ddraw.sphere", duration, Color.red, corner, 1f);
					}
					
					//LINES
                    for (int i = 0; i < corners.Length; i++)
                    {
						var startPos = corners[i];
						foreach (var endPos in corners)
						{
							if (endPos != startPos)
								player.SendConsoleCommand("ddraw.line", duration, Color.red, startPos, endPos);
						}
                    }
				}
                catch {}
                finally
                {
                    if (!isAdmin) UpdateFlag(player, BasePlayer.PlayerFlags.IsAdmin, false);
                }
            }
        }
		
		private void UpdateFlag(BasePlayer player, BasePlayer.PlayerFlags flag, bool addFlag)
		{
			if (player != null)
			{
				player.SetPlayerFlag(flag, addFlag);
				player.SendNetworkUpdateImmediate();
			}
		}
		
		private static string ClearMonumentName(string prefabName)
        {
            prefabName = prefabName.Replace(".prefab", string.Empty);
            string[] parts = prefabName.Split('/');

            return parts[^1];
        }
		
		private static void SendMessage(IPlayer player, string message, bool isWarning = true)
        {
            if (_config.GameTips_Enabled && !player.IsServer)
                player.Command("gametip.showtoast", (int)(isWarning ? GameTip.Styles.Error : GameTip.Styles.Blue_Long), message, string.Empty);
            else
                player.Reply(message);
        }
        #endregion

        #region ~API~
		private string[] GetAllMonuments() => _monumentsList.Keys.ToArray();
		private Dictionary<string, string> GetAllMonumentsCategories() => _monumentsList.ToDictionary(watcher => watcher.Key, watcher => watcher.Value.CategoryString);
		private string[] GetMonumentsByCategory(string category) => _monumentsList.Where(watcher => watcher.Value.CategoryString.Equals(category, StringComparison.OrdinalIgnoreCase)).Select(watcher => watcher.Key).ToArray();
		private string GetMonumentCategory(string monumentID) => _monumentsList.TryGetValue(monumentID, out var watcher) ? watcher.CategoryString : string.Empty;
		
		private string GetMonumentDisplayName(string monumentID, object obj, bool showSuffix = true) => GetMonumentDisplayName(monumentID, $"{obj}", showSuffix);
		private string GetMonumentDisplayName(string monumentID, ulong userID, bool showSuffix = true) => GetMonumentDisplayName(monumentID, $"{userID}", showSuffix);
		private string GetMonumentDisplayName(string monumentID, BasePlayer player, bool showSuffix = true) => GetMonumentDisplayName(monumentID, player.UserIDString, showSuffix);
		private string GetMonumentDisplayName(string monumentID, IPlayer player, bool showSuffix = true) => GetMonumentDisplayName(monumentID, player.Id, showSuffix);
		private string GetMonumentDisplayName(string monumentID, string userID = "", bool showSuffix = true) => GetMonumentDisplayNameByLang(monumentID, lang.GetLanguage(userID), showSuffix);
		
		private string GetMonumentDisplayNameByLang(string monumentID, string langKey = "en", bool showSuffix = true)
        {
			if (string.IsNullOrWhiteSpace(langKey) || langKey.Length != 2 || !langKey.All(c => char.IsLetter(c))) langKey = "en";
			if (_monumentsList.TryGetValue(monumentID, out var watcher))
                return $"{lang.GetMessageByLanguage(watcher.LangKey, this, langKey)}{(showSuffix && !string.IsNullOrWhiteSpace(watcher.Suffix) ? $" {watcher.Suffix}" : string.Empty)}";
            return string.Empty;
        }
		
		private Vector3 GetMonumentPosition(string monumentID) => _monumentsList.TryGetValue(monumentID, out var watcher) ? watcher.transform.position : Vector3.zero;
		
		private string GetMonumentByPos(Vector3 pos)
		{
			foreach (var watcher in _monumentsList.Values)
            {
				if (watcher.IsInBounds(pos))
					return watcher.ID;
			}
			return string.Empty;
		}
		
		private object GetMonumentsByPos(Vector3 pos)
        {
			var result = new List<string>();
            foreach (var watcher in _monumentsList.Values)
            {
				if (watcher.IsInBounds(pos))
                    result.Add(watcher.ID);
            }
			if (result.Any())
				return result.ToArray();
			return null;
		}
		
		private string GetClosestMonument(Vector3 pos)
        {
			MonumentWatcher result = null;
			float minDistance = float.MaxValue;
			foreach (var watcher in _monumentsList.Values)
            {
                float distance = (pos - watcher.transform.position).sqrMagnitude;
                if (distance < minDistance)
                {
                    minDistance = distance;
                    result = watcher;
                }
            }
			return result != null ? result.ID : string.Empty;
		}
		
		private bool IsPosInMonument(string monumentID, Vector3 pos)
		{
			if (_monumentsList.TryGetValue(monumentID, out var watcher))
				return watcher.IsInBounds(pos);
			return false;
		}
		
		private void ShowBounds(string monumentID, BasePlayer player, float duration = 20f)
		{
			if (player != null && _monumentsList.TryGetValue(monumentID, out var watcher))
				ShowBounds(watcher, player, duration);
		}
        #endregion

        #region ~API - Players~
		private object GetMonumentPlayers(string monumentID) => _monumentsList.TryGetValue(monumentID, out var watcher) ? watcher.PlayersList.ToArray() : null;
		
		private string GetPlayerMonument(object obj) => GetPlayerMonument($"{obj}");
		private string GetPlayerMonument(string userIDStr) => GetPlayerMonument(ulong.TryParse(userIDStr, out var userID) ? userID : 0uL);
		private string GetPlayerMonument(BasePlayer player) => GetPlayerMonument(player.userID);
		private string GetPlayerMonument(ulong userID) => _playersInMonuments.TryGetValue(userID, out var watchers) && watchers.Any() ? watchers[^1].ID : string.Empty;
		
		private object GetPlayerMonuments(object obj) => GetPlayerMonuments($"{obj}");
		private object GetPlayerMonuments(string userIDStr) => GetPlayerMonuments(ulong.TryParse(userIDStr, out var userID) ? userID : 0uL);
		private object GetPlayerMonuments(BasePlayer player) => GetPlayerMonuments(player.userID);
		private object GetPlayerMonuments(ulong userID)
        {
            if (_playersInMonuments.TryGetValue(userID, out var watchers) && watchers.Any())
            {
				string[] result = new string[watchers.Count];
                for (int i = 0; i < result.Length; i++)
                    result[i] = watchers[i].ID;
                return result;
            }
            return null;
        }
		
		private string GetPlayerClosestMonument(object obj) => GetPlayerClosestMonument($"{obj}");
        private string GetPlayerClosestMonument(string userIDStr) => GetPlayerClosestMonument(ulong.TryParse(userIDStr, out var userID) ? userID : 0uL);
        private string GetPlayerClosestMonument(ulong userID) => GetPlayerClosestMonument(BasePlayer.FindAwakeOrSleepingByID(userID));
        private string GetPlayerClosestMonument(BasePlayer player)
        {
            if (player != null)
            {
                if (_playersInMonuments.TryGetValue(player.userID, out var watchers) && watchers.Any())
                    return watchers[^1].ID;

                MonumentWatcher result = null;
                float minDistance = float.MaxValue;
                var pos = player.transform.position;
                foreach (var watcher in _monumentsList.Values)
                {
                    float distance = (pos - watcher.transform.position).sqrMagnitude;
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        result = watcher;
                    }
                }
                if (result != null)
                    return result.ID;
            }
            return string.Empty;
        }
		
		private bool IsPlayerInMonument(string monumentID, object obj) => IsPlayerInMonument(monumentID, $"{obj}");
		private bool IsPlayerInMonument(string monumentID, string userIDStr) => IsPlayerInMonument(monumentID, ulong.TryParse(userIDStr, out var userID) ? userID : 0uL);
		private bool IsPlayerInMonument(string monumentID, BasePlayer player) => IsPlayerInMonument(monumentID, player.userID);
		private bool IsPlayerInMonument(string monumentID, ulong userID) => _playersInMonuments.TryGetValue(userID, out var watchers) && _monumentsList.TryGetValue(monumentID, out var watcher) ? watchers.Contains(watcher) : false;
        #endregion

        #region ~API - NPCs~
		private object GetMonumentNpcs(string monumentID) => _monumentsList.TryGetValue(monumentID, out var watcher) ? watcher.NpcsList.ToArray() : null;
		
		private string GetNpcMonument(BasePlayer npcPlayer) => npcPlayer.IsValid() ? GetNpcMonument(npcPlayer.net.ID) : string.Empty;
		private string GetNpcMonument(NetworkableId netID) => _npcsInMonuments.TryGetValue(netID, out var watchers) && watchers.Any() ? watchers[^1].ID : string.Empty;
		
		private object GetNpcMonuments(BasePlayer npcPlayer) => npcPlayer.IsValid() ? GetNpcMonuments(npcPlayer.net.ID) : null;
		private object GetNpcMonuments(NetworkableId netID)
        {
            if (_npcsInMonuments.TryGetValue(netID, out var watchers) && watchers.Any())
            {
				string[] result = new string[watchers.Count];
                for (int i = 0; i < result.Length; i++)
                    result[i] = watchers[i].ID;
                return result;
            }
            return null;
        }
		
		private bool IsNpcInMonument(string monumentID, NetworkableId netID) => IsNpcInMonument(monumentID, BaseNetworkable.serverEntities.Find(netID) as BasePlayer);
		private bool IsNpcInMonument(string monumentID, BasePlayer npcPlayer) => npcPlayer.IsValid() && _npcsInMonuments.TryGetValue(npcPlayer.net.ID, out var watchers) && _monumentsList.TryGetValue(monumentID, out var watcher) ? watchers.Contains(watcher) : false;
        #endregion

        #region ~API - Entities~
		private object GetMonumentEntities(string monumentID) => _monumentsList.TryGetValue(monumentID, out var watcher) ? watcher.EntitiesList.ToArray() : null;
		
		private string GetEntityMonument(BaseEntity entity) => entity.IsValid() ? GetEntityMonument(entity.net.ID) : string.Empty;
		private string GetEntityMonument(NetworkableId netID)
        {
            if (_entitiesInMonuments.TryGetValue(netID, out var watchers) && watchers.Any())
                return watchers[^1].ID;
            return string.Empty;
        }
		
		private object GetEntityMonuments(BaseEntity entity) => entity.IsValid() ? GetEntityMonuments(entity.net.ID) : null;
		private object GetEntityMonuments(NetworkableId netID)
        {
            if (_entitiesInMonuments.TryGetValue(netID, out var watchers) && watchers.Any())
            {
				string[] result = new string[watchers.Count];
                for (int i = 0; i < result.Length; i++)
                    result[i] = watchers[i].ID;
                return result;
            }
            return null;
        }
		
		private bool IsEntityInMonument(string monumentID, NetworkableId netID) => IsEntityInMonument(monumentID, BaseNetworkable.serverEntities.Find(netID) as BaseEntity);
		private bool IsEntityInMonument(string monumentID, BaseEntity entity) => entity.IsValid() && _entitiesInMonuments.TryGetValue(entity.net.ID, out var watchers) && _monumentsList.TryGetValue(monumentID, out var watcher) ? watchers.Contains(watcher) : false;
		#endregion
		
		#region ~Oxide Hooks~
        void OnEntitySpawned(CargoShip cargoShip) => CreateCargoWatcher(cargoShip);
		
		void OnEntityDeath(BasePlayer player)
		{
			if (player.userID.IsSteamId())
            {
				if (_playersInMonuments.TryGetValue(player.userID, out var watchers))
				{
					for (int i = watchers.Count - 1; i >= 0; i--)
						watchers[i]?.OnPlayerExit(player, Str_Death);
				}
			}
			else if (_npcsInMonuments.TryGetValue(player.net.ID, out var watchers))
			{
				for (int i = watchers.Count - 1; i >= 0; i--)
                    watchers[i]?.OnNpcExit(player, Str_Death);
			}
		}
		
		void OnEntityKill(BaseEntity entity)
		{
			if (entity.IsValid() && _entitiesInMonuments.TryGetValue(entity.net.ID, out var watchers))
            {
				for (int i = watchers.Count - 1; i >= 0; i--)
					watchers[i]?.OnEntityExit(entity, Str_Death);
			}
		}
		
		void OnPlayerTeleported(BasePlayer player, Vector3 oldPos, Vector3 newPos)
        {
			if (_playersInMonuments.TryGetValue(player.userID, out var watchers))
            {
				MonumentWatcher watcher;
				for (int i = watchers.Count - 1; i >= 0; i--)
				{
					watcher = watchers[i];
					if (watcher != null && watcher.IsInBounds(oldPos) && !watcher.IsInBounds(newPos))
						watcher.OnPlayerExit(player, Str_Leave);
				}
            }
		}
		
		void Init()
        {
			Unsubscribe(nameof(OnEntitySpawned));
			Unsubscribe(nameof(OnEntityDeath));
			Unsubscribe(nameof(OnEntityKill));
			Unsubscribe(nameof(OnPlayerTeleported));
			Instance = this;
			permission.RegisterPermission(PERMISSION_ADMIN, this);
			AddCovalenceCommand(_config.Command, nameof(MonumentsWatcher_Command));
			_monumentsList = new Hash<string, MonumentWatcher>();
			_playersInMonuments = new Hash<ulong, List<MonumentWatcher>>();
			_npcsInMonuments = new Hash<NetworkableId, List<MonumentWatcher>>();
			_entitiesInMonuments = new Hash<NetworkableId, List<MonumentWatcher>>();
			string path = $@"MonumentsWatcher{Path.DirectorySeparatorChar}{{0}}";
			_defaultBoundsPath = string.Format(path, "DefaultBounds");
            _monumentsBoundsPath = string.Format(path, "MonumentsBounds");
            _customMonumentsBoundsPath = string.Format(path, "CustomMonumentsBounds");
		}
		
		void OnServerInitialized(bool initial)
        {
			if (initial)
            {
                Interface.Oxide.ReloadPlugin(Name);
                return;
            }
			
			if (string.IsNullOrWhiteSpace(_config.WipeID) || _config.WipeID != SaveRestore.WipeId)
			{
				_config.WipeID = SaveRestore.WipeId;
				if (_config.RecreateOnWipe)
				{
					_monumentsBounds = new Hash<string, MonumentBounds>();
					SaveBoundsConfig(_monumentsBoundsPath, _monumentsBounds);
					PrintWarning("Wipe detected! Monument boundaries(excluding custom ones) have been reset to ensure proper creation of new boundaries.");
				}
				SaveConfig();
            }
			InitMonuments();
			HandleLanguageFile(_enLang);
			_enLang.Clear();
			HandleLanguageFile(_ruLang, "ru");
			_ruLang.Clear();
			Interface.CallHook(Hooks_OnLoaded, Version);
		}
		
		void Unload()
        {
			ClearWatchers();
			_monumentsList = null;
			_playersInMonuments = null;
			_npcsInMonuments = null;
			_entitiesInMonuments = null;
			_defaultBounds = null;
			_monumentsBounds = null;
			_customMonumentsBounds = null;
			Instance = null;
			_config = null;
		}
		#endregion

        #region ~Commands~
		private static readonly string[] _cmdKeys = { "show", "list", "rotate", "recreate" };
		private void MonumentsWatcher_Command(IPlayer player, string command, string[] args)
		{
			if (!player.IsAdmin && !permission.UserHasPermission(player.Id, PERMISSION_ADMIN)) return;
            int index = args != null && args.Length > 0 ? Array.FindIndex(_cmdKeys, key => key.Equals(args[0], StringComparison.OrdinalIgnoreCase)) : -1;
			if (index < 0)
				goto notValid;
			
			var bPlayer = player.Object as BasePlayer;
			if (index == 0)
			{
				//show
				if (bPlayer == null)
				{
					player.Reply("This command is only available to players!");
					return;
				}
				
				var monumentsList = new List<MonumentWatcher>();
				if (args.Length < 2 || args[1].Equals("closest", StringComparison.OrdinalIgnoreCase))
				{
					if (TryGetPlayerWatcher(bPlayer, out var watcher))
						monumentsList.Add(watcher);
                }
				else if (args[1].Equals("help", StringComparison.OrdinalIgnoreCase))
					goto notValid;
				else if (_monumentsList.TryGetValue(args[1], out var watcher))
                    monumentsList.Add(watcher);
                else
                {
                    foreach (var watcher2 in _monumentsList.Values)
                    {
                        if (watcher2.LangKey == args[1] && !monumentsList.Contains(watcher2))
                            monumentsList.Add(watcher2);
                    }
                }
				
				int total = monumentsList.Count;
				if (total > 0)
                {
					if (args.Length < 3 || !float.TryParse(args[2], out var displayTime))
						displayTime = 30f;
					if (total == 1)
					{
						var watcher = monumentsList[0];
						ShowBounds(watcher, bPlayer, displayTime);
						SendMessage(player, string.Format(lang.GetMessage("CmdMainShow", this, player.Id), GetMonumentDisplayName(watcher.ID, player.Id), watcher.transform.position), false);
					}
					else
					{
                        foreach (var watcher in monumentsList)
                            ShowBounds(watcher, bPlayer, displayTime);
                        SendMessage(player, string.Format(lang.GetMessage("CmdMainShowList", this, player.Id), args[1], total), false);
                    }
				}
                else
					SendMessage(player, lang.GetMessage("CmdMainShowNotFound", this, player.Id));
				monumentsList.Clear();
            }
			else if (index == 1)
            {
				//list
				player.Reply(string.Format(lang.GetMessage("CmdMainList", this, player.Id), string.Join(", ", _monumentsList.Values.Select(watcher => watcher.ID).ToArray())));
            }
			else if (index == 2)
            {
                //rotate
                if (args.Length > 1 && args[1].Equals("help", StringComparison.OrdinalIgnoreCase))
					goto notValid;
				
				MonumentWatcher watcher = null;
                float yRot = 0f;
				if ((args.Length < 3 || !float.TryParse(args[2], out yRot)) && bPlayer != null)
					yRot = bPlayer.viewAngles.y;
				if ((args.Length < 2 || !_monumentsList.TryGetValue(args[1], out watcher)) && bPlayer != null)
					TryGetPlayerWatcher(bPlayer, out watcher, args.Length > 1 && args[1].Equals("closest", StringComparison.OrdinalIgnoreCase));
				
				if (watcher == null || watcher.ID.Contains(Str_CargoShip))
					SendMessage(player, lang.GetMessage("CmdMainRotateNotFound", this, player.Id));
				else
				{
					var newRot = new Vector3(0f, yRot, 0f);
					
					LoadBoundsConfig(_monumentsBoundsPath, out _monumentsBounds);
					if (_monumentsBounds.TryGetValue(watcher.ID, out var bounds))
					{
						bounds.Rotation = newRot;
						SaveBoundsConfig(_monumentsBoundsPath, _monumentsBounds);
					}
					else
                    {
						LoadBoundsConfig(_customMonumentsBoundsPath, out _customMonumentsBounds);
						if (_customMonumentsBounds.TryGetValue(watcher.ID, out var customBounds))
						{
							customBounds.Rotation = newRot;
                            SaveBoundsConfig(_customMonumentsBoundsPath, _customMonumentsBounds);
						}
					}
					FreeBoundsConfig();
					
					watcher.transform.rotation = Quaternion.Euler(newRot);
					ShowBounds(watcher, bPlayer, 30f);
					SendMessage(player, string.Format(lang.GetMessage("CmdMainRotated", this, player.Id), GetMonumentDisplayName(watcher.ID, player.Id), yRot), false);
				}
			}
			else if (index == 3)
            {
				//recreate
				string[] array = null;
                if (args.Length > 1)
                {
                    if (args[1].Equals("custom", StringComparison.OrdinalIgnoreCase))
                        array = new string[1] { $"{Name}{Path.DirectorySeparatorChar}CustomMonumentsBounds" };
                    else if (args[1].Equals("all", StringComparison.OrdinalIgnoreCase))
                        array = new string[2] { $"{Name}{Path.DirectorySeparatorChar}MonumentsBounds", $"{Name}{Path.DirectorySeparatorChar}CustomMonumentsBounds" };
                }
                if (array == null)
                    array = new string[1] { $"{Name}{Path.DirectorySeparatorChar}MonumentsBounds" };
                for (int i = 0; i < array.Length; i++)
                    Interface.Oxide.DataFileSystem.DeleteDataFile(array[i]);
                InitMonuments();
                SendMessage(player, lang.GetMessage("CmdMainRecreated", this, player.Id), false);
            }
			else
				goto notValid;
			return;
		
		notValid:
			player.Reply(lang.GetMessage("CmdMain", this, player.Id));
		}
		#endregion

        #region ~Bounds Config~
		public class BoundsValues
        {
            public Vector3 CenterOffset { get; set; }
            public Vector3 Size { get; set; }

            public BoundsValues(Vector3 offset, Vector3 size)
            {
                CenterOffset = offset;
                Size = size;
            }
        }
		
		public class MonumentBounds
        {
			public Vector3 CenterOffset { get; set; }
			public Vector3 Size { get; set; }
			public Vector3 Center { get; set; }
			public Vector3 Rotation { get; set; }
			
			public MonumentBounds() {}
			public MonumentBounds(BoundsValues bounds, Vector3 center, Vector3 rotation)
            {
				Center = center;
				Rotation = rotation;
				CenterOffset = bounds.CenterOffset;
				Size = bounds.Size;
			}
		}
		
		public class CustomMonumentBounds : MonumentBounds
		{ 
			public MonumentCategory MonumentCategory { get; set; }
			
			public CustomMonumentBounds() {}
			public CustomMonumentBounds(BoundsValues bounds, Vector3 center, Vector3 rotation, MonumentCategory monumentCategory)
				: base(bounds, center, rotation)
			{
				MonumentCategory = monumentCategory;
			}
		}
		
		private static Hash<string, BoundsValues> _defaultBounds;
        private static Hash<string, MonumentBounds> _monumentsBounds;
        private static Hash<string, CustomMonumentBounds> _customMonumentsBounds;
        private string _defaultBoundsPath = string.Empty, _monumentsBoundsPath = string.Empty, _customMonumentsBoundsPath = string.Empty;
		
		private void LoadDefaultBounds()
        {
			LoadBoundsConfig(_defaultBoundsPath, out _defaultBounds);
			if (_defaultBounds == null)
				_defaultBounds = new Hash<string, BoundsValues>();
			
			var initialBounds = new Hash<string, BoundsValues>()
			{
				{ Str_CargoShip, new BoundsValues(new Vector3(0, 17, 10), new Vector3(26, 60, 147)) },
				{ "airfield_1", new BoundsValues(new Vector3(0, 15, -25), new Vector3(360, 60, 210)) },
				{ "arctic_research_base_a", new BoundsValues(new Vector3(-2, 15, -2), new Vector3(120, 40, 115)) },
				{ "bandit_town", new BoundsValues(new Vector3(0, 15, -5), new Vector3(160, 45, 160)) },
				{ "compound", new BoundsValues(new Vector3(0, 15, 0), new Vector3(180, 60, 200)) },
				{ "desert_military_base_a", new BoundsValues(new Vector3(0, 20, 3), new Vector3(100, 45, 100)) },
				{ "desert_military_base_b", new BoundsValues(new Vector3(0, 20, 3), new Vector3(100, 45, 100)) },
				{ "desert_military_base_c", new BoundsValues(new Vector3(0, 20, 3), new Vector3(100, 45, 100)) },
				{ "desert_military_base_d", new BoundsValues(new Vector3(0, 20, 3), new Vector3(100, 45, 100)) },
				{ "excavator_1", new BoundsValues(new Vector3(20, 35, -23), new Vector3(245, 100, 245)) },
				{ "ferry_terminal_1", new BoundsValues(new Vector3(4, 12, 18), new Vector3(215, 45, 205)) },
				{ "fishing_village_a", new BoundsValues(new Vector3(-2, 5, 0), new Vector3(85, 40, 90)) },
				{ "fishing_village_b", new BoundsValues(new Vector3(-3, 5, 4), new Vector3(60, 40, 90)) },
				{ "fishing_village_c", new BoundsValues(new Vector3(-3, 5, 4), new Vector3(60, 40, 90)) },
				{ "gas_station_1", new BoundsValues(new Vector3(0, 10, 14), new Vector3(70, 30, 60)) },
				{ "harbor_1", new BoundsValues(new Vector3(0, 20, 42), new Vector3(235, 60, 265)) },
				{ "harbor_1_old", new BoundsValues(new Vector3(0, 20, 15), new Vector3(235, 60, 210)) },
				{ "harbor_2", new BoundsValues(new Vector3(20, 20, 12), new Vector3(230, 60, 260)) },
				{ "harbor_2_old", new BoundsValues(new Vector3(10, 20, 15), new Vector3(220, 60, 250)) },
				{ "junkyard_1", new BoundsValues(new Vector3(0, 15, 10), new Vector3(180, 40, 180)) },
				{ "launch_site_1", new BoundsValues(new Vector3(10, 28, -25), new Vector3(555, 125, 290)) },
				{ "lighthouse", new BoundsValues(new Vector3(8, 35, 2), new Vector3(60, 80, 60)) },
				{ "military_tunnel_1", new BoundsValues(new Vector3(0, 20, -20), new Vector3(270, 85, 245)) },
				{ "mining_quarry_a", new BoundsValues(new Vector3(0, 10, 0), new Vector3(65, 25, 75)) },
				{ "mining_quarry_b", new BoundsValues(new Vector3(-5, 10, -2), new Vector3(65, 25, 60)) },
				{ "mining_quarry_c", new BoundsValues(new Vector3(-5, 10, 5), new Vector3(50, 25, 65)) },
				{ "nuclear_missile_silo", new BoundsValues(new Vector3(10, 15, 0), new Vector3(140, 100, 120)) },
				{ "oilrig_1", new BoundsValues(new Vector3(3, 30, 12), new Vector3(85, 110, 120)) },
				{ "oilrig_2", new BoundsValues(new Vector3(18, 15, -2), new Vector3(75, 80, 85)) },
				{ "powerplant_1", new BoundsValues(new Vector3(-15, 30, -13), new Vector3(220, 75, 300)) },
				{ "radtown_1", new BoundsValues(new Vector3(2.75f, 7.5f, 0.5f), new Vector3(125, 20, 80)) },
				{ "radtown_small_3", new BoundsValues(new Vector3(0, 20, -13), new Vector3(150, 55, 150)) },
				{ "satellite_dish", new BoundsValues(new Vector3(0, 25, 5), new Vector3(170, 60, 140)) },
				{ "sphere_tank", new BoundsValues(new Vector3(0, 41, 5), new Vector3(110, 85, 110)) },
				{ "stables_a", new BoundsValues(new Vector3(5, 15, -3), new Vector3(70, 30, 80)) },
				{ "stables_b", new BoundsValues(new Vector3(8, 15, 8), new Vector3(85, 30, 80)) },
				{ "station-sn-0", new BoundsValues(new Vector3(0, 8, 0), new Vector3(105, 18, 215)) },
				{ "station-sn-1", new BoundsValues(new Vector3(0, 8, 0), new Vector3(105, 18, 215)) },
				{ "station-sn-2", new BoundsValues(new Vector3(0, 8, 0), new Vector3(105, 18, 215)) },
				{ "station-sn-3", new BoundsValues(new Vector3(0, 8, 0), new Vector3(105, 18, 215)) },
				{ "station-we-0", new BoundsValues(new Vector3(0, 8, 0), new Vector3(215, 18, 105)) },
				{ "station-we-1", new BoundsValues(new Vector3(0, 8, 0), new Vector3(215, 18, 105)) },
				{ "station-we-2", new BoundsValues(new Vector3(0, 8, 0), new Vector3(215, 18, 105)) },
				{ "station-we-3", new BoundsValues(new Vector3(0, 8, 0), new Vector3(215, 18, 105)) },
				{ "supermarket_1", new BoundsValues(new Vector3(2, 5, 0), new Vector3(50, 15, 50)) },
				{ "swamp_a", new BoundsValues(new Vector3(-11, 15, 3), new Vector3(160, 35, 190)) },
				{ "swamp_b", new BoundsValues(new Vector3(-1, 15, -3), new Vector3(125, 35, 125)) },
				{ "swamp_c", new BoundsValues(new Vector3(6, 15, -1), new Vector3(130, 35, 130)) },
				{ "trainyard_1", new BoundsValues(new Vector3(5, 25, -5), new Vector3(250, 80, 230)) },
				{ "underwater_lab_a", new BoundsValues(new Vector3(0, 15, 0), new Vector3(110, 25, 110)) },
				{ "underwater_lab_b", new BoundsValues(new Vector3(0, 15, 0), new Vector3(110, 25, 110)) },
				{ "underwater_lab_c", new BoundsValues(new Vector3(0, 15, 0), new Vector3(110, 25, 110)) },
				{ "underwater_lab_d", new BoundsValues(new Vector3(0, 15, 0), new Vector3(110, 25, 110)) },
				{ "warehouse", new BoundsValues(new Vector3(0, 5, -7), new Vector3(45, 15, 30)) },
				{ "water_treatment_plant_1", new BoundsValues(new Vector3(0, 30, -30), new Vector3(250, 90, 290)) },
				{ "entrance_bunker_a", new BoundsValues(new Vector3(-4, 1, -1), new Vector3(20, 30, 20)) },
				{ "entrance_bunker_b", new BoundsValues(new Vector3(-8, 1, 0), new Vector3(30, 30, 20)) },
				{ "entrance_bunker_c", new BoundsValues(new Vector3(-4, 1, -1), new Vector3(20, 30, 20)) },
				{ "entrance_bunker_d", new BoundsValues(new Vector3(-4, 1, -1), new Vector3(20, 30, 20)) },
				{ "cave_small_easy", new BoundsValues(new Vector3(6, -28, 17), new Vector3(45, 46, 66)) },
				{ "cave_small_medium", new BoundsValues(new Vector3(20, -30, -18), new Vector3(80, 50, 65)) },
				{ "cave_small_hard", new BoundsValues(new Vector3(8, -21, 0), new Vector3(45, 35, 80)) },
				{ "cave_medium_easy", new BoundsValues(new Vector3(8, -21, 0), new Vector3(45, 35, 80)) },
				{ "cave_medium_medium", new BoundsValues(new Vector3(-1, -25, 2), new Vector3(110, 50, 110)) },
				{ "cave_medium_hard", new BoundsValues(new Vector3(8, -21, 0), new Vector3(45, 35, 80)) },
				{ "cave_large_medium", new BoundsValues(new Vector3(8, -21, 0), new Vector3(45, 35, 80)) },
				{ "cave_large_hard", new BoundsValues(new Vector3(8, -21, 0), new Vector3(45, 35, 80)) },
				{ "cave_large_sewers_hard", new BoundsValues(new Vector3(50, -25, -7), new Vector3(170, 40, 165)) },
				{ "ice_lake_1", new BoundsValues(new Vector3(-2, 15, 0), new Vector3(140, 35, 160)) },
				{ "ice_lake_2", new BoundsValues(new Vector3(0, 15, 0), new Vector3(150, 35, 150)) },
				{ "ice_lake_3", new BoundsValues(new Vector3(0, 15, 0), new Vector3(180, 35, 240)) },
				{ "ice_lake_4", new BoundsValues(new Vector3(0, 15, 0), new Vector3(85, 35, 85)) },
				{ "power_sub_small_1", new BoundsValues(new Vector3(0, 5, 0), new Vector3(15, 10, 15)) },
				{ "power_sub_small_2", new BoundsValues(new Vector3(0, 5, 0), new Vector3(15, 10, 15)) },
				{ "power_sub_big_1", new BoundsValues(new Vector3(0, 5, 1), new Vector3(20, 10, 22)) },
				{ "power_sub_big_2", new BoundsValues(new Vector3(-1, 5, 1), new Vector3(23, 10, 22)) },
				{ "water_well_a", new BoundsValues(new Vector3(-2, 7, 0), new Vector3(25, 20, 25)) },
				{ "water_well_b", new BoundsValues(new Vector3(-1, 7, 0), new Vector3(25, 20, 25)) },
				{ "water_well_c", new BoundsValues(new Vector3(0, 10, 1), new Vector3(30, 25, 30)) },
				{ "water_well_d", new BoundsValues(new Vector3(0, 10, 1), new Vector3(30, 25, 30)) },
				{ "water_well_e", new BoundsValues(new Vector3(-1, 7, 0), new Vector3(25, 20, 25)) },
				{ "monument_marker", new BoundsValues(new Vector3(0, 0, 0), new Vector3(100, 100, 100)) }
			};
			
			foreach (var kvp in initialBounds)
			{
				if (!_defaultBounds.TryGetValue(kvp.Key, out var bounds) || bounds == null)
					_defaultBounds[kvp.Key] = kvp.Value;
			}
			initialBounds.Clear();
			_cargoBounds = _defaultBounds[Str_CargoShip];
		}
		
		private void FreeBoundsConfig()
        {
            _defaultBounds.Clear();
            _monumentsBounds.Clear();
            _customMonumentsBounds.Clear();
        }

        private static void LoadBoundsConfig<T>(string filePath, out T result) where T : new()
		{
            try { result = Interface.Oxide.DataFileSystem.ReadObject<T>(filePath); }
            catch (Exception ex) { UnityEngine.Debug.LogException(ex); result = new T(); }
            if (result == null)
            {
                result = new T();
                SaveBoundsConfig(filePath, result);
            }
		}
        private static void SaveBoundsConfig<T>(string filePath, T obj) => Interface.Oxide.DataFileSystem.WriteObject(filePath, obj);
        #endregion

        #region ~Monument Watcher~
		[JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
		public enum MonumentCategory
        {
            SafeZone,
            RadTown,
            RadTownWater,
            RadTownSmall,
            TunnelStation,
            MiningQuarry,
            BunkerEntrance,
            Cave,
            Swamp,
            IceLake,
            PowerSubstation,
            WaterWell,
            Custom
        }
		
		public class MonumentWatcher : MonoBehaviour
		{
			public string ID { get; private set; }
			
			private MonumentCategory _category;
			public MonumentCategory Category
            {
				get => _category;
				private set
                {
					_category = value;
					CategoryString = value.ToString();
				}
			}
			public string CategoryString { get; private set; }
			
			public string Prefab { get; private set; }
            public string LangKey { get; set; }
            public string Suffix { get; private set; }
			public bool IsMoveable { get; private set; }
			public bool IsCustom { get; private set; }
			public Vector3 Size { get; private set; }
			
			public HashSet<BasePlayer> PlayersList = Pool.Get<HashSet<BasePlayer>>();
			public HashSet<BasePlayer> NpcsList = Pool.Get<HashSet<BasePlayer>>();
			public HashSet<BaseEntity> EntitiesList = Pool.Get<HashSet<BaseEntity>>();
			private Rigidbody rigidbody;
			public BoxCollider boxCollider;
			public Bounds colliderBounds;
			
			private void Awake()
			{
				gameObject.layer = (int)Layer.Reserved1;
				gameObject.name = "MonumentWatcher";
				enabled = false;
			}

            public void InitializeProperties(string monumentID, MonumentCategory category, string prefab, string langKey, string suffix = "", bool isCustom = false)
            {
                ID = monumentID;
                Category = category;
				Prefab = prefab;
                LangKey = langKey;
				Suffix = suffix;
				IsCustom = isCustom;
			}
			
			public void InitializeBounds(Vector3 center, Vector3 size, Quaternion rotation, Transform parent = null)
            {
                if (parent is not null)
                {
					IsMoveable = true;
					transform.parent = parent;
                    transform.localPosition = center;
                    transform.localRotation = rotation;
                }
                else
                {
					IsMoveable = false;
					transform.position = center;
                    transform.rotation = rotation;
                }

                if (boxCollider is not null)
                    DestroyImmediate(boxCollider);
                if (rigidbody is not null)
                    DestroyImmediate(rigidbody);

                rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
                rigidbody.detectCollisions = true;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;

                boxCollider = gameObject.GetComponent<BoxCollider>();
                if (boxCollider is null)
                {
                    boxCollider = gameObject.AddComponent<BoxCollider>();
                    boxCollider.isTrigger = true;
                }
                boxCollider.size = Size = size;
                colliderBounds = boxCollider.bounds;
            }
			
			private void OnTriggerEnter(Collider collider)
            {
				if (Instance is null || collider is null || collider.gameObject is null || collider.gameObject.ToBaseEntity() is not BaseEntity entity || !entity.IsValid()) return;
				
				bool callHook = true;
				string oldMonumentID = string.Empty;
                MonumentWatcher moveableWatcher = null;
				if (entity is BasePlayer player)
				{
					if (player.userID.IsSteamId())
                    {
						if (PlayersList.Add(player))
						{
							if (!_playersInMonuments.TryGetValue(player.userID, out var watchers))
								_playersInMonuments[player.userID] = watchers = new List<MonumentWatcher>();
							if (watchers.Any()) oldMonumentID = watchers[^1].ID;
							watchers.Add(this);
							if (!this.IsMoveable)
							{
								foreach (var watcher in watchers)
                                {
									if (watcher.IsMoveable)
										moveableWatcher = watcher;
                                }
								if (moveableWatcher != null)
								{
									watchers.Remove(moveableWatcher);
                                    watchers.Add(moveableWatcher);
									callHook = false;
                                }
							}
							if (callHook) Interface.CallHook(Hooks_OnPlayerEnteredMonument, ID, player, CategoryString, oldMonumentID);
						}
					}
					else if (NpcsList.Add(player))
					{
                        if (!_npcsInMonuments.TryGetValue(player.net.ID, out var watchers))
							_npcsInMonuments[player.net.ID] = watchers = new List<MonumentWatcher>();
						if (watchers.Any()) oldMonumentID = watchers[^1].ID;
						watchers.Add(this);
						if (!this.IsMoveable)
                        {
                            foreach (var watcher in watchers)
                            {
                                if (watcher.IsMoveable)
                                    moveableWatcher = watcher;
                            }
                            if (moveableWatcher != null)
                            {
                                watchers.Remove(moveableWatcher);
                                watchers.Add(moveableWatcher);
                                callHook = false;
                            }
                        }
						if (callHook) Interface.CallHook(Hooks_OnNpcEnteredMonument, ID, player, CategoryString, oldMonumentID);
					}
				}
                else if (EntitiesList.Add(entity))
				{
                    if (!_entitiesInMonuments.TryGetValue(entity.net.ID, out var watchers))
						_entitiesInMonuments[entity.net.ID] = watchers = new List<MonumentWatcher>();
					if (watchers.Any()) oldMonumentID = watchers[^1].ID;
					watchers.Add(this);
					if (!this.IsMoveable)
                    {
                        foreach (var watcher in watchers)
                        {
                            if (watcher.IsMoveable)
                                moveableWatcher = watcher;
                        }
                        if (moveableWatcher != null)
                        {
                            watchers.Remove(moveableWatcher);
                            watchers.Add(moveableWatcher);
                            callHook = false;
                        }
                    }
					if (callHook) Interface.CallHook(Hooks_OnEntityEnteredMonument, ID, entity, CategoryString, oldMonumentID);
				}
			}
			
			private void OnTriggerExit(Collider collider)
			{
				if (collider is null || collider.gameObject is null || collider.gameObject.ToBaseEntity() is not BaseEntity entity || !entity.IsValid()) return;

				if (entity is BasePlayer player)
				{
					if (player.userID.IsSteamId()) OnPlayerExit(player, Str_Leave);
					else OnNpcExit(player, Str_Leave);
				}
				else
					OnEntityExit(entity, Str_Leave);
			}
			
			public void OnPlayerExit(BasePlayer player, string reason, bool remove = true)
            {
				string newMonumentID = string.Empty;
				if (_playersInMonuments.TryGetValue(player.userID, out var watchers))
                {
					watchers.Remove(this);
					if (!watchers.Any()) _playersInMonuments.Remove(player.userID);
					else if (!reason.Equals(Str_Death, StringComparison.OrdinalIgnoreCase)) newMonumentID = watchers[^1].ID;
				}
				Interface.CallHook(Hooks_OnPlayerExitedMonument, ID, player, CategoryString, reason, newMonumentID);
				if (remove) PlayersList.Remove(player);
			}
			
			public void OnNpcExit(BasePlayer player, string reason, bool remove = true)
            {
				string newMonumentID = string.Empty;
				if (_npcsInMonuments.TryGetValue(player.net.ID, out var watchers))
				{
					watchers.Remove(this);
					if (!watchers.Any()) _npcsInMonuments.Remove(player.net.ID);
					else if (!reason.Equals(Str_Death, StringComparison.OrdinalIgnoreCase)) newMonumentID = watchers[^1].ID;
				}
				Interface.CallHook(Hooks_OnNpcExitedMonument, ID, player, CategoryString, reason, newMonumentID);
                if (remove) NpcsList.Remove(player);
            }
			
			public void OnEntityExit(BaseEntity entity, string reason, bool remove = true)
            {
				string newMonumentID = string.Empty;
				if (_entitiesInMonuments.TryGetValue(entity.net.ID, out var watchers))
                {
					watchers.Remove(this);
					if (!watchers.Any()) _entitiesInMonuments.Remove(entity.net.ID);
					else if (!reason.Equals(Str_Death, StringComparison.OrdinalIgnoreCase)) newMonumentID = watchers[^1].ID;
				}
				Interface.CallHook(Hooks_OnEntityExitedMonument, ID, entity, CategoryString, reason, newMonumentID);
				if (remove) EntitiesList.Remove(entity);
			}
			
			public bool IsInBounds(Vector3 pos) => boxCollider.bounds.Contains(pos);
			
			private void OnDestroy()
            {
				_monumentsList.Remove(ID);
                foreach (var player in PlayersList)
                {
					if (player.IsValid())
						OnPlayerExit(player, Str_ClearList, false);
				}
                foreach (var npcPlayer in NpcsList)
                {
					if (npcPlayer.IsValid())
						OnNpcExit(npcPlayer, Str_ClearList, false);
				}
                foreach (var entity in EntitiesList)
                {
					if (entity.IsValid())
						OnEntityExit(entity, Str_ClearList, false);
				}
                Pool.FreeUnmanaged(ref PlayersList);
                Pool.FreeUnmanaged(ref NpcsList);
                Pool.FreeUnmanaged(ref EntitiesList);
				if (ID.Contains(Str_CargoShip)) Interface.CallHook(Hooks_OnCargoWatcherDeleted, ID);
			}
		}
		#endregion
    }
} 