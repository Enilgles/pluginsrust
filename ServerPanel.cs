// #define TESTING

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Facepunch;
using Facepunch.Extend;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins.ServerPanelExtensionMethods;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

#if CARBON
using Carbon.Base;
using Carbon.Modules;
#endif

namespace Oxide.Plugins
{
	[Info("ServerPanel", "Mevent", "1.1.3")]
	public class ServerPanel : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin
			ImageLibrary = null,
			NoEscape = null,
			Notify = null,
			UINotify = null,
			KillRecords = null,
			Statistics = null;

		private static ServerPanel Instance;

#if CARBON
		private ImageDatabaseModule imageDatabase;
#endif

		private bool _enabledImageLibrary;

		private Dictionary<int, int> _categoriesByID = new();

		private Dictionary<string, int> _categoriesByCommand = new();

		private Dictionary<int, Coroutine> _categoriesActiveCoroutines = new();
		
		private Dictionary<string, Func<BasePlayer, string>> _headerUpdateFields = new();

		private const string
			Perm_Edit = "serverpanel.edit",
			CmdMainConsole = "UI_ServerPanel",
			Layer = "UI.Server.Panel",
			LayerHeader = "UI.Server.Panel.Header",
			LayerContent = "UI.Server.Panel.Content",
			LayerCategories = "UI.Server.Panel.Categories",
			EditingLayerPageEditor = "UI.Server.Panel.Editor.Page",
			EditingLayerElementEditor = "UI.Server.Panel.Editor.Element",
			EditingLayerModal = "UI.Server.Panel.Editor.Modal",
			EditingLayerModalArrayView = EditingLayerModal + ".Content.View",
			EditingLayerModalAnchorSelector = "UI.Server.Panel.Editor.Modal.Anchor.Selector",
			EditingLayerModalColorSelector = "UI.Server.Panel.Editor.Modal.Color.Selector",
			EditingLayerModalTextEditor = "UI.Server.Panel.Editor.Modal.Text.Editor",
			EditingElementOutline = "UI.Server.Panel.Editor.EditingElement.Outline";

		private Dictionary<(float AnchorMinX, float AnchorMinY, float AnchorMaxX, float AnchorMaxY), string>
			rectToImage = new()
			{
				[(0, 1, 0, 1)] =
					"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/Anchors/top-left.png",
				[(0.5f, 1, 0.5f, 1)] =
					"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/Anchors/top-center.png",
				[(1, 1, 1, 1)] =
					"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/Anchors/top-right.png",

				[(0, 0.5f, 0, 0.5f)] =
					"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/Anchors/middle-left.png",
				[(0.5f, 0.5f, 0.5f, 0.5f)] =
					"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/Anchors/middle-center.png",
				[(1, 0.5f, 1, 0.5f)] =
					"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/Anchors/middle-right.png",

				[(0, 0, 0, 0)] =
					"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/Anchors/bottom-left.png",
				[(0.5f, 0, 0.5f, 0)] =
					"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/Anchors/bottom-center.png",
				[(1, 0, 1, 0)] =
					"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/Anchors/bottom-right.png",

				[(0, 1, 1, 1)] =
					"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/Anchors/top-stretch.png",
				[(0, 0, 0, 1)] =
					"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/Anchors/stretch-left.png",
				[(0.5f, 0, 0.5f, 1)] =
					"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/Anchors/stretch-center.png",
				[(1, 0, 1, 1)] =
					"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/Anchors/stretch-right.png",

				[(0, 0, 1, 1)] =
					"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/Anchors/stretch-stretch.png",
			};

		#endregion

		#region Config

		private static Configuration _config;

		private class Configuration
		{
			#region Fields

			[JsonProperty(PropertyName = "Work with Notify?")]
			public bool UseNotify = true;

			[JsonProperty(PropertyName = "Economy Header Fields",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<EconomyHeadField> EconomyFields = new()
			{
				EconomyHeadField.Create(true, EconomyEntry.CreateEconomics(), "{economy_economics}"),
				EconomyHeadField.Create(true, EconomyEntry.CreateServerRewards(), "{economy_server_rewards}"),
				EconomyHeadField.Create(true, EconomyEntry.CreateBankSystem(), "{economy_bank_system}"),
			};

			[JsonProperty(PropertyName = "Block Settings")]
			public BlockSettings Block = new()
			{
				BlockWhenBuildingBlock = false,
				BlockWhenRaidBlock = false,
				BlockWhenCombatBlock = false
			};

			[JsonProperty(PropertyName = "Auto-Open Settings")]
			public AutoOpenSettings AutoOpen = new()
			{
				ShowMenuEveryTime = true
			};

			#endregion Fields

			#region Classes

			public class AutoOpenSettings
			{
				[JsonProperty(PropertyName = "Show menu every time player connects to server?")]
				public bool ShowMenuEveryTime = true;
			}

			public class BlockSettings
			{
				[JsonProperty(PropertyName = "Block the opening during a building block?")]
				public bool BlockWhenBuildingBlock;

				[JsonProperty(PropertyName = "Block the opening during a raid block?")]
				public bool BlockWhenRaidBlock;

				[JsonProperty(PropertyName = "Block the opening during a combat block?")]
				public bool BlockWhenCombatBlock;
			}

			public class EconomyHeadField
			{
				#region Fields

				[JsonProperty(PropertyName = "Enabled")]
				public bool Enabled;

				[JsonProperty(PropertyName = "Economy Settings")]
				public EconomyEntry Economy = new();

				[JsonProperty(PropertyName = "Update key (MUST BE UNIQUE)")]
				public string UpdateKey;

				#endregion

				#region Constructors

				public static EconomyHeadField Create(bool enabled, EconomyEntry economy, string updateKey)
				{
					return new EconomyHeadField
					{
						Enabled = enabled,
						Economy = economy,
						UpdateKey = updateKey
					};
				}

				#endregion
			}

			public enum EconomyType
			{
				Plugin,
				Item
			}

			public class EconomyEntry
			{
				#region Fields

				[JsonProperty(PropertyName = "Type (Plugin/Item)")] [JsonConverter(typeof(StringEnumConverter))]
				public EconomyType Type;

				[JsonProperty(PropertyName = "Plugin name")]
				public string Plug;

				[JsonProperty(PropertyName = "Balance add hook")]
				public string AddHook;

				[JsonProperty(PropertyName = "Balance remove hook")]
				public string RemoveHook;

				[JsonProperty(PropertyName = "Balance show hook")]
				public string BalanceHook;

				[JsonProperty(PropertyName = "ShortName")]
				public string ShortName;

				[JsonProperty(PropertyName = "Display Name (empty - default)")]
				public string DisplayName;

				[JsonProperty(PropertyName = "Skin")] public ulong Skin;

				[JsonProperty(PropertyName = "Lang Key (for Title)")]
				public string TitleLangKey;

				[JsonProperty(PropertyName = "Lang Key (for Balance)")]
				public string BalanceLangKey;

				#endregion Fields

				#region Public Methods

				#region Titles

				public string GetTitle(BasePlayer player)
				{
					return Instance.Msg(player, TitleLangKey);
				}

				public string GetBalanceTitle(BasePlayer player)
				{
					return Instance.Msg(player, BalanceLangKey, ShowBalance(player).ToString());
				}

				#endregion Titles

				#region Economy

				public double ShowBalance(BasePlayer player)
				{
					switch (Type)
					{
						case EconomyType.Plugin:
						{
							var plugin = Instance?.plugins?.Find(Plug);
							if (plugin == null)
								return 0;

							return Convert.ToDouble(plugin.Call(BalanceHook, player.UserIDString));
						}
						case EconomyType.Item:
						{
							return PlayerItemsCount(player, ShortName, Skin);
						}
						default:
							return 0;
					}
				}

				public void AddBalance(BasePlayer player, double amount)
				{
					switch (Type)
					{
						case EconomyType.Plugin:
						{
							var plugin = Instance?.plugins?.Find(Plug);
							if (plugin == null) return;

							switch (Plug)
							{
								case "BankSystem":
								case "ServerRewards":
								case "IQEconomic":
									plugin.Call(AddHook, player.UserIDString, (int) amount);
									break;
								default:
									plugin.Call(AddHook, player.UserIDString, amount);
									break;
							}

							break;
						}
						case EconomyType.Item:
						{
							var am = (int) amount;

							var item = ToItem(am);
							if (item == null) return;

							player.GiveItem(item);
							break;
						}
					}
				}

				public bool RemoveBalance(BasePlayer player, double amount)
				{
					switch (Type)
					{
						case EconomyType.Plugin:
						{
							if (ShowBalance(player) < amount) return false;

							var plugin = Instance?.plugins.Find(Plug);
							if (plugin == null) return false;

							switch (Plug)
							{
								case "BankSystem":
								case "ServerRewards":
								case "IQEconomic":
									plugin.Call(RemoveHook, player.UserIDString, (int) amount);
									break;
								default:
									plugin.Call(RemoveHook, player.UserIDString, amount);
									break;
							}

							return true;
						}
						case EconomyType.Item:
						{
							var playerItems = Pool.Get<List<Item>>();
							player.inventory.GetAllItems(playerItems);
					
							var am = (int) amount;

							if (ItemCount(playerItems, ShortName, Skin) < am)
							{
								Pool.Free(ref playerItems);
								return false;
							}

							Take(playerItems, ShortName, Skin, am);
							Pool.Free(ref playerItems);
							return true;
						}
						default:
							return false;
					}
				}

				#endregion Economy

				#endregion

				#region Private Methods

				private Item ToItem(int amount)
				{
					var item = ItemManager.CreateByName(ShortName, amount, Skin);
					if (item == null)
					{
						Debug.LogError($"Error creating item with ShortName: '{ShortName}'");
						return null;
					}

					if (!string.IsNullOrEmpty(DisplayName)) item.name = DisplayName;

					return item;
				}

				private static int PlayerItemsCount(BasePlayer player, string shortname, ulong skin)
				{
					var items = Pool.Get<List<Item>>();
					player.inventory.GetAllItems(items);
			
					var result = ItemCount(items, shortname, skin);
			
					Pool.Free(ref items);
					return result;
				}

				private static int ItemCount(List<Item> items, string shortname, ulong skin)
				{
					return items.FindAll(item =>
							item.info.shortname == shortname && !item.isBroken && (skin == 0 || item.skin == skin))
						.Sum(item => item.amount);
				}

				private static void Take(List<Item> itemList, string shortname, ulong skinId, int amountToTake)
				{
					if (amountToTake == 0) return;

					var takenAmount = 0;

					var itemsToTake = Pool.Get<List<Item>>();

					foreach (var item in itemList)
					{
						if (item.info.shortname != shortname ||
						    (skinId != 0 && item.skin != skinId) || item.isBroken) continue;

						var remainingAmount = amountToTake - takenAmount;
						if (remainingAmount <= 0) break;

						if (item.amount > remainingAmount)
						{
							item.MarkDirty();
							item.amount -= remainingAmount;
							break;
						}

						if (item.amount <= remainingAmount)
						{
							takenAmount += item.amount;
							itemsToTake.Add(item);
						}

						if (takenAmount == amountToTake)
							break;
					}

					foreach (var itemToTake in itemsToTake)
						itemToTake.RemoveFromContainer();

					Pool.FreeUnmanaged(ref itemsToTake);
				}

				#endregion Private Methods

				#region Constructors

				public static EconomyEntry CreateEconomics()
				{
					return new EconomyEntry
					{
						Type = EconomyType.Plugin,
						Plug = "Economics",
						TitleLangKey = "Economy.Economics.Title",
						BalanceLangKey = "Economy.Economics.Balance",
						AddHook = "Deposit",
						BalanceHook = "Balance",
						RemoveHook = "Withdraw",
						ShortName = string.Empty,
						DisplayName = string.Empty,
						Skin = 0,
					};
				}

				public static EconomyEntry CreateServerRewards()
				{
					return new EconomyEntry
					{
						Type = EconomyType.Plugin,
						Plug = "ServerRewards",
						TitleLangKey = "Economy.ServerRewards.Title",
						BalanceLangKey = "Economy.ServerRewards.Balance",
						AddHook = "AddPoints",
						BalanceHook = "CheckPoints",
						RemoveHook = "TakePoints",
						ShortName = string.Empty,
						DisplayName = string.Empty,
						Skin = 0,
					};
				}

				public static EconomyEntry CreateBankSystem()
				{
					return new EconomyEntry
					{
						Type = EconomyType.Plugin,
						Plug  = "BankSystem",
						TitleLangKey = "Economy.BankSystem.Title",
						BalanceLangKey = "Economy.BankSystem.Balance",
						AddHook = "API_BankSystemDeposit",
						BalanceHook = "API_BankSystemBalance",
						RemoveHook = "API_BankSystemWithdraw",
						ShortName = string.Empty,
						DisplayName = string.Empty,
						Skin = 0,
					};
				}

				public static EconomyEntry CreateIQEconomic()
				{
					return new EconomyEntry
					{
						Type = EconomyType.Plugin,
						Plug = "IQEconomic",
						TitleLangKey = "Economy.IQEconomic.Title",
						BalanceLangKey = "Economy.IQEconomic.Balance",
						AddHook = "API_SET_BALANCE",
						BalanceHook = "API_GET_BALANCE",
						RemoveHook = "API_REMOVE_BALANCE",
						ShortName = string.Empty,
						DisplayName = string.Empty,
						Skin = 0,
					};
				}

				public static EconomyEntry CreateScrap()
				{
					return new EconomyEntry
					{
						Type = EconomyType.Item,
						Plug = string.Empty,
						TitleLangKey = "Economy.Scrap.Title",
						BalanceLangKey = "Economy.Scrap.Balance",
						AddHook = string.Empty,
						BalanceHook = string.Empty,
						RemoveHook = string.Empty,
						ShortName = "scrap",
						DisplayName = string.Empty,
						Skin = 0,
					};
				}

				#endregion Constructors
			}

			#endregion
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				_config = Config.ReadObject<Configuration>();
				if (_config == null) throw new Exception();
				SaveConfig();
			}
			catch
			{
				PrintError("Your configuration file contains an error. Using default configuration values.");
				LoadDefaultConfig();
			}
		}

		protected override void SaveConfig()
		{
			Config.WriteObject(_config);
		}

		protected override void LoadDefaultConfig()
		{
			_config = new Configuration();
		}

		#endregion Config

		#region Data

		#region Data.General

		public void SaveData()
		{
			SaveCategoriesData();

			SaveTemplateData();

			SaveLocalizationData();

			SaveHeaderFieldsData();
			
			SavePlayersData();
		}

		private void LoadData()
		{
			LoadCategoriesData();

			LoadTemplateData();

			LoadLocalizationData();

			LoadHeaderFieldsData();
			
			LoadPlayersData();
		}

		private void LoadDataFromFile<T>(ref T data, string filePath)
		{
			try
			{
				data = Interface.Oxide.DataFileSystem.ReadObject<T>(filePath);
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			data ??= Activator.CreateInstance<T>();
		}

		private void SaveDataToFile<T>(T data, string filePath)
		{
			Interface.Oxide.DataFileSystem.WriteObject(filePath, data);
		}

		#endregion Data.General

		#region Data.Categories

		private static CategoriesData _categoriesData;

		private class CategoriesData
		{
			[JsonProperty(PropertyName = "Categories", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<MenuCategory> Categories = new();
		}

		private void LoadCategoriesData() => LoadDataFromFile(ref _categoriesData, $"{Name}/Categories");

		private void SaveCategoriesData() => SaveDataToFile(_categoriesData, $"{Name}/Categories");

		#endregion Data.Categories

		#region Data.Template

		private static TemplateData _templateData;

		private void SaveTemplateData() => SaveDataToFile(_templateData, $"{Name}/Template");

		private void LoadTemplateData() => LoadDataFromFile(ref _templateData, $"{Name}/Template");

		private class TemplateData
		{
			#region Fields

			[JsonProperty(PropertyName = "Use an expert mod?")]
			public bool UseExpertMod = false;

			[JsonProperty(PropertyName = "UI Settings")]
			public UISettings UI = default;

			#endregion

			#region Public Methods

			public void ShowEditPageButtonsUI(BasePlayer player, CuiElementContainer container, string parent,
				string cmdAddPage = "",
				string cmdRemovePage = "",
				string cmdClonePage = "")
			{
				#region Add Page

				container.Add(new CuiButton
				{
					Button =
					{
						Color = HexToCuiColor("#4CAF50"),
						Command = cmdAddPage
					},
					Text =
					{
						Text = "+ NEW PAGE", Font = "robotocondensed-bold.ttf", FontSize = 11,
						Align = TextAnchor.MiddleCenter, Color = "0.8862745 0.8588235 0.827451 1"
					},
					RectTransform = {AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "10 10", OffsetMax = "90 35"}
				}, parent);

				#endregion

				#region Remove Page

				container.Add(new CuiButton
				{
					Button =
					{
						Color = HexToCuiColor("#CF432D"),
						Command = cmdRemovePage
					},
					Text =
					{
						Text = "DELETE PAGE", Font = "robotocondensed-bold.ttf", FontSize = 11,
						Align = TextAnchor.MiddleCenter, Color = "0.8862745 0.8588235 0.827451 1"
					},
					RectTransform = {AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "95 10", OffsetMax = "175 35"}
				}, parent);

				#endregion

				#region Clone Page

				container.Add(new CuiButton
				{
					Button =
					{
						Color = HexToCuiColor("#005FB7"),
						Command = cmdClonePage
					},
					Text =
					{
						Text = "CLONE PAGE", Font = "robotocondensed-bold.ttf", FontSize = 11,
						Align = TextAnchor.MiddleCenter, Color = "0.8862745 0.8588235 0.827451 1"
					},
					RectTransform = {AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "180 10", OffsetMax = "260 35"}
				}, parent);

				#endregion
			}

			public void ShowContentUI(BasePlayer player, CuiElementContainer container,
				string cmdPage = "",
				Action<string> callback = null)
			{
				if (!TryGetOpenedMenu(player.userID, out var openedMenu))
					return;

				var menuCategory = Instance.GetCategoryById(openedMenu.SelectedCategory);

				var page = openedMenu.PageIndex;
				var maxPages = menuCategory.Pages.Count;

				UI.Content.Background.Get(ref container, player, Layer, LayerContent,
					destroy: LayerContent);

				var categoryPage = menuCategory?.Pages[page];
				if (categoryPage != null)
				{
					switch (categoryPage.Type)
					{
						case CategoryPage.PageType.Plugin:
						{
							var elements =
								categoryPage.Plugin?.Call<CuiElementContainer>(categoryPage.PluginHook, player);
							if (elements != null)
							{
								container.AddRange(elements);
							}

							break;
						}

						case CategoryPage.PageType.UI:
						{
							categoryPage.Elements?.ForEach(element => element?.Get(ref container, player, LayerContent, element.Name, textFormatter: text => Instance.FormatUpdateField(player, text)));

							if (menuCategory.ShowPages)
								UI.Content.Pagination.ShowPagination(player, container, LayerContent, page,
									maxPages,
									LayerContent + ".Navigation", cmdPage);

							if (CanPlayerEdit(player))
							{
								UI.Content.EditButton.ShowEditButtonUI(player, container, LayerContent,
									cmdEdit: $"{CmdMainConsole} edit_page start {menuCategory.ID} {page}");

								ShowEditPageButtonsUI(player, container, LayerContent,
									cmdAddPage: $"{CmdMainConsole} edit_page add {menuCategory.ID} {page}",
									cmdRemovePage: $"{CmdMainConsole} edit_page remove {menuCategory.ID} {page}",
									cmdClonePage: $"{CmdMainConsole} edit_page clone {menuCategory.ID} {page}");
							}

							break;
						}
					}
				}

				callback?.Invoke(LayerContent);
			}

			public void ShowCloseButtonUI(BasePlayer player, CuiElementContainer container,
				string parent,
				string closeLayer = "",
				string command = "")
			{
				UI.CloseButton.ShowButtonUI(player, container, parent, parent + ".CloseButton", closeLayer: closeLayer,
					command: command);
			}

			public void ShowBackgroundUI(BasePlayer player, CuiElementContainer container,
				string cmdOnClick = "")
			{
				UI.Background.Background.Get(ref container, player, UI.Background.ParentLayer, Layer, destroy: Layer);

				if (UI.Background.CloseAfterClick)
				{
					container.Add(new CuiElement()
					{
						Parent = Layer,
						Components =
						{
							new CuiButtonComponent()
							{
								Color = "0 0 0 0",
								Close = Layer,
								Command = cmdOnClick
							},
							new CuiRectTransformComponent()
						}
					});
				}
			}

			public void ShowHeaderUI(BasePlayer player, CuiElementContainer container)
			{
				UI.Header.Background.Get(ref container, player, Layer, LayerHeader,
					destroy: LayerHeader);

				foreach (var headerField in _headerFieldsData.Fields)
					headerField.Get(ref container, player, LayerHeader,
						headerField.Name,
						destroy: headerField.Name,
						textFormatter: text => Instance.FormatUpdateField(player, text));
			}

			public void UpdateGlobalHeaderUI(BasePlayer player, CuiElementContainer container)
			{
				foreach (var headerField in _headerFieldsData.Fields)
					headerField.Get(ref container, player, LayerHeader,
						headerField.Name,
						textFormatter: text => Instance.FormatUpdateField(player, text),
						needUpdate: true);
			}

			public void UpdateHeaderUI(BasePlayer player, CuiElementContainer container)
			{
				foreach (var headerField in _headerFieldsData.elementsToUpdate)
					headerField.Get(ref container, player, LayerHeader,
						headerField.Name,
						destroy: headerField.Name,
						textFormatter: text => Instance.FormatUpdateField(player, text));
			}

			public void ShowUpdateHeaderUI(BasePlayer player)
			{
				UpdateUI(player, container => { UpdateHeaderUI(player, container); });
			}

			public void ShowCategories(BasePlayer player, CuiElementContainer container)
			{
				UI.Categories.Background.Get(ref container, player, Layer, LayerCategories,
					destroy: LayerCategories);

				container.Add(new CuiElement()
				{
					Name = Layer + ".Scroll.Panel",
					DestroyUi = Layer + ".Scroll.Panel",
					Parent = LayerCategories,
					Components =
					{
						new CuiImageComponent()
						{
							Color = "0 0 0 0"
						},
						UI.Categories.CategoriesScroll.GetRectTransform()
					}
				});
				
				ShowCategoriesScrollUI(player, container);
			}

			public void ShowCategoriesScrollUI(BasePlayer player, CuiElementContainer container, bool needUpdate = false)
			{
				var targetCategoriesCount = CountAvailableCategories(player.userID);

				if (CanPlayerEdit(player))
					targetCategoriesCount += 2;

				var totalWidth = targetCategoriesCount * UI.Categories.CategoryWidth +
				                 (targetCategoriesCount - 1) * UI.Categories.CategoriesMargin;

				if (UI.Categories.UseScrolling)
				{
					container.Add(new CuiElement
					{
						Name = Layer + ".Scroll.View",
						DestroyUi = Layer + ".Scroll.View",
						Parent = Layer + ".Scroll.Panel",
						Update = needUpdate,
						Components =
						{
							UI.Categories.CategoriesScroll.GetScrollView(CalculateContentRectTransform(totalWidth)),
						}
					});
				}
				else
				{
					container.Add(new CuiElement
					{
						Name = Layer + ".Scroll.View",
						DestroyUi = Layer + ".Scroll.View",
						Parent = Layer + ".Scroll.Panel",
						Update = needUpdate,
						Components =
						{
							new CuiImageComponent()
							{
								Color = "0 0 0 0"
							},
							new CuiRectTransformComponent()
						}
					});
				}

				ShowCategoriesLoopUI(player, container);
			}

			public void ShowCategoriesLoopUI(BasePlayer player,
				CuiElementContainer container)
			{
				if (!TryGetOpenedMenu(player.userID, out var openedMenu))
					return;

				var selectedCategory = openedMenu.SelectedCategory;

				var mainOffset = UI.Categories.CategoriesIndent;
				
				var availableCategories = GetAvailableCategories(player.userID);

				try
				{
					foreach (var menuCategory in availableCategories)
					{
						var isSelected = selectedCategory == menuCategory.ID;

						var categoryButton = Layer + ".Scroll.View" + $".Category.{menuCategory.ID}";

						container.Add(new CuiElement()
						{
							Name = categoryButton,
							DestroyUi = categoryButton,
							Parent = Layer + ".Scroll.View",
							Components =
							{
								GetCategoriesButtonComponent(isSelected, menuCategory),
								CalculateCategoriesPosition(mainOffset, UI.Categories.CategoryWidth,
									UI.Categories.CategoryHeight)
							}
						});

						UI.Categories.CategoryTitle.Get(player, container, categoryButton,
							text: Instance.Msg(player, menuCategory.Title),
							isSelected: isSelected);

						if (UI.Categories.ShowSelectedElement && isSelected)
							UI.Categories.SelectedElement.Get(ref container, player, categoryButton);

						if (UI.Categories.CategoryTitle.UseIcon && !string.IsNullOrEmpty(menuCategory.Icon))
							UI.Categories.CategoryTitle.Icon.Get(ref container, player, categoryButton,
								textFormatter: menuIcon => menuCategory.Icon);

						if (UI.Categories.CategoryTitle.UseOutline)
							(isSelected
									? UI.Categories.CategoryTitle.SelectedOutline
									: UI.Categories.CategoryTitle.Outline)
								.ShowOutlineUI(player, container, categoryButton);

						if (openedMenu.isEditMode)
							UI.Categories.CategoryEditPanel.GetCategoriesEditPanel(player,
								ref container,
								categoryButton,
								categoryButton + ".Panel.Settings",
								$"{CmdMainConsole} edit_menu category up {menuCategory.ID}",
								$"{CmdMainConsole} edit_menu category down {menuCategory.ID}",
								$"{CmdMainConsole} edit_category start {menuCategory.ID}");

						CategoriesLoopPosition(ref mainOffset);
					}
				}
				finally
				{
					Pool.FreeUnmanaged(ref availableCategories);
				}
				
				if (CanPlayerEdit(player))
				{
					container.Add(new CuiElement()
					{
						Name = Layer + ".Scroll.View.Category.Create",
						DestroyUi = Layer + ".Scroll.View.Category.Create",
						Parent = Layer + ".Scroll.View",
						Components =
						{
							new CuiImageComponent() {Color = "0 0 0 0"},
							CalculateCategoriesPosition(mainOffset, UI.Categories.CategoryWidth,
								UI.Categories.CategoryHeight)
						}
					});

					UI.Categories.AdminCategory.AdminCheckbox.GetCheckbox(player, container,
						Layer + ".Scroll.View.Category.Create",
						Layer + ".Scroll.View.Category.Create.CheckBox.AdminMode",
						$"{CmdMainConsole} edit_menu change_mode",
						openedMenu.isEditMode);

					UI.Categories.AdminCategory.ButtonAddCategory.Get(ref container,
						player, Layer + ".Scroll.View.Category.Create",
						cmdFormatter: text => $"{CmdMainConsole} edit_category create");

					CategoriesLoopPosition(ref mainOffset);

					container.Add(new CuiElement()
					{
						Name = Layer + ".Scroll.View.Category.Admin.Settings",
						DestroyUi = Layer + ".Scroll.View.Category.Admin.Settings",
						Parent = Layer + ".Scroll.View",
						Components =
						{
							new CuiButtonComponent()
							{
								Command = $"{CmdMainConsole} edit_header_fields start",
								Color = "0 0 0 0"
							},
							CalculateCategoriesPosition(mainOffset, UI.Categories.CategoryWidth,
								UI.Categories.CategoryHeight)
						}
					});

					UI.Categories.AdminCategory.ButtonAdminSettings.Get(ref container,
						player, Layer + ".Scroll.View.Category.Admin.Settings",
						cmdFormatter: text => $"{CmdMainConsole} edit_header_fields start");
				}
			}

			private void CategoriesLoopPosition(ref float mainOffset)
			{
				if (UI.Categories.CategoriesScroll.ScrollType == ScrollType.Horizontal)
					mainOffset += UI.Categories.CategoriesMargin + UI.Categories.CategoryWidth;
				else
					mainOffset = mainOffset - UI.Categories.CategoriesMargin - UI.Categories.CategoryHeight;
			}

			private CuiButtonComponent GetCategoriesButtonComponent(bool isSelected, MenuCategory menuCategory)
			{
				var btnComponent = new CuiButtonComponent
				{
					Color = isSelected
						? UI.Categories.CategoryTitle.SelectedBackgroundColor.Get()
						: UI.Categories.CategoryTitle.BackgroundColor.Get(),
					Command = menuCategory.ChatBtn && menuCategory.Commands.Length > 0 ? $"UI_ServerPanel_Send_Command UI_ServerPanel_Close|{menuCategory.Commands[0]}" : $"{CmdMainConsole} menu category {menuCategory.ID}",
				};

				if (!string.IsNullOrEmpty(UI.Categories.CategoryTitle.BackgroundSprite))
					btnComponent.Sprite = UI.Categories.CategoryTitle.BackgroundSprite;

				if (!string.IsNullOrEmpty(UI.Categories.CategoryTitle.BackgroundMaterial))
					btnComponent.Material = UI.Categories.CategoryTitle.BackgroundMaterial;
				return btnComponent;
			}

			private CuiRectTransform CalculateContentRectTransform(float totalWidth)
			{
				CuiRectTransform contentRect;
				if (UI.Categories.CategoriesScroll.ScrollType == ScrollType.Horizontal)
				{
					contentRect = new CuiRectTransform()
					{
						AnchorMin = "0 0", AnchorMax = "0 1",
						OffsetMin = "0 0",
						OffsetMax = $"{totalWidth} 0"
					};
				}
				else
				{
					contentRect = new CuiRectTransform()
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = $"0 -{totalWidth}",
						OffsetMax = "0 0"
					};
				}

				return contentRect;
			}

			private CuiRectTransformComponent CalculateCategoriesPosition(float offsetVal, float categoryWidth,
				float categoryHeight)
			{
				CuiRectTransformComponent cuiRect;
				if (UI.Categories.CategoriesScroll.ScrollType == ScrollType.Horizontal)
				{
					cuiRect = new CuiRectTransformComponent()
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = $"{offsetVal} -{categoryHeight}",
						OffsetMax = $"{offsetVal + categoryWidth} 0"
					};
				}
				else
				{
					cuiRect = new CuiRectTransformComponent()
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = $"0 {offsetVal - categoryHeight}",
						OffsetMax = $"{categoryWidth} {offsetVal}"
					};
				}

				return cuiRect;
			}

			#endregion
		}

		#endregion Data.Template

		#region Data.Localization

		private static LocalizationData _localizationData;

		private void SaveLocalizationData() => SaveDataToFile(_localizationData, $"{Name}/Localization");

		private void LoadLocalizationData() => LoadDataFromFile(ref _localizationData, $"{Name}/Localization");

		private class LocalizationData
		{
			[JsonProperty(PropertyName = "Localization Settings")]
			public LocalizationSettings Localization = new();
		}

		#endregion Data.Localization

		#region Data.Header

		private static HeaderFieldsData _headerFieldsData;

		private void SaveHeaderFieldsData() => SaveDataToFile(_headerFieldsData, $"{Name}/HeaderFields");

		private void LoadHeaderFieldsData()
		{
			LoadDataFromFile(ref _headerFieldsData, $"{Name}/HeaderFields");

			LoadHeaderFieldsDataCache();
		}

		private void LoadHeaderFieldsDataCache()
		{
			_headerFieldsData?.Load();
		}

		private class HeaderFieldsData
		{
			#region Fields

			[JsonProperty(PropertyName = "Fields", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<HeaderFieldUI> Fields = new();

			#endregion

			#region Cache

			[JsonIgnore] public bool needToUpdate;

			[JsonIgnore] public List<HeaderFieldUI> elementsToUpdate;

			public void Load()
			{
				elementsToUpdate?.Clear();

				elementsToUpdate = Fields.FindAll(x => x.NeedToUpdate);
				if (elementsToUpdate.Count > 0)
					needToUpdate = true;
			}

			#endregion
		}

		public class HeaderFieldUI : UiElement
		{
			#region Fields

			[JsonProperty(PropertyName = "Need to update?")]
			public bool NeedToUpdate;

			#endregion

			#region Constructors

			public HeaderFieldUI()
			{
			}

			public HeaderFieldUI(UiElement other) : base(other)
			{
				NeedToUpdate = false;
			}

			public HeaderFieldUI(UiElement other, bool needToUpdate) : base(other)
			{
				NeedToUpdate = needToUpdate;
			}

			#endregion
		}

		#endregion Data.Localization

		#region Data.Players

		private static PlayersData _playersData;

		private void SavePlayersData() => SaveDataToFile(_playersData, $"{Name}/Players");

		private void LoadPlayersData()
		{
			LoadDataFromFile(ref _playersData, $"{Name}/Players");
		}

		private class PlayersData
		{
			#region Fields

			[JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, PlayerData> Players = new();

			#endregion
		}

		public class PlayerData
		{
			#region Fields

			[JsonProperty(PropertyName = "Selected Editor Position")]
			public EditorPosition SelectedEditorPosition = EditorPosition.Left;

			#endregion

			#region Public Methods

			public static PlayerData GetOrCreate(string userID)
			{
				if (!_playersData.Players.TryGetValue(userID, out var data))
					_playersData.Players.TryAdd(userID, data = new PlayerData());

				return data;
			}
			
			public CuiRectTransformComponent GetEditorPosition()
			{
				switch (SelectedEditorPosition)
				{
					case EditorPosition.Center:
					{
						return new CuiRectTransformComponent()
						{
							AnchorMin = "0.5 0",
							AnchorMax = "0.5 1",
							OffsetMin = "-130 0",
							OffsetMax = "130 0"
						};
					}
                    
					case EditorPosition.Right:
					{
						return new CuiRectTransformComponent()
						{
							AnchorMin = "1 0",
							AnchorMax = "1 1",
							OffsetMin = "-260 0",
							OffsetMax = "0 0"
						};
					}
                    
					default:
					{
						return new CuiRectTransformComponent()
						{
							AnchorMin = "0 0",
							AnchorMax = "0 1",
							OffsetMin = "0 0",
							OffsetMax = "260 0"
						};
					}
				}
			}
			
			#endregion
		}

		public enum EditorPosition
		{
			Left, Center, Right
		}
		
		#endregion Data.Players

		#region Classes

		#region Categories

		public class MenuCategory
		{
			#region Fields

			[JsonProperty(PropertyName = "ID")] 
			public int ID;

			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = "Permission")]
			public string Permission = string.Empty;

			[JsonProperty(PropertyName = "Title")] 
			public string Title = string.Empty;

			[JsonProperty(PropertyName = "Chat Button")]
			public bool ChatBtn;

			[JsonProperty(PropertyName = "Show Pages?")]
			public bool ShowPages;

			[JsonProperty(PropertyName = "Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] Commands;

			[JsonProperty(PropertyName = "Icon")] 
			public string Icon = string.Empty;

			[JsonProperty(PropertyName = "Pages", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<CategoryPage> Pages = new();

			#endregion

			#region Public Methods

			public void MoveUp()
			{
				var index = _categoriesData.Categories.IndexOf(this);
				if (index > 0 && index < _categoriesData.Categories.Count)
				{
					(_categoriesData.Categories[index], _categoriesData.Categories[index - 1]) = (
						_categoriesData.Categories[index - 1],
						_categoriesData.Categories[index]); // Swap
				}
			}

			public void MoveDown()
			{
				var index = _categoriesData.Categories.IndexOf(this);
				if (index >= 0 && index < _categoriesData.Categories.Count - 1)
					(_categoriesData.Categories[index], _categoriesData.Categories[index + 1]) = (
						_categoriesData.Categories[index + 1],
						_categoriesData.Categories[index]); // Swap
			}

			public void ProcessCategory(string pluginName = null)
			{
				if (Pages is not {Count: 1})
					return;

				var page = Pages[0];
				if (page == null || page.Type != CategoryPage.PageType.Plugin || page.PluginName != pluginName) 
					return;

				var plugin = page.Plugin;
				if (plugin == null) return;
                
				if (plugin.IsLoaded)
					plugin.Call("OnReceiveCategoryInfo", ID);
				else
				{
					var coroutine = ServerMgr.Instance.StartCoroutine(Instance.CheckPluginLoaded(plugin, ID, 5));
					Instance._categoriesActiveCoroutines[ID] = coroutine;
				}
			}

			#endregion

			#region Constructors

			public static MenuCategory GetDefault()
			{
				return new MenuCategory
					{
						ID = Instance.GetUniqueCategoryID(),
						Enabled = true,
						Permission = string.Empty,
						Title = "New Category",
						ChatBtn = false,
						ShowPages = false,
						Commands = new[]
						{
							"test_command_123"
						},
						Icon = string.Empty,
						Pages = new List<CategoryPage>
						{
							new()
							{
								Title = string.Empty,
								Command = string.Empty,
								Type = CategoryPage.PageType.UI,
								PluginName = string.Empty,
								PluginHook = string.Empty,
								Elements = new List<UiElement>
								{
									UiElement.CreatePanel(
										InterfacePosition.CreatePosition(0.5f, 0.5f, 0.5f, 0.5f, -50, -50, 50, 50),
										IColor.CreateWhite())
								}
							}
						}
					};
			}

			public JObject ToJson()
			{
				var obj = new JObject()
				{
					["ID"] = ID,
					["Enabled"] = Enabled,
					["Permission"] = Permission,
					["Title"] = Title,
					["ChatBtn"] = ChatBtn,
					["ShowPages"] = ShowPages,
					["Commands"] = JArray.FromObject(Commands),
					["Icon"] = Icon,
					["Pages"] = JArray.FromObject(Pages.Select(p => p.ToJson()).ToArray()),
				};
				
				
				return obj;
			}

			public static MenuCategory FromJson(JObject obj)
			{
				var menuCategory = GetDefault();

				if (obj.TryGetValue("ID", out var id)) menuCategory.ID = Convert.ToInt32(id);
				if (obj.TryGetValue("Enabled", out var enabled)) menuCategory.Enabled = Convert.ToBoolean(enabled);
				if (obj.TryGetValue("Permission", out var permission)) menuCategory.Permission = Convert.ToString(permission);
				if (obj.TryGetValue("Title", out var title)) menuCategory.Title = Convert.ToString(title);
				if (obj.TryGetValue("ChatBtn", out var chatBtn)) menuCategory.ChatBtn = Convert.ToBoolean(chatBtn);
				if (obj.TryGetValue("ShowPages", out var showPages)) menuCategory.ShowPages = Convert.ToBoolean(showPages);
				if (obj.TryGetValue("Icon", out var icon)) menuCategory.Icon = Convert.ToString(icon);

				if (obj.TryGetValue("Commands", out var arrCommands))
				{
					var list = new List<string>();

					foreach (var targetCommand in (JArray) arrCommands)
					{
						list.Add(targetCommand?.ToString());
					}

					if (list.Count > 0)
						menuCategory.Commands = list.ToArray();
				}

				if (obj.TryGetValue("Pages", out var arrPages))
				{
					var list = new List<CategoryPage>();

					foreach (var jPage in (JArray) arrPages)
					{
						var targetPage = CategoryPage.FromJson((JObject) jPage);
						if (targetPage != null)
							list.Add(targetPage);
					}

					if (list.Count > 0)
						menuCategory.Pages = list;
				}

				return menuCategory;
			}

			#endregion
		}

		public class MenuCategoryBuilder
		{
			private bool _enabled;
			private string _permission = string.Empty;
			private string _title = string.Empty;
			private bool _chatButton;
			private bool _showPages = true;
			private List<string> _commands = new();
			private List<CategoryPage> _pages;

			public MenuCategory Build()
			{
				var menuCategory = new MenuCategory
				{
					ID = Instance.GetUniqueCategoryID(),
					Enabled = _enabled,
					Permission = _permission,
					Title = _title,
					ChatBtn = _chatButton,
					ShowPages = _showPages,
					Commands = _commands?.ToArray(),
					Pages = _pages
				};

				return menuCategory;
			}

			public MenuCategoryBuilder WithEnabled(bool enabled)
			{
				_enabled = enabled;
				return this;
			}

			public MenuCategoryBuilder WithTitle(string title)
			{
				_title = title;
				return this;
			}

			public MenuCategoryBuilder WithPermission(string permission)
			{
				_permission = permission;
				return this;
			}

			public MenuCategoryBuilder WithChatButton(bool chatButton)
			{
				_chatButton = chatButton;
				return this;
			}

			public MenuCategoryBuilder WithShowPages(bool showPages)
			{
				_showPages = showPages;
				return this;
			}

			public MenuCategoryBuilder WithCommand(string command)
			{
				_commands.Add(command);
				return this;
			}

			public MenuCategoryBuilder WithPages(List<CategoryPage> pages)
			{
				_pages = pages;
				return this;
			}
		}

		public class CategoryPage
		{
			#region Fields

			[JsonProperty(PropertyName = "Title")] 
			public string Title;

			[JsonProperty(PropertyName = "Command")]
			public string Command;

			[JsonProperty(PropertyName = "Type (Plugin/UI)")] [JsonConverter(typeof(StringEnumConverter))]
			public PageType Type;

			[JsonProperty(PropertyName = "Plugin Name")]
			public string PluginName;

			[JsonProperty(PropertyName = "Plugin Hook")]
			public string PluginHook;

			[JsonProperty(PropertyName = "UI Elements", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<UiElement> Elements = new();

			#endregion

			#region Cache

			[JsonIgnore] public Plugin Plugin => Instance?.plugins.Find(PluginName);

			#endregion

			#region Constructors

			public static CategoryPage GetDefault()
			{
				return new CategoryPage
							{
								Title = string.Empty,
								Command = string.Empty,
								Type = CategoryPage.PageType.UI,
								PluginName = string.Empty,
								PluginHook = string.Empty,
								Elements = new List<UiElement>
								{
									UiElement.CreatePanel(
										InterfacePosition.CreatePosition("0.5 0.5", "0.5 0.5", "-100 -100", "100 100"),
										IColor.CreateBlack()
									),
									UiElement.CreateLabel(
										InterfacePosition.CreatePosition("0.5 0.5", "0.5 0.5", "-100 -100", "100 100"),
										IColor.Create("#E2DBD3"), "TEST ELEMENT",
										fontSize: 14, align: TextAnchor.MiddleCenter),
								}
							};
			}

			public static CategoryPage FromJson(JObject obj)
			{
				var categoryPage = GetDefault();

				if (obj.TryGetValue("Title", out var Title)) categoryPage.Title = Title?.ToString();
				if (obj.TryGetValue("Command", out var Command)) categoryPage.Command = Command?.ToString();
				if (obj.TryGetValue("Type", out var Type)) categoryPage.Type = (PageType) Enum.Parse(typeof(PageType), Type?.ToString());
				if (obj.TryGetValue("PluginName", out var PluginName)) categoryPage.PluginName = PluginName?.ToString();
				if (obj.TryGetValue("PluginHook", out var PluginHook)) categoryPage.PluginHook = PluginHook?.ToString();

				return categoryPage;
			}

			public JObject ToJson()
			{
				return new JObject()
				{
					["Title"] = Title,
					["Command"] = Command,
					["Type"] = Type.ToString(),
					["PluginName"] = PluginName,
					["PluginHook"] = PluginHook,
				};
			}

			#endregion Constructors

			#region Classes

			public enum PageType
			{
				Plugin,
				UI
			}

			#endregion
		}

		#endregion Categories

		#region UI

		public class UISettings
		{
			#region Fields

			[JsonProperty(PropertyName = "ID (DONT CHANGE)")]
			public string ID;

			[JsonProperty(PropertyName = "Background")]
			public BackgroundUI Background = new();

			[JsonProperty(PropertyName = "Content")]
			public ContentUI Content = new();

			[JsonProperty(PropertyName = "Header")]
			public HeaderUI Header = new();

			[JsonProperty(PropertyName = "Categories")]
			public CategoriesUI Categories = new();

			[JsonProperty(PropertyName = "Close Button")]
			public CloseButtonUI CloseButton = new();

			#endregion

			#region Classes

			public class OutlineUI
			{
				#region Fields

				[JsonProperty(PropertyName = "Color")] public IColor Color = IColor.CreateTransparent();

				[JsonProperty(PropertyName = "Size")] public float Size = 0f;

				[JsonProperty(PropertyName = "Sprite")]
				public string Sprite = string.Empty;

				[JsonProperty(PropertyName = "Material")]
				public string Material = string.Empty;

				#endregion

				#region Public Methods

				public void ShowOutlineUI(BasePlayer player, CuiElementContainer container,
					string outlineParent,
					string name = "")
				{
					if (string.IsNullOrEmpty(name)) name = CuiHelper.GetGuid();

					var imageComponent = new CuiImageComponent
					{
						Color = Color.Get()
					};

					if (!string.IsNullOrWhiteSpace(Sprite))
						imageComponent.Sprite = Sprite;

					if (!string.IsNullOrWhiteSpace(Material))
						imageComponent.Material = Material;

					container.Add(new CuiElement()
					{
						Parent = outlineParent,
						Name = name + ".1",
						DestroyUi = name + ".1",
						Components =
						{
							imageComponent,
							new CuiRectTransformComponent
								{AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{Size}", OffsetMax = "0 0"}
						}
					});

					container.Add(new CuiElement()
					{
						Parent = outlineParent,
						Name = name + ".2",
						DestroyUi = name + ".2",
						Components =
						{
							imageComponent,
							new CuiRectTransformComponent
								{AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = $"0 {Size}"}
						}
					});

					container.Add(new CuiElement()
					{
						Parent = outlineParent,
						Name = name + ".3",
						DestroyUi = name + ".3",
						Components =
						{
							imageComponent,
							new CuiRectTransformComponent
							{
								AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = $"0 {Size}",
								OffsetMax = $"{Size} -{Size}"
							}
						}
					});

					container.Add(new CuiElement()
					{
						Parent = outlineParent,
						Name = name + ".4",
						DestroyUi = name + ".4",
						Components =
						{
							imageComponent,
							new CuiRectTransformComponent
							{
								AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = $"-{Size} {Size}",
								OffsetMax = $"0 -{Size}"
							}
						}
					});
				}

				#endregion
			}

			public class CloseButtonUI
			{
				#region Fields

				[JsonProperty(PropertyName = "Background")]
				public UiElement Background = new();

				[JsonProperty(PropertyName = "Title")] public UiElement Title = new();

				#endregion

				#region Public Methods

				public void ShowButtonUI(BasePlayer player, CuiElementContainer container,
					string parent,
					string name = "",
					string closeLayer = "",
					string command = "")
				{
					if (string.IsNullOrEmpty(name))
						name = CuiHelper.GetGuid();

					Background.Get(ref container, player, parent, name, name);

					Title.Get(ref container, player, name, name + ".Title");

					container.Add(new CuiElement()
					{
						Parent = name,
						Components =
						{
							new CuiButtonComponent()
							{
								Color = "0 0 0 0",
								Command = command,
								Close = closeLayer
							}
						}
					});
				}

				#endregion
			}

			public class EditButtonUI
			{
				#region Fields

				[JsonProperty(PropertyName = "Background")]
				public UiElement Background = new();

				[JsonProperty(PropertyName = "Title")] public UiElement Title = new();

				[JsonProperty(PropertyName = "Description Background")]
				public UiElement DescriptionBackground = new();

				[JsonProperty(PropertyName = "Description Title")]
				public UiElement DescriptionTitle = new();

				#endregion

				#region Public Methods

				public void ShowEditButtonUI(BasePlayer player, CuiElementContainer container,
					string parent,
					string name = "",
					string closeLayer = "",
					string cmdEdit = "")
				{
					if (string.IsNullOrEmpty(name))
						name = CuiHelper.GetGuid();

					Background.Get(ref container, player, parent, name);
					Title.Get(ref container, player, name);
					DescriptionBackground.Get(ref container, player, name, name + ".Description");
					DescriptionTitle.Get(ref container, player, name + ".Description");

					container.Add(new CuiElement()
					{
						Parent = name,
						Components =
						{
							new CuiButtonComponent()
							{
								Color = "0 0 0 0",
								Command = cmdEdit,
								Close = closeLayer
							}
						}
					});
				}

				#endregion
			}

			public class ContentUI
			{
				[JsonProperty(PropertyName = "Background")]
				public UiElement Background = new();

				[JsonProperty(PropertyName = "Pagination")]
				public PaginationUI Pagination = new();

				[JsonProperty(PropertyName = "Edit Button")]
				public EditButtonUI EditButton = new();
			}

			public class PaginationUI
			{
				#region Fields

				[JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
				public PaginationType Type = PaginationType.Text;

				[JsonProperty(PropertyName = "Text Pagination Settings")]
				public TextPaginationUI TextPagination = new();

				[JsonProperty(PropertyName = "Multiple Buttons Settings")]
				public MultipleButtonsPagination MultipleButtons = new();

				#endregion

				#region Public Methods

				public void ShowPagination(BasePlayer player, CuiElementContainer container,
					string parent,
					int page,
					int maxPages,
					string name = "",
					string cmdPage = "")
				{
					switch (Type)
					{
						case PaginationType.Text:
							ShowTextPaginationUI(player, container, parent, page, maxPages, name, cmdPage);
							break;

						case PaginationType.MultipleButtons:
							CreateMultipleButtonsPaginationUI(player, container, parent, page, maxPages, name, cmdPage);
							break;

						default:
							throw new ArgumentOutOfRangeException();
					}
				}

				#endregion

				#region Private Methods

				private void ShowTextPaginationUI(BasePlayer player, CuiElementContainer container,
					string parent,
					int page,
					int maxPages,
					string name = "",
					string cmdPage = "")
				{
					if (string.IsNullOrEmpty(name)) name = CuiHelper.GetGuid();

					TextPagination.TextLabel.Get(ref container, player, parent, name, name,
						textFormatter: paginationText => paginationText.Replace("{page}",
							(page + 1).ToString()).Replace("{maxPages}", maxPages.ToString()));

					TextPagination.ButtonBack.ShowButtonUI(player, container, name,
						command: $"{cmdPage} {Mathf.Max(page - 1, 0)}");

					TextPagination.ButtonNext.ShowButtonUI(player, container, name,
						command: $"{cmdPage} {Mathf.Min(page + 1, maxPages - 1)}");
				}

				private void CreateMultipleButtonsPaginationUI(BasePlayer player, CuiElementContainer container,
					string parent,
					int page,
					int maxPages,
					string name = "",
					string cmdPage = "")
				{
					if (string.IsNullOrEmpty(name)) name = CuiHelper.GetGuid();

					container.Add(new CuiElement()
					{
						Parent = parent,
						Name = name,
						DestroyUi = name,
						Components =
						{
							new CuiImageComponent() {Color = "0 0 0 0"},
							MultipleButtons.GetRectTransform()
						}
					});

					var totalWidth = maxPages * MultipleButtons.PageTitle.Width +
					                 (maxPages - 1) * MultipleButtons.Margin;

					string offsetMin, offsetMax;
					switch (MultipleButtons.Type)
					{
						case MultipleButtonsPagination.SortingType.Left:
							offsetMin = $"{-totalWidth} 0";
							offsetMax = "0 0";
							break;
						case MultipleButtonsPagination.SortingType.Center:
							var halfOfWidth = (float) Math.Round(totalWidth / 2f, 2);

							offsetMin = $"-{halfOfWidth} 0";
							offsetMax = $"{halfOfWidth} 0";
							break;
						case MultipleButtonsPagination.SortingType.Right:
							offsetMin = $"0 0";
							offsetMax = $"{totalWidth} 0";
							break;
						default:
							throw new ArgumentOutOfRangeException();
					}

					var pagesBackground = container.Add(new CuiPanel()
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = offsetMin, OffsetMax = offsetMax
						},
						Image =
						{
							Color = "0 0 0 0"
						}
					}, name);

					MultipleButtons.ButtonBack.ShowButtonUI(player, container, pagesBackground,
						command: $"{cmdPage} {Mathf.Max(page - 1, 0)}");

					MultipleButtons.ButtonNext.ShowButtonUI(player, container, pagesBackground,
						command: $"{cmdPage} {Mathf.Min(page + 1, maxPages - 1)}");

					var offsetX = 0f;
					for (var targetPage = 1; targetPage <= maxPages; targetPage++)
					{
						container.Add(new CuiPanel()
							{
								RectTransform =
								{
									AnchorMin = "0 1", AnchorMax = "0 1",
									OffsetMin = $"{offsetX} 0",
									OffsetMax =
										$"{offsetX + MultipleButtons.PageTitle.Width} {MultipleButtons.PageTitle.Height}",
								},
								Image =
								{
									Color = MultipleButtons.PageTitle.BackgroundColor.Get()
								}
							}, pagesBackground, name + $".Page.{targetPage}", name + $".Page.{targetPage}");

						MultipleButtons.PageTitle.Title.Get(ref container, player, name + $".Page.{targetPage}",
							textFormatter: paginationText => paginationText.Replace("{page}", targetPage.ToString()));

						#region Selected

						if ((targetPage - 1) == page)
							MultipleButtons.SelectedLine.Get(ref container, player, name + $".Page.{targetPage}");

						#endregion

						container.Add(new CuiElement()
						{
							Parent = name + $".Page.{targetPage}",
							Components =
							{
								new CuiButtonComponent()
								{
									Color = "0 0 0 0",
									Command = $"{cmdPage} {targetPage - 1}"
								},
								new CuiRectTransformComponent()
							}
						});

						offsetX += MultipleButtons.PageTitle.Width + MultipleButtons.Margin;
					}
				}

				#endregion

				#region Classes

				public class TextPaginationUI
				{
					[JsonProperty(PropertyName = "Button Back")]
					public CloseButtonUI ButtonBack = new();

					[JsonProperty(PropertyName = "Button Next")]
					public CloseButtonUI ButtonNext = new();

					[JsonProperty(PropertyName = "Label Settings")]
					public UiElement TextLabel = new();
				}

				public class MultipleButtonsPagination : InterfacePosition
				{
					#region Fields

					[JsonProperty(PropertyName = "Margin")]
					public float Margin;

					[JsonProperty(PropertyName = "Sorting Type")]
					public SortingType Type;

					[JsonProperty(PropertyName = "Page Title")]
					public PageButton PageTitle = new();

					[JsonProperty(PropertyName = "Selected Line")]
					public UiElement SelectedLine = new();

					[JsonProperty(PropertyName = "Button Back")]
					public CloseButtonUI ButtonBack = new();

					[JsonProperty(PropertyName = "Button Next")]
					public CloseButtonUI ButtonNext = new();

					#endregion

					#region Classes

					public enum SortingType
					{
						Left,
						Center,
						Right
					}

					public class PageButton
					{
						[JsonProperty(PropertyName = "Width")] public float Width;

						[JsonProperty(PropertyName = "Height")]
						public float Height;

						[JsonProperty(PropertyName = "Background Color")]
						public IColor BackgroundColor;

						[JsonProperty(PropertyName = "Title")] public UiElement Title = new();
					}

					#endregion
				}

				public enum PaginationType
				{
					Text,
					MultipleButtons
				}

				#endregion
			}

			public class BackgroundUI
			{
				#region Fields

				[JsonProperty(PropertyName = "Parent (Overlay/Hud)")]
				public string ParentLayer = "Overlay";

				[JsonProperty(PropertyName = "Background")]
				public UiElement Background = new();

				[JsonProperty(PropertyName = "Close after click?")]
				public bool CloseAfterClick;

				#endregion
			}

			public class HeaderUI
			{
				#region Fields

				[JsonProperty(PropertyName = "Background")]
				public UiElement Background = new();

				#endregion
			}

			public class CategoriesUI
			{
				#region Fields

				[JsonProperty(PropertyName = "Background")]
				public UiElement Background = new();

				[JsonProperty(PropertyName = "Use scrolling?")]
				public bool UseScrolling;

				[JsonProperty(PropertyName = "Categories Scroll")]
				public ScrollUIElement CategoriesScroll = new();

				[JsonProperty(PropertyName = "Categories Indent")]
				public float CategoriesIndent;

				[JsonProperty(PropertyName = "Category Width")]
				public float CategoryWidth;

				[JsonProperty(PropertyName = "Category Height")]
				public float CategoryHeight;

				[JsonProperty(PropertyName = "Categories Margin")]
				public float CategoriesMargin;

				[JsonProperty(PropertyName = "Show selected element?")]
				public bool ShowSelectedElement;

				[JsonProperty(PropertyName = "Selected Element")]
				public UiElement SelectedElement = new();

				[JsonProperty(PropertyName = "Category Title")]
				public CategoryTitleUI CategoryTitle = new();

				[JsonProperty(PropertyName = "Category Edit Panel")]
				public CategoryEditPanelUI CategoryEditPanel = new();

				[JsonProperty(PropertyName = "Admin Category")]
				public CategoriesAdminCategoryUI AdminCategory = new();

				#endregion
			}

			public class CategoriesAdminCategoryUI
			{
				[JsonProperty(PropertyName = "Admin Mode Checkbox")]
				public CheckboxElement AdminCheckbox = new();

				[JsonProperty(PropertyName = "Add Category Button")]
				public UiElement ButtonAddCategory = new();

				[JsonProperty(PropertyName = "Admin Settings Button")]
				public UiElement ButtonAdminSettings = new();
			}

			public class CategoryEditPanelUI
			{
				#region Fields

				[JsonProperty(PropertyName = "Background")]
				public UiElement Background = new();

				[JsonProperty(PropertyName = "Up Button")]
				public UiElement ButtonUp = new();

				[JsonProperty(PropertyName = "Down Button")]
				public UiElement ButtonDown = new();

				[JsonProperty(PropertyName = "Edit Button")]
				public UiElement ButtonEdit = new();

				#endregion

				#region Public Methods

				public void GetCategoriesEditPanel(BasePlayer player, ref CuiElementContainer container,
					string parent,
					string name = "",
					string cmdUp = "",
					string cmdDown = "",
					string cmdEdit = "")
				{
					Background?.Get(ref container, player, parent, name);

					ButtonUp?.Get(ref container, player, name, name + ".UpButton", cmdFormatter: cmd => cmdUp);
					ButtonDown?.Get(ref container, player, name, name + ".DownButton", cmdFormatter: cmd => cmdDown);
					ButtonEdit?.Get(ref container, player, name, name + ".EditButton", cmdFormatter: cmd => cmdEdit);
				}

				#endregion
			}

			public class CategoryTitleUI : InterfacePosition
			{
				#region Fields

				[JsonProperty(PropertyName = "Enabled")]
				public bool Enabled;

				[JsonProperty(PropertyName = "Font Size")]
				public int FontSize;

				[JsonProperty(PropertyName = "Font")] public string Font;

				[JsonProperty(PropertyName = "Align")] [JsonConverter(typeof(StringEnumConverter))]
				public TextAnchor Align;

				[JsonProperty(PropertyName = "Text Color")]
				public IColor TextColor = IColor.CreateTransparent();

				[JsonProperty(PropertyName = "Selected Text Color")]
				public IColor SelectedTextColor = IColor.CreateTransparent();

				[JsonProperty(PropertyName = "Background Color")]
				public IColor BackgroundColor = IColor.CreateTransparent();

				[JsonProperty(PropertyName = "Selected Background Color")]
				public IColor SelectedBackgroundColor = IColor.CreateTransparent();

				[JsonProperty(PropertyName = "Background Sprite")]
				public string BackgroundSprite = string.Empty;

				[JsonProperty(PropertyName = "Background Material")]
				public string BackgroundMaterial = string.Empty;

				[JsonProperty(PropertyName = "Show icon?")]
				public bool UseIcon;

				[JsonProperty(PropertyName = "Icon")] public UiElement Icon = new();

				[JsonProperty(PropertyName = "Show outline?")]
				public bool UseOutline;

				[JsonProperty(PropertyName = "Selected Outline")]
				public OutlineUI SelectedOutline = new();

				[JsonProperty(PropertyName = "Outline")]
				public OutlineUI Outline = new();

				#endregion

				#region Public Methods

				public CuiTextComponent GetText(string text, bool isSelected)
				{
					return new CuiTextComponent()
					{
						Text = text,
						Font = Font ?? "robotocondensed-bold.ttf",
						FontSize = FontSize,
						Align = Align,
						Color = isSelected ? SelectedTextColor.Get() : TextColor.Get()
					};
				}

				public void Get(BasePlayer player, CuiElementContainer container, string parent, string name = "",
					string text = "", bool isSelected = false)
				{
					if (string.IsNullOrEmpty(name)) name = CuiHelper.GetGuid();

					container.Add(new CuiElement()
					{
						Name = name,
						Parent = parent,
						Components =
						{
							GetText(text, isSelected),
							GetRectTransform()
						}
					});
				}

				#endregion
			}

			#endregion

			#region Public Methods

			public List<UiElement> GetAllUiElements()
			{
				var allUiElements = new List<UiElement>();

				GetAllUiElementsRecursive(this, ref allUiElements);

				return allUiElements;
			}

			#endregion

			#region Private Methods

			private void GetAllUiElementsRecursive(object obj, ref List<UiElement> allUiElements)
			{
				if (obj is UiElement uiElement)
				{
					allUiElements.Add(uiElement);
				}

				if (obj is UISettings uiSettings)
				{
					GetAllUiElementsRecursive(uiSettings.Background, ref allUiElements);
					GetAllUiElementsRecursive(uiSettings.Content, ref allUiElements);
					GetAllUiElementsRecursive(uiSettings.Header, ref allUiElements);
					GetAllUiElementsRecursive(uiSettings.Categories, ref allUiElements);
					GetAllUiElementsRecursive(uiSettings.CloseButton, ref allUiElements);
				}

				if (obj is BackgroundUI backgroundUI)
				{
					GetAllUiElementsRecursive(backgroundUI.Background, ref allUiElements);
				}

				if (obj is HeaderUI headerUI)
				{
					GetAllUiElementsRecursive(headerUI.Background, ref allUiElements); 
				}

				if (obj is ContentUI contentUI)
				{
					GetAllUiElementsRecursive(contentUI.Background, ref allUiElements);
					GetAllUiElementsRecursive(contentUI.Pagination, ref allUiElements);
					GetAllUiElementsRecursive(contentUI.EditButton, ref allUiElements);
				}

				if (obj is PaginationUI paginationUI)
				{
					GetAllUiElementsRecursive(paginationUI.TextPagination, ref allUiElements);
					GetAllUiElementsRecursive(paginationUI.MultipleButtons, ref allUiElements);
				}

				if (obj is CloseButtonUI closeButtonUI)
				{
					GetAllUiElementsRecursive(closeButtonUI.Background, ref allUiElements);
					GetAllUiElementsRecursive(closeButtonUI.Title, ref allUiElements);
				}

				if (obj is PaginationUI.MultipleButtonsPagination multipleButtonsPagination)
				{
					GetAllUiElementsRecursive(multipleButtonsPagination.PageTitle.Title, ref allUiElements);
					GetAllUiElementsRecursive(multipleButtonsPagination.SelectedLine, ref allUiElements);
					GetAllUiElementsRecursive(multipleButtonsPagination.ButtonBack, ref allUiElements);
					GetAllUiElementsRecursive(multipleButtonsPagination.ButtonNext, ref allUiElements);
				}

				if (obj is PaginationUI.TextPaginationUI textPagination)
				{
					GetAllUiElementsRecursive(textPagination.ButtonBack, ref allUiElements);
					GetAllUiElementsRecursive(textPagination.ButtonNext, ref allUiElements);
					GetAllUiElementsRecursive(textPagination.TextLabel, ref allUiElements);
				}

				if (obj is EditButtonUI editButton)
				{
					GetAllUiElementsRecursive(editButton.Background, ref allUiElements);
					GetAllUiElementsRecursive(editButton.Title, ref allUiElements);
					GetAllUiElementsRecursive(editButton.DescriptionBackground, ref allUiElements);
					GetAllUiElementsRecursive(editButton.DescriptionTitle, ref allUiElements);
				}
			}

			#endregion
		}

		public class InterfacePosition
		{
			#region Fields

			[JsonProperty(PropertyName = "AnchorMin (X)")]
			public float AnchorMinX;

			[JsonProperty(PropertyName = "AnchorMin (Y)")]
			public float AnchorMinY;

			[JsonProperty(PropertyName = "AnchorMax (X)")]
			public float AnchorMaxX;

			[JsonProperty(PropertyName = "AnchorMax (Y)")]
			public float AnchorMaxY;

			[JsonProperty(PropertyName = "OffsetMin (X)")]
			public float OffsetMinX;

			[JsonProperty(PropertyName = "OffsetMin (Y)")]
			public float OffsetMinY;

			[JsonProperty(PropertyName = "OffsetMax (X)")]
			public float OffsetMaxX;

			[JsonProperty(PropertyName = "OffsetMax (Y)")]
			public float OffsetMaxY;

			#endregion Fields

			#region Public Methods

			public float GetAxis(bool isX)
			{
				if (isX)
				{
					return OffsetMinX;
				}

				return -OffsetMaxY;
			}

			public void SetVerticalAxis(VerticalConstraint constraint)
			{
				switch (constraint)
				{
					case VerticalConstraint.Center:
						AnchorMinY = AnchorMaxY = 0.5f;
						break;
					case VerticalConstraint.Bottom:
						AnchorMinY = AnchorMaxY = 0f;
						break;
					case VerticalConstraint.Top:
						AnchorMinY = AnchorMaxY = 1f;
						break;
					case VerticalConstraint.Scale:
						AnchorMinY = 0f;
						AnchorMaxY = 1f;
						break;
				}
			}

			public VerticalConstraint GetVerticalAxis()
			{
				if (Mathf.Approximately(AnchorMinY, AnchorMaxY))
				{
					if (Mathf.Approximately(AnchorMinY, 0.5f))
						return VerticalConstraint.Center;
					if (Mathf.Approximately(AnchorMinY, 0f))
						return VerticalConstraint.Bottom;
					if (Mathf.Approximately(AnchorMinY, 1f))
						return VerticalConstraint.Top;
				}

				if (Mathf.Approximately(AnchorMinY, 0) && Mathf.Approximately(AnchorMaxY, 1))
					return VerticalConstraint.Scale;

				return VerticalConstraint.Custom;
			}
			
			public void SetHorizontalAxis(HorizontalConstraint constraint)
			{
				switch (constraint)
				{
					case HorizontalConstraint.Center:
						AnchorMinX = AnchorMaxX = 0.5f;
						break;
					case HorizontalConstraint.Left:
						AnchorMinX = AnchorMaxX = 0f;
						break;
					case HorizontalConstraint.Right:
						AnchorMinX = AnchorMaxX = 1f;
						break;
					case HorizontalConstraint.Scale:
						AnchorMinX = 0f;
						AnchorMaxX = 1f;
						break;
				}
			}
			
			public HorizontalConstraint GetHorizontalAxis()
			{
				if (Mathf.Approximately(AnchorMinX, AnchorMaxX))
				{
					if (Mathf.Approximately(AnchorMinX, 0.5f))
						return HorizontalConstraint.Center;
					if (Mathf.Approximately(AnchorMinX, 0f))
						return HorizontalConstraint.Left;
					if (Mathf.Approximately(AnchorMinX, 1f))
						return HorizontalConstraint.Right;
				}

				if (Mathf.Approximately(AnchorMinX, 0) && Mathf.Approximately(AnchorMaxX, 1))
					return HorizontalConstraint.Scale;

				return HorizontalConstraint.Custom;
			}
			
			public enum HorizontalConstraint
			{
				Left,
				Center,
				Right,
				Scale,
				Custom
			}
			
			public enum VerticalConstraint
			{
				Bottom,
				Center,
				Top,
				Scale,
				Custom
			}
			
			public void SetAxis(bool isX, float value)
			{
				if (isX)
				{
					var oldX = OffsetMinX;
					
					OffsetMinX = value;
					OffsetMaxX = OffsetMaxX - oldX + value;
				}
				else
				{ 
					var oldY = -OffsetMaxY;
					
					OffsetMaxY = -value;
					OffsetMinY = OffsetMinY + oldY - value;
				}
			}

			public void MoveX(float value)
			{
				OffsetMinX += value;
				OffsetMaxX += value;
			}
			
			public void MoveY(float value)
			{
				OffsetMinY += value;
				OffsetMaxY += value;
			}

			public float GetPadding(int type = 0) // 0 - left, 1 - right, 2 - top, 3 - bottom
			{
				switch (type)
				{
					case 0: return OffsetMinX;
					case 1: return -OffsetMaxX;
					case 2: return -OffsetMaxY;
					case 3: return OffsetMinY;
					default: return OffsetMinX;
				}
			}

			public void SetPadding(
				float? left = null,
				float? top = null, 
				float? right = null, 
				float? bottom = null)
			{
				if (left.HasValue) OffsetMinX = left.Value;
				if (right.HasValue) OffsetMaxX = -right.Value;
				
				if (bottom.HasValue) OffsetMinY = bottom.Value;
				if (top.HasValue) OffsetMaxY = -top.Value;
			}
			
			public float GetWidth()
			{
				return OffsetMaxX - OffsetMinX;
			}
			
			public void SetWidth(float width)
			{
				if (GetHorizontalAxis() == HorizontalConstraint.Center)
				{
					var half = (float) Math.Round(width / 2f, 2);
					
					OffsetMinX = -half;
					OffsetMaxX = half;
					return;
				}
				
				OffsetMaxX = OffsetMinX + width;
			}
			
			public float GetHeight()
			{
				return OffsetMaxY - OffsetMinY;
			}
			
			public void SetHeight(float height)
			{
				if (GetVerticalAxis() == VerticalConstraint.Center)
				{
					var half = (float) Math.Round(height / 2f, 2);
					
					OffsetMinY = -half;
					OffsetMaxY = half;
					return;
				}

				OffsetMaxY = OffsetMinY + height;
			}
			
			public Rect GetRect()
			{
				var rect = new Rect();

				ManipulateRect(GetPivot(), rectTransform => rect = rectTransform.rect);
				
				return rect;
			}

			private Vector2 GetPivot()
			{
				return Mathf.Approximately(AnchorMinX, 0.5f) ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f);
			}

			private void ManipulateRect(Vector2 pivot, Action<RectTransform> callback)
			{
				var rect = new GameObject().AddComponent<RectTransform>();	
				try
				{
					rect.pivot = pivot;
					rect.anchorMin = new Vector2(AnchorMinX, AnchorMinY);
					rect.anchorMax = new Vector2(AnchorMaxX, AnchorMaxY);
					rect.offsetMin = new Vector2(OffsetMinX, OffsetMinY);
					rect.offsetMax = new Vector2(OffsetMaxX, OffsetMaxY);
					
					callback?.Invoke(rect);

					AnchorMinX = rect.anchorMin.x;
					AnchorMinY = rect.anchorMin.y;
					AnchorMaxX = rect.anchorMax.x;
					AnchorMaxY = rect.anchorMax.y;
					OffsetMinX = rect.offsetMin.x;
					OffsetMinY = rect.offsetMin.y;
					OffsetMaxX = rect.offsetMax.x;
					OffsetMaxY = rect.offsetMax.y;
				}
				finally
				{
					UnityEngine.Object.Destroy(rect.gameObject);
				}
			}

			#region CuiRectTransformComponent

			[JsonIgnore] private CuiRectTransformComponent _cachedRectTransform = null;

			public CuiRectTransformComponent GetRectTransform()
			{
				if (_cachedRectTransform != null)
					return _cachedRectTransform;
				
				_cachedRectTransform = new CuiRectTransformComponent
				{
					AnchorMin = $"{AnchorMinX} {AnchorMinY}",
					AnchorMax = $"{AnchorMaxX} {AnchorMaxY}",
					OffsetMin = $"{OffsetMinX} {OffsetMinY}",
					OffsetMax = $"{OffsetMaxX} {OffsetMaxY}"
				};
				
				return _cachedRectTransform;
			}
			
			public void InvalidateCache()
			{
				_cachedRectTransform = null;
			}

			#endregion

			public string GetAnchorImage()
			{
				if (Instance?.rectToImage.TryGetValue(
					    new ValueTuple<float, float, float, float>(AnchorMinX, AnchorMinY, AnchorMaxX, AnchorMaxY),
					    out var image) == true)
					return image;

				return string.Empty;
			}
			
			public CuiRectTransformComponent GetRectTransform(Func<float, float> formatterOffMaxX,
				Func<float, float> formatterOffMaxY)
			{
				var oMaxX = OffsetMaxX;
				if (formatterOffMaxX != null) oMaxX = formatterOffMaxX(OffsetMaxX);

				var oMaxY = OffsetMaxY;
				if (formatterOffMaxY != null) oMaxY = formatterOffMaxY(OffsetMaxY);

				return new CuiRectTransformComponent
				{
					AnchorMin = $"{AnchorMinX} {AnchorMinY}",
					AnchorMax = $"{AnchorMaxX} {AnchorMaxY}",
					OffsetMin = $"{OffsetMinX} {OffsetMinY}",
					OffsetMax = $"{oMaxX} {oMaxY}"
				};
			}

			public override string ToString()
			{
				return JsonConvert.SerializeObject(GetRectTransform(), 0, new JsonSerializerSettings
				{
					DefaultValueHandling = DefaultValueHandling.Ignore
				}).Replace("\\n", "\n");
			}

			#endregion

			#region Constructors

			public static InterfacePosition CreatePosition(float aMinX, float aMinY, float aMaxX, float aMaxY,
				float oMinX, float oMinY, float oMaxX, float oMaxY)
			{
				return new InterfacePosition
				{
					AnchorMinX = aMinX,
					AnchorMinY = aMinY,
					AnchorMaxX = aMaxX,
					AnchorMaxY = aMaxY,
					OffsetMinX = oMinX,
					OffsetMinY = oMinY,
					OffsetMaxX = oMaxX,
					OffsetMaxY = oMaxY
				};
			}

			public static InterfacePosition CreatePosition(
				string anchorMin = "0 0",
				string anchorMax = "1 1",
				string offsetMin = "0 0",
				string offsetMax = "0 0")
			{
				var aMinX = float.Parse(anchorMin.Split(' ')[0]);
				var aMinY = float.Parse(anchorMin.Split(' ')[1]);
				var aMaxX = float.Parse(anchorMax.Split(' ')[0]);
				var aMaxY = float.Parse(anchorMax.Split(' ')[1]);
				var oMinX = float.Parse(offsetMin.Split(' ')[0]);
				var oMinY = float.Parse(offsetMin.Split(' ')[1]);
				var oMaxX = float.Parse(offsetMax.Split(' ')[0]);
				var oMaxY = float.Parse(offsetMax.Split(' ')[1]);

				return new InterfacePosition
				{
					AnchorMinX = aMinX,
					AnchorMinY = aMinY,
					AnchorMaxX = aMaxX,
					AnchorMaxY = aMaxY,
					OffsetMinX = oMinX,
					OffsetMinY = oMinY,
					OffsetMaxX = oMaxX,
					OffsetMaxY = oMaxY
				};
			}

			public static InterfacePosition CreatePosition(CuiRectTransform rectTransform)
			{
				var aMinX = float.Parse(rectTransform.AnchorMin.Split(' ')[0]);
				var aMinY = float.Parse(rectTransform.AnchorMin.Split(' ')[1]);
				var aMaxX = float.Parse(rectTransform.AnchorMax.Split(' ')[0]);
				var aMaxY = float.Parse(rectTransform.AnchorMax.Split(' ')[1]);
				var oMinX = float.Parse(rectTransform.OffsetMin.Split(' ')[0]);
				var oMinY = float.Parse(rectTransform.OffsetMin.Split(' ')[1]);
				var oMaxX = float.Parse(rectTransform.OffsetMax.Split(' ')[0]);
				var oMaxY = float.Parse(rectTransform.OffsetMax.Split(' ')[1]);

				return new InterfacePosition
				{
					AnchorMinX = aMinX,
					AnchorMinY = aMinY,
					AnchorMaxX = aMaxX,
					AnchorMaxY = aMaxY,
					OffsetMinX = oMinX,
					OffsetMinY = oMinY,
					OffsetMaxX = oMaxX,
					OffsetMaxY = oMaxY
				};
			}

			#endregion Constructors
		}

		public enum CuiElementType
		{
			Label,
			Panel,
			Button,
			Image,
			InputField
		}

		public enum ScrollType
		{
			Horizontal,
			Vertical
		}

				public class UiElement : InterfacePosition
		{
			#region Fields

			[JsonProperty(PropertyName = "Enabled?")]
			public bool Enabled;

			[JsonProperty(PropertyName = "Visible")]
			public bool Visible = true;

			[JsonProperty(PropertyName = "Name")] public string Name = string.Empty;

			[JsonProperty(PropertyName = "Type (Label/Panel/Button/Image)")]
			[JsonConverter(typeof(StringEnumConverter))]
			public CuiElementType Type;

			[JsonProperty(PropertyName = "Color")] public IColor Color = new("#FFFFFF", 100);

			[JsonProperty(PropertyName = "Text", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> Text = new();

			[JsonProperty(PropertyName = "Font Size")]
			public int FontSize;

			[JsonProperty(PropertyName = "Font")] public CuiElementFont Font = CuiElementFont.RobotoCondensedBold;

			[JsonProperty(PropertyName = "Align")] [JsonConverter(typeof(StringEnumConverter))]
			public TextAnchor Align;

			[JsonProperty(PropertyName = "Text Color")]
			public IColor TextColor = new("#FFFFFF", 100);

			[JsonProperty(PropertyName = "Command ({user} - user steamid)")]
			public string Command = string.Empty;

			[JsonProperty(PropertyName = "Image")] public string Image = string.Empty;

			[JsonProperty(PropertyName = "Cursor Enabled")]
			public bool CursorEnabled;

			[JsonProperty(PropertyName = "Keyboard Enabled")]
			public bool KeyboardEnabled;

			[JsonProperty(PropertyName = "Sprite")]
			public string Sprite = string.Empty;

			[JsonProperty(PropertyName = "Material")]
			public string Material = string.Empty;

			#endregion Fields

			#region Public Methods

			public new void InvalidateCache()
			{
				base.InvalidateCache();

				Color?.InvalidateCache();
				TextColor?.InvalidateCache();
			}

			public bool TryGetImage(out string image)
			{
				if (Type == CuiElementType.Image)
				{
					if (!string.IsNullOrEmpty(Image))
					{
						if (Image.IsURL())
						{
							image = Image;
							return true;
						}
					}
				}

				image = null;
				return false;
			}

			public void Get(ref CuiElementContainer container, BasePlayer player,
				string parent,
				string name = null,
				string destroy = "",
				string close = "",
				Func<string, string> textFormatter = null,
				Func<string, string> cmdFormatter = null,
				bool needUpdate = false)
			{
				if (!Enabled) return;
				
				if (string.IsNullOrEmpty(name))
					name = CuiHelper.GetGuid();

				if (needUpdate) destroy = string.Empty;

				switch (Type)
				{
					case CuiElementType.Label:
					{
						var targetText = GetLocalizedText(player);

						var text = string.Join("\n", targetText);

						if (textFormatter != null)
							text = textFormatter(text);

						container.Add(new CuiElement
						{
							Name = name,
							Parent = parent,
							DestroyUi = destroy,
							Update = needUpdate,
							Components =
							{
								new CuiTextComponent
								{
									Text = Visible ? text : string.Empty,
									Align = Align,
									Font = GetFontByType(Font),
									FontSize = FontSize,
									Color = Visible ? TextColor.Get() : "0 0 0 0"
								},
								GetRectTransform()
							}
						});
						break;
					}

					case CuiElementType.InputField:
					{
						var targetText = GetLocalizedText(player);

						var text = $"{string.Join("\n", targetText)}";

						if (textFormatter != null)
							text = textFormatter(text);

						container.Add(new CuiElement
						{
							Name = name,
							Parent = parent,
							DestroyUi = destroy,
							Update = needUpdate,
							Components =
							{
								new CuiInputFieldComponent
								{
									Text = Visible ? text : string.Empty,
									Align = Align,
									Font = GetFontByType(Font),
									FontSize = FontSize,
									Color = Visible ? TextColor.Get() : "0 0 0 0",
									HudMenuInput = true,
									ReadOnly = true
								},
								GetRectTransform()
							}
						});
						break;
					}

					case CuiElementType.Panel:
					{
						var imageElement = new CuiImageComponent
						{
							Color = Visible ? Color.Get() : "0 0 0 0"
						};

						if (!string.IsNullOrEmpty(Sprite)) imageElement.Sprite = Sprite;
						if (!string.IsNullOrEmpty(Material)) imageElement.Material = Material;

						var cuiElement = new CuiElement
						{
							Name = name,
							Parent = parent,
							DestroyUi = destroy,
							Update = needUpdate,
							Components =
							{
								imageElement,
								GetRectTransform()
							}
						};

						if (CursorEnabled)
							cuiElement.Components.Add(new CuiNeedsCursorComponent());

						if (KeyboardEnabled)
							cuiElement.Components.Add(new CuiNeedsKeyboardComponent());

						container.Add(cuiElement);
						break;
					}

					case CuiElementType.Button:
					{
						var targetCommand = $"{Command}".Replace("{user}", player.UserIDString);

						if (cmdFormatter != null)
							targetCommand = cmdFormatter(targetCommand);

						var btnElement = new CuiButtonComponent()
						{
							Command = targetCommand,
							Color = Visible ? Color.Get() : "0 0 0 0",
							Close = close
						};

						if (!string.IsNullOrEmpty(Sprite)) btnElement.Sprite = Sprite;
						if (!string.IsNullOrEmpty(Material)) btnElement.Material = Material;

						container.Add(new CuiElement
						{
							Name = name,
							Parent = parent,
							DestroyUi = destroy,
							Update = needUpdate,
							Components =
							{
								btnElement,
								GetRectTransform()
							}
						});

						var targetText = GetLocalizedText(player);
						var message = $"{string.Join("\n", targetText)}";

						if (textFormatter != null)
							message = textFormatter(message);

						if (!string.IsNullOrEmpty(message))
						{
							container.Add(new CuiElement
							{
								Parent = name,
								Components =
								{
									new CuiTextComponent()
									{
										Text = Visible ? message : string.Empty, 
										Align = Align,
										Font = GetFontByType(Font),
										FontSize = FontSize,
										Color = Visible ? TextColor.Get() : "0 0 0 0"
									},
									new CuiRectTransformComponent()
								}
							});
						}

						break;
					}

					case CuiElementType.Image:
					{
						if (string.IsNullOrEmpty(Image)) return;
						
						ICuiComponent imageElement;
						if (Image.StartsWith("assets/"))
						{
							if (Image.Contains("Linear"))
							{
								imageElement = new CuiRawImageComponent
								{
									Color = Visible ? Color.Get() : "0 0 0 0",
									Sprite = Image
								};
							}
							else
							{
								imageElement = new CuiImageComponent
								{
									Color =  Enabled ? Color.Get() : "0 0 0 0",
									Sprite = Image
								};
							}
						}
						else if (Image.IsURL())
						{
							imageElement = new CuiRawImageComponent
							{
								Png = Instance?.GetImage(Image),
								Color = Visible ? Color.Get(): "0 0 0 0",
							};
						}
						else
						{
							var image = Image;
							if (textFormatter != null)
								image = textFormatter(image);

							imageElement = new CuiRawImageComponent
							{
								Png = Instance?.GetImage(image),
								Color = Visible ? Color.Get(): "0 0 0 0",
							};
						}
						
						var cuiElement = new CuiElement
						{
							Name = name,
							Parent = parent,
							DestroyUi = destroy,
							Update = needUpdate,
							Components =
							{
								imageElement,
								GetRectTransform()
							}
						};
						
						if (CursorEnabled)
							cuiElement.Components.Add(new CuiNeedsCursorComponent());

						if (KeyboardEnabled)
							cuiElement.Components.Add(new CuiNeedsKeyboardComponent());

						container.Add(cuiElement);
						break;
					}
				}
			}

			private List<string> GetLocalizedText(BasePlayer player)
			{
				List<string> targetText;

				var playerLang = Instance?.lang?.GetLanguage(player.UserIDString);
				if (!string.IsNullOrWhiteSpace(playerLang) &&
				    _localizationData.Localization.Elements.TryGetValue(Name, out var elementLocalization) &&
				    elementLocalization.Messages.TryGetValue(playerLang, out var textLocalization))
				{
					targetText = textLocalization.Text;
				}
				else
				{
					targetText = Text;
				}

				return targetText;
			}

			private static string GenerateElementGUID(CuiElementType elementType)
			{
				return $"{elementType}_{CuiHelper.GetGuid().Substring(0, 10)}";
			}

			#endregion Public Methods

			#region Constructors

			public UiElement()
			{
			}

			public UiElement(UiElement other)
			{
				AnchorMinX = other.AnchorMinX;
				AnchorMinY = other.AnchorMinY;
				AnchorMaxX = other.AnchorMaxX;
				AnchorMaxY = other.AnchorMaxY;
				OffsetMinX = other.OffsetMinX;
				OffsetMinY = other.OffsetMinY;
				OffsetMaxX = other.OffsetMaxX;
				OffsetMaxY = other.OffsetMaxY;
				Enabled = other.Enabled;
				Name = other.Name;
				Type = other.Type;
				Color = other.Color;
				Text = other.Text;
				FontSize = other.FontSize;
				Font = other.Font;
				Align = other.Align;
				TextColor = other.TextColor;
				Command = other.Command;
				Image = other.Image;
				CursorEnabled = other.CursorEnabled;
				KeyboardEnabled = other.KeyboardEnabled;
			}

			public static UiElement CreatePanel(
				InterfacePosition position,
				IColor color,
				bool cursorEnabled = false,
				bool keyboardEnabled = false,
				string sprite = "",
				string material = "",
				string randomName = "")
			{
				if (string.IsNullOrWhiteSpace(randomName)) randomName = GenerateElementGUID(CuiElementType.Panel);

				return new UiElement
				{
					Name = randomName,
					AnchorMinX = position.AnchorMinX,
					AnchorMinY = position.AnchorMinY,
					AnchorMaxX = position.AnchorMaxX,
					AnchorMaxY = position.AnchorMaxY,
					OffsetMinX = position.OffsetMinX,
					OffsetMinY = position.OffsetMinY,
					OffsetMaxX = position.OffsetMaxX,
					OffsetMaxY = position.OffsetMaxY,
					Enabled = true,
					Visible = true,
					Type = CuiElementType.Panel,
					Color = color,
					Text = new List<string>(),
					FontSize = 14,
					Font = CuiElementFont.RobotoCondensedBold,
					Align = TextAnchor.UpperLeft,
					TextColor = new IColor("#FFFFFF", 100),
					Command = string.Empty,
					Image = string.Empty,
					CursorEnabled = cursorEnabled,
					KeyboardEnabled = keyboardEnabled,
					Sprite = sprite,
					Material = material
				};
			}

			public static UiElement CreateImage(
				InterfacePosition position,
				string image,
				IColor color = null,
				string randomName = "")
			{
				if (string.IsNullOrWhiteSpace(randomName)) randomName = GenerateElementGUID(CuiElementType.Image);

				color ??= new IColor("#FFFFFF", 100);

				return new UiElement
				{
					Name = randomName,
					AnchorMinX = position.AnchorMinX,
					AnchorMinY = position.AnchorMinY,
					AnchorMaxX = position.AnchorMaxX,
					AnchorMaxY = position.AnchorMaxY,
					OffsetMinX = position.OffsetMinX,
					OffsetMinY = position.OffsetMinY,
					OffsetMaxX = position.OffsetMaxX,
					OffsetMaxY = position.OffsetMaxY,
					Enabled = true,
					Visible = true,
					Type = CuiElementType.Image,
					Color = color,
					Text = new List<string>(),
					FontSize = 14,
					Font = CuiElementFont.RobotoCondensedBold,
					Align = TextAnchor.UpperLeft,
					TextColor = new IColor("#FFFFFF", 100),
					Command = string.Empty,
					Image = image
				};
			}

			public static UiElement CreateLabel(
				InterfacePosition position,
				IColor textColor,
				List<string> text,
				int fontSize = 14,
				string font = "robotocondensed-bold.ttf",
				TextAnchor align = TextAnchor.UpperLeft,
				string randomName = "")
			{
				if (string.IsNullOrWhiteSpace(randomName)) randomName = GenerateElementGUID(CuiElementType.Label);

				return new UiElement
				{
					Name = randomName,
					AnchorMinX = position.AnchorMinX,
					AnchorMinY = position.AnchorMinY,
					AnchorMaxX = position.AnchorMaxX,
					AnchorMaxY = position.AnchorMaxY,
					OffsetMinX = position.OffsetMinX,
					OffsetMinY = position.OffsetMinY,
					OffsetMaxX = position.OffsetMaxX,
					OffsetMaxY = position.OffsetMaxY,
					Enabled = true,
					Visible = true,
					Type = CuiElementType.Label,
					Color = new IColor("#FFFFFF", 100),
					Text = text,
					FontSize = fontSize,
					Font = GetFontTypeByFont(font),
					Align = align,
					TextColor = textColor,
					Command = string.Empty,
					Image = string.Empty
				};
			}

			public static UiElement CreateLabel(
				InterfacePosition position,
				IColor textColor,
				string text,
				int fontSize = 14,
				string font = "robotocondensed-bold.ttf",
				TextAnchor align = TextAnchor.UpperLeft,
				string randomName = "")
			{
				if (string.IsNullOrWhiteSpace(randomName)) randomName = GenerateElementGUID(CuiElementType.Label);

				return new UiElement
				{
					Name = randomName,
					AnchorMinX = position.AnchorMinX,
					AnchorMinY = position.AnchorMinY,
					AnchorMaxX = position.AnchorMaxX,
					AnchorMaxY = position.AnchorMaxY,
					OffsetMinX = position.OffsetMinX,
					OffsetMinY = position.OffsetMinY,
					OffsetMaxX = position.OffsetMaxX,
					OffsetMaxY = position.OffsetMaxY,
					Enabled = true,
					Visible = true,
					Type = CuiElementType.Label,
					Color = new IColor("#FFFFFF", 100),
					Text = new List<string> {text},
					FontSize = fontSize,
					Font = GetFontTypeByFont(font),
					Align = align,
					TextColor = textColor,
					Command = string.Empty,
					Image = string.Empty
				};
			}

			public static UiElement CreateButton(
				InterfacePosition position,
				IColor color,
				IColor textColor,
				string text = "",
				bool cursorEnabled = false,
				bool keyboardEnabled = false,
				string sprite = "",
				string material = "",
				int fontSize = 14,
				string font = "robotocondensed-bold.ttf",
				TextAnchor align = TextAnchor.UpperLeft,
				string command = "",
				string randomName = "")
			{
				if (string.IsNullOrWhiteSpace(randomName)) randomName = GenerateElementGUID(CuiElementType.Button);
				
				return new UiElement
				{
					Name = randomName,
					AnchorMinX = position.AnchorMinX,
					AnchorMinY = position.AnchorMinY,
					AnchorMaxX = position.AnchorMaxX,
					AnchorMaxY = position.AnchorMaxY,
					OffsetMinX = position.OffsetMinX,
					OffsetMinY = position.OffsetMinY,
					OffsetMaxX = position.OffsetMaxX,
					OffsetMaxY = position.OffsetMaxY,
					Enabled = true,
					Visible = true,
					Type = CuiElementType.Button,
					Color = color,
					Text = new List<string> {text},
					FontSize = fontSize,
					Font = GetFontTypeByFont(font),
					Align = align,
					TextColor = textColor,
					Command = command ?? string.Empty,
					Image = string.Empty,
					CursorEnabled = cursorEnabled,
					KeyboardEnabled = keyboardEnabled,
					Sprite = sprite,
					Material = material
				};
			}

			public static UiElement CreateInputField(
				InterfacePosition position,
				IColor textColor,
				string text,
				int fontSize = 14,
				string font = "robotocondensed-bold.ttf",
				TextAnchor align = TextAnchor.UpperLeft,
				string randomName = "")
			{
				if (string.IsNullOrWhiteSpace(randomName)) randomName = GenerateElementGUID(CuiElementType.InputField);

				return new UiElement
				{
					Name = randomName,
					AnchorMinX = position.AnchorMinX,
					AnchorMinY = position.AnchorMinY,
					AnchorMaxX = position.AnchorMaxX,
					AnchorMaxY = position.AnchorMaxY,
					OffsetMinX = position.OffsetMinX,
					OffsetMinY = position.OffsetMinY,
					OffsetMaxX = position.OffsetMaxX,
					OffsetMaxY = position.OffsetMaxY,
					Enabled = true,
					Visible = true,
					Type = CuiElementType.InputField,
					Color = new IColor("#FFFFFF", 100),
					Text = new List<string> {text},
					FontSize = fontSize,
					Font = GetFontTypeByFont(font),
					Align = align,
					TextColor = textColor,
					Command = string.Empty,
					Image = string.Empty
				};
			}

			#endregion Constructors
		}
				
		public class ScrollUIElement : InterfacePosition
		{
			#region Fields

			[JsonProperty(PropertyName = "Scroll Type")] [JsonConverter(typeof(StringEnumConverter))]
			public ScrollType ScrollType;

			[JsonProperty(PropertyName = "Movement Type")] [JsonConverter(typeof(StringEnumConverter))]
			public ScrollRect.MovementType MovementType;

			[JsonProperty(PropertyName = "Elasticity")]
			public float Elasticity;

			[JsonProperty(PropertyName = "Deceleration Rate")]
			public float DecelerationRate;

			[JsonProperty(PropertyName = "Scroll Sensitivity")]
			public float ScrollSensitivity;

			[JsonProperty(PropertyName = "Scrollbar Settings")]
			public ScrollBarSettings Scrollbar = new();

			#endregion

			#region Public Methods

			public CuiScrollViewComponent GetScrollView(CuiRectTransform contentTransform)
			{
				var cuiScrollView = new CuiScrollViewComponent
				{
					MovementType = MovementType,
					Elasticity = Elasticity,
					DecelerationRate = DecelerationRate,
					ScrollSensitivity = ScrollSensitivity,
					ContentTransform = contentTransform,
					Inertia = true
				};

				switch (ScrollType)
				{
					case ScrollType.Vertical:
					{
						cuiScrollView.Vertical = true;
						cuiScrollView.Horizontal = false;

						cuiScrollView.VerticalScrollbar = Scrollbar.Get();
						break;
					}

					case ScrollType.Horizontal:
					{
						cuiScrollView.Horizontal = true;
						cuiScrollView.Vertical = false;

						cuiScrollView.HorizontalScrollbar = Scrollbar.Get();
						break;
					}
				}

				return cuiScrollView;
			}

			#endregion

			#region Classes

			public class ScrollBarSettings
			{
				#region Fields

				[JsonProperty(PropertyName = "Invert")]
				public bool Invert;

				[JsonProperty(PropertyName = "Auto Hide")]
				public bool AutoHide;

				[JsonProperty(PropertyName = "Handle Sprite")]
				public string HandleSprite;

				[JsonProperty(PropertyName = "Size")] public float Size;

				[JsonProperty(PropertyName = "Handle Color")]
				public IColor HandleColor;

				[JsonProperty(PropertyName = "Highlight Color")]
				public IColor HighlightColor;

				[JsonProperty(PropertyName = "Pressed Color")]
				public IColor PressedColor;

				[JsonProperty(PropertyName = "Track Sprite")]
				public string TrackSprite;

				[JsonProperty(PropertyName = "Track Color")]
				public IColor TrackColor;

				#endregion

				#region Public Methods

				public CuiScrollbar Get()
				{
					var cuiScrollbar = new CuiScrollbar()
					{
						Size = Size
					};

					if (Invert) cuiScrollbar.Invert = Invert;
					if (AutoHide) cuiScrollbar.AutoHide = AutoHide;
					if (!string.IsNullOrEmpty(HandleSprite)) cuiScrollbar.HandleSprite = HandleSprite;
					if (!string.IsNullOrEmpty(TrackSprite)) cuiScrollbar.TrackSprite = TrackSprite;

					if (HandleColor != null) cuiScrollbar.HandleColor = HandleColor.Get();
					if (HighlightColor != null) cuiScrollbar.HighlightColor = HighlightColor.Get();
					if (PressedColor != null) cuiScrollbar.PressedColor = PressedColor.Get();
					if (TrackColor != null) cuiScrollbar.TrackColor = TrackColor.Get();

					return cuiScrollbar;
				}

				#endregion
			}

			#endregion
		}

		public class IColor
		{
			#region Fields

			[JsonProperty(PropertyName = "HEX")] public string Hex;

			[JsonProperty(PropertyName = "Opacity (0 - 100)")]
			public float Alpha;

			#endregion

			#region Public Methods
			
			[JsonIgnore]
			private string _cachedColorString;
			
			public string Get()
			{
				if (_cachedColorString != null)
					return _cachedColorString;
				
				_cachedColorString = GetNotCachedColor();
				return _cachedColorString;
			}

			public string GetNotCachedColor()
			{
				if (string.IsNullOrEmpty(Hex)) Hex = "#FFFFFF";

				var hexValue = Hex.Trim('#');
				if (hexValue.Length != 6)
					throw new ArgumentException("Invalid HEX color format. Must be 6 characters (e.g., #RRGGBB).", nameof(Hex));
				
				var r = byte.Parse(hexValue.Substring(0, 2), NumberStyles.HexNumber);
				var g = byte.Parse(hexValue.Substring(2, 2), NumberStyles.HexNumber);
				var b = byte.Parse(hexValue.Substring(4, 2), NumberStyles.HexNumber);

				return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100}";
			}

			public void InvalidateCache()
			{
				_cachedColorString = null;
			}
			
			#endregion

			#region Constructors

			public IColor(string hex, float alpha)
			{
				Hex = hex;
				Alpha = alpha;
			}

			public static IColor Create(string hex, float alpha = 100)
			{
				return new IColor(hex, alpha);
			}

			public static IColor CreateTransparent()
			{
				return new IColor("#000000", 0);
			}

			public static IColor CreateWhite()
			{
				return new IColor("#FFFFFF", 100);
			}

			public static IColor CreateBlack()
			{
				return new IColor("#000000", 100);
			}

			#endregion
		}

		public class CheckboxElement
		{
			#region Fields

			[JsonProperty(PropertyName = "Checkbox")]
			public UiElement Checkbox;

			[JsonProperty(PropertyName = "Title")] public UiElement Title;

			#endregion

			#region Public Methods

			public void GetCheckbox(BasePlayer player,
				CuiElementContainer container,
				string parent,
				string name,
				string cmd,
				bool isChecked)
			{
				Checkbox?.Get(ref container, player, parent, name, name, cmdFormatter: text => cmd,
					textFormatter: text => isChecked ? text : string.Empty);

				Title?.Get(ref container, player, name);
			}

			#endregion
		}

		#region Font

		public enum CuiElementFont
		{
			RobotoCondensedBold,
			RobotoCondensedRegular,
			DroidSansMono,
			PermanentMarker,
		}

		public static string GetFontByType(CuiElementFont fontType)
		{
			switch (fontType)
			{
				case CuiElementFont.RobotoCondensedBold:
					return "robotocondensed-bold.ttf";
				case CuiElementFont.RobotoCondensedRegular:
					return "robotocondensed-regular.ttf";
				case CuiElementFont.DroidSansMono:
					return "droidsansmono.ttf";
				case CuiElementFont.PermanentMarker:
					return "permanentmarker.ttf";
				default:
					throw new ArgumentOutOfRangeException(nameof(fontType), fontType, null);
			}
		}

		public static CuiElementFont GetFontTypeByFont(string font)
		{
			switch (font)
			{
				case "robotocondensed-bold.ttf":
					return CuiElementFont.RobotoCondensedBold;
				case "robotocondensed-regular.ttf":
					return CuiElementFont.RobotoCondensedRegular;
				case "droidsansmono.ttf":
					return CuiElementFont.DroidSansMono;
				case "permanentmarker.ttf":
					return CuiElementFont.PermanentMarker;
				default:
					throw new ArgumentOutOfRangeException(nameof(font), font, null);
			}
		}

		#endregion

		#endregion

		#region Localization

		private class LocalizationSettings
		{
			#region Fields

			[JsonProperty(PropertyName = "UI Elements", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, ElementLocalization> Elements = new();

			#endregion

			#region Classes

			public class ElementLocalization
			{
				[JsonProperty(PropertyName = "Messages", ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public Dictionary<string, LocalizationInfo> Messages = new();
			}

			public class LocalizationInfo
			{
				[JsonProperty(PropertyName = "Text", ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public List<string> Text = new();
			}

			#endregion
		}

		#endregion

		#endregion

		#endregion Data

		#region Hooks

		private void Init()
		{
			Instance = this;

			if (!_config.AutoOpen.ShowMenuEveryTime)
				Unsubscribe(nameof(OnPlayerConnected));
		}

		private void OnServerInitialized()
		{
			LoadData();
			
			LoadCategories();

			LoadImages();

			RegisterCommands();

			RegisterPermissions();

			LoadUpdateFields();
		}

		private void Unload()
		{
			foreach (var player in BasePlayer.activePlayerList) 
				API_OnServerPanelDestroyUI(player);

			foreach (var coroutine in _categoriesActiveCoroutines.Values) 
				ServerMgr.Instance.StopCoroutine(coroutine);

			_categoriesActiveCoroutines.Clear();
			
			_config = null;
			Instance = null;
			_templateData = null;
			_categoriesData = null;
			_headerFieldsData = null;
			_localizationData = null;
		}

		#region Images

#if !CARBON
		private void OnPluginLoaded(Plugin plugin)
		{
			switch (plugin.Name)
			{
				case "ImageLibrary":
					timer.In(1, LoadImages);
					break;
			}
		}

		private void OnPluginUnloaded(Plugin plugin)
		{
			switch (plugin.Name)
			{
				case "ImageLibrary":
					_enabledImageLibrary = false;
					break;
			}
		}
#endif

		#endregion

		#region Player Hooks

		private void OnPlayerConnected(BasePlayer player)
		{
			if (player == null || !player.userID.IsSteamId()) return;

			var availableCategories = GetAvailableCategories(player.userID);
			try
			{
				if (availableCategories.Count <= 0) return;
				
				var targetCategory = availableCategories[0];
				if (targetCategory == null) return;
				
				NextTick(() => StartShowMenu(player, targetCategory));
			}
			finally
			{
				Pool.FreeUnmanaged(ref availableCategories);
			}
		}

		private void OnPlayerDisconnected(BasePlayer player)
		{
			API_OnServerPanelClosed(player);
		}

		#endregion

		#endregion Hooks

		#region Commands

		private void CmdOpenMenu(IPlayer covPlayer, string command, string[] args)
		{
			var player = covPlayer.Object as BasePlayer;
			if (player == null) return;

			if (_categoriesData?.Categories == null || _templateData?.UI == null)
			{
				if (covPlayer.IsAdmin)
				{
					SendReply(player, "Plugin is not initialized! Please, contact admin");
				}
				else
				{
					SendReply(player, "Plugin is not initialized! Please, contact admin");
				}

				return;
			}

			if (_enabledImageLibrary == false)
			{
				SendNotify(player, NoILError, 1);

				BroadcastILNotInstalled();
				return;
			}

			var category = GetCategoryByCommand(command);
			if (category == null)
			{
				Reply(player, MsgCantOpenMenuInvalidCommand);
				return;
			}

			if (_config.Block.BlockWhenBuildingBlock && player.IsBuildingBlocked())
			{
				Reply(player, MsgCantOpenMenuBuildingBlock);
				return;
			}

			if (_config.Block.BlockWhenRaidBlock && IsServerPanelPlayerRaidBlocked(player))
			{
				Reply(player, MsgCantOpenMenuRaidBlock);
				return;
			}

			if (_config.Block.BlockWhenCombatBlock && IsServerPanelPlayerCombatBlocked(player))
			{
				Reply(player, MsgCantOpenMenuCombatBlock);
				return;
			}

			StartShowMenu(player, category);
		}

		[ConsoleCommand("UI_ServerPanel_Close")]
		private void CmdConsoleServerPanelClose(ConsoleSystem.Arg args)
		{
			var player = args.Player();
			if (player == null) return;
			
			API_OnServerPanelCallClose(player);
		}
		
		[ConsoleCommand("UI_ServerPanel_Send_Command")]
		private void CmdConsoleServerPanelSendCmd(ConsoleSystem.Arg args)
		{
			var player = args.Player();
			if (player == null || !args.HasArgs()) return;
			
			var allCommands = string.Join(" ", args.Args);
			foreach (var targetCMD in allCommands.Split('|'))
			{
				var targetArgs = targetCMD.Split(' ');
				
				var command =
					$"{targetArgs[0]}  \" {string.Join(" ", targetArgs.ToList().GetRange(1, targetArgs.Length - 1))}\" 0";
				
				player.SendConsoleCommand(command);
			}
		}

		[ConsoleCommand("serverpanel_broadcastvideo")]
		private void CmdConsoleServerPanelBroadcastVideo(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !arg.HasArgs()) return;
			
			var videoURL = string.Join(" ", arg.Args);
			if (string.IsNullOrWhiteSpace(videoURL)) return;

			player.Command("client.playvideo", videoURL);
		}

		[ConsoleCommand(CmdMainConsole)]
		private void CmdServerPanel(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !arg.HasArgs()) return;

#if TESTING
			Puts($"[CmdMainConsole] args: {string.Join(" ", arg.Args)}");
#endif
			
			switch (arg.GetString(0))
			{
				case "close":
				{
					API_OnServerPanelClosed(player);
					break;
				}

				case "menu":
				{
					if (!TryGetOpenedMenu(player.userID, out var openedMenu))
						return;

					switch (arg.GetString(1))
					{
						case "category":
						{
							var nextCategory = arg.GetInt(2);

							var menuCategory = GetCategoryById(nextCategory);
							if (menuCategory == null) return;

							if (Interface.CallHook("OnServerPanelCategoryPage", player, nextCategory, 0) != null)
								return;

							openedMenu.OnSelectCategory(nextCategory);

							UpdateUI(player, container =>
							{
								_templateData?.ShowCategoriesLoopUI(player, container);

								ShowContent(player, container);

								ShowCloseButton(player, container);
							});
							break;
						}

						case "page":
						{
							var targetPage = arg.GetInt(2);

							if (Interface.CallHook("OnServerPanelCategoryPage", player, openedMenu.SelectedCategory,
								    targetPage) != null)
								return;

							openedMenu.OnSelectPage(targetPage);

							openedMenu.UpdateContent();
							break;
						}
					}

					break;
				}

				case "edit_page":
				{
					if (!CanPlayerEdit(player)) return;

					switch (arg.GetString(1))
					{
						case "start":
						{
							var categoryID = arg.GetInt(2);
							var pageID = arg.GetInt(3);

							EditPageData.Create(player, categoryID, pageID);

							ShowPageEditorPanel(player);
							break;
						}

						case "save":
						{
							var editData = EditPageData.Get(player.userID);

							editData?.Save();
							break;
						}

						case "change_position":
						{
							var editPageData = EditPageData.Get(player.userID);

							API_OnServerPanelEditorChangePosition(player);
							
							editPageData?.OnChangePosition();
							break;
						}

						case "add": // add page
						{
							if (!TryGetOpenedMenu(player.userID, out var openedMenu))
								return;

							var categoryID = arg.GetInt(2);
							var pageID = arg.GetInt(3);

							GetCategoryById(categoryID)?.Pages.Add(CategoryPage.GetDefault());

							openedMenu.OnSelectPage(openedMenu.GetLastPage());

							ShowMenuUI(player);
							break;
						}

						case "remove": // remove page
						{
							if (!TryGetOpenedMenu(player.userID, out var openedMenu))
								return;

							var categoryID = arg.GetInt(2);
							var targetCategory = GetCategoryById(categoryID);
							if (targetCategory == null) return;

							var pageID = arg.GetInt(3);
							if (pageID <= 0) return;

							targetCategory.Pages.RemoveAt(pageID);

							openedMenu.OnSelectPage(openedMenu.GetLastPage());

							ShowMenuUI(player);
							break;
						}

						case "clone": // clone page
						{
							if (!TryGetOpenedMenu(player.userID, out var openedMenu))
								return;

							var categoryID = arg.GetInt(2);
							var targetCategory = GetCategoryById(categoryID);
							if (targetCategory == null) return;

							var pageID = arg.GetInt(3);
							if (pageID < 0) return;
							
							var linkedPages = new LinkedList<CategoryPage>(targetCategory.Pages);
							
							var currentNode = linkedPages.First;
							for (var i = 0; i < pageID; i++) currentNode = currentNode.Next;

							var clonedPage = currentNode.Value;
							
							linkedPages.AddAfter(currentNode, clonedPage);

							targetCategory.Pages = linkedPages.ToList();

							openedMenu.OnSelectPage(pageID + 1);

							ShowMenuUI(player);
							break;
						}

						case "element":
						{
							var editData = EditPageData.Get(player.userID);

							switch (arg.GetString(2))
							{
								case "edit":
								{
									var elementIndex = arg.GetInt(3);
									if (!editData.StartEditElement(elementIndex, LayerContent))
										return;

									EditUiElementData.Create(player,
										editData.elementIndex,
										editData.OnEditElementSave,
										editData.OnEditElementStartEdit,
										editData.OnEditElementStopEdit,
										editData.OnStartTextEditing,
										editData.OnStopTextEditing,
										editData.OnChangePosition);

									ShowElementEditorPanel(player);
									break;
								}
								
								case "add":
								{
									editData.categoryPage.Elements.Add(UiElement.CreatePanel(
										InterfacePosition.CreatePosition(0.5f, 0.5f, 0.5f, 0.5f, -50, -50, 50, 50),
										new IColor("#FFFFFF", 100)));

									SaveData();

									if (TryGetOpenedMenu(player.userID, out var openedMenu))
										openedMenu.UpdateContent();

									ShowPageEditorPanel(player);
									break;
								}

								case "remove":
								{
									if (!arg.HasArgs(3)) return;

									editData.categoryPage.Elements.RemoveAt(arg.GetInt(3));

									SaveData();

									if (TryGetOpenedMenu(player.userID, out var openedMenu))
										openedMenu.UpdateContent();

									ShowPageEditorPanel(player);
									break;
								}

								case "move":
								{
									if (!arg.HasArgs(4)) return;

									var targetElement = editData.categoryPage.Elements[arg.GetInt(3)];

									switch (arg.GetString(4))
									{
										case "up":
										{
											editData.categoryPage.Elements.MoveUp(targetElement);
											break;
										}

										case "down":
										{
											editData.categoryPage.Elements.MoveDown(targetElement);
											break;
										}
									}

									SaveData();

									if (TryGetOpenedMenu(player.userID, out var openedMenu))
										openedMenu.UpdateContent();

									ShowPageEditorPanel(player);
									break;
								}
								
								case "clone":
								{
									if (!arg.HasArgs(3)) return;

									var elementIndex = arg.GetInt(3);
									if (elementIndex < 0 || elementIndex >= editData.categoryPage.Elements.Count) return;

									var originalElement = editData.categoryPage.Elements[elementIndex];
									var clonedElement = new UiElement(originalElement);

									var originalName = originalElement.Name;
									var newName = originalName;
									
									var counter = 1;
									while (editData.categoryPage.Elements.Any(e => e.Name == newName))
									{
										newName = $"{originalName} ({counter})";
										counter++;
									}
									clonedElement.Name = newName;

									editData.categoryPage.Elements.Add(clonedElement);

									SaveData();

									if (TryGetOpenedMenu(player.userID, out var openedMenu))
										openedMenu.UpdateContent();

									ShowPageEditorPanel(player);
									break;
								}
								
								case "switch_show":
								{
									if (!arg.HasArgs(3)) return;

									var elementIndex = arg.GetInt(3);
									if (elementIndex < 0 || elementIndex >= editData.categoryPage.Elements.Count) return;

									var element = editData.categoryPage.Elements[elementIndex];
									element.Visible = !element.Visible;
											
									UpdateUI(player, container =>
									{
										element.Get(ref container, 
											player,
											Layer + ".Content",
											element.Name, element.Name, needUpdate: true);
									});
									
									SaveConfig();
									
									UpdateUI(player, container =>
									{
										UpdatePointPageEditorUI(container, 
											elementIndex,
											element,
											string.Join(" ", arg.Args.SkipLast(1)));
									});
									break;
								}
							}

							break;
						}
					}

					break;
				}

				case "edit_element":
				{
					if (!CanPlayerEdit(player)) return;

					switch (arg.GetString(1))
					{
						case "cancel":
						{
							var editPageData = EditUiElementData.Get(player.userID);
							editPageData.EndEditElement(true);
							break;
						}

						case "save":
						{
							var editPageData = EditUiElementData.Get(player.userID);

							UpdateUI(player, container =>
							{
								UpdateTitlePageEditorFieldUI(container, editPageData.elementIndex,
									editPageData.editingElement, true);
							});

							editPageData.EndEditElement();
							break;
						}

						case "change_position":
						{
							var editPageData = EditUiElementData.Get(player.userID);
							
							API_OnServerPanelEditorChangePosition(player);
							
							editPageData?.OnChangePosition?.Invoke();
							
							ShowElementEditorPanel(player);
							break;
						}
						
						case "field":
						{
							var editPageData = EditUiElementData.Get(player.userID);

							var fieldName = arg.GetString(2);

							var parent = arg.GetString(3);
							if (string.IsNullOrEmpty(parent)) return;
							
							var targetField = editPageData.editingElement.GetType().GetField(fieldName);
							if (targetField == null)
								return;

							if (targetField.FieldType.IsEnum)
							{
								if (targetField.GetValue(editPageData.editingElement) is not Enum nowEnum) return;

								Enum targetEnum = null;
								switch (arg.GetString(4))
								{
									case "prev":
									{
										targetEnum = nowEnum.Previous();
										break;
									}

									case "next":
									{
										targetEnum = nowEnum.Next();
										break;
									}
								}

								if (targetEnum == null) return;

								targetField.SetValue(editPageData.editingElement, targetEnum);
							}
							else if (targetField.FieldType == typeof(List<string>))
							{
								var val = string.Join(" ", arg.Args.Skip(4));
								if (!string.IsNullOrEmpty(val))
								{
									var text = new List<string>();
									foreach (var line in val.Split('\n'))
									{
										text.Add(line);
									}

									targetField.SetValue(editPageData.editingElement, text);
								}
							}
							else if (targetField.FieldType == typeof(string))
							{
								var val = string.Join(" ", arg.Args.Skip(4));
								if (!string.IsNullOrEmpty(val))
									targetField.SetValue(editPageData.editingElement, val);
							}
							else
							{
								var newValue = string.Join(" ", arg.Args.Skip(4));

								try
								{
									var convertedValue = Convert.ChangeType(newValue, targetField.FieldType);
									targetField.SetValue(editPageData.editingElement, convertedValue);
								}
								catch (Exception ex)
								{
									Puts($"Error setting property '{fieldName}': {ex.Message}");
									player.SendMessage($"Error setting property '{fieldName}': {ex.Message}");
									return;
								}
							}

							if (targetField.Name == nameof(UiElement.Type))
							{
								UpdateUI(player, container =>
								{
									if (editPageData.isTextEditing)
									{
										ShowTextEditorLinesUI(player, ref container);
									}
									else
									{
										editPageData.UpdateEditElement(ref container, player,
											needAddImage: targetField.Name == nameof(UiElement.Image));
									}
								});

								ShowElementEditorPanel(player);
							}
							else
								UpdateUI(player, container =>
								{
									if (editPageData.isTextEditing)
										ShowTextEditorLinesUI(player, ref container);
									else
										editPageData.UpdateEditElement(ref container, player,
											needAddImage: targetField.Name == nameof(UiElement.Image),
											needUpdate: editPageData.editingElement.Type == CuiElementType.Label);
									
									FieldElementUI(container, parent, targetField,
										targetField.GetValue(editPageData.editingElement));
								});

							break;
						}

						case "color":
						{
							var editPageData = EditUiElementData.Get(player.userID);

							switch (arg.GetString(2))
							{
								case "start":
								{
									var fieldName = arg.GetString(3);
									if (string.IsNullOrEmpty(fieldName)) return;

									var targetField = editPageData.editingElement.GetType().GetField(fieldName);
									if (targetField == null)
										return;

									var parent = arg.GetString(4);
									if (string.IsNullOrEmpty(parent)) return;

									ShowColorSelectionPanel(player, fieldName, parent);
									break;
								}

								case "close":
								{
									break;
								}

								case "set":
								{
									var fieldName = arg.GetString(3);
									if (string.IsNullOrEmpty(fieldName)) return;

									var targetField = editPageData.editingElement.GetType().GetField(fieldName);
									if (targetField == null)
										return;

									var parent = arg.GetString(4);
									if (string.IsNullOrEmpty(parent)) return;

									if (targetField.GetValue(editPageData.editingElement) is not IColor 
									    targetValue) return;

									switch (arg.GetString(5))
									{
										case "hex":
										{
											var hex = string.Join(" ", arg.Args.Skip(6));
											if (string.IsNullOrEmpty(hex)) return;

											var str = hex.Trim('#');
											if (!str.IsHex())
												return;
											
											targetValue.Hex = str;

											targetField.SetValue(editPageData.editingElement, targetValue);
											break;
										}

										case "opacity":
										{
											var opacity = arg.GetFloat(6);
											if (opacity is < 0 or > 100)
												return;

											opacity = (float) Math.Round(opacity, 2);

											targetValue.Alpha = opacity;

											targetField.SetValue(editPageData.editingElement, targetValue);
											break;
										}
									}
									
									targetValue?.InvalidateCache();
									
									UpdateUI(player, container =>
									{
										if (editPageData.isTextEditing)
											ShowTextEditorLinesUI(player, ref container);
										else
											editPageData.UpdateEditElement(ref container, player);

										FieldElementUI(container, parent, targetField,
											targetField.GetValue(editPageData.editingElement));
									});

									ShowColorSelectionPanel(player, fieldName, parent);
									break;
								}
							}

							break;
						}

						case "text":
						{
							var editPageData = EditUiElementData.Get(player.userID);

							switch (arg.GetString(2))
							{
								case "start":
								{
									editPageData.StartTextEditing();

									ShowTextEditorPanel(player);
									break;
								}

								case "pre_close":
								{
									ShowTextEditorNotifyBeforeClose(player);
									break;
								}

								case "close":
								{
									editPageData.CloseTextEditingWithoutSaving();
									break;
								}

								case "save":
								{
									editPageData.SaveTextEditingChanges();
									break;
								}

								case "toggle_formatting":
								{
									editPageData.ToggleTextFormatting();

									UpdateUI(player, container =>
									{
										ShowTextEditorScrollLinesUI(player, ref container);

										FormattingFieldElementUI(player, container);
									});
									break;
								}
								
								case "lang":
								{
									switch (arg.GetString(3))
									{
										case "select":
										{
											var targetLang = arg.GetString(4);
											if (string.IsNullOrEmpty(targetLang)) return;

											editPageData.SelectLang(targetLang);

											UpdateUI(player, container =>
											{
												ShowTextEditorLangsUI(player, container);
												
												ShowTextEditorScrollLinesUI(player, ref container);
											});

											break;
										}

										case "remove":
										{
											var targetLang = arg.GetString(4);
											if (string.IsNullOrEmpty(targetLang)) return;

											editPageData.RemoveLang(targetLang);

											UpdateUI(player, container =>
											{
												ShowTextEditorLangsUI(player, container);

												ShowTextEditorLinesUI(player, ref container);
											});
											break;
										}
									}

									break;
								}

								case "line":
								{
									var textAction = arg.GetString(3);

									var textIndex = arg.GetInt(4);

									var text = editPageData.GetEditableText().ToList();

									if (textAction != "add")
										if (textIndex < 0 || textIndex >= text.Count)
											return;

									switch (textAction)
									{
										case "set":
										{
											var argsToJoin = arg.FullString.SplitQuotesStrings().Skip(5);
											
											var val = string.Join(" ", argsToJoin).FormatEscapedRichText();
											if (string.IsNullOrEmpty(val)) return;

											text[textIndex] = val;

											editPageData.SaveTextForLang(text);

											UpdateUI(player, container => ShowTextEditorLinesUI(player, ref container));
											break;
										}

										case "remove":
										{
											text.RemoveAt(textIndex);

											editPageData.SaveTextForLang(text);

											CuiHelper.DestroyUi(player,
												EditingLayerModalTextEditor + $".Main.Edit.Text.Line.{text.Count + 1}");

											UpdateUI(player, container => ShowTextEditorLinesUI(player, ref container));
											break;
										}

										case "add":
										{
											text.Add(string.Empty);

											editPageData.SaveTextForLang(text);

											UpdateUI(player,
												container =>
												{
													ShowTextEditorScrollLinesUI(player, ref container);
												});
											break;
										}
									}

									break;
								}
							}

							break;
						}

						case "rect_transform":
						{
							switch (arg.GetString(2))
							{
								case "move":
								{
									var editPageData = EditUiElementData.Get(player.userID);
									
									var pos = editPageData.editingElement;

									var axis = arg.GetString(3);

									switch (axis)
									{
										case "left":
										{
											editPageData.editingElement.MoveX(-editPageData.movementStep);
											break;
										}

										case "right":
										{
											editPageData.editingElement.MoveX(editPageData.movementStep);
											break;
										}

										case "top":
										{
											editPageData.editingElement.MoveY(editPageData.movementStep);
											break;
										}

										case "bottom":
										{
											editPageData.editingElement.MoveY(-editPageData.movementStep);
											break;
										}
									}

									UpdateUI(player, container =>
									{
										PositionSectionUI(player, container, arg.GetString(0), pos);

										editPageData.UpdateEditElement(ref container, player, needUpdate: editPageData.editingElement.Type == CuiElementType.Label);
									});

									break;
								}

								case "expert_mode":
								{
									var editPageData = EditUiElementData.Get(player.userID);

									editPageData.ExpertMode = !editPageData.ExpertMode;
									
									UpdateUI(player, container =>
									{
										PositionSectionUI(player, container, arg.GetString(0), editPageData.editingElement);
									});
									
									break;
								}
								
								case "enter":
								{
									var editPageData = EditUiElementData.Get(player.userID);
									
									var pos = editPageData.editingElement;

									var targetName = arg.GetString(3);
									switch (targetName)
									{
										case "axis":
										{
											var label = arg.GetString(4);
											var size = arg.GetFloat(5);

											switch (label)
											{
												case "X":
												{
													editPageData.editingElement.SetAxis(true, size);
													break;
												}
												case "Y":
												{
													editPageData.editingElement.SetAxis(false, size);
													break;
												}
											}
											break;
										}
										
										case "W":
										{ 
											var size = arg.GetFloat(4);

											editPageData.editingElement.SetWidth(size);
											break;
										}
										
										case "H":
										{ 
											var size = arg.GetFloat(4);

											editPageData.editingElement.SetHeight(size);
											break;
										}

										case "padding":
										{
											var vector = arg.GetString(4);
											var label = arg.GetString(5);
											var size = arg.GetFloat(6);

											switch (vector)
											{
												case "left":
												{
													editPageData.editingElement.SetPadding(left: size);
													break;
												}
												
												case "right":
												{
													editPageData.editingElement.SetPadding(right: size);
													break;
												}
												
												case "top":
												{
													editPageData.editingElement.SetPadding(top: size);
													break;
												}

												case "bottom":
												{
													editPageData.editingElement.SetPadding(bottom: size);
													break;
												}
											}
											break;
										}

										case "step":
										{
											var step = arg.GetFloat(4);
											
											editPageData.SetMovementStep(step);
											break;
										}

										case "rect":
										{
											var fieldName = arg.GetString(4);
											if (string.IsNullOrEmpty(fieldName)) return;
									
											var targetField = editPageData.editingElement.GetType().GetField(fieldName);
											if (targetField == null)
												return;

											var targetValue = Convert.ToSingle(targetField.GetValue(editPageData.editingElement));
											
											var stepSize = 1f;
											if (targetField.Name.Contains("Anchor"))
												stepSize = 0.1f;

											switch (arg.GetString(5))
											{
												case "-":
												{
													targetValue -= stepSize;
													break;
												}
												case "+":
												{
													targetValue += stepSize;
													break;
												}
												
												default:
												{
													var newValue = arg.GetFloat(5);

													targetValue = newValue;
													break;
												}
											}
											
											targetField.SetValue(editPageData.editingElement, targetValue);
											break;
										}

										case "constraint":
										{
											switch (arg.GetString(4))
											{
												case "horizontal":
												{
													switch (arg.GetString(5))
													{
														case "prev":
														{
															pos.SetHorizontalAxis((InterfacePosition.HorizontalConstraint) pos.GetHorizontalAxis().Previous(InterfacePosition.HorizontalConstraint.Custom));
															break;
														}
														case "next":
														{
															pos.SetHorizontalAxis((InterfacePosition.HorizontalConstraint) pos.GetHorizontalAxis().Next(InterfacePosition.HorizontalConstraint.Custom));
															break;
														}
													}
													break;
												}

												case "vertical":
												{
													switch (arg.GetString(5))
													{
														case "prev":
														{
															pos.SetVerticalAxis((InterfacePosition.VerticalConstraint) pos.GetVerticalAxis().Previous(InterfacePosition.VerticalConstraint.Custom));
															break;
														}
														case "next":
														{
															pos.SetVerticalAxis((InterfacePosition.VerticalConstraint) pos.GetVerticalAxis().Next(InterfacePosition.VerticalConstraint.Custom));
															break;
														}
													}
													break;
												}
											}

											break;
										}
									}
									
									UpdateUI(player, container =>
									{ 
										PositionSectionUI(player, container, arg.GetString(0), pos);

										editPageData.UpdateEditElement(ref container, player, needUpdate: editPageData.editingElement.Type == CuiElementType.Label);
									});
									break;
								}
							}

							break;
						}
					}

					break;
				}

				case "edit_category":
				{
					if (!CanPlayerEdit(player) ||
					    !TryGetOpenedMenu(player.userID, out var openedMenu))
						return;
					
					switch (arg.GetString(1))
					{
						case "start":
						{
							var categoryID = arg.GetInt(2);

							var targetCategory = GetCategoryById(categoryID);
							if (targetCategory == null) return;
							
							EditCategoryData.Create(player, categoryID);

							ShowCategoryEditorPanel(player);
							break;
						}

						case "create":
						{
							EditCategoryData.Create(player, -1, needCreate: true);

							ShowCategoryEditorPanel(player);
							break;
						}

						case "close":
						{
							EditCategoryData.Remove(player.userID);
							break;
						}

						case "save":
						{
							var editCategoryData = EditCategoryData.Get(player.userID);
							if (editCategoryData == null) return;
							
							editCategoryData.Save();
							
							UpdateUI(player, container =>
							{
								_templateData?.ShowCategoriesScrollUI(player, container, false);
							});
							break;
						}
 
						case "remove": // remove category
						{
							var editCategoryData = EditCategoryData.Get(player.userID);
							if (editCategoryData == null) return;

							editCategoryData.Remove();

							UpdateUI(player, container =>
							{
								_templateData?.ShowCategoriesScrollUI(player, container, false);
							});
							break;
						}

						case "field":
						{
							var editCategoryData = EditCategoryData.Get(player.userID);
							if (editCategoryData == null) return;

							var fieldName = arg.GetString(2);

							var targetField = editCategoryData.menuCategory.GetType().GetField(fieldName);
							if (targetField == null)
								return;

							var parent = arg.GetString(3);
							if (string.IsNullOrEmpty(parent)) return;

							if (targetField.FieldType.IsEnum)
							{
								if (targetField.GetValue(editCategoryData.menuCategory) is not Enum nowEnum) return;

								Enum targetEnum = null;
								switch (arg.GetString(4))
								{
									case "prev":
									{
										targetEnum = nowEnum.Previous();
										break;
									}

									case "next":
									{
										targetEnum = nowEnum.Next();
										break;
									}
								}

								if (targetEnum == null) return;

								targetField.SetValue(editCategoryData.menuCategory, targetEnum);
							}
							else if (targetField.FieldType == typeof(List<string>))
							{
								var val = string.Join(" ", arg.Args.Skip(4));
								if (!string.IsNullOrEmpty(val))
								{
									var text = new List<string>();
									foreach (var line in val.Split('\n'))
									{
										text.Add(line);
									}

									targetField.SetValue(editCategoryData.menuCategory, text);
								}
							}
							else if (targetField.FieldType == typeof(string))
							{
								var val = string.Join(" ", arg.Args.Skip(4));
								if (!string.IsNullOrEmpty(val))
									targetField.SetValue(editCategoryData.menuCategory, val);
							}
							else
							{
								var newValue = string.Join(" ", arg.Args.Skip(4));

								try
								{
									var convertedValue = Convert.ChangeType(newValue, targetField.FieldType);
									targetField.SetValue(editCategoryData.menuCategory, convertedValue);
								}
								catch (Exception ex)
								{
									Puts($"Error setting property '{fieldName}': {ex.Message}");
									player.SendMessage($"Error setting property '{fieldName}': {ex.Message}");
									return;
								}
							}

							UpdateUI(player, container =>
							{
								CategoryEditorFieldUI(player, container, parent, targetField,
									targetField?.GetValue(editCategoryData.menuCategory));
							});
							break;
						}

						case "page":
						{
							switch (arg.GetString(2))
							{
								case "field":
								{
									var editCategoryData = EditCategoryData.Get(player.userID);
									if (editCategoryData == null) return;

									var pageIndex = arg.GetInt(3);

									var fieldName = arg.GetString(4);

									var categoryPage = editCategoryData.menuCategory.Pages[pageIndex];
									if (categoryPage == null) return;

									var targetField = categoryPage.GetType().GetField(fieldName);
									if (targetField == null)
										return;

									var parent = arg.GetString(5);
									if (string.IsNullOrEmpty(parent)) return;

									if (targetField.FieldType.IsEnum)
									{
										if (targetField.GetValue(categoryPage) is not Enum nowEnum)
											return;

										Enum targetEnum = null;
										switch (arg.GetString(6))
										{
											case "prev":
											{
												targetEnum = nowEnum.Previous();
												break;
											}

											case "next":
											{
												targetEnum = nowEnum.Next();
												break;
											}
										}

										if (targetEnum == null) return;

										targetField.SetValue(categoryPage, targetEnum);
									}
									else if (targetField.FieldType == typeof(List<string>))
									{
										var val = string.Join(" ", arg.Args.Skip(6));
										if (!string.IsNullOrEmpty(val))
										{
											var text = new List<string>();
											foreach (var line in val.Split('\n'))
											{
												text.Add(line);
											}

											targetField.SetValue(categoryPage, text);
										}
									}
									else if (targetField.FieldType == typeof(string))
									{
										var val = string.Join(" ", arg.Args.Skip(6));

										targetField.SetValue(categoryPage, val);
									}
									else
									{
										var newValue = string.Join(" ", arg.Args.Skip(6));

										try
										{
											var convertedValue = Convert.ChangeType(newValue, targetField.FieldType);
											targetField.SetValue(categoryPage, convertedValue);
										}
										catch (Exception ex)
										{
											Puts($"Error setting property '{fieldName}': {ex.Message}");
											player.SendMessage($"Error setting property '{fieldName}': {ex.Message}");
											return;
										}
									}

									UpdateUI(player, container =>
									{
										CategoryEditorPagesFieldUI(player, container, parent, targetField,
											targetField?.GetValue(categoryPage), pageIndex);
									});
									break;
								}
							}

							break;
						}

						case "array":
						{
							var editCategoryData = EditCategoryData.Get(player.userID);
							if (editCategoryData == null) return;

							switch (arg.GetString(2))
							{
								case "start":
								{
									var fieldName = arg.GetString(3);

									var targetField = editCategoryData.menuCategory.GetType().GetField(fieldName);
									if (targetField == null)
										return;

									editCategoryData.StartEditArray(
										targetField.GetValue(editCategoryData.menuCategory) as object[],
										targetField.Name);

									ShowCategoryArrayEditorModal(player);
									break;
								}

								case "close":
								{
									editCategoryData.StopEditArray();
									break;
								}

								case "add":
								{
									var targetField = editCategoryData.menuCategory.GetType()
										.GetField(editCategoryData.editableArrayName);
									if (targetField == null)
										return;

									var currentArray = editCategoryData.editableArray;
									var elementType = currentArray?.GetType().GetElementType();
									if (elementType == null) return;

									var newElementValue = (object) arg.GetString(3);

									var newLength = currentArray.Length + 1;
									var newArray = Array.CreateInstance(elementType, newLength);

									newArray.SetValue(newElementValue, 0);

									for (var i = 0; i < currentArray.Length; i++)
										newArray.SetValue(currentArray.GetValue(i), i + 1);

									targetField.SetValue(editCategoryData.menuCategory, newArray);

									editCategoryData.editableArray = newArray as object[];

									UpdateUI(player,
										container =>
										{
											CategoryArrayEditorLoopUI(editCategoryData.GetEditableArrayValues(),
												container);
										});
									break;
								}

								case "remove":
								{
									var targetField = editCategoryData.menuCategory.GetType()
										.GetField(editCategoryData.editableArrayName);
									if (targetField == null)
										return;

									var currentArray = editCategoryData.editableArray;
									var elementType = currentArray?.GetType().GetElementType();
									if (elementType == null) return;

									var indexToRemove = arg.GetInt(3, -1);
									if (indexToRemove < 0) return;

									var newLength = currentArray.Length - 1;

									var newArray = Array.CreateInstance(elementType, newLength);

									var j = 0;
									for (var i = 0; i < currentArray.Length; i++)
									{
										if (i != indexToRemove)
											newArray.SetValue(currentArray.GetValue(i), j++);
									}

									targetField.SetValue(editCategoryData.menuCategory, newArray);

									editCategoryData.editableArray = newArray as object[];

									CuiHelper.DestroyUi(player,
										EditingLayerModalArrayView + $".Command.{currentArray.Length - 1}");

									UpdateUI(player,
										container =>
										{
											CategoryArrayEditorLoopUI(editCategoryData.GetEditableArrayValues(),
												container);
										});
									break;
								}

								case "edit":
								{
									var targetField = editCategoryData.menuCategory.GetType()
										.GetField(editCategoryData.editableArrayName);
									if (targetField == null)
										return;

									var currentArray = editCategoryData.editableArray;
									var elementType = currentArray?.GetType().GetElementType();
									if (elementType == null) return;

									var indexToChange = arg.GetInt(3, -1);
									if (indexToChange < 0) return;

									object newElementValue = null;

									if (arg.Args.Length > 5) 
										newElementValue = (object) string.Join(" ", arg.Args.Skip(4));
									else
										newElementValue= (object) arg.GetString(4);
									
									currentArray.SetValue(newElementValue, indexToChange);

									targetField.SetValue(editCategoryData.menuCategory, currentArray);

									editCategoryData.editableArray = currentArray;

									UpdateUI(player,
										container =>
										{
											CategoryArrayEditorLoopUI(editCategoryData.GetEditableArrayValues(),
												container);
										});
									break;
								}
							}

							break;
						}
					}

					break;
				}

				case "edit_header_fields":
				{
					if (!CanPlayerEdit(player))
						return;

					switch (arg.GetString(1))
					{
						case "start":
						{
							EditHeaderFieldsData.Create(player);

							ShowHeaderFieldsEditorPanel(player);
							break;
						}

						case "save":
						{
							var editData = EditHeaderFieldsData.Get(player.userID);

							editData?.Save();
							break;
						}

						case "change_position":
						{
							var editPageData = EditHeaderFieldsData.Get(player.userID);

							API_OnServerPanelEditorChangePosition(player);
							
							editPageData?.OnChangePosition();
							break;
						}

						case "element":
						{
							var editData = EditHeaderFieldsData.Get(player.userID);

							switch (arg.GetString(2))
							{
								case "edit":
								{
									var elementIndex = arg.GetInt(3);

									if (!editData.StartEditElement(elementIndex, LayerHeader))
										return;

									EditUiElementData.Create(player,
										editData.elementIndex,
										editData.OnEditElementSave,
										editData.OnEditElementStartEdit,
										editData.OnEditElementStopEdit,
										editData.OnStartTextEditing,
										editData.OnStopTextEditing,
										editData.OnChangePosition);

									ShowElementEditorPanel(player);
									break;
								}

								case "add":
								{
									editData.HeaderFields.Add(new HeaderFieldUI(UiElement.CreatePanel(
										InterfacePosition.CreatePosition(0.5f, 0.5f, 0.5f, 0.5f, -50, -50, 50, 50),
										new IColor("#FFFFFF", 100))));

									SaveData();

									if (TryGetOpenedMenu(player.userID, out var openedMenu))
										openedMenu.UpdateContent();

									ShowPageEditorPanel(player);
									break;
								}

								case "remove":
								{
									if (!arg.HasArgs(3)) return;

									editData.HeaderFields.RemoveAt(arg.GetInt(3));

									SaveData();

									if (TryGetOpenedMenu(player.userID, out var openedMenu))
										openedMenu.UpdateContent();

									ShowPageEditorPanel(player);
									break;
								}

								case "move":
								{
									if (!arg.HasArgs(4)) return;

									switch (arg.GetString(4))
									{
										case "up":
										{
											editData.HeaderFields.MoveUp(arg.GetInt(3));
											break;
										}

										case "down":
										{
											editData.HeaderFields.MoveDown(arg.GetInt(3));
											break;
										}
									}

									SaveData();

									if (TryGetOpenedMenu(player.userID, out var openedMenu))
										openedMenu.UpdateContent();

									ShowPageEditorPanel(player);
									break;
								}
								
								case "clone":
								{
									if (!arg.HasArgs(3)) return;

									var elementIndex = arg.GetInt(3);
									if (elementIndex < 0 || elementIndex >= editData.HeaderFields.Count) return;

									var originalElement = editData.HeaderFields[elementIndex];
									var clonedElement = new HeaderFieldUI(originalElement);

									var originalName = originalElement.Name;
									var newName = originalName;
									var counter = 1;
									while (editData.HeaderFields.Any(e => e.Name == newName))
									{
										newName = $"{originalName} ({counter})";
										counter++;
									}
									clonedElement.Name = newName;

									editData.HeaderFields.Add(clonedElement);

									SaveData();

									if (TryGetOpenedMenu(player.userID, out var openedMenu))
										openedMenu.UpdateContent();

									ShowPageEditorPanel(player);
									break;
								}
								
								case "switch_show":
								{
									if (!arg.HasArgs(3)) return;

									var elementIndex = arg.GetInt(3);
									if (elementIndex < 0 || elementIndex >= editData.HeaderFields.Count) return;

									var element = editData.HeaderFields[elementIndex];
									element.Visible = !element.Visible;
											
									UpdateUI(player, container =>
									{
										element.Get(ref container, 
											player,
											LayerHeader,
											element.Name, element.Name, needUpdate: true,
											textFormatter: text => Instance.FormatUpdateField(player, text));
									});
									
									SaveConfig();
									
									UpdateUI(player, container =>
									{
										UpdatePointPageEditorUI(container, 
											elementIndex,
											element,
											string.Join(" ", arg.Args.SkipLast(1)));
									});
									break;
								}
							}

							break;
						}
					}

					break;
				}

				case "edit_menu":
				{
					if (!CanPlayerEdit(player) ||
					    !TryGetOpenedMenu(player.userID, out var openedMenu))
						return;

					switch (arg.GetString(1))
					{
						case "change_mode":
						{
							openedMenu.OnChangeEditMode();

							ShowMenuUI(player);
							break;
						}

						case "category_create":
						{
							break;
						}

						case "category":
						{
							var targetCategoryID = arg.GetInt(3);
							var menuCategory = GetCategoryById(targetCategoryID);
							if (menuCategory == null) return;

							switch (arg.GetString(2))
							{
								case "up":
								{
									menuCategory.MoveUp();
									break;
								}

								case "down":
								{
									menuCategory.MoveDown();
									break;
								}
							}

							LoadCategories();

							UpdateUI(player, container => { _templateData?.ShowCategoriesLoopUI(player, container); });
							break;
						}
					}

					break;
				}
			}
		}

		#endregion Commands

		#region Interface

		#region Main Panel

		private void ShowMenuUI(BasePlayer player)
		{
			UpdateUI(player, container =>
			{
				ShowBackground(player, container);

				ShowNavigation(player, container);

				ShowHeader(player, container);

				ShowContent(player, container);

				ShowCloseButton(player, container);
			});
		}

		private void ShowBackground(BasePlayer player, CuiElementContainer container)
		{
			_templateData.ShowBackgroundUI(player, container,
				cmdOnClick: $"{CmdMainConsole} close");
		}

		private void ShowNavigation(BasePlayer player, CuiElementContainer container)
		{
			_templateData.ShowCategories(player, container);
		}

		private void ShowHeader(BasePlayer player, CuiElementContainer container)
		{
			_templateData.ShowHeaderUI(player, container);
		}

		private void ShowContent(BasePlayer player, CuiElementContainer container)
		{
			_templateData.ShowContentUI(player, container, $"{CmdMainConsole} menu page");
		}

		private void ShowCloseButton(BasePlayer player, CuiElementContainer container)
		{
			_templateData.ShowCloseButtonUI(player, container, Layer, Layer, $"{CmdMainConsole} close");
		}

		#endregion Main Panel

		#region Editor Panel

		private void ShowPageEditorPanel(BasePlayer player)
		{
			var container = new CuiElementContainer();

			var editData = EditPageData.Get(player.userID);
			var playerData = PlayerData.GetOrCreate(player.UserIDString);

			#region Background

			container.Add(new CuiElement()
			{
				Parent = Layer,
				Name = EditingLayerPageEditor,
				DestroyUi = EditingLayerPageEditor,
				Components =
				{
					new CuiImageComponent(){Color = "0 0 0 0"},
					playerData.GetEditorPosition()
				}
			});

			container.Add(new CuiPanel()
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image =
				{
					Color = "0 0 0 0.9",
					Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
				},
			}, EditingLayerPageEditor);

			#region Title

			container.Add(new CuiElement
			{
				Parent = EditingLayerPageEditor,
				Components =
				{
					new CuiTextComponent
					{
						Text = "CUI ELEMENT\nSELECTION",
						Font = "robotocondensed-bold.ttf",
						FontSize = 30,
						Align = TextAnchor.MiddleLeft,
						Color = HexToCuiColor("#E2DBD3", 90)
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "40 -145", OffsetMax = "0 0"
					}
				}
			});

			#endregion Title

			ShowCloseButtonUI(container, EditingLayerPageEditor, EditingLayerPageEditor + ".CloseButton",
				EditingLayerPageEditor,
				$"{CmdMainConsole} edit_page save");

			ShowCloseButtonUI(container, EditingLayerPageEditor,
				EditingLayerPageEditor + ".ChangePosition",
				EditingLayerPageEditor,
				commandOnClose: $"{CmdMainConsole} edit_page change_position",
				closeButtonOffsetMin: "-80 -40",
				closeButtonOffsetMax: "-40 0",
				backgroundColor: HexToCuiColor("#71B8ED", 20),
				iconSprite: "assets/icons/arrow_right.png",
				iconColor: HexToCuiColor("#71B8ED"));

			#endregion Background

			#region Selection

			#region Scroll View

			var offsetY = 0f;
			var fieldHeight = 40f;
			var fieldMarginY = 10f;

			var elements = editData.categoryPage.Elements;

			var totalHeight = elements.Count * fieldHeight + (elements.Count - 1) * fieldMarginY;

			totalHeight += 2f + fieldMarginY + fieldHeight;

			totalHeight = Mathf.Max(510, totalHeight);

			container.Add(new CuiElement()
			{
				Parent = EditingLayerPageEditor,
				Name = EditingLayerPageEditor + ".Selection",
				DestroyUi = EditingLayerPageEditor + ".Selection",
				Components =
				{
					new CuiScrollViewComponent
					{
						MovementType = ScrollRect.MovementType.Elastic,
						Vertical = true,
						Inertia = true,
						Horizontal = false,
						Elasticity = 0.25f,
						DecelerationRate = 0.3f,
						ScrollSensitivity = 24f,
						ContentTransform = new CuiRectTransform
						{
							AnchorMin = "0 1",
							AnchorMax = "1 1",
							OffsetMin = $"0 -{totalHeight}",
							OffsetMax = "0 0"
						},
						VerticalScrollbar = new CuiScrollbar()
						{
							Size = 5f, AutoHide = false,
							HandleColor = HexToCuiColor("#D74933"),
						},
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 55", OffsetMax = "-10 -145"
					}
				}
			});

			#endregion Scroll View

			#region UI Elements

			foreach (var cuiElement in elements)
			{
				var elementIndex = editData.categoryPage.Elements.IndexOf(cuiElement);

				container.Add(new CuiPanel
					{
						Image = {Color = "0.3019608 0.3019608 0.3019608 0.4"},
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"10 {offsetY - fieldHeight}",
							OffsetMax = $"-20 {offsetY}"
						}
					}, EditingLayerPageEditor + ".Selection",
					EditingLayerPageEditor + $".Selection.Element.{elementIndex}",
					EditingLayerPageEditor + $".Selection.Element.{elementIndex}");

				PageEditorFieldUI(container, elementIndex, cuiElement,
					cmdRemove: "edit_page element remove",
					cmdClone: "edit_page element clone",
					cmdSwitch: "edit_page element switch_show",
					cmdEdit: "edit_page element edit",
					cmdMove: "edit_page element move");

				offsetY = offsetY - fieldHeight - fieldMarginY;
			}

			if (elements.Count > 0)
			{
				container.Add(new CuiPanel()
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = $"10 {offsetY - 2f}",
						OffsetMax = $"-20 {offsetY}"
					},
					Image =
					{
						Color = HexToCuiColor("#E2DBD3", 20)
					}
				}, EditingLayerPageEditor + ".Selection");

				offsetY = offsetY - 2f - fieldMarginY;
			}

			container.Add(new CuiButton()
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = $"10 {offsetY - fieldHeight}",
					OffsetMax = $"-20 {offsetY}"
				},
				Text =
				{
					Text = "+ ADD NEW LAYER",
					Font = "robotocondensed-bold.ttf", FontSize = 22,
					Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#CF432D", 90)
				},
				Button =
				{
					Color = "0 0 0 0",
					Command = $"{CmdMainConsole} edit_page element add"
				}
			}, EditingLayerPageEditor + ".Selection");

			#endregion UI Elements

			#endregion Selection

			CuiHelper.AddUi(player, container);
		}

		private void ShowHeaderFieldsEditorPanel(BasePlayer player)
		{
			var container = new CuiElementContainer();

			var editData = EditHeaderFieldsData.Get(player.userID);
			var playerData = PlayerData.GetOrCreate(player.UserIDString);

			#region Background

			container.Add(new CuiElement()
			{
				Parent = Layer,
				Name = EditingLayerPageEditor,
				DestroyUi = EditingLayerPageEditor,
				Components =
				{
					new CuiImageComponent(){Color = "0 0 0 0"},
					playerData.GetEditorPosition()
				}
			});

			container.Add(new CuiPanel()
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image =
				{
					Color = "0 0 0 0.9",
					Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
				},
			}, EditingLayerPageEditor);

			#region Title

			container.Add(new CuiElement
			{
				Parent = EditingLayerPageEditor,
				Components =
				{
					new CuiTextComponent
					{
						Text = "CUI ELEMENT\nSELECTION",
						Font = "robotocondensed-bold.ttf",
						FontSize = 30,
						Align = TextAnchor.MiddleLeft,
						Color = HexToCuiColor("#E2DBD3", 90)
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "40 -145", OffsetMax = "0 0"
					}
				}
			});

			#endregion Title

			ShowCloseButtonUI(container, EditingLayerPageEditor, EditingLayerPageEditor + ".CloseButton",
				EditingLayerPageEditor,
				$"{CmdMainConsole} edit_header_fields save");

			ShowCloseButtonUI(container, EditingLayerPageEditor,
				EditingLayerPageEditor + ".ChangePosition",
				EditingLayerPageEditor,
				commandOnClose: $"{CmdMainConsole} edit_header_fields change_position",
				closeButtonOffsetMin: "-80 -40",
				closeButtonOffsetMax: "-40 0",
				backgroundColor: HexToCuiColor("#71B8ED", 20),
				iconSprite: "assets/icons/arrow_right.png",
				iconColor: HexToCuiColor("#71B8ED"));

			#endregion Background

			#region Selection

			#region Scroll View

			var scrollRect = new CuiRectTransform
			{
				AnchorMin = "0 1",
				AnchorMax = "1 1",
				OffsetMin = "0 -{PAGE_EDITOR_SCROLL_SIZE}",
				OffsetMax = "0 0"
			};
			container.Add(new CuiElement()
			{
				Parent = EditingLayerPageEditor,
				Name = EditingLayerPageEditor + ".Selection",
				DestroyUi = EditingLayerPageEditor + ".Selection",
				Components =
				{
					new CuiScrollViewComponent
					{
						MovementType = ScrollRect.MovementType.Elastic,
						Vertical = true,
						Inertia = true,
						Horizontal = false,
						Elasticity = 0.25f,
						DecelerationRate = 0.3f,
						ScrollSensitivity = 24f,
						ContentTransform = scrollRect,
						VerticalScrollbar = new CuiScrollbar()
						{
							Size = 5f, AutoHide = false,
							HandleColor = HexToCuiColor("#D74933"),
						},
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 55", OffsetMax = "-10 -145"
					}
				}
			});

			#endregion Scroll View

			#region UI Elements

			var offsetY = 0f;
			var fieldHeight = 40f;
			var fieldMarginY = 10f;

			var elements = editData.HeaderFields;

			TitleEditorUI(container, EditingLayerPageEditor + ".Selection", ref offsetY, "HEADER FIELDS",
				margin: fieldMarginY);

			foreach (var cuiElement in elements)
			{
				var elementIndex = editData.HeaderFields.IndexOf(cuiElement);

				container.Add(new CuiPanel
					{
						Image = {Color = "0.3019608 0.3019608 0.3019608 0.4"},
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"10 {offsetY - fieldHeight}",
							OffsetMax = $"-20 {offsetY}"
						}
					}, EditingLayerPageEditor + ".Selection",
					EditingLayerPageEditor + $".Selection.Element.{elementIndex}",
					EditingLayerPageEditor + $".Selection.Element.{elementIndex}");

				PageEditorFieldUI(container, elementIndex, cuiElement,
					cmdRemove: "edit_header_fields element remove",
					cmdClone: "edit_header_fields element clone",
					cmdSwitch: "edit_header_fields element switch_show",
					cmdEdit: "edit_header_fields element edit",
					cmdMove: "edit_header_fields element move");

				offsetY = offsetY - fieldHeight - fieldMarginY;
			}

			if (elements.Count > 0)
			{
				container.Add(new CuiPanel()
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = $"10 {offsetY - 2f}",
						OffsetMax = $"-20 {offsetY}"
					},
					Image =
					{
						Color = HexToCuiColor("#E2DBD3", 20)
					}
				}, EditingLayerPageEditor + ".Selection");

				offsetY = offsetY - 2f - fieldMarginY;
			}

			container.Add(new CuiButton()
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = $"10 {offsetY - fieldHeight}",
					OffsetMax = $"-20 {offsetY}"
				},
				Text =
				{
					Text = "+ ADD NEW LAYER",
					Font = "robotocondensed-bold.ttf", FontSize = 22,
					Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#CF432D", 90)
				},
				Button =
				{
					Color = "0 0 0 0",
					Command = $"{CmdMainConsole} edit_header_fields element add"
				}
			}, EditingLayerPageEditor + ".Selection");

			#endregion UI Elements

			scrollRect.OffsetMin = $"0 -{(Mathf.Abs(offsetY) + 100):N}";

			#endregion Selection

			CuiHelper.AddUi(player, container);
		}

		private void ShowElementEditorPanel(BasePlayer player)
		{
			var container = new CuiElementContainer();

			var editData = EditUiElementData.Get(player.userID);
			var playerData = PlayerData.GetOrCreate(player.UserIDString);

			var targetElement = editData.editingElement;

			#region Background

			container.Add(new CuiElement()
			{
				Parent = Layer,
				Name = EditingLayerElementEditor,
				DestroyUi = EditingLayerElementEditor,
				Components =
				{
					new CuiImageComponent(){Color = "0 0 0 0"},
					playerData.GetEditorPosition()
				}
			});
			
			container.Add(new CuiPanel()
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image =
				{
					Color = "0 0 0 0.95",
					Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
				},
			}, EditingLayerElementEditor);

			#region Title

			container.Add(new CuiElement
			{
				Parent = EditingLayerElementEditor,
				Components =
				{
					new CuiTextComponent
					{
						Text = "UI EDITOR",
						Font = "robotocondensed-bold.ttf",
						FontSize = 30,
						Align = TextAnchor.MiddleLeft,
						Color = HexToCuiColor("#E2DBD3", 90)
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "40 -145", OffsetMax = "0 0"
					}
				}
			});

			#endregion Title

			ShowCloseButtonUI(container, EditingLayerElementEditor,
				EditingLayerElementEditor + ".CloseButton",
				EditingLayerElementEditor,
				commandOnClose: $"{CmdMainConsole} edit_element save");

			ShowCloseButtonUI(container, EditingLayerElementEditor,
				EditingLayerElementEditor + ".ChangePosition",
				EditingLayerElementEditor,
				commandOnClose: $"{CmdMainConsole} edit_element change_position",
				closeButtonOffsetMin: "-80 -40",
				closeButtonOffsetMax: "-40 0",
				backgroundColor: HexToCuiColor("#71B8ED", 20),
				iconSprite: "assets/icons/arrow_right.png",
				iconColor: HexToCuiColor("#71B8ED"));

			#endregion Background

			#region Selection

			#region Scroll View

			var offsetY = 0f;
			var fieldHeight = 40f;
			var fieldMarginY = 10f;

			var scrollRect = new CuiRectTransform
			{
				AnchorMin = "0 1",
				AnchorMax = "1 1",
				OffsetMin = "0 %TOTAL_SCROLL_HEIGHT%",
				OffsetMax = "0 0"
			};
			container.Add(new CuiElement()
			{
				Parent = EditingLayerElementEditor,
				Name = EditingLayerElementEditor + ".Selection",
				DestroyUi = EditingLayerElementEditor + ".Selection",
				Components =
				{
					new CuiScrollViewComponent
					{
						MovementType = ScrollRect.MovementType.Elastic,
						Vertical = true,
						Inertia = true,
						Horizontal = false,
						Elasticity = 0.25f,
						DecelerationRate = 0.3f,
						ScrollSensitivity = 24f,
						ContentTransform = scrollRect,
						VerticalScrollbar = new CuiScrollbar()
						{
							Size = 5f, AutoHide = false,
							HandleColor = HexToCuiColor("#D74933"),
						},
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 55", OffsetMax = "-10 -145"
					}
				}
			});

			#endregion Scroll View

			#region Enabled Section

			var enabledField = targetElement.GetType().GetField("Enabled");
			var enabledLayer = CuiHelper.GetGuid();

			container.Add(new CuiPanel
			{
				Image = {Color = "0 0 0 0"},
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = $"20 {offsetY - fieldHeight}",
					OffsetMax = $"-20 {offsetY}"
				}
			}, EditingLayerElementEditor + ".Selection", enabledLayer + ".Background", enabledLayer + ".Background");

			FieldElementUI(container, enabledLayer, enabledField, enabledField?.GetValue(targetElement));

			offsetY = offsetY - fieldHeight - fieldMarginY;

			#endregion Enabled Section

			#region Type Section

			var typeField = targetElement.GetType().GetField("Type");
			var typeLayer = CuiHelper.GetGuid();

			container.Add(new CuiPanel
			{
				Image = {Color = "0 0 0 0"},
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = $"20 {offsetY - fieldHeight}",
					OffsetMax = $"-20 {offsetY}"
				}
			}, EditingLayerElementEditor + ".Selection", typeLayer + ".Background", typeLayer + ".Background");

			FieldElementUI(container, typeLayer, typeField, typeField?.GetValue(targetElement));

			offsetY = offsetY - fieldHeight - fieldMarginY;

			#endregion Type Section

			#region Name Section

			var nameField = targetElement.GetType().GetField("Name");
			var nameLayer = CuiHelper.GetGuid();

			container.Add(new CuiPanel
			{
				Image = {Color = "0 0 0 0"},
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = $"20 {offsetY - fieldHeight}",
					OffsetMax = $"-20 {offsetY}"
				}
			}, EditingLayerElementEditor + ".Selection", nameLayer + ".Background", nameLayer + ".Background");

			FieldElementUI(container, nameLayer, nameField, nameField?.GetValue(targetElement));

			offsetY = offsetY - fieldHeight - fieldMarginY;

			#endregion Name Section

			#region Rect Transform Section

			TitleEditorUI(container, EditingLayerElementEditor + ".Selection", ref offsetY, "RECT TRANSFORM",
				margin: fieldMarginY);

			if (targetElement is InterfacePosition interfacePos)
			{
				#region Section.Position
				
				container.Add(new CuiPanel
				{
					Image = {Color = "0 0 0 0"},
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = $"20 {offsetY - 270}",
						OffsetMax = $"-20 {offsetY}"
					}
				}, EditingLayerElementEditor + ".Selection", EditingLayerElementEditor + ".Selection.RectTransform.Section.Position.Background", EditingLayerElementEditor + ".Selection.RectTransform.Section.Position.Background");

				PositionSectionUI(player, container, "edit_element", interfacePos);

				offsetY = offsetY - 270 - fieldMarginY;

				#endregion
			}

			#endregion Rect Transform Section

			#region Label Section

			if (targetElement.Type is CuiElementType.Label or CuiElementType.InputField)
			{
				TitleEditorUI(container, EditingLayerElementEditor + ".Selection", ref offsetY, "TEXT STYLE",
					margin: fieldMarginY);

				container.Add(new CuiButton()
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = $"20 {offsetY - 40f}",
						OffsetMax = $"-20 {offsetY}"
					},
					Text =
					{
						Text = "EDIT TEXT",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 20,
						Color = HexToCuiColor("#E2DBD3")
					},
					Button =
					{
						Color = HexToCuiColor("#4D4D4D", 50),
						Command = $"{CmdMainConsole} edit_element text start"
					}
				}, EditingLayerElementEditor + ".Selection");

				offsetY = offsetY - 40f;
			}

			#endregion Label Section

			#region Panel Section

			if (targetElement.Type == CuiElementType.Panel)
			{
				TitleEditorUI(container, EditingLayerElementEditor + ".Selection", ref offsetY, "PANEL STYLE",
					margin: fieldMarginY);

				var targetFields = new List<FieldInfo>
				{
					targetElement.GetType().GetField("Color")
				};

				foreach (var panelField in targetFields)
				{
					var targetFieldLayer = CuiHelper.GetGuid();

					container.Add(new CuiPanel
						{
							Image = {Color = "0 0 0 0"},
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "1 1",
								OffsetMin = $"20 {offsetY - fieldHeight}",
								OffsetMax = $"-20 {offsetY}"
							}
						}, EditingLayerElementEditor + ".Selection", targetFieldLayer + ".Background",
						targetFieldLayer + ".Background");

					FieldElementUI(container, targetFieldLayer, panelField, panelField.GetValue(targetElement));

					offsetY = offsetY - fieldHeight - fieldMarginY;
				}
			}

			#endregion Panel Section

			#region Image Section

			if (targetElement.Type == CuiElementType.Image)
			{
				TitleEditorUI(container, EditingLayerElementEditor + ".Selection", ref offsetY, "IMAGE STYLE",
					margin: fieldMarginY);

				var targetFields = new List<FieldInfo>
				{
					targetElement.GetType().GetField("Color"),
					targetElement.GetType().GetField("Image")
				};

				foreach (var textField in targetFields)
				{
					var targetFieldLayer = CuiHelper.GetGuid();

					container.Add(new CuiPanel
						{
							Image = {Color = "0 0 0 0"},
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "1 1",
								OffsetMin = $"20 {offsetY - fieldHeight}",
								OffsetMax = $"-20 {offsetY}"
							}
						}, EditingLayerElementEditor + ".Selection", targetFieldLayer + ".Background",
						targetFieldLayer + ".Background");

					FieldElementUI(container, targetFieldLayer, textField, textField.GetValue(targetElement));

					offsetY = offsetY - fieldHeight - fieldMarginY;
				}
			}

			#endregion Image Section

			#region Button Section

			if (targetElement.Type == CuiElementType.Button)
			{
				#region Text

				TitleEditorUI(container, EditingLayerElementEditor + ".Selection", ref offsetY, "TEXT SETTINGS",
					margin: fieldMarginY);

				container.Add(new CuiButton()
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = $"20 {offsetY - 40f}",
						OffsetMax = $"-20 {offsetY}"
					},
					Text =
					{
						Text = "EDIT TEXT",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 20,
						Color = HexToCuiColor("#E2DBD3")
					},
					Button =
					{
						Color = HexToCuiColor("#4D4D4D", 50),
						Command = $"{CmdMainConsole} edit_element text start"
					}
				}, EditingLayerElementEditor + ".Selection");

				offsetY = offsetY - 40f;

				#endregion

				offsetY = offsetY - 40f;
                
				#region Panel

				TitleEditorUI(container, EditingLayerElementEditor + ".Selection", ref offsetY, "BUTTON STYLE",
					margin: fieldMarginY);

				var targetFields = new List<FieldInfo>
				{
					targetElement.GetType().GetField("Command"),
					targetElement.GetType().GetField("Color"),
					targetElement.GetType().GetField("Sprite"),
					targetElement.GetType().GetField("Material"),
				};

				foreach (var textField in targetFields)
				{
					var targetFieldLayer = CuiHelper.GetGuid();

					container.Add(new CuiPanel
						{
							Image = {Color = "0 0 0 0"},
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "1 1",
								OffsetMin = $"20 {offsetY - fieldHeight}",
								OffsetMax = $"-20 {offsetY}"
							}
						}, EditingLayerElementEditor + ".Selection", targetFieldLayer + ".Background",
						targetFieldLayer + ".Background");

					FieldElementUI(container, targetFieldLayer, textField, textField.GetValue(targetElement));

					offsetY = offsetY - fieldHeight - fieldMarginY;
				}

				#endregion
			}

			#endregion
            
			scrollRect.OffsetMin = $"0 {offsetY:N}";
			
			#endregion Selection

			CuiHelper.AddUi(player, container);
		}

		#region Editor Selection Panels

		private void ShowColorSelectionPanel(BasePlayer player, string fieldName, string parentLayer)
		{
			var editPageData = EditUiElementData.Get(player.userID);

			var targetField = editPageData.editingElement.GetType().GetField(fieldName);
			if (targetField == null || targetField.GetValue(editPageData.editingElement) is not IColor selectedColor)
				return;

			var container = new CuiElementContainer();

			#region Background

			var bgLayer = container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1"
				},
				Image =
				{
					Color = HexToCuiColor("#000000", 98)
				}
			}, "Overlay", EditingLayerModalColorSelector, EditingLayerModalColorSelector);

			#endregion

			#region Main

			var mainLayer = container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-240 -260",
					OffsetMax = "240 260"
				},
				Image =
				{
					Color = HexToCuiColor("#202224")
				}
			}, bgLayer, EditingLayerModalColorSelector + ".Main", EditingLayerModalColorSelector + ".Main");

			#region Header

			#region Title

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 0",
					OffsetMax = "0 40"
				},
				Text =
				{
					Text = Msg(player, "COLOR PICKER"),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 22,
					Color = HexToCuiColor("#DCDCDC")
				}
			}, mainLayer);

			#endregion

			#region Close

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-30 5",
					OffsetMax = "0 35"
				},
				Text =
				{
					Text = Msg(player, "X"),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 22,
					Color = HexToCuiColor("#EF5125")
				},
				Button =
				{
					Color = "0 0 0 0",
					Close = bgLayer,
					Command = $"{CmdMainConsole} edit_element color close",
				}
			}, mainLayer, mainLayer + ".BTN.Close.Edit");

			#endregion

			#endregion

			#region Colors

			var topRightColor = Color.blue;
			var bottomRightColor = Color.green;
			var topLeftColor = Color.red;
			var bottomLeftColor = Color.yellow;

			var scale = 20f;
			var total = scale * 2 - 8f;

			var width = 20f;
			var height = 20f;

			var constSwitchX = -((int) scale * width) / 2f;
			var xSwitch = constSwitchX;
			var ySwitch = -20f;

			for (var y = 0f; y < scale; y += 1f)
			{
				var heightColor = Color.Lerp(topRightColor, bottomRightColor, y.Scale(0f, scale, 0f, 1f));

				for (float x = 0; x < scale; x += 1f)
				{
					var widthColor = Color.Lerp(topLeftColor, bottomLeftColor, (x + y).Scale(0f, total, 0f, 1f));
					var targetColor = Color.Lerp(widthColor, heightColor, x.Scale(0f, scale, 0f, 1f)) * 1f;

					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = $"{xSwitch} {ySwitch - height}",
							OffsetMax = $"{xSwitch + width} {ySwitch}"
						},
						Text = {Text = string.Empty},
						Button =
						{
							Color = $"{targetColor.r} {targetColor.g} {targetColor.b} 1",
							Command =
								$"{CmdMainConsole} edit_element color set {fieldName} {parentLayer} hex {ColorUtility.ToHtmlStringRGB(targetColor)}"
						}
					}, mainLayer);

					xSwitch += width;
				}

				xSwitch = constSwitchX;
				ySwitch -= height;
			}

			#endregion

			#region Selected Color

			if (selectedColor != null)
			{
				#region Show Color

				container.Add(new CuiElement
				{
					Name = mainLayer + ".Selected.Color",
					Parent = mainLayer,
					Components =
					{
						new CuiImageComponent
						{
							Color = selectedColor.GetNotCachedColor()
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.5 0",
							AnchorMax = "0.5 0",
							OffsetMin = $"{constSwitchX} 30",
							OffsetMax = $"{constSwitchX + 100f} 60"
						},
						new CuiOutlineComponent
						{
							Color = HexToCuiColor("#575757"),
							Distance = "3 -3",
							UseGraphicAlpha = true
						}
					}
				});

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1",
						AnchorMax = "1 1",
						OffsetMin = "0 0",
						OffsetMax = "0 25"
					},
					Text =
					{
						Text = Msg(player, "Selected color:"),
						Align = TextAnchor.UpperLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 14,
						Color = "1 1 1 1"
					}
				}, mainLayer + ".Selected.Color");

				#endregion

				#region Input

				#region HEX

				container.Add(new CuiElement
				{
					Name = mainLayer + ".Selected.Color.Input.HEX",
					Parent = mainLayer,
					Components =
					{
						new CuiImageComponent
						{
							Color = HexToCuiColor("#2F3134")
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0",
							OffsetMin = $"{Mathf.Abs(constSwitchX) - 180} 30",
							OffsetMax = $"{Mathf.Abs(constSwitchX) - 100} 60"
						},
						new CuiOutlineComponent
						{
							Color = HexToCuiColor("#575757"),
							Distance = "1 -1"
						}
					}
				});

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1",
						AnchorMax = "1 1",
						OffsetMin = "0 0",
						OffsetMax = "0 20"
					},
					Text =
					{
						Text = Msg(player, "HEX"),
						Align = TextAnchor.UpperLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = HexToCuiColor("#FFFFFF")
					}
				}, mainLayer + ".Selected.Color.Input.HEX");

				container.Add(new CuiElement
				{
					Parent = mainLayer + ".Selected.Color.Input.HEX",
					Components =
					{
						new CuiInputFieldComponent
						{
							FontSize = 10,
							Align = TextAnchor.MiddleCenter,
							Command = $"{CmdMainConsole} edit_element color set {fieldName} {parentLayer} hex",
							Color = HexToCuiColor("#575757"),
							CharsLimit = 150,
							Text = $"{selectedColor.Hex}",
							NeedsKeyboard = true
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "0 0", OffsetMax = "0 0"
						}
					}
				});

				#endregion

				#region Opacity

				container.Add(new CuiElement
				{
					Name = mainLayer + ".Selected.Color.Input.Opacity",
					Parent = mainLayer,
					Components =
					{
						new CuiImageComponent
						{
							Color = HexToCuiColor("#2F3134")
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0",
							OffsetMin = $"{Mathf.Abs(constSwitchX) - 90} 30",
							OffsetMax = $"{Mathf.Abs(constSwitchX)} 60"
						},
						new CuiOutlineComponent
						{
							Color = HexToCuiColor("#575757"),
							Distance = "1 -1"
						}
					}
				});

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1",
						AnchorMax = "1 1",
						OffsetMin = "0 0",
						OffsetMax = "0 20"
					},
					Text =
					{
						Text = Msg(player, "Opacity (0-100)"),
						Align = TextAnchor.UpperLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = HexToCuiColor("#FFFFFF")
					}
				}, mainLayer + ".Selected.Color.Input.Opacity");

				container.Add(new CuiElement
				{
					Parent = mainLayer + ".Selected.Color.Input.Opacity",
					Components =
					{
						new CuiInputFieldComponent
						{
							FontSize = 10,
							Align = TextAnchor.MiddleCenter,
							Command = $"{CmdMainConsole} edit_element color set {fieldName} {parentLayer} opacity",
							Color = HexToCuiColor("#575757"),
							CharsLimit = 150,
							Text = $"{selectedColor.Alpha}",
							NeedsKeyboard = true
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "0 0", OffsetMax = "0 0"
						}
					}
				});

				#endregion

				#endregion
			}

			#endregion

			#endregion

			CuiHelper.AddUi(player, container);
		}

		#region Text Editor

		private const float
			UI_TextEditor_Lines_Width = 620f,
			UI_TextEditor_TextStyle_Height = 45f,
			UI_TextEditor_TextStyle_Margin_Y = 5f,
			UI_TextEditor_Lines_Margin_Y = 0f,
			UI_TextEditor_Lang_Margin_Y = 4f,
			UI_TextEditor_Lang_Height = 26f;

		private void ShowTextEditorPanel(BasePlayer player)
		{
			var elementData = EditUiElementData.Get(player.userID);

			var container = new CuiElementContainer();

			#region Background

			var bgLayer = container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1"
				},
				Image =
				{
					Color = HexToCuiColor("#000000", 98)
				},
				CursorEnabled = true
			}, "Overlay", EditingLayerModalTextEditor, EditingLayerModalTextEditor);

			#endregion

			#region Main

			var mainLayer = container.Add(new CuiPanel
			{
				RectTransform =
					{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-640 -360", OffsetMax = "640 360"},
				Image =
				{
					Color = HexToCuiColor("#202224")
				}
			}, bgLayer, EditingLayerModalTextEditor + ".Main", EditingLayerModalTextEditor + ".Main");

			#region Header

			#region Title

			container.Add(new CuiLabel
			{
				RectTransform =
					{AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "40 -75", OffsetMax = "0 -25"},
				Text =
				{
					Text = Msg(player, "TEXT EDITOR"),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 32,
					Color = HexToCuiColor("#E2DBD3", 90)
				}
			}, mainLayer);

			#endregion

			#region Close

			ShowTextEditorCloseButton(container, $"{CmdMainConsole} edit_element text pre_close");
			
			#endregion

			#endregion

			#region Select Lang

			#region Fields

			var totalHeight = _langList.Count * UI_TextEditor_Lang_Height +
			                  (_langList.Count - 1) * UI_TextEditor_Lang_Margin_Y;

			#endregion

			container.Add(new CuiPanel
				{
					RectTransform =
						{AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "20 -585", OffsetMax = "300 -135"},
					Image =
					{
						Color = "0 0 0 0"
					}
				}, EditingLayerModalTextEditor + ".Main", EditingLayerModalTextEditor + ".Main.Select.Lang",
				EditingLayerModalTextEditor + ".Main.Select.Lang");

			#region Title

			container.Add(new CuiElement
			{
				Parent = EditingLayerModalTextEditor + ".Main.Select.Lang",
				Components =
				{
					new CuiTextComponent
					{
						Text = "SELECT LANG", Font = "robotocondensed-regular.ttf", FontSize = 32,
						Align = TextAnchor.UpperLeft, Color = "0.8117647 0.2627451 0.1764706 0.9019608"
					},
					new CuiRectTransformComponent
						{AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -50", OffsetMax = "0 0"}
				}
			});

			#endregion Title

			#region Scroll

			container.Add(new CuiPanel
				{
					RectTransform =
						{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 -50"},
					Image =
					{
						Color = "0 0 0 0"
					}
				}, EditingLayerModalTextEditor + ".Main.Select.Lang",
				EditingLayerModalTextEditor + ".Main.Edit.Select.Lang.Panel",
				EditingLayerModalTextEditor + ".Main.Edit.Select.Lang.Panel");

			container.Add(new CuiElement
			{
				Name = EditingLayerModalTextEditor + ".Main.Select.Lang.Scroll.View",
				Parent = EditingLayerModalTextEditor + ".Main.Edit.Select.Lang.Panel",
				Components =
				{
					new CuiScrollViewComponent()
					{
						MovementType = ScrollRect.MovementType.Elastic,
						Vertical = true,
						Inertia = true,
						Horizontal = false,
						Elasticity = 0.25f,
						DecelerationRate = 0.3f,
						ScrollSensitivity = 24f,
						ContentTransform = new CuiRectTransform
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"0 {-totalHeight}",
							OffsetMax = "0 0"
						},
						VerticalScrollbar = new CuiScrollbar
						{
							Size = 3,
							AutoHide = false,
							HighlightColor = HexToCuiColor("#CE412B"),
							HandleColor = HexToCuiColor("#CE412B"),
							PressedColor = HexToCuiColor("#CE412B"),
							TrackColor = HexToCuiColor("#2C2F31")
						}
					}
				}
			});

			#endregion

			#region Loop

			ShowTextEditorLangsUI(player, container);

			#endregion

			#endregion

			#region Text Lines

			container.Add(new CuiPanel
				{
					RectTransform =
						{AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-320 -585", OffsetMax = "320 -135"},
					Image =
					{
						Color = "0 0 0 0"
					}
				}, EditingLayerModalTextEditor + ".Main",
				EditingLayerModalTextEditor + ".Main.Edit.Text",
				EditingLayerModalTextEditor + ".Main.Edit.Text");

			#region Title

			container.Add(new CuiElement
			{
				Parent = EditingLayerModalTextEditor + ".Main.Edit.Text",
				Components =
				{
					new CuiTextComponent
					{
						Text = "EDIT TEXT LINES", Font = "robotocondensed-regular.ttf", FontSize = 32,
						Align = TextAnchor.UpperLeft, Color = "0.8117647 0.2627451 0.1764706 0.9019608"
					},
					new CuiRectTransformComponent
						{AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -50", OffsetMax = "0 0"}
				}
			});

			#endregion Title

			ShowTextEditorScrollLinesUI(player, ref container);

			#endregion

			#region Text Style

			#region Fields

			var targetFields = new List<FieldInfo>
			{
				elementData.editingElement.GetType().GetField("Font"),
				elementData.editingElement.GetType().GetField("FontSize"),
				elementData.editingElement.GetType().GetField("Align"),
				elementData.editingElement.GetType().GetField("TextColor")
			};

			totalHeight = targetFields.Count * UI_TextEditor_TextStyle_Height +
			              (targetFields.Count - 1) * UI_TextEditor_TextStyle_Margin_Y;

			totalHeight = Mathf.Max(totalHeight, 450);
			
			#endregion

			container.Add(new CuiPanel
				{
					RectTransform =
						{AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-300 -585", OffsetMax = "-20 -135"},
					Image =
					{
						Color = "0 0 0 0"
					}
				}, EditingLayerModalTextEditor + ".Main", EditingLayerModalTextEditor + ".Main.Text.Style",
				EditingLayerModalTextEditor + ".Main.Text.Style");

			#region Title

			container.Add(new CuiElement
			{
				Parent = EditingLayerModalTextEditor + ".Main.Text.Style",
				Components =
				{
					new CuiTextComponent
					{
						Text = "TEXT STYLE", Font = "robotocondensed-regular.ttf", FontSize = 32,
						Align = TextAnchor.UpperLeft, Color = "0.8117647 0.2627451 0.1764706 0.9019608"
					},
					new CuiRectTransformComponent
						{AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -50", OffsetMax = "0 0"}
				}
			});

			#endregion Title

			#region Scroll

			container.Add(new CuiPanel
				{
					RectTransform =
						{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 -50"},
					Image =
					{
						Color = "0 0 0 0"
					}
				}, EditingLayerModalTextEditor + ".Main.Text.Style",
				EditingLayerModalTextEditor + ".Main.Edit.Text.Style.Panel",
				EditingLayerModalTextEditor + ".Main.Edit.Text.Style.Panel");

			container.Add(new CuiElement
			{
				Name = EditingLayerModalTextEditor + ".Main.Text.Style.Scroll.View",
				Parent = EditingLayerModalTextEditor + ".Main.Edit.Text.Style.Panel",
				Components =
				{
					new CuiScrollViewComponent()
					{
						MovementType = ScrollRect.MovementType.Elastic,
						Vertical = true,
						Inertia = true,
						Horizontal = false,
						Elasticity = 0.25f,
						DecelerationRate = 0.3f,
						ScrollSensitivity = 24f,
						ContentTransform = new CuiRectTransform
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"0 {-totalHeight}",
							OffsetMax = "0 0"
						},
						VerticalScrollbar = new CuiScrollbar
						{
							Size = 3,
							AutoHide = false,
							HighlightColor = HexToCuiColor("#CE412B"),
							HandleColor = HexToCuiColor("#CE412B"),
							PressedColor = HexToCuiColor("#CE412B"),
							TrackColor = HexToCuiColor("#2C2F31")
						}
					}
				}
			});

			#endregion

			#region Loop

			var offsetY = 0f;

			#region Formatting

			container.Add(new CuiPanel
				{
					Image = {Color = "0 0 0 0"},
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = $"0 {offsetY - UI_TextEditor_TextStyle_Height}", OffsetMax = $"-10 {offsetY}"
					}
				}, EditingLayerModalTextEditor + ".Main.Text.Style.Scroll.View",
				EditingLayerModalTextEditor + $".Main.Text.Style.Field.Formatting.Background",
				EditingLayerModalTextEditor + $".Main.Text.Style.Field.Formatting.Background");

			FormattingFieldElementUI(player, container);

			offsetY = offsetY - UI_TextEditor_TextStyle_Height - UI_TextEditor_TextStyle_Margin_Y;

			#endregion

			#region Target Fields

			foreach (var targetField in targetFields)
			{
				container.Add(new CuiPanel
					{
						Image = {Color = "0 0 0 0"},
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"0 {offsetY - UI_TextEditor_TextStyle_Height}", OffsetMax = $"-10 {offsetY}"
						}
					}, EditingLayerModalTextEditor + ".Main.Text.Style.Scroll.View",
					EditingLayerModalTextEditor + $".Main.Text.Style.Field.{targetField.Name}.Background",
					EditingLayerModalTextEditor + $".Main.Text.Style.Field.{targetField.Name}.Background");

				FieldElementUI(container, EditingLayerModalTextEditor + $".Main.Text.Style.Field.{targetField.Name}",
					targetField,
					targetField.GetValue(elementData.editingElement));

				offsetY = offsetY - UI_TextEditor_TextStyle_Height - UI_TextEditor_TextStyle_Margin_Y;
			}

			#endregion

			#endregion

			#endregion

			#endregion

			#region Save

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-84 30", OffsetMax = "84 70"
				},
				Text =
				{
					Text = "SAVE", Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter,
					Color = "0.8862745 0.8588235 0.827451 1"
				},
				Button =
				{
					Color = "0 0.372549 0.7176471 1",
					Close = bgLayer,
					Command = $"{CmdMainConsole} edit_element text save",
				}
			}, EditingLayerModalTextEditor + ".Main");

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private void ShowTextEditorScrollLinesUI(BasePlayer player,
			ref CuiElementContainer container)
		{
			#region Fields

			var elementData = EditUiElementData.Get(player.userID);

			var text = elementData.GetEditableText();
			
			var fontSize = elementData.editingElement.FontSize;

			var textLineHeight = fontSize * 1.5f;

			var totalHeight = 0f;
			foreach (var textLine in text)
			{
				var textSize = CalcTextSize(textLine, fontSize);
				if (textSize.x > UI_TextEditor_Lines_Width)
				{
					var xSize = Mathf.CeilToInt(textSize.x / UI_TextEditor_Lines_Width);
					
					totalHeight += (textLineHeight * xSize);
				}
				else
				{
					totalHeight += textLineHeight;
				}
			}

			totalHeight += 34 + 20;
			totalHeight = Mathf.Max(totalHeight + 300, 1000);

			#endregion
			
			#region Scroll

			container.Add(new CuiPanel
				{
					RectTransform =
						{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 -50"},
					Image =
					{
						Color = "0 0 0 0"
					}
				}, EditingLayerModalTextEditor + ".Main.Edit.Text",
				EditingLayerModalTextEditor + ".Main.Edit.Text.Scroll.Panel",
				EditingLayerModalTextEditor + ".Main.Edit.Text.Scroll.Panel");

			container.Add(new CuiElement
			{
				Name = EditingLayerModalTextEditor + ".Main.Edit.Text.Scroll.View",
				Parent = EditingLayerModalTextEditor + ".Main.Edit.Text.Scroll.Panel",
				Components =
				{
					new CuiScrollViewComponent()
					{
						MovementType = ScrollRect.MovementType.Elastic,
						Vertical = true,
						Inertia = true,
						Horizontal = false,
						Elasticity = 0.25f,
						DecelerationRate = 0.3f,
						ScrollSensitivity = 24f,
						ContentTransform = new CuiRectTransform
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"0 {-totalHeight}",
							OffsetMax = "0 0"
						},
						VerticalScrollbar = new CuiScrollbar
						{
							Size = 3,
							AutoHide = false,
							HighlightColor = HexToCuiColor("#CE412B"),
							HandleColor = HexToCuiColor("#CE412B"),
							PressedColor = HexToCuiColor("#CE412B"),
							TrackColor = HexToCuiColor("#2C2F31")
						}
					}
				}
			});

			#endregion

			ShowTextEditorLinesUI(player, ref container);
		}

		private void ShowTextEditorLangsUI(BasePlayer player, CuiElementContainer container)
		{
			var editPageData = EditUiElementData.Get(player.userID);

			var offsetY = 0f;
			foreach (var (flagPath, langKey, langName) in _langList)
			{
				var selectedLang = editPageData.IsSelectedLang(langKey);

				container.Add(new CuiPanel
					{
						Image =
						{
							Color = selectedLang ? HexToCuiColor("#A5EA32", 20) : HexToCuiColor("#4D4D4D", 40)
						},
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"0 {offsetY - UI_TextEditor_Lang_Height}", OffsetMax = $"-10 {offsetY}"
						}
					}, EditingLayerModalTextEditor + ".Main.Select.Lang.Scroll.View",
					EditingLayerModalTextEditor + $".Main.Select.Lang.Item.{langKey}",
					EditingLayerModalTextEditor + $".Main.Select.Lang.Item.{langKey}");

				#region flag

				container.Add(new CuiElement
				{
					Parent = EditingLayerModalTextEditor + $".Main.Select.Lang.Item.{langKey}",
					Components =
					{
						new CuiImageComponent
						{
							Sprite = flagPath,
							Material = flagPath,
						},
						new CuiRectTransformComponent
							{AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "4 -7", OffsetMax = "18 7"}
					}
				});

				#endregion flag

				#region country

				container.Add(new CuiElement
				{
					Parent = EditingLayerModalTextEditor + $".Main.Select.Lang.Item.{langKey}",
					Components =
					{
						new CuiTextComponent
						{
							Text = langName, Font = "robotocondensed-bold.ttf", FontSize = 14,
							Align = TextAnchor.MiddleLeft, Color = "0.8862745 0.8588235 0.827451 0.9019608"
						},
						new CuiRectTransformComponent
							{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "25 0", OffsetMax = "0 0"}
					}
				});

				#endregion country

				#region btn

				container.Add(new CuiElement
				{
					Parent = EditingLayerModalTextEditor + $".Main.Select.Lang.Item.{langKey}",
					Components =
					{
						new CuiButtonComponent()
						{
							Color = "0 0 0 0",
							Command = $"{CmdMainConsole} edit_element text lang select {langKey}",
						},
						new CuiRectTransformComponent()
					}
				});

				#endregion

				#region clear action

				if (editPageData.HasLang(langKey))
				{
					container.Add(new CuiElement
					{
						Name = EditingLayerModalTextEditor + $".Main.Select.Lang.Item.{langKey}.Clear",
						Parent = EditingLayerModalTextEditor + $".Main.Select.Lang.Item.{langKey}",
						Components =
						{
							new CuiImageComponent
							{
								Color = HexToCuiColor("#E2DBD3"),
								Sprite = "assets/icons/close.png"
							},
							new CuiRectTransformComponent
								{AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-18 -7", OffsetMax = "-4 7"}
						}
					});

					container.Add(new CuiElement
					{
						Parent = EditingLayerModalTextEditor + $".Main.Select.Lang.Item.{langKey}.Clear",
						Components =
						{
							new CuiButtonComponent()
							{
								Color = "0 0 0 0",
								Command = $"{CmdMainConsole} edit_element text lang remove {langKey}",
							},
							new CuiRectTransformComponent()
						}
					});
				}

				#endregion clear action

				offsetY = offsetY - UI_TextEditor_Lang_Height - UI_TextEditor_Lang_Margin_Y;
			}
		}

		private void ShowTextEditorLinesUI(BasePlayer player, ref CuiElementContainer container)
		{
			#region Fields

			var elementData = EditUiElementData.Get(player.userID);

			var text = elementData.GetEditableText();

			var font = GetFontByType(elementData.editingElement.Font);

			var fontSize = elementData.editingElement.FontSize;
			var textColor = elementData.editingElement.TextColor;
			var align = elementData.editingElement.Align;
			
			var textLineHeight = fontSize * 1.5f;

			#endregion

			#region Loop

			var offsetY = 0f;
			
			for (var index = 0; index < text.Count; index++)
			{
				var textLine = text[index];
				
				var targetHeight = textLineHeight;

				var textSize = CalcTextSize(textLine, fontSize);
				if (textSize.x > UI_TextEditor_Lines_Width)
				{
					var xSize = Mathf.CeilToInt(textSize.x / UI_TextEditor_Lines_Width);
					
					targetHeight = textLineHeight * xSize;
				}
				
				container.Add(new CuiPanel
					{
						Image = {Color = "0.3019608 0.3019608 0.3019608 0.4"},
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"0 {offsetY - targetHeight}",
							OffsetMax = $"-40 {offsetY}"
						}
					}, EditingLayerModalTextEditor + ".Main.Edit.Text.Scroll.View",
					EditingLayerModalTextEditor + $".Main.Edit.Text.Line.{index}",
					EditingLayerModalTextEditor + $".Main.Edit.Text.Line.{index}");

				#region title

				container.Add(new CuiElement
				{
					Parent = EditingLayerModalTextEditor + $".Main.Edit.Text.Line.{index}",
					Components =
					{
						new CuiInputFieldComponent
						{
							Text = elementData.isFormattingEnabled ? textLine : Formatter.ToPlaintext(textLine).EscapeRichText(),
							Font = font ?? "robotocondensed-bold.ttf",
							FontSize = fontSize,
							Align = align,
							Color = textColor?.Get() ?? "1 1 1 1",
							Command = $"{CmdMainConsole} edit_element text line set {index}",
							NeedsKeyboard = true,
							LineType = InputField.LineType.MultiLineNewline,
							HudMenuInput = true,

						},
						new CuiRectTransformComponent
							{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 0", OffsetMax = "-5 0"}
					}
				});

				#endregion title

				#region clear

				container.Add(new CuiElement
				{
					Name = EditingLayerModalTextEditor + $".Main.Edit.Text.Line.{index}.Clear",
					Parent = EditingLayerModalTextEditor + $".Main.Edit.Text.Line.{index}",
					Components =
					{
						new CuiImageComponent
						{
							Color = HexToCuiColor("#E2DBD3"),
							Sprite = "assets/icons/close.png"
						},
						new CuiRectTransformComponent
							{AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "12 -8", OffsetMax = "28 8"}
					}
				});

				container.Add(new CuiElement
				{
					Parent = EditingLayerModalTextEditor + $".Main.Edit.Text.Line.{index}.Clear",
					Components =
					{
						new CuiButtonComponent()
						{
							Color = "0 0 0 0",
							Command = $"{CmdMainConsole} edit_element text line remove {index}"
						},
						new CuiRectTransformComponent()
					}
				});

				#endregion clear

				if (index == text.Count - 1)
					offsetY -= targetHeight;
				else
					offsetY = offsetY - targetHeight - UI_TextEditor_Lines_Margin_Y;
			}

			#endregion

			#region Add New Line

			offsetY -= 20;

			container.Add(new CuiPanel
				{
					Image = {Color = "0 0 0 0"},
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 {offsetY - 34}",
						OffsetMax = $"-40 {offsetY}"
					}
				}, EditingLayerModalTextEditor + ".Main.Edit.Text.Scroll.View",
				EditingLayerModalTextEditor + $".Main.Edit.Text.Line.{text.Count}",
				EditingLayerModalTextEditor + $".Main.Edit.Text.Line.{text.Count}");

			#region line

			container.Add(new CuiPanel
				{
					Image = {Color = "0.8862745 0.8588235 0.827451 0.1490196"},
					RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -2", OffsetMax = "0 0"}
				}, EditingLayerModalTextEditor + $".Main.Edit.Text.Line.{text.Count}");

			#endregion

			#region title

			container.Add(new CuiElement
			{
				Parent = EditingLayerModalTextEditor + $".Main.Edit.Text.Line.{text.Count}",
				Components =
				{
					new CuiTextComponent
					{
						Text = "+ ADD NEW LINE", Font = "robotocondensed-bold.ttf", FontSize = 14,
						Align = TextAnchor.MiddleLeft, Color = "0.8117647 0.2627451 0.1764706 0.9019608"
					},
					new CuiRectTransformComponent
						{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 0", OffsetMax = "-5 0"}
				}
			});

			#endregion title

			container.Add(new CuiElement()
			{
				Parent = EditingLayerModalTextEditor + $".Main.Edit.Text.Line.{text.Count}",
				Components =
				{
					new CuiButtonComponent()
					{
						Color = "0 0 0 0",
						Command = $"{CmdMainConsole} edit_element text line add {text.Count}"
					},
					new CuiRectTransformComponent()
				}
			});

			#endregion
		}

		private static Vector2 CalcTextSize(string line, int fontSize)
		{
			var width = (line.Length + 1) * fontSize * 0.5f;
			return new Vector2(width, fontSize);
		}

		private string ShowTextEditorCloseButton(CuiElementContainer container, 
			string command,
			string close = "" )
		{
			var targetName = EditingLayerModalTextEditor + ".Main" + ".BTN.Close.Edit";
			container.Add(new CuiElement()
			{
				Parent = EditingLayerModalTextEditor+ ".Main",
				Name = targetName,
				DestroyUi = targetName,
				Components =
				{
					new CuiButtonComponent()
					{
						Color = "0 0 0 0",
						Close = close,
						Command = command ?? $"{CmdMainConsole} edit_element text close",
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "1 1", AnchorMax = "1 1",
						OffsetMin = "-30 -30",
						OffsetMax = "0 0"
					}
				}
			});

			container.Add(new CuiElement()
			{
				Parent = targetName,
				Components =
				{
					new CuiImageComponent
					{
						Color = HexToCuiColor("#EF5125"),
						Sprite = "assets/icons/close.png"
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "-10 -10",
						OffsetMax = "10 10",
					}
				}
			});

			return targetName;
		}
		
		private void ShowTextEditorNotifyBeforeClose(BasePlayer player)
		{
			var container = new CuiElementContainer();

			var closeLayer = ShowTextEditorCloseButton(container, $"{CmdMainConsole} edit_element text close", close: EditingLayerModalTextEditor);
			
			container.Add(new CuiButton()
			{
				Button =
				{
					Color = "0.1137255 0.2588235 0.372549 1",
					Close = EditingLayerModalTextEditor + ".Main" + ".BTN.Close.Edit" + ".Notify"
				},
				Text =
				{
					Text = Msg(player, "You are about to close the text editor. All unsaved changes will be lost.\nTo confirm closing the editor and lose all unsaved data, click the close icon again."), Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.282353 0.6039216 0.8313726 1"
				},
				RectTransform ={ AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "-440 -50", OffsetMax = "0 0" }
			}, closeLayer, closeLayer + ".Notify", closeLayer + ".Notify");

			CuiHelper.AddUi(player, container);
		}
		
		#endregion

		#endregion

		#region Category Editor

		private const int
			UI_CategoryEditor_EditField_Left_Indent = 0,
			UI_CategoryEditor_EditField_Width = 164,
			UI_CategoryEditor_EditField_Height = 50,
			UI_CategoryEditor_EditField_MarginX = 10,
			UI_CategoryEditor_EditField_MarginY = 6,
			UI_CategoryEditor_EditField_OnLine = 4,
			UI_CategoryEditor_EditArrayField_Left_Indent = 30,
			UI_CategoryEditor_EditArrayField_Width = 164,
			UI_CategoryEditor_EditArrayField_Height = 50,
			UI_CategoryEditor_EditArrayField_MarginX = 10,
			UI_CategoryEditor_EditArrayField_MarginY = 6,
			UI_CategoryEditor_EditArrayField_OnLine = 4,
			UI_CategoryEditor_CommandField_Height = 26,
			UI_CategoryEditor_CommandField_Margin = 2;

		private void ShowCategoryEditorPanel(BasePlayer player)
		{
			var editCategoryData = EditCategoryData.Get(player.userID);
			if (editCategoryData == null) return;

			var container = new CuiElementContainer();

			#region Background

			container.Add(new CuiPanel()
			{
				RectTransform =
				{
					AnchorMin = "0 0",
					AnchorMax = "1 1"
				},
				Image =
				{
					Color = "0 0 0 0.9",
					Material = "assets/content/ui/uibackgroundblur.mat",
					Sprite = "assets/content/ui/ui.background.transparent.radial.psd"
				}
			}, Layer, EditingLayerPageEditor, EditingLayerPageEditor);

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				Image = {Color = HexToCuiColor("#000000")},
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-400 -300", OffsetMax = "400 300"
				}
			}, EditingLayerPageEditor, EditingLayerPageEditor + ".Main", EditingLayerPageEditor + ".Main");

			#endregion

			#region Header

			container.Add(new CuiElement
			{
				Parent = EditingLayerPageEditor + ".Main",
				Components =
				{
					new CuiRawImageComponent
					{
						Png = GetImage("ServerPanel_Editor_EditCategory")
					},
					new CuiRectTransformComponent
						{AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "16 -50", OffsetMax = "44 -22"}
				}
			});

			container.Add(new CuiElement
			{
				Parent = EditingLayerPageEditor + ".Main",
				Components =
				{
					new CuiTextComponent
					{
						Text = "EDIT CATEGORY", Font = "robotocondensed-regular.ttf", FontSize = 22,
						Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#CF432D", 90)
					},
					new CuiRectTransformComponent
						{AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "54 -54", OffsetMax = "0 -18"}
				}
			});

			#endregion

			#region Close Button

			container.Add(new CuiElement
			{
				Name = EditingLayerPageEditor + ".Button.Close",
				Parent = EditingLayerPageEditor + ".Main",
				Components =
				{
					new CuiButtonComponent
					{
						Color = "0 0 0 0",
						Command = $"{CmdMainConsole} edit_category close",
						Close = EditingLayerPageEditor
					},
					new CuiRectTransformComponent
						{AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-50 -50", OffsetMax = "-10 -10"}
				}
			});

			container.Add(new CuiElement
			{
				Parent = EditingLayerPageEditor + ".Button.Close",
				Components =
				{
					new CuiImageComponent {Sprite = "assets/icons/close.png"},
					new CuiRectTransformComponent
						{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-10 -10", OffsetMax = "10 10"}
				}
			});

			#endregion

			#region Outline

			CreateOutline(container, HexToCuiColor("#CF432D"), 4, EditingLayerPageEditor + ".Main");

			#endregion

			#region Content

			var targetFields =
				Array.FindAll(editCategoryData.menuCategory.GetType().GetFields(),
					field => field.FieldType.IsPrimitive || field.FieldType == typeof(string));

			var maxLines = Mathf.CeilToInt((float) targetFields.Length / UI_CategoryEditor_EditField_OnLine);

			var totalHeight = maxLines * UI_CategoryEditor_EditField_Height +
			                  (maxLines - 1) * UI_CategoryEditor_EditField_MarginY;

			totalHeight = Mathf.Max(475, totalHeight);

			container.Add(new CuiPanel
				{
					Image = {Color = HexToCuiColor("#000000", 0)},
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "32 55", OffsetMax = "-32 -70"
					}
				}, EditingLayerPageEditor + ".Main", EditingLayerPageEditor + ".Content",
				EditingLayerPageEditor + ".Content");

			container.Add(new CuiElement
			{
				Parent = EditingLayerPageEditor + ".Content",
				Name = EditingLayerPageEditor + ".Content.View",
				DestroyUi = EditingLayerPageEditor + ".Content.View",
				Components =
				{
					new CuiScrollViewComponent
					{
						MovementType = ScrollRect.MovementType.Elastic,
						Vertical = true,
						Inertia = true,
						Horizontal = false,
						Elasticity = 0.25f,
						DecelerationRate = 0.3f,
						ScrollSensitivity = 24f,
						ContentTransform = new CuiRectTransform
						{
							AnchorMin = "0 1",
							AnchorMax = "1 1",
							OffsetMin = $"0 -{totalHeight}",
							OffsetMax = "0 0"
						},
						VerticalScrollbar = new CuiScrollbar
						{
							Size = 4,
							AutoHide = false,
							Invert = true,
							HighlightColor = HexToCuiColor("#D74933"),
							HandleColor = HexToCuiColor("#D74933"),
							PressedColor = HexToCuiColor("#D74933"),
							TrackColor = HexToCuiColor("#373737")
						}
					}
				}
			});

			#region Loop

			var offsetY = 0f;

			CategoryEditorCategoriesLoopUI(player, targetFields, container, editCategoryData, ref offsetY);

			#endregion

			#region Pages

			var pagesField = editCategoryData.menuCategory.GetType().GetField("Pages");
			if (pagesField?.GetValue(editCategoryData.menuCategory) is List<CategoryPage> categoryPages)
			{
				var pageTargetFields =
					Array.FindAll(typeof(CategoryPage).GetFields(),
						field => field.Name != nameof(CategoryPage.Elements));

				offsetY = offsetY - 20;

				var pagesHeader = container.Add(new CuiPanel
				{
					Image = {Color = HexToCuiColor("#E44028", 0)},
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = $"0 {offsetY - 30}",
						OffsetMax = $"700 {offsetY}"
					}
				}, EditingLayerPageEditor + ".Content.View");

				container.Add(new CuiLabel()
				{
					Text =
					{
						Text = "PAGES SECTION", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf",
						FontSize = 24, Color = HexToCuiColor("#CF432D", 90)
					}
				}, pagesHeader);

				offsetY = offsetY - 30 - 20;

				for (var pageIndex = 0; pageIndex < categoryPages.Count; pageIndex++)
				{
					var categoryPage = categoryPages[pageIndex];

					var pageLayer = CuiHelper.GetGuid();

					#region Header

					container.Add(new CuiPanel
					{
						Image = {Color = HexToCuiColor("#FFFFFF", 20)},
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "0 1",
							OffsetMin = $"0 {offsetY - 30f}",
							OffsetMax = $"700 {offsetY}"
						}
					}, EditingLayerPageEditor + ".Content.View", pageLayer, pageLayer);

					container.Add(new CuiElement
					{
						Parent = pageLayer,
						Components =
						{
							new CuiTextComponent
							{
								Text = $"{pageIndex + 1}", Font = "robotocondensed-bold.ttf", FontSize = 14,
								Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#E2DBD3")
							},
							new CuiRectTransformComponent
								{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"}
						}
					});

					#endregion

					offsetY = offsetY - 30f - 20f;

					#region Page Fields

					var offsetX = UI_CategoryEditor_EditField_Left_Indent;
					for (var fieldIndex = 0; fieldIndex < pageTargetFields.Length; fieldIndex++)
					{
						var targetField = pageTargetFields[fieldIndex];
						var fieldLayer = CuiHelper.GetGuid();

						container.Add(new CuiPanel
							{
								Image = {Color = "0 0 0 0"},
								RectTransform =
								{
									AnchorMin = "0 1",
									AnchorMax = "0 1",
									OffsetMin = $"{offsetX} {offsetY - UI_CategoryEditor_EditField_Height}",
									OffsetMax = $"{offsetX + UI_CategoryEditor_EditField_Width} {offsetY}"
								}
							}, EditingLayerPageEditor + ".Content.View", fieldLayer + ".Background",
							fieldLayer + ".Background");

						CategoryEditorPagesFieldUI(player, container, fieldLayer, targetField,
							targetField.GetValue(categoryPage), pageIndex);

						#region Calculate Position

						if (fieldIndex + 1 != pageTargetFields.Length)
						{
							if ((fieldIndex + 1) % UI_CategoryEditor_EditField_OnLine == 0)
							{
								offsetX = UI_CategoryEditor_EditField_Left_Indent;
								offsetY = offsetY - UI_CategoryEditor_EditField_Height -
								          UI_CategoryEditor_EditField_MarginY;
							}
							else
							{
								offsetX = offsetX + UI_CategoryEditor_EditField_Width +
								          UI_CategoryEditor_EditField_MarginX;
							}
						}

						#endregion
					}

					#endregion
				}

				#endregion
			}

			#endregion

			#region Buttons

			if (editCategoryData.NeedCreate)
			{
				#region Create

				container.Add(new CuiButton
				{
					Text =
					{
						Text = "CREATE", Font = "robotocondensed-regular.ttf", FontSize = 14,
						Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#E2DBD3")
					},
					Button =
					{
						Color = HexToCuiColor("#005FB7"),
						Command = $"{CmdMainConsole} edit_category save",
						Close = EditingLayerPageEditor
					},
					RectTransform =
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = "-65 15", OffsetMax = "65 45"
					}
				}, EditingLayerPageEditor + ".Main");

				#endregion Create
			}
			else
			{
				#region Save

				container.Add(new CuiButton
				{
					Text =
					{
						Text = "SAVE", Font = "robotocondensed-regular.ttf", FontSize = 14,
						Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#E2DBD3")
					},
					Button =
					{
						Color = HexToCuiColor("#005FB7"),
						Command = $"{CmdMainConsole} edit_category save",
						Close = EditingLayerPageEditor
					},
					RectTransform =
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = "-140 15", OffsetMax = "-10 45"
					}
				}, EditingLayerPageEditor + ".Main");

				#endregion Save

				#region Remove

				container.Add(new CuiButton
				{
					Text =
					{
						Text = "REMOVE", Font = "robotocondensed-regular.ttf", FontSize = 14,
						Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#E2DBD3")
					},
					Button =
					{
						Color = HexToCuiColor("#CF432D"),
						Command = $"{CmdMainConsole} edit_category remove",
						Close = EditingLayerPageEditor
					},
					RectTransform =
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = "10 15", OffsetMax = "140 45"
					}
				}, EditingLayerPageEditor + ".Main");

				#endregion Remove
			}

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private void ShowCategoryArrayEditorModal(BasePlayer player)
		{
			var editCategoryData = EditCategoryData.Get(player.userID);
			if (editCategoryData == null) return;

			var container = new CuiElementContainer();

			#region Background

			container.Add(new CuiPanel()
			{
				RectTransform =
				{
					AnchorMin = "0 0",
					AnchorMax = "1 1"
				},
				Image =
				{
					Color = "0 0 0 0.9",
					Material = "assets/content/ui/uibackgroundblur.mat",
					Sprite = "assets/content/ui/ui.background.transparent.radial.psd"
				}
			}, Layer, EditingLayerModal, EditingLayerModal);

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				Image = {Color = HexToCuiColor("#000000")},
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-200 -150", OffsetMax = "200 150"
				}
			}, EditingLayerModal, EditingLayerModal + ".Main", EditingLayerModal + ".Main");

			#endregion

			#region Header

			container.Add(new CuiElement
			{
				Parent = EditingLayerModal + ".Main",
				Components =
				{
					new CuiTextComponent
					{
						Text = editCategoryData.editableArrayName ?? string.Empty, Font = "robotocondensed-regular.ttf",
						FontSize = 22, Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#CF432D", 90)
					},
					new CuiRectTransformComponent
						{AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "20 -47", OffsetMax = "-50 -13"}
				}
			});

			#endregion

			#region Close Button

			container.Add(new CuiElement
			{
				Name = EditingLayerModal + ".Button.Close",
				Parent = EditingLayerModal + ".Main",
				Components =
				{
					new CuiButtonComponent
					{
						Color = "0 0 0 0",
						Command = $"{CmdMainConsole} edit_category array close",
						Close = EditingLayerModal
					},
					new CuiRectTransformComponent
						{AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-50 -50", OffsetMax = "-10 -10"}
				}
			});

			container.Add(new CuiElement
			{
				Parent = EditingLayerModal + ".Button.Close",
				Components =
				{
					new CuiImageComponent {Color = HexToCuiColor("#FFFFFF"), Sprite = "assets/icons/close.png"},
					new CuiRectTransformComponent
						{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-10 -10", OffsetMax = "10 10"}
				}
			});

			#endregion

			#region Add Button

			container.Add(new CuiElement
			{
				Name = EditingLayerModal + ".Button.Add",
				Parent = EditingLayerModal + ".Main",
				Components =
				{
					new CuiButtonComponent
					{
						Color = "0 0 0 0",
						Command = $"{CmdMainConsole} edit_category array add",
					},
					new CuiRectTransformComponent
						{AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-100 -50", OffsetMax = "-60 -10"}
				}
			});

			container.Add(new CuiElement
			{
				Parent = EditingLayerModal + ".Button.Add",
				Components =
				{
					new CuiImageComponent {Color = HexToCuiColor("#FFFFFF"), Sprite = "assets/icons/add.png"},
					new CuiRectTransformComponent
						{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-10 -10", OffsetMax = "10 10"}
				}
			});

			#endregion

			#region Outline

			CreateOutline(container, HexToCuiColor("#CF432D"), 4, EditingLayerModal + ".Main");

			#endregion

			#region Content

			var arrayValues = editCategoryData.GetEditableArrayValues();

			var maxLines = Mathf.CeilToInt((float) arrayValues.Length / UI_CategoryEditor_EditField_OnLine);

			var totalHeight = maxLines * UI_CategoryEditor_EditField_Height +
			                  (maxLines - 1) * UI_CategoryEditor_EditField_MarginY;

			totalHeight = Mathf.Max(475, totalHeight);

			#region Scroll View

			container.Add(new CuiPanel
				{
					Image = {Color = HexToCuiColor("#000000", 0)},
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "32 55", OffsetMax = "-32 -70"
					}
				}, EditingLayerModal + ".Main",
				EditingLayerModal + ".Content",
				EditingLayerModal + ".Content");

			container.Add(new CuiElement
			{
				Parent = EditingLayerModal + ".Content",
				Name = EditingLayerModalArrayView,
				DestroyUi = EditingLayerModalArrayView,
				Components =
				{
					new CuiScrollViewComponent
					{
						MovementType = ScrollRect.MovementType.Elastic,
						Vertical = true,
						Inertia = true,
						Horizontal = false,
						Elasticity = 0.25f,
						DecelerationRate = 0.3f,
						ScrollSensitivity = 24f,
						ContentTransform = new CuiRectTransform
						{
							AnchorMin = "0 1",
							AnchorMax = "1 1",
							OffsetMin = $"0 -{totalHeight}",
							OffsetMax = "0 0"
						},
						VerticalScrollbar = new CuiScrollbar
						{
							Size = 4,
							AutoHide = false,
							Invert = true,
							HighlightColor = HexToCuiColor("#D74933"),
							HandleColor = HexToCuiColor("#D74933"),
							PressedColor = HexToCuiColor("#D74933"),
							TrackColor = HexToCuiColor("#373737")
						}
					}
				}
			});

			#endregion

			#region Loop

			CategoryArrayEditorLoopUI(arrayValues, container);

			#endregion

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private static void CategoryArrayEditorLoopUI(object[] targetFields, CuiElementContainer container)
		{
			var offsetY = 0f;
			for (var cmdIndex = 0; cmdIndex < targetFields.Length; cmdIndex++)
			{
				var targetCMD = targetFields[cmdIndex];

				container.Add(new CuiPanel
					{
						Image =
						{
							Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
							Color = HexToCuiColor("#38393F", 40)
						},
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"0 {offsetY - UI_CategoryEditor_CommandField_Height}",
							OffsetMax = $"-20 {offsetY}"
						}
					}, EditingLayerModalArrayView,
					EditingLayerModalArrayView + $".Command.{cmdIndex}",
					EditingLayerModalArrayView + $".Command.{cmdIndex}");

				container.Add(new CuiElement
				{
					Parent = EditingLayerModalArrayView + $".Command.{cmdIndex}",
					Components =
					{
						new CuiInputFieldComponent
						{
							Text = targetCMD?.ToString() ?? string.Empty,
							Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft,
							Color = HexToCuiColor("#E2DBD3"),
							NeedsKeyboard = true,
							Command = $"{CmdMainConsole} edit_category array edit {cmdIndex}"
						},
						new CuiRectTransformComponent
							{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 0", OffsetMax = "-5 0"}
					}
				});

				container.Add(new CuiElement
				{
					Parent = EditingLayerModalArrayView + $".Command.{cmdIndex}",
					Components =
					{
						new CuiButtonComponent()
						{
							Color = HexToCuiColor("#E2DBD3"),
							Sprite = "assets/icons/close.png",
							Command = $"{CmdMainConsole} edit_category array remove {cmdIndex}"
						},
						new CuiRectTransformComponent
							{AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-20 -6", OffsetMax = "-8 6"}
					}
				});

				#region Calculate Position

				offsetY = offsetY - UI_CategoryEditor_CommandField_Height - UI_CategoryEditor_CommandField_Margin;

				#endregion
			}
		}

		#endregion

		#endregion Editor Panel

		#region UI.Components

		private static void TitleEditorUI(CuiElementContainer container,
			string parent,
			ref float offsetY,
			string textTitle,
			float size = 40f,
			float margin = 10f,
			int fontSize = 24)
		{
			var textStyleLayer = container.Add(new CuiPanel
			{
				Image = {Color = "0 0 0 0"},
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = $"20 {offsetY - size}",
					OffsetMax = $"-20 {offsetY}"
				}
			}, parent);

			container.Add(new CuiLabel()
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1"
				},
				Text =
				{
					Text = textTitle,
					Font = "robotocondensed-bold.ttf",
					FontSize = fontSize,
					Align = TextAnchor.MiddleLeft,
					Color = HexToCuiColor("#CF432D", 90)
				}
			}, textStyleLayer);

			offsetY = offsetY - size - margin;
		}

		private static void CategoryEditorCategoriesLoopUI(BasePlayer player, FieldInfo[] targetFields,
			CuiElementContainer container,
			EditCategoryData editCategoryData, ref float offsetY)
		{
			var offsetX = UI_CategoryEditor_EditField_Left_Indent;
			for (var fieldIndex = 0; fieldIndex < targetFields.Length; fieldIndex++)
			{
				var targetField = targetFields[fieldIndex];
				var fieldLayer = CuiHelper.GetGuid();

				container.Add(new CuiPanel
				{
					Image = {Color = "0 0 0 0"},
					RectTransform =
					{
						AnchorMin = "0 1",
						AnchorMax = "0 1",
						OffsetMin = $"{offsetX} {offsetY - UI_CategoryEditor_EditField_Height}",
						OffsetMax = $"{offsetX + UI_CategoryEditor_EditField_Width} {offsetY}"
					}
				}, EditingLayerPageEditor + ".Content.View", fieldLayer + ".Background", fieldLayer + ".Background");

				CategoryEditorFieldUI(player, container, fieldLayer, targetField,
					targetField.GetValue(editCategoryData.menuCategory));

				#region Calculate Position

				if (fieldIndex + 1 != targetFields.Length)
				{
					if ((fieldIndex + 1) % UI_CategoryEditor_EditField_OnLine == 0)
					{
						offsetX = UI_CategoryEditor_EditField_Left_Indent;
						offsetY = offsetY - UI_CategoryEditor_EditField_Height - UI_CategoryEditor_EditField_MarginY;
					}
					else
					{
						offsetX = offsetX + UI_CategoryEditor_EditField_Width + UI_CategoryEditor_EditField_MarginX;
					}
				}

				#endregion
			}

			offsetY = offsetY - UI_CategoryEditor_EditField_Height - UI_CategoryEditor_EditField_MarginY;

			#region Commands

			var commandsField = editCategoryData.menuCategory.GetType().GetField("Commands");
			if (commandsField?.GetValue(editCategoryData.menuCategory) is string[] commandsList)
			{
				offsetX = UI_CategoryEditor_EditField_Left_Indent;

				var targetFieldLayer = CuiHelper.GetGuid();

				container.Add(new CuiPanel
				{
					Image = {Color = "0 0 0 0"},
					RectTransform =
					{
						AnchorMin = "0 1",
						AnchorMax = "0 1",
						OffsetMin = $"{offsetX} {offsetY - UI_CategoryEditor_EditField_Height}",
						OffsetMax = $"{offsetX + UI_CategoryEditor_EditField_Width} {offsetY}"
					}
				}, EditingLayerPageEditor + ".Content.View", targetFieldLayer, targetFieldLayer);

				container.Add(new CuiLabel()
				{
					RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -24", OffsetMax = "0 0"},
					Text =
					{
						Text = $"{commandsField.GetFieldTitle()}",
						Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft,
						Color = HexToCuiColor("#E2DBD3")
					}
				}, targetFieldLayer);

				container.Add(new CuiButton()
				{
					Text =
					{
						Text = "OPEN",
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Align = TextAnchor.MiddleCenter,
						Color = HexToCuiColor("#E2DBD3"),
					},
					Button =
					{
						Color = HexToCuiColor("#38393F", 40),
						Command = $"{CmdMainConsole} edit_category array start {commandsField.Name}"
					},
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 26"
					}
				}, targetFieldLayer);
			}

			#endregion

			offsetY = offsetY - UI_CategoryEditor_EditField_Height;
		}

		private static void CategoryEditorLoopFieldUI(CuiElementContainer container,
			MenuCategory targetCategory,
			string commandsFieldLayer,
			FieldInfo commandsField)
		{
			if (commandsField?.GetValue(targetCategory) is not string[] commandsList)
				return;

			container.Add(new CuiPanel()
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image = {Color = "0 0 0 0"}
				}, commandsFieldLayer + ".Background",
				commandsFieldLayer, commandsFieldLayer);

			container.Add(new CuiElement
			{
				Parent = commandsFieldLayer,
				Components =
				{
					new CuiTextComponent
					{
						Text =
							$"{commandsField.GetFieldTitle()}",
						Font = "robotocondensed-bold.ttf", FontSize = 10,
						Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#E2DBD3")
					},
					new CuiRectTransformComponent
						{AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -24", OffsetMax = "0 0"}
				}
			});

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-24 -24", OffsetMax = "0 0"},
				Text =
				{
					Text = "+",
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 18,
					Color = HexToCuiColor("#E2DBD3")
				},
				Button =
				{
					Color = "0 0 0 0",
					Command = $"{CmdMainConsole} edit_category array {commandsField.Name} {commandsFieldLayer} add"
				}
			}, commandsFieldLayer);

			var offsetY = -24f;
			for (var cmdIndex = 0; cmdIndex < commandsList.Length; cmdIndex++)
			{
				var targetCMD = commandsList[cmdIndex];

				container.Add(new CuiPanel
					{
						Image =
						{
							Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
							Color = HexToCuiColor("#38393F", 40)
						},
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"0 {offsetY - UI_CategoryEditor_CommandField_Height}",
							OffsetMax = $"0 {offsetY}"
						}
					}, commandsFieldLayer, commandsFieldLayer + $".Command.{cmdIndex}",
					commandsFieldLayer + $".Command.{cmdIndex}");

				container.Add(new CuiElement
				{
					Parent = commandsFieldLayer + $".Command.{cmdIndex}",
					Components =
					{
						new CuiInputFieldComponent
						{
							Text = targetCMD ?? string.Empty,
							Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft,
							Color = HexToCuiColor("#E2DBD3"),
							NeedsKeyboard = true,
							Command =
								$"{CmdMainConsole} edit_category array {commandsField.Name} {commandsFieldLayer} edit {cmdIndex}"
						},
						new CuiRectTransformComponent
							{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 0", OffsetMax = "-5 0"}
					}
				});

				container.Add(new CuiElement
				{
					Parent = commandsFieldLayer + $".Command.{cmdIndex}",
					Components =
					{
						new CuiButtonComponent()
						{
							Color = HexToCuiColor("#E2DBD3"),
							Sprite = "assets/icons/close.png",
							Command =
								$"{CmdMainConsole} edit_category array {commandsField.Name} {commandsFieldLayer} remove {cmdIndex}"
						},
						new CuiRectTransformComponent
							{AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-16 -6", OffsetMax = "-4 6"}
					}
				});

				#region Calculate Position

				offsetY = offsetY - UI_CategoryEditor_CommandField_Height - UI_CategoryEditor_CommandField_Margin;

				#endregion
			}
		}

		private static void CategoryEditorFieldUI(BasePlayer player,
			CuiElementContainer container,
			string targetFieldLayer,
			FieldInfo targetField,
			object fieldValue)
		{
			container.Add(new CuiPanel()
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image = {Color = "0 0 0 0"}
			}, targetFieldLayer + ".Background", targetFieldLayer, targetFieldLayer);

			if (fieldValue is bool boolValue)
			{
				container.Add(new CuiLabel()
				{
					RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -24", OffsetMax = "0 0"},
					Text =
					{
						Text = $"{targetField.GetFieldTitle()}",
						Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft,
						Color = HexToCuiColor("#E2DBD3")
					}
				}, targetFieldLayer);

				container.Add(new CuiButton()
				{
					Text =
					{
						Text = boolValue ? "ON" : "OFF",
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Align = TextAnchor.MiddleCenter,
						Color = HexToCuiColor("#E2DBD3")
					},
					Button =
					{
						Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
						Color = boolValue ? HexToCuiColor("#D74933", 90) : HexToCuiColor("#71B8ED", 20),
						Command =
							$"{CmdMainConsole} edit_category field {targetField.Name} {targetFieldLayer} {!boolValue}",
					},
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 26"}
				}, targetFieldLayer);
			}
			else
			{
				container.Add(new CuiLabel()
				{
					RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -24", OffsetMax = "0 0"},
					Text =
					{
						Text = $"{targetField.GetFieldTitle()}",
						Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft,
						Color = HexToCuiColor("#E2DBD3")
					}
				}, targetFieldLayer);

				#region Value

				var fieldValueStr = fieldValue?.ToString();

				container.Add(new CuiPanel
				{
					Image = {Color = HexToCuiColor("#38393F", 40)},
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 26"}
				}, targetFieldLayer, targetFieldLayer + ".Value");

				container.Add(new CuiElement()
				{
					Parent = targetFieldLayer + ".Value",
					Components =
					{
						new CuiInputFieldComponent()
						{
							Font = "robotocondensed-bold.ttf",
							FontSize = 12,
							Align = TextAnchor.MiddleLeft,
							Color = HexToCuiColor("#E2DBD3"),
							Text = $"{fieldValueStr}",
							NeedsKeyboard = true,
							Command = $"{CmdMainConsole} edit_category field {targetField.Name} {targetFieldLayer}"
						},
						new CuiRectTransformComponent() {AnchorMin = "0 0", AnchorMax = "1 1"}
					}
				});

				#endregion
			}
		}

		private static void CategoryEditorPagesFieldUI(BasePlayer player,
			CuiElementContainer container,
			string targetFieldLayer,
			FieldInfo targetField,
			object fieldValue,
			int pageIndex)
		{
			container.Add(new CuiPanel()
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image = {Color = "0 0 0 0"}
			}, targetFieldLayer + ".Background", targetFieldLayer, targetFieldLayer);

			container.Add(new CuiLabel()
			{
				RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -24", OffsetMax = "0 0"},
				Text =
				{
					Text = $"{targetField.GetFieldTitle()}",
					Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft,
					Color = HexToCuiColor("#E2DBD3")
				}
			}, targetFieldLayer);

			if (targetField.FieldType.IsEnum)
			{
				#region Value

				var fieldValueStr = fieldValue?.ToString();

				container.Add(new CuiPanel
				{
					Image = {Color = HexToCuiColor("#38393F", 40)},
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 26"}
				}, targetFieldLayer, targetFieldLayer + ".Value");

				container.Add(new CuiElement()
				{
					Parent = targetFieldLayer + ".Value",
					Components =
					{
						new CuiTextComponent()
						{
							Font = "robotocondensed-bold.ttf",
							FontSize = 12,
							Align = TextAnchor.MiddleCenter,
							Color = HexToCuiColor("#E2DBD3"),
							Text = $"{fieldValueStr}",
						},
						new CuiRectTransformComponent() {AnchorMin = "0 0", AnchorMax = "1 1"}
					}
				});

				#endregion

				#region Buttons

				container.Add(new CuiButton()
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "0 1",
						OffsetMin = "0 0", OffsetMax = "18 0"
					},
					Text =
					{
						Text = "<",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 14,
						Color = HexToCuiColor("#E2DBD3", 90)
					},
					Button =
					{
						Command =
							$"{CmdMainConsole} edit_category page field {pageIndex} {targetField.Name} {targetFieldLayer} prev",
						Color = HexToCuiColor("#4D4D4D", 50)
					}
				}, targetFieldLayer + ".Value");

				container.Add(new CuiButton()
				{
					RectTransform =
					{
						AnchorMin = "1 0", AnchorMax = "1 1",
						OffsetMin = "-18 0", OffsetMax = "0 0"
					},
					Text =
					{
						Text = ">",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 14,
						Color = HexToCuiColor("#E2DBD3", 90)
					},
					Button =
					{
						Command =
							$"{CmdMainConsole} edit_category page field {pageIndex} {targetField.Name} {targetFieldLayer} next",
						Color = HexToCuiColor("#4D4D4D", 50)
					}
				}, targetFieldLayer + ".Value");

				#endregion
			}
			else if (fieldValue is bool boolValue)
			{
				container.Add(new CuiButton()
				{
					Text =
					{
						Text = boolValue ? "ON" : "OFF",
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Align = TextAnchor.MiddleCenter,
						Color = HexToCuiColor("#E2DBD3")
					},
					Button =
					{
						Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
						Color = boolValue ? HexToCuiColor("#D74933", 90) : HexToCuiColor("#71B8ED", 20),
						Command =
							$"{CmdMainConsole} edit_category page field {pageIndex} {targetField.Name} {targetFieldLayer} {!boolValue}",
					},
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 26"}
				}, targetFieldLayer);
			}
			else
			{
				#region Value

				var fieldValueStr = fieldValue?.ToString();

				container.Add(new CuiPanel
				{
					Image = {Color = HexToCuiColor("#38393F", 40)},
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 26"}
				}, targetFieldLayer, targetFieldLayer + ".Value");

				container.Add(new CuiElement()
				{
					Parent = targetFieldLayer + ".Value",
					Components =
					{
						new CuiInputFieldComponent()
						{
							Font = "robotocondensed-bold.ttf",
							FontSize = 12,
							Align = TextAnchor.MiddleLeft,
							Color = HexToCuiColor("#E2DBD3"),
							Text = $"{fieldValueStr}",
							NeedsKeyboard = true,
							Command =
								$"{CmdMainConsole} edit_category page field {pageIndex} {targetField.Name} {targetFieldLayer}"
						},
						new CuiRectTransformComponent() {AnchorMin = "0 0", AnchorMax = "1 1"}
					}
				});

				#endregion
			}
		}

		private void PageEditorFieldUI(CuiElementContainer container,
			int elementIndex,
			UiElement cuiElement,
			string cmdRemove,
			string cmdClone,
			string cmdSwitch,
			string cmdEdit,
			string cmdMove)
		{
			container.Add(new CuiPanel()
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image = {Color = "0 0 0 0"}
				}, EditingLayerPageEditor + $".Selection.Element.{elementIndex}",
				EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel",
				EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel");

			UpdatePointPageEditorUI(container, elementIndex, cuiElement, cmdSwitch);

			UpdateTitlePageEditorFieldUI(container, elementIndex, cuiElement);

			AddButton(cmdRemove, "ServerPanel_Editor_Btn_Remove", "-60 -6", "-48 6");
			AddButton(cmdClone, "ServerPanel_Editor_Btn_Clone", "-44 -6", "-32 6");
			AddButton(cmdEdit, "ServerPanel_Editor_Btn_Edit", "-28 -5", "-16 6");
			AddButton(cmdMove + " up", "ServerPanel_Editor_Btn_Up", "-12 1", "-5 6");
			AddButton(cmdMove + " down", "ServerPanel_Editor_Btn_Down", "-12 -6", "-5 -1");
			
			#region Helpers

			void AddButton(string command, string image, string offsetMin, string offsetMax)
			{
				container.Add(new CuiElement()
				{
					Name = EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel" + ".Button." + command.Replace(" ", "."),
					Parent = EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel",
					Components =
					{
						new CuiRawImageComponent()
						{
							Png = GetImage(image)
						},
						new CuiRectTransformComponent()
						{
							AnchorMin = "1 0.5", AnchorMax = "1 0.5",
							OffsetMin = offsetMin,
							OffsetMax = offsetMax
						}
					}
				});

				container.Add(new CuiElement()
				{
					Parent = EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel" + ".Button." + command.Replace(" ", "."),
					Components =
					{
						new CuiButtonComponent()
						{
							Color = "0 0 0 0",
							Command = $"{CmdMainConsole} {command} {elementIndex}",
						},
						new CuiRectTransformComponent()
						{
							AnchorMin = "0 0", AnchorMax = "1 1"
						}
					}
				});
			}

			#endregion
		}
		
		private static void UpdatePointPageEditorUI(CuiElementContainer container, 
			int elementIndex,
			UiElement cuiElement,
			string cmdSwitch)
		{
			container.Add(new CuiElement()
			{
				Name = EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel" + ".Point",
				DestroyUi = EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel" + ".Point",
				Parent = EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel",
				Components =
				{
					new CuiButtonComponent()
					{
						Color = "0 0 0 0",
						Command = $"{CmdMainConsole} {cmdSwitch} {elementIndex}"
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "0 0.5",
						AnchorMax = "0 0.5",
						OffsetMin = "4 -6", OffsetMax = "16 6"
					}
				}
			});

			container.Add(new CuiElement()
			{
				Parent = EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel" + ".Point",
				Components =
				{
					new CuiRawImageComponent()
					{
						Png = Instance.GetImage(cuiElement.Visible ? "ServerPanel_Editor_Visible_On" : "ServerPanel_Editor_Visible_Off")
					},
					new CuiRectTransformComponent() {AnchorMin = "0 0", AnchorMax = "1 1"}
				}
			});
		}

		private static void UpdateTitlePageEditorFieldUI(CuiElementContainer container, int elementIndex,
			UiElement cuiElement, bool needUpdate = false)
		{
			var element = new CuiElement()
			{
				Name = EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel" + ".Title",
				Parent = EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel",
				Components =
				{
					new CuiInputFieldComponent()
					{
						Text = cuiElement.Name ?? string.Empty,
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 9,
						Color = HexToCuiColor("#E2DBD3", 90),
						ReadOnly = true
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "22 0", OffsetMax = "-65 0"
					}
				}
			};

			if (needUpdate) element.Update = true;

			container.Add(element);
		}

		private static void PositionSectionUI(BasePlayer player,
			CuiElementContainer container,
			string parentCommand,
			InterfacePosition pos)
		{
			var editPageData = EditUiElementData.Get(player.userID);

			container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image = {Color = "0 0 0 0"}
				}, EditingLayerElementEditor + ".Selection.RectTransform.Section.Position.Background",
				EditingLayerElementEditor + ".Selection.RectTransform.Section.Position",
				EditingLayerElementEditor + ".Selection.RectTransform.Section.Position");

			AdminFieldUI("EXPERT MODE", "0 1", "0 1", "0 -20", "200 0",
				$"{CmdMainConsole} {parentCommand} rect_transform expert_mode");

			#region constraints

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -50", OffsetMax = "0 -30"
				},
				Text =
				{
					Text = "Constraints",
					Align = TextAnchor.UpperLeft,
					FontSize = 11,
					Font = "robotocondensed-bold.ttf",
					Color = HexToCuiColor("#E2DBD3", 90)
				}
			}, EditingLayerElementEditor + ".Selection.RectTransform.Section.Position");

			PositionSectionConstraintsFieldUI("0 1", "0 1", "0 -70", "90 -50", "horizontal",
				$"{CmdMainConsole} {parentCommand} rect_transform enter constraint");

			PositionSectionConstraintsFieldUI("0 1", "0 1", "110 -70", "200 -50", "vertical",
				$"{CmdMainConsole} {parentCommand} rect_transform enter constraint");

			#endregion

			if (editPageData.ExpertMode)
			{
				#region Anchors

				PositionSectionAnchorFieldUI("0 1", "0 1", "0 -120", "90 -100", nameof(pos.AnchorMinX),
					$"{CmdMainConsole} {parentCommand} rect_transform enter rect");

				PositionSectionAnchorFieldUI("0 1", "0 1", "110 -120", "200 -100", nameof(pos.AnchorMinY),
					$"{CmdMainConsole} {parentCommand} rect_transform enter rect");

				PositionSectionAnchorFieldUI("0 1", "0 1", "0 -170", "90 -150", nameof(pos.AnchorMaxX),
					$"{CmdMainConsole} {parentCommand} rect_transform enter rect");

				PositionSectionAnchorFieldUI("0 1", "0 1", "110 -170", "200 -150", nameof(pos.AnchorMaxY),
					$"{CmdMainConsole} {parentCommand} rect_transform enter rect");

				#endregion

				#region Offsets

				PositionSectionAnchorFieldUI("0 1", "0 1", "0 -220", "90 -200", nameof(pos.OffsetMinX),
					$"{CmdMainConsole} {parentCommand} rect_transform enter rect");

				PositionSectionAnchorFieldUI("0 1", "0 1", "110 -220", "200 -200", nameof(pos.OffsetMinY),
					$"{CmdMainConsole} {parentCommand} rect_transform enter rect");

				PositionSectionAnchorFieldUI("0 1", "0 1", "0 -270", "90 -250", nameof(pos.OffsetMaxX),
					$"{CmdMainConsole} {parentCommand} rect_transform enter rect");

				PositionSectionAnchorFieldUI("0 1", "0 1", "110 -270", "200 -250", nameof(pos.OffsetMaxY),
					$"{CmdMainConsole} {parentCommand} rect_transform enter rect");

				#endregion
			}
			else
			{
				#region Titles

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "0 -100", OffsetMax = "0 -80"
					},
					Text =
					{
						Text = "Horizontal",
						Align = TextAnchor.UpperLeft,
						FontSize = 11,
						Font = "robotocondensed-bold.ttf",
						Color = HexToCuiColor("#E2DBD3", 90)
					}
				}, EditingLayerElementEditor + ".Selection.RectTransform.Section.Position");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "0 -150", OffsetMax = "0 -130"
					},
					Text =
					{
						Text = "Vertical",
						Align = TextAnchor.UpperLeft,
						FontSize = 11,
						Font = "robotocondensed-bold.ttf",
						Color = HexToCuiColor("#E2DBD3", 90)
					}
				}, EditingLayerElementEditor + ".Selection.RectTransform.Section.Position");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "0 -200", OffsetMax = "0 -180"
					},
					Text =
					{
						Text = "Movement",
						Align = TextAnchor.UpperLeft,
						FontSize = 11,
						Font = "robotocondensed-bold.ttf",
						Color = HexToCuiColor("#E2DBD3", 90)
					}
				}, EditingLayerElementEditor + ".Selection.RectTransform.Section.Position");

				#endregion

				#region Axis.X

				if (Mathf.Approximately(pos.AnchorMinX, 0) && Mathf.Approximately(pos.AnchorMaxX, 1))
				{
					PositionSectionFieldUI("L", "0 1", "0 1", "0 -120", "90 -100",
						$"{CmdMainConsole} {parentCommand} rect_transform enter padding left",
						pos.GetPadding().ToString(CultureInfo.CurrentCulture));

					PositionSectionFieldUI("R", "0 1", "0 1", "110 -120", "200 -100",
						$"{CmdMainConsole} {parentCommand} rect_transform enter padding right",
						pos.GetPadding(1).ToString(CultureInfo.InvariantCulture));
				}
				else
				{
					PositionSectionFieldUI("X", "0 1", "0 1", "0 -120", "90 -100",
						$"{CmdMainConsole} {parentCommand} rect_transform enter axis", pos.GetAxis(true).ToString(CultureInfo.CurrentCulture));

					PositionSectionFieldUI("W", "0 1", "0 1", "110 -120", "200 -100",
						$"{CmdMainConsole} {parentCommand} rect_transform enter", pos.GetWidth().ToString(CultureInfo.CurrentCulture));
				}

				#endregion

				#region Axis.Y

				if (Mathf.Approximately(pos.AnchorMinY, 0) && Mathf.Approximately(pos.AnchorMaxY, 1))
				{
					PositionSectionFieldUI("T", "0 1", "0 1", "0 -170", "90 -150",
						$"{CmdMainConsole} {parentCommand} rect_transform enter padding top",
						pos.GetPadding(2).ToString(CultureInfo.CurrentCulture));

					PositionSectionFieldUI("B", "0 1", "0 1", "110 -170", "200 -150",
						$"{CmdMainConsole} {parentCommand} rect_transform enter padding bottom",
						pos.GetPadding(3).ToString(CultureInfo.CurrentCulture));
				}
				else
				{
					PositionSectionFieldUI("Y", "0 1", "0 1", "0 -170", "90 -150",
						$"{CmdMainConsole} {parentCommand} rect_transform enter axis", pos.GetAxis(false).ToString(CultureInfo.CurrentCulture));

					PositionSectionFieldUI("H", "0 1", "0 1", "110 -170", "200 -150",
						$"{CmdMainConsole} {parentCommand} rect_transform enter", pos.GetHeight().ToString(CultureInfo.CurrentCulture));
				}

				#endregion

				#region Move

				container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "0 1",
							OffsetMin = "0 -220", OffsetMax = "90 -200"
						},
						Image =
						{
							Color = HexToCuiColor("#2C2C2C", 90)
						}
					}, EditingLayerElementEditor + ".Selection.RectTransform.Section.Position",
					EditingLayerElementEditor + ".Selection.RectTransform.Section.Position.Move.Input.Background",
					EditingLayerElementEditor + ".Selection.RectTransform.Section.Position.Move.Input.Background");

				PositionSectionMovementFieldUI($"{CmdMainConsole} {parentCommand} rect_transform enter step");

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = "110 -220", OffsetMax = "130 -200"
					},
					Text =
					{
						Text = "◀",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 14,
						Color = HexToCuiColor("#E2DBD3", 90)
					},
					Button =
					{
						Color = HexToCuiColor("#4D4D4D", 90),
						Command = $"{CmdMainConsole} {parentCommand} rect_transform move left"
					}
				}, EditingLayerElementEditor + ".Selection.RectTransform.Section.Position");

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = "180 -220", OffsetMax = "200 -200"
					},
					Text =
					{
						Text = "▶",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 14,
						Color = HexToCuiColor("#E2DBD3", 90)
					},
					Button =
					{
						Color = HexToCuiColor("#4D4D4D", 90),
						Command = $"{CmdMainConsole} {parentCommand} rect_transform move right"
					}
				}, EditingLayerElementEditor + ".Selection.RectTransform.Section.Position");

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = "134 -208", OffsetMax = "176 -200"
					},
					Text =
					{
						Text = "▲",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 6,
						Color = HexToCuiColor("#E2DBD3", 90)
					},
					Button =
					{
						Color = HexToCuiColor("#4D4D4D", 90),
						Command = $"{CmdMainConsole} {parentCommand} rect_transform move top"
					}
				}, EditingLayerElementEditor + ".Selection.RectTransform.Section.Position");

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = "134 -220", OffsetMax = "176 -212"
					},
					Text =
					{
						Text = "▼",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 6,
						Color = HexToCuiColor("#E2DBD3", 90)
					},
					Button =
					{
						Color = HexToCuiColor("#4D4D4D", 90),
						Command = $"{CmdMainConsole} {parentCommand} rect_transform move bottom"
					}
				}, EditingLayerElementEditor + ".Selection.RectTransform.Section.Position");

				#endregion
			}

			#region Position Utils

			void AdminFieldUI(
				string label,
				string aMin1, string aMax1, string oMin1, string oMax1,
				string targetCmd)
			{
				container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = aMin1, AnchorMax = aMax1,
							OffsetMin = oMin1, OffsetMax = oMax1
						},
						Image =
						{
							Color = "0 0 0 0"
						}
					}, EditingLayerElementEditor + ".Selection.RectTransform.Section.Position",
					EditingLayerElementEditor + $".Selection.RectTransform.Section.Position.Field.{label}",
					EditingLayerElementEditor + $".Selection.RectTransform.Section.Position.Field.{label}");

				container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "0 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = label,
							Align = TextAnchor.MiddleLeft,
							FontSize = 14,
							Font = "robotocondensed-bold.ttf",
							Color = HexToCuiColor("#E2DBD3", 90)
						}
					}, EditingLayerElementEditor + $".Selection.RectTransform.Section.Position.Field.{label}");

				container.Add(new CuiElement
				{
					Parent = EditingLayerElementEditor + $".Selection.RectTransform.Section.Position.Field.{label}",
					Name = EditingLayerElementEditor +
					       $".Selection.RectTransform.Section.Position.Field.{label}.Button",
					DestroyUi = EditingLayerElementEditor +
					            $".Selection.RectTransform.Section.Position.Field.{label}.Button",
					Components =
					{
						new CuiButtonComponent
						{
							Color = "0 0 0 0",
							Command = targetCmd
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "1 0.5",
							AnchorMax = "1 0.5",
							OffsetMin = "-40 -8", OffsetMax = "0 8"
						}
					}
				});

				container.Add(new CuiElement
				{
					Parent = EditingLayerElementEditor +
					         $".Selection.RectTransform.Section.Position.Field.{label}.Button",
					Components =
					{
						new CuiRawImageComponent
						{
							Png = Instance.GetImage(editPageData.ExpertMode
								? "ServerPanel_Editor_Switch_On"
								: "ServerPanel_Editor_Switch_Off")
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "1 1"
						}
					}
				});
			}

			void PositionSectionMovementFieldUI(string targetCmd)
			{
				container.Add(new CuiElement
				{
					Name = EditingLayerElementEditor + ".Selection.RectTransform.Section.Position.Move.Input",
					DestroyUi = EditingLayerElementEditor + ".Selection.RectTransform.Section.Position.Move.Input",
					Parent = EditingLayerElementEditor +
					         ".Selection.RectTransform.Section.Position.Move.Input.Background",
					Components =
					{
						new CuiInputFieldComponent
						{
							FontSize = 12,
							Font = "robotocondensed-bold.ttf",
							Align = TextAnchor.MiddleCenter,
							Command = targetCmd,
							Color = HexToCuiColor("#E2DBD3", 90),
							Text = editPageData.movementStep.ToString(CultureInfo.CurrentCulture),
							NeedsKeyboard = true
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "0 0", OffsetMax = "0 0"
						}
					}
				});
			}

			void PositionSectionFieldUI(string label,
				string aMin1, string aMax1, string oMin1, string oMax1, string targetCmd,
				string targetValue)
			{
				container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = aMin1, AnchorMax = aMax1,
							OffsetMin = oMin1, OffsetMax = oMax1
						},
						Image =
						{
							Color = HexToCuiColor("#2C2C2C")
						}
					}, EditingLayerElementEditor + ".Selection.RectTransform.Section.Position",
					EditingLayerElementEditor + $".Selection.RectTransform.Section.Position.Field.{label}",
					EditingLayerElementEditor + $".Selection.RectTransform.Section.Position.Field.{label}");

				var labelLayer = container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "0 1",
							OffsetMin = "0 00", OffsetMax = "20 0"
						},
						Image =
						{
							Color = HexToCuiColor("#434343")
						}
					}, EditingLayerElementEditor + $".Selection.RectTransform.Section.Position.Field.{label}");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1"
					},
					Text =
					{
						Text = label,
						Align = TextAnchor.MiddleCenter,
						FontSize = 11,
						Font = "robotocondensed-bold.ttf",
						Color = HexToCuiColor("#E2DBD3", 90)
					}
				}, labelLayer);

				PositionSectionFieldInputUI(label, targetCmd, targetValue);
			}

			void PositionSectionFieldInputUI(string label, string targetCmd,
				string targetValue)
			{
				container.Add(new CuiElement
				{
					Name = EditingLayerElementEditor + $".Selection.RectTransform.Section.Position.Field.{label}.Input",
					DestroyUi = EditingLayerElementEditor +
					            $".Selection.RectTransform.Section.Position.Field.{label}.Input",
					Parent = EditingLayerElementEditor + $".Selection.RectTransform.Section.Position.Field.{label}",
					Components =
					{
						new CuiInputFieldComponent
						{
							FontSize = 12,
							Font = "robotocondensed-bold.ttf",
							Align = TextAnchor.MiddleCenter,
							Command = $"{targetCmd} {label}",
							Color = HexToCuiColor("#E2DBD3", 90),
							Text = targetValue,
							NeedsKeyboard = true
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "25 0", OffsetMax = "0 0"
						}
					}
				});
			}

			void PositionSectionConstraintsFieldUI(
				string aMin1, string aMax1, string oMin1, string oMax1,
				string axis,
				string targetCmd)
			{
				container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = aMin1, AnchorMax = aMax1,
							OffsetMin = oMin1, OffsetMax = oMax1
						},
						Image =
						{
							Color = HexToCuiColor("#2C2C2C")
						}
					}, EditingLayerElementEditor + ".Selection.RectTransform.Section.Position",
					EditingLayerElementEditor + $".Selection.RectTransform.Section.Position.Field.{axis}",
					EditingLayerElementEditor + $".Selection.RectTransform.Section.Position.Field.{axis}");

				container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "0 1",
							OffsetMin = "0 0", OffsetMax = "20 0"
						},
						Text =
						{
							Text = "<",
							Align = TextAnchor.MiddleCenter,
							FontSize = 11,
							Font = "robotocondensed-bold.ttf",
							Color = HexToCuiColor("#E2DBD3", 90)
						},
						Button =
						{
							Color = HexToCuiColor("#434343"),
							Command = targetCmd + " " + axis + " " + "prev"
						}
					}, EditingLayerElementEditor + $".Selection.RectTransform.Section.Position.Field.{axis}");

				container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "1 0", AnchorMax = "1 1",
							OffsetMin = "-20 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = ">",
							Align = TextAnchor.MiddleCenter,
							FontSize = 11,
							Font = "robotocondensed-bold.ttf",
							Color = HexToCuiColor("#E2DBD3", 90)
						},
						Button =
						{
							Color = HexToCuiColor("#434343"),
							Command = targetCmd + " " + axis + " " + "next"
						}
					}, EditingLayerElementEditor + $".Selection.RectTransform.Section.Position.Field.{axis}");

				var targetValue = axis == "horizontal"
					? editPageData.editingElement.GetHorizontalAxis().ToString()
					: editPageData.editingElement.GetVerticalAxis().ToString();

				container.Add(new CuiElement
				{
					Name = EditingLayerElementEditor + $".Selection.RectTransform.Section.Position.Field.{axis}.Input",
					DestroyUi = EditingLayerElementEditor +
					            $".Selection.RectTransform.Section.Position.Field.{axis}.Input",
					Parent = EditingLayerElementEditor + $".Selection.RectTransform.Section.Position.Field.{axis}",
					Components =
					{
						new CuiTextComponent
						{
							FontSize = 12,
							Font = "robotocondensed-bold.ttf",
							Align = TextAnchor.MiddleCenter,
							Color = HexToCuiColor("#E2DBD3", 90),
							Text = targetValue
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "20 0", OffsetMax = "-20 0"
						}
					}
				});
			}

			void PositionSectionAnchorFieldUI(
				string aMin1, string aMax1, string oMin1, string oMax1,
				string fieldName,
				string targetCmd)
			{
				var targetField = editPageData.editingElement.GetType().GetField(fieldName);
				if (targetField == null)
					return;

				container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = aMin1, AnchorMax = aMax1,
							OffsetMin = oMin1, OffsetMax = oMax1
						},
						Image =
						{
							Color = HexToCuiColor("#2C2C2C")
						}
					}, EditingLayerElementEditor + ".Selection.RectTransform.Section.Position",
					EditingLayerElementEditor + $".Selection.RectTransform.Section.Position.Field.{fieldName}",
					EditingLayerElementEditor + $".Selection.RectTransform.Section.Position.Field.{fieldName}");

				container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = "0 0", OffsetMax = "0 20"
						},
						Text =
						{
							Text = targetField.GetFieldTitle() ?? string.Empty,
							Align = TextAnchor.MiddleLeft,
							FontSize = 11,
							Font = "robotocondensed-bold.ttf",
							Color = HexToCuiColor("#E2DBD3", 90)
						}
					}, EditingLayerElementEditor + $".Selection.RectTransform.Section.Position.Field.{fieldName}");

				container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "0 1",
							OffsetMin = "0 0", OffsetMax = "20 0"
						},
						Text =
						{
							Text = "-",
							Align = TextAnchor.MiddleCenter,
							FontSize = 11,
							Font = "robotocondensed-bold.ttf",
							Color = HexToCuiColor("#E2DBD3", 90)
						},
						Button =
						{
							Color = HexToCuiColor("#434343"),
							Command = targetCmd + " " + fieldName + " " + "-"
						}
					}, EditingLayerElementEditor + $".Selection.RectTransform.Section.Position.Field.{fieldName}");

				container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "1 0", AnchorMax = "1 1",
							OffsetMin = "-20 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = "+",
							Align = TextAnchor.MiddleCenter,
							FontSize = 11,
							Font = "robotocondensed-bold.ttf",
							Color = HexToCuiColor("#E2DBD3", 90)
						},
						Button =
						{
							Color = HexToCuiColor("#434343"),
							Command = targetCmd + " " + fieldName + " " + "+"
						}
					}, EditingLayerElementEditor + $".Selection.RectTransform.Section.Position.Field.{fieldName}");

				PositionSectionAnchorInputUI(fieldName, targetCmd);
			}

			void PositionSectionAnchorInputUI(string fieldName, string targetCmd)
			{
				var targetField = editPageData.editingElement.GetType().GetField(fieldName);
				if (targetField == null || targetField.GetValue(editPageData.editingElement) is not float targetValue)
					return;

				container.Add(new CuiElement
				{
					Name = EditingLayerElementEditor +
					       $".Selection.RectTransform.Section.Position.Field.{fieldName}.Input",
					DestroyUi = EditingLayerElementEditor +
					            $".Selection.RectTransform.Section.Position.Field.{fieldName}.Input",
					Parent = EditingLayerElementEditor + $".Selection.RectTransform.Section.Position.Field.{fieldName}",
					Components =
					{
						new CuiInputFieldComponent
						{
							FontSize = 12,
							Font = "robotocondensed-bold.ttf",
							Align = TextAnchor.MiddleCenter,
							Command = $"{targetCmd} {fieldName}",
							Color = HexToCuiColor("#E2DBD3", 90),
							Text = targetValue.ToString(CultureInfo.CurrentCulture),
							NeedsKeyboard = true
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "20 0", OffsetMax = "-20 0"
						}
					}
				});
			}

			#endregion
		}

		private static void FieldElementUI(CuiElementContainer container,
			string targetFieldLayer,
			FieldInfo targetField,
			object fieldValue)
		{
			container.Add(new CuiPanel()
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image = {Color = "0 0 0 0"}
			}, targetFieldLayer + ".Background", targetFieldLayer, targetFieldLayer);

			if (fieldValue is bool boolValue)
			{
				container.Add(new CuiLabel()
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "5 0", OffsetMax = "0 0"
					},
					Text =
					{
						Text = targetField.GetFieldTitle() ?? string.Empty,
						Align = TextAnchor.MiddleLeft,
						FontSize = 14,
						Font = "robotocondensed-bold.ttf",
						Color = HexToCuiColor("#E2DBD3", 90)
					}
				}, targetFieldLayer);
				
				container.Add(new CuiElement()
				{
					Parent = targetFieldLayer,
					Name = targetFieldLayer + ".Button",
					DestroyUi = targetFieldLayer + ".Button",
					Components =
					{
						new CuiButtonComponent()
						{
							Color = "0 0 0 0",
							Command = $"{CmdMainConsole} edit_element field {targetField.Name} {targetFieldLayer} {!boolValue}",
						},
						new CuiRectTransformComponent()
						{
							AnchorMin = "1 0.5", AnchorMax = "1 0.5",
							OffsetMin = "-40 -8", OffsetMax = "0 8"
						}
					}
				});
			
				container.Add(new CuiElement()
				{
					Parent = targetFieldLayer + ".Button",
					Components =
					{
						new CuiRawImageComponent()
						{
							Png = Instance.GetImage(boolValue ? "ServerPanel_Editor_Switch_On" : "ServerPanel_Editor_Switch_Off")
						},
						new CuiRectTransformComponent()
						{
							AnchorMin = "0 0", AnchorMax = "1 1"
						}
					}
				});
			}
			else
			{
				container.Add(new CuiLabel()
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "0 -20", OffsetMax = "0 0"
					},
					Text =
					{
						Text = targetField.GetFieldTitle() ?? string.Empty,
						Align = TextAnchor.UpperLeft,
						FontSize = 11,
						Font = "robotocondensed-bold.ttf",
						Color = HexToCuiColor("#E2DBD3", 90)
					}
				}, targetFieldLayer);

				#region Value

				if (fieldValue is IColor colorValue)
				{
					container.Add(new CuiPanel()
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0",
							OffsetMin = "0 0", OffsetMax = "0 25"
						},
						Image =
						{
							Color = colorValue.Get()
						}
					}, targetFieldLayer, targetFieldLayer + ".Value");

					container.Add(new CuiPanel()
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "0.6 1",
							OffsetMin = "0 0", OffsetMax = "0 0"
						},
						Image =
						{
							Color = HexToCuiColor("#000000", 75),
							Material = "assets/content/ui/uibackgroundblur.mat"
						}
					}, targetFieldLayer + ".Value", targetFieldLayer + ".Value.Color");

					container.Add(new CuiElement()
					{
						Parent = targetFieldLayer + ".Value.Color",
						Components =
						{
							new CuiRawImageComponent()
							{
								Png = Instance.GetImage("ServerPanel_Editor_Select")
							},
							new CuiRectTransformComponent()
							{
								AnchorMin = "0 0.5", AnchorMax = "0 0.5",
								OffsetMin = "12 -9", OffsetMax = "30 9"
							}
						}
					});
					container.Add(new CuiElement()
					{
						Parent = targetFieldLayer + ".Value.Color",
						Components =
						{
							new CuiTextComponent()
							{
								Text = $"{colorValue.Hex}",
								Align = TextAnchor.MiddleLeft,
								Font = "robotocondensed-bold.ttf",
								FontSize = 12,
								Color = HexToCuiColor("#E2DBD3", 90)
							},
							new CuiRectTransformComponent()
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "35 0", OffsetMax = "0 0"
							}
						}
					});

					container.Add(new CuiElement()
					{
						Parent = targetFieldLayer + ".Value.Color",
						Components =
						{
							new CuiButtonComponent()
							{
								Color = "0 0 0 0",
								Command =
									$"{CmdMainConsole} edit_element color start {targetField.Name} {targetFieldLayer}"
							},
							new CuiRectTransformComponent()
							{
								AnchorMin = "0 0", AnchorMax = "1 1"
							}
						}
					});
				}
				else if (targetField.FieldType.IsEnum)
				{
					var fieldValueStr = fieldValue?.ToString();

					container.Add(new CuiPanel()
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0",
							OffsetMin = "0 0", OffsetMax = "0 20"
						},
						Image =
						{
							Color = HexToCuiColor("#2C2C2C", 100)
						}
					}, targetFieldLayer, targetFieldLayer + ".Value");

					container.Add(new CuiElement()
					{
						Parent = targetFieldLayer + ".Value",
						Components =
						{
							new CuiTextComponent()
							{
								FontSize = 12,
								Font = "robotocondensed-bold.ttf",
								Align = TextAnchor.MiddleCenter,
								Color = HexToCuiColor("#E2DBD3", 90),
								Text = fieldValueStr ?? string.Empty,
							},
							new CuiRectTransformComponent()
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "20 0", OffsetMax = "-20 0"
							}
						}
					});

					#region Prev

					container.Add(new CuiButton()
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "0 1",
							OffsetMin = "0 0", OffsetMax = "20 0"
						},
						Text =
						{
							Text = "<",
							Align = TextAnchor.MiddleCenter,
							FontSize = 11,
							Font = "robotocondensed-bold.ttf",
							Color = HexToCuiColor("#E2DBD3", 90)
						},
						Button =
						{
							Command = $"{CmdMainConsole} edit_element field {targetField.Name} {targetFieldLayer} prev",
							Color = HexToCuiColor("#434343", 100),
						}
					}, targetFieldLayer + ".Value");

					#endregion

					#region Next

					container.Add(new CuiButton()
					{
						RectTransform =
						{
							AnchorMin = "1 0", AnchorMax = "1 1",
							OffsetMin = "-20 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = ">",
							Align = TextAnchor.MiddleCenter,
							FontSize = 11,
							Font = "robotocondensed-bold.ttf",
							Color = HexToCuiColor("#E2DBD3", 90)
						},
						Button =
						{
							Command = $"{CmdMainConsole} edit_element field {targetField.Name} {targetFieldLayer} next",
							Color = HexToCuiColor("#434343", 100),
						}
					}, targetFieldLayer + ".Value");

					#endregion
				}
				else if (double.TryParse(fieldValue?.ToString(), out var numberValue))
				{
					container.Add(new CuiPanel()
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0",
							OffsetMin = "0 0", OffsetMax = "0 20"
						},
						Image =
						{
							Color = HexToCuiColor("#2C2C2C", 100)
						}
					}, targetFieldLayer, targetFieldLayer + ".Value");

					container.Add(new CuiElement()
					{
						Parent = targetFieldLayer + ".Value",
						Components =
						{
							new CuiInputFieldComponent()
							{
								FontSize = 12,
								Font = "robotocondensed-bold.ttf",
								Align = TextAnchor.MiddleCenter,
								Command = $"{CmdMainConsole} edit_element field {targetField.Name} {targetFieldLayer}",
								Color = HexToCuiColor("#E2DBD3", 90),
								Text = numberValue.ToString() ?? string.Empty,
								NeedsKeyboard = true
							},
							new CuiRectTransformComponent() {AnchorMin = "0 0", AnchorMax = "1 1"}
						}
					});
					
					var stepSize = 1f;
					if (targetField.Name.Contains("Anchor"))
						stepSize = 0.1f;

					#region Minus

					container.Add(new CuiButton()
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "0 1",
							OffsetMin = "0 0", OffsetMax = "20 0"
						},
						Text =
						{
							Text = "-",
							Align = TextAnchor.MiddleCenter,
							FontSize = 11,
							Font = "robotocondensed-bold.ttf",
							Color = HexToCuiColor("#E2DBD3", 90)
						},
						Button =
						{
							Command =
								$"{CmdMainConsole} edit_element field {targetField.Name} {targetFieldLayer} {numberValue - stepSize}",
							Color = HexToCuiColor("#434343", 100),
						}
					}, targetFieldLayer + ".Value");

					#endregion

					#region Add

					container.Add(new CuiButton()
					{
						RectTransform =
						{
							AnchorMin = "1 0", AnchorMax = "1 1",
							OffsetMin = "-20 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = "+",
							Align = TextAnchor.MiddleCenter,
							FontSize = 11,
							Font = "robotocondensed-bold.ttf",
							Color = HexToCuiColor("#E2DBD3", 90)
						},
						Button =
						{
							Command =
								$"{CmdMainConsole} edit_element field {targetField.Name} {targetFieldLayer} {numberValue + stepSize}",
							Color = HexToCuiColor("#434343", 100),
						}
					}, targetFieldLayer + ".Value");

					#endregion
				}
				else
				{
					var targetStr = fieldValue?.ToString();

					container.Add(new CuiPanel()
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0",
							OffsetMin = "0 0", OffsetMax = "0 20"
						},
						Image =
						{
							Color = HexToCuiColor("#2C2C2C", 100)
						}
					}, targetFieldLayer, targetFieldLayer + ".Value");

					container.Add(new CuiElement()
					{
						Parent = targetFieldLayer + ".Value",
						Components =
						{
							new CuiInputFieldComponent()
							{
								FontSize = 12,
								Font = "robotocondensed-bold.ttf",
								Align = TextAnchor.MiddleCenter,
								Command = $"{CmdMainConsole} edit_element field {targetField.Name} {targetFieldLayer}",
								Color = HexToCuiColor("#E2DBD3", 90),
								Text = targetStr ?? string.Empty,
								NeedsKeyboard = true
							},
							new CuiRectTransformComponent() {AnchorMin = "0 0", AnchorMax = "1 1"}
						}
					});
				}

				#endregion
			}
		}

		private static void FormattingFieldElementUI(
			BasePlayer player,
			CuiElementContainer container)
		{
			var elementData = EditUiElementData.Get(player.userID);

			container.Add(new CuiPanel()
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image = {Color = "0 0 0 0"}
			}, EditingLayerModalTextEditor + ".Main.Text.Style.Field.Formatting" + ".Background", EditingLayerModalTextEditor + ".Main.Text.Style.Field.Formatting", EditingLayerModalTextEditor + ".Main.Text.Style.Field.Formatting");

			container.Add(new CuiLabel()
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "5 0", OffsetMax = "0 0"
					},
					Text =
					{
						Text = "Toggle Formatting",
						Align = TextAnchor.MiddleLeft,
						FontSize = 16,
						Font = "robotocondensed-bold.ttf",
						Color = HexToCuiColor("#E2DBD3", 90)
					}
				}, EditingLayerModalTextEditor + ".Main.Text.Style.Field.Formatting");

			container.Add(new CuiButton()
				{
					RectTransform =
					{
						AnchorMin = "1 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "-25 -12.5", OffsetMax = "0 12.5"
					},
					Text =
					{
						Text = elementData.isFormattingEnabled ? "✔" : string.Empty,
						Font = "robotocondensed-bold.ttf",
						FontSize = 14,
						Align = TextAnchor.MiddleCenter,
						Color = HexToCuiColor("#E2DBD3", 90)
					},
					Button =
					{
						Color = HexToCuiColor("#4D4D4D", 50),
						Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
						Command = $"{CmdMainConsole} edit_element text toggle_formatting"
					},
				}, EditingLayerModalTextEditor + ".Main.Text.Style.Field.Formatting");
		}

		private static void CreateOutline(CuiElementContainer container, string outlineColor, int outlineSize,
			string outlineParent)
		{
			#region Outline (1)

			container.Add(new CuiPanel
			{
				Image = {Color = outlineColor},
				RectTransform =
					{AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{outlineSize}", OffsetMax = "0 0"}
			}, outlineParent);

			#endregion Outline (1)

			#region Outline (2)

			container.Add(new CuiPanel
			{
				Image = {Color = outlineColor},
				RectTransform =
					{AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = $"0 {outlineSize}"}
			}, outlineParent);

			#endregion Outline (2)

			#region Outline (3)

			container.Add(new CuiPanel
			{
				Image = {Color = outlineColor},
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = "0 4", OffsetMax = $"{outlineSize} -{outlineSize}"
				}
			}, outlineParent);

			#endregion Outline (3)

			#region Outline (4)

			container.Add(new CuiPanel
			{
				Image = {Color = outlineColor},
				RectTransform =
				{
					AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = $"-{outlineSize} {outlineSize}",
					OffsetMax = $"0 -{outlineSize}"
				}
			}, outlineParent);

			#endregion Outline (4)
		}

		private static void ShowCloseButtonUI(CuiElementContainer container,
			string closeButtonParent,
			string closeButtonName,
			string closeLayer = "",
			string commandOnClose = "",
			string closeButtonAnchorMin = "1 1",
			string closeButtonAnchorMax = "1 1",
			string closeButtonOffsetMin = "-40 -40",
			string closeButtonOffsetMax = "0 0",
            string backgroundColor = null,
			string iconSprite = null,
            string iconColor = null)
		{
			if (string.IsNullOrWhiteSpace(backgroundColor)) backgroundColor = HexToCuiColor("#E44028");
			if (string.IsNullOrWhiteSpace(iconSprite)) iconSprite = "assets/icons/close.png";
			if (string.IsNullOrWhiteSpace(iconColor)) iconColor = HexToCuiColor("#E2DBD3");
			
			container.Add(new CuiPanel
			{
				Image =
				{
					Color = backgroundColor
				},
				RectTransform =
				{
					AnchorMin = closeButtonAnchorMin,
					AnchorMax = closeButtonAnchorMax,
					OffsetMin = closeButtonOffsetMin,
					OffsetMax = closeButtonOffsetMax
				}
			}, closeButtonParent, closeButtonName);

			container.Add(new CuiElement()
			{
				Parent = closeButtonName,
				Components =
				{
					new CuiImageComponent()
					{
						Sprite = iconSprite,
						Color = iconColor
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 10", OffsetMax = "-10 -10"
					}
				}
			});

			container.Add(new CuiElement()
			{
				Parent = closeButtonName,
				Components =
				{
					new CuiButtonComponent()
					{
						Color = "0 0 0 0",
						Close = closeLayer,
						Command = commandOnClose
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "0 0", AnchorMax = "1 1"
					}
				}
			});
		}

		private static void UpdateUI(BasePlayer player, Action<CuiElementContainer> callback = null)
		{
			var container = new CuiElementContainer();
			callback?.Invoke(container);
			CuiHelper.AddUi(player, container);
		}

		#endregion UI.Components

		#endregion Interface

		#region Utils

		private void StartShowMenu(BasePlayer player, MenuCategory category)
		{
			if (!_openedMenus.ContainsKey(player.userID))
				_openedMenus.TryAdd(player.userID, new OpenedMenu(player, category));

			ShowMenuUI(player);
		}

		#region Opened Menu

		private Dictionary<ulong, OpenedMenu> _openedMenus = new();

		private class OpenedMenu
		{
			#region Fields

			public BasePlayer Player;

			public int SelectedCategory;

			public int PageIndex;

			private Timer updateTimer = null;

			#endregion

			#region Initialization

			public OpenedMenu(BasePlayer player, MenuCategory targetCategory)
			{
				Player = player;

				SelectedCategory = targetCategory.ID;

				PageIndex = 0;

				if (_headerFieldsData.needToUpdate)
					updateTimer = Instance?.timer.Every(1f, UpdateHeader);
			}

			#endregion

			#region Public Methods

			public bool isEditMode = false;

			public void OnChangeEditMode()
			{
				if (!CanPlayerEdit(Player)) return;

				isEditMode = !isEditMode;
			}

			public void OnSelectCategory(int category)
			{
				SelectedCategory = category;

				PageIndex = 0;
			}

			public void OnSelectPage(int pageIndex)
			{
				PageIndex = pageIndex;
			}

			public int GetLastPage()
			{
				var category = Instance.GetCategoryById(SelectedCategory);
				if (category.Pages.Count == 0) return 0;

				return category.Pages.Count - 1;
			}

			public void UpdateContent()
			{
				UpdateUI(Player,
					container =>
						_templateData.ShowContentUI(Player, container, $"{CmdMainConsole} menu page"));
			}

			#endregion

			#region Private Methods

			private void UpdateHeader()
			{
				if (Player != null)
				{
					_templateData?.ShowUpdateHeaderUI(Player);
				}
			}

			#endregion

			#region Destroy

			public void OnDestroy()
			{
				updateTimer?.Destroy();
			}

			#endregion
		}

		private static OpenedMenu GetOpenedMenu(ulong player)
		{
			return Instance._openedMenus.GetValueOrDefault(player);
		}

		private static bool TryGetOpenedMenu(ulong player, out OpenedMenu openedMenu)
		{
			return Instance._openedMenus.TryGetValue(player, out openedMenu);
		}

		private void RemoveOpenedMenu(ulong player)
		{
			if (_openedMenus.TryGetValue(player, out var menu))
				menu.OnDestroy();

			_openedMenus.Remove(player);
		}

		#endregion

		#region Update Fields

		private void LoadUpdateFields()
		{
			var dict = new Dictionary<string, Func<BasePlayer, string>>
			{
				{"{online_players}", GetOnlinePlayers},
				{"{sleeping_players}", GetSleepingPlayers},
				{"{all_players}", GetAllPlayers},
				{"{max_players}", GetMaxPlayers},
				{"{player_kills}", GetPlayerKills},
				{"{player_deaths}", GetPlayerDeaths},
				{"{player_username}", GetPlayerUsername},
				{"{player_avatar}", GetPlayerAvatar}
			};

			_config.EconomyFields.ForEach(economyField =>
			{
				if (!economyField.Enabled)
					return;

				if (dict.ContainsKey(economyField.UpdateKey))
				{
					PrintError($"{economyField.UpdateKey} already defined!");
					return;
				}

				dict.Add(economyField.UpdateKey, player => economyField.Economy.ShowBalance(player).ToString());
			});

			_headerUpdateFields = dict;
		}

		private string FormatUpdateField(BasePlayer player, string updateField)
		{
			var sb = Pool.Get<StringBuilder>();
			
			try
			{
				sb.Clear().Append(updateField);
				
				foreach (var updateInfo in _headerUpdateFields)
					sb.Replace(updateInfo.Key, updateInfo.Value(player));
				
				return sb.ToString();
			}
			finally
			{
				Pool.FreeUnmanaged(ref sb);
			}
		}
		
		#region Actions

		private string GetOnlinePlayers(BasePlayer player)
		{
			return BasePlayer.activePlayerList.Count.ToString();
		}

		private string GetSleepingPlayers(BasePlayer player)
		{
			return BasePlayer.sleepingPlayerList.Count.ToString();
		}

		private string GetAllPlayers(BasePlayer player)
		{
			return (BasePlayer.activePlayerList.Count + BasePlayer.sleepingPlayerList.Count).ToString();
		}

		private string GetMaxPlayers(BasePlayer player)
		{
			return ConVar.Server.maxplayers.ToString();
		}

		private string GetPlayerUsername(BasePlayer player)
		{
			return player.displayName;
		}

		private string GetPlayerAvatar(BasePlayer player)
		{
			return $"avatar_{player.UserIDString}";
		}

		private string GetPlayerKills(BasePlayer player)
		{
			if (KillRecords != null) return Convert.ToString(KillRecords.Call("GetKillRecord", player.UserIDString, "baseplayer"));
			if (Statistics != null) return Convert.ToString(Statistics.Call("GetStatsValue", player.userID.Get(), "kills"));

			return 0.ToString();
		}

		private string GetPlayerDeaths(BasePlayer player)
		{
			if (KillRecords != null) return Convert.ToString(KillRecords.Call("GetKillRecord", player.UserIDString, "death"));

			if (Statistics != null) return Convert.ToString(Statistics.Call("GetStatsValue", player.userID.Get(), "deaths"));

			return 0.ToString();
		}

		#endregion

		#endregion

		#region Editing

		private List<(string FlagPath, string LangKey, string LangName)> _langList = new()
		{
			("assets/icons/flags/af.png", "af", "Afrikaans"),
			("assets/icons/flags/ar.png", "ar", "العربية"),
			("assets/icons/flags/ca.png", "ca", "Català"),
			("assets/icons/flags/cs.png", "cs", "Čeština"),
			("assets/icons/flags/da.png", "da", "Dansk"),
			("assets/icons/flags/de.png", "de", "Deutsch"),
			("assets/icons/flags/el.png", "el", "Ελληνικά"),
			("assets/icons/flags/en-pt.png", "en-PT", "Portuguese (Portugal)"),
			("assets/icons/flags/en.png", "en", "English"),
			("assets/icons/flags/es-es.png", "es-ES", "Español (España)"),
			("assets/icons/flags/fi.png", "fi", "Suomi"),
			("assets/icons/flags/fr.png", "fr", "Français"),
			("assets/icons/flags/he.png", "he", "עברית"),
			("assets/icons/flags/hu.png", "hu", "Magyar"),
			("assets/icons/flags/it.png", "it", "Italiano"),
			("assets/icons/flags/ja.png", "ja", "日本語"),
			("assets/icons/flags/ko.png", "ko", "한국어"),
			("assets/icons/flags/nl.png", "nl", "Nederlands"),
			("assets/icons/flags/no.png", "no", "Norsk"),
			("assets/icons/flags/pl.png", "pl", "Polski"),
			("assets/icons/flags/pt-br.png", "pt-BR", "Português (Brasil)"),
			("assets/icons/flags/pt-pt.png", "pt-PT", "Português (Portugal)"),
			("assets/icons/flags/ro.png", "ro", "Română"),
			("assets/icons/flags/ru.png", "ru", "Русский"),
			("assets/icons/flags/sr.png", "sr", "Српски"),
			("assets/icons/flags/sv-se.png", "sv-SE", "Svenska"),
			("assets/icons/flags/tr.png", "tr", "Türkçe"),
			("assets/icons/flags/uk.png", "uk", "Українська"),
			("assets/icons/flags/vi.png", "vi", "Tiếng Việt"),
			("assets/icons/flags/zh-cn.png", "zh-CN", "中文 (简体)"),
			("assets/icons/flags/zh-tw.png", "zh-TW", "中文 (繁體)")
		};

		#region Edit Page

		private Dictionary<ulong, EditPageData> editPages = new();

		private class EditPageData
		{
			#region Page

			public ulong playerID;

			public int Category;

			public int Page;

			public CategoryPage categoryPage;

			public static EditPageData Create(BasePlayer player, int categoryID, int page)
			{
				var data = new EditPageData()
				{
					playerID = player.userID,
					Category = categoryID,
					Page = page,
					categoryPage = Instance.GetCategoryById(categoryID).Pages[page]
				};

				Instance?.editPages.TryAdd(player.userID, data);

				return data;
			}

			public static EditPageData Get(ulong playerID)
			{
				return Instance?.editPages.TryGetValue(playerID, out var data) == true ? data : null;
			}

			public void Save()
			{
				var category = Instance?.GetCategoryById(Category);
				if (category != null)
					category.Pages[Page] = categoryPage;

				Instance?.editPages?.Remove(playerID);

				Instance?.SaveData();
			}

			#endregion

			#region Edit Element

			public UiElement editingElement = null;

			public string editingElementParent, editingElementName;

			public int elementIndex;

			public bool StartEditElement(int elementID, string parent)
			{
				elementIndex = elementID;

				editingElementParent = parent;

				if (elementID >= 0 && elementID < categoryPage.Elements.Count)
				{
					editingElement = categoryPage.Elements[elementID];

					editingElementName = editingElement.Name;
				}

				return editingElement != null;
			}

			public void EndEditElement(bool cancel = false)
			{
				if (cancel)
				{
					editingElement = null;
					return;
				}

				categoryPage.Elements[elementIndex] = editingElement;
			}

			public void UpdateEditElement(ref CuiElementContainer container, BasePlayer player, bool isRename = false)
			{
				if (isRename)
					editingElement.Get(ref container, player, editingElementParent, editingElement.Name,
						editingElementName);
				else
					editingElement.Get(ref container, player, editingElementParent, editingElement.Name,
						needUpdate: true);
			}


			public void OnEditElementSave()
			{
				Instance?.SaveCategoriesData();
			}

			public (UiElement uiElement, string parent) OnEditElementStartEdit()
			{
				return (editingElement, editingElementParent);
			}

			public void OnEditElementStopEdit(UiElement uiElement)
			{
				categoryPage.Elements[elementIndex] = uiElement;
			}

			#endregion

			#region Edit Text

			public void OnStartTextEditing()
			{
			}

			public void OnStopTextEditing()
			{
				if (BasePlayer.TryFindByID(playerID, out var player))
					UpdateUI(player, container => UpdateEditElement(ref container, player));
			}

			#endregion Edit Text

			#region Change Position

			public void OnChangePosition()
			{
				if (BasePlayer.TryFindByID(playerID, out var player))
					Instance.ShowPageEditorPanel(player);
			}

			#endregion
		}

		#endregion

		#region Edit Header Fields

		private Dictionary<ulong, EditHeaderFieldsData> editHeaderFields = new();

		private class EditHeaderFieldsData
		{
			#region Page

			public ulong playerID;

			public List<HeaderFieldUI> HeaderFields = new();

			public static void Create(BasePlayer player)
			{
				var data = new EditHeaderFieldsData()
				{
					playerID = player.userID,
					HeaderFields = _headerFieldsData.Fields
				};

				Instance?.editHeaderFields.TryAdd(player.userID, data);
			}

			public static EditHeaderFieldsData Get(ulong playerID)
			{
				return Instance?.editHeaderFields.TryGetValue(playerID, out var data) == true ? data : null;
			}

			public void Save()
			{
				Instance?.editHeaderFields?.Remove(playerID);

				Instance?.SaveHeaderFieldsData();

				Instance?.LoadHeaderFieldsDataCache();
			}

			#endregion

			#region Edit Element

			public HeaderFieldUI editingElement = null;

			public string editingElementParent, editingElementName;

			public int elementIndex;

			public bool StartEditElement(int elementID, string parent)
			{
				editingElement = null;
				editingElementName = null;

				elementIndex = elementID;

				editingElementParent = parent;

				if (elementID >= 0 && elementID < HeaderFields.Count)
				{
					editingElement = HeaderFields[elementID];

					editingElementName = editingElement.Name;
				}

				return editingElement != null;
			}

			public void EndEditElement(bool cancel = false)
			{
				if (cancel)
				{
					editingElement = null;
					return;
				}

				HeaderFields[elementIndex] = editingElement;

				editingElement = null;
				editingElementParent = null;
				editingElementName = null;
				elementIndex = default;
			}

			public void UpdateEditElement(ref CuiElementContainer container, BasePlayer player, bool isRename = false)
			{
				if (isRename)
					editingElement.Get(ref container, player, editingElementParent, editingElement.Name,
						editingElementName);
				else
					editingElement.Get(ref container, player, editingElementParent, editingElement.Name,
						needUpdate: true);
			}

			public void OnEditElementSave()
			{
				Instance?.SaveHeaderFieldsData();
			}

			public (UiElement uiElement, string parent) OnEditElementStartEdit()
			{
				return (editingElement, editingElementParent);
			}

			public void OnEditElementStopEdit(UiElement uiElement)
			{
				HeaderFields[elementIndex] = new HeaderFieldUI(uiElement, editingElement.NeedToUpdate);
			}

			#endregion

			#region Edit Text

			public void OnStartTextEditing()
			{
			}

			public void OnStopTextEditing()
			{
				Instance?.SaveHeaderFieldsData();

				Instance?.LoadHeaderFieldsDataCache();

				if (BasePlayer.TryFindByID(playerID, out var player))
					UpdateUI(player, container => _templateData?.UpdateGlobalHeaderUI(player, container));
			}

			#endregion Edit Text

			#region Change Position

			public void OnChangePosition()
			{
				if (BasePlayer.TryFindByID(playerID, out var player))
					Instance.ShowHeaderFieldsEditorPanel(player);
			}

			#endregion
		}

		#endregion

		#region Edit Category

		private Dictionary<ulong, EditCategoryData> editMenuCategories = new();

		private class EditCategoryData
		{
			#region Fields

			public ulong playerID;

			public int MenuCategoryID;

			public MenuCategory menuCategory;

			public bool NeedCreate = false;

			#endregion

			#region Public Methods

			public static void Create(BasePlayer player, int menuCategoryID, bool needCreate = false)
			{
				var targetCategory = needCreate
					? MenuCategory.GetDefault()
					: Instance?.GetCategoryById(menuCategoryID);

				if (targetCategory == null)
				{
					Instance?.PrintError($"Error: Can't find category with id {menuCategoryID}");
					return;
				}

				var data = new EditCategoryData()
				{
					playerID = player.userID,
					MenuCategoryID = menuCategoryID,
					menuCategory = targetCategory,
					NeedCreate = needCreate
				};

				Instance?.editMenuCategories?.TryAdd(player.userID, data);
			}

			public static EditCategoryData Get(ulong playerID)
			{
				return Instance?.editMenuCategories?.TryGetValue(playerID, out var data) == true ? data : null;
			}

			public static bool Remove(ulong playerID)
			{
				return Instance?.editMenuCategories?.Remove(playerID) ?? true;
			}

			public void Save()
			{
				if (NeedCreate)
				{
					_categoriesData.Categories.Add(menuCategory);
				}
				else
				{
					var targetIndex = _categoriesData.Categories.FindIndex(x => x.ID == MenuCategoryID);
					_categoriesData.Categories[targetIndex] = menuCategory;
				}
				
				Remove(playerID);

				Instance?.LoadCategories();
				
				Instance?.SaveData();
			}

			public void Remove()
			{
				if (!NeedCreate)
					_categoriesData?.Categories?.RemoveAll(x => x.ID == MenuCategoryID);

				Remove(playerID);
				
				Instance?.LoadCategories();
				
				Instance?.SaveData();
			}

			#endregion Public Methods

			#region Array

			public object[] editableArray;

			public string editableArrayName;

			public void StartEditArray(object[] targetArray, string fieldName)
			{
				editableArray = targetArray;
				editableArrayName = fieldName;
			}

			public void StopEditArray()
			{
				editableArray = null;
				editableArrayName = null;
			}

			public object[] GetEditableArrayValues()
			{
				return editableArray;
			}

			#endregion
		}

		#endregion Edit Category

		#region Edit UI Element

		private Dictionary<ulong, EditUiElementData> editUiElement = new();

		private class EditUiElementData
		{
			#region Fields

			public ulong playerID;

			public int elementIndex;

			public Action OnSave, OnStartTextEditing, OnStopTextEditing, OnChangePosition;

			public Func<(UiElement uiElement, string parent)> startEditElement;

			public Action<UiElement> onStopEditElement;

			public static void Create(BasePlayer player,
				int elementIndex,
				Action onSave,
				Func<(UiElement uiElement, string parent)> startEditElement,
				Action<UiElement> stopEditElement,
				Action onStartTextEditing = null,
				Action onStopTextEditing = null,
				Action onChangePosition = null)
			{
				var data = new EditUiElementData()
				{
					playerID = player.userID,
					elementIndex = elementIndex,
					OnSave = onSave,
					startEditElement = startEditElement,
					onStopEditElement = stopEditElement,
					OnStartTextEditing = onStartTextEditing,
					OnStopTextEditing = onStopTextEditing,
					OnChangePosition = onChangePosition,
				};

				data.StartEditElement();

				Instance?.editUiElement.TryAdd(player.userID, data);
			}

			public static EditUiElementData Get(ulong playerID)
			{
				return Instance?.editUiElement.TryGetValue(playerID, out var data) == true ? data : null;
			}
			
			public static void Remove(ulong playerID)
			{
				Instance?.editUiElement?.Remove(playerID);
			}

			public void Save()
			{
				OnSave?.Invoke();

				Remove(playerID);
			}

			#endregion

			#region Edit Element

			public UiElement editingElement = null;

			public string editingElementParent, editingElementName;

			public float movementStep;

			public bool ExpertMode;

			public void StartEditElement()
			{
				var targetElement = startEditElement?.Invoke();
				if (targetElement == null)
				{
					return;
				}

				editingElement = targetElement.Value.uiElement;
				editingElementParent = targetElement.Value.parent;

				editingElementName = editingElement.Name;
				
				CreateEditableOutline();
			}

			public void EndEditElement(bool cancel = false)
			{
				DestroyEditableOutline();
				
				if (cancel)
				{
					editingElement = null;
					return;
				}

				onStopEditElement?.Invoke(editingElement);

				Save();
			}
			
			public void SetMovementStep(float step)
			{
				movementStep = step;
			}

			public void UpdateEditElement(ref CuiElementContainer container, BasePlayer player, 
				bool needAddImage = false,
				bool needUpdate = false)
			{
				editingElement.InvalidateCache();
				
				if (needAddImage && editingElement.Type == CuiElementType.Image && editingElement.TryGetImage(out var image))
					Instance?.AddImage(image, image);

				editingElement.Get(ref container, player, editingElementParent, editingElement.Name,
					editingElementName, needUpdate: needUpdate);
			}

			#endregion

			#region Outline

			private void CreateEditableOutline()
			{
				if (!BasePlayer.TryFindByID(playerID, out var player)) return;
				
				UpdateUI(player, container =>
				{
					container.Add(new CuiElement()
					{
						Parent = editingElement.Name ?? string.Empty,
						Name = EditingElementOutline,
						DestroyUi = EditingElementOutline,
						Components =
						{
							new CuiImageComponent()
							{
								Sprite = "Assets/Content/UI/UI.Box.tga",
								Color = HexToCuiColor("#71B8ED"),
								ImageType = Image.Type.Tiled
							},
							new CuiRectTransformComponent()
							{
								AnchorMin = "0 0", AnchorMax = "1 1"
							}
						}
					});
				});
			}

			private void DestroyEditableOutline()
			{
				if (!BasePlayer.TryFindByID(playerID, out var player)) return;
				
				CuiHelper.DestroyUi(player, EditingElementOutline);
			}

			#endregion

			#region Edit Text

			public bool isTextEditing = false, isFormattingEnabled = true;
			
			private Dictionary<string, List<string>> _editingText = new();

			public void StartTextEditing()
			{
				isTextEditing = true;
				
				LoadEditingText();
				
				OnStartTextEditing?.Invoke();
			}

			public void StopTextEditing()
			{
				isTextEditing = false;

				OnStopTextEditing?.Invoke();
				
				_editingText.Clear();
			}

			public void CloseTextEditingWithoutSaving()
			{
				StopTextEditing();
			}
			
			public void SaveTextEditingChanges()
			{
				if (_editingText != null)
				{
					SaveEditingText();
					
					Instance?.SaveData();
				}
				
				StopTextEditing();
			}

			public void ToggleTextFormatting()
			{
				isFormattingEnabled = !isFormattingEnabled;
			}
			
			private void LoadEditingText()
			{
				_editingText.Clear();
				
				_editingText["en"] = editingElement.Text.ToList();
				
				if (_localizationData.Localization.Elements.TryGetValue(editingElement.Name,
					    out var elementLocalization))
					foreach (var (langKey, text) in elementLocalization.Messages) _editingText[langKey] = text.Text.ToList();
			}

			private void SaveEditingText()
			{
				foreach (var (langKey, text) in _editingText) 
					SaveTextForLang(langKey, text);
			}

			#region Lang

			private string _targetLang;

			public void SelectLang(string langKey)
			{
				_targetLang = langKey;
			}

			public bool IsSelectedLang(string langKey)
			{
				if (string.IsNullOrWhiteSpace(_targetLang) || _targetLang == "en")
					return langKey == "en";

				return langKey == _targetLang;
			}
			
			#endregion Lang

			public List<string> GetEditableText()
			{
				return !string.IsNullOrWhiteSpace(_targetLang) && _editingText.TryGetValue(_targetLang, out var text) ? text : _editingText["en"];
			}
			
			public List<string> GetText()
			{
				if (string.IsNullOrWhiteSpace(_targetLang) || _targetLang == "en")
					return editingElement.Text;

				if (_localizationData.Localization.Elements.TryGetValue(editingElement.Name,
					    out var elementLocalization) &&
				    elementLocalization.Messages.TryGetValue(_targetLang, out var langValue))
					return langValue.Text;

				return editingElement.Text;
			}

			public void SaveTextForLang(List<string> text)
			{
				if (string.IsNullOrWhiteSpace(_targetLang) || _targetLang == "en")
				{
					_editingText["en"] = text;
				}
				else
				{
					_editingText[_targetLang] = text;
				}
			}

			public void SaveTextForLang(string targetLang, List<string> text)
			{
				if (string.IsNullOrWhiteSpace(targetLang) || targetLang == "en")
				{
					editingElement.Text = text;
				}
				else
				{
					if (_localizationData.Localization.Elements.TryGetValue(editingElement.Name,
						    out var elementLocalization))
					{
						elementLocalization.Messages[targetLang] = new LocalizationSettings.LocalizationInfo()
						{
							Text = text
						};
					}
					else
					{
						_localizationData.Localization.Elements[editingElement.Name] =
							new LocalizationSettings.ElementLocalization
							{
								Messages = new Dictionary<string, LocalizationSettings.LocalizationInfo>
								{
									[targetLang] = new()
									{
										Text = text
									}
								}
							};
					}
				}
			}

			public bool HasLang(string langKey)
			{
				if (string.IsNullOrWhiteSpace(langKey) || langKey == "en")
					return true;

				return _localizationData.Localization.Elements.TryGetValue(editingElement.Name,
					       out var elementLocalization) &&
				       elementLocalization.Messages.ContainsKey(langKey);
			}

			public void RemoveLang(string langKey)
			{
				if (string.IsNullOrWhiteSpace(langKey) || langKey == "en")
				{
					editingElement.Text = new List<string>();
				}
				else
				{
					if (_localizationData.Localization.Elements.TryGetValue(editingElement.Name,
						    out var elementLocalization))
						elementLocalization.Messages?.Remove(langKey);
				}

				if (_targetLang == langKey)
					SelectLang(default);
			}

			#endregion Edit Text
		}

		#endregion Edit UI Element

		#endregion

		#region Working with Images
		
		private void AddImage(string url, string fileName, ulong imageId = 0)
		{
#if CARBON
			imageDatabase.Queue(true, new Dictionary<string, string>
			{
				[fileName] = url
			});
#else
			ImageLibrary?.Call("AddImage", url, fileName, imageId);
#endif
		}

		private string GetImage(string name)
		{
#if CARBON
			return imageDatabase.GetImageString(name);
#else
			return Convert.ToString(ImageLibrary?.Call("GetImage", name));
#endif
		}

		private bool HasImage(string name)
		{
#if CARBON
			return Convert.ToBoolean(imageDatabase.HasImage(name));
#else
			return Convert.ToBoolean(ImageLibrary?.Call("HasImage", name));
#endif
		}

		private void LoadImages()
		{
#if CARBON
			imageDatabase = BaseModule.GetModule<ImageDatabaseModule>();
#endif
			
			_enabledImageLibrary = true;

			var imagesList = new Dictionary<string, string>
			{
				["ServerPanel_Editor_Btn_Remove"] = "https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/serverpanel-editor-icon-remove.png",
				["ServerPanel_Editor_Btn_Clone"] = "https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/serverpanel-editor-icon-clone.png",
				["ServerPanel_Editor_Btn_Edit"] = "https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/serverpanel-editor-icon-edit.png",
				["ServerPanel_Editor_Btn_Up"] = "https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/serverpanel-editor-icon-up.png",
				["ServerPanel_Editor_Btn_Down"] = "https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/serverpanel-editor-icon-down.png",
				["ServerPanel_Editor_Select"] = "https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/serverpanel-editor-icon-select.png",
				["ServerPanel_Editor_EditCategory"] = "https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/serverpanel-editor-icon-category.png",
				["ServerPanel_Editor_Switch_On"] = "https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/serverpanel-editor-icon-switch-on.png",
				["ServerPanel_Editor_Switch_Off"] = "https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/serverpanel-editor-icon-switch-off.png",
				["ServerPanel_Editor_Visible_On"] = "https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/serverpanel-editor-icon-show-on.png",
				["ServerPanel_Editor_Visible_Off"] = "https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/serverpanel-editor-icon-show-off.png",

			};

			foreach (var rectImage in rectToImage.Values)
				RegisterImage(ref imagesList, rectImage, rectImage);

			_templateData.UI?.GetAllUiElements()?.ForEach(uiElement =>
			{
				if (uiElement.TryGetImage(out var img))
				{
					RegisterImage(ref imagesList, img, img);
				}
			});

			_headerFieldsData.Fields?.ForEach(uiElement =>
			{
				if (uiElement.TryGetImage(out var img))
				{
					RegisterImage(ref imagesList, img, img);
				}
			});

			_categoriesData.Categories?.ForEach(category =>
			{
				if (!string.IsNullOrEmpty(category.Icon) && category.Icon.IsURL()) 
					RegisterImage(ref imagesList, category.Icon, category.Icon);

				category.Pages?.ForEach(page =>
				{
					page.Elements?.ForEach(uiElement =>
					{
						if (uiElement.TryGetImage(out var img))
						{
							RegisterImage(ref imagesList, img, img);
						}
					});
				});
			});

#if CARBON
            imageDatabase.Queue(false, imagesList);
#else
			timer.In(1f, () =>
			{
				if (ImageLibrary is not {IsLoaded: true})
				{
					_enabledImageLibrary = false;

					BroadcastILNotInstalled();
					return;
				}

				ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
			});
#endif
		}

		private void RegisterImage(ref Dictionary<string, string> images, string name, string image)
		{
			if (string.IsNullOrEmpty(image) || string.IsNullOrEmpty(name)) return;

			images.TryAdd(name, image);
		}

		private void BroadcastILNotInstalled()
		{
			for (var i = 0; i < 5; i++) PrintError("IMAGE LIBRARY IS NOT INSTALLED.");
		}

		#endregion

		#region Server Loading

		private void LoadCategories()
		{
			_categoriesByID.Clear();
			_categoriesByCommand.Clear();

			for (var index = 0; index < _categoriesData.Categories.Count; index++)
			{
				var menuCategory = _categoriesData.Categories[index];

				_categoriesByID[menuCategory.ID] = index;

				foreach (var menuCommand in menuCategory.Commands)
					_categoriesByCommand[menuCommand] = index;
				
				menuCategory.ProcessCategory();
			}
		}

		private IEnumerator CheckPluginLoaded(Plugin plugin, int categoryId, int maxAttempts)
		{
			var attempts = 0; 

			while (attempts < maxAttempts)
			{
				if (plugin is {IsLoaded: true})
				{
					plugin.Call("OnReceiveCategoryInfo", categoryId);
					_categoriesActiveCoroutines.Remove(categoryId);
					yield break;
				}

				attempts++;
				yield return CoroutineEx.waitForSeconds(1f);
			}

			Puts($"Plugin '{plugin?.Name}' did not load within the expected time for category ID: {categoryId}");
			_categoriesActiveCoroutines.Remove(categoryId);
		}
		
		private void RegisterCommands()
		{
			var openMenuCommands = new HashSet<string>();

			_categoriesData?.Categories?.FindAll(menuCategory => menuCategory.Enabled && !menuCategory.ChatBtn)?.ForEach(menuCategory =>
			{
				foreach (var menuCommand in menuCategory.Commands)
					openMenuCommands.Add(menuCommand);
			});

			if (openMenuCommands.Count > 0)
				AddCovalenceCommand(openMenuCommands.ToArray(), nameof(CmdOpenMenu));
		}

		private void RegisterPermissions()
		{
			var menuPermissions = new HashSet<string>
			{
				Perm_Edit
			};

			_categoriesData?.Categories?.FindAll(menuCategory => menuCategory.Enabled)
				?.ForEach(menuCategory => menuPermissions.Add(menuCategory.Permission));

			foreach (var perm in menuPermissions)
				if (!permission.PermissionExists(perm))
					permission.RegisterPermission(perm, this);
		}

		#endregion

		#region Categories

		private static List<MenuCategory> GetAvailableCategories(ulong player)
		{
			var list = Pool.Get<List<MenuCategory>>();

			if (CanPlayerEdit(player) && TryGetOpenedMenu(player, out var openedMenu) && openedMenu.isEditMode)
			{
				list.AddRange(_categoriesData.Categories);
			}
			else
			{
				for (var i = 0; i < _categoriesData.Categories.Count; i++)
				{
					var category = _categoriesData.Categories[i];
					if (!category.Enabled)
						continue;

					if (!string.IsNullOrEmpty(category.Permission) && !player.HasPermission(category.Permission))
						continue;

					list.Add(category);
				}
			}
			
			return list;
		}

		private static int CountAvailableCategories(ulong player)
		{
			var list = GetAvailableCategories(player);

			try
			{
				return list.Count;
			}
			finally
			{
				Pool.FreeUnmanaged(ref list);
			}
		}

		private MenuCategory GetCategoryById(int categoryID)
		{
			return _categoriesByID.TryGetValue(categoryID, out var categoryIndex)
				? _categoriesData.Categories[categoryIndex]
				: null;
		}

		private MenuCategory GetCategoryByCommand(string categoryName)
		{
			return _categoriesByCommand.TryGetValue(categoryName, out var categoryIndex)
				? _categoriesData.Categories[categoryIndex]
				: null;
		}

		private int GetUniqueCategoryID()
		{
			int categoryID;
			do
			{
				categoryID = Random.Range(int.MinValue, int.MaxValue);
			} while (_categoriesByID.ContainsKey(categoryID));

			return categoryID;
		}

		#endregion

		#region Other Plugins

		private bool IsServerPanelPlayerRaidBlocked(BasePlayer player)
		{
			return Convert.ToBoolean(NoEscape?.Call("IsRaidBlocked", player) ?? false);
		}

		private bool IsServerPanelPlayerCombatBlocked(BasePlayer player)
		{
			return Convert.ToBoolean(NoEscape?.Call("IsCombatBlocked", player) ?? false);
		}

		#endregion

		private static bool CanPlayerEdit(BasePlayer player)
		{
			return player.HasPermission(Perm_Edit);
		}

		private static bool CanPlayerEdit(ulong player)
		{
			return player.HasPermission(Perm_Edit);
		}

		private static string HexToCuiColor(string hex, float alpha = 100)
		{
			if (string.IsNullOrEmpty(hex)) hex = "#FFFFFF";

			var str = hex.Trim('#');
			if (str.Length != 6) throw new Exception(hex);
			var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
			var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
			var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

			return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {alpha / 100f}";
		}

		#endregion Utils

		#region API

		private void API_OnServerPanelCallClose(BasePlayer player)
		{
			if (player == null) return;

			API_OnServerPanelDestroyUI(player);

			API_OnServerPanelClosed(player);
		}

		private void API_OnServerPanelClosed(BasePlayer player)
		{
			if (player == null) return;

			Interface.CallHook("OnServerPanelClosed", player);

			RemoveOpenedMenu(player.userID);
		}

		private static void API_OnServerPanelDestroyUI(BasePlayer player)
		{
			CuiHelper.DestroyUi(player, Layer);
			CuiHelper.DestroyUi(player, EditingLayerModal);
			CuiHelper.DestroyUi(player, EditingLayerModalColorSelector);
			CuiHelper.DestroyUi(player, EditingLayerModalTextEditor);
		}

		public void API_OnServerPanelSetHeaderFields(List<HeaderFieldUI> targetHeaderFields)
		{
			_headerFieldsData.Fields = targetHeaderFields?.ToList();
		}

		public void API_OnServerPanelSetTemplate(UISettings targetUI)
		{
			_templateData.UI = targetUI;
		}

		public void API_OnServerPanelSetCategories(List<MenuCategory> targetCategories)
		{
			if (targetCategories is null) return;

			_categoriesData.Categories = targetCategories?.ToList();
		}

		public void API_OnServerPanelAddCategory(JObject newCategory)
		{
			if (newCategory == null) 
			{
				PrintError("[API_OnServerPanelAddCategory] Received null category object.");
				return;
			}

			var menuCategory = MenuCategory.FromJson(newCategory);
			if (menuCategory == null)
			{
				PrintError("[API_OnServerPanelAddCategory] Failed to create MenuCategory from JSON.");
				return;
			}

			_categoriesData?.Categories?.Add(menuCategory);

			Puts($"[API_OnServerPanelAddCategory] Category successfully added.");
		}

		public void API_OnServerPanelUpdateText(Dictionary<string, string> targetUpdateFields)
		{
			if (targetUpdateFields == null) return;

			_templateData?.UI?.GetAllUiElements()?.ForEach(uiElement =>
			{
				foreach (var (key, val) in targetUpdateFields)
				{
					if (uiElement.Text?.Count > 0)
					{
						var newText = new List<string>();

						uiElement.Text?.ForEach(targetText => newText.Add(targetText.Replace(key, val)));

						uiElement.Text = newText;
					}

					if (!string.IsNullOrWhiteSpace(uiElement.Image))
						uiElement.Image = uiElement.Image.Replace(key, val);
				}
			});

			_headerFieldsData?.Fields?.ForEach(uiElement =>
			{
				foreach (var (key, val) in targetUpdateFields)
				{
					if (uiElement.Text?.Count > 0)
					{
						var newText = new List<string>();

						uiElement.Text?.ForEach(targetText => newText.Add(targetText.Replace(key, val)));

						uiElement.Text = newText;
					}

					if (!string.IsNullOrWhiteSpace(uiElement.Image))
						uiElement.Image = uiElement.Image.Replace(key, val);
				}
			});

			_categoriesData?.Categories?.ForEach(menuCategory =>
			{
				menuCategory?.Pages?.ForEach(page =>
				{
					if (page.Type == CategoryPage.PageType.UI)
					{
						page.Elements?.ForEach(uiElement =>
						{
							foreach (var (key, val) in targetUpdateFields)
							{
								if (uiElement.Text?.Count > 0)
								{
									var newText = new List<string>();

									uiElement.Text?.ForEach(targetText => newText.Add(targetText.Replace(key, val)));

									uiElement.Text = newText;
								}

								if (!string.IsNullOrWhiteSpace(uiElement.Image))
									uiElement.Image = uiElement.Image.Replace(key, val);
							}
						});
					}
				});
			});
		}

		private void API_OnServerPanelEditorChangePosition(BasePlayer player)
		{
			var data = PlayerData.GetOrCreate(player.UserIDString);
			if (data == null) return;
			
			data.SelectedEditorPosition = (EditorPosition) data.SelectedEditorPosition.Next();
		}
		
		private CuiRectTransformComponent API_OnServerPanelEditorGetPosition(BasePlayer player)
		{
			return PlayerData.GetOrCreate(player.UserIDString)?.GetEditorPosition();
		}

		private void API_OnServerPanelProcessCategory(string pluginName)
		{
			if (string.IsNullOrWhiteSpace(pluginName)) return;
			
			if (_categoriesData == null)
				LoadCategoriesData();

			NextTick(() =>
				{
					if (_categoriesData != null && _categoriesData.Categories.Count > 0)
						foreach (var menuCategory in _categoriesData.Categories)
							menuCategory.ProcessCategory(pluginName);
				}
			);
		}
		
		private void API_OnServerPanelOpenCategoryByID(BasePlayer player, int categoryId)
		{
			if (_categoriesData?.Categories == null || _templateData?.UI == null)
			{
				if (player.IsAdmin)
				{
					SendReply(player, "Plugin is not initialized! Please, contact admin");
				}
				else
				{
					SendReply(player, "Plugin is not initialized! Please, contact admin");
				}
				return;
			}

			var category = GetCategoryById(categoryId);
			if (category == null)
			{
				Reply(player, MsgCantOpenMenuInvalidCommand);
				return;
			}

			if (_config.Block.BlockWhenBuildingBlock && player.IsBuildingBlocked())
			{
				Reply(player, MsgCantOpenMenuBuildingBlock);
				return;
			}

			if (_config.Block.BlockWhenRaidBlock && IsServerPanelPlayerRaidBlocked(player))
			{
				Reply(player, MsgCantOpenMenuRaidBlock);
				return;
			}

			if (_config.Block.BlockWhenCombatBlock && IsServerPanelPlayerCombatBlocked(player))
			{
				Reply(player, MsgCantOpenMenuCombatBlock);
				return;
			}

			StartShowMenu(player, category);
		}
		
		#endregion

		#region Lang

		private const string
			NoILError = "NoILError",
			MsgCantOpenMenuInvalidCommand = "MsgCantOpenMenuInvalidCommand",
			MsgCantOpenMenuBuildingBlock = "MsgCantOpenMenuBuildingBlock",
			MsgCantOpenMenuRaidBlock = "MsgCantOpenMenuRaidBlock",
			MsgCantOpenMenuCombatBlock = "MsgCantOpenMenuCombatBlock";

		protected override void LoadDefaultMessages()
		{
			LoadCategoriesData();

			var messages = new Dictionary<string, string>
			{
				["Economy.Economics.Title"] = "Economics",
				["Economy.Economics.Balance"] = "{0} $",

				["Economy.ServerRewards.Title"] = "Server Rewards",
				["Economy.ServerRewards.Balance"] = "{0} RP",

				["Economy.BankSystem.Title"] = "Bank System",
				["Economy.BankSystem.Balance"] = "{0} $",

				["Economy.IQEconomic.Title"] = "IQEconomic",
				["Economy.IQEconomic.Balance"] = "{0} $",

				["Economy.Scrap.Title"] = "Scrap",
				["Economy.Scrap.Balance"] = "{0} scrap",

				[MsgCantOpenMenuInvalidCommand] =
					"Sorry, you typed the wrong command. Please check the spelling and try again.",
				[MsgCantOpenMenuBuildingBlock] = "You cannot open the menu: you are in a building zone!",
				[MsgCantOpenMenuRaidBlock] = "You can't open the menu: you are raid blocked!",
				[MsgCantOpenMenuCombatBlock] = "You can't open the menu: you are combat blocked!",
				[NoILError] = "The plugin does not work correctly, contact the administrator!",
			};
			
			if (_categoriesData != null && _categoriesData.Categories.Count > 0)
				foreach (var menuCategory in _categoriesData.Categories)
					if (!messages.ContainsKey(menuCategory.Title)) 
						messages.Add(menuCategory.Title, menuCategory.Title);
			
			lang.RegisterMessages(messages, this);
		}

		private string Msg(string key, string userid = null, params object[] obj) =>
			string.Format(lang.GetMessage(key, this, userid), obj);

		private string Msg(BasePlayer player, string key, params object[] obj)
		{
			return Msg(key, player.UserIDString, obj);
		}

		private void Reply(BasePlayer player, string key, params object[] obj)
		{
			SendReply(player, Msg(key, player.UserIDString, obj));
		}

		private void SendNotify(BasePlayer player, string key, int type, params object[] obj)
		{
			if (_config.UseNotify && (Notify != null || UINotify != null))
				Interface.Oxide.CallHook("SendNotify", player, type, Msg(player, key, obj));
			else
				Reply(player, key, obj);
		}

		#endregion

		#region Testing Functions

#if TESTING
		private static void SayDebug(BasePlayer player, string hook, string message)
		{
			Debug.Log($"[ServerPanel | {hook} | {player.UserIDString}] {message}");
		}

		private static void SayDebug(ulong player, string hook, string message)
		{
			Debug.Log($"[ServerPanel | {hook} | {player}] {message}");
		}

		private static void SayDebug(string message)
		{
			Debug.Log($"[ServerPanel] {message}");
		}
#endif

		#endregion
	}
}

#region Extension Methods

namespace Oxide.Plugins.ServerPanelExtensionMethods
{
	// ReSharper disable ForCanBeConvertedToForeach
	// ReSharper disable LoopCanBeConvertedToQuery
	public static class ExtensionMethods
	{
		internal static Permission perm;

		public static bool IsURL(this string uriName)
		{
			return Uri.TryCreate(uriName, UriKind.Absolute, out var uriResult) &&
			       (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
		}

		public static string FormatEscapedRichText(this string val)
		{
			val = Formatter.ToPlaintext(val);

			if (val.Contains("<\u200B"))
				val = val.Replace("<\u200B", "<");
											
			if (val.Contains("\u200B>"))
				val = val.Replace("\u200B>", ">");
			
			return val;
		}
		
		public static Enum Next(this Enum input, params Enum[] ignoredValues)
		{
			var values = Enum.GetValues(input.GetType());
			var ignoredSet = new HashSet<Enum>(ignoredValues);
			int j = Array.IndexOf(values, input) + 1;

			while (j < values.Length && ignoredSet.Contains((Enum)values.GetValue(j)))
			{
				j++;
			}

			return j >= values.Length ? (Enum)values.GetValue(0) : (Enum)values.GetValue(j);
		}

		public static Enum Previous(this Enum input, params Enum[] ignoredValues)
		{
			var values = Enum.GetValues(input.GetType());
			var ignoredSet = new HashSet<Enum>(ignoredValues);
			int j = Array.IndexOf(values, input) - 1;

			while (j >= 0 && ignoredSet.Contains((Enum)values.GetValue(j)))
			{
				j--;
			}

			return j < 0 ? (Enum)values.GetValue(values.Length - 1) : (Enum)values.GetValue(j);
		}

		public static Enum Next(this Enum input)
		{
			var values = Enum.GetValues(input.GetType());
			var j = Array.IndexOf(values, input) + 1;
			return values.Length == j ? (Enum) values.GetValue(0) : (Enum) values.GetValue(j);
		}

		public static Enum Previous(this Enum input)
		{
			var values = Enum.GetValues(input.GetType());
			var j = Array.IndexOf(values, input) - 1;
			return j == -1 ? (Enum) values.GetValue(values.Length - 1) : (Enum) values.GetValue(j);
		}

		public static float Scale(this float oldValue, float oldMin, float oldMax, float newMin, float newMax)
		{
			var oldRange = oldMax - oldMin;
			var newRange = newMax - newMin;
			var newValue = (oldValue - oldMin) * newRange / oldRange + newMin;

			return newValue;
		}

		public static int Scale(this int oldValue, int oldMin, int oldMax, int newMin, int newMax)
		{
			var oldRange = oldMax - oldMin;
			var newRange = newMax - newMin;
			var newValue = (oldValue - oldMin) * newRange / oldRange + newMin;

			return newValue;
		}

		public static long Scale(this long oldValue, long oldMin, long oldMax, long newMin, long newMax)
		{
			var oldRange = oldMax - oldMin;
			var newRange = newMax - newMin;
			var newValue = (oldValue - oldMin) * newRange / oldRange + newMin;

			return newValue;
		}

		public static bool IsHex(this string s)
		{
			return s.Length == 6 && Regex.IsMatch(s, "^[0-9A-Fa-f]+$");
		}


		public static bool All<T>(this IList<T> a, Func<T, bool> b)
		{
			for (var i = 0; i < a.Count; i++)
				if (!b(a[i]))
					return false;
			return true;
		}

		public static int Average(this IList<int> a)
		{
			if (a.Count == 0) return 0;
			var b = 0;
			for (var i = 0; i < a.Count; i++) b += a[i];
			return b / a.Count;
		}

		public static T ElementAt<T>(this IEnumerable<T> a, int b)
		{
			using var c = a.GetEnumerator();
			while (c.MoveNext())
			{
				if (b == 0) return c.Current;
				b--;
			}

			return default;
		}

		public static bool Exists<T>(this IEnumerable<T> a, Func<T, bool> b = null)
		{
			using var c = a.GetEnumerator();
			while (c.MoveNext())
				if (b == null || b(c.Current))
					return true;

			return false;
		}

		public static T FirstOrDefault<T>(this IEnumerable<T> a, Func<T, bool> b = null)
		{
			using (var c = a.GetEnumerator())
			{
				while (c.MoveNext())
					if (b == null || b(c.Current))
						return c.Current;
			}

			return default;
		}

		public static int RemoveAll<T, V>(this IDictionary<T, V> a, Func<T, V, bool> b)
		{
			var c = new List<T>();
			using (var d = a.GetEnumerator())
			{
				while (d.MoveNext())
					if (b(d.Current.Key, d.Current.Value))
						c.Add(d.Current.Key);
			}

			c.ForEach(e => a.Remove(e));
			return c.Count;
		}

		public static IEnumerable<V> Select<T, V>(this IEnumerable<T> a, Func<T, V> b)
		{
			var c = new List<V>();
			using var d = a.GetEnumerator();
			while (d.MoveNext()) c.Add(b(d.Current));

			return c;
		}

		public static List<TResult> Select<T, TResult>(this List<T> source, Func<T, TResult> selector)
		{
			if (source == null || selector == null) return new List<TResult>();

			var r = new List<TResult>(source.Count);
			for (var i = 0; i < source.Count; i++) r.Add(selector(source[i]));

			return r;
		}

		public static List<T> SkipAndTake<T>(this List<T> source, int skip, int take)
		{
			var index = Mathf.Min(Mathf.Max(skip, 0), source.Count);
			return source.GetRange(index, Mathf.Min(take, source.Count - index));
		}

		public static string[] Skip(this string[] a, int count)
		{
			if (a.Length == 0) return Array.Empty<string>();
			var c = new string[a.Length - count];
			var n = 0;
			for (var i = 0; i < a.Length; i++)
			{
				if (i < count) continue;
				c[n] = a[i];
				n++;
			}

			return c;
		}

		public static List<T> Skip<T>(this IList<T> source, int count)
		{
			if (count < 0)
				count = 0;

			if (source == null || count > source.Count)
				return new List<T>();

			var result = new List<T>(source.Count - count);
			for (var i = count; i < source.Count; i++)
				result.Add(source[i]);
			return result;
		}

		public static T[] SkipLast<T>(this T[] source, int count)
		{
			if (source == null)
				return Array.Empty<T>();

			var length = source.Length;
			if (count <= 0 || length <= count)
				return Array.Empty<T>();

			var result = new T[length - count];
			Array.Copy(source, 0, result, 0, length - count);
			return result;
		}
		
		public static Dictionary<T, V> Skip<T, V>(
			this IDictionary<T, V> source,
			int count)
		{
			var result = new Dictionary<T, V>();
			using var iterator = source.GetEnumerator();
			for (var i = 0; i < count; i++)
				if (!iterator.MoveNext())
					break;

			while (iterator.MoveNext()) result.Add(iterator.Current.Key, iterator.Current.Value);

			return result;
		}

		public static List<T> Take<T>(this IList<T> a, int b)
		{
			var c = new List<T>();
			for (var i = 0; i < a.Count; i++)
			{
				if (c.Count == b) break;
				c.Add(a[i]);
			}

			return c;
		}

		public static Dictionary<T, V> Take<T, V>(this IDictionary<T, V> a, int b)
		{
			var c = new Dictionary<T, V>();
			foreach (var f in a)
			{
				if (c.Count == b) break;
				c.Add(f.Key, f.Value);
			}

			return c;
		}

		public static Dictionary<T, V> ToDictionary<S, T, V>(this IEnumerable<S> a, Func<S, T> b, Func<S, V> c)
		{
			var d = new Dictionary<T, V>();
			using var e = a.GetEnumerator();
			while (e.MoveNext()) d[b(e.Current)] = c(e.Current);

			return d;
		}

		public static List<T> ToList<T>(this IEnumerable<T> a)
		{
			var b = new List<T>();

			using var c = a.GetEnumerator();
			while (c.MoveNext()) b.Add(c.Current);

			return b;
		}

		public static T[] ToArray<T>(this IEnumerable<T> a)
		{
			var b = new List<T>();

			using (var c = a.GetEnumerator())
				while (c.MoveNext())
					b.Add(c.Current);

			return b.ToArray();
		}

		public static T[] ToArray<T>(this HashSet<T> source)
		{
			var array = new T[source.Count];

			var index = 0;
			foreach (var item in source)
				array[index++] = item;

			return array;
		}

		public static HashSet<T> ToHashSet<T>(this IEnumerable<T> a)
		{
			return new HashSet<T>(a);
		}

		public static List<T> Where<T>(this List<T> source, Predicate<T> predicate)
		{
			if (source == null)
				return new List<T>();

			if (predicate == null)
				return new List<T>();

			return source.FindAll(predicate);
		}

		public static List<T> Where<T>(this List<T> source, Func<T, int, bool> predicate)
		{
			if (source == null)
				return new List<T>();

			if (predicate == null)
				return new List<T>();

			var r = new List<T>();
			for (var i = 0; i < source.Count; i++)
				if (predicate(source[i], i))
					r.Add(source[i]);
			return r;
		}

		public static List<T> Where<T>(this IEnumerable<T> source, Func<T, bool> predicate)
		{
			var c = new List<T>();

			using (var d = source.GetEnumerator())
			{
				while (d.MoveNext())
					if (predicate(d.Current))
						c.Add(d.Current);
			}

			return c;
		}

		public static List<T> OfType<T>(this IEnumerable<BaseNetworkable> a) where T : BaseEntity
		{
			var b = new List<T>();
			using var c = a.GetEnumerator();
			while (c.MoveNext())
				if (c.Current is T entity)
					b.Add(entity);

			return b;
		}

		public static int Sum<T>(this IList<T> a, Func<T, int> b)
		{
			var c = 0;
			for (var i = 0; i < a.Count; i++)
			{
				var d = b(a[i]);
				if (!float.IsNaN(d)) c += d;
			}

			return c;
		}

		public static T LastOrDefault<T>(this List<T> source)
		{
			if (source == null || source.Count == 0)
				return default;

			return source[^1];
		}

		public static int Count<T>(this List<T> source, Func<T, bool> predicate)
		{
			if (source == null)
				return 0;

			if (predicate == null)
				return 0;

			var count = 0;
			for (var i = 0; i < source.Count; i++)
				checked
				{
					if (predicate(source[i])) count++;
				}

			return count;
		}

		public static TAccumulate Aggregate<TSource, TAccumulate>(this List<TSource> source, TAccumulate seed,
			Func<TAccumulate, TSource, TAccumulate> func)
		{
			if (source == null) throw new Exception("Aggregate: source is null");

			if (func == null) throw new Exception("Aggregate: func is null");

			var result = seed;
			for (var i = 0; i < source.Count; i++) result = func(result, source[i]);
			return result;
		}

		public static int Sum(this IList<int> a)
		{
			var c = 0;
			for (var i = 0; i < a.Count; i++)
			{
				var d = a[i];
				if (!float.IsNaN(d)) c += d;
			}

			return c;
		}

		public static bool HasPermission(this string userID, string b)
		{
			perm ??= Interface.Oxide.GetLibrary<Permission>();
			return !string.IsNullOrEmpty(userID) && (string.IsNullOrEmpty(b) || perm.UserHasPermission(userID, b));
		}

		public static bool HasPermission(this BasePlayer a, string b)
		{
			return a.UserIDString.HasPermission(b);
		}

		public static bool HasPermission(this ulong a, string b)
		{
			return a.ToString().HasPermission(b);
		}

		public static bool IsReallyConnected(this BasePlayer a)
		{
			return a.IsReallyValid() && a.net.connection != null;
		}

		public static bool IsKilled(this BaseNetworkable a)
		{
			return (object) a == null || a.IsDestroyed;
		}

		public static bool IsNull<T>(this T a) where T : class
		{
			return a == null;
		}

		public static bool IsNull(this BasePlayer a)
		{
			return (object) a == null;
		}

		public static bool IsReallyValid(this BaseNetworkable a)
		{
			return !((object) a == null || a.IsDestroyed || a.net == null);
		}

		public static void SafelyKill(this BaseNetworkable a)
		{
			if (a.IsKilled()) return;
			a.Kill();
		}

		public static bool CanCall(this Plugin o)
		{
			return o is {IsLoaded: true};
		}

		public static bool IsInBounds(this OBB o, Vector3 a)
		{
			return o.ClosestPoint(a) == a;
		}

		public static bool IsHuman(this BasePlayer a)
		{
			return !(a.IsNpc || !a.userID.IsSteamId());
		}

		public static BasePlayer ToPlayer(this IPlayer user)
		{
			return user.Object as BasePlayer;
		}

		public static List<TResult> SelectMany<TSource, TResult>(this List<TSource> source,
			Func<TSource, List<TResult>> selector)
		{
			if (source == null || selector == null)
				return new List<TResult>();

			var result = new List<TResult>(source.Count);
			source.ForEach(i => selector(i).ForEach(j => result.Add(j)));
			return result;
		}

		public static IEnumerable<TResult> SelectMany<TSource, TResult>(
			this IEnumerable<TSource> source,
			Func<TSource, IEnumerable<TResult>> selector)
		{
			using var item = source.GetEnumerator();
			while (item.MoveNext())
			{
				using var result = selector(item.Current).GetEnumerator();
				while (result.MoveNext()) yield return result.Current;
			}
		}

		public static int Sum<TSource>(this IEnumerable<TSource> source, Func<TSource, int> selector)
		{
			var sum = 0;

			using var element = source.GetEnumerator();
			while (element.MoveNext()) sum += selector(element.Current);

			return sum;
		}

		public static double Sum<TSource>(this IEnumerable<TSource> source, Func<TSource, double> selector)
		{
			var sum = 0.0;

			using var element = source.GetEnumerator();
			while (element.MoveNext()) sum += selector(element.Current);

			return sum;
		}

		public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
		{
			if (source == null) return false;

			using var element = source.GetEnumerator();
			while (element.MoveNext())
				if (predicate(element.Current))
					return true;

			return false;
		}

		public static string GetFieldTitle<T>(this string field)
		{
			var fieldInfo = typeof(T).GetField(field);
			return fieldInfo == null ? field : GetFieldTitle(fieldInfo);
		}

		public static string GetFieldTitle(this FieldInfo fieldInfo)
		{
			var jsonAttribute = fieldInfo.GetCustomAttribute<JsonPropertyAttribute>();
			return jsonAttribute == null ? string.Empty : jsonAttribute.PropertyName;
		}


		public static bool MoveDown<T>(this List<T> source, T target)
		{
			if (source == null) return false;

			var index = source.LastIndexOf(target);
			if (index > 0 && index < source.Count)
			{
				(source[index], source[index - 1]) = (source[index - 1], source[index]); // Swap

				return true;
			}

			return false;
		}

		public static bool MoveUp<T>(this List<T> source, T target)
		{
			if (source == null) return false;

			var index = source.LastIndexOf(target);
			if (index >= 0 && index < source.Count - 1)
			{
				(source[index], source[index + 1]) = (
					source[index + 1], source[index]); // Swap

				return true;
			}

			return false;
		}

		public static bool MoveDown<T>(this List<T> source, int index)
		{
			if (source == null) return false;

			if (index > 0 && index < source.Count)
			{
				(source[index], source[index - 1]) = (source[index - 1], source[index]); // Swap

				return true;
			}

			return false;
		}

		public static bool MoveUp<T>(this List<T> source, int index)
		{
			if (source == null) return false;

			if (index >= 0 && index < source.Count - 1)
			{
				(source[index], source[index + 1]) = (
					source[index + 1], source[index]); // Swap

				return true;
			}

			return false;
		}
	}
}

#endregion Extension Methods
