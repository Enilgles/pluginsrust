using ConVar;
using Facepunch;
using Facepunch.Utility;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Pool = Facepunch.Pool;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Heavy Rig", "NooBlet", "1.5.0")]
    [Description("Spawns a Heavy Oilrig Event")]
    public class HeavyRig : RustPlugin
    {
        #region Vars

        [PluginReference]
        private readonly Plugin ZoneManager;

        private const string SHADED_SPHERE = "assets/prefabs/visualization/sphere.prefab";
        private const string BR_SPHERE_RED = "assets/bundled/prefabs/modding/events/twitch/br_sphere_red.prefab";
        private const string BR_SPHERE_BLUE = "assets/bundled/prefabs/modding/events/twitch/br_sphere.prefab";
        private const string BR_SPHERE_GREEN = "assets/bundled/prefabs/modding/events/twitch/br_sphere_green.prefab";
        private const string BR_SPHERE_PURPLE = "assets/bundled/prefabs/modding/events/twitch/br_sphere_purple.prefab";
        private const string LightroterPrefab = "assets/prefabs/deployable/playerioents/lights/flasherlight/electric.flasherlight.deployed.prefab";
        readonly string crate = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab";
        readonly string tugboatPrefab = "assets/content/vehicles/boats/tugboat/tugboat.prefab";       
        public HeavyRig _plugin;
        Timer CrateCheckTimer;
        public bool EventActive = false;
        public bool HardmodeEvent = false;
        public bool ExtremeModeEvent = false;
        public int Eventrigid = 0;
        public bool hackcycleactive = false;
        BaseEntity largeReader = null;
        BaseEntity smallReader = null;
        BradleyAPC EventBradley = null;
        public static ulong NormalCard = 3431997187;
        public static ulong HardCard = 3431996394;
        public static ulong ExtremeCard = 3433949427;
        private Configuration _config;
        public static string _cardName;
        public static string _HardcardName;
        public static string _ExtremecardName;
        public List<HackableLockedCrate> spawnedCrates = new List<HackableLockedCrate>();
        public List<MonumentInfo> Rigs = new List<MonumentInfo>();
        public Dictionary<Vector3, float> LargeCorrections = new Dictionary<Vector3, float>
        {
              { new Vector3(2.5f, 37f, 1f), 0f },
              { new Vector3(12f, 37f, 1f), -90f },
              { new Vector3(16f, 37f, 12f), -90f },
              { new Vector3(16f, 42f, 12f), -90f },
              { new Vector3(-10.25f, 39.5f, -2.13f), -180f},
              { new Vector3(-1.57f, 36.15f, 13.17f), -180f},
        };
        public Dictionary<Vector3, float> SmallCorrections = new Dictionary<Vector3, float>
        {
              { new Vector3(14f, 28f,5f), 180f },
              { new Vector3(18.8f, 27.2f,1.5f), -90f },
              { new Vector3(12.49f, 30.5f, -24.93f), 0f},
              { new Vector3(6.09f, 30.5f, 2.12f), 180f},
        };
        public Dictionary<Vector3, float> SmallReaderCorrection = new Dictionary<Vector3, float>
        {
              { new Vector3(24f, 28.7f, -10.78f), 0f },
        };
        public Dictionary<Vector3, float> LargeReaderCorrection = new Dictionary<Vector3, float>
        {
              { new Vector3(-14.5f, 39f-1.35f,5.85f), 180f },
        };

        #endregion Vars

        #region Hooks

        void OnServerInitialized(bool initial)
        {
            SetupCardNames();
            _plugin = this;
            SetupRigs();
            largeReader = SetReader(SpawnCratePos(LargeReaderCorrection.FirstOrDefault().Key, "Large Oil Rig", null).pos, SpawnCratePos(LargeReaderCorrection.FirstOrDefault().Key, "Large Oil Rig", null).rot);
            smallReader = SetReader(SpawnCratePos(SmallReaderCorrection.FirstOrDefault().Key, "Oil Rig", null).pos, SpawnCratePos(SmallReaderCorrection.FirstOrDefault().Key, "Oil Rig", null).rot);
           
        }
        void Unload()
        {
            largeReader.Kill();
            smallReader.Kill();
            KillButtons();
            int nCrateCount = spawnedCrates.Count;
            if (nCrateCount > 0)
            {
                for (int n = nCrateCount; n > 0; n--)
                {
                    var c = spawnedCrates[n - 1];
                    c?.Kill();
                }
            }
            foreach (var p in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(p, "HRConfigUI");              
            }
        }

        object CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            if (crate.OwnerID == 0304 || crate._name == "0304") { return false; }
            if (!EventActive||hackcycleactive) { return null; }
            else
            {
                foreach (var r in Rigs)
                {
                    if (Vector3.Distance(crate.transform.position, r.transform.position) < 100)
                    {
                        if (r.displayPhrase.english.Contains("Oil Rig"))
                        {
                            if (r.displayPhrase.english.StartsWith("Large"))
                            {
                                if (EventActive && GetRigID(player.transform.position) == Eventrigid) { StartHackcycle(spawnedCrates); MonitorCrate(crate, r); }
                                // Puts("Large Hacked");
                            }
                            else
                            {
                                if (EventActive && GetRigID(player.transform.position) == Eventrigid) { StartHackcycle(spawnedCrates); MonitorCrate(crate, r); }
                                // Puts("Small Hacked");
                            }
                            hackcycleactive = true;
                        }
                    }
                }
              
            }
            return null;
        }

        void HeavyOilRigWaveEventStarted(bool hardmode, bool extrememode,Vector3 monumentpos)
        {
            Events.Clear();
            Events = new List<string>
    {
        "Rad",
        "Patrol",
        "Heavy",
        "Mlrs",
        "Bradley",
    };

            string mode = extrememode ? "ExtremeMode" : hardmode ? "HardMode" : "NormalMode";
            Puts($"Heavy OilRig Wave Event Started - {mode}");

            timer.Once(1800f, () =>
            {
                EventBradley?.Kill();
                EventBradley = null;
                Interface.CallHook("HeavyOilRigWaveEventStopped");
                EventActive = false;
                Eventrigid = 0;
                CrateCheckTimer?.Destroy();
                HardmodeEvent = false;
                ExtremeModeEvent = false;
                if (_config.RemoveHeavys) { RemoveHeavys(monumentpos); }
            });
        }       

        void HeavyOilRigWaveEventStopped()
        {
            Puts("Heavy OilRig Wave Event Stopped");
            EventActive = false;
            Eventrigid = 0;
            CrateCheckTimer?.Destroy();
            HardmodeEvent = false;
            ExtremeModeEvent = false;
        }


        void OnButtonPress(PressButton button, BasePlayer player)
        {
            if (button._name == "9999")
            {
                var oilrig = getOilrigName(player);
                Eventrigid = GetRigID(player.transform.position);
                var card = GetPlayerCard(player);                
                if (card != null)
                {
                    var cardid = GetCardID(card.GetHeldEntity() as Keycard);
                    if (cardid == "hard") { HardmodeEvent = true; }
                    if (cardid == "extreme") { ExtremeModeEvent = true; }
                    if (EventActive) { player.ChatMessage(GetLang("EventActiveMessage", player)); return; }
                    if (!CrateReady(player.transform.position)) { player.ChatMessage(GetLang("CrateNotReady", player)); return; }
                    card.UseItem(1);
                    Activateevent(GetRig(player.transform.position), player,HardmodeEvent);
                    BroadcastEvent(player, GetRig(player.transform.position),HardmodeEvent,ExtremeModeEvent);
                    Interface.CallHook("HeavyOilRigWaveEventStarted",HardmodeEvent,ExtremeModeEvent,player.transform.position);

                    timer.Once(300f, () =>
                    {
                        int nCrateCount = spawnedCrates.Count;
                        if (nCrateCount > 0)
                        {
                            for (int n = nCrateCount; n > 0; n--)
                            {
                                var c = spawnedCrates[n - 1];
                                if (!c.IsBeingHacked() && !hackcycleactive)
                                {
                                    c.Kill();
                                }
                            }
                        }
                      
                        hackcycleactive = false;
                    });
                    if (!HardmodeEvent&&!ExtremeModeEvent)
                    {
                        timer.Once(900f, () =>
                        {
                            Interface.CallHook("HeavyOilRigWaveEventStopped");
                            EventActive = false;
                            Eventrigid = 0;
                        });
                    }                  
                   
                    return;
                }
                else
                {
                    player.ChatMessage("You dont seem to have a card!!!");
                }
            }
            return;
        }

        void OnCardSwipe(CardReader cardReader, Keycard card, BasePlayer player)
        {
            if ((card.skinID == NormalCard||card.skinID ==HardCard||card.skinID==ExtremeCard) && GetRig(player.transform.position).Contains("") && cardReader.accessLevel == 3)
            {
                var cardid = GetCardID(card);
                if (cardid == "hard") { HardmodeEvent = true; }
                if(cardid == "extreme") { ExtremeModeEvent = true; }
                if (EventActive) { player.ChatMessage(GetLang("EventActiveMessage", player)); return; }
                if (!CrateReady(player.transform.position)) { player.ChatMessage(GetLang("CrateNotReady", player)); return; }

                timer.Once(1f, () =>
                {
                    var c1 = GetPlayerCard(player);
                    c1.UseItem(1);
                });
                Eventrigid = GetRigID(player.transform.position);
                Activateevent(GetRig(player.transform.position), player,HardmodeEvent);
                BroadcastEvent(player, GetRig(player.transform.position),HardmodeEvent,ExtremeModeEvent);
                Interface.CallHook("HeavyOilRigWaveEventStarted", HardmodeEvent, ExtremeModeEvent,player.transform.position);

                timer.Once(300f, () =>
                {
                    int nCrateCount = spawnedCrates.Count;
                    if (nCrateCount > 0)
                    {
                        for (int n = nCrateCount; n > 0; n--)
                        {
                            var c = spawnedCrates[n - 1];
                            if (!c.IsBeingHacked() && !hackcycleactive)
                            {
                                c.Kill();
                            }
                        }
                    }                  
                    hackcycleactive = false;
                });
                if (!HardmodeEvent)
                {
                    timer.Once(900f, () =>
                    {
                        Interface.CallHook("HeavyOilRigWaveEventStopped");
                        EventActive = false;
                        Eventrigid = 0;
                    });
                }
            }
        }             

        private void OnLootSpawn(LootContainer container)
        {
            if (container == null || !_config.EnableSpawn) return;

            foreach (var item in Items)
            {
                if(item.SkinID == ExtremeCard) { continue; }
                if(item.SkinID == HardCard && !_config.HardMode) { continue; }
                if(item.SkinID == HardCard && _config.HardcardOnlyinNormal) { continue;}
                var customItem = _config.Drop.Find(x => x.ShortPrefabName.Contains(container.ShortPrefabName));
                if (customItem == null || !(Random.Range(0f, 100f) <= customItem.DropChance)) return;

                timer.In(0.21f, () =>
                {
                    if (container.inventory == null) return;

                    var count = Random.Range(customItem.MinAmount, customItem.MaxAmount + 1);

                    if (container.inventory.capacity <= container.inventory.itemList.Count)
                        container.inventory.capacity = container.inventory.itemList.Count + count;

                    for (var i = 0; i < count; i++)
                    {
                        var item1 = item?.ToItem();
                        if (item1 == null) break;

                        item1.MoveToContainer(container.inventory);
                    }
                });
            }
        }

        private void OnEntitySpawned(LootableCorpse corpse)
        {
            if (!_config.UseNocorpse) { return; }
            var pos = GetRig(corpse.transform.position);
            if(pos != "" && corpse._playerName =="Scientist")
            {
                NextFrame(() =>
                {
                    corpse.ResetRemovalTime(0.5f);
                });
            }
        }
        object OnEntityKill(HackableLockedCrate entity)
        {
            if (spawnedCrates.Contains(entity)) { spawnedCrates.Remove(entity); }
            return null;
        }

        #endregion Hooks

        #region Methods

        private void RemoveHeavys(Vector3 pos)
        {
            var Players = Pool.Get<List<BasePlayer>>();
            Vis.Entities(pos, 150, Players);

            foreach (var b in Players)
            {
                if (b is ScientistNPC && b != null)
                {
                    b?.Kill();
                }
            }
            Pool.FreeUnmanaged(ref Players);
        }
        private void AddExtraLootLight(HackableLockedCrate crate)
        {            
            FlasherLight ExtraLootLights =
                GameManager.server.CreateEntity(LightroterPrefab, crate.transform.position) as FlasherLight;
            if (ExtraLootLights == null) return;

            ExtraLootLights.Spawn();
            ExtraLootLights.SetFlag(BaseEntity.Flags.On, true);
            ExtraLootLights.SetParent(crate);
            ExtraLootLights.transform.localPosition = new Vector3(0.3f, 1.4f, 0f);
            ExtraLootLights.transform.localRotation = Quaternion.Euler(new Vector3(0, 180, 0));
            ExtraLootLights.UpdateHasPower(10, 1);
            RemoveColliderProtection(ExtraLootLights);
            ExtraLootLights.SendNetworkUpdateImmediate();
        }
        void RemoveColliderProtection(BaseEntity colliderEntity)
        {
            foreach (var meshCollider in colliderEntity.GetComponentsInChildren<MeshCollider>())
            {
                UnityEngine.Object.DestroyImmediate(meshCollider);
            }

            UnityEngine.Object.DestroyImmediate(colliderEntity.GetComponent<GroundWatch>());
        }

        private Item GetPlayerCard(BasePlayer player)
        {
            foreach (var b in player.inventory.containerBelt.itemList)
            {
                if (b.info.shortname == "keycard_red" && (b.skin == NormalCard || b.skin == HardCard || b.skin == ExtremeCard))
                {
                    return b;
                }
            }
            foreach (var m in player.inventory.containerMain.itemList)
            {
                if (m.info.shortname == "keycard_red" && (m.skin == NormalCard || m.skin == HardCard || m.skin == ExtremeCard))
                {
                    return m;
                }
            }
            return null;
        }
        private string GetCardID(Keycard card)
        {
            if (card.skinID == NormalCard) { return "soft"; }
            if (card.skinID == HardCard) { return "hard"; }
            if (card.skinID == ExtremeCard) { return "extreme"; }

            return "";
        }
        private void SetupCardNames()
        {
            timer.Once(0.5f, () =>
            {
                _cardName = _config.CardName;
                _HardcardName = _config.HardCardName;
                _ExtremecardName = _config.ExtremeCardName;
            });
        }
        private void SetupRigs()
        {
            TerrainMeta.Path.Monuments.ForEach(monument =>
            {
                if (monument == null) return;
                if (monument.displayPhrase.english.Contains("Oil Rig"))
                {
                    Rigs.Add(monument);
                }
            });
        }

        private void KillButtons()
        {
            var oilRigMonuments = TerrainMeta.Path.Monuments.Where(monument => monument.displayPhrase.english.Contains("Oil Rig"));
            foreach (var monument in oilRigMonuments)
            {
                if (monument == null) return;
                var buttons = Pool.Get<List<PressButton>>();
                Vis.Entities(monument.transform.position, 200f, buttons);

                foreach (var b in buttons)
                {
                    if (b._name == "9999")
                    {
                        Puts("killing button");
                        b.Kill();
                    }
                }
                Pool.FreeUnmanaged(ref buttons);
            }
        }
        private void FindCards()
        {
            List<Keycard> keycards = new List<Keycard>();
            foreach (var c in Keycard.serverEntities)
            {
                var card = c as Keycard;
                if (card != null && card.accessLevel == 3 && card.skinID == 1988408422)
                {
                    Puts($"card Found : {card.GetOwnerItemDefinition().displayName.english}");
                    keycards.Add(card);
                }
            }          
        }

        private List<BasePlayer> GetPlayers(MonumentInfo monument)
        {
            var players = Pool.Get<List<BasePlayer>>();
            Vis.Entities(monument.transform.position, 300, players);                      
            players.RemoveAll(player => player.IsNpc|| !player.userID.IsSteamId());

            timer.Once(5f, () =>
            {
                Pool.FreeUnmanaged(ref players); 
            });

            return players;
        }

        void SendToast(BasePlayer player, string message)
        {
            player.ShowToast(GameTip.Styles.Red_Normal, message);
            timer.Repeat(1f, 10, () =>
            {
                player.ShowToast(GameTip.Styles.Red_Normal, message);
            });              
        }
        private bool CrateReady(Vector3 position)
        {
            foreach (var c in HackableLockedCrate.serverEntities)
            {
                var crate = c as HackableLockedCrate;
                if (crate == null) continue;
                if (Vector3.Distance(position, crate.transform.position) <= 100)
                {
                    if (crate.IsBeingHacked() || crate.IsFullyHacked())
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void BroadcastEvent(BasePlayer player, string v,bool hard , bool extreme)
        {
            string monument = "";
            string Mode = "";
            if (!hard && !extreme) { Mode = "Normal Mode"; }
            if (hard && !extreme) { Mode = "Hard Mode"; }
            if (!hard && extreme) { Mode = "Extreme Mode"; }
            if (v == "large")
            {
                monument = "Large OilRig";
            }
            else
            {
                monument = "Small OilRig";
            }
            player.ChatMessage(GetLang("ActivateEventPlayerMessage", player));
            foreach (var p in BasePlayer.activePlayerList)
            {
                if (p == null) { continue; }
                timer.Repeat(1f, 4, () =>
                {
                    p.ShowToast(GameTip.Styles.Server_Event, $"{GetLang("ActivateEvent", p)} <color=green>{monument}</color> . (<color=yellow>{Mode}</color>)");
                });
            }
        }
        private void Activateevent(string rig, BasePlayer player, bool isHardmode)
        {
            if (rig == "large")
            {
                int spawnLimit = ExtremeModeEvent ? LargeCorrections.Count : 4;
                int randomIndexL = UnityEngine.Random.Range(0, spawnLimit);
                int currentIndexL = 0;
               
                foreach (var pos in LargeCorrections.Take(spawnLimit))
                {
                    var entity = GameManager.server.CreateEntity(crate, SpawnCratePos(pos.Key, "Large Oil Rig", player).pos, SpawnCratePos(pos.Key, "Large Oil Rig", player).rot);
                    var hack = entity?.GetComponent<HackableLockedCrate>();
                    hack._name = "0304";
                    float addedRotationY = pos.Value;
                    Quaternion addedRotation = Quaternion.Euler(0f, addedRotationY, 0f);
                    hack.transform.localRotation *= addedRotation;
                    hack.Spawn();
                  //  HackableLockedCrate.requiredHackSeconds = 0f;
                  //  hack.StartHacking();
                    spawnedCrates.Add(hack);
                 
                    if (currentIndexL == randomIndexL && _config.HardcardOnlyinNormal && !isHardmode&&!ExtremeModeEvent)
                    {
                        timer.Once(1.5f, () => HardModeCardLoot(hack));
                    }
                    if (!isHardmode&&!ExtremeModeEvent) { currentIndexL++; continue; }
                    if (_config.UseDoubleLoot && _config.AllCrateDouble)
                    {
                        timer.Once(1.5f, () => SpawnAdditionalLoot(hack));
                        continue;
                    }
                    if (currentIndexL == randomIndexL)
                    {
                        if (_config.UseDoubleLoot) { timer.Once(1.5f, () => SpawnAdditionalLoot(hack)); }
                        if (isHardmode) { timer.Once(1.7f, () => ExtremeModeCardLoot(hack)); }
                    }

                    currentIndexL++;
                }
              //  timer.Once(3f, () => HackableLockedCrate.requiredHackSeconds = 900f);
                EventActive = true;
            }
            else if (rig == "small")
            {
                int spawnLimit = ExtremeModeEvent ? SmallCorrections.Count : 2;
                int randomIndexS = UnityEngine.Random.Range(0, spawnLimit);
                int currentIndexS = 0;
               
                foreach (var pos in SmallCorrections.Take(spawnLimit))
                {
                    var entity = GameManager.server.CreateEntity(crate, SpawnCratePos(pos.Key, "Oil Rig", player).pos, SpawnCratePos(pos.Key, "Oil Rig", player).rot);
                    var hack = entity?.GetComponent<HackableLockedCrate>();
                    hack._name = "0304";
                    float addedRotationY = pos.Value;
                    Quaternion addedRotation = Quaternion.Euler(0f, addedRotationY, 0f);
                    hack.transform.localRotation *= addedRotation;
                    hack.Spawn();
                  //  HackableLockedCrate.requiredHackSeconds = 0f;
                   // hack.StartHacking();
                    spawnedCrates.Add(hack);                 
                  
                    if (currentIndexS == randomIndexS && _config.HardcardOnlyinNormal && !isHardmode&&!ExtremeModeEvent)
                    {
                        timer.Once(1.5f, () => HardModeCardLoot(hack));
                    }
                    if (!isHardmode&&!ExtremeModeEvent) { currentIndexS++; continue; }
                    if (_config.OnlyLarge&&!ExtremeModeEvent&&!isHardmode) { continue; }
                   
                    if (_config.UseDoubleLoot && _config.AllCrateDouble)
                    {
                        timer.Once(1.5f, () => SpawnAdditionalLoot(hack));
                        continue;
                    }
                    if (currentIndexS == randomIndexS)
                    {
                        if (_config.UseDoubleLoot && (!_config.OnlyLarge||ExtremeModeEvent)) { timer.Once(1.5f, () => SpawnAdditionalLoot(hack)); }
                        if (isHardmode) { timer.Once(1.7f, () => ExtremeModeCardLoot(hack)); }                            
                    }
                  
                    currentIndexS++;
                }
               // timer.Once(3f, () => HackableLockedCrate.requiredHackSeconds = 900f);
                EventActive = true;
            }
        }

        private void ExtremeModeCardLoot(HackableLockedCrate crate)
        {
            crate.inventory.capacity = 32;           
            var item = Items[2]?.ToItem();
            if (item != null)
            {               
                crate.inventory.GiveItem(item);
            }           
        }

        private void HardModeCardLoot(HackableLockedCrate crate)
        {
            crate.inventory.capacity = 32;
            var item = Items[1]?.ToItem();
            if (item != null)
            {
                crate.inventory.GiveItem(item);
            }
        }

        void SpawnAdditionalLoot(HackableLockedCrate crate)
        {
            if (crate == null || crate.inventory == null) return;
            crate.inventory.capacity = 32;
            AddExtraLootLight(crate);
            foreach(var item in _config.ExtraLoot)
            {
                if(item == null) continue;               
                int randomAmount = Random.Range(item.MinAmount, item.MaxAmount + 1);
                Item newItem = ItemManager.CreateByPartialName(item.ShortPrefabName, randomAmount, item.SkinID);
                if (newItem != null)
                {
                    crate.inventory.GiveItem(newItem);
                }
            }
        }

        public int GetRigID(Vector3 pos)
        {
            int rig = 0;
            foreach (var r in Rigs)
            {
                if (Vector3.Distance(pos, r.transform.position) < 150)
                {
                    if (r.displayPhrase.english.Contains("Oil Rig"))
                    {
                        rig = r.GetInstanceID();
                    }
                }
            }
            return rig;
        }

        public string GetRig(Vector3 pos)
        {
            var rig = "";
            foreach (var r in Rigs)
            {
                if (Vector3.Distance(pos, r.transform.position) < 150)
                {
                    if (r.displayPhrase.english.Contains("Oil Rig"))
                    {
                        if (r.displayPhrase.english.StartsWith("Large"))
                        {
                            rig = "large";
                        }
                        else
                        {
                            rig = "small";
                        }
                    }
                }
            }          
            return rig;
        }

        private String getOilrigName(BasePlayer player)
        {
            var rig = "";
            foreach (var r in Rigs)
            {
                if (Vector3.Distance(player.transform.position, r.transform.position) < 100)
                {
                    if (r.displayPhrase.english.Contains("Oil Rig"))
                    {
                        if (r.displayPhrase.english.StartsWith("Large"))
                        {
                            rig = "large";
                        }
                        else
                        {
                            rig = "small";
                        }
                    }
                }
            }
            return rig;
        }
        public GetVecs SpawnCratePos(Vector3 correction, string rig, BasePlayer player)
        {
            Vector3 pos = new Vector3(0, 0, 0);
            Quaternion rot = new Quaternion(0, 0, 0, 0);

            foreach (var r in Rigs)
            {
                bool isPlayerCloseOrWithinBounds = false;
                if (player != null)
                {
                    isPlayerCloseOrWithinBounds =
                        Vector3.Distance(player.transform.position,r.transform.position) <= 100f ||
                        r.Bounds.Contains(player.transform.position);
                }

                if (player == null || isPlayerCloseOrWithinBounds)
                {
                    if (r.displayPhrase.english == rig)
                    {
                        var correct = correction;
                        if (correct == Vector3.zero) continue;

                        var transform = r.transform;
                        rot = transform.rotation;
                        pos = transform.position + rot * correct;
                        break;
                    }
                }
            }
          
            return new GetVecs { pos = pos, rot = rot };
        }

        [ChatCommand("getc")]
        private void GetCorrectionCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null)
                return;

            Vector3 playerPos = player.transform.position;
            Vector3 nearestRigPos = Vector3.zero;
            Quaternion nearestRigRot = Quaternion.identity;
            float minDistance = float.MaxValue;

            foreach (var r in Rigs)
            {
                float distance = Vector3.Distance(playerPos, r.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestRigPos = r.transform.position;
                    nearestRigRot = r.transform.rotation;
                }
            }

            if (minDistance == float.MaxValue)
            {
                player.ChatMessage("No nearby rigs found.");
                return;
            }

            Vector3 correction = Quaternion.Inverse(nearestRigRot) * (playerPos - nearestRigPos);
            Puts($"Correction vector: ({correction.x:F2}f, {correction.y:F2}f, {correction.z:F2}f)");
        }


        public void StartHackcycle(List<HackableLockedCrate> list)
        {
            int currentIndex = 0;

            void HackNextCrate()
            {
                if (currentIndex < list.Count)
                {
                    var crate = list[currentIndex];
                    if (crate != null) { crate.StartHacking(); }
                    currentIndex++;
                }
                else
                {
                    list.Clear();
                    Interface.CallHook("HeavyOilRigWaveEventStopped");
                    EventActive = false;
                    HardmodeEvent = false;  
                    ExtremeModeEvent = false;
                    spawnedCrates.Clear();
                    Eventrigid = 0;
                }
            }

            timer.Repeat(30f, list.Count, HackNextCrate);
        }

        BaseEntity SetReader(Vector3 pos, Quaternion rot)
        {
            PressButton changed = null;
            var reader = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/button/button.prefab", pos, rot);
            if (reader == null) { Puts("reader null"); return null; }
            reader.gameObject.SetActive(true);
            if (reader is PressButton)
                changed = reader as PressButton;
            if (changed != null)
            {
                //changed.OwnerID = 0304;
                changed._name = "9999";
            }
            float addedRotationY = 0;
            if (GetRig(reader.transform.position) == "large")
            {
                addedRotationY = LargeReaderCorrection.FirstOrDefault().Value;
            }
            else
            {
                addedRotationY = SmallReaderCorrection.FirstOrDefault().Value;
            }

            Quaternion addedRotation = Quaternion.Euler(0f, addedRotationY, 0f);
            reader.transform.localRotation *= addedRotation;

            reader.Spawn();
            SpawnRefresh(reader);
            changed._name = "9999";
            reader.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            reader.SendNetworkUpdateImmediate();
            return reader;
        }

        void SpawnRefresh(BaseNetworkable entity1)
        {
            UnityEngine.Object.Destroy(entity1.GetComponent<Collider>());
        }

        #endregion Methods

        #region Config

        private class Configuration
        {
            [JsonProperty(PropertyName = "Enable HardMode mode?")]
            public bool HardMode = true;

            [JsonProperty(PropertyName = "Enable Card spawn?")]
            public bool EnableSpawn = true;

            [JsonProperty(PropertyName = "Drop Settings", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<DropInfo> Drop = new List<DropInfo>
            {
                new DropInfo
                {
                    ShortPrefabName = "crate_elite",
                    MinAmount = 1,
                    MaxAmount = 1,
                    DropChance = 10
                },
                 new DropInfo
                {
                    ShortPrefabName = "codelockedhackablecrate",
                    MinAmount = 1,
                    MaxAmount = 1,
                    DropChance = 10
                },
            };

            [JsonProperty(PropertyName = "Card Name")]
            public string CardName = "Normal Wave Card";

            [JsonProperty(PropertyName = "Hard Card Name")]
            public string HardCardName = "Hard Wave Card";

            [JsonProperty(PropertyName = "Extreme Card Name")]
            public string ExtremeCardName = "Extreme Wave Card";

            [JsonProperty(PropertyName = "Remove Npc Corpses?")]
            public bool UseNocorpse = true;

            [JsonProperty(PropertyName = "Remove Heavy Scientists after Oilrig reset?")]
            public bool RemoveHeavys = true;

            [JsonProperty(PropertyName = "Use Extra Loot System?")]
            public bool UseDoubleLoot = true;

            [JsonProperty(PropertyName = "Spawn HardCard In Normal Mode only?")]
            public bool HardcardOnlyinNormal = true;

            [JsonProperty(PropertyName = "Use Extra Loot only in HardMode?")]
            public bool DoubleLootHardOnly = true;

            [JsonProperty(PropertyName = "Set all crate to Extra Loot? (False will pick one)")]
            public bool AllCrateDouble = false;

            [JsonProperty(PropertyName = "Only use Extra Loot On Large OilRig?")]
            public bool OnlyLarge = true;

            [JsonProperty(PropertyName = "Extra Loot Table", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<LootItem> ExtraLoot = new List<LootItem>
            {
    new LootItem { ShortPrefabName = "explosive.timed", MinAmount = 5, MaxAmount = 10, SkinID = 0 }, // C4
    new LootItem { ShortPrefabName = "ammo.rocket.basic", MinAmount = 3, MaxAmount = 6, SkinID = 0 },  // Rockets
    new LootItem { ShortPrefabName = "wall.frame.garagedoor", MinAmount = 1, MaxAmount = 2, SkinID = 0 },  // Garage Door
    new LootItem { ShortPrefabName = "m249", MinAmount = 1, MaxAmount = 1, SkinID = 0 }, // M249
    new LootItem { ShortPrefabName = "l96", MinAmount = 1, MaxAmount = 1, SkinID = 0 },  // L96
    new LootItem { ShortPrefabName = "scrap", MinAmount = 1000, MaxAmount = 5000, SkinID = 0 }  // Scrap
            };
           
            public class DropInfo
            {
                [JsonProperty(PropertyName = "Object Short prefab name")]
                public string ShortPrefabName;

                [JsonProperty(PropertyName = "Minimum item to drop")]
                public int MinAmount;

                [JsonProperty(PropertyName = "Maximum item to drop")]
                public int MaxAmount;

                [JsonProperty(PropertyName = "Item Drop Chance")]
                public float DropChance;
            }
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
            Config.WriteObject(_config,true);
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
            PrintWarning("Default Configuration File Created");
        }

        #endregion

        #region Lang

        private string GetLang(string key, BasePlayer player)
        {
            return lang.GetMessage(key, this)
                .Replace("{playername}", player.displayName);
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CrateNotReady"] = "Main Crate not Active or Hacked",
                ["ActivateEvent"] = "A Player Activated the Wave Event at :",
                ["ActivateEventPlayerMessage"] = "You have 5min's to hack main crate , or event will fail",
                ["EventActiveMessage"] = "Event already running",
                ["Rad"] = "WARNING !!!! Incoming Radiation!!",
                ["Heavy"] = "WARNING !!!! Incoming Heavy Scientists!!",
                ["Patrol"] = "WARNING !!!! Incoming Patrol Helicopter!!",
                ["Mlrs"] = "WARNING !!!! Incoming MLRS Rockets!!",
                ["Bradley"] = "WARNING !!!! Incoming Bradley spawn on PAD!!",

            }, this, "en");
        }

        #endregion Lang       

        #region Extra
        public List<string> Events = new List<string>
        {
            "Rad",
            "Patrol",
            "Heavy",  
            "Mlrs",
            "Bradley",
        };
        
        private BradleyAPC SpawnBradley(Vector3 pos)
        {           
            var bradley = GameManager.server.CreateEntity("assets/prefabs/npc/m2bradley/bradleyapc.prefab",pos) as BradleyAPC;
            bradley.Spawn();
            if (HardmodeEvent) { bradley.maxCratesToSpawn = 2; }
            if (ExtremeModeEvent) { bradley.maxCratesToSpawn = 5; }
            EventBradley = bradley;          
            bradley.ClearPath();
            Vector3[] points = CirclePointsGenerator.GetCirclePoints(pos, 7.5f);

            foreach (Vector3 point in points)
            {
                bradley.currentPath.Add(point);
            }


            return bradley;
        }
     
        public class CirclePointsGenerator
        {
            public static Vector3[] GetCirclePoints(Vector3 center, float radius, int count = 10)
            {
                Vector3[] points = new Vector3[count];
                float angleStep = 360f / count;

                for (int i = 0; i < count; i++)
                {
                    float angle = angleStep * i * Mathf.Deg2Rad;
                    points[i] = new Vector3(
                        center.x + Mathf.Cos(angle) * radius,
                        center.y, // Keep Y the same
                        center.z + Mathf.Sin(angle) * radius
                    );
                }

                return points;
            }
        }
        private void StartBradleyAttack(MonumentInfo monument)
        {
            Vector3 originalPos = monument.transform.position;           
            if (monument.displayPhrase.english.StartsWith("Large"))
            {
                originalPos.x += 1.89f;
                originalPos.z += -28f;
                originalPos.y += 45.23f;
            }
            else
            {
                originalPos.x += 28.61f;
                originalPos.z += -27.5f;
                originalPos.y += 31.64f;               
            }
            timer.Once(8f, () =>
            {
               SpawnBradley(originalPos);
            });
           
        }
        private static void SpawnRocket(BaseEntity entity, Vector3 rocketTargetPosition)
        {
            var rocketEntity = GameManager.server.CreateEntity("assets/prefabs/ammo/rocket/rocket_basic.prefab", entity.transform.position, new Quaternion());
            if (rocketEntity != null)
            {
                var proj = rocketEntity.GetComponent<ServerProjectile>();
                if (proj == null) return;
                proj.InitializeVelocity((rocketTargetPosition - entity.transform.position).normalized * 150f);
                rocketEntity.Spawn();
                rocketEntity._name = "radexplo";               
            }
        }

        private void StartMLRSAttack(MonumentInfo monument)
        {
            Vector3 directionToZero = (Vector3.zero - monument.transform.position).normalized;
            Vector3 targetPosition = monument.transform.position + directionToZero * 700f;
            targetPosition.y = 60f;
           

            timer.Repeat(0.3f, 30, () =>
            {
                var MlrsEntity = GameManager.server.CreateEntity("assets/content/vehicles/mlrs/mlrs.entity.prefab", targetPosition, new Quaternion()) as MLRS;
                var Rockets = ItemManager.CreateByItemID(-1843426638, 24);
                MlrsEntity.Spawn();
                var container = MlrsEntity.GetRocketContainer();
                Rockets.MoveToContainer(container.inventory);
                Vector3 originalPos = monument.transform.position;
                Vector3 randomOffset = new Vector3();
                if (monument.displayPhrase.english.StartsWith("Large"))
                {
                    originalPos.x += 2f;
                    originalPos.z += 0f;
                    originalPos.y += 30f;
                    randomOffset = new Vector3(
                    UnityEngine.Random.Range(-28f, 28f),
                    0f,
                    UnityEngine.Random.Range(-28f, 28f)
                );
                }
                else
                {
                    originalPos.x += 15f;
                    originalPos.z += -19f;
                    originalPos.y += 26f;
                    randomOffset = new Vector3(
                    UnityEngine.Random.Range(-15f, 15f),
                    0f,
                    UnityEngine.Random.Range(-15f, 15f)
                );
                }
                
              
                MlrsEntity.trueTargetHitPos = originalPos - randomOffset;

                timer.Once(0.17f, () =>
                {
                    MlrsEntity.FireNextRocket();
                });
                timer.Once(0.28f, () =>
                {
                    MlrsEntity.Kill();
                });
            });
            //Color color = Color.Lerp(Color.blue, Color.red, 0.5f);
            //float time = 28f;
            //string x = $"<size=38>X</size>";
            //var player = GetPlayers(monument).FirstOrDefault();
            //Puts(player.displayName);
            //player.SendConsoleCommand("ddraw.text", time, color, targetPosition, x);
        }

        private void StartHeavyAttack(MonumentInfo monument)
        {
            MonumentInfo nearestMonument = monument;
            Vector3 correction = new Vector3(0, 0, 0);           
            if (nearestMonument == null)
            {
                return;
            }
            if (nearestMonument.displayPhrase.english.StartsWith("Large"))
            {
                correction = new Vector3(7.30f, 36.15f, -12.68f);              
            }
            else
            {
                correction = new Vector3(2.33f, 30.57f, -8.26f);               
            }
            if (correction != Vector3.zero)
            {
                SpawnHeavyScientists(nearestMonument, correction);
            }
        }

        private void DoRadiation(MonumentInfo monument)
        {
            var pos = monument.transform.position;
            pos.y = monument.transform.position.y + 20;
           
            var radius = 50;
            var tier1 = "80";
            var tier2 = Math.Round(80 * 0.3333333333333333).ToString();
            var tier3 = Math.Round(80 * 0.1666666666666667).ToString();
            if (!monument.displayPhrase.english.StartsWith("Large"))
            {
                pos = monument.transform.position;
                pos.x += -20.79f;
                pos.z += -6.23f;
                pos.y = monument.transform.position.y + 20;
                radius = 40;
                tier1 = "65";
                tier2 = Math.Round(65 * 0.3333333333333333).ToString();
                tier3 = Math.Round(65 * 0.1666666666666667).ToString();
            }
            CreateZone(pos, "mainzone", "main", (radius - 25).ToString(), tier1);
            CreateZone(pos, "2zone", "2zone", (radius - 15).ToString(), tier2);
            CreateZone(pos, "4zone", "4zone", radius.ToString(), tier3);
            var sp = CreateSphere(pos, radius, 1);
            timer.Once(20f, () =>
            {
                RemoveZones("2zone");
                RemoveZones("mainzone");
                RemoveZones("4zone");
                DestroySpheres(sp);                
            });         
        }
     
        private void StartRadAttack(MonumentInfo monument)
        {
            timer.Once(8f, () =>
            {
                var pos = monument.transform.position;
                if (!monument.displayPhrase.english.StartsWith("Large"))
                {
                    pos = monument.transform.position;
                    pos.x += -20.79f;
                    pos.z += -6.23f;  
                }
                var spawnPosition = monument.transform.position + monument.transform.forward * 700;
                spawnPosition.y = pos.y + 200;
                var entity = GameManager.server.CreateEntity("assets/scripts/entity/misc/f15/f15e.prefab", spawnPosition, new Quaternion());

                entity.transform.LookAt(pos);
                entity.Spawn();
                float rocketLaunchDelay = 0;
                if (monument.displayPhrase.english.StartsWith("Large"))
                {
                    rocketLaunchDelay = 4f;
                }
                else
                {
                    rocketLaunchDelay = 4.2f;
                }

                timer.Once(12f, () =>
                {
                    if (entity != null)
                    {
                        entity.Kill();
                    }
                });
                timer.Once(5f, () =>
                {
                    DoRadiation(monument);
                });

                timer.Once(rocketLaunchDelay, () =>
                {
                    if (entity == null || entity.transform == null)
                        return;

                    timer.Repeat(0.2f, 3, () =>
                    {
                        if (entity == null || entity.transform == null)
                            return;

                        SpawnRocket(entity, pos);
                    });
                });
            });           
        }
      
        private void StartPatrolAttack(MonumentInfo monument)
        {
            Vector3 pos = monument.transform.position;
            pos.y = 70;
            Vector3 rocketpos = monument.transform.position;
            rocketpos.y = 40;
            if (!monument.displayPhrase.english.StartsWith("Large"))
            {
                pos = monument.transform.position;
                pos.x += -20.79f;
                pos.z += -6.23f;
                pos.y = 55f; 
                rocketpos = monument.transform.position;
                rocketpos.x += -20.79f;
                rocketpos.z += -6.23f;
                rocketpos.y = 40;
            }
            Vector3 direction = (pos - Vector3.zero).normalized;
            float distanceToPos = Vector3.Distance(Vector3.zero, pos);
            float extraDistance = 1000f;
            Vector3 spawnpos = Vector3.zero + direction * (distanceToPos + extraDistance);
            Quaternion rotation = Quaternion.LookRotation(pos - spawnpos);
            var patrolheli = GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab", spawnpos) as PatrolHelicopter;
            var ai = patrolheli?.GetComponent<PatrolHelicopterAI>();
            ai?.SetInitialDestination(pos);   
            patrolheli.Spawn();           
            patrolheli.transform.position = spawnpos;
            ai.SetIdealRotation(rotation);
           
            timer.Once(25f, () =>
            {
                ai.AtDestination();
                timer.Repeat(0.1f, 50, () =>
                {
                    ai.FireRocket(monument.transform.position);
                });
            });
            timer.Once(28f, () =>
            {
            });
        }

        private void MonitorCrate(HackableLockedCrate crate,MonumentInfo monument)
        {
            var crateState = new CrateState();
            crateState.Monument = monument;
            if ((HardmodeEvent||ExtremeModeEvent) && (CrateCheckTimer ==null || CrateCheckTimer.Destroyed)) { CrateCheckTimer = timer.Repeat(10f, 0, () => CheckCrateStatus(crate, crateState)); }
        }

        private void CheckCrateStatus(HackableLockedCrate crate, CrateState crateState)
        {
            if ((crate == null || crate.IsDestroyed)&&CrateCheckTimer!=null)
            {
                PrintWarning("Crate is null or destroyed. Stopping timer.");
                CrateCheckTimer?.Destroy();
                return;
            }

            float remainingTime = HackableLockedCrate.requiredHackSeconds - crate.hackSeconds;
            float halfTime = HackableLockedCrate.requiredHackSeconds * 0.5f;
            float thirtySevenPointFivePercentTime = HackableLockedCrate.requiredHackSeconds * 0.375f;
            float quarterTime = HackableLockedCrate.requiredHackSeconds * 0.25f;
            float seventeenPointFivePercentTime = HackableLockedCrate.requiredHackSeconds * 0.175f;
            float tenthTime = HackableLockedCrate.requiredHackSeconds * 0.1f;

            if (remainingTime <= halfTime && !crateState.Triggered50Percent)
            {
                Puts($"50% of the crate timer remaining.");
                crateState.Triggered50Percent = true;
                DoStuff(crateState);
            }
            else if (remainingTime <= thirtySevenPointFivePercentTime && !crateState.Triggered32Percent && ExtremeModeEvent)
            {
                Puts($"37.5% of the crate timer remaining.");
                crateState.Triggered32Percent = true;
                DoStuff(crateState);
            }
            else if (remainingTime <= quarterTime && !crateState.Triggered25Percent)
            {
                Puts($"25% of the crate timer remaining.");
                crateState.Triggered25Percent = true;
                DoStuff(crateState);
            }
            else if (remainingTime <= seventeenPointFivePercentTime && !crateState.Triggered17Percent && ExtremeModeEvent)
            {
                Puts($"17.5% of the crate timer remaining.");
                crateState.Triggered17Percent = true;
                DoStuff(crateState);
            }
            else if (remainingTime <= tenthTime && !crateState.Triggered10Percent)
            {
                Puts($"10% of the crate timer remaining.");
                crateState.Triggered10Percent = true;
                DoStuff(crateState);
                timer.Once(90f, () =>
                {
                    Interface.CallHook("HeavyOilRigWaveEventStopped");
                    EventActive = false;
                    Eventrigid = 0;
                    CrateCheckTimer?.Destroy();
                    HardmodeEvent = false;
                    ExtremeModeEvent = false;
                });               
            }
        }
        private void DoStuff(CrateState crateState)
        {
            var e = Events.GetRandom();
            Puts($"Doing : {e}");
            if (e == "Rad") { Events.Remove(e); SendNotification(crateState.Monument, e); StartRadAttack(crateState.Monument); }
            else if (e == "Patrol") { Events.Remove(e); SendNotification(crateState.Monument, e); StartPatrolAttack(crateState.Monument); }
            else if (e == "Heavy") { Events.Remove(e); SendNotification(crateState.Monument, e); StartHeavyAttack(crateState.Monument); }
            else if (e == "Mlrs") { Events.Remove(e); SendNotification(crateState.Monument, e); StartMLRSAttack(crateState.Monument); }
            else if (e == "Bradley") { Events.Remove(e); SendNotification(crateState.Monument, e); StartBradleyAttack(crateState.Monument); }
        }
            

       private void SendNotification(MonumentInfo monument,string e)
        {
            var players = GetPlayers(monument);
            
            foreach(var player in players)
            {
                if(player != null)
                {
                    SendToast(player, GetLang(e,player));                   
                    Effect.server.Run("assets/content/nexus/ferry/effects/nexus-ferry-departure-horn.prefab", player.transform.position);
                }
            }
        }

        private class CrateState
        {
            public bool Triggered50Percent { get; set; } = false;
            public bool Triggered32Percent { get; set; } = false;
            public bool Triggered25Percent { get; set; } = false;
            public bool Triggered17Percent { get; set; } = false;
            public bool Triggered10Percent { get; set; } = false;
            public MonumentInfo Monument { get; set; }
        }


        [ChatCommand("radattack")]
        private void radattack(BasePlayer player, string command, string[] args)
        {
            StartRadAttack(FindNearestMonument(player.transform.position));
            SendNotification(FindNearestMonument(player.transform.position), "Rad");
        }
        [ChatCommand("bradleyattack")]
        private void bradleyattack(BasePlayer player, string command, string[] args)
        {
            StartBradleyAttack(FindNearestMonument(player.transform.position));
            SendNotification(FindNearestMonument(player.transform.position), "Bradley");
        }

        [ChatCommand("mlrsattack")]
        private void mlrsattack(BasePlayer player, string command, string[] args)
        {
            StartMLRSAttack(FindNearestMonument(player.transform.position));
            SendNotification(FindNearestMonument(player.transform.position), "Mlrs");
        }

        [ChatCommand("patrolattack")]
        private void patrolattack(BasePlayer player, string command, string[] args)
        {
            SendNotification(FindNearestMonument(player.transform.position),"Patrol");
            StartPatrolAttack(FindNearestMonument(player.transform.position));
        }

        [ChatCommand("heavyattack")]
        private void heavyattack(BasePlayer player, string command, string[] args)
        {
            StartHeavyAttack(FindNearestMonument(player.transform.position));
            SendNotification(FindNearestMonument(player.transform.position), "Heavy");
        }
        [ChatCommand("cmonument")]
        private void CheckMonumentCommand(BasePlayer player, string command, string[] args)
        {
            MonumentInfo nearestMonument = FindNearestMonument(player.transform.position);
            Vector3 correction = new Vector3(0,0,0);           
            if (nearestMonument == null)
            {
                player.ChatMessage("No monument found nearby.");
                return;
            }

            Vector3 relativePosition = player.transform.position - nearestMonument.transform.position;
            Puts($"Nearest Monument: {nearestMonument.displayPhrase.english}"); 
            Puts($"Relative Position: {relativePosition.ToString()}");
                      
        }

        private MonumentInfo FindNearestMonument(Vector3 playerPosition)
        {
            MonumentInfo nearestMonument = null;
            float shortestDistance = float.MaxValue;

            foreach (var monument in Rigs)
            {
                float distance = Vector3.Distance(playerPosition, monument.transform.position);

                if (distance < shortestDistance)
                {
                    shortestDistance = distance;
                    nearestMonument = monument;
                }
            }

            return nearestMonument;
        }

       
        private void SpawnHeavyScientists(MonumentInfo monument, Vector3 correction/*,Vector3 movecorrection*/)
        {
            if (monument == null)
            {
                PrintWarning("Monument is null. Cannot spawn scientists.");
                return;
            }

            Vector3 centerPosition = monument.transform.position + correction;
            var entity = GameManager.server.CreateEntity(crate, centerPosition) as HackableLockedCrate;
            if(entity == null) { Puts("null crate"); }
            entity.Spawn();
            entity.StartHacking();
            timer.Once(0.5f, () =>
            {
                entity.Kill();
            });
           
        }

        #endregion

        #region Spheres
        private List<BaseEntity> CreateSphere(Vector3 position, float radius, int color)
        {
            string prefab = Getcolor(color);
            List<BaseEntity> Spheres = Pool.Get<List<BaseEntity>>();
            for (int i = 0; i < 5; i++)
            {
                BaseEntity sphere = GameManager.server.CreateEntity(prefab, position, new Quaternion(), true);
                SphereEntity ent = sphere.GetComponent<SphereEntity>();
                ent.currentRadius = radius * 2;
                ent.lerpSpeed = 0f;

                sphere.Spawn();
                Spheres.Add(sphere);
            }
            BaseEntity sphere1 = GameManager.server.CreateEntity(prefab, position, new Quaternion(), true);
            SphereEntity ent1 = sphere1.GetComponent<SphereEntity>();
            ent1.currentRadius = radius * 2;
            ent1.lerpSpeed = 0f;

            sphere1.Spawn();
            Spheres.Add(sphere1);
            return Spheres;
        }

        private string Getcolor(int color)
        {
            switch (color)
            {
                case 0:
                    return SHADED_SPHERE;

                case 1:
                    return BR_SPHERE_RED;

                case 2:
                    return BR_SPHERE_GREEN;

                case 3:
                    return BR_SPHERE_BLUE;

                case 4:
                    return BR_SPHERE_PURPLE;
            }
            return "";
        }

        private void DestroySpheres(List<BaseEntity> Spheres)
        {
            foreach (var sphere in Spheres)
                if (sphere != null)
                    sphere.KillMessage();
            Spheres.Clear();
            Pool.FreeUnmanaged(ref Spheres);
        }

        #endregion Spheres

        #region ZoneManager
        void CreateZone(Vector3 zonepos, string zonename, string zoneid, string radius, string radiation)
        {
            string[] messages = new string[8];
            messages[0] = "name";
            messages[1] = zonename;
            messages[2] = "id";
            messages[3] = zoneid;
            messages[4] = "radius";
            messages[5] = radius;
            messages[6] = "radiation";
            messages[7] = radiation;

            ZoneManager?.Call("CreateOrUpdateZone", zonename, messages, zonepos);
        }
        void RemoveZones(string ZoneID)
        {
            ZoneManager?.Call("EraseZone", ZoneID);

        }

        #endregion ZoneManager

        #region Commands


        [ChatCommand("hrconfig")]
        private void hrconfigcommand(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin) { CreateConfigMenu(player); }
        }
      
        [ConsoleCommand("givecard")]
        private void giveplayercardCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.GetString(0);
            var item = Items[0]?.ToItem();
            if (item == null) return;
            if(BasePlayer.FindAwakeOrSleeping(player) == null) { Puts("Player not found!"); return; }
            BasePlayer.FindAwakeOrSleeping(player).GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
        }
        [ConsoleCommand("givehardcard")]
        private void giveplayerhardcardCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.GetString(0);
            var item = Items[1]?.ToItem();
            if (item == null) return;
            if (BasePlayer.FindAwakeOrSleeping(player) == null) { Puts("Player not found!"); return; }
            BasePlayer.FindAwakeOrSleeping(player).GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
        }
        [ConsoleCommand("giveextremecard")]
        private void giveplayerextremecardCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.GetString(0);
            var item = Items[2]?.ToItem();
            if (item == null) return;
            if (BasePlayer.FindAwakeOrSleeping(player) == null) { Puts("Player not found!"); return; }
            BasePlayer.FindAwakeOrSleeping(player).GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
        }

        #endregion Commands

        #region Classes
        public class LootItem
        {
            [JsonProperty(PropertyName = "Short Prefab Name")]
            public string ShortPrefabName;

            [JsonProperty(PropertyName = "Minimum Amount")]
            public int MinAmount;

            [JsonProperty(PropertyName = "Maximum Amount")]
            public int MaxAmount;

            [JsonProperty(PropertyName = "Skin ID")]
            public ulong SkinID;
        }
        public class GetVecs
        {
            public Vector3 pos { get; set; }
            public Quaternion rot { get; set; }
        }

        public class ItemConfig
        {
            [JsonProperty(PropertyName = "DisplayName")]
            public string DisplayName;

            [JsonProperty(PropertyName = "Discription")]
            public string Discription;

            [JsonProperty(PropertyName = "ShortName")]
            public string ShortName;

            [JsonProperty(PropertyName = "SkinID")]
            public ulong SkinID;
            [JsonProperty(PropertyName = "Name")]
            public string Name;

            public Item ToItem()
            {
                var newItem = ItemManager.CreateByName(ShortName, 1, SkinID);
                if (newItem == null)
                {
                    Debug.LogError($"Error creating item with shortName '{ShortName}'!");
                    return null;
                }
               
                newItem.name = GetCardName(SkinID);
                newItem.info.displayDescription.english = Discription;
                newItem.MarkDirty();
                return newItem;
            }

            private string GetCardName(ulong skinID)
            {
                if(skinID == HardCard) { return _HardcardName; }
                else if(skinID == NormalCard) { return _cardName; }
                else if(skinID==ExtremeCard) { return _ExtremecardName; }
                return "Wave Card";
            }

            public bool IsSame(Item item)
            {
                return item != null && item.info.shortname == ShortName && item.skin == SkinID;
            }
        }       

        public List<ItemConfig> Items = new List<ItemConfig>
        {
           new ItemConfig
           {
                DisplayName = _cardName,
                Discription = "Access Card For OilRig Wave Event",
                ShortName = "keycard_red",
                SkinID = NormalCard,
               Name = "0304",
           },
           new ItemConfig
           {
                DisplayName = _HardcardName,
                Discription = "Access Card For OilRig Wave Event",
                ShortName = "keycard_red",
                SkinID = HardCard,
               Name = "0304",
           },
            new ItemConfig
           {
                DisplayName = _ExtremecardName,
                Discription = "Access Card For OilRig Wave Event",
                ShortName = "keycard_red",
                SkinID = ExtremeCard,
               Name = "0304",
           },
        };
        
        #endregion Classes

        #region Extra Tugboat

        Tugboat SpawnTugBoat(string prefab,Vector3 pos,Quaternion rot)
        {           
            var entity = GameManager.server.CreateEntity(prefab,pos,rot);
            Vector3 position = entity.transform.position;
            position.y = 0;

            var TB = entity?.GetComponent<Tugboat>();
            entity.transform.position = position;

            var tugboat = TB;
            entity.Spawn();
          
            EntityFuelSystem fuelsys = (EntityFuelSystem)TB.GetFuelSystem();
            var container = fuelsys?.fuelStorageInstance.Get(TB.isServer)?.GetComponent<StorageContainer>();
            if (container == null) { return null; }

            var item = ItemManager.CreateByItemID(-946369541, 100);
            if (item == null) { return null; }
            item.MoveToContainer(container.inventory);
            return tugboat;
        }
        public BasePlayer SpawnNPC(Vector3 position)
        {
            var npc = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", position) as BasePlayer;
            if (npc == null)
            {
                Puts("Failed to create NPC");
                return null;
            }

            npc.Spawn();
            npc.EnableSaving(false);
            npc.displayName = "Tug Captain";
            npc.inventory.containerMain.Clear();
            npc.inventory.containerBelt.Clear();
            npc.inventory.containerWear.Clear();

            return npc;
        }

        public void MountNPCToTugboat(Tugboat tugboat, BasePlayer npc)
        {
            var mountPoints = tugboat.mountPoints;
            if (mountPoints == null || mountPoints.Count == 0)
            {
                Puts("No mount points on Tugboat!");
                return;
            }
            var driverSeat = mountPoints[0];
            driverSeat.mountable.AttemptMount(npc);
        }


        private IEnumerator MoveBoatToDestination(BaseVehicle tugboat, Vector3 destination)
        {
            Rigidbody boatRigidbody = tugboat.GetComponent<Rigidbody>();
            if (boatRigidbody == null)
            {
                yield break;
            }

            while (Vector3.Distance(tugboat.transform.position,destination)>100) 
            {
               // Puts(Math.Round(Vector3.Distance(tugboat.transform.position, destination)).ToString());              
                Vector3 forceDirection = tugboat.transform.forward;
                ApplyThrottleForce(tugboat, boatRigidbody, forceDirection);

                tugboat.SendNetworkUpdateImmediate();
              
                yield return new WaitForFixedUpdate();              
            }

            ApplyThrottleForce(tugboat, boatRigidbody, Vector3.zero);
            var npc = tugboat.GetDriver();
            timer.Once(7f, () =>
            {
                DoTugReachDestination(tugboat, npc);
            });
        }

        private void DoTugReachDestination(BaseVehicle tugboat, BasePlayer npc)
        {
            npc.AdminKill();
            timer.Once(2f, () =>
            {
                MonumentInfo nearestMonument = FindNearestMonument(tugboat.transform.position);
                Vector3 correction = new Vector3(0, 0, 0);
                Vector3 moveCorrection = new Vector3(0, 0, 0);
                if (nearestMonument == null)
                {
                    return;
                }
                Puts($"Nearest Monument: {nearestMonument.displayPhrase.english}");               

              

                tugboat.Kill();
            });
        }

        private void ApplyThrottleForce(BaseVehicle tugboat, Rigidbody boatRigidbody, Vector3 direction)
        {
            float forceAmount = 10f; 
            boatRigidbody.AddForce(direction * forceAmount, ForceMode.Acceleration);
        }

        [ChatCommand("testnpcdrive")]
        private void TestNPCDriveCommand(BasePlayer player, string command, string[] args)
        {
            var pos = player.transform.position;
            pos.y = 0;
            Vector3 destination = FindNearestMonument(player.transform.position).transform.position;           

            Vector3 directionToDestination = destination - pos; 
            directionToDestination.y = 0; 

            Quaternion rotationToDestination = Quaternion.LookRotation(directionToDestination);


            var tugboat = SpawnTugBoat(tugboatPrefab, pos, rotationToDestination);
            if (tugboat == null) return;
            tugboat.SetFlag(BaseEntity.Flags.Reserved1, true); 
            timer.Once(5f, () =>
            {
                var npc = SpawnNPC(tugboat.transform.position);
                if (npc == null) return;

                MountNPCToTugboat(tugboat, npc);
                tugboat.SetFlag(BaseEntity.Flags.On, true);               
                tugboat.rigidBody.WakeUp();
                tugboat.BuoyancyWake();
               
                timer.Once(2f, () =>
                {                   
                    tugboat.StartCoroutine(MoveBoatToDestination(tugboat, destination));
                });
            });
        }
        private Vector3 GetMonumentPosition(string monumentName)
        {
            var monument = TerrainMeta.Path.Monuments.FirstOrDefault(m => m.displayPhrase.english.StartsWith(monumentName)&& m.displayPhrase.english.Contains("Oil Rig"));

            if (monument != null)
            {
                return monument.transform.position;
            }
            else
            {
                Puts($"Monument {monumentName} not found.");
                return Vector3.zero; 
            }
        }

        #endregion Extra Tugboat

        #region UI Class

        static class UI
        {
            public static CuiElementContainer CreateElementContainer(string name, string color, string aMin, string aMax, bool Usemouse = false, bool UseKeyboard = false)
            {
                var element = new CuiElementContainer()
              {
                  {
                      new CuiPanel
                      {
                          Image = { Color = color },
                          RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                          CursorEnabled = Usemouse,
                          KeyboardEnabled = UseKeyboard,                         
                      },
                      new CuiElement().Parent = "Hud",
                      name
                  }
              };
                return element;
            }
            public static void Panel(ref CuiElementContainer container, string name, string panel, string color, string aMin, string aMax)
            {
                container.Add(new CuiPanel
                {                     
                    Image = { Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                },
                panel, name);
            }
            public static void Label(ref CuiElementContainer container, string panel, string name, string text, int size, string aMin, string aMax, string color, TextAnchor align = TextAnchor.MiddleCenter,string labelid="ZmV1BQp=")
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Color = color, Font = "robotocondensed-bold.ttf", Align = align, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel, name);
            }
            public static void Button(ref CuiElementContainer container, string panel, string text,string textcolor, int size, string aMin, string aMax, string command, string buttoncolor ,TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiButton
                {
                    Button =
          {
             Command = command,
             Color = buttoncolor,
          },


                    RectTransform =
          {
              AnchorMin = aMin,
              AnchorMax = aMax,
          },

                    Text =
          {
              Text = text,
              Color = textcolor,
              FontSize = size,
              Align = align
          }
                },
                panel, CuiHelper.GetGuid());
            }
            public static void InputField(ref CuiElementContainer container, string panel, string text, int size, string textcolor,int charlenght ,string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiElement
                {
                    Name = "TestNameInput",
                    Parent = panel,
                    Components =
              {
                          new CuiInputFieldComponent
                          {
                             Text = text,
                             CharsLimit = charlenght,
                             Color = textcolor,
                             IsPassword = false,
                             Command = command,
                             Font = "robotocondensed-regular.ttf",
                             FontSize = size,
                             Align = align

                          },

                         new CuiRectTransformComponent
                         {
                             AnchorMin = aMin,
                             AnchorMax = aMax
                         }
              }
                });
            }

            public static void LoadImage(ref CuiElementContainer container, string panel, string png, string aMin, string aMax)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
              {
                  new CuiRawImageComponent { Png = png },
                  new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax }
              }
                });
            }
        }


        #endregion UI Class

        #region UI

        private void CreateConfigMenu(BasePlayer target)
        {
            var info = "All Config Settings You Can Change In-Game\n\nPlease select a config section to modify.\n\nNote:\n- ExtraLoot and Spawns are currently disabled (coming soon).";

            var elements = UI.CreateElementContainer("HRConfigUI", "0.5 0.5 0.5 0.9", "0.2 0.15", "0.75 0.9", true);
            UI.Panel(ref elements, "TopPanel", "HRConfigUI", "0.1 0.1 0.1 1", "0 0.95", "1 0.9999");
            UI.Button(ref elements, "TopPanel","X", "0 0 0 1", 16,"0.95 0.15","0.99 0.8","close.button", "1 0 0 1", TextAnchor.MiddleCenter);
            UI.Label(ref elements, "TopPanel","TopbarText","HeavyRig Config",16,"0.42 0.1", "0.6 0.9","1 1 1 1");

            UI.Button(ref elements, "HRConfigUI", "Bools", "1 1 1 1", 14, "0.36 0.85", "0.43 0.93", $"panelselect bool", "0.3 0.2 0.3 1");
            UI.Button(ref elements, "HRConfigUI", "Strings", "1 1 1 1", 14, "0.44 0.85", "0.51 0.93", $"panelselect string", "0.3 0.2 0.3 1");
            UI.Button(ref elements, "HRConfigUI", "ExtraLoot", "1 1 1 1", 14, "0.52 0.85", "0.59 0.93", ""/*$"panelselect el"*/, "0.7 0.7 0.7 0.7");
            UI.Button(ref elements, "HRConfigUI", "Spawns", "1 1 1 1", 14, "0.60 0.85", "0.67 0.93", ""/*$"panelselect spawn"*/, "0.7 0.7 0.7 0.7");

            UI.Label(ref elements, "HRConfigUI","InfoText",info,24, "0.02 0.02", "0.98 0.83","0.9 0.95 0 1");

            CuiHelper.AddUi(target, elements);           
        }
        private void CreateBoolPanel(BasePlayer target)
        {
            Dictionary<string, object> config = GetConfigValues();
            var bools = config.Where(kvp => kvp.Value is bool).ToDictionary(kvp => kvp.Key, kvp => (bool)kvp.Value);

            var elements = UI.CreateElementContainer("HRConfigUI", "0.5 0.5 0.5 0.9", "0.2 0.15", "0.75 0.9", true);
            UI.Panel(ref elements, "TopPanel", "HRConfigUI", "0.1 0.1 0.1 1", "0 0.95", "1 0.9999");
            UI.Button(ref elements, "TopPanel", "X", "0 0 0 1", 16, "0.95 0.15", "0.99 0.8", "close.button", "1 0 0 1", TextAnchor.MiddleCenter);
            UI.Label(ref elements, "TopPanel", "TopbarText", "HeavyRig Config", 16, "0.42 0.1", "0.6 0.9", "1 1 1 1");

            UI.Button(ref elements, "HRConfigUI", "Bools", "1 1 1 1", 14, "0.36 0.85", "0.43 0.93", $"panelselect bool", "0.3 0.2 0.3 1");
            UI.Button(ref elements, "HRConfigUI", "Strings", "1 1 1 1", 14, "0.44 0.85", "0.51 0.93", $"panelselect string", "0.3 0.2 0.3 1");
            UI.Button(ref elements, "HRConfigUI", "ExtraLoot", "1 1 1 1", 14, "0.52 0.85", "0.59 0.93", ""/*$"panelselect el"*/, "0.7 0.7 0.7 0.7");
            UI.Button(ref elements, "HRConfigUI", "Spawns", "1 1 1 1", 14, "0.60 0.85", "0.67 0.93", ""/*$"panelselect spawn"*/, "0.7 0.7 0.7 0.7");

            UI.Panel(ref elements, "BoolPanel", "HRConfigUI", "0.2 0.2 0.2 1", "0.02 0.02", "0.98 0.83");

            float yPos = 0.92f; 
            float yStep = 0.08f; 

            foreach (var setting in bools)
            {
                string key = setting.Key;
                bool value = setting.Value;

                UI.Label(ref elements, "BoolPanel", key, key, 20, $"0.02 {yPos}", $"0.75 {yPos + 0.05}", "1 1 1 1");
                UI.Button(ref elements, "BoolPanel", "True", "1 1 1 1", 16, $"0.77 {yPos}", $"0.85 {yPos + 0.05}", $"setconfigbool true {key}", value ? "0.2 0.8 0.2 1" : "0.5 0.5 0.5 1");
                UI.Button(ref elements, "BoolPanel", "False", "1 1 1 1", 16, $"0.87 {yPos}", $"0.95 {yPos + 0.05}", $"setconfigbool false {key}", !value ? "0.8 0.2 0.2 1" : "0.5 0.5 0.5 1");

                yPos -= yStep;
            }

            CuiHelper.AddUi(target, elements);
        }

        private void CreateStringPanel(BasePlayer target)
        {
            Dictionary<string, object> config = GetConfigValues();
            var strings = config.Where(kvp => kvp.Value is string).ToDictionary(kvp => kvp.Key, kvp => (string)kvp.Value);

            var elements = UI.CreateElementContainer("HRConfigUI", "0.5 0.5 0.5 0.9", "0.2 0.15", "0.75 0.9", true,true);
            UI.Panel(ref elements, "TopPanel", "HRConfigUI", "0.1 0.1 0.1 1", "0 0.95", "1 0.9999");
            UI.Button(ref elements, "TopPanel", "X", "0 0 0 1", 16, "0.95 0.15", "0.99 0.8", "close.button", "1 0 0 1", TextAnchor.MiddleCenter);
            UI.Label(ref elements, "TopPanel", "TopbarText", "HeavyRig Config", 16, "0.42 0.1", "0.6 0.9", "1 1 1 1");

            UI.Button(ref elements, "HRConfigUI", "Bools", "1 1 1 1", 14, "0.36 0.85", "0.43 0.93", $"panelselect bool", "0.3 0.2 0.3 1");
            UI.Button(ref elements, "HRConfigUI", "Strings", "1 1 1 1", 14, "0.44 0.85", "0.51 0.93", $"panelselect string", "0.3 0.2 0.3 1");
            UI.Button(ref elements, "HRConfigUI", "ExtraLoot", "1 1 1 1", 14, "0.52 0.85", "0.59 0.93", ""/*$"panelselect el"*/, "0.7 0.7 0.7 0.7");
            UI.Button(ref elements, "HRConfigUI", "Spawns", "1 1 1 1", 14, "0.60 0.85", "0.67 0.93", ""/*$"panelselect spawn"*/, "0.7 0.7 0.7 0.7");

            UI.Panel(ref elements, "StringPanel", "HRConfigUI", "0.2 0.2 0.2 1", "0.02 0.02", "0.98 0.83");

            float yPos = 0.92f;
            float yStep = 0.08f;

            foreach (var setting in strings)
            {
                string key = setting.Key;
                string value = setting.Value;
               
                UI.Label(ref elements, "StringPanel", key, key, 20, $"0.02 {yPos}", $"0.25 {yPos + 0.05}", "1 1 1 1");

                UI.InputField(ref elements, "StringPanel", value, 16,"1 1 1 1",50, $"0.27 {yPos}", $"0.95 {yPos + 0.05}", $"setconfigstring '{key}'");


                yPos -= yStep;
            }

            CuiHelper.AddUi(target, elements);
        }

        private void CreateELPanel(BasePlayer target)
        {
            var elements = UI.CreateElementContainer("HRConfigUI", "0.5 0.5 0.5 0.9", "0.2 0.15", "0.75 0.9", true);
            UI.Panel(ref elements, "TopPanel", "HRConfigUI", "0.1 0.1 0.1 1", "0 0.95", "1 0.9999");
            UI.Button(ref elements, "TopPanel", "X", "0 0 0 1", 16, "0.95 0.15", "0.99 0.8", "close.button", "1 0 0 1", TextAnchor.MiddleCenter);
            UI.Label(ref elements, "TopPanel", "TopbarText", "HeavyRig Config", 16, "0.42 0.1", "0.6 0.9", "1 1 1 1");

            UI.Button(ref elements, "HRConfigUI", "Bools", "1 1 1 1", 14, "0.36 0.85", "0.43 0.93", $"panelselect bool", "0.3 0.2 0.3 1");
            UI.Button(ref elements, "HRConfigUI", "Strings", "1 1 1 1", 14, "0.44 0.85", "0.51 0.93", $"panelselect string", "0.3 0.2 0.3 1");
            UI.Button(ref elements, "HRConfigUI", "ExtraLoot", "1 1 1 1", 14, "0.52 0.85", "0.59 0.93","" /*$"panelselect el"*/, "0.7 0.7 0.7 0.7");
            UI.Button(ref elements, "HRConfigUI", "Spawns", "1 1 1 1", 14, "0.60 0.85", "0.67 0.93","" /*$"panelselect spawn"*/, "0.7 0.7 0.7 0.7");

            UI.Panel(ref elements, "ExtraLootPanel", "HRConfigUI", "0.2 0.2 0.2 1", "0.02 0.02", "0.98 0.83");

            CuiHelper.AddUi(target, elements);
        }
        private void CreateLootPanel(BasePlayer target)
        {
            var elements = UI.CreateElementContainer("HRConfigUI", "0.5 0.5 0.5 0.9", "0.2 0.15", "0.75 0.9", true);
            UI.Panel(ref elements, "TopPanel", "HRConfigUI", "0.1 0.1 0.1 1", "0 0.95", "1 0.9999");
            UI.Button(ref elements, "TopPanel", "X", "0 0 0 1", 16, "0.95 0.15", "0.99 0.8", "close.button", "1 0 0 1", TextAnchor.MiddleCenter);
            UI.Label(ref elements, "TopPanel", "TopbarText", "HeavyRig Config", 16, "0.42 0.1", "0.6 0.9", "1 1 1 1");

            UI.Button(ref elements, "HRConfigUI", "Bools", "1 1 1 1", 14, "0.36 0.85", "0.43 0.93", $"panelselect bool", "0.3 0.2 0.3 1");
            UI.Button(ref elements, "HRConfigUI", "Strings", "1 1 1 1", 14, "0.44 0.85", "0.51 0.93", $"panelselect string", "0.3 0.2 0.3 1");
            UI.Button(ref elements, "HRConfigUI", "ExtraLoot", "1 1 1 1", 14, "0.52 0.85", "0.59 0.93","" /*$"panelselect el"*/, "0.7 0.7 0.7 0.7");
            UI.Button(ref elements, "HRConfigUI", "Spawns", "1 1 1 1", 14, "0.60 0.85", "0.67 0.93", ""/*$"panelselect spawn"*/, "0.7 0.7 0.7 0.7");

            UI.Panel(ref elements, "SpawnPanel", "HRConfigUI", "0.2 0.2 0.2 1", "0.02 0.02", "0.98 0.83");

            CuiHelper.AddUi(target, elements);
        }
        [ConsoleCommand("close.button")]
        private void CloseConfigUI(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection?.player as BasePlayer;
            if (player == null) return;
            CuiHelper.DestroyUi(player, "HRConfigUI");
        }

        [ConsoleCommand("panelselect")]
        private void SelectConfigPanel(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length == 0) return;

            string selection = arg.Args[0];

            CuiHelper.DestroyUi(player, "HRConfigUI");

            // Add selected panel
            switch (selection)
            {
                case "bool":
                    CreateBoolPanel(player);
                    break;
                case "string":
                    CreateStringPanel(player);
                    break;
                case "el":
                    CreateELPanel(player);
                    break;
                case "spawn":
                    CreateLootPanel(player);
                    break;
            }
        }
        [ConsoleCommand("setconfigbool")]
        private void CmdSetboolConfig(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
           
            string valueArg = arg.Args[0].ToLower();
            bool newValue = valueArg == "true";
          
            string settingKey = string.Join(" ", arg.Args.Skip(1));

            Dictionary<string, object> config = GetConfigValues();

            if (config.ContainsKey(settingKey) && config[settingKey] is bool)
            {
                config[settingKey] = newValue;
                SaveConfig(config);               
            }          

            CuiHelper.DestroyUi(player, "HRConfigUI");
            CreateBoolPanel(player);
        }

        [ConsoleCommand("setconfigstring")]
        private void CmdSetConfigString(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();

            string key = arg.Args[0].Trim('\'');
            string value = string.Join(" ", arg.Args.Skip(1));

            Dictionary<string, object> config = GetConfigValues();
            if (config.ContainsKey(key))
            {
                config[key] = value;
                SaveConfig(config);
               // Puts($"Updated {key} to {value}");
            }
            
            CuiHelper.DestroyUi(player, "HRConfigUI");
            CreateStringPanel(player);
        }


        private Dictionary<string, object> GetConfigValues()
        {
            return Config.ReadObject<Dictionary<string, object>>();
        }

        private void SaveConfig(Dictionary<string, object> config)
        {
            Config.WriteObject(config, true);
        }

        #endregion UI
    }
}
 