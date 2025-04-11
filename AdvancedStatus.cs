/*
*  < ----- End-User License Agreement ----->
*  
*  You may not copy, modify, merge, publish, distribute, sublicense, or sell copies of this software without the developer's consent.
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
*  Codefling plugin page: https://codefling.com/plugins/advanced-status
*  Codefling license: https://codefling.com/plugins/advanced-status?tab=downloads_field_4
*  
*  Lone.Design plugin page: https://lone.design/product/advanced-status/
*
*  Copyright © 2023-2025 IIIaKa
*/

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Network;
using Facepunch;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
	[Info("Advanced Status", "IIIaKa", "0.1.18")]
	[Description("A useful API plugin that mimics in-game status bars and allows the addition of custom ones.")]
	class AdvancedStatus : RustPlugin
    {
		//TODO
        //When the ability to change the hook call order is added, remove InitPlayer from the CreateBar method

        //TODO
        //When the in-game HUD is disabled, the bars remain enabled. Need to find a way to track this

        //TODO
        //ToolsHUDUI
        //GameUI.Hud.Vitals.Main - hp bar
        //GameUI.Hud.Vitals - status bars

        [PluginReference]
		private Plugin ImageLibrary;

		#region ~Variables~
		private static AdvancedStatus Instance { get; set; }
		private bool _isReady = false;
		private const string PERMISSION_ADMIN = "advancedstatus.admin", AdvancedStatusName = "AdvancedStatus", DefaultImage = "AdvancedStatus_Default", GetImage = "GetImage", NoteInv = "note.inv", ImagesPath = $@"{AdvancedStatusName}\Images", ImagesPattern = "*.png",
			Hooks_OnLoaded = $"On{AdvancedStatusName}Loaded", Hooks_OnBarDeleted = "OnAdvancedBarDeleted", Hooks_GainedBuilding = "OnPlayerGainedBuildingPrivilege", Hooks_LostBuilding = "OnPlayerLostBuildingPrivilege",
			Placeholders_UserId = "*userId*", Placeholders_UserName = "*userName*";
		private readonly string[] HttpScheme = new string[2] { "http://", "https://" };
		private string ImagePath = string.Empty;
		public Hash<ulong, PlayerWatcher> _playersList = new Hash<ulong, PlayerWatcher>();
		public Dictionary<string, string> _imagesList = new Dictionary<string, string>();
		public string[] _cachedBarsContainer = new string[25];
		
		private readonly HashSet<ItemCategory> _buildingItemsExceptedCategories = new HashSet<ItemCategory> { ItemCategory.Attire, ItemCategory.Medical, ItemCategory.Component };
		private readonly Dictionary<ItemCategory, HashSet<int>> _buildingItems = new Dictionary<ItemCategory, HashSet<int>>()
		{
			{ ItemCategory.Weapon, new HashSet<int>() { 1714509152, -187304968, 1145722690, -759279626, -1290278434 }},
			{ ItemCategory.Resources, new HashSet<int>() { 634478325 }},
			{ ItemCategory.Tool, new HashSet<int>() { 1803831286, 200773292 }},
			{ ItemCategory.Food, new HashSet<int>()
			{
				122783240, 1911552868, 838831151, 803954639, -778875547, 998894949, 1512054436, -2084071424, -1305326964, -1776128552,
				-886280491, -237809779, 1898094925, -1511285251, 2133269020, 830839496, 390728933, -520133715, 1533551194, -992286106
			}},
			{ ItemCategory.Ammunition, new HashSet<int>() { -484006286, -1827561369 }},
			{ ItemCategory.Misc, new HashSet<int>()
			{
				573676040, 1242522330, -1973785141, -695124222, 282103175, 809199956, -1679267738, -489848205, -2058362263, 1358643074,
				-173268125, -173268126, -173268128, -173268129, -173268131, -173268132, 882559853, 1885488976, 2104517339, 699075597
			}}
		};
		
		private readonly Dictionary<ItemCategory, HashSet<int>> _buildingItems2 = new Dictionary<ItemCategory, HashSet<int>>()
        {
			{ ItemCategory.Items, new HashSet<int>() { 613961768, 696029452, -1501434104 }},
			{ ItemCategory.Electrical, new HashSet<int>() { 363163265, -144513264, -566907190, -144417939 }},
			{ ItemCategory.Fun, new HashSet<int>()
            {
                -2124352573, -1379036069, -1530414568, -1049881973, -1961560162, -979951147, 476066818, -912398867, 1523403414, -583379016,
                -20045316, -2040817543, 273172220, 576509618, -2107018088, 1784406797, 204970153, 1094293920
			}}
		};
		
		private readonly HashSet<int> _stackableItems = new HashSet<int>()
		{
			1055319033, 349762871, 915408809, -1023065463, -1234735557, 215754713, 14241751, 588596902, -2097376851, 785728077,
			51984655, -1691396643, -1211166256, -1321651331, 605467368, 1712070256, -1685290200, -1036635990, -727717969, -1800345240,
			-1671551935, -1199897169, -1199897172, -1023374709, 1553078977, 1401987718, -89874794, -493159321, 1072924620, 1330084809,
			926800282, -1802083073, 479143914, 1882709339, 95950017, 1199391518, 1414245522, 1234880403, -1994909036, -1021495308,
			642482233, 2019042823, 73681876, 1744298439, 1324203999, -656349006, -7270019, -379734527, -1553999294, -280223496, -515830359,
			-1306288356, -1486461488, -99886070, 261913429, -454370658, -1538109120, -586342290, -568419968, 1770475779, 1668129151,
			989925924, 1973684065, -1848736516, -1440987069, -751151717, -78533081, -1509851560, 1422530437, 1917703890, -1162759543,
			-1130350864, -682687162, 1536610005, -1709878924, 1272768630, -989755543, 1873897110, -1520560807, 1827479659, 813023040,
			-395377963, -1167031859, 1391703481, -242084766, 621915341, -996920608, 1819863051, -1770889433, -1824770114, 831955134,
			-1433390281, 1036321299, 1223900335, -602717596, -126305173, -888153050, 1364514421, -455286320, 1762167092, 1223729384,
			1572152877, -282193997, 180752235, -1386082991, 70102328, 22947882, 81423963, 1058261682, -151387974, 550753330,
			-384243979, 1771755747, 122783240, 1911552868, 1112162468, 838831151, 803954639, 858486327, -1305326964, -1776128552,
			1272194103, 2133269020, 830839496, 854447607, 1533551194, -992286106, 1660145984, 390728933, -520133715, 1367190888,
			-778875547, 998894949, -886280491, -237809779, -2086926071, 1512054436, -2084071424, -567909622, 1898094925, -1511285251,
			-1018587433, 1776460938, 1719978075, 634478325, -1938052175, -858312878, -321733511, 1568388703, -592016202, -930193596,
			-265876753, -1579932985, -1982036270, 317398316, 1381010055, -946369541, 69511070, -4031221, -1779183908, -804769727,
			-544317637, -277057363, -932201673, -2099697608, -1157596551, -1581843485, 1523195708, -1779180711, -151838493, -1366326648,
			1735402444, -1673693549
		};
		
		private static readonly HashSet<string> _doubleMountsList = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "saddlerear", "saddletest", "bikedriverseat", "bikepassengerseat" };
        private static readonly HashSet<string> _mountsList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
			"motorbikedriverseat", "motorbikepassengerseat", "modularcardriverseat", "modularcarpassengerseatleft", "modularcarpassengerseatright",
			"modularcarpassengerseatlesslegroomleft", "modularcarpassengerseatlesslegroomright", "modularcarpassengerseatsidewayleft", "miniheliseat", "minihelipassenger",
			"transporthelipilot", "transporthelicopilot", "attackhelidriver", "attackheligunner", "submarinesolodriverstanding",
			"submarineduodriverseat", "submarineduopassengerseat", "snowmobiledriverseat", "snowmobilepassengerseat", "snowmobilepassengerseat tomaha",
			"workcartdriver", "locomotivedriver", "craneoperator"
		};
		private static readonly string[] _layersList = new string[] { "Under", "Overall", "Overlay", "Hud", "Hud.Menu" };
		private static readonly string[] _fontsList = new string[] { "RobotoCondensed-Bold.ttf", "RobotoCondensed-Regular.ttf", "PermanentMarker.ttf", "DroidSansMono.ttf" };
		#endregion

        #region ~Configuration~
        private static Configuration _config;

		private class Configuration
        {
			[JsonProperty(PropertyName = "Chat command")]
            public string Command = "bar";
			
			[JsonProperty(PropertyName = "Is it worth enabling console notifications for the successful loading of local images?")]
            public bool Notify_Images = false;
			
			[JsonProperty(PropertyName = "Interval(in seconds) for counting in-game status bars")]
			public float Count_Interval = 0.5f;
			
			[JsonProperty(PropertyName = "Interval(in seconds) for counting Building Privilege status bars. Note: Calculating Building Privilege is significantly more resource-intensive than other counts")]
            public float Count_Interval_Privilege = 1f;
			
			[JsonProperty(PropertyName = "Bar - Display Layer. If you have button bars, it's advisable to use Hud(https://umod.org/guides/rust/basic-concepts-of-gui#layers)")]
			public string Bar_Layer = "Under";
			
			[JsonProperty(PropertyName = "Bar - Left to Right")]
			public bool Bar_LeftToRight = true;

			[JsonProperty(PropertyName = "Bar - Offset between status bars")]
			public int Bar_Offset_Between = 2;

			[JsonProperty(PropertyName = "Bar - Default Height")]
			public int Bar_Height = 26;

			[JsonProperty(PropertyName = "Main - Default Color")]
			public string Bar_Main_Color = "#505F75";

			[JsonProperty(PropertyName = "Main - Default Transparency")]
			public float Bar_Main_Transparency = 0.7f;

			[JsonProperty(PropertyName = "Main - Default Material(empty to disable)")]
			public string Bar_Main_Material = "";
			
			[JsonProperty(PropertyName = "Image - Default Image")]
            public string Bar_Image = "AdvancedBar_Image";
			
			[JsonProperty(PropertyName = "Image - Default Color")]
			public string Bar_Image_Color = "#6B7E95";
			
			[JsonProperty(PropertyName = "Image - Default Transparency")]
			public float Bar_Image_Transparency = 1.0f;
			
			[JsonProperty(PropertyName = "Image - Outline Default Color")]
            public string Bar_Image_Outline_Color = "#000000";
			
			[JsonProperty(PropertyName = "Image - Outline Default Transparency")]
            public float Bar_Image_Outline_Transparency = 1.0f;
			
			[JsonProperty(PropertyName = "Image - Outline Default Distance")]
            public string Bar_Image_Outline_Distance = "0.75 0.75";
			
			[JsonProperty(PropertyName = "Text - Default Size")]
			public int Bar_Text_Size = 12;

			[JsonProperty(PropertyName = "Text - Default Color")]
			public string Bar_Text_Color = "#FFFFFF";
			
			[JsonProperty(PropertyName = "Text - Default Transparency")]
            public float Bar_Text_Transparency = 1f;
			
			[JsonProperty(PropertyName = "Text - Default Font(https://umod.org/guides/rust/basic-concepts-of-gui#fonts)")]
			public string Bar_Text_Font = "RobotoCondensed-Bold.ttf";
			
			[JsonProperty(PropertyName = "Text - Default Offset Horizontal")]
			public int Bar_Text_Offset_Horizontal = 0;
			
			[JsonProperty(PropertyName = "Text - Outline Default Color")]
            public string Bar_Text_Outline_Color = "#000000";

            [JsonProperty(PropertyName = "Text - Outline Default Transparency")]
            public float Bar_Text_Outline_Transparency = 1.0f;

            [JsonProperty(PropertyName = "Text - Outline Default Distance")]
            public string Bar_Text_Outline_Distance = "0.75 0.75";
			
			[JsonProperty(PropertyName = "SubText - Default Size")]
			public int Bar_SubText_Size = 12;

			[JsonProperty(PropertyName = "SubText - Default Color")]
			public string Bar_SubText_Color = "#FFFFFF";
			
			[JsonProperty(PropertyName = "SubText - Default Transparency")]
            public float Bar_SubText_Transparency = 1f;
			
			[JsonProperty(PropertyName = "SubText - Default Font")]
			public string Bar_SubText_Font = "RobotoCondensed-Bold.ttf";
			
			[JsonProperty(PropertyName = "SubText - Outline Default Color")]
            public string Bar_SubText_Outline_Color = "#000000";

            [JsonProperty(PropertyName = "SubText - Outline Default Transparency")]
            public float Bar_SubText_Outline_Transparency = 1.0f;

            [JsonProperty(PropertyName = "SubText - Outline Default Distance")]
            public string Bar_SubText_Outline_Distance = "0.75 0.75";
			
			[JsonProperty(PropertyName = "Progress - Default Color")]
			public string Bar_Progress_Color = "#89B840";

			[JsonProperty(PropertyName = "Progress - Default Transparency")]
			public float Bar_Progress_Transparency = 0.7f;

			[JsonProperty(PropertyName = "Progress - Default OffsetMin")]
			public string Bar_Progress_OffsetMin = "25 2.5";

			[JsonProperty(PropertyName = "Progress - Default OffsetMax")]
			public string Bar_Progress_OffsetMax = "-3.5 -3.5";

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
			
			if (!_layersList.Contains(_config.Bar_Layer))
				_config.Bar_Layer = _layersList[0];
			
			if (!IsStringHexColor(_config.Bar_Main_Color))
				_config.Bar_Main_Color = "#505F75";
			_config.Bar_Main_Transparency = Mathf.Clamp01(_config.Bar_Main_Transparency);
			
			if (!IsStringHexColor(_config.Bar_Image_Color))
                _config.Bar_Image_Color = "#6B7E95";
            _config.Bar_Image_Transparency = Mathf.Clamp01(_config.Bar_Image_Transparency);
			
			if (!IsStringHexColor(_config.Bar_Image_Outline_Color))
                _config.Bar_Image_Outline_Color = "#000000";
            _config.Bar_Image_Outline_Transparency = Mathf.Clamp01(_config.Bar_Image_Outline_Transparency);
			
			_config.Bar_Text_Size = Math.Clamp(_config.Bar_Text_Size, 1, 25);
			if (!IsStringHexColor(_config.Bar_Text_Color))
                _config.Bar_Text_Color = "#FFFFFF";
            _config.Bar_Text_Transparency = Mathf.Clamp01(_config.Bar_Text_Transparency);
			_config.Bar_Text_Font = GetValidFont(_config.Bar_Text_Font);
			
			if (!IsStringHexColor(_config.Bar_Text_Outline_Color))
                _config.Bar_Text_Outline_Color = "#000000";
            _config.Bar_Text_Outline_Transparency = Mathf.Clamp01(_config.Bar_Text_Outline_Transparency);
			
			_config.Bar_SubText_Size = Math.Clamp(_config.Bar_SubText_Size, 1, 25);
            if (!IsStringHexColor(_config.Bar_SubText_Color))
                _config.Bar_SubText_Color = "#FFFFFF";
            _config.Bar_SubText_Transparency = Mathf.Clamp01(_config.Bar_SubText_Transparency);
            _config.Bar_SubText_Font = GetValidFont(_config.Bar_SubText_Font);
			
			if (!IsStringHexColor(_config.Bar_SubText_Outline_Color))
                _config.Bar_SubText_Outline_Color = "#000000";
            _config.Bar_SubText_Outline_Transparency = Mathf.Clamp01(_config.Bar_SubText_Outline_Transparency);
			
			if (!IsStringHexColor(_config.Bar_Progress_Color))
                _config.Bar_Progress_Color = "#89B840";
            _config.Bar_Progress_Transparency = Mathf.Clamp01(_config.Bar_Progress_Transparency);
			
			SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);
        protected override void LoadDefaultConfig() => _config = new Configuration() { Version = Version };
		#endregion

        #region ~Language~
        protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["MsgDays"] = "d",
				["MsgHours"] = "h",
				["MsgMinutes"] = "m",
				["MsgSeconds"] = "s"
			}, this);
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["MsgDays"] = "д",
				["MsgHours"] = "ч",
				["MsgMinutes"] = "м",
				["MsgSeconds"] = "с"
			}, this, "ru");
		}
        #endregion

        #region ~Methods~
		private System.Collections.IEnumerator InitPlugin()
        {
			for (int i = 0; i <= 24; i++)
            {
				int y = 102 + (28 * i);
				_cachedBarsContainer[i] = $@"[{{""name"":""{AdvancedStatusName}"",""parent"":""{_config.Bar_Layer}"",""components"":[{{""type"":""UnityEngine.UI.Image"",""color"":""0 0 0 0""}},
{{""type"":""RectTransform"",""anchormin"":""1 0"",""anchormax"":""1 0"",""offsetmin"":""-208 {y}"",""offsetmax"":""-16 {y + 1500}""}}]}}]";
			}
			yield return ServerMgr.Instance.StartCoroutine(InitImages());
			yield return new WaitForSeconds(1);
			foreach (var player in BasePlayer.activePlayerList)
            {
                if (!player.IsNpc)
                    InitPlayer(player);
            }
			Subscribe(nameof(OnPlayerConnected));
            Subscribe(nameof(OnPlayerDisconnected));
			Subscribe(nameof(OnPlayerSleep));
			Subscribe(nameof(OnPlayerSleepEnded));
			Subscribe(nameof(OnEntityDeath));
            Subscribe(nameof(OnSendCommand));
            Subscribe(nameof(OnEntityMounted));
            Subscribe(nameof(OnEntityDismounted));
            Subscribe(nameof(OnNpcConversationStart));
            Subscribe(nameof(OnNpcConversationEnded));
			_isReady = true;
			yield return new WaitForSeconds(1);
			Interface.CallHook(Hooks_OnLoaded, Version);
		}
		
		private System.Collections.IEnumerator InitImages()
        {
			string[] images = Interface.Oxide.DataFileSystem.GetFiles(ImagesPath, ImagesPattern);
            if (images.Any())
            {
                int lastBackslash, lastDot;
                string name;
                foreach (var path in images)
                {
					if (string.IsNullOrWhiteSpace(path)) continue;
					lastBackslash = path.LastIndexOf('\\');
                    lastDot = path.LastIndexOf('.');
                    name = path.Substring(lastBackslash + 1, lastDot - lastBackslash - 1);
                    yield return ServerMgr.Instance.StartCoroutine(StoreImage(name, path));
                }
            }
			
			if (!_imagesList.ContainsKey(DefaultImage))
			{
				_imagesList[DefaultImage] = string.Empty;
				PrintError($"The image folder is empty, or the default image is missing!\nFor proper functionality, the file ***{Path.DirectorySeparatorChar}data{Path.DirectorySeparatorChar}{ImagesPath}{Path.DirectorySeparatorChar}{DefaultImage}.png must be present! Please reload the plugin after adding it.");
			}
		}
		
		private System.Collections.IEnumerator StoreImages(Dictionary<string, string> images)
        {
			foreach (var kvp in images)
				yield return ServerMgr.Instance.StartCoroutine(StoreImage(kvp.Key, kvp.Value));
		}
		
		private System.Collections.IEnumerator StoreImage(string name, string path)
        {
			using (UnityWebRequest request = UnityWebRequestTexture.GetTexture($"file://{path}"))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    PrintError($"Failed to load the image '{name}' from {path}");
                    yield break;
                }
				Texture2D tex = DownloadHandlerTexture.GetContent(request);
				if (_imagesList.ContainsKey(name))
					FileStorage.server.Remove(uint.Parse(_imagesList[name]), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);
				_imagesList[name] = FileStorage.server.Store(tex.EncodeToPNG(), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
				if (_config.Notify_Images)
					Puts($"The image '{name}' was stored successfully!");
				UnityEngine.Object.DestroyImmediate(tex);
            }
        }
		
		private void InitPlayer(BasePlayer player)
		{
			RemovePlayer(player);
			_playersList[player.userID] = player.gameObject.AddComponent<PlayerWatcher>();
		}
		
		private void RemovePlayer(BasePlayer player)
		{
			var playerWatcher = player.gameObject.GetComponent<PlayerWatcher>();
			if (playerWatcher != null)
				UnityEngine.Object.DestroyImmediate(playerWatcher);
			_playersList.Remove(player.userID);
		}
		
		private bool IsBuildingBlockItem(ItemDefinition itemDef)
        {
			if (itemDef != null && !_buildingItemsExceptedCategories.Contains(itemDef.category))
			{
				if (_buildingItems.TryGetValue(itemDef.category, out var list))
					return list.Contains(itemDef.itemid);
				else if (_buildingItems2.TryGetValue(itemDef.category, out var list2))
					return !list2.Contains(itemDef.itemid);
				return true;
			}
			return false;
		}
		
		public static BuildingPrivlidge GetBuildingPrivilege(BasePlayer player)
        {
			var obb = new OBB(player.transform.position, player.transform.lossyScale, player.transform.rotation, player.bounds);
            BuildingBlock other = null;
			BuildingPrivlidge result = null;
			var obj2 = Pool.Get<List<BuildingBlock>>();
            Vis.Entities(obb.position, 16f + obb.extents.magnitude, obj2, 2097152);
            for (int i = 0; i < obj2.Count; i++)
            {
				var buildingBlock = obj2[i];
				if (buildingBlock.isServer != player.isServer || !buildingBlock.IsOlderThan(other) || obb.Distance(buildingBlock.WorldSpaceBounds()) > 16f)
					continue;
				
				var building = buildingBlock.GetBuilding();
                if (building != null)
                {
                    var dominatingBuildingPrivilege = building.GetDominatingBuildingPrivilege();
                    if (!(dominatingBuildingPrivilege == null))
                    {
                        other = buildingBlock;
                        result = dominatingBuildingPrivilege;
                    }
                }
            }
			Pool.FreeUnmanaged(ref obj2);
            return result;
        }

        public static bool TryGetEntityPrivilege(BasePlayer player, out EntityPrivilege result)
        {
            var obb = new OBB(player.transform.position, player.transform.lossyScale, player.transform.rotation, player.bounds);
            LegacyShelter other = null;
			var shelterList = Pool.Get<List<LegacyShelter>>();
			Vis.Entities(obb.position, 10f, shelterList, 2097152);
			for (int i = 0; i < shelterList.Count; i++)
			{
				var shelter = shelterList[i];
				if (shelter.isServer != player.isServer || !shelter.IsOlderThan(other) || obb.Distance(shelter.WorldSpaceBounds()) > 3f) continue;
				other = shelter;
			}
			Pool.FreeUnmanaged(ref shelterList);
			result = other?.GetEntityBuildingPrivilege();
			return result != null;
		}
		
		public static HashSet<VehiclePrivilege> GetVehiclePrivileges(BasePlayer player)
        {
			var obb = new OBB(player.transform.position, player.transform.lossyScale, player.transform.rotation, player.bounds);
            var result = new HashSet<VehiclePrivilege>();
			var tugList = Pool.Get<List<Tugboat>>();
			Vis.Entities(obb.position, 1f, tugList);
			for (int i = 0; i < tugList.Count; i++)
			{
				var tug = tugList[i];
				if (tug.isServer != player.isServer || tug.children == null || obb.Distance(tug.WorldSpaceBounds()) > 3f) continue;
				foreach (var child in tug.children)
				{
					if (child is VehiclePrivilege privilege)
					{
						result.Add(privilege);
						break;
					}
				}
			}
			Pool.FreeUnmanaged(ref tugList);
			return result;
		}
		
		public static void SortAdvancedBars(List<AdvancedBar> bars)
        {
			bars.Sort((b1, b2) =>
            {
                int orderComparison = b1.Order.CompareTo(b2.Order);
                return (orderComparison != 0) ? orderComparison : b1.Id.CompareTo(b2.Id);
            });
		}
		
		public static bool IsStringHexColor(string hexColor) => !string.IsNullOrWhiteSpace(hexColor) && hexColor[0] == '#' && hexColor.Length == 7;
		
		public static string StringColorFromHex(string hexColor, float transparent)
		{
			if (string.IsNullOrWhiteSpace(hexColor))
				return $"1 1 1 {transparent:F2}";
            if (hexColor[0] != '#' || hexColor.Length < 7)
				return hexColor;
			
			int red = Convert.ToInt32(hexColor.Substring(1, 2), 16);
			int green = Convert.ToInt32(hexColor.Substring(3, 2), 16);
			int blue = Convert.ToInt32(hexColor.Substring(5, 2), 16);

			double redPercentage = (double)red / 255;
			double greenPercentage = (double)green / 255;
			double bluePercentage = (double)blue / 255;

			return $"{redPercentage:F2} {greenPercentage:F2} {bluePercentage:F2} {transparent:F2}";
		}
		
		public static string GetValidFont(string font)
        {
            for (int i = 0; i < _fontsList.Length; i++)
            {
				if (font.Equals(_fontsList[i], StringComparison.OrdinalIgnoreCase))
					return _fontsList[i];
			}
            return _fontsList[0];
		}
		#endregion

        #region ~API~
        private object IsReady() => _isReady ? true : null;
		
		private void CreateBar(object obj, Dictionary<string, object> parameters) => CreateBar($"{obj}", parameters);
		private void CreateBar(string userIDStr, Dictionary<string, object> parameters)
        {
            if (ulong.TryParse(userIDStr, out var userID))
                CreateBar(userID, parameters);
        }
		private void CreateBar(BasePlayer player, Dictionary<string, object> parameters) => CreateBar(player.userID.Get(), parameters);
		private void CreateBar(ulong userID, Dictionary<string, object> parameters)
        {
			if (parameters == null || !userID.IsSteamId() || !parameters.TryGetValue("Plugin", out var pNameObj) || pNameObj is not string pluginName ||
				!parameters.TryGetValue("Id", out var bIdObj) || bIdObj is not string barId) return;
			
			if (!_playersList.TryGetValue(userID, out var watcher))
			{
				if (BasePlayer.TryFindByID(userID, out var player))
				{
					InitPlayer(player);
					watcher = _playersList[userID];
				}
				else
					return;
			}
			
			AdvancedBar bar = null;
			for (int i = 0; i < watcher.Bars.Count; i++)
            {
				var tBar = watcher.Bars[i];
				if (tBar.Plugin == pluginName && tBar.Id == barId)
                {
					bar = tBar;
					break;
                }
            }
			
			bool isOrder = true;
			if (bar != null)
				bar.Update(parameters, out var isProgress, out var isText, out isOrder);
			else
			{
				bar = new AdvancedBar(pluginName, barId, parameters);
				watcher.Bars.Add(bar);
			}
			
			if (isOrder)
				SortAdvancedBars(watcher.Bars);
			bar.ShouldRemove = false;
			if (watcher.CanShowBars)
				DrawBars(watcher, !watcher.IsUiActive ? 0 : watcher.Bars.IndexOf(bar));
		}
		
		private void UpdateContent(object obj, Dictionary<string, object> parameters) => UpdateContent($"{obj}", parameters);
		private void UpdateContent(string userIDStr, Dictionary<string, object> parameters)
        {
            if (ulong.TryParse(userIDStr, out var userID))
                UpdateContent(userID, parameters);
        }
		private void UpdateContent(BasePlayer player, Dictionary<string, object> parameters) => UpdateContent(player.userID.Get(), parameters);
		private void UpdateContent(ulong userID, Dictionary<string, object> parameters)
        {
			if (parameters == null || !parameters.TryGetValue("Plugin", out var pNameObj) || pNameObj is not string pluginName ||
				!parameters.TryGetValue("Id", out var bIdObj) || bIdObj is not string barId || !_playersList.TryGetValue(userID, out var watcher)) return;
			
			AdvancedBar bar;
			bool isOrder = false;
			for (int i = 0; i < watcher.Bars.Count; i++)
			{
				bar = watcher.Bars[i];
				if (bar.Plugin == pluginName && bar.Id == barId)
				{
					bar.Update(parameters, out var isProgress, out var isText, out isOrder);
					if (watcher.IsUiActive)
					{
						if (isProgress)
							DrawProgress(watcher.Player, bar);
						if (isText)
							DrawText(watcher.Player, bar);
					}
					break;
				}
			}
			
			if (isOrder)
				SortAdvancedBars(watcher.Bars);
		}
		
		private void CreateBar(object obj, Dictionary<int, object> parameters) => CreateBar($"{obj}", parameters);
        private void CreateBar(string userIDStr, Dictionary<int, object> parameters)
        {
            if (ulong.TryParse(userIDStr, out var userID))
                CreateBar(userID, parameters);
        }
        private void CreateBar(BasePlayer player, Dictionary<int, object> parameters) => CreateBar(player.userID.Get(), parameters);
        private void CreateBar(ulong userID, Dictionary<int, object> parameters)
        {
            if (parameters == null || !userID.IsSteamId() || !parameters.TryGetValue(1, out var pNameObj) || pNameObj is not string pluginName ||
                !parameters.TryGetValue(0, out var bIdObj) || bIdObj is not string barId) return;

            if (!_playersList.TryGetValue(userID, out var watcher))
            {
                if (BasePlayer.TryFindByID(userID, out var player))
                {
                    InitPlayer(player);
                    watcher = _playersList[userID];
                }
                else
                    return;
            }
			
			AdvancedBar bar = null;
			for (int i = 0; i < watcher.Bars.Count; i++)
            {
                var tBar = watcher.Bars[i];
                if (tBar.Plugin == pluginName && tBar.Id == barId)
                {
                    bar = tBar;
                    break;
                }
            }
			
			bool isOrder = true;
			if (bar != null)
                bar.Update(parameters, out var isProgress, out var isText, out isOrder);
            else
            {
				bar = new AdvancedBar(pluginName, barId, parameters);
				watcher.Bars.Add(bar);
			}
			
			if (isOrder)
				SortAdvancedBars(watcher.Bars);
			bar.ShouldRemove = false;
			if (watcher.CanShowBars)
				DrawBars(watcher, !watcher.IsUiActive ? 0 : watcher.Bars.IndexOf(bar));
		}
		
		private void UpdateContent(object obj, Dictionary<int, object> parameters) => UpdateContent($"{obj}", parameters);
        private void UpdateContent(string userIDStr, Dictionary<int, object> parameters)
        {
            if (ulong.TryParse(userIDStr, out var userID))
                UpdateContent(userID, parameters);
        }
        private void UpdateContent(BasePlayer player, Dictionary<int, object> parameters) => UpdateContent(player.userID.Get(), parameters);
        private void UpdateContent(ulong userID, Dictionary<int, object> parameters)
        {
            if (parameters == null || !parameters.TryGetValue(1, out var pNameObj) || pNameObj is not string pluginName ||
                !parameters.TryGetValue(0, out var bIdObj) || bIdObj is not string barId || !_playersList.TryGetValue(userID, out var watcher)) return;
			
			AdvancedBar bar;
			bool isOrder = false;
			for (int i = 0; i < watcher.Bars.Count; i++)
            {
				bar = watcher.Bars[i];
                if (bar.Plugin == pluginName && bar.Id == barId)
                {
                    bar.Update(parameters, out var isProgress, out var isText, out isOrder);
                    if (watcher.IsUiActive)
                    {
                        if (isProgress)
                            DrawProgress(watcher.Player, bar);
                        if (isText)
                            DrawText(watcher.Player, bar);
                    }
					break;
				}
            }
			
			if (isOrder)
				SortAdvancedBars(watcher.Bars);
		}
		
		private void DeleteBar(object obj, string barId, string pluginName) => DeleteBar($"{obj}", barId, pluginName);
		private void DeleteBar(string userIDStr, string barId, string pluginName)
        {
            if (ulong.TryParse(userIDStr, out var userID))
                DeleteBar(userID, barId, pluginName);
        }
		private void DeleteBar(BasePlayer player, string barId, string pluginName) => DeleteBar(player.userID.Get(), barId, pluginName);
		private void DeleteBar(ulong userID, string barId, string pluginName)
		{
			if (!_playersList.TryGetValue(userID, out var watcher)) return;
			AdvancedBar bar;
			for (int i = 0; i < watcher.Bars.Count; i++)
			{
				bar = watcher.Bars[i];
				if (bar.Plugin == pluginName && bar.Id == barId)
				{
					bar.ShouldRemove = true;
					break;
				}
			}
		}
		
		private void DeleteBarForAll(string barId, string pluginName)
        {
			AdvancedBar bar;
			foreach (var watcher in _playersList.Values)
			{
				for (int i = 0; i < watcher.Bars.Count; i++)
                {
					bar = watcher.Bars[i];
                    if (bar.Plugin == pluginName && bar.Id == barId)
                    {
                        bar.ShouldRemove = true;
                        break;
                    }
                }
			}
		}
		
		private void DeleteCategory(object obj, string category, string pluginName) => DeleteCategory($"{obj}", category, pluginName);
		private void DeleteCategory(string userIDStr, string category, string pluginName)
        {
            if (ulong.TryParse(userIDStr, out var userID))
                DeleteCategory(userID, category, pluginName);
        }
		private void DeleteCategory(BasePlayer player, string category, string pluginName) => DeleteCategory(player.userID.Get(), category, pluginName);
        private void DeleteCategory(ulong userID, string category, string pluginName)
        {
            if (!_playersList.TryGetValue(userID, out var watcher)) return;
			AdvancedBar bar;
			for (int i = 0; i < watcher.Bars.Count; i++)
			{
				bar = watcher.Bars[i];
				if (bar.Plugin == pluginName && bar.Category == category)
					bar.ShouldRemove = true;
			}
        }
		
		private void DeleteCategoryForAll(string category, string pluginName)
        {
			AdvancedBar bar;
			foreach (var watcher in _playersList.Values)
			{
				for (int i = 0; i < watcher.Bars.Count; i++)
                {
					bar = watcher.Bars[i];
                    if (bar.Plugin == pluginName && bar.Category == category)
                        bar.ShouldRemove = true;
                }
			}
		}
		
		private void DeleteAllBars(object obj, string pluginName) => DeleteAllBars($"{obj}", pluginName);
		private void DeleteAllBars(string userIDStr, string pluginName)
        {
            if (ulong.TryParse(userIDStr, out var userID))
                DeleteAllBars(userID, pluginName);
        }
		private void DeleteAllBars(BasePlayer player, string pluginName) => DeleteAllBars(player.userID.Get(), pluginName);
        private void DeleteAllBars(ulong userID, string pluginName)
        {
            if (!_playersList.TryGetValue(userID, out var watcher)) return;
			AdvancedBar bar;
			for (int i = 0; i < watcher.Bars.Count; i++)
			{
				bar = watcher.Bars[i];
				if (bar.Plugin == pluginName)
                    bar.ShouldRemove = true;
            }
        }
		
		private void DeleteAllPluginBars(string pluginName)
        {
			AdvancedBar bar;
			foreach (var watcher in _playersList.Values)
            {
				for (int i = 0; i < watcher.Bars.Count; i++)
                {
                    bar = watcher.Bars[i];
                    if (bar.Plugin == pluginName)
                        bar.ShouldRemove = true;
                }
			}
        }
		
		private int GetTotalClientBars(object obj) => GetTotalClientBars($"{obj}");
		private int GetTotalClientBars(string userIDStr) => ulong.TryParse(userIDStr, out var userID) ? GetTotalClientBars(userID) : 0;
		private int GetTotalClientBars(BasePlayer player) => GetTotalClientBars(player.userID.Get());
        private int GetTotalClientBars(ulong userID) => _playersList.TryGetValue(userID, out var watcher) ? Math.Max(watcher.ClientBars, 0) : 0;
		
		private int GetTotalPlayerBars(object obj) => GetTotalPlayerBars($"{obj}");
		private int GetTotalPlayerBars(string userIDStr) => ulong.TryParse(userIDStr, out var userID) ? GetTotalPlayerBars(userID) : 0;
		private int GetTotalPlayerBars(BasePlayer player) => GetTotalPlayerBars(player.userID.Get());
        private int GetTotalPlayerBars(ulong userID)
        {
			int result = 0;
			if (_playersList.TryGetValue(userID, out var watcher))
            {
				for (int i = 0; i < watcher.Bars.Count; i++)
				{
					if (!watcher.Bars[i].ShouldRemove)
						result++;
				}
            }
			return result;
		}
		
		private void LoadImages(List<string> list, bool force = false)
		{
			Dictionary<string, string> images;
			if (force)
				images = list.Where(i => !string.IsNullOrWhiteSpace(i)).ToDictionary(i => i, i => string.Format(ImagePath, i));
			else
            {
				string path;
				images = new Dictionary<string, string>();
				for (int i = 0; i < list.Count; i++)
				{
					var name = list[i];
					if (!string.IsNullOrWhiteSpace(name) && !_imagesList.ContainsKey(name))
					{
						path = string.Format(ImagePath, name);
						if (!_imagesList.ContainsKey(name) && File.Exists(path))
							images[name] = string.Format(ImagePath, name);
					}
				}
            }
			ServerMgr.Instance.StartCoroutine(StoreImages(images));
		}
		
		private void LoadImage(string name, bool force = false)
        {
			if (string.IsNullOrWhiteSpace(name) || (!force && _imagesList.ContainsKey(name))) return;
			string path = string.Format(ImagePath, name);
            if (File.Exists(path))
                ServerMgr.Instance.StartCoroutine(StoreImage(name, path));
        }
		
		private void CopyImage(string source, string newImage, bool force = false)
        {
			if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(newImage)) return;
			string sourcePath = string.Format(ImagePath, source), newPath = string.Format(ImagePath, newImage);
			if (File.Exists(sourcePath) && (force || !File.Exists(newPath)))
            {
				try
				{
					File.Copy(sourcePath, newPath, overwrite: true);
					ServerMgr.Instance.StartCoroutine(StoreImage(newImage, newPath));
                }
                catch {}
			}
        }
		
		private void DeleteImages(List<string> list, bool deleteFile = true)
        {
			for (int i = 0; i < list.Count; i++)
				DeleteImage(list[i], deleteFile);
		}
		
		private void DeleteImage(string name, bool deleteFile = true)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            if (_imagesList.TryGetValue(name, out var crcStr))
            {
                uint crc = uint.Parse(crcStr);
                FileStorage.server.Remove(crc, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);
                _imagesList.Remove(name);
            }
            if (deleteFile)
            {
                try { File.Delete(string.Format(ImagePath, name)); }
                catch {}
            }
        }
		
		private bool BarExists(object obj, string barId, string pluginName) => BarExists($"{obj}", barId, pluginName);
		private bool BarExists(string userIDStr, string barId, string pluginName) => ulong.TryParse(userIDStr, out var userID) ? BarExists(userID, barId, pluginName) : false;
		private bool BarExists(BasePlayer player, string barId, string pluginName) => BarExists(player.userID.Get(), barId, pluginName);
		private bool BarExists(ulong userID, string barId, string pluginName)
		{
			if (_playersList.TryGetValue(userID, out var watcher))
            {
				AdvancedBar bar;
				for (int i = 0; i < watcher.Bars.Count; i++)
				{
					bar = watcher.Bars[i];
					if (bar.Plugin == pluginName && bar.Id == barId)
						return true;
				}
			}
			return false;
		}
		
		private bool InBuildingPrivilege(object obj) => InBuildingPrivilege($"{obj}");
		private bool InBuildingPrivilege(string userIDStr) => ulong.TryParse(userIDStr, out var userID) ? InBuildingPrivilege(userID) : false;
		private bool InBuildingPrivilege(BasePlayer player) => InBuildingPrivilege(player.userID.Get());
		private bool InBuildingPrivilege(ulong userID) => _playersList.TryGetValue(userID, out var watcher) ? watcher.InOwnBuilding : false;
		#endregion

		#region ~Oxide Hooks~
		void OnPlayerConnected(BasePlayer player)
		{
			if (!_playersList.ContainsKey(player.userID))
				InitPlayer(player);
		}
		
		void OnPlayerDisconnected(BasePlayer player, string reason) => RemovePlayer(player);
		
		void OnPlayerSleep(BasePlayer player)
        {
			if (_playersList.TryGetValue(player.userID, out var watcher))
				watcher.OnDeathOrSleeping();
		}
		
		void OnPlayerSleepEnded(BasePlayer player)
		{
			if (_playersList.TryGetValue(player.userID, out var watcher))
            {
				watcher.IsDeadOrSleeping = false;
				watcher.StartCount();
			}
		}
		
		void OnEntityDeath(BasePlayer player)
		{
			if (_playersList.TryGetValue(player.userID, out var watcher))
				watcher.OnDeathOrSleeping();
		}
		
		void OnSendCommand(Connection connection, string command, object[] args)
		{
			if (connection == null || !command.StartsWith(NoteInv, StringComparison.OrdinalIgnoreCase) || !_playersList.TryGetValue(connection.userid, out var watcher)) return;
			int itemId = 0;
            if (!(command == NoteInv && int.TryParse(args[0].ToString(), out itemId)))
            {
                string[] parts = command.Split(" ");
                if (parts.Length > 1 && int.TryParse(parts[1], out itemId)) {}
            }
			var realtimeSinceStartup = Time.realtimeSinceStartup;
			if (!_stackableItems.Contains(itemId))
                watcher.AddedItems[$"{itemId}_{realtimeSinceStartup}"] = realtimeSinceStartup + 3.7f;
            else
                watcher.AddedItems[itemId.ToString()] = realtimeSinceStartup + 3.7f;
        }
		
		void OnEntityMounted(BaseMountable mount, BasePlayer player)
        {
			if (_playersList.TryGetValue(player.userID, out var watcher))
				watcher.OnMount(mount);
		}
		
		void OnEntityDismounted(BaseMountable mount, BasePlayer player)
        {
			if (!player.isMounted && _playersList.TryGetValue(player.userID, out var watcher))
				watcher.MountCount = 0;
		}
		
		void OnEntityMounted(ComputerStation computerStation, BasePlayer player)
        {
            if (_playersList.TryGetValue(player.userID, out var watcher))
                watcher.OnVanillaUi();
        }
		
		void OnEntityDismounted(ComputerStation computerStation, BasePlayer player)
        {
            if (_playersList.TryGetValue(player.userID, out var watcher))
                watcher.OnVanillaUi(false);
        }
		
		void OnNpcConversationStart(NPCTalking npcTalking, BasePlayer player, ConversationData conversationData)
		{
			NextTick(() =>
			{
                //npcTalking.conversingPlayers.Contains(player) - Not the best solution, as the conversation UI will display the status bar UI.
                if (npcTalking.conversingPlayers.Contains(player) && _playersList.TryGetValue(player.userID, out var watcher))
					watcher.OnVanillaUi();
			});
		}
		
		void OnNpcConversationEnded(NPCTalking npcTalking, BasePlayer player)
		{
			if (_playersList.TryGetValue(player.userID, out var watcher))
				watcher.OnVanillaUi(false);
		}
		
		void OnPluginUnloaded(Plugin plugin) => DeleteAllPluginBars(plugin.Name);
		
		void Init()
		{
			Instance = this;
			Unsubscribe(nameof(OnPlayerConnected));
			Unsubscribe(nameof(OnPlayerDisconnected));
			Unsubscribe(nameof(OnPlayerSleep));
			Unsubscribe(nameof(OnPlayerSleepEnded));
			Unsubscribe(nameof(OnEntityDeath));
			Unsubscribe(nameof(OnSendCommand));
			Unsubscribe(nameof(OnEntityMounted));
			Unsubscribe(nameof(OnEntityDismounted));
			Unsubscribe(nameof(OnNpcConversationStart));
			Unsubscribe(nameof(OnNpcConversationEnded));
			permission.RegisterPermission(PERMISSION_ADMIN, this);
			AddCovalenceCommand(_config.Command, nameof(AdvancedStatus_Command));
		}

		void OnServerInitialized(bool initial)
		{
			if (initial)
			{
				Interface.Oxide.ReloadPlugin(Name);
				return;
			}
			string folderPath = $"{Interface.Oxide.DataDirectory}{Path.DirectorySeparatorChar}{AdvancedStatusName}{Path.DirectorySeparatorChar}Images";
			ImagePath = $"{folderPath}{Path.DirectorySeparatorChar}{{0}}.png";
			if (!Directory.Exists(folderPath))
				Directory.CreateDirectory(folderPath);
			ServerMgr.Instance.StartCoroutine(InitPlugin());
		}
        #endregion

        #region ~Commands~
        private void AdvancedStatus_Command(IPlayer player, string command, string[] args)
        {
			if (args == null || args.Length < 3 || (!player.IsServer && !permission.UserHasPermission(player.Id, PERMISSION_ADMIN))) return;
			if (args[0] == "images")
			{
				if (args[1] == "reload")
                {
					if (args[2] == "all")
						ServerMgr.Instance.StartCoroutine(InitImages());
					else
						LoadImage(args[2], true);
				}
			}
		}
        #endregion
		
		#region ~UI~
        private void DrawBars(PlayerWatcher watcher, int startIndex = 0)
		{
			if (startIndex <= 0)
            {
				startIndex = Math.Max(startIndex, 0);
				CuiHelper.DestroyUi(watcher.Player, AdvancedStatusName);
				CuiHelper.AddUi(watcher.Player, _cachedBarsContainer[Math.Clamp(watcher.ClientBars, 0, 24)]);
				watcher.IsUiActive = true;
            }
			int barOffset = 0;
			for (int i = 0; i < watcher.Bars.Count; i++)
			{
				var bar = watcher.Bars[i];
				if (i < startIndex)
                {
					barOffset += bar.Height + _config.Bar_Offset_Between;
					continue;
				}
				
				string barImage;
                if (!string.IsNullOrWhiteSpace(bar.Image_Sprite))
					barImage = $@"{{""type"":""UnityEngine.UI.Image"",""color"":""{bar.Image_Color}"",""sprite"":""{bar.Image_Sprite}""}}";
				else if (!string.IsNullOrWhiteSpace(bar.Image_Local))
                    barImage = $@"{{""type"":""UnityEngine.UI.{(bar.Is_RawImage ? "RawImage" : "Image")}"",""color"":""{bar.Image_Color}"",""png"":""{_imagesList[_imagesList.ContainsKey(bar.Image_Local) ? bar.Image_Local : DefaultImage]}""}}";
                else if (ImageLibrary == null || !ImageLibrary.IsLoaded || bar.Image.StartsWithAny(HttpScheme))
                    barImage = $@"{{""type"":""UnityEngine.UI.RawImage"",""url"":""{bar.Image}""}}";
                else
                    barImage = $@"{{""type"":""UnityEngine.UI.{(bar.Is_RawImage ? "RawImage" : "Image")}"",""color"":""{bar.Image_Color}"",""png"":""{(string)(ImageLibrary?.Call(GetImage, bar.Image) ?? _imagesList[DefaultImage])}""}}";
				
				CuiHelper.DestroyUi(watcher.Player, bar.BarID);
				CuiHelper.AddUi(watcher.Player,
					$@"[{{""name"":""{bar.BarID}"",""parent"":""{AdvancedStatusName}"",""components"":[{{""type"":""UnityEngine.UI.Image"",""color"":""{bar.Main_Color}""{(!string.IsNullOrWhiteSpace(bar.Main_Material) ? $@",""material"":""{bar.Main_Material}""" : string.Empty)}}},{{""type"":""RectTransform"",""anchormin"":""0 0"",""anchormax"":""1 0"",""offsetmin"":""0 {barOffset}"",""offsetmax"":""0 {barOffset + bar.Height}""}}]}},
{{""name"":""{bar.UiNames[2]}"",""parent"":""{bar.BarID}"",""components"":[{{""type"":""UnityEngine.UI.Image"",""color"":""0 0 0 0""}},{{""type"":""RectTransform"",""anchormin"":""0 0"",""anchormax"":""1 1"",""offsetmin"":""0 0"",""offsetmax"":""0 0""}}]}},
{{""name"":""{bar.UiNames[0]}"",""parent"":""{bar.BarID}"",""components"":[{{""type"":""UnityEngine.UI.Image"",""color"":""0 0 0 0""}},{{""type"":""RectTransform"",""anchormin"":""0 0"",""anchormax"":""1 1"",""offsetmin"":""0 0"",""offsetmax"":""0 0""}}]}},
{{""name"":""{bar.UiNames[4]}"",""parent"":""{bar.UiNames[2]}"",""components"":[{{""type"":""UnityEngine.UI.Image"",""color"":""0 0 0 0""}},{{""type"":""RectTransform"",""anchormin"":""0 0"",""anchormax"":""1 1"",""offsetmin"":""0 0"",""offsetmax"":""0 0""}}]}},
{{""name"":""{bar.UiNames[2]}_Image"",""parent"":""{bar.UiNames[2]}"",""components"":[{barImage},{(bar._image_Outline ? $@"{{""type"":""UnityEngine.UI.Outline"",""color"":""{bar.Image_Outline_Color}"",""distance"":""{bar.Image_Outline_Distance}""}}," : string.Empty)}{{""type"":""RectTransform"",""anchormin"":""{(_config.Bar_LeftToRight ? 0 : 1)} 0.5"",""anchormax"":""{(_config.Bar_LeftToRight ? 0 : 1)} 0.5"",""offsetmin"":""{(_config.Bar_LeftToRight ? 4 : -20)} -8"",""offsetmax"":""{(_config.Bar_LeftToRight ? 20 : -4)} 8""}}]}}]");
				
				barOffset += bar.Height + _config.Bar_Offset_Between;
				DrawProgress(watcher.Player, bar);
				DrawText(watcher.Player, bar);
				DrawButton(watcher.Player, bar);
			}
		}
		
		public static void DrawProgress(BasePlayer player, AdvancedBar bar)
        {
			string name = bar.UiNames[5];
			CuiHelper.DestroyUi(player, name);
			if (bar.Progress > 0f)
			{
				CuiHelper.AddUi(player,
					$@"[{{""name"":""{name}"",""parent"":""{bar.UiNames[4]}"",""components"":[{{""type"":""UnityEngine.UI.Image"",""color"":""0 0 0 0""}},{{""type"":""RectTransform"",""anchormin"":""0 0"",""anchormax"":""1 1"",""offsetmin"":""{bar.Progress_OffsetMin}"",""offsetmax"":""{bar.Progress_OffsetMax}""}}]}},
{{""name"":""{bar.UiNames[6]}"",""parent"":""{name}"",""components"":[{{""type"":""UnityEngine.UI.Image"",""color"":""{bar.Progress_Color}""}},{{""type"":""RectTransform"",""anchormin"":""{(_config.Bar_LeftToRight ? 0 : 1 - bar.Progress)} 0"",""anchormax"":""{(_config.Bar_LeftToRight ? bar.Progress : 1)} 1"",""offsetmin"":""0 0"",""offsetmax"":""0 0""}}]}}]");
			}
		}
		
		public void DrawText(BasePlayer player, AdvancedBar bar)
		{
			string SubText = bar.SubText;
			bool subNotEmpty;
			if (bar.BarType == AdvancedBarType.TimeCounter || bar.BarType == AdvancedBarType.TimeProgressCounter)
			{
				subNotEmpty = true;
				double timeLeft = bar.TimeStamp - Network.TimeEx.currentTimestamp;
				TimeSpan timeSpan = TimeSpan.FromSeconds(timeLeft);
				int days = timeSpan.Days;
				int hours = timeSpan.Hours;
				int minutes = timeSpan.Minutes;
				int seconds = timeSpan.Seconds;
				string[] stringTime = new string[4] {
					days > 0 ? $"{days}{lang.GetMessage("MsgDays", this, player.UserIDString)}" : string.Empty,
					hours > 0 || days > 0 ? $"{hours}{lang.GetMessage("MsgHours", this, player.UserIDString)}" : string.Empty,
					days > 0 || hours > 0 || minutes > 0 ? $"{minutes}{lang.GetMessage("MsgMinutes", this, player.UserIDString)}" : string.Empty,
					days < 1 && hours < 1 ? $"{seconds}{lang.GetMessage("MsgSeconds", this, player.UserIDString)}" : string.Empty };
				SubText = string.Join(" ", stringTime.Where(s => !string.IsNullOrEmpty(s)));
			}
            else
				subNotEmpty = !string.IsNullOrWhiteSpace(SubText);
			
			float width = 1f;
			if (subNotEmpty)
            {
				float subWidth = bar.SubText_Size * (bar.SubText_Size > 15 ? 0.68f : 0.75f) * SubText.Length / 243f;
				if (subWidth > 0.5f)
				{
					width = Mathf.Clamp((bar.Text_Size * (bar.Text_Size > 15 ? 0.68f : 0.75f) * bar.Text.Length / 243f), 0.3f, 0.5f);
					float num = 1f - width;
					if (num < subWidth)
					{
						int num2 = (int)Mathf.Round(num * 243f / (bar.SubText_Size * (bar.SubText_Size > 15 ? 0.68f : 0.75f))) - 3;
						if (num2 > 0 && num2 < SubText.Length)
							SubText = SubText.Substring(0, num2) + "...";
						else if (num2 < SubText.Length)
							SubText = "...";
					}
				}
				else
					width -= subWidth;
			}
			
			CuiHelper.DestroyUi(player, bar.UiNames[3]);
            CuiHelper.AddUi(player,
				$@"[{{""name"":""{bar.UiNames[3]}"",""parent"":""{bar.UiNames[2]}"",""components"":[{{""type"":""UnityEngine.UI.Image"",""color"":""0 0 0 0""}},{{""type"":""RectTransform"",""anchormin"":""0 0"",""anchormax"":""1 1"",""offsetmin"":""{(_config.Bar_LeftToRight ? 25 : 5)} 2"",""offsetmax"":""{(_config.Bar_LeftToRight ? -5 : -25)} -2""}}]}},
{{""name"":""{bar.UiNames[3]}_Text"",""parent"":""{bar.UiNames[3]}"",""components"":[{{""type"":""UnityEngine.UI.Text"",""text"":""{bar.Text}"",""fontSize"":{bar.Text_Size},""font"":""{bar.Text_Font}"",""align"":""{(_config.Bar_LeftToRight ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight)}"",""color"":""{bar.Text_Color}""}}{(bar._text_Outline ? $@",{{""type"":""UnityEngine.UI.Outline"",""color"":""{bar.Text_Outline_Color}"",""distance"":""{bar.Text_Outline_Distance}""}}" : string.Empty)},{{""type"":""RectTransform"",""anchormin"":""0 0"",""anchormax"":""{width} 1"",""offsetmin"":""{(_config.Bar_LeftToRight ? bar.Text_Offset_Horizontal : 0)} 0"",""offsetmax"":""{(_config.Bar_LeftToRight ? 0 : -bar.Text_Offset_Horizontal)} 0""}}]}}
{(subNotEmpty ? $@",{{""name"":""{bar.UiNames[3]}_SubText"",""parent"":""{bar.UiNames[3]}"",""components"":[{{""type"":""UnityEngine.UI.Text"",""text"":""{SubText}"",""fontSize"":{bar.SubText_Size},""font"":""{bar.SubText_Font}"",""align"":""{(_config.Bar_LeftToRight ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft)}"",""color"":""{bar.SubText_Color}""}}{(bar._subText_Outline ? $@",{{""type"":""UnityEngine.UI.Outline"",""color"":""{bar.SubText_Outline_Color}"",""distance"":""{bar.SubText_Outline_Distance}""}}" : string.Empty)},{{""type"":""RectTransform"",""anchormin"":""{width} 0"",""anchormax"":""1 1"",""offsetmin"":""0 0"",""offsetmax"":""0 0""}}]}}" : string.Empty)}]");
		}
		
		public static void DrawButton(BasePlayer player, AdvancedBar bar)
        {
			string name = bar.UiNames[1];
			CuiHelper.DestroyUi(player, name);
			if (!string.IsNullOrWhiteSpace(bar.Command))
				CuiHelper.AddUi(player, $@"[{{""name"":""{name}"",""parent"":""{bar.UiNames[0]}"",""components"":[{{""type"":""UnityEngine.UI.Button"",""command"":""{bar.Command}"",""color"":""0 0 0 0""}},{{""type"":""RectTransform"",""anchormin"":""0 0"",""anchormax"":""1 1"",""offsetmin"":""0 0"",""offsetmax"":""0 0""}}]}}]");
		}
		#endregion

        #region ~Unload~
        void Unload()
		{
			foreach (var player in BasePlayer.activePlayerList)
            {
				RemovePlayer(player);
				CuiHelper.DestroyUi(player, AdvancedStatusName);
			}
			foreach (var crcStr in _imagesList.Values)
            {
				if (uint.TryParse(crcStr, out var crc))
					FileStorage.server.Remove(crc, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);
			}
			_imagesList.Clear();
			Instance = null;
			_config = null;
		}
		#endregion

		#region ~AdvancedBar~
		public enum AdvancedBarType
		{
			Default,
			Timed,
			TimeCounter,
			TimeProgress,
            TimeProgressCounter
		}
		
		public class AdvancedBar
		{
			public string Id { get; }
			public string Plugin { get; }
			
			public AdvancedBarType BarType { get; private set; }
			public string Category { get; private set; }
			public int Order { get; private set; }
			public int Height { get; private set; }
			public string Main_Color { get; private set; }
			public string Main_Material { get; private set; }
			public string Image { get; private set; }
			public string Image_Local { get; private set; }
			public string Image_Sprite { get; private set; }
			public bool Is_RawImage { get; private set; }
			public string Image_Color { get; private set; }
			public bool _image_Outline { get; private set; }
			public string Image_Outline_Color { get; private set; }
			public string Image_Outline_Distance { get; private set; }
			public string Text { get; private set; }
			public int Text_Size { get; private set; }
			public string Text_Color { get; private set; }
			public string Text_Font { get; private set; }
			public int Text_Offset_Horizontal { get; private set; }
			public bool _text_Outline { get; private set; }
			public string Text_Outline_Color { get; private set; }
			public string Text_Outline_Distance { get; private set; }
			public string SubText { get; private set; }
			public int SubText_Size { get; private set; }
			public string SubText_Color { get; private set; }
			public string SubText_Font { get; private set; }
			public bool _subText_Outline { get; private set; }
            public string SubText_Outline_Color { get; private set; }
            public string SubText_Outline_Distance { get; private set; }
			public double TimeStampStart { get; private set; }
			public double TimeStamp { get; private set; }
			public double TimeStampDestroy { get; private set; }
			private float _progress;
			public float Progress
			{
				get { return _progress; }
				set
				{
					_progress = Mathf.Clamp01(value);
				}
			}
			public bool Progress_Reverse { get; private set; }
			public string Progress_Color { get; private set; }
			public string Progress_OffsetMin { get; private set; }
			public string Progress_OffsetMax { get; private set; }
			public string Command { get; private set; }
			public List<string> PlayerCommands { get; private set; } = null;
			public List<string> ConsoleCommands { get; private set; } = null;
			
			public string BarID { get; }
			public bool ShouldRemove { get; set; }
			public string[] UiNames { get; }
			
			public AdvancedBar() {}
			public AdvancedBar(string pluginName, string id, Dictionary<string, object> parameters)
            {
				object obj;
				Id = id;
				Plugin = pluginName;
				BarType = (parameters.TryGetValue("BarType", out obj) && obj is string barTypeString && Enum.TryParse(barTypeString, true, out AdvancedBarType barType)) ? barType : AdvancedBarType.Default;
				Category = parameters.TryGetValue("Category", out obj) && obj is string category ? category : "Default";
				Order = parameters.TryGetValue("Order", out obj) && obj is int order ? order : 10;
				Height = parameters.TryGetValue("Height", out obj) && obj is int height ? height : _config.Bar_Height;
				Main_Color = StringColorFromHex(parameters.TryGetValue("Main_Color", out obj) && obj is string mainColor ? mainColor : _config.Bar_Main_Color,
					parameters.TryGetValue("Main_Transparency", out obj) && obj is float mainTransp ? mainTransp : _config.Bar_Main_Transparency);
				Main_Material = parameters.TryGetValue("Main_Material", out obj) && obj is string mainMaterial ? mainMaterial : _config.Bar_Main_Material;
				Image = parameters.TryGetValue("Image", out obj) && obj is string image ? image : _config.Bar_Image;
				Image_Local = parameters.TryGetValue("Image_Local", out obj) && obj is string imageLocal ? imageLocal : string.Empty;
                Image_Sprite = parameters.TryGetValue("Image_Sprite", out obj) && obj is string imageSprite ? imageSprite : string.Empty;
                Is_RawImage = parameters.TryGetValue("Is_RawImage", out obj) && obj is bool isRawImage ? isRawImage : false;
				Image_Color = StringColorFromHex(parameters.TryGetValue("Image_Color", out obj) && obj is string imageColor ? imageColor : _config.Bar_Image_Color,
					parameters.TryGetValue("Image_Transparency", out obj) && obj is float imageTransp ? imageTransp : _config.Bar_Image_Transparency);
				if (parameters.TryGetValue("Image_Outline_Color", out obj) && obj is string imageOutlineColor)
					_image_Outline = true;
				else
					imageOutlineColor = _config.Bar_Image_Outline_Color;
				Image_Outline_Color = StringColorFromHex(imageOutlineColor,
					parameters.TryGetValue("Image_Outline_Transparency", out obj) && obj is float imageOutlineTransp ? imageOutlineTransp : _config.Bar_Image_Outline_Transparency);
				if (parameters.TryGetValue("Image_Outline_Distance", out obj) && obj is string imageOutlineDistance)
                {
                    Image_Outline_Distance = imageOutlineDistance;
                    _image_Outline = true;
                }
                else
                    Image_Outline_Distance = _config.Bar_Image_Outline_Distance;
				Text = parameters.TryGetValue("Text", out obj) && obj is string text ? text : string.Empty;
                Text_Size = parameters.TryGetValue("Text_Size", out obj) && obj is int textSize ? textSize : _config.Bar_Text_Size;
				Text_Color = StringColorFromHex(parameters.TryGetValue("Text_Color", out obj) && obj is string textColor ? textColor : _config.Bar_Text_Color,
					parameters.TryGetValue("Text_Transparency", out obj) && obj is float textTransp ? textTransp : _config.Bar_Text_Transparency);
				Text_Font = parameters.TryGetValue("Text_Font", out obj) && obj is string textFont ? textFont : _config.Bar_Text_Font;
                Text_Offset_Horizontal = parameters.TryGetValue("Text_Offset_Horizontal", out obj) && obj is int textOffsetHorizontal ? textOffsetHorizontal : _config.Bar_Text_Offset_Horizontal;
				if (parameters.TryGetValue("Text_Outline_Color", out obj) && obj is string textOutlineColor)
                    _text_Outline = true;
                else
                    textOutlineColor = _config.Bar_Text_Outline_Color;
				Text_Outline_Color = StringColorFromHex(textOutlineColor,
					parameters.TryGetValue("Text_Outline_Transparency", out obj) && obj is float textOutlineTransp ? textOutlineTransp : _config.Bar_Text_Outline_Transparency);
				if (parameters.TryGetValue("Text_Outline_Distance", out obj) && obj is string textOutlineDistance)
                {
                    Text_Outline_Distance = textOutlineDistance;
                    _text_Outline = true;
                }
                else
                    Text_Outline_Distance = _config.Bar_Text_Outline_Distance;
				SubText = parameters.TryGetValue("SubText", out obj) && obj is string subText ? subText : string.Empty;
				SubText_Size = parameters.TryGetValue("SubText_Size", out obj) && obj is int subTextSize ? subTextSize : _config.Bar_SubText_Size;
				SubText_Color = StringColorFromHex(parameters.TryGetValue("SubText_Color", out obj) && obj is string subTextColor ? subTextColor : _config.Bar_SubText_Color,
                    parameters.TryGetValue("SubText_Transparency", out obj) && obj is float subTextTransp ? subTextTransp : _config.Bar_SubText_Transparency);
				SubText_Font = parameters.TryGetValue("SubText_Font", out obj) && obj is string subTextFont ? subTextFont : _config.Bar_SubText_Font;
				if (parameters.TryGetValue("SubText_Outline_Color", out obj) && obj is string subTextOutlineColor)
                    _subText_Outline = true;
                else
                    subTextOutlineColor = _config.Bar_Text_Outline_Color;
                SubText_Outline_Color = StringColorFromHex(subTextOutlineColor,
                    parameters.TryGetValue("SubText_Outline_Transparency", out obj) && obj is float subTextOutlineTransp ? subTextOutlineTransp : _config.Bar_SubText_Outline_Transparency);
                if (parameters.TryGetValue("SubText_Outline_Distance", out obj) && obj is string subTextOutlineDistance)
                {
                    SubText_Outline_Distance = subTextOutlineDistance;
                    _subText_Outline = true;
                }
                else
                    SubText_Outline_Distance = _config.Bar_SubText_Outline_Distance;
				if (parameters.TryGetValue("TimeStampStart", out obj) && obj is double timeStampStart) TimeStampStart = timeStampStart;
				if (parameters.TryGetValue("TimeStamp", out obj) && obj is double timeStamp) TimeStamp = timeStamp;
				if (parameters.TryGetValue("TimeStampDestroy", out obj) && obj is double timeStampDestroy) TimeStampDestroy = timeStampDestroy;
				if (parameters.TryGetValue("Progress", out obj) && obj is float progress) Progress = progress;
				Progress_Reverse = parameters.TryGetValue("Progress_Reverse", out obj) && obj is bool progressReverse ? progressReverse : false;
				Progress_Color = StringColorFromHex(parameters.TryGetValue("Progress_Color", out obj) && obj is string progressColor ? progressColor : _config.Bar_Progress_Color,
					parameters.TryGetValue("Progress_Transparency", out obj) && obj is float progressTransp ? progressTransp : _config.Bar_Progress_Transparency);
				Progress_OffsetMin = parameters.TryGetValue("Progress_OffsetMin", out obj) && obj is string progressOffsetMin ? progressOffsetMin : _config.Bar_Progress_OffsetMin;
                Progress_OffsetMax = parameters.TryGetValue("Progress_OffsetMax", out obj) && obj is string progressOffsetMax ? progressOffsetMax : _config.Bar_Progress_OffsetMax;
                Command = parameters.TryGetValue("Command", out obj) && obj is string command ? command : string.Empty;
				if (parameters.TryGetValue("PlayerCommands", out obj) && obj is IEnumerable<string> playerCommands) PlayerCommands = new List<string>(playerCommands);
				if (parameters.TryGetValue("ConsoleCommands", out obj) && obj is IEnumerable<string> consoleCommands) ConsoleCommands = new List<string>(consoleCommands);
				
				BarID = $"{AdvancedStatusName}_{Plugin}_{Id}";
				UiNames = new string[7];
				UiNames[0] = $"{BarID}_ButtonLayer";
                UiNames[1] = $"{UiNames[0]}_Button";
                UiNames[2] = $"{BarID}_MainLayer";
                UiNames[3] = $"{UiNames[2]}_Content";
                UiNames[4] = $"{UiNames[2]}_ProgressBar";
                UiNames[5] = $"{UiNames[4]}_Offset";
                UiNames[6] = $"{UiNames[4]}_Fill";
			}
			
			public AdvancedBar(string pluginName, string id, Dictionary<int, object> parameters)
            {
                object obj;
				Id = id;
				Plugin = pluginName;
				BarType = (parameters.TryGetValue(2, out obj) && obj is string barTypeString && Enum.TryParse(barTypeString, true, out AdvancedBarType barType)) ? barType : AdvancedBarType.Default;
				Category = parameters.TryGetValue(3, out obj) && obj is string category ? category : "Default";
                Order = parameters.TryGetValue(4, out obj) && obj is int order ? order : 10;
                Height = parameters.TryGetValue(5, out obj) && obj is int height ? height : _config.Bar_Height;
                Main_Color = StringColorFromHex(parameters.TryGetValue(6, out obj) && obj is string mainColor ? mainColor : _config.Bar_Main_Color,
                    parameters.TryGetValue(-6, out obj) && obj is float mainTransp ? mainTransp : _config.Bar_Main_Transparency);
                Main_Material = parameters.TryGetValue(7, out obj) && obj is string mainMaterial ? mainMaterial : _config.Bar_Main_Material;
                Image = parameters.TryGetValue(8, out obj) && obj is string image ? image : _config.Bar_Image;
                Image_Local = parameters.TryGetValue(9, out obj) && obj is string imageLocal ? imageLocal : string.Empty;
                Image_Sprite = parameters.TryGetValue(10, out obj) && obj is string imageSprite ? imageSprite : string.Empty;
                Is_RawImage = parameters.TryGetValue(11, out obj) && obj is bool isRawImage ? isRawImage : false;
                Image_Color = StringColorFromHex(parameters.TryGetValue(12, out obj) && obj is string imageColor ? imageColor : _config.Bar_Image_Color,
                    parameters.TryGetValue(-12, out obj) && obj is float imageTransp ? imageTransp : _config.Bar_Image_Transparency);
				if (parameters.TryGetValue(13, out obj) && obj is string imageOutlineColor)
                    _image_Outline = true;
                else
                    imageOutlineColor = _config.Bar_Image_Outline_Color;
                Image_Outline_Color = StringColorFromHex(imageOutlineColor,
                    parameters.TryGetValue(-13, out obj) && obj is float imageOutlineTransp ? imageOutlineTransp : _config.Bar_Image_Outline_Transparency);
				if (parameters.TryGetValue(14, out obj) && obj is string imageOutlineDistance)
                {
                    Image_Outline_Distance = imageOutlineDistance;
                    _image_Outline = true;
                }
                else
                    Image_Outline_Distance = _config.Bar_Image_Outline_Distance;
				Text = parameters.TryGetValue(15, out obj) && obj is string text ? text : string.Empty;
                Text_Size = parameters.TryGetValue(16, out obj) && obj is int textSize ? textSize : _config.Bar_Text_Size;
                Text_Color = StringColorFromHex(parameters.TryGetValue(17, out obj) && obj is string textColor ? textColor : _config.Bar_Text_Color,
                    parameters.TryGetValue(-17, out obj) && obj is float textTransp ? textTransp : _config.Bar_Text_Transparency);
                Text_Font = parameters.TryGetValue(18, out obj) && obj is string textFont ? textFont : _config.Bar_Text_Font;
                Text_Offset_Horizontal = parameters.TryGetValue(19, out obj) && obj is int textOffsetHorizontal ? textOffsetHorizontal : _config.Bar_Text_Offset_Horizontal;
				if (parameters.TryGetValue(20, out obj) && obj is string textOutlineColor)
                    _text_Outline = true;
                else
                    textOutlineColor = _config.Bar_Text_Outline_Color;
                Text_Outline_Color = StringColorFromHex(textOutlineColor,
                    parameters.TryGetValue(-20, out obj) && obj is float textOutlineTransp ? textOutlineTransp : _config.Bar_Text_Outline_Transparency);
                if (parameters.TryGetValue(21, out obj) && obj is string textOutlineDistance)
                {
                    Text_Outline_Distance = textOutlineDistance;
                    _text_Outline = true;
                }
                else
                    Text_Outline_Distance = _config.Bar_Text_Outline_Distance;
				SubText = parameters.TryGetValue(22, out obj) && obj is string subText ? subText : string.Empty;
                SubText_Size = parameters.TryGetValue(23, out obj) && obj is int subTextSize ? subTextSize : _config.Bar_SubText_Size;
                SubText_Color = StringColorFromHex(parameters.TryGetValue(24, out obj) && obj is string subTextColor ? subTextColor : _config.Bar_SubText_Color,
                    parameters.TryGetValue(-24, out obj) && obj is float subTextTransp ? subTextTransp : _config.Bar_SubText_Transparency);
                SubText_Font = parameters.TryGetValue(25, out obj) && obj is string subTextFont ? subTextFont : _config.Bar_SubText_Font;
				if (parameters.TryGetValue(26, out obj) && obj is string subTextOutlineColor)
                    _subText_Outline = true;
                else
                    subTextOutlineColor = _config.Bar_SubText_Outline_Color;
				SubText_Outline_Color = StringColorFromHex(subTextOutlineColor,
                    parameters.TryGetValue(-26, out obj) && obj is float subTextOutlineTransp ? subTextOutlineTransp : _config.Bar_SubText_Outline_Transparency);
                if (parameters.TryGetValue(27, out obj) && obj is string subTextOutlineDistance)
                {
                    SubText_Outline_Distance = subTextOutlineDistance;
                    _subText_Outline = true;
                }
                else
                    SubText_Outline_Distance = _config.Bar_SubText_Outline_Distance;
				if (parameters.TryGetValue(28, out obj) && obj is double timeStampStart) TimeStampStart = timeStampStart;
                if (parameters.TryGetValue(29, out obj) && obj is double timeStamp) TimeStamp = timeStamp;
                if (parameters.TryGetValue(30, out obj) && obj is double timeStampDestroy) TimeStampDestroy = timeStampDestroy;
                if (parameters.TryGetValue(31, out obj) && obj is float progress) Progress = progress;
                Progress_Reverse = parameters.TryGetValue(32, out obj) && obj is bool progressReverse ? progressReverse : false;
				Progress_Color = StringColorFromHex(parameters.TryGetValue(33, out obj) && obj is string progressColor ? progressColor : _config.Bar_Progress_Color,
                    parameters.TryGetValue(-33, out obj) && obj is float progressTransp ? progressTransp : _config.Bar_Progress_Transparency);
				Progress_OffsetMin = parameters.TryGetValue(34, out obj) && obj is string progressOffsetMin ? progressOffsetMin : _config.Bar_Progress_OffsetMin;
                Progress_OffsetMax = parameters.TryGetValue(35, out obj) && obj is string progressOffsetMax ? progressOffsetMax : _config.Bar_Progress_OffsetMax;
                Command = parameters.TryGetValue(36, out obj) && obj is string command ? command : string.Empty;
				if (parameters.TryGetValue(37, out obj) && obj is IEnumerable<string> playerCommands) PlayerCommands = new List<string>(playerCommands);
				if (parameters.TryGetValue(38, out obj) && obj is IEnumerable<string> consoleCommands) ConsoleCommands = new List<string>(consoleCommands);
				
				BarID = $"{AdvancedStatusName}_{Plugin}_{Id}";
                UiNames = new string[7];
                UiNames[0] = $"{BarID}_ButtonLayer";
                UiNames[1] = $"{UiNames[0]}_Button";
                UiNames[2] = $"{BarID}_MainLayer";
                UiNames[3] = $"{UiNames[2]}_Content";
                UiNames[4] = $"{UiNames[2]}_ProgressBar";
                UiNames[5] = $"{UiNames[4]}_Offset";
                UiNames[6] = $"{UiNames[4]}_Fill";
            }
			
			public void Update(Dictionary<string, object> parameters, out bool isProgress, out bool isText, out bool isOrder)
            {
                object obj;
                isProgress = false;
                isText = false;
				isOrder = false;
				if (parameters.TryGetValue("BarType", out obj) && obj is string barTypeString && Enum.TryParse(barTypeString, true, out AdvancedBarType barType))
                {
                    if ((BarType == AdvancedBarType.TimeProgress || BarType == AdvancedBarType.TimeProgressCounter) && barType != AdvancedBarType.TimeProgress && barType != AdvancedBarType.TimeProgressCounter)
                    {
                        Progress = 0f;
                        isProgress = true;
                    }
                    BarType = barType;
                }
                if (parameters.TryGetValue("Category", out obj) && obj is string category) Category = category;
                if (parameters.TryGetValue("Order", out obj) && obj is int order)
				{
					Order = order;
					isOrder = true;
				}
				if (parameters.TryGetValue("Height", out obj) && obj is int height) Height = height;
                if (parameters.TryGetValue("Main_Color", out obj) && obj is string mainColor)
                    Main_Color = StringColorFromHex(mainColor, parameters.TryGetValue("Main_Transparency", out obj) && obj is float mainTransp ? mainTransp : _config.Bar_Main_Transparency);
                if (parameters.TryGetValue("Main_Material", out obj) && obj is string mainMaterial) Main_Material = mainMaterial;
                if (parameters.TryGetValue("Image", out obj) && obj is string image) Image = image;
                if (parameters.TryGetValue("Image_Local", out obj) && obj is string imageLocal) Image_Local = imageLocal;
                if (parameters.TryGetValue("Image_Sprite", out obj) && obj is string imageSprite) Image_Sprite = imageSprite;
                if (parameters.TryGetValue("Is_RawImage", out obj) && obj is bool isRawImage) Is_RawImage = isRawImage;
                if (parameters.TryGetValue("Image_Color", out obj) && obj is string imageColor)
                    Image_Color = StringColorFromHex(imageColor, parameters.TryGetValue("Image_Transparency", out obj) && obj is float imageTransp ? imageTransp : _config.Bar_Image_Transparency);
				if (parameters.TryGetValue("Image_Outline_Color", out obj) && obj is string imageOutlineColor)
				{
					Image_Outline_Color = StringColorFromHex(imageOutlineColor,
						parameters.TryGetValue("Image_Outline_Transparency", out obj) && obj is float imageOutlineTransp ? imageOutlineTransp : _config.Bar_Image_Outline_Transparency);
					_image_Outline = true;
				}
				if (parameters.TryGetValue("Image_Outline_Distance", out obj) && obj is string imageOutlineDistance)
                {
                    Image_Outline_Distance = imageOutlineDistance;
                    _image_Outline = true;
                }
				if (parameters.TryGetValue("Text", out obj) && obj is string text && Text != text)
                {
                    Text = text;
                    isText = true;
                }
                if (parameters.TryGetValue("Text_Size", out obj) && obj is int textSize) Text_Size = textSize;
                if (parameters.TryGetValue("Text_Color", out obj) && obj is string textColor)
                    Text_Color = StringColorFromHex(textColor, parameters.TryGetValue("Text_Transparency", out obj) && obj is float textTransp ? textTransp : _config.Bar_Text_Transparency);
                if (parameters.TryGetValue("Text_Font", out obj) && obj is string textFont) Text_Font = textFont;
                if (parameters.TryGetValue("Text_Offset_Horizontal", out obj) && obj is int textOffsetHorizontal) Text_Offset_Horizontal = textOffsetHorizontal;
				if (parameters.TryGetValue("Text_Outline_Color", out obj) && obj is string textOutlineColor)
                {
					Text_Outline_Color = StringColorFromHex(textOutlineColor,
						parameters.TryGetValue("Text_Outline_Transparency", out obj) && obj is float textOutlineTransp ? textOutlineTransp : _config.Bar_Text_Outline_Transparency);
					_text_Outline = true;
				}
				if (parameters.TryGetValue("Text_Outline_Distance", out obj) && obj is string textOutlineDistance)
                {
                    Text_Outline_Distance = textOutlineDistance;
                    _text_Outline = true;
                }
				if (parameters.TryGetValue("SubText", out obj) && obj is string subText && SubText != subText)
                {
                    SubText = subText;
                    isText = true;
                }
                if (parameters.TryGetValue("SubText_Size", out obj) && obj is int subTextSize) SubText_Size = subTextSize;
                if (parameters.TryGetValue("SubText_Color", out obj) && obj is string subTextColor)
                    SubText_Color = StringColorFromHex(subTextColor, parameters.TryGetValue("SubText_Transparency", out obj) && obj is float subTextTransp ? subTextTransp : _config.Bar_SubText_Transparency);
                if (parameters.TryGetValue("SubText_Font", out obj) && obj is string subTextFont) SubText_Font = subTextFont;
				if (parameters.TryGetValue("SubText_Outline_Color", out obj) && obj is string subTextOutlineColor)
				{
					SubText_Outline_Color = StringColorFromHex(subTextOutlineColor,
						parameters.TryGetValue("SubText_Outline_Transparency", out obj) && obj is float subTextOutlineTransp ? subTextOutlineTransp : _config.Bar_SubText_Outline_Transparency);
					_subText_Outline = true;
				}
				if (parameters.TryGetValue("SubText_Outline_Distance", out obj) && obj is string subTextOutlineDistance)
                {
                    SubText_Outline_Distance = subTextOutlineDistance;
                    _subText_Outline = true;
                }
				if (parameters.TryGetValue("TimeStampStart", out obj) && obj is double timeStampStart) TimeStampStart = timeStampStart;
                if (parameters.TryGetValue("TimeStamp", out obj) && obj is double timeStamp) TimeStamp = timeStamp;
                if (parameters.TryGetValue("TimeStampDestroy", out obj) && obj is double timeStampDestroy) TimeStampDestroy = timeStampDestroy;
                if (parameters.TryGetValue("Progress", out obj) && obj is float progress && Progress != progress)
                {
                    Progress = progress;
                    isProgress = true;
                }
                if (parameters.TryGetValue("Progress_Reverse", out obj) && obj is bool progressReverse) Progress_Reverse = progressReverse;
				if (parameters.TryGetValue("Progress_Color", out obj) && obj is string progressColor)
                    Progress_Color = StringColorFromHex(progressColor, parameters.TryGetValue("Progress_Transparency", out obj) && obj is float progressTransp ? progressTransp : _config.Bar_Progress_Transparency);
				if (parameters.TryGetValue("Progress_OffsetMin", out obj) && obj is string progressOffsetMin) Progress_OffsetMin = progressOffsetMin;
                if (parameters.TryGetValue("Progress_OffsetMax", out obj) && obj is string progressOffsetMax) Progress_OffsetMax = progressOffsetMax;
                if (parameters.TryGetValue("Command", out obj) && obj is string command) Command = command;
				if (parameters.TryGetValue("PlayerCommands", out obj) && obj is IEnumerable<string> playerCommands) PlayerCommands = new List<string>(playerCommands);
				if (parameters.TryGetValue("ConsoleCommands", out obj) && obj is IEnumerable<string> consoleCommands) ConsoleCommands = new List<string>(consoleCommands);
			}
			
			public void Update(Dictionary<int, object> parameters, out bool isProgress, out bool isText, out bool isOrder)
            {
				object obj;
				isProgress = false;
				isText = false;
				isOrder = false;
				if (parameters.TryGetValue(2, out obj) && obj is string barTypeString && Enum.TryParse(barTypeString, true, out AdvancedBarType barType))
				{
					if ((BarType == AdvancedBarType.TimeProgress || BarType == AdvancedBarType.TimeProgressCounter) && barType != AdvancedBarType.TimeProgress && barType != AdvancedBarType.TimeProgressCounter)
                    {
						Progress = 0f;
						isProgress = true;
					}
					BarType = barType;
				}
				if (parameters.TryGetValue(3, out obj) && obj is string category) Category = category;
				if (parameters.TryGetValue(4, out obj) && obj is int order)
				{
					Order = order;
					isOrder = true;
				}
				if (parameters.TryGetValue(5, out obj) && obj is int height) Height = height;
				if (parameters.TryGetValue(6, out obj) && obj is string mainColor)
					Main_Color = StringColorFromHex(mainColor, parameters.TryGetValue(-6, out obj) && obj is float mainTransp ? mainTransp : _config.Bar_Main_Transparency);
				if (parameters.TryGetValue(7, out obj) && obj is string mainMaterial) Main_Material = mainMaterial;
				if (parameters.TryGetValue(8, out obj) && obj is string image) Image = image;
				if (parameters.TryGetValue(9, out obj) && obj is string imageLocal) Image_Local = imageLocal;
				if (parameters.TryGetValue(10, out obj) && obj is string imageSprite) Image_Sprite = imageSprite;
				if (parameters.TryGetValue(11, out obj) && obj is bool isRawImage) Is_RawImage = isRawImage;
				if (parameters.TryGetValue(12, out obj) && obj is string imageColor)
					Image_Color = StringColorFromHex(imageColor, parameters.TryGetValue(-12, out obj) && obj is float imageTransp ? imageTransp : _config.Bar_Image_Transparency);
				if (parameters.TryGetValue(13, out obj) && obj is string imageOutlineColor)
                {
                    Image_Outline_Color = StringColorFromHex(imageOutlineColor,
                        parameters.TryGetValue(-13, out obj) && obj is float imageOutlineTransp ? imageOutlineTransp : _config.Bar_Image_Outline_Transparency);
                    _image_Outline = true;
                }
				if (parameters.TryGetValue(14, out obj) && obj is string imageOutlineDistance)
                {
                    Image_Outline_Distance = imageOutlineDistance;
                    _image_Outline = true;
                }
				if (parameters.TryGetValue(15, out obj) && obj is string text && Text != text)
                {
					Text = text;
					isText = true;
				}
				if (parameters.TryGetValue(16, out obj) && obj is int textSize) Text_Size = textSize;
				if (parameters.TryGetValue(17, out obj) && obj is string textColor)
					Text_Color = StringColorFromHex(textColor, parameters.TryGetValue(-17, out obj) && obj is float textTransp ? textTransp : _config.Bar_Text_Transparency);
				if (parameters.TryGetValue(18, out obj) && obj is string textFont) Text_Font = textFont;
				if (parameters.TryGetValue(19, out obj) && obj is int textOffsetHorizontal) Text_Offset_Horizontal = textOffsetHorizontal;
				if (parameters.TryGetValue(20, out obj) && obj is string textOutlineColor)
                {
                    Text_Outline_Color = StringColorFromHex(textOutlineColor,
                        parameters.TryGetValue(-20, out obj) && obj is float textOutlineTransp ? textOutlineTransp : _config.Bar_Text_Outline_Transparency);
                    _text_Outline = true;
                }
				if (parameters.TryGetValue(21, out obj) && obj is string textOutlineDistance)
                {
                    Text_Outline_Distance = textOutlineDistance;
                    _text_Outline = true;
                }
				if (parameters.TryGetValue(22, out obj) && obj is string subText && SubText != subText)
				{
					SubText = subText;
					isText = true;
				}
				if (parameters.TryGetValue(23, out obj) && obj is int subTextSize) SubText_Size = subTextSize;
				if (parameters.TryGetValue(24, out obj) && obj is string subTextColor)
                    SubText_Color = StringColorFromHex(subTextColor, parameters.TryGetValue(-24, out obj) && obj is float subTextTransp ? subTextTransp : _config.Bar_SubText_Transparency);
				if (parameters.TryGetValue(25, out obj) && obj is string subTextFont) SubText_Font = subTextFont;
				if (parameters.TryGetValue(26, out obj) && obj is string subTextOutlineColor)
                {
                    SubText_Outline_Color = StringColorFromHex(subTextOutlineColor,
                        parameters.TryGetValue(-26, out obj) && obj is float subTextOutlineTransp ? subTextOutlineTransp : _config.Bar_SubText_Outline_Transparency);
                    _subText_Outline = true;
                }
                if (parameters.TryGetValue(27, out obj) && obj is string subTextOutlineDistance)
                {
                    SubText_Outline_Distance = subTextOutlineDistance;
                    _subText_Outline = true;
                }
				if (parameters.TryGetValue(28, out obj) && obj is double timeStampStart) TimeStampStart = timeStampStart;
				if (parameters.TryGetValue(29, out obj) && obj is double timeStamp) TimeStamp = timeStamp;
				if (parameters.TryGetValue(30, out obj) && obj is double timeStampDestroy) TimeStampDestroy = timeStampDestroy;
				if (parameters.TryGetValue(31, out obj) && obj is float progress && Progress != progress)
				{
					Progress = progress;
					isProgress = true;
                }
				if (parameters.TryGetValue(32, out obj) && obj is bool progressReverse) Progress_Reverse = progressReverse;
				if (parameters.TryGetValue(33, out obj) && obj is string progressColor)
					Progress_Color = StringColorFromHex(progressColor, parameters.TryGetValue(-33, out obj) && obj is float progressTransp ? progressTransp : _config.Bar_Progress_Transparency);
				if (parameters.TryGetValue(34, out obj) && obj is string progressOffsetMin) Progress_OffsetMin = progressOffsetMin;
				if (parameters.TryGetValue(35, out obj) && obj is string progressOffsetMax) Progress_OffsetMax = progressOffsetMax;
				if (parameters.TryGetValue(36, out obj) && obj is string command) Command = command;
				if (parameters.TryGetValue(37, out obj) && obj is IEnumerable<string> playerCommands) PlayerCommands = new List<string>(playerCommands);
                if (parameters.TryGetValue(38, out obj) && obj is IEnumerable<string> consoleCommands) ConsoleCommands = new List<string>(consoleCommands);
			}
		}

        #endregion

        #region ~PlayerWatcher~
        public class PlayerWatcher : MonoBehaviour
		{
			public BasePlayer Player { get; private set; }
			public List<AdvancedBar> Bars { get; set; }
			public Dictionary<string, float> AddedItems { get; set; }
			
			private int _clientBars = -1;
			public int ClientBars
			{
				get { return _clientBars; }
				private set
				{
					if (_clientBars != value)
					{
						_clientBars = value;
						Instance.DrawBars(this);
					}
				}
			}
			
			public bool IsDeadOrSleeping { get; set; }
			public bool InVanillaUi { get; set; }
			public bool CanShowBars => !IsDeadOrSleeping && !InVanillaUi;
			public int MountCount { get; set; }
			
			private bool _inOwnBuilding;
			public bool InOwnBuilding
			{
				get { return _inOwnBuilding; }
				private set
				{
					if (_inOwnBuilding != value)
					{
						_inOwnBuilding = value;
						Interface.CallHook(_inOwnBuilding ? Hooks_GainedBuilding : Hooks_LostBuilding, Player);
					}
				}
			}
			
			private float _nextPrivilegeTime { get; set; }
			private int PrivilegeCount { get; set; }

			public bool IsUiActive { get; set; }
			
			private void Awake()
			{
				var player = GetComponentInParent<BasePlayer>();
				if (player != null)
				{
					Player = player;
					Bars = new List<AdvancedBar>();
					AddedItems = new Dictionary<string, float>();
					if (Player.isMounted)
						OnMount(Player.GetMounted());
					IsDeadOrSleeping = Player.IsDead() || Player.IsSleeping();
					if (CanShowBars)
						StartCount();
					InvokeRepeating("UpdateTimedBars", 1f, 1f);
				}
				else
					Destroy(this);
			}
			
			private void CountClientBars()
			{
				if (Player == null)
				{
					Destroy(this);
					return;
                }
				
				int result = 0;
				if (Player.metabolism.oxygen.value < 1) result++;
                if (Player.metabolism.temperature.value < 5 || Player.metabolism.temperature.value > 40) result++;
                if (Player.metabolism.wetness.value >= 0.011) result++;
                if (Player.metabolism.comfort.value > 0) result++;
                if (Player.metabolism.hydration.value <= 40) result++;
                if (Player.metabolism.calories.value < 40) result++;
                if (Player.metabolism.radiation_poison.value > 0) result++;
				if (Player.metabolism.bleeding.value != 0) result++;
				if (Player.currentCraftLevel > 0) result++;
                if (Player.inventory.crafting.queue.Count > 0) result++;
                if (Player.modifiers.ActiveModifierCount > 0) result++;
				if (Player.PetEntity != null) result++;
				if (Player.HasPlayerFlag(BasePlayer.PlayerFlags.SafeZone)) result++;
				result += MountCount;
				
				float realtimeSinceStartup = Time.realtimeSinceStartup;
				if (realtimeSinceStartup > _nextPrivilegeTime)
					CountPrivilege();
				result += PrivilegeCount;
				var remList = Pool.Get<List<string>>();
                foreach (var kvp in AddedItems)
                {
                    if (kvp.Value < realtimeSinceStartup)
                        remList.Add(kvp.Key);
                    else
                        result++;
                }
                foreach (var remItem in remList)
                    AddedItems.Remove(remItem);
                Pool.FreeUnmanaged(ref remList);
				ClientBars = result;
			}
			
			private void CountPrivilege()
            {
				int counter = 0;
				var buildingPrivilege = GetBuildingPrivilege(Player);
				bool isRightItem = Instance.IsBuildingBlockItem(Player.GetActiveItem()?.info);
				if (buildingPrivilege != null)
                {
                    if (!buildingPrivilege.IsAuthed(Player))
                    {
                        InOwnBuilding = false;
						if (isRightItem) counter++;
					}
                    else
                    {
                        counter += 2;
                        InOwnBuilding = true;
                    }
                }
                else
                    InOwnBuilding = false;
				var vehiclePrivileges = GetVehiclePrivileges(Player);
				foreach (var vehPriv in vehiclePrivileges)
                {
					bool isVehAuthed = vehPriv.IsAuthed(Player);
                    if (buildingPrivilege != null && buildingPrivilege.IsAuthed(Player) && !isVehAuthed) counter = 0;
                    if (isVehAuthed || isRightItem) counter++;
                }

                if (TryGetEntityPrivilege(Player, out var entPriv))
                {
					bool isEntAuthed = entPriv.IsAuthed(Player);
					//if (buildingPrivilege != null && buildingPrivilege.IsAuthed(player) && !entPriv.IsAuthed(player)) counter = 0;
                    if (buildingPrivilege != null)
                    {
						if (buildingPrivilege.IsAuthed(Player) && !isEntAuthed) counter = 0;
                        else if (!isEntAuthed && isRightItem && !buildingPrivilege.IsAuthed(Player)) counter--;
                    }
                    if (isEntAuthed || isRightItem) counter++;
                }
				PrivilegeCount = Math.Clamp(counter, 0, 2);
				_nextPrivilegeTime = Time.realtimeSinceStartup + _config.Count_Interval_Privilege;
			}
			
			public void UpdateTimedBars()
			{
				int index = int.MaxValue;
				var currentTimestamp = Network.TimeEx.currentTimestamp;
				var remList = Pool.Get<List<AdvancedBar>>();
				for (int i = 0; i < Bars.Count; i++)
				{
					var bar = Bars[i];
					if (bar == null) continue;
					if (bar.ShouldRemove)
					{
						remList.Add(bar);
						if (i < index)
							index = i;
						continue;
					}
					if (bar.BarType == AdvancedBarType.Default) continue;
					if (bar.TimeStamp <= currentTimestamp || (bar.TimeStampDestroy > 0d && bar.TimeStampDestroy <= currentTimestamp))
					{
						remList.Add(bar);
						if (i < index)
							index = i;
					}
					else if (i < index && IsUiActive)
                    {
						if (bar.BarType == AdvancedBarType.TimeCounter)
                            Instance.DrawText(Player, bar);
                        else if (bar.BarType == AdvancedBarType.TimeProgress || bar.BarType == AdvancedBarType.TimeProgressCounter)
                        {
                            float progress = (float)((currentTimestamp - bar.TimeStampStart) / (bar.TimeStamp - bar.TimeStampStart));
                            bar.Progress = bar.Progress_Reverse ? 1f - progress : progress;
                            DrawProgress(Player, bar);
                            Instance.DrawText(Player, bar);
                        }
					}
				}
				foreach (var remBar in remList)
                {
					CuiHelper.DestroyUi(Player, remBar.BarID);
					if (remBar.PlayerCommands != null)
                    {
                        for (int i = 0; i < remBar.PlayerCommands.Count; i++)
                            Player.Command(remBar.PlayerCommands[i].Replace(Placeholders_UserId, Player.UserIDString).Replace(Placeholders_UserName, Player.displayName));
                        remBar.PlayerCommands.Clear();
                    }
					if (remBar.ConsoleCommands != null)
                    {
                        for (int i = 0; i < remBar.ConsoleCommands.Count; i++)
                            Instance.rust.RunServerCommand(remBar.ConsoleCommands[i].Replace(Placeholders_UserId, Player.UserIDString).Replace(Placeholders_UserName, Player.displayName));
						remBar.ConsoleCommands.Clear();
					}
					Interface.CallHook(Hooks_OnBarDeleted, Player, remBar.Plugin, remBar.Id);
					Bars.Remove(remBar);
				}
				Pool.FreeUnmanaged(ref remList);
				if (IsUiActive && index != int.MaxValue && index <= Bars.Count)
					Instance.DrawBars(this, index);
			}
			
			public void StartCount() => Player.InvokeRepeating(CountClientBars, 0f, _config.Count_Interval);
			
			public void OnMount(BaseMountable mount)
            {
                if (mount == null || !mount.isMobile) return;
                if (_doubleMountsList.Contains(mount.ShortPrefabName))
                    MountCount = 2;
                else if (_mountsList.Contains(mount.ShortPrefabName))
                    MountCount = 1;
            }
			
			public void OnDeathOrSleeping()
            {
				if (IsDeadOrSleeping)
					return;
				StopCountAndClear();
				IsDeadOrSleeping = true;
				InVanillaUi = false;
				InOwnBuilding = false;
				CuiHelper.DestroyUi(Player, AdvancedStatusName);
			}
			
			public void OnVanillaUi(bool isStart = true)
            {
				if (isStart)
				{
					if (!InVanillaUi)
					{
						StopCountAndClear();
						InVanillaUi = true;
						CuiHelper.DestroyUi(Player, AdvancedStatusName);
					}
				}
				else if (InVanillaUi)
                {
					InVanillaUi = false;
					if (CanShowBars)
						StartCount();
				}
			}
			
			private void StopCountAndClear()
            {
				IsUiActive = false;
				Player?.CancelInvoke(CountClientBars);
				AddedItems.Clear();
                _clientBars = -1;
                _nextPrivilegeTime = 0;
			}
			
			private void OnDestroy()
            {
				CancelInvoke("UpdateTimedBars");
				StopCountAndClear();
			}
		}
		#endregion
	}
}