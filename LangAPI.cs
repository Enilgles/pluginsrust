using System;
using System.Collections.Generic;
using System.Collections;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;
using WebSocketSharp;

namespace Oxide.Plugins
{
    [Info("Lang API", "Khan", "1.0.5")]
    [Description("Automatically translates rust item display names & descriptions to use in you're plugins")]
    public class LangAPI : CovalencePlugin
    {
        private string _langTypes = "af,ar,ca,cs,da,de,el,en-PT,es-ES,fi,fr,he,hu,it,ja,ko,nl,no,pl,pt-PT,pt-BR,ro,ru,sr,sv-SE,tr,uk,vi,zh-CN,zh-TW,en";
        private bool _working;
        private Coroutine _coroutine;
        private bool _isReady;

        private readonly Dictionary<string, string> _corrections = new Dictionary<string, string>
        {
            {"sunglasses02black", "Sunglasses Style 2"},
            {"sunglasses02camo", "Sunglasses Camo"},
            {"sunglasses02red", "Sunglasses Red"},
            {"sunglasses03black", "Sunglasses Style 3"},
            {"sunglasses03chrome", "Sunglasses Chrome"},
            {"sunglasses03gold", "Sunglasses Gold"},
            {"twitchsunglasses", "Twitch Sunglasses"},
        };

        private readonly HashSet<string> _defaults = new HashSet<string>();

        private void OnServerInitialized()
        {
            ItemManager.Initialize();

            _coroutine = ServerMgr.Instance.StartCoroutine(DoLangRoutine());
        }

        private void Unload()
        {
            if (_coroutine != null)
                ServerMgr.Instance.StopCoroutine(_coroutine);
            _defaults.Clear();
            _corrections.Clear();
        }

        private IEnumerator DoLangRoutine()
        {
            foreach (var type in _langTypes.Split(','))
            {
                _working = true;

                ProcessLang(type);

                yield return new WaitUntil(() => !_working);
            }

            _coroutine = null;
            _isReady = true;

            Interface.CallHook("OnLangAPIFinished");

            PrintWarning("has finished processing and is now ready.");

            yield return null;
        }

        private void ProcessLang(string type)
        {
            webrequest.Enqueue($"https://raw.githubusercontent.com/Khan8615/RustLanguages/main/{type}.json", "",
                (code, response) => ProcessCallback(code, response, type),
                this);
        }

        private void ProcessCallback(int code, string response, string type)
        {
            if (code != 200 || response.IsNullOrEmpty())
            {
                _working = false;

                PrintWarning($"Failed to download {type} translation.");
                return;
            }

            Dictionary<string, string> langItems = new Dictionary<string, string>();
            Dictionary<string, string> tempItems = DeserializeLang(response);

            foreach (var itemDefinition in ItemManager.itemList)
            {
                langItems.Add(itemDefinition.shortname, CorrectName(itemDefinition.shortname, tempItems.ContainsKey(itemDefinition.displayName.token)
                        ? tempItems[itemDefinition.displayName.token]
                        : itemDefinition.displayName.english));

                langItems.Add($"{itemDefinition.shortname}.desc",
                    tempItems.ContainsKey($"{itemDefinition.displayName.token}.desc")
                        ? tempItems[$"{itemDefinition.displayName.token}.desc"]
                        : itemDefinition.displayDescription.english);

                _defaults.Add(itemDefinition.displayName.english);
            }

            _working = false;

            lang.RegisterMessages(langItems, this, type);
        }

        private Dictionary<string, string> DeserializeLang(string content)
        {
            Dictionary<string, string> original = JsonConvert.DeserializeObject<Dictionary<string, string>>(content);
            Dictionary<string, string> messages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {"burlap.gloves", "Leather Gloves"},
                {"burlap.gloves.desc", "Gloves made out leather, offers a small amount of protection to the upper body."}
            };

            foreach (var item in original)
            {
                if (!messages.ContainsKey(item.Key))
                    messages.Add(item.Key, item.Value);
            }

            return messages;
        }

        private string CorrectName(string shortname, string original)
        {
            if (!_corrections.ContainsKey(shortname)) return original;

            return _corrections[shortname];
        }

        string GetItemDisplayName(string key, string def, string userID = null)
        {
            string message = lang.GetMessage(key, this, userID);
            return message == key ? def : message;
        }

        string GetItemDescription(string key, string def, string userID = null)
        {
            key = $"{key}.desc";
            string message = lang.GetMessage(key, this, userID);
            return message == key ? def : message;
        }

        bool IsDefaultDisplayName(string key) => _defaults.Contains(key);

        bool IsReady() => _isReady;
    }
}