// #define TESTING

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Facepunch.Extend;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins.ServerPanelPopUpsExtensionMethods;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;
using Random = UnityEngine.Random;

#if CARBON
using Carbon.Base;
using Carbon.Modules;
#endif

namespace Oxide.Plugins
{
	[Info("ServerPanel Pop Ups", "Mevent", "1.1.3")]
	public class ServerPanelPopUps : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin
			ServerPanel = null,
			Notify = null,
			UINotify = null,
			ImageLibrary = null;

		private static ServerPanelPopUps Instance;

#if CARBON
		private ImageDatabaseModule imageDatabase;
#endif

		private bool _enabledImageLibrary;
		
		private const string
			Perm_Edit = "serverpanelpopups.edit",
			CmdMainConsole = "UI_ServerPanel_PopUps",
			Layer = "UI.PopUp",
			EditingLayerPopUpEditor = "UI.PopUp.EditingLayer.PopUpEditor",
			EditingLayerElementEditor = "UI.PopUp.EditingLayer.ElementEditor",
			EditingLayerModalAnchorSelector = "UI.PopUp.EditingLayer.Modal.AnchorSelector",
			EditingLayerModalColorSelector = "UI.PopUp.EditingLayer.Modal.ColorSelector",
			EditingLayerModalTextEditor = "UI.PopUp.EditingLayer.Modal.TextEditor",
			EditingElementOutline = "UI.PopUp.EditingLayer.EditingElement.Outline";

		private Dictionary<string, int> _popUpByCommand = new();
		private Dictionary<int, int> _popUpByID = new();

		#endregion

		#region Config

		private Configuration _config;

		private class Configuration
		{
			#region Fields

			[JsonProperty(PropertyName = "Work with Notify?")]
			public bool UseNotify = true;

			[JsonProperty(PropertyName = "Pop Ups", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<PopUpEntry> PopUps = new()
			{
				new()
				{
					Enabled = true,
					ID = GetUniquePopUpID(),
					Commands = new[] {"pop.server.rules"},
					Background = new PopUpEntry.BackgroundUI
					{
						Background = UiElement.CreatePanel(InterfacePosition.CreatePosition(),
							new IColor("#000000", 90), material: "assets/content/ui/uibackgroundblur.mat",
							sprite: "assets/content/ui/ui.background.transparent.radial.psd"),
						CloseAfterClick = true,
						ParentLayer = "Overlay"
					},
					Content = new PopUpEntry.ContentUI
					{
						Background =
							UiElement.CreatePanel(
								InterfacePosition.CreatePosition("0.5 0.5", "0.5 0.5", "-450 -175", "450 175"),
								new IColor("#000000", 100),
								randomName: "PopUp.ServerRules.Background"),
						ContentElements = new List<UiElement>
						{
							UiElement.CreateImage(InterfacePosition.CreatePosition("0 1", "0 1", "4 -346", "394 -4"),
								"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/serverpanel-popups-banner-rules.png",
								randomName: "PopUp.ServerRules.Banner"), // Banner
							UiElement.CreateImage(InterfacePosition.CreatePosition("0 1", "0 1", "410 -54", "438 -26"),
								"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/serverpanel-icon-rules.png",
								randomName: "PopUp.ServerRules.HeaderIcon"), // HeaderIcon
							UiElement.CreateLabel(
								InterfacePosition.CreatePosition("0 1", "1 1", "448 -62.5", "-22 -17.5"),
								new IColor("#CF432D", 90), "SERVER RULES", 32,
								align: TextAnchor.UpperLeft,
								randomName: "PopUp.ServerRules.HeaderTitle"), // HeaderTitle

							UiElement.CreatePanel(InterfacePosition.CreatePosition("0 1", "1 1", "0 -4"),
								new IColor("#CF432D", 100),
								randomName: "PopUp.ServerRules.Outline.1"), // Outline (1)
							UiElement.CreatePanel(InterfacePosition.CreatePosition("0 0", "1 0", "0 0", "0 4"),
								new IColor("#CF432D", 100),
								randomName: "PopUp.ServerRules.Outline.2"), // Outline (2)
							UiElement.CreatePanel(InterfacePosition.CreatePosition("0 0", "0 1", "0 4", "4 -4"),
								new IColor("#CF432D", 100),
								randomName: "PopUp.ServerRules.Outline.3"), // Outline (3)
							UiElement.CreatePanel(InterfacePosition.CreatePosition("1 0", "1 1", "-4 4", "0 -4"),
								new IColor("#CF432D", 100),
								randomName: "PopUp.ServerRules.Outline.4") // Outline (4)
						},
						UseScrolling = true,
						ScrollView = new PopUpEntry.ScrollViewUI
						{
							Scroll = new ScrollUIElement
							{
								AnchorMinX = 0, AnchorMinY = 0,
								AnchorMaxX = 1, AnchorMaxY = 1,
								OffsetMinX = 410, OffsetMinY = 20,
								OffsetMaxX = -22, OffsetMaxY = -80,
								ScrollType = ScrollType.Vertical,
								MovementType = ScrollRect.MovementType.Elastic,
								Elasticity = 0.25f,
								DecelerationRate = 0.3f,
								ScrollSensitivity = 24f,
								Scrollbar = new ScrollUIElement.ScrollBarSettings
								{
									AutoHide = false,
									Size = 3f,
									HandleColor = new IColor("#D74933", 100),
									PressedColor = new IColor("#D74933", 100),
									HighlightColor = new IColor("#D74933", 100),
									TrackColor = new IColor("#373737", 100)
								}
							},
							ScrollElements = new List<UiElement>
							{
								UiElement.CreateLabel(
									InterfacePosition.CreatePosition("0 1", "1 1", "0 -350", "0 0"),
									new IColor("#E2DBD3", 100),
									new List<string>
									{
										"Welcome to <color=#CF432D>{server_name}</color>! Before you start playing, we would like to remind you of our rules.",
										"",
										"1. <color=#CF432D>Respect for other players</color>: Please be polite and respectful when interacting with other members of the community.",
										"2. <color=#CF432D>No cheats or exploits</color>: We strictly prohibit the use of any software or methods that give an unfair advantage to players.",
										"3. <color=#CF432D>No insults or threats</color>: Any form of harassment, discrimination, or threats is not tolerated.",
										"4. <color=#CF432D>No profanity</color>: Avoid using harsh language or obscene words in chat.",
										"5. <color=#CF432D>Fair play</color>: Do not grief or kill other players without reason. Cooperation and fair play are encouraged.",
										"6. <color=#CF432D>Non-interference</color>: Do not interfere with the progress of other players to complete their objectives.",
										"7. <color=#CF432D>Privacy</color>: Do not disclose personal information or private details about other members of our community.",
										"8. <color=#CF432D>Advertising of third-party resources is not allowed.</color> Please do not share links to other websites or resources without the permission of the administrators.",
										"9. The administrators have the right to remove any content that does not comply with the rules of this server.",
										"10. In case of any violation of the rules by a player, the administrators have the authority to block their account.",
										"", "Thank you for your cooperation and enjoy the game!"
									},
									14, "robotocondensed-regular.ttf",
									randomName: "PopUp.ServerRules.Description") // Description
							},
							ScrollHeight = 350
						}
					},
					CloseButton = new PopUpEntry.CloseButtonUI
					{
						Background =
							UiElement.CreatePanel(InterfacePosition.CreatePosition("1 1", "1 1", "-49 -49", "-9 -9"),
								new IColor("#000000", 0)),
						Title = UiElement.CreateImage(
							InterfacePosition.CreatePosition("0.5 0.5", "0.5 0.5", "-10 -10", "10 10"),
							"assets/icons/close.png")
					}
				},

				new()
				{
					Enabled = true,
					ID = GetUniquePopUpID(),
					Commands = new[] {"pop.server.commands"},
					Background = new PopUpEntry.BackgroundUI
					{
						Background = UiElement.CreatePanel(InterfacePosition.CreatePosition(),
							new IColor("#000000", 90), material: "assets/content/ui/uibackgroundblur.mat",
							sprite: "assets/content/ui/ui.background.transparent.radial.psd"),
						CloseAfterClick = true,
						ParentLayer = "Overlay"
					},
					Content = new PopUpEntry.ContentUI
					{
						Background =
							UiElement.CreatePanel(
								InterfacePosition.CreatePosition("0.5 0.5", "0.5 0.5", "-450 -175", "450 175"),
								new IColor("#000000", 100),
								randomName: "PopUp.ServerCommands.Background"),
						ContentElements = new List<UiElement>
						{
							UiElement.CreateImage(InterfacePosition.CreatePosition("0 1", "0 1", "4 -346", "394 -4"),
								"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/serverpanel-popups-banner-commands.png",
								randomName: "PopUp.ServerCommands.Banner"), // Banner
							UiElement.CreateImage(InterfacePosition.CreatePosition("0 1", "0 1", "410 -54", "438 -26"),
								"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/serverpanel-icon-commands.png",
								randomName: "PopUp.ServerCommands.HeaderIcon"), // HeaderIcon
							UiElement.CreateLabel(
								InterfacePosition.CreatePosition("0 1", "1 1", "448 -62.5", "-22 -17.5"),
								new IColor("#CF432D", 90), "SERVER COMMANDS", 32,
								align: TextAnchor.UpperLeft,
								randomName: "PopUp.ServerCommands.HeaderTitle"), // HeaderTitle

							UiElement.CreatePanel(InterfacePosition.CreatePosition("0 1", "1 1", "0 -4"),
								new IColor("#CF432D", 100),
								randomName: "PopUp.ServerCommands.Outline.1"), // Outline (1)
							UiElement.CreatePanel(InterfacePosition.CreatePosition("0 0", "1 0", "0 0", "0 4"),
								new IColor("#CF432D", 100),
								randomName: "PopUp.ServerCommands.Outline.2"), // Outline (2)
							UiElement.CreatePanel(InterfacePosition.CreatePosition("0 0", "0 1", "0 4", "4 -4"),
								new IColor("#CF432D", 100),
								randomName: "PopUp.ServerCommands.Outline.3"), // Outline (3)
							UiElement.CreatePanel(InterfacePosition.CreatePosition("1 0", "1 1", "-4 4", "0 -4"),
								new IColor("#CF432D", 100),
								randomName: "PopUp.ServerCommands.Outline.4") // Outline (4)
						},
						UseScrolling = true,
						ScrollView = new PopUpEntry.ScrollViewUI
						{
							Scroll = new ScrollUIElement
							{
								AnchorMinX = 0, AnchorMinY = 0,
								AnchorMaxX = 1, AnchorMaxY = 1,
								OffsetMinX = 410, OffsetMinY = 20,
								OffsetMaxX = -22, OffsetMaxY = -80,
								ScrollType = ScrollType.Vertical,
								MovementType = ScrollRect.MovementType.Elastic,
								Elasticity = 0.25f,
								DecelerationRate = 0.3f,
								ScrollSensitivity = 24f,
								Scrollbar = new ScrollUIElement.ScrollBarSettings
								{
									AutoHide = false,
									Size = 3f,
									HandleColor = new IColor("#D74933", 100),
									PressedColor = new IColor("#D74933", 100),
									HighlightColor = new IColor("#D74933", 100),
									TrackColor = new IColor("#373737", 100)
								}
							},
							ScrollElements = new List<UiElement>
							{
								UiElement.CreateLabel(
									InterfacePosition.CreatePosition("0 1", "1 1", "0 -350", "0 0"),
									new IColor("#E2DBD3", 100),
									new List<string>
									{
										"<color=#CF432D>/info</color> – open this menu",
										"<color=#CF432D>/trade player_name</color> – safe trade with other players",
										"<color=#CF432D>/online</color> – see how many players online",
										"<color=#CF432D>/shop</color> – open in-game shop",
										"<color=#CF432D>/kits</color> – open kits menu",
										"<color=#CF432D>/clan</color> – open clans menu",
										"<color=#CF432D>/tpr player_name</color> – send teleport request to other player",
										"<color=#CF432D>/tpa</color> – accept player teleport request",
										"<color=#CF432D>/tpc</color> – cancel player teleport request",
										"<color=#CF432D>/sethome home_name</color> – set your home location",
										"<color=#CF432D>/home home_name</color> – teleport to your home"
									},
									14, "robotocondensed-regular.ttf",
									randomName: "PopUp.ServerCommands.Description") // Description
							},
							ScrollHeight = 350
						}
					},
					CloseButton = new PopUpEntry.CloseButtonUI
					{
						Background =
							UiElement.CreatePanel(InterfacePosition.CreatePosition("1 1", "1 1", "-49 -49", "-9 -9"),
								new IColor("#000000", 0)),
						Title = UiElement.CreateImage(
							InterfacePosition.CreatePosition("0.5 0.5", "0.5 0.5", "-10 -10", "10 10"),
							"assets/icons/close.png")
					}
				},

				new()
				{
					Enabled = true,
					ID = GetUniquePopUpID(),
					Commands = new[] {"pop.server.binds"},
					Background = new PopUpEntry.BackgroundUI
					{
						Background = UiElement.CreatePanel(InterfacePosition.CreatePosition(),
							new IColor("#000000", 90), material: "assets/content/ui/uibackgroundblur.mat",
							sprite: "assets/content/ui/ui.background.transparent.radial.psd"),
						CloseAfterClick = true,
						ParentLayer = "Overlay"
					},
					Content = new PopUpEntry.ContentUI
					{
						Background =
							UiElement.CreatePanel(
								InterfacePosition.CreatePosition("0.5 0.5", "0.5 0.5", "-450 -175", "450 175"),
								new IColor("#000000", 100),
								randomName: "PopUp.ServerBinds.Background"),
						ContentElements = new List<UiElement>
						{
							UiElement.CreateImage(InterfacePosition.CreatePosition("0 1", "0 1", "4 -346", "394 -4"),
								"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/serverpanel-popups-banner-binds.png",
								randomName: "PopUp.ServerBinds.Banner"), // Banner
							UiElement.CreateImage(InterfacePosition.CreatePosition("0 1", "0 1", "410 -54", "438 -26"),
								"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/serverpanel-icon-binds.png",
								randomName: "PopUp.ServerBinds.HeaderIcon"), // HeaderIcon
							UiElement.CreateLabel(
								InterfacePosition.CreatePosition("0 1", "1 1", "448 -62.5", "-22 -17.5"),
								new IColor("#CF432D", 90), "SERVER BINDS", 32,
								align: TextAnchor.UpperLeft,
								randomName: "PopUp.ServerBinds.HeaderTitle"), // HeaderTitle

							UiElement.CreatePanel(InterfacePosition.CreatePosition("0 1", "1 1", "0 -4"),
								new IColor("#CF432D", 100),
								randomName: "PopUp.ServerBinds.Outline.1"), // Outline (1)
							UiElement.CreatePanel(InterfacePosition.CreatePosition("0 0", "1 0", "0 0", "0 4"),
								new IColor("#CF432D", 100),
								randomName: "PopUp.ServerBinds.Outline.2"), // Outline (2)
							UiElement.CreatePanel(InterfacePosition.CreatePosition("0 0", "0 1", "0 4", "4 -4"),
								new IColor("#CF432D", 100),
								randomName: "PopUp.ServerBinds.Outline.3"), // Outline (3)
							UiElement.CreatePanel(InterfacePosition.CreatePosition("1 0", "1 1", "-4 4", "0 -4"),
								new IColor("#CF432D", 100),
								randomName: "PopUp.ServerBinds.Outline.4") // Outline (4)
						},
						UseScrolling = true,
						ScrollView = new PopUpEntry.ScrollViewUI
						{
							Scroll = new ScrollUIElement
							{
								AnchorMinX = 0, AnchorMinY = 0,
								AnchorMaxX = 1, AnchorMaxY = 1,
								OffsetMinX = 410, OffsetMinY = 20,
								OffsetMaxX = -22, OffsetMaxY = -80,
								ScrollType = ScrollType.Vertical,
								MovementType = ScrollRect.MovementType.Elastic,
								Elasticity = 0.25f,
								DecelerationRate = 0.3f,
								ScrollSensitivity = 24f,
								Scrollbar = new ScrollUIElement.ScrollBarSettings
								{
									AutoHide = false,
									Size = 3f,
									HandleColor = new IColor("#D74933", 100),
									PressedColor = new IColor("#D74933", 100),
									HighlightColor = new IColor("#D74933", 100),
									TrackColor = new IColor("#373737", 100)
								}
							},
							ScrollElements = new List<UiElement>
							{
								UiElement.CreateLabel(
									InterfacePosition.CreatePosition("0 1", "1 1", "0 -350", "0 0"),
									new IColor("#E2DBD3", 100),
									"To install binds, use the console (F1 key):",
									13, "robotocondensed-regular.ttf",
									randomName: "PopUp.ServerBinds.Description"), // Description

								#region Binds

								#region Bind 1

								UiElement.CreateLabel(
									InterfacePosition.CreatePosition("0 1", "0 1", "0 -50", "188 -30"),
									IColor.Create("#E2DBD3"), "OPEN GRAFFITI MENU", 12,
									randomName: "PopUp.ServerBinds.Bind.1.Title"),
								UiElement.CreatePanel(
									InterfacePosition.CreatePosition("0 1", "0 1", "213 -50", "363 -30"),
									IColor.Create("#696969", 30),
									randomName: "PopUp.ServerBinds.Bind.1.Value.Background"),
								UiElement.CreateInputField(
									InterfacePosition.CreatePosition("0 1", "0 1", "213 -50", "363 -30"),
									IColor.Create("#E2DBD3"), "bind KEY +spray", 10,
									align: TextAnchor.MiddleCenter,
									randomName: "PopUp.ServerBinds.Bind.1.Value.Input"),

								#endregion

								#region Bind 2

								UiElement.CreateLabel(
									InterfacePosition.CreatePosition("0 1", "0 1", "0 -75", "188 -55"),
									IColor.Create("#E2DBD3"), "AUTO-RUN", 12,
									randomName: "PopUp.ServerBinds.Bind.2.Title"),
								UiElement.CreatePanel(
									InterfacePosition.CreatePosition("0 1", "0 1", "213 -75", "363 -55"),
									IColor.Create("#696969", 30),
									randomName: "PopUp.ServerBinds.Bind.2.Value.Background"),
								UiElement.CreateInputField(
									InterfacePosition.CreatePosition("0 1", "0 1", "213 -75", "363 -55"),
									IColor.Create("#E2DBD3"), "bind KEY forward;sprint", 10,
									align: TextAnchor.MiddleCenter,
									randomName: "PopUp.ServerBinds.Bind.2.Value.Input"),

								#endregion

								#region Bind 3

								UiElement.CreateLabel(
									InterfacePosition.CreatePosition("0 1", "0 1", "0 -100", "188 -80"),
									IColor.Create("#E2DBD3"), "AUTO-ATTACK", 12,
									randomName: "PopUp.ServerBinds.Bind.3.Title"),
								UiElement.CreatePanel(
									InterfacePosition.CreatePosition("0 1", "0 1", "213 -100", "363 -80"),
									IColor.Create("#696969", 30),
									randomName: "PopUp.ServerBinds.Bind.3.Value.Background"),
								UiElement.CreateInputField(
									InterfacePosition.CreatePosition("0 1", "0 1", "213 -100", "363 -80"),
									IColor.Create("#E2DBD3"), "bind KEY attack", 10,
									align: TextAnchor.MiddleCenter,
									randomName: "PopUp.ServerBinds.Bind.3.Value.Input"),

								#endregion

								#region Bind 4

								UiElement.CreateLabel(
									InterfacePosition.CreatePosition("0 1", "0 1", "0 -125", "188 -105"),
									IColor.Create("#E2DBD3"), "OPEN KITS MENU", 12,
									randomName: "PopUp.ServerBinds.Bind.4.Title"),
								UiElement.CreatePanel(
									InterfacePosition.CreatePosition("0 1", "0 1", "213 -125", "363 -105"),
									IColor.Create("#696969", 30),
									randomName: "PopUp.ServerBinds.Bind.4.Value.Background"),
								UiElement.CreateInputField(
									InterfacePosition.CreatePosition("0 1", "0 1", "213 -125", "363 -105"),
									IColor.Create("#E2DBD3"), "bind KEY chat.say /kits", 10,
									align: TextAnchor.MiddleCenter,
									randomName: "PopUp.ServerBinds.Bind.4.Value.Input"),

								#endregion

								#region Bind 5

								UiElement.CreateLabel(
									InterfacePosition.CreatePosition("0 1", "0 1", "0 -150", "188 -130"),
									IColor.Create("#E2DBD3"), "TELEPORT TO HOME", 12,
									randomName: "PopUp.ServerBinds.Bind.5.Title"),
								UiElement.CreatePanel(
									InterfacePosition.CreatePosition("0 1", "0 1", "213 -150", "363 -130"),
									IColor.Create("#696969", 30),
									randomName: "PopUp.ServerBinds.Bind.5.Value.Background"),
								UiElement.CreateInputField(
									InterfacePosition.CreatePosition("0 1", "0 1", "213 -150", "363 -130"),
									IColor.Create("#E2DBD3"), "bind KEY chat.say /home {name}", 10,
									align: TextAnchor.MiddleCenter,
									randomName: "PopUp.ServerBinds.Bind.5.Value.Input"),

								#endregion

								#region Bind 6

								UiElement.CreateLabel(
									InterfacePosition.CreatePosition("0 1", "0 1", "0 -175", "188 -155"),
									IColor.Create("#E2DBD3"), "ACCEPT TELEPORT REQUEST", 12,
									randomName: "PopUp.ServerBinds.Bind.6.Title"),
								UiElement.CreatePanel(
									InterfacePosition.CreatePosition("0 1", "0 1", "213 -175", "363 -155"),
									IColor.Create("#696969", 30),
									randomName: "PopUp.ServerBinds.Bind.6.Value.Background"),
								UiElement.CreateInputField(
									InterfacePosition.CreatePosition("0 1", "0 1", "213 -175", "363 -155"),
									IColor.Create("#E2DBD3"), "bind KEY chat.say /tpa", 10,
									align: TextAnchor.MiddleCenter,
									randomName: "PopUp.ServerBinds.Bind.6.Value.Input"),

								#endregion

								#region Bind 7

								UiElement.CreateLabel(
									InterfacePosition.CreatePosition("0 1", "0 1", "0 -200", "188 -180"),
									IColor.Create("#E2DBD3"), "OPEN SKILLS", 12,
									randomName: "PopUp.ServerBinds.Bind.7.Title"),
								UiElement.CreatePanel(
									InterfacePosition.CreatePosition("0 1", "0 1", "213 -200", "363 -180"),
									IColor.Create("#696969", 30),
									randomName: "PopUp.ServerBinds.Bind.7.Value.Background"),
								UiElement.CreateInputField(
									InterfacePosition.CreatePosition("0 1", "0 1", "213 -200", "363 -180"),
									IColor.Create("#E2DBD3"), "bind KEY chat.say /skills", 10,
									align: TextAnchor.MiddleCenter,
									randomName: "PopUp.ServerBinds.Bind.7.Value.Input"),

								#endregion

								#region Bind 8

								UiElement.CreateLabel(
									InterfacePosition.CreatePosition("0 1", "0 1", "0 -225", "188 -205"),
									IColor.Create("#E2DBD3"), "OPEN DAILY REWARDS", 12,
									randomName: "PopUp.ServerBinds.Bind.8.Title"),
								UiElement.CreatePanel(
									InterfacePosition.CreatePosition("0 1", "0 1", "213 -225", "363 -205"),
									IColor.Create("#696969", 30),
									randomName: "PopUp.ServerBinds.Bind.8.Value.Background"),
								UiElement.CreateInputField(
									InterfacePosition.CreatePosition("0 1", "0 1", "213 -225", "363 -205"),
									IColor.Create("#E2DBD3"), "bind KEY chat.say /daily", 10,
									align: TextAnchor.MiddleCenter,
									randomName: "PopUp.ServerBinds.Bind.8.Value.Input"),

								#endregion

								#endregion Binds
							},
							ScrollHeight = 350
						}
					},
					CloseButton = new PopUpEntry.CloseButtonUI
					{
						Background =
							UiElement.CreatePanel(InterfacePosition.CreatePosition("1 1", "1 1", "-49 -49", "-9 -9"),
								new IColor("#000000", 0)),
						Title = UiElement.CreateImage(
							InterfacePosition.CreatePosition("0.5 0.5", "0.5 0.5", "-10 -10", "10 10"),
							"assets/icons/close.png")
					}
				},

				new()
				{
					Enabled = true,
					ID = GetUniquePopUpID(),
					Commands = new[] {"pop.server.contacts"},
					Background = new PopUpEntry.BackgroundUI
					{
						Background = UiElement.CreatePanel(InterfacePosition.CreatePosition(),
							new IColor("#000000", 90), material: "assets/content/ui/uibackgroundblur.mat",
							sprite: "assets/content/ui/ui.background.transparent.radial.psd"),
						CloseAfterClick = true,
						ParentLayer = "Overlay"
					},
					Content = new PopUpEntry.ContentUI
					{
						Background =
							UiElement.CreatePanel(
								InterfacePosition.CreatePosition("0.5 0.5", "0.5 0.5", "-450 -175", "450 175"),
								new IColor("#000000", 100),
								randomName: "PopUp.ServerContacts.Background"),
						ContentElements = new List<UiElement>
						{
							UiElement.CreateImage(InterfacePosition.CreatePosition("0 1", "0 1", "4 -346", "394 -4"),
								"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/serverpanel-popups-banner-contacts.png",
								randomName: "PopUp.ServerContacts.Banner"), // Banner
							UiElement.CreateImage(InterfacePosition.CreatePosition("0 1", "0 1", "410 -54", "438 -26"),
								"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/serverpanel-icon-contacts.png",
								randomName: "PopUp.ServerContacts.HeaderIcon"), // HeaderIcon
							UiElement.CreateLabel(
								InterfacePosition.CreatePosition("0 1", "1 1", "448 -62.5", "-22 -17.5"),
								new IColor("#CF432D", 90), "SERVER CONTACTS", 32,
								align: TextAnchor.UpperLeft,
								randomName: "PopUp.ServerContacts.HeaderTitle"), // HeaderTitle

							UiElement.CreatePanel(InterfacePosition.CreatePosition("0 1", "1 1", "0 -4"),
								new IColor("#CF432D", 100),
								randomName: "PopUp.ServerContacts.Outline.1"), // Outline (1)
							UiElement.CreatePanel(InterfacePosition.CreatePosition("0 0", "1 0", "0 0", "0 4"),
								new IColor("#CF432D", 100),
								randomName: "PopUp.ServerContacts.Outline.2"), // Outline (2)
							UiElement.CreatePanel(InterfacePosition.CreatePosition("0 0", "0 1", "0 4", "4 -4"),
								new IColor("#CF432D", 100),
								randomName: "PopUp.ServerContacts.Outline.3"), // Outline (3)
							UiElement.CreatePanel(InterfacePosition.CreatePosition("1 0", "1 1", "-4 4", "0 -4"),
								new IColor("#CF432D", 100),
								randomName: "PopUp.ServerContacts.Outline.4") // Outline (4)
						},
						UseScrolling = true,
						ScrollView = new PopUpEntry.ScrollViewUI
						{
							Scroll = new ScrollUIElement
							{
								AnchorMinX = 0, AnchorMinY = 0,
								AnchorMaxX = 1, AnchorMaxY = 1,
								OffsetMinX = 410, OffsetMinY = 20,
								OffsetMaxX = -22, OffsetMaxY = -80,
								ScrollType = ScrollType.Vertical,
								MovementType = ScrollRect.MovementType.Elastic,
								Elasticity = 0.25f,
								DecelerationRate = 0.3f,
								ScrollSensitivity = 24f,
								Scrollbar = new ScrollUIElement.ScrollBarSettings
								{
									AutoHide = false,
									Size = 3f,
									HandleColor = new IColor("#D74933", 100),
									PressedColor = new IColor("#D74933", 100),
									HighlightColor = new IColor("#D74933", 100),
									TrackColor = new IColor("#373737", 100)
								}
							},
							ScrollElements = new List<UiElement>
							{
								UiElement.CreateLabel(
									InterfacePosition.CreatePosition("0 1", "0 1", "0 -250", "468 0"),
									new IColor("#E2DBD3", 100),
									new List<string>
									{
										"Are your questions urgent or do you have any questions depending on your VIP, please contact us through our website."
									},
									14, "robotocondensed-regular.ttf",
									randomName: "PopUp.ServerContacts.Description"), // Description

								#region QR (1)

								UiElement.CreatePanel(
									InterfacePosition.CreatePosition("0 1", "0 1", "0 -235", "120 -55"),
									new IColor("#696969", 20),
									randomName: "PopUp.ServerContacts.QR.Website.Background"), //BG
								UiElement.CreateImage(
									InterfacePosition.CreatePosition("0 1", "0 1", "10 -170", "110 -70"),
									"{qr_website}",
									randomName: "PopUp.ServerContacts.QR.Website.Image"), //IMAGE

								UiElement.CreatePanel(
									InterfacePosition.CreatePosition("0 1", "0 1", "0 -235", "120 -210"),
									new IColor("#CF432D", 80),
									randomName: "PopUp.ServerContacts.QR.Website.Contact"), // Contact Panel
								UiElement.CreateLabel(
									InterfacePosition.CreatePosition("0 1", "0 1", "0 -235", "120 -210"),
									new IColor("#E2DBD3", 100), "STORE", 12,
									align: TextAnchor.MiddleCenter,
									randomName: "PopUp.ServerContacts.QR.Website.Contact.Label"), // Contact Label

								UiElement.CreatePanel(
									InterfacePosition.CreatePosition("0 1", "0 1", "0 -210", "120 -185"),
									new IColor("#CF432D", 15),
									randomName: "PopUp.ServerContacts.QR.Website.URL.Panel"), // Link Panel
								UiElement.CreateInputField(
									InterfacePosition.CreatePosition("0 1", "0 1", "0 -210", "120 -185"),
									new IColor("#E2DBD3", 60),
									"{url_website}",
									10,
									align: TextAnchor.MiddleCenter, font:
									"robotocondensed-regular.ttf",
									randomName: "PopUp.ServerContacts.QR.Website.URL.Input"), // Link Label

								#endregion QR (1)

								#region QR (2)

								UiElement.CreatePanel(
									InterfacePosition.CreatePosition("0 1", "0 1", "140 -235", "260 -55"),
									new IColor("#696969", 20),
									randomName: "PopUp.ServerContacts.QR.Discord.Background"), //BG
								UiElement.CreateImage(
									InterfacePosition.CreatePosition("0 1", "0 1", "150 -170", "250 -70"),
									"{qr_discord}",
									randomName: "PopUp.ServerContacts.QR.Discord.Image"), //IMAGE

								UiElement.CreatePanel(
									InterfacePosition.CreatePosition("0 1", "0 1", "140 -235", "260 -210"),
									new IColor("#CF432D", 80),
									randomName: "PopUp.ServerContacts.QR.Discord.Contact"), // Contact Panel
								UiElement.CreateLabel(
									InterfacePosition.CreatePosition("0 1", "0 1", "140 -235", "260 -210"),
									new IColor("#E2DBD3", 100), "DISCORD", 12,
									align: TextAnchor.MiddleCenter,
									randomName: "PopUp.ServerContacts.QR.Discord.Contact.Label"), // Contact Label

								UiElement.CreatePanel(
									InterfacePosition.CreatePosition("0 1", "0 1", "140 -210", "260 -185"),
									new IColor("#CF432D", 15),
									randomName: "PopUp.ServerContacts.QR.Discord.URL.Panel"), // Link Panel
								UiElement.CreateInputField(
									InterfacePosition.CreatePosition("0 1", "0 1", "140 -210", "260 -185"),
									new IColor("#E2DBD3", 60),
									"{url_discord}",
									10,
									align: TextAnchor.MiddleCenter, font:
									"robotocondensed-regular.ttf",
									randomName: "PopUp.ServerContacts.QR.Discord.URL.Input"), // Link Label

								#endregion QR (2)
							},
							ScrollHeight = 350
						}
					},
					CloseButton = new PopUpEntry.CloseButtonUI
					{
						Background =
							UiElement.CreatePanel(InterfacePosition.CreatePosition("1 1", "1 1", "-49 -49", "-9 -9"),
								new IColor("#000000", 0)),
						Title = UiElement.CreateImage(
							InterfacePosition.CreatePosition("0.5 0.5", "0.5 0.5", "-10 -10", "10 10"),
							"assets/icons/close.png")
					}
				},

				new()
				{
					Enabled = true,
					ID = GetUniquePopUpID(),
					Commands = new[] {"pop.server.info"},
					Background = new PopUpEntry.BackgroundUI
					{
						Background = UiElement.CreatePanel(InterfacePosition.CreatePosition(),
							new IColor("#000000", 90), material: "assets/content/ui/uibackgroundblur.mat",
							sprite: "assets/content/ui/ui.background.transparent.radial.psd"),
						CloseAfterClick = true,
						ParentLayer = "Overlay"
					},
					Content = new PopUpEntry.ContentUI
					{
						Background =
							UiElement.CreatePanel(
								InterfacePosition.CreatePosition("0.5 0.5", "0.5 0.5", "-450 -175", "450 175"),
								new IColor("#000000", 100),
								randomName: "PopUp.ServerInfo.Background"),
						ContentElements = new List<UiElement>
						{
							UiElement.CreateImage(InterfacePosition.CreatePosition("0 1", "0 1", "4 -346", "394 -4"),
								"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/serverpanel-popups-banner-info.png",
								randomName: "PopUp.ServerInfo.Banner"), // Banner
							UiElement.CreateImage(InterfacePosition.CreatePosition("0 1", "0 1", "410 -54", "438 -26"),
								"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/serverpanel-icon-info.png",
								randomName: "PopUp.ServerInfo.HeaderIcon"), // HeaderIcon
							UiElement.CreateLabel(
								InterfacePosition.CreatePosition("0 1", "1 1", "448 -62.5", "-22 -17.5"),
								new IColor("#CF432D", 90), "SERVER INFO", 32,
								align: TextAnchor.UpperLeft,
								randomName: "PopUp.ServerInfo.HeaderTitle"), // HeaderTitle

							UiElement.CreatePanel(InterfacePosition.CreatePosition("0 1", "1 1", "0 -4"),
								new IColor("#CF432D", 100),
								randomName: "PopUp.ServerInfo.Outline.1"), // Outline (1)
							UiElement.CreatePanel(InterfacePosition.CreatePosition("0 0", "1 0", "0 0", "0 4"),
								new IColor("#CF432D", 100),
								randomName: "PopUp.ServerInfo.Outline.2"), // Outline (2)
							UiElement.CreatePanel(InterfacePosition.CreatePosition("0 0", "0 1", "0 4", "4 -4"),
								new IColor("#CF432D", 100),
								randomName: "PopUp.ServerInfo.Outline.3"), // Outline (3)
							UiElement.CreatePanel(InterfacePosition.CreatePosition("1 0", "1 1", "-4 4", "0 -4"),
								new IColor("#CF432D", 100),
								randomName: "PopUp.ServerInfo.Outline.4"), // Outline (4)

							UiElement.CreateImage(InterfacePosition.CreatePosition("0 1", "0 1", "4 -346", "394 -175"),
								"assets/content/ui/UI.Background.Transparent.Linear.psd",
								new IColor("#1E2022", 100),
								randomName: "PopUp.ServerInfo.Hover"), // Hover
							UiElement.CreateImage(
								InterfacePosition.CreatePosition("0 1", "0 1", "169 -205", "229 -145"),
								"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/serverpanel-button-play.png",
								randomName: "PopUp.ServerInfo.Image.Play"), // Image Play
							UiElement.CreateButton(
								InterfacePosition.CreatePosition("0 1", "0 1", "169 -205", "229 -145"),
								IColor.CreateTransparent(), IColor.CreateTransparent(), string.Empty,
								command:
								"serverpanel_broadcastvideo https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/rust-official-trailer.mp4",
								randomName: "PopUp.ServerInfo.Button.Play") // Button Play
						},
						UseScrolling = true,
						ScrollView = new PopUpEntry.ScrollViewUI
						{
							Scroll = new ScrollUIElement
							{
								AnchorMinX = 0, AnchorMinY = 0,
								AnchorMaxX = 1, AnchorMaxY = 1,
								OffsetMinX = 410, OffsetMinY = 20,
								OffsetMaxX = -22, OffsetMaxY = -80,
								ScrollType = ScrollType.Vertical,
								MovementType = ScrollRect.MovementType.Elastic,
								Elasticity = 0.25f,
								DecelerationRate = 0.3f,
								ScrollSensitivity = 24f,
								Scrollbar = new ScrollUIElement.ScrollBarSettings
								{
									AutoHide = false,
									Size = 3f,
									HandleColor = new IColor("#D74933", 100),
									PressedColor = new IColor("#D74933", 100),
									HighlightColor = new IColor("#D74933", 100),
									TrackColor = new IColor("#373737", 100)
								}
							},
							ScrollElements = new List<UiElement>
							{
								UiElement.CreateLabel(
									InterfacePosition.CreatePosition("0 1", "1 1", "0 -350", "0 0"),
									new IColor("#E2DBD3", 100),
									new List<string>
									{
										"WIPE SCHEDULE: <color=#CF432D>{server_wipe_days}</color> @ <color=#CF432D>{server_wipe_time}</color> ({server_wipe_time_zone})",
										"",
										"<size=18><color=#CF432D><b>SERVER INFO:</b></color></size>",
										"– {server_rates} GATHER RATES ON RESOURCES, COMPONENTS AND FOOD",
										"– {server_stack_size} STACK SIZE",
										"– {server_craft_speed} CRAFTING SPEED",
										"– FASTER SMELTING/RECYCLING SPEED",
										"– LONG DAYS & SHORT NIGHTS",
										"– ACTIVE ADMINS",
										"– DDoS PROTECTED",
										"– LAG FREE EXPERIENCE"
									},
									14, "robotocondensed-regular.ttf",
									randomName: "PopUp.ServerInfo.Description") // Description
							},
							ScrollHeight = 350
						}
					},
					CloseButton = new PopUpEntry.CloseButtonUI
					{
						Background =
							UiElement.CreatePanel(InterfacePosition.CreatePosition("1 1", "1 1", "-49 -49", "-9 -9"),
								new IColor("#000000", 0)),
						Title = UiElement.CreateImage(
							InterfacePosition.CreatePosition("0.5 0.5", "0.5 0.5", "-10 -10", "10 10"),
							"assets/icons/close.png")
					}
				}
			};

			#endregion
		}

		#region Classes

		public class PopUpEntry
		{
			#region Fields

			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = "ID")] 
			public int ID;

			[JsonProperty(PropertyName = "Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] Commands = Array.Empty<string>();

			[JsonProperty(PropertyName = "Background")]
			public BackgroundUI Background = new();

			[JsonProperty(PropertyName = "Close Button")]
			public CloseButtonUI CloseButton = new();

			[JsonProperty(PropertyName = "Content")]
			public ContentUI Content = new();

			#endregion

			#region Public Methods
			
			public void Show(BasePlayer player)
			{
				var container = new CuiElementContainer();

				ShowBackground(player, container);

				ShowContent(player, container);
				
				ShowCloseButton(player, container);

				CuiHelper.AddUi(player, container);
			}

			private void ShowEditButton(BasePlayer player, CuiElementContainer container)
			{
				ShowEditButtonUI(player, container, Layer + ".Content", Layer + ".Button.Edit",
					cmdEdit: $"{CmdMainConsole} edit_popup start {ID}");
			}

			private void ShowCloseButton(BasePlayer player, CuiElementContainer container)
			{
				CloseButton.Show(player, container, Layer + ".Content", Layer,
					command: $"{CmdMainConsole} close");
			}

			private void ShowBackground(BasePlayer player, CuiElementContainer container)
			{
				Background.ShowBackground(player, container, Layer, cmdBG: $"{CmdMainConsole} close");
			}

			public void ShowContent(BasePlayer player, CuiElementContainer container)
			{
				Content.ShowContent(player, container, Layer, Layer + ".Content");
				
				if (CanPlayerEdit(player)) ShowEditButton(player, container);
			}

			public List<UiElement> GetAllUiElements()
			{
				var nestedElements = new List<UiElement>();
				GetNestedUiElementsRecursive(this, nestedElements);
				return nestedElements;
			}

			private void GetNestedUiElementsRecursive(object obj, List<UiElement> nestedElements)
			{
				switch (obj)
				{
					case UiElement uiElement:
						nestedElements.Add(uiElement);
						break;

					case PopUpEntry popUpEntry:
						GetNestedUiElementsRecursive(popUpEntry.Background, nestedElements);
						GetNestedUiElementsRecursive(popUpEntry.Content, nestedElements);
						GetNestedUiElementsRecursive(popUpEntry.CloseButton, nestedElements);
						break;

					case BackgroundUI backgroundUI:
						GetNestedUiElementsRecursive(backgroundUI.Background, nestedElements);
						break;

					case ContentUI contentUI:
						foreach (var element in contentUI.ContentElements)
							GetNestedUiElementsRecursive(element, nestedElements);

						if (contentUI.UseScrolling)
							foreach (var element in contentUI.ScrollView.ScrollElements)
								GetNestedUiElementsRecursive(element, nestedElements);
						break;

					case CloseButtonUI closeButtonUI:
						GetNestedUiElementsRecursive(closeButtonUI.Background, nestedElements);

						GetNestedUiElementsRecursive(closeButtonUI.Title, nestedElements);
						break;
				}
			}

			#endregion

			#region Classes

			public class BackgroundUI
			{
				#region Fields

				[JsonProperty(PropertyName = "Parent Layer (Overlay/Hud)")]
				public string ParentLayer;

				[JsonProperty(PropertyName = "Background")]
				public UiElement Background = new();

				[JsonProperty(PropertyName = "Close after click?")]
				public bool CloseAfterClick;

				#endregion

				#region Public Methods

				public string ShowBackground(BasePlayer player, 
					CuiElementContainer container,
					string name = "",
					string cmdBG = "")
				{
					if (string.IsNullOrEmpty(name))
						name = CuiHelper.GetGuid();

					if (string.IsNullOrEmpty(ParentLayer))
						ParentLayer = "Overlay";

					Background.Get(ref container, player, ParentLayer, name, name);

					if (CloseAfterClick)
						container.Add(new CuiElement
						{
							Parent = name,
							Components =
							{
								new CuiButtonComponent
								{
									Color = "0 0 0 0",
									Command = cmdBG,
									Close = name
								},
								new CuiRectTransformComponent(),
								new CuiNeedsCursorComponent()
							}
						});

					return name;
				}

				#endregion
			}

			public class ContentUI
			{
				#region Fields

				[JsonProperty(PropertyName = "Background")]
				public UiElement Background = new();

				[JsonProperty(PropertyName = "Content Elements",
					ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public List<UiElement> ContentElements = new();

				[JsonProperty(PropertyName = "Use scrolling?")]
				public bool UseScrolling;

				[JsonProperty(PropertyName = "Scroll Settings")]
				public ScrollViewUI ScrollView = new();

				#endregion

				#region Public Methods

				public void ShowContent(BasePlayer player, CuiElementContainer container,
					string parent,
					string name = "",
					Action<string> callback = null)
				{
					if (string.IsNullOrEmpty(name))
						name = CuiHelper.GetGuid();

					Background.Get(ref container, player, parent, name, name);

					ContentElements?.ForEach(element => element.Get(ref container, player, name,
						element.Name, element.Name));

					if (UseScrolling)
						ScrollView?.Get(player, container, name, Layer + ".Scroll");

					callback?.Invoke(name);
				}

				public List<UiElement> GetEditableUiElements()
				{
					var list = new List<UiElement>(ContentElements);

					if (UseScrolling)
						list.AddRange(ScrollView.ScrollElements);

					return list;
				}

				#endregion
			}

			public class ScrollViewUI
			{
				#region Fields

				[JsonProperty(PropertyName = "Scroll")]
				public ScrollUIElement Scroll = new();

				[JsonProperty(PropertyName = "Scroll Elements",
					ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public List<UiElement> ScrollElements = new();

				[JsonProperty(PropertyName = "Scroll Height")]
				public float ScrollHeight;

				#endregion

				#region Public Methods

				public void Get(BasePlayer player, CuiElementContainer container, string parent, string name = "")
				{
					if (string.IsNullOrEmpty(name))
						name = parent + ".Scroll.Panel";

					container.Add(new CuiElement
					{
						Name = name,
						DestroyUi = name,
						Parent = parent,
						Components =
						{
							new CuiImageComponent
							{
								Color = "0 0 0 0"
							},
							Scroll.GetRectTransform()
						}
					});

					container.Add(new CuiElement
					{
						Name = name + ".View",
						Parent = name,
						Components =
						{
							Scroll.GetScrollView(CalculateContentRectTransform(ScrollHeight))
						}
					});

					ScrollElements?.ForEach(element =>
						element.Get(ref container, player, name + ".View", element.Name, element.Name));
				}

				public CuiRectTransform CalculateContentRectTransform(float totalWidth)
				{
					CuiRectTransform contentRect;
					if (Scroll.ScrollType == ScrollType.Horizontal)
						contentRect = new CuiRectTransform
						{
							AnchorMin = "0 0", AnchorMax = "0 1",
							OffsetMin = "0 0",
							OffsetMax = $"{totalWidth} 0"
						};
					else
						contentRect = new CuiRectTransform
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"0 -{totalWidth}",
							OffsetMax = "0 0"
						};

					return contentRect;
				}

				private CuiRectTransformComponent CalculateCategoriesPosition(float offsetX, float categoryWidth,
					float categoryHeight)
				{
					CuiRectTransformComponent cuiRect;
					if (Scroll.ScrollType == ScrollType.Horizontal)
						cuiRect = new CuiRectTransformComponent
						{
							AnchorMin = "0 1", AnchorMax = "0 1",
							OffsetMin = $"{offsetX} -{categoryHeight}",
							OffsetMax = $"{offsetX + categoryWidth} 0"
						};
					else
						cuiRect = new CuiRectTransformComponent
						{
							AnchorMin = "0 1", AnchorMax = "0 1",
							OffsetMin = $"0 {offsetX - categoryHeight}",
							OffsetMax = $"{categoryWidth} {offsetX}"
						};

					return cuiRect;
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

				public void Show(BasePlayer player, CuiElementContainer container, string parent,
					string closeLayer = "", string command = "")
				{
					Background.Get(ref container, player, parent, parent + ".CloseButton", parent + ".CloseButton");

					Title.Get(ref container, player, parent + ".CloseButton", parent + ".CloseButton.Command");

					container.Add(new CuiElement
					{
						Parent = parent + ".CloseButton",
						Components =
						{
							new CuiButtonComponent
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

			#endregion
		}

		#region UI

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
					var cuiScrollbar = new CuiScrollbar
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
				
				if (string.IsNullOrEmpty(Hex)) Hex = "#FFFFFF";

				var hexValue = Hex.Trim('#');
				if (hexValue.Length != 6)
					throw new ArgumentException("Invalid HEX color format. Must be 6 characters (e.g., #RRGGBB).", nameof(Hex));
				
				var r = byte.Parse(hexValue.Substring(0, 2), NumberStyles.HexNumber);
				var g = byte.Parse(hexValue.Substring(2, 2), NumberStyles.HexNumber);
				var b = byte.Parse(hexValue.Substring(4, 2), NumberStyles.HexNumber);

				_cachedColorString = $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100}";
				return _cachedColorString;
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
			PermanentMarker
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

		#endregion

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

		#endregion

		#region Data

		#region Data.General

		public void SaveData()
		{
			SaveLocalizationData();
		}

		private void LoadData()
		{
			LoadLocalizationData();
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

		#region Data.Localization

		private static LocalizationData _localizationData;

		private void SaveLocalizationData()
		{
			SaveDataToFile(_localizationData, $"{Name}/Localization");
		}

		private void LoadLocalizationData()
		{
			LoadDataFromFile(ref _localizationData, $"{Name}/Localization");
		}

		private class LocalizationData
		{
			[JsonProperty(PropertyName = "Localization Settings")]
			public LocalizationSettings Localization = new();
		}

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

		#endregion Data.Localization

		#endregion Data

		#region Hooks

		private void Init()
		{
			Instance = this;
		}

		private void OnServerInitialized()
		{
			LoadData();

			LoadImages();

			LoadPopUps();

			RegisterPermissions();

			RegisterCommands();
		}

		private void Unload()
		{
			Instance = null;
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

		#endregion

		#region Commands

		private void CmdOpenPopUp(IPlayer covPlayer, string command, string[] args)
		{
			var player = covPlayer?.Object as BasePlayer;
			if (player == null) return;
			
			if (_enabledImageLibrary == false)
			{
				SendNotify(player, NoILError, 1);

				BroadcastILNotInstalled();
				return;
			}
			
			GetPopByCommand(command)?.Show(player);
		}

		[ConsoleCommand(CmdMainConsole)]
		private void CmdConsolePopUps(ConsoleSystem.Arg arg)
		{
			var player = arg?.Player();
			if (player == null) return;

#if TESTING
			SayDebug(player, CmdMainConsole, $"called: {string.Join(" ", arg.Args)}");
#endif
            
			switch (arg.GetString(0))
			{
				case "close":
				{
					EditPopUpData.Remove(player.userID);
					break;
				}
				
				case "edit_popup":
				{
					if (!CanPlayerEdit(player)) return;

					switch (arg.GetString(1))
					{
						case "start":
						{
							var popUpID = arg.GetInt(2);

							EditPopUpData.Create(player, popUpID);

							ShowPageEditorPanel(player);
							break;
						}

						case "save":
						{
							var editData = EditPopUpData.Get(player.userID);

							editData?.Save();
							break;
						}

						case "change_position":
						{
							var editPageData = EditPopUpData.Get(player.userID);

							ServerPanel?.Call("API_OnServerPanelEditorChangePosition", player);
                            
							editPageData?.OnChangePosition();
							break;
						}

						case "element":
						{
							var editData = EditPopUpData.Get(player.userID);

							switch (arg.GetString(2))
							{
								case "add":
								{
									var targetList = arg.GetString(3);

									switch (targetList)
									{
										case "content":
										{
											editData.popUpEntry.Content.ContentElements.Add(UiElement.CreatePanel(
												InterfacePosition.CreatePosition(0.5f, 0.5f, 0.5f, 0.5f, -50, -50, 50, 50),
												new IColor("#FFFFFF", 100)));
											break;
										}

										case "scroll":
										{
											editData.popUpEntry.Content.ScrollView.ScrollElements.Add(UiElement.CreatePanel(
												InterfacePosition.CreatePosition(0.5f, 0.5f, 0.5f, 0.5f, -50, -50, 50, 50),
												new IColor("#FFFFFF", 100)));
											break;
										}
									}
									
									SaveConfig();
									
									editData.UpdateContent();
									
									ShowPageEditorPanel(player);
									break;
								}

								case "remove":
								{
									if (!arg.HasArgs(4)) return;

									var index = arg.GetInt(4);
									
									var targetList = arg.GetString(3);

									switch (targetList)
									{
										case "content":
										{
											editData.popUpEntry.Content.ContentElements.RemoveAt(index);
											break;
										}

										case "scroll":
										{
											editData.popUpEntry.Content.ScrollView.ScrollElements.RemoveAt(index);
											break;
										}
									}

									SaveConfig();
									
									editData.UpdateContent();
									
									ShowPageEditorPanel(player);
									break;
								}

								case "move":
								{
									if (!arg.HasArgs(4)) return;
									
									var targetList = arg.GetString(3);
									var targetMode = arg.GetString(4);
									var index = arg.GetInt(5);

									switch (targetList)
									{
										case "content":
										{
											switch (targetMode)
											{
												case "up":
												{
													editData.popUpEntry.Content.ContentElements.MoveUp(index);
													break;
												}

												case "down":
												{
													editData.popUpEntry.Content.ContentElements.MoveDown(index);
													break;
												}
											}
											
											break;
										}

										case "scroll":
										{
											switch (targetMode)
											{
												case "up":
												{
													editData.popUpEntry.Content.ScrollView.ScrollElements.MoveUp(index);
													break;
												}

												case "down":
												{
													editData.popUpEntry.Content.ScrollView.ScrollElements.MoveDown(index);
													break;
												}
											}

											break;
										}
										
										default:
											return;
									}
									
									SaveConfig();
									
									editData.UpdateContent();
									
									ShowPageEditorPanel(player);
									break;
								}

								case "clone":
								{
									var targetType = arg.GetString(3);

									var elements = targetType switch
									{
										"content" => editData.popUpEntry.Content.ContentElements,
										"scroll" => editData.popUpEntry.Content.ScrollView.ScrollElements,
										_ => null
									};
									if (elements == null) return;
									
									var elementIndex = arg.GetInt(4);
									if (elementIndex < 0 || elementIndex >= elements.Count) return;

									var originalElement = elements[elementIndex];
									var clonedElement = new UiElement(originalElement);

									var originalName = originalElement.Name;
									var newName = originalName;
									
									var counter = 1;
									while (elements.Any(e => e.Name == newName))
									{
										newName = $"{originalName} ({counter})";
										counter++;
									}
									clonedElement.Name = newName;

									elements.Add(clonedElement);

									SaveConfig();
									
									editData.UpdateContent();
									
									ShowPageEditorPanel(player);
									break;
								}

								case "switch_show":
								{
									if (!arg.HasArgs(4)) return;

									var index = arg.GetInt(4);
									
									var targetList = arg.GetString(3);

									UiElement element = null;
									switch (targetList)
									{
										case "content":
										{
											element = editData.popUpEntry.Content.ContentElements[index];
											element.Visible = !element.Visible;
											
											UpdateUI(player, container =>
											{
												element.Get(ref container, 
													player,
													Layer + ".Content",
													element.Name, element.Name, needUpdate: true);
											});
											break;
										}

										case "scroll":
										{
											element = editData.popUpEntry.Content.ScrollView.ScrollElements[index];
											element.Visible = !element.Visible;
											
											UpdateUI(player, container =>
											{
												element.Get(ref container, 
													player,
													Layer + ".Scroll.View",
													element.Name, element.Name, needUpdate: true);
											});
											break;
										}
										default:
											return;
									}

									SaveConfig();
									
                                    
									UpdateUI(player, container =>
									{
										UpdatePointPageEditorUI(container, 
											targetList,
											index,
											element,
											string.Join(" ", arg.Args.SkipLast(2)));
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
						case "start":
						{
							var targetList = arg.GetString(2);
							var elementIndex = arg.GetInt(3);
							
							var editPageData = EditPopUpData.Get(player.userID);
							if (!editPageData.StartEditElement(elementIndex, targetList))
								return;

							ShowElementEditorPanel(player);
							break;
						}

						case "cancel":
						{
							var editPageData = EditPopUpData.Get(player.userID);
							editPageData.EndEditElement(true);
							break;
						}

						case "save":
						{
							var editPageData = EditPopUpData.Get(player.userID);
							
							UpdateUI(player, container =>
							{
								UpdateTitlePageEditorFieldUI(container, editPageData.editingElementLayer,
									editPageData.elementIndex,
									editPageData.editingElement, true);
							});
							
							editPageData.EndEditElement();
							break;
						}

						case "change_position":
						{
							var editPageData = EditPopUpData.Get(player.userID);

							ServerPanel?.Call("API_OnServerPanelEditorChangePosition", player);
                            
							editPageData?.OnChangePosition();
							
							ShowElementEditorPanel(player);
							break;
						}

						case "field":
						{
							var editPageData = EditPopUpData.Get(player.userID);

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
							
							if (fieldName == nameof(UiElement.Type))
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
											needAddImage: fieldName == nameof(UiElement.Image));
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
											needUpdate: editPageData.editingElement.Type == CuiElementType.Label);

									FieldElementUI(container, arg.GetString(0), parent, targetField, targetField.GetValue(editPageData.editingElement));
								});

							break;
						}

						case "color":
						{
							var editPageData = EditPopUpData.Get(player.userID);
							
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

									switch (arg.GetString(5))
									{
										case "hex":
										{
											var hex = string.Join(" ", arg.Args.Skip(6));
											if (string.IsNullOrEmpty(hex)) return;

											var str = hex.Trim('#');
											if (!str.IsHex())
												return;

											if (targetField.GetValue(editPageData.editingElement) is not IColor targetValue) return;

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

											if (targetField.GetValue(editPageData.editingElement) is not IColor targetValue) return;

											targetValue.Alpha = opacity;
											
											targetField.SetValue(editPageData.editingElement, targetValue);
											break;
										}
									}
									
									UpdateUI(player, container =>
									{
										if (editPageData.isTextEditing)
											ShowTextEditorLinesUI(player, ref container);
										else
											editPageData.UpdateEditElement(ref container, player);
										
										FieldElementUI(container, arg.GetString(0), parent, targetField,
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
							var editPageData = EditPopUpData.Get(player.userID);

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
									var editPageData = EditPopUpData.Get(player.userID);
									
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
									var editPageData = EditPopUpData.Get(player.userID);

									editPageData.ExpertMode = !editPageData.ExpertMode;
									
									UpdateUI(player, container =>
									{
										PositionSectionUI(player, container, arg.GetString(0), editPageData.editingElement);
									});
									
									break;
								}
								
								case "enter":
								{
									var editPageData = EditPopUpData.Get(player.userID);
									
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
			}
		}

		[ConsoleCommand("serverpanelpopups_broadcastvideo")]
		private void CmdBroadcastVideo(ConsoleSystem.Arg arg)
		{
			var player = arg?.Player();
			if (player == null) return;

			var videoURL = string.Join(" ", arg.Args);
			if (string.IsNullOrWhiteSpace(videoURL)) return;

			player.Command("client.playvideo", videoURL);
		}

		#endregion Commands

		#region Interface

		#region Editor.UI

		private void ShowPageEditorPanel(BasePlayer player)
		{
			var container = new CuiElementContainer();

			var editData = EditPopUpData.Get(player.userID);

			#region Background

			container.Add(new CuiElement()
			{
				Parent = Layer,
				Name = EditingLayerPopUpEditor,
				DestroyUi = EditingLayerPopUpEditor,
				Components =
				{
					new CuiImageComponent(){Color = "0 0 0 0"},
					ServerPanel?.Call<CuiRectTransformComponent>("API_OnServerPanelEditorGetPosition", player) ?? new CuiRectTransformComponent
					{
						AnchorMin = "0 0",
						AnchorMax = "0 1",
						OffsetMin = "0 0",
						OffsetMax = "260 0"
					}
				}
			});
			
			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image =
				{
					Color = "0 0 0 0.9",
					Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
				}
			}, EditingLayerPopUpEditor);

			#region Title

			container.Add(new CuiElement
			{
				Parent = EditingLayerPopUpEditor,
				Components =
				{
					new CuiTextComponent
					{
						Text = "POP UP\nSETTINGS",
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

			ShowCloseButtonUI(container, EditingLayerPopUpEditor, EditingLayerPopUpEditor + ".CloseButton",
				EditingLayerPopUpEditor,
				$"{CmdMainConsole} edit_popup save");

			ShowCloseButtonUI(container, EditingLayerPopUpEditor,
				EditingLayerPopUpEditor + ".ChangePosition",
				EditingLayerPopUpEditor,
				commandOnClose: $"{CmdMainConsole} edit_popup change_position",
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
				OffsetMin = "0 -{PAGE_EDITOR_SCROLL_SIZE}",
				OffsetMax = "0 0"
			};
			container.Add(new CuiElement
			{
				Parent = EditingLayerPopUpEditor,
				Name = EditingLayerPopUpEditor + ".Selection",
				DestroyUi = EditingLayerPopUpEditor + ".Selection",
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
						VerticalScrollbar = new CuiScrollbar
						{
							Size = 5f, AutoHide = false,
							HandleColor = HexToCuiColor("#D74933")
						}
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 55", OffsetMax = "-10 -145"
					}
				}
			});

			#endregion

			#region Fields

			fieldHeight = 40f;
			fieldMarginY = 0f;
			
			var targetFields = Array.FindAll(editData.popUpEntry.GetType().GetFields(),
				field => field.FieldType.IsPrimitive && field.Name != nameof(PopUpEntry.ContentUI.UseScrolling));

			TitleEditorUI(container, EditingLayerPopUpEditor + ".Selection", ref offsetY, "OPTIONS", margin: 0f);

			foreach (var property in targetFields)
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
					}, 
					EditingLayerPopUpEditor + ".Selection", 
					targetFieldLayer + ".Background", 
					targetFieldLayer + ".Background");

				FieldElementUI(container, "edit_popup", targetFieldLayer, property, property.GetValue(editData.popUpEntry));

				offsetY = offsetY - fieldHeight - fieldMarginY;
			}

			offsetY = offsetY - 20f;
			
			#endregion

			fieldHeight = 40f;
			fieldMarginY = 10f;
			
			#region Content Elements
			
			TitleEditorUI(container, EditingLayerPopUpEditor + ".Selection", ref offsetY, "CONTENT ELEMENTS", margin: 0f);
			
			foreach (var cuiElement in editData.popUpEntry.Content.ContentElements)
			{
				var elementIndex = editData.popUpEntry.Content.ContentElements.IndexOf(cuiElement);
				
				container.Add(new CuiPanel
					{
						Image = {Color = "0.3019608 0.3019608 0.3019608 0.4"},
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"10 {offsetY - fieldHeight}",
							OffsetMax = $"-20 {offsetY}"
						}
					}, EditingLayerPopUpEditor + ".Selection", 
					EditingLayerPopUpEditor + $".Selection.Element.Content.{elementIndex}", 
					EditingLayerPopUpEditor + $".Selection.Element.Content.{elementIndex}");

				PageEditorFieldUI(container, "content", elementIndex, cuiElement);

				offsetY = offsetY - fieldHeight - fieldMarginY;
			}

			if (editData.popUpEntry.Content.ContentElements.Count > 0)
			{
				container.Add(new CuiPanel
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
				}, EditingLayerPopUpEditor + ".Selection");

				offsetY = offsetY - 2f - fieldMarginY;
			}

			container.Add(new CuiButton
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
					Command = $"{CmdMainConsole} edit_popup element add content"
				}
			}, EditingLayerPopUpEditor + ".Selection");
            
			offsetY = offsetY - fieldHeight - fieldMarginY;
			
			#endregion Content Elements

			offsetY = offsetY - 30f;

			#region Scroll Elements

			TitleEditorUI(container, EditingLayerPopUpEditor + ".Selection", ref offsetY, "SCROLL ELEMENTS");

			foreach (var cuiElement in editData.popUpEntry.Content.ScrollView.ScrollElements)
			{
				var elementIndex = editData.popUpEntry.Content.ScrollView.ScrollElements.IndexOf(cuiElement);

				container.Add(new CuiPanel
					{
						Image = {Color = "0.3019608 0.3019608 0.3019608 0.4"},
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"10 {offsetY - fieldHeight}",
							OffsetMax = $"-20 {offsetY}"
						}
					}, EditingLayerPopUpEditor + ".Selection", 
					EditingLayerPopUpEditor + $".Selection.Element.Scroll.{elementIndex}", 
					EditingLayerPopUpEditor + $".Selection.Element.Scroll.{elementIndex}");

				PageEditorFieldUI(container, "scroll", elementIndex, cuiElement);
				
				offsetY = offsetY - fieldHeight - fieldMarginY;
			}

			if (editData.popUpEntry.Content.ContentElements.Count > 0)
			{
				container.Add(new CuiPanel
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
				}, EditingLayerPopUpEditor + ".Selection");

				offsetY = offsetY - 2f - fieldMarginY;
			}

			container.Add(new CuiButton
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
					Command = $"{CmdMainConsole} edit_popup element add scroll"
				}
			}, EditingLayerPopUpEditor + ".Selection");

			offsetY = offsetY - fieldHeight - fieldMarginY;

			#endregion

			scrollRect.OffsetMin = $"0 -{Mathf.Abs(offsetY) + 100:N}";
			
			#endregion Selection

			CuiHelper.AddUi(player, container);
		}

		private void ShowElementEditorPanel(BasePlayer player)
		{
			var container = new CuiElementContainer();

			var editData = EditPopUpData.Get(player.userID);

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
					ServerPanel?.Call<CuiRectTransformComponent>("API_OnServerPanelEditorGetPosition", player) ?? new CuiRectTransformComponent
					{
						AnchorMin = "0 0",
						AnchorMax = "0 1",
						OffsetMin = "0 0",
						OffsetMax = "260 0"
					}
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
				EditingLayerElementEditor + ".CloseButton", EditingLayerElementEditor,
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

			FieldElementUI(container, "edit_element", enabledLayer, enabledField, enabledField?.GetValue(targetElement));

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

			FieldElementUI(container,"edit_element", typeLayer, typeField, typeField?.GetValue(targetElement));

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

			FieldElementUI(container, "edit_element",nameLayer, nameField, nameField?.GetValue(targetElement));

			offsetY = offsetY - fieldHeight - fieldMarginY;

			#endregion Name Section

			#region Rect Transform Section

			TitleEditorUI(container, EditingLayerElementEditor + ".Selection", ref offsetY, "RECT TRANSFORM", margin: fieldMarginY);

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
				TitleEditorUI(container, EditingLayerElementEditor + ".Selection", ref offsetY, "TEXT STYLE", margin: fieldMarginY);

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
				TitleEditorUI(container, EditingLayerElementEditor + ".Selection", ref offsetY, "PANEL STYLE", margin: fieldMarginY);

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
					}, EditingLayerElementEditor + ".Selection", targetFieldLayer + ".Background", targetFieldLayer + ".Background");

					FieldElementUI(container,"edit_element", targetFieldLayer, panelField, panelField.GetValue(targetElement));

					offsetY = offsetY - fieldHeight - fieldMarginY;
				}
			}

			#endregion Panel Section

			#region Image Section

			if (targetElement.Type == CuiElementType.Image)
			{
				TitleEditorUI(container, EditingLayerElementEditor + ".Selection", ref offsetY, "IMAGE STYLE", margin: fieldMarginY);

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
					}, EditingLayerElementEditor + ".Selection", targetFieldLayer + ".Background", targetFieldLayer + ".Background");

					FieldElementUI(container,"edit_element", targetFieldLayer, textField, textField.GetValue(targetElement));

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

					FieldElementUI(container,"edit_element", targetFieldLayer, textField, textField.GetValue(targetElement));

					offsetY = offsetY - fieldHeight - fieldMarginY;
				}

				#endregion
			}

			#endregion

			scrollRect.OffsetMin = $"0 {offsetY:N}";
			
			#endregion Selection

			CuiHelper.AddUi(player, container);
		}

		#region Editor.UI.Components

		private void PageEditorFieldUI(CuiElementContainer container,
			string targetType,
			int elementIndex, 
			UiElement cuiElement,
			string cmdElementRemove = "edit_popup element remove",
			string cmdElementClone = "edit_popup element clone",
			string cmdElementSwitch = "edit_popup element switch_show",
			string cmdStartEdit = "edit_element start",
			string cmdMove = "edit_popup element move")
		{
			var parentLayer = GetEditorFieldParentLayer(targetType, elementIndex);
			if (string.IsNullOrWhiteSpace(parentLayer)) return;

			var cuiLayer = container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image = {Color = "0 0 0 0"}
				}, parentLayer,
				parentLayer + ".Panel",
				parentLayer + ".Panel");

			UpdatePointPageEditorUI(container, targetType, elementIndex, cuiElement, cmdElementSwitch);

			UpdateTitlePageEditorFieldUI(container, targetType, elementIndex, cuiElement);

			AddButton(cmdElementRemove, "ServerPanel_Editor_Btn_Remove", "-60 -6", "-48 6");
			AddButton(cmdElementClone, "ServerPanel_Editor_Btn_Clone", "-44 -6", "-32 6");
			AddButton(cmdStartEdit, "ServerPanel_Editor_Btn_Edit", "-28 -5", "-16 6");
            AddButton(cmdMove + " up", "ServerPanel_Editor_Btn_Up", "-12 1", "-5 6");
			AddButton(cmdMove + " down", "ServerPanel_Editor_Btn_Down", "-12 -6", "-5 -1");

