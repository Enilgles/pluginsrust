// #define TESTING

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Oxide.Core.Plugins;

#if CARBON
using Carbon.Base;
using Carbon.Modules;
#endif

namespace Oxide.Plugins
{
	[Info("ServerPanel Avatars", "Mevent", "1.1.3")]
	public class ServerPanelAvatars : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin ImageLibrary = null;

#if CARBON
		private ImageDatabaseModule imageDatabase;
#endif

		#endregion

		#region Hooks

		private void OnServerInitialized()
		{
			LoadImages();
		}
	
		private void OnPlayerConnected(BasePlayer player)
		{
			if (player == null || !player.userID.IsSteamId()) return;

			GetAvatar(player.UserIDString,
				avatar => AddImage(avatar, $"avatar_{player.UserIDString}"));
		}

		#endregion

		#region Utils

		#region Avatar

		private readonly Regex Regex = new(@"<avatarFull><!\[CDATA\[(.*)\]\]></avatarFull>");

		private void GetAvatar(string userId, Action<string> callback)
		{
			if (callback == null) return;

			try
			{
				webrequest.Enqueue($"http://steamcommunity.com/profiles/{userId}?xml=1", null, (code, response) =>
				{
					if (code != 200 || response == null)
						return;

					var avatar = Regex.Match(response).Groups[1].ToString();
					if (string.IsNullOrEmpty(avatar))
						return;

					callback.Invoke(avatar);
				}, this);
			}
			catch (Exception e)
			{
				PrintError($"{e.Message}");
			}
		}

		#endregion

		#region Working with Images

		private void AddImage(string url, string fileName, ulong imageId = 0)
		{
#if CARBON
		imageDatabase?.Queue(true, new Dictionary<string, string>
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
		return imageDatabase?.GetImageString(name);
#else
			return Convert.ToString(ImageLibrary?.Call("GetImage", name));
#endif
		}

		private bool HasImage(string name)
		{
#if CARBON
		return Convert.ToBoolean(imageDatabase?.HasImage(name));
#else
			return Convert.ToBoolean(ImageLibrary?.Call("HasImage", name));
#endif
		}

		private void LoadImages()
		{
#if CARBON
			imageDatabase = BaseModule.GetModule<ImageDatabaseModule>();
#endif
		
#if !CARBON
			timer.In(1f, () =>
			{
				if (ImageLibrary is not {IsLoaded: true})
				{
					BroadcastILNotInstalled();
					return;
				}
			});
#endif
		}
		
		private void BroadcastILNotInstalled()
		{
			for (var i = 0; i < 5; i++) PrintError("IMAGE LIBRARY IS NOT INSTALLED.");
		}

		#endregion

		#endregion
	}
}