			#region Helpers

			void AddButton(string command, string image, string offsetMin, string offsetMax)
			{
				container.Add(new CuiElement()
				{
					Name = cuiLayer + ".Button." + command.Replace(" ", "."),
					Parent = cuiLayer,
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
					Parent = cuiLayer + ".Button." + command.Replace(" ", "."),
					Components =
					{
						new CuiButtonComponent()
						{
							Color = "0 0 0 0",
							Command = $"{CmdMainConsole} {command} {targetType} {elementIndex}",
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
			string targetType,
			int elementIndex,
			UiElement cuiElement,
			string cmdElementSwitch)
		{
			var parentLayer = GetEditorFieldParentLayer(targetType, elementIndex);
			if (string.IsNullOrWhiteSpace(parentLayer)) return;

			#region Point

			container.Add(new CuiElement()
			{
				Parent = parentLayer,
				Name = parentLayer + ".Point",
				DestroyUi = parentLayer + ".Point",
				Components =
				{
					new CuiButtonComponent()
					{
						Color = "0 0 0 0",
						Command = $"{CmdMainConsole} {cmdElementSwitch} {targetType} {elementIndex}"
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
				Parent = parentLayer + ".Point",
				Components =
				{
					new CuiRawImageComponent()
					{
						Png = Instance.GetImage(cuiElement.Visible ? "ServerPanel_Editor_Visible_On" : "ServerPanel_Editor_Visible_Off")
					},
					new CuiRectTransformComponent() {AnchorMin = "0 0", AnchorMax = "1 1"}
				}
			});

			#endregion
		}

		private static string GetEditorFieldParentLayer(string targetType, int elementIndex)
		{
			return targetType switch
			{
				"content" => EditingLayerPopUpEditor + $".Selection.Element.Content.{elementIndex}",
				"scroll" => EditingLayerPopUpEditor + $".Selection.Element.Scroll.{elementIndex}",
				_ => null
			};
		}

		private static void UpdateTitlePageEditorFieldUI(CuiElementContainer container, 
			string targetType,
			int elementIndex,
			UiElement cuiElement, bool needUpdate = false)
		{
			var parentLayer = GetEditorFieldParentLayer(targetType, elementIndex);
			if (string.IsNullOrWhiteSpace(parentLayer)) return;

			var element = new CuiElement()
			{
				Name = parentLayer + ".Title",
				Parent = parentLayer,
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
		
		private static void ShowEditButtonUI(BasePlayer player, CuiElementContainer container,
			string parent,
			string name = "",
			string closeLayer = "",
			string cmdEdit = "")
		{
			if (string.IsNullOrEmpty(name))
				name = CuiHelper.GetGuid();

			var Background = UiElement.CreatePanel(
				InterfacePosition.CreatePosition("1 1", "1 1", "10 -40", "50 0"),
				IColor.Create("#CD4632"));
			var Title = UiElement.CreateImage(
				InterfacePosition.CreatePosition("0.5 0.5", "0.5 0.5", "-9 -9", "9 9"),
				"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/serverpanel-icon-edit-ui.png");

			var DescriptionBackground = UiElement.CreatePanel(
				InterfacePosition.CreatePosition("0 0", "1 0", "0 -18"),
				IColor.Create("#CD4632", 70));

			var DescriptionTitle = UiElement.CreateLabel(
				InterfacePosition.CreatePosition(),
				IColor.Create("#FFFFFF"), "Edit UI", align: TextAnchor.MiddleCenter, font:
				"robotocondensed-regular.ttf", fontSize: 10);

			Background.Get(ref container, player, parent, name);
			Title.Get(ref container, player, name);
			DescriptionBackground.Get(ref container, player, name, name + ".Description");
			DescriptionTitle.Get(ref container, player, name + ".Description");

			container.Add(new CuiElement
			{
				Parent = name,
				Components =
				{
					new CuiButtonComponent
					{
						Color = "0 0 0 0",
						Command = cmdEdit,
						Close = closeLayer
					}
				}
			});
		}
		
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

		private static void FieldElementUI(CuiElementContainer container, 
			string parentCommand,
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
							Command = $"{CmdMainConsole} {parentCommand} field {targetField.Name} {targetFieldLayer} {!boolValue}",
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
									$"{CmdMainConsole} {parentCommand} color start {targetField.Name} {targetFieldLayer}"
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
							new CuiTextComponent()
							{
								FontSize = 12,
								Font = "robotocondensed-bold.ttf",
								Align = TextAnchor.MiddleCenter,
								Color = HexToCuiColor("#E2DBD3", 90),
								Text = targetStr ?? string.Empty,
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
							Command = $"{CmdMainConsole} {parentCommand} field {targetField.Name} {targetFieldLayer} prev",
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
							Command = $"{CmdMainConsole} {parentCommand} field {targetField.Name} {targetFieldLayer} next",
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
								Command = $"{CmdMainConsole} {parentCommand} field {targetField.Name} {targetFieldLayer}",
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
								$"{CmdMainConsole} {parentCommand} field {targetField.Name} {targetFieldLayer} {numberValue - stepSize}",
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
								$"{CmdMainConsole} {parentCommand} field {targetField.Name} {targetFieldLayer} {numberValue + stepSize}",
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
								Command = $"{CmdMainConsole} {parentCommand} field {targetField.Name} {targetFieldLayer}",
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

		private static void PositionSectionUI(BasePlayer player,
			CuiElementContainer container,
			string parentCommand,
			InterfacePosition pos)
		{
			var editPageData = EditPopUpData.Get(player.userID);

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

		private static void FormattingFieldElementUI(
			BasePlayer player,
			CuiElementContainer container)
		{
			var elementData = EditPopUpData.Get(player.userID);

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

		#endregion Editor.UI.Components
		
		#region Editor Selection Panels

		private void ShowColorSelectionPanel(BasePlayer player, string fieldName, string parentLayer)
		{
			var editPageData = EditPopUpData.Get(player.userID);

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
							Command = $"{CmdMainConsole} edit_element color set {fieldName} {parentLayer} hex {ColorUtility.ToHtmlStringRGB(targetColor)}"
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
							Color = selectedColor.Get()
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
			var elementData = EditPopUpData.Get(player.userID);
			
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
				RectTransform ={ AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-640 -360", OffsetMax = "640 360" },
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
					{ AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "40 -75", OffsetMax = "0 -25" },
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
			
			var totalHeight = _langList.Count * UI_TextEditor_Lang_Height + (_langList.Count - 1) * UI_TextEditor_Lang_Margin_Y;

			#endregion
			
			container.Add(new CuiPanel
			{
				RectTransform =
					{AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "20 -585", OffsetMax = "300 -135"},
				Image =
				{
					Color = "0 0 0 0"
				}
			}, EditingLayerModalTextEditor + ".Main", EditingLayerModalTextEditor + ".Main.Select.Lang", EditingLayerModalTextEditor + ".Main.Select.Lang");

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
			}, EditingLayerModalTextEditor + ".Main.Select.Lang", EditingLayerModalTextEditor + ".Main.Edit.Select.Lang.Panel", EditingLayerModalTextEditor + ".Main.Edit.Select.Lang.Panel");

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
					{ AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-320 -585", OffsetMax = "320 -135" },
				Image =
				{
					Color = "0 0 0 0"
				}
			}, EditingLayerModalTextEditor + ".Main", EditingLayerModalTextEditor + ".Main.Edit.Text", EditingLayerModalTextEditor + ".Main.Edit.Text");

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
			
			totalHeight = targetFields.Count * UI_TextEditor_TextStyle_Height + (targetFields.Count - 1) * UI_TextEditor_TextStyle_Margin_Y;

			totalHeight = Mathf.Max(totalHeight, 450);

			#endregion
			
			container.Add(new CuiPanel
			{
				RectTransform =
					{ AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-300 -585", OffsetMax = "-20 -135" },
				Image =
				{
					Color = "0 0 0 0"
				}
			}, EditingLayerModalTextEditor + ".Main", EditingLayerModalTextEditor + ".Main.Text.Style", EditingLayerModalTextEditor + ".Main.Text.Style");

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
						{ AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -50", OffsetMax = "0 0" }
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
			}, EditingLayerModalTextEditor + ".Main.Text.Style", EditingLayerModalTextEditor + ".Main.Edit.Text.Style.Panel", EditingLayerModalTextEditor + ".Main.Edit.Text.Style.Panel");

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

				FieldElementUI(container, "edit_element", EditingLayerModalTextEditor + $".Main.Text.Style.Field.{targetField.Name}", targetField,
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

			var elementData = EditPopUpData.Get(player.userID);

			var text = elementData.GetEditableText();

			var fontSize = elementData.editingElement.FontSize;

			var textLineHeight = fontSize * 1.5f;

			var totalHeight = 0f;
			foreach (var textLine in text)
			{
				var targetHeight = textLineHeight;

				var textSize = CalcTextSize(textLine, fontSize);
				if (textSize.x > UI_TextEditor_Lines_Width)
				{
					var xSize = Mathf.CeilToInt(textSize.x / UI_TextEditor_Lines_Width);
					
					targetHeight = textLineHeight * xSize;
				}
				
				totalHeight += targetHeight;
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
			var editPageData = EditPopUpData.Get(player.userID);

			var offsetY = 0f;
			foreach (var (flagPath, langKey, langName) in _langList)
			{
				var selectedLang = editPageData.IsSelectedLang(langKey);

				container.Add(new CuiPanel
					{
						Image = {
							Color = selectedLang ? HexToCuiColor("#A5EA32", 20) : HexToCuiColor("#4D4D4D", 40)},
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"0 {offsetY - UI_TextEditor_Lang_Height}", OffsetMax = $"-10 {offsetY}"
						}
					}, EditingLayerModalTextEditor + ".Main.Select.Lang.Scroll.View", EditingLayerModalTextEditor + $".Main.Select.Lang.Item.{langKey}",
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

			var elementData = EditPopUpData.Get(player.userID);

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
			UpdateUI(player, container =>
			{
				var closeLayer = ShowTextEditorCloseButton(container, $"{CmdMainConsole} edit_element text close",
					close: EditingLayerModalTextEditor);

				container.Add(new CuiButton()
				{
					Button =
					{
						Color = "0.1137255 0.2588235 0.372549 1",
						Close = EditingLayerModalTextEditor + ".Main" + ".BTN.Close.Edit" + ".Notify"
					},
					Text =
					{
						Text = Msg(player,
							"You are about to close the text editor. All unsaved changes will be lost.\nTo confirm closing the editor and lose all unsaved data, click the close icon again."),
						Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter,
						Color = "0.282353 0.6039216 0.8313726 1"
					},
					RectTransform = {AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "-440 -50", OffsetMax = "0 0"}
				}, closeLayer, closeLayer + ".Notify", closeLayer + ".Notify");
			});
		}

		#endregion

		#endregion
		
		#endregion Editor.UI

		#endregion

		#region API

		private void API_OnServerPanelPopUpsUpdateText(Dictionary<string, string> targetUpdateFields)
		{
			_config?.PopUps?.ForEach(popUp =>
			{
				popUp.GetAllUiElements().ForEach(uiElement =>
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
			});

			SaveConfig();
		}

		#endregion

		#region Utils
		
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
					"https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/Anchors/stretch-stretch.png"
			};

		#region Edit Page

		private Dictionary<ulong, EditPopUpData> editPopUps = new();

		private class EditPopUpData
		{
			#region Page

			public ulong playerID;

			public int Category;

			public PopUpEntry popUpEntry;

			public static EditPopUpData Create(BasePlayer player, int popUpID)
			{
				var data = new EditPopUpData
				{
					playerID = player.userID,
					Category = popUpID,
					popUpEntry = Instance.GetPopUpByID(popUpID)
				};
				
				Instance?.editPopUps.TryAdd(player.userID, data);

				return data;
			}

			public static EditPopUpData Get(ulong playerID)
			{
				return Instance?.editPopUps.TryGetValue(playerID, out var data) == true ? data : null;
			}

			public static void Remove(ulong playerID)
			{
				Instance?.editPopUps?.Remove(playerID);
			}

			public void Save()
			{
				var popUp = Instance?.GetPopUpByID(Category);
				if (popUp != null)
				{
					var targetIndex = Instance._config.PopUps.IndexOf(popUp);

					Instance._config.PopUps[targetIndex] = popUpEntry;
				}
				else
				{
					Instance?._config.PopUps.Add(popUpEntry);
				}
				
				Instance?.editPopUps?.Remove(playerID);
				
				Instance?.SaveConfig();
			}

			#endregion

			#region Edit Element

			public UiElement editingElement;

			public string
				editingElementLayer,
				editingElementName;

			public int elementIndex;

			public float movementStep;

			public bool ExpertMode;
			
			public bool StartEditElement(int elementID, string targetList)
			{
				elementIndex = elementID;

				editingElementLayer = targetList;

				movementStep = 10f;

				ExpertMode = false;
				
				if (elementID >= 0 && elementID < GetTargetEditingElements().Count)
				{
					editingElement = GetTargetEditingElements()[elementID];

					editingElementName = editingElement.Name;

					CreateEditableOutline();
				}

				return editingElement != null;
			}

			public void SetMovementStep(float step)
			{
				movementStep = step;
			}

			private List<UiElement> GetTargetEditingElements()
			{
				return editingElementLayer switch
				{
					"content" => popUpEntry.Content.ContentElements,
					"scroll" => popUpEntry.Content.ScrollView.ScrollElements,
					_ => new List<UiElement>()
				};
			}

			private string GetElementParentLayer()
			{
				return editingElementLayer switch
				{
					"content" => Layer + ".Content",
					"scroll" => Layer + ".Scroll.View",
					_ => string.Empty
				};
			}
			
			public void EndEditElement(bool cancel = false)
			{
				DestroyEditableOutline();
				
				if (cancel)
				{
					editingElement = null;
					return;
				}

				GetTargetEditingElements()[elementIndex] = editingElement;
			}

			public void UpdateEditElement(ref CuiElementContainer container, BasePlayer player,
				bool needAddImage = false,
				bool needUpdate = false)
			{
				editingElement.InvalidateCache();
				
				if (needAddImage && editingElement.Type == CuiElementType.Image && editingElement.TryGetImage(out var image))
					Instance?.AddImage(image, image);

				editingElement.Get(ref container, player, GetElementParentLayer(), editingElement.Name,
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
			}

			public void StopTextEditing()
			{
				isTextEditing = false;
				
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
			
			#region Content
			
			public void UpdateContent()
			{
				if (BasePlayer.TryFindByID(playerID, out var player))
					UpdateUI(player, container => popUpEntry.ShowContent(player, container));
			}
			
			#endregion
			
			#region Change Position

			public void OnChangePosition()
			{
				if (BasePlayer.TryFindByID(playerID, out var player))
					Instance.ShowPageEditorPanel(player);
			}

			#endregion
		}

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

			var imagesList = new Dictionary<string, string>();

			foreach (var rectImage in rectToImage.Values)
				RegisterImage(ref imagesList, rectImage, rectImage);

			_config?.PopUps?.ForEach(popUp =>
			{
				popUp?.GetAllUiElements()?.ForEach(uiElement =>
				{
					if (uiElement.TryGetImage(out var image)) 
						RegisterImage(ref imagesList, image, image);
				});
			});

#if CARBON
            imageDatabase.Queue(true, imagesList);
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

		private void LoadPopUps()
		{
			_popUpByID.Clear();
			_popUpByCommand.Clear();
			
			for (var index = 0; index < _config.PopUps.Count; index++)
			{
				var popUp = _config.PopUps[index];
				if (!popUp.Enabled) continue;

				_popUpByID[popUp.ID] = index;

				foreach (var popUpCommand in popUp.Commands)
					if (!_popUpByCommand.TryAdd(popUpCommand, index))
						PrintError($"{popUpCommand} already defined!");
			}
		}

		private void RegisterCommands()
		{
			AddCovalenceCommand(_popUpByCommand.Keys.ToArray(), nameof(CmdOpenPopUp));
		}

		private void RegisterPermissions()
		{
			var menuPermissions = new HashSet<string>
			{
				Perm_Edit
			};

			foreach (var perm in menuPermissions)
				if (!permission.PermissionExists(perm))
					permission.RegisterPermission(perm, this);
		}

		private PopUpEntry GetPopByCommand(string command)
		{
			return _popUpByCommand.TryGetValue(command, out var popIndex) && popIndex >= 0 &&
			       popIndex < _config.PopUps.Count
				? _config.PopUps[popIndex]
				: null;
		}

		private PopUpEntry GetPopUpByID(int popUpID)
		{
			return _popUpByID.TryGetValue(popUpID, out var popIndex) && popIndex >= 0 && popIndex < _config.PopUps.Count
				? _config.PopUps[popIndex]
				: null;
		}

		private static int GetUniquePopUpID()
		{
			int categoryID;
			do
			{
				categoryID = Random.Range(int.MinValue, int.MaxValue);
			} while (Instance?._popUpByID?.ContainsKey(categoryID) == true);

			return categoryID;
		}

		private static bool CanPlayerEdit(BasePlayer player)
		{
			return player.HasPermission("serverpanel.edit") || player.HasPermission(Perm_Edit);
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

		#endregion

		#region Lang
		
		private const string
			NoILError = "NoILError";
		
		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				[NoILError] = "The plugin does not work correctly, contact the administrator!",
			}, this);
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
			Debug.Log($"[ServerPanel.PopUps | {hook} | {player.UserIDString}] {message}");
		}

		private static void SayDebug(ulong player, string hook, string message)
		{
			Debug.Log($"[ServerPanel.PopUps | {hook} | {player}] {message}");
		}

		private static void SayDebug(string message)
		{
			Debug.Log($"[ServerPanel.PopUps] {message}");
		}
#endif

		#endregion
	}
}

#region Extension Methods

namespace Oxide.Plugins.ServerPanelPopUpsExtensionMethods
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
			{
				while (c.MoveNext()) b.Add(c.Current);
			}

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
