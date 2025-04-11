using Rust;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins.SputnikExtensionMethods;
using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.AI;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("Sputnik", "Adem", "1.4.8")]

    class Sputnik : RustPlugin
    {
        #region Variables
        const bool en = false;
        static Sputnik ins;
        [PluginReference] Plugin NpcSpawn, PveMode, GUIAnnouncements, DiscordMessages, ZoneManager, RaidableBases, Economics, ServerRewards, IQEconomic, Notify, DynamicPVP, IQDronePatrol, AlphaLoot;
        HashSet<string> subscribeMethods = new HashSet<string>
        {
           "OnEntitySpawned",
           "OnEntityTakeDamage",
           "OnEntityDeath",
           "OnCorpsePopulate",
           "OnPlayerSleep",
           "CanHelicopterTarget",
           "OnTrapTrigger",
           "OnCardSwipe",
           "OnLootSpawn",
           "CanHackCrate",
           "CanLootEntity",
           "OnLootEntity",

           "OnCustomNpcTarget",
           "CanEntityTakeDamage",
           "CanEntityTrapTrigger",
           "CanEntityBeTargeted",
           "CanPopulateLoot",
           "OnCustomLootContainer",
           "OnCustomLootNPC",
           "OnRestoreUponDeath",
           "SetOwnerPveMode",
           "ClearOwnerPveMode",
           "OnCreateDynamicPVP"
        };
        HashSet<string> permanentHooks = new HashSet<string>
        {
            "OnLootSpawn"
        };
        EventController eventController;
        #endregion Variables

        #region Hooks
        void Init()
        {
            Unsubscribes(true);
        }

        void OnServerInitialized()
        {
            ins = this;

            if (!NpcSpawnManager.IsNpcSpawnReady())
                return;
            else if (!DataFileManager.TryLoadData())
                return;

            LoadDefaultMessages();
            UpdateConfig();
            UpdateLootTables();
            UnsubscribesPermanentHooks();

            GuiManager.LoadImages();
            SpawnPositionFinder.InitialUpdate();
            LootManager.InitialLootManagerUpdate();
            EventLauncher.AutoStartEvent();
        }

        void Unload()
        {
            EventLauncher.StopEvent(true);
            SpawnPositionFinder.StopCachingSpawnPoints();
            ins = null;
        }

        void OnEntitySpawned(LockedByEntCrate entity)
        {
            if (!entity.IsExists())
                return;

            if (entity.ShortPrefabName == "heli_crate")
                LootManager.OnHeliCrateSpawned(entity);
        }

        object OnEntityTakeDamage(PatrolHelicopter patrolHelicopter, HitInfo info)
        {
            if (patrolHelicopter == null || patrolHelicopter.net == null || info == null || !info.InitiatorPlayer.IsRealPlayer())
                return null;

            EventHeli eventHeli = EventHeli.GetEventHeliByNetId(patrolHelicopter.net.ID.Value);

            if (eventHeli == null)
                return null;

            SputnikClass sputnikClass = eventHeli.sputnikClass;

            if (!sputnikClass.IsPlayerCanDealDamage(info.InitiatorPlayer, patrolHelicopter, true))
                return true;
            else
                eventHeli.OnHeliAttacked(info.InitiatorPlayer.userID);

            return null;
        }

        object OnEntityTakeDamage(AutoTurret autoTurret, HitInfo info)
        {
            if (autoTurret == null || autoTurret.net == null || info == null)
                return null;

            SputnikClass sputnikClass = SputnikClass.GetSputnikByTurretUid(autoTurret.net.ID.Value);

            if (sputnikClass == null)
                return null;

            if (!info.InitiatorPlayer.IsRealPlayer())
                return true;
            else if (!sputnikClass.IsPlayerCanDealDamage(info.InitiatorPlayer, autoTurret, true))
                return true;
            else
                sputnikClass.OnSputnikAttacked();

            return null;
        }

        object OnEntityTakeDamage(Landmine landmine, HitInfo info)
        {
            if (landmine == null || landmine.net == null || info == null)
                return null;

            SputnikClass sputnikClass = SputnikClass.GetSputnikByLandmineUid(landmine.net.ID.Value);

            if (sputnikClass == null)
                return null;

            if (!info.InitiatorPlayer.IsRealPlayer())
                return true;
            else if (!sputnikClass.IsPlayerCanDealDamage(info.InitiatorPlayer, landmine, true) || sputnikClass.IsPveModeBlockAction(info.InitiatorPlayer))
                return true;
            else
                sputnikClass.OnSputnikAttacked();

            return null;
        }

        object OnEntityTakeDamage(ScientistNPC scientistNPC, HitInfo info)
        {
            if (scientistNPC == null || scientistNPC.net == null || info == null || !info.InitiatorPlayer.IsRealPlayer())
                return null;

            SputnikClass sputnikClass = SputnikClass.GetSputnikByNpcUid(scientistNPC.net.ID.Value);

            if (sputnikClass == null)
                return null;

            if (!sputnikClass.IsPlayerCanDealDamage(info.InitiatorPlayer, scientistNPC, true))
                return true;
            else
                sputnikClass.OnSputnikAttacked();

            return null;
        }

        void OnEntityDeath(PatrolHelicopter patrolHelicopter, HitInfo info)
        {
            if (patrolHelicopter == null || patrolHelicopter.net == null)
                return;

            EventHeli eventHeli = EventHeli.GetEventHeliByNetId(patrolHelicopter.net.ID.Value);

            if (eventHeli != null && eventHeli.lastAttackedPlayer != 0)
                EconomyManager.AddBalance(eventHeli.lastAttackedPlayer, _config.supportedPluginsConfig.economicsConfig.heliPoint);
        }

        void OnEntityDeath(AutoTurret autoTurret, HitInfo info)
        {
            if (autoTurret == null || autoTurret.net == null || info == null || !info.InitiatorPlayer.IsRealPlayer())
                return;

            SputnikClass sputnikClass = SputnikClass.GetSputnikByTurretUid(autoTurret.net.ID.Value);

            if (sputnikClass != null)
                EconomyManager.AddBalance(info.InitiatorPlayer.userID, _config.supportedPluginsConfig.economicsConfig.turretPoint);
        }

        void OnEntityDeath(ScientistNPC scientistNPC, HitInfo info)
        {
            if (scientistNPC == null || scientistNPC.net == null || info == null || !info.InitiatorPlayer.IsRealPlayer())
                return;

            if (NpcSpawnManager.GetScientistByNetId(scientistNPC.net.ID.Value))
                EconomyManager.AddBalance(info.InitiatorPlayer.userID, _config.supportedPluginsConfig.economicsConfig.npcPoint);
        }

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null)
                return;

            ZoneController zoneController = ZoneController.GetZoneControllerByPlayerUserID(player.userID);

            if (zoneController != null)
                zoneController.OnPlayerLeaveZone(player);
        }

        void OnCorpsePopulate(ScientistNPC scientistNPC, NPCPlayerCorpse corpse)
        {
            if (scientistNPC == null)
                return;

            NpcConfig npcConfig = NpcSpawnManager.GetNpcConfigByDisplayName(scientistNPC.displayName);

            if (npcConfig == null)
                return;

            ins.NextTick(() =>
            {
                if (corpse == null)
                    return;

                if (!corpse.containers.IsNullOrEmpty() && corpse.containers[0] != null)
                    LootManager.UpdateItemContainer(corpse.containers[0], npcConfig.lootTableConfig, npcConfig.lootTableConfig.clearDefaultItemList);

                if (npcConfig.deleteCorpse && !corpse.IsDestroyed)
                    corpse.Kill();
            });
        }

        void OnPlayerSleep(BasePlayer player)
        {
            if (!player.IsRealPlayer())
                return;

            ZoneController zoneController = ZoneController.GetZoneControllerByPlayerUserID(player.userID);

            if (zoneController != null)
                zoneController.OnPlayerLeaveZone(player);
        }

        object OnEntityEnter(TargetTrigger trigger, ScientistNPC scientistNPC)
        {
            if (trigger == null || scientistNPC == null || scientistNPC.net == null)
                return null;

            AutoTurret autoTurret = trigger.GetComponentInParent<AutoTurret>();

            if (autoTurret == null || autoTurret.net == null)
                return null;

            SputnikClass sputnikClass = SputnikClass.GetSputnikByTurretUid(autoTurret.net.ID.Value);

            if (sputnikClass != null)
                return true;

            return null;
        }

        object CanHelicopterTarget(PatrolHelicopterAI heli, BasePlayer player)
        {
            if (heli == null || heli.helicopterBase == null || heli.helicopterBase.net == null)
                return null;

            EventHeli eventHeli = EventHeli.GetEventHeliByNetId(heli.helicopterBase.net.ID.Value);

            if (eventHeli != null && !eventHeli.IsHeliCanTarget())
                return false;

            return null;
        }

        object OnTrapTrigger(Landmine landmine, GameObject gameObject)
        {
            if (landmine == null)
                return null;

            ScientistNPC scientistNPC = gameObject.ToBaseEntity() as ScientistNPC;

            if (scientistNPC != null && NpcSpawnManager.GetNpcConfigByDisplayName(scientistNPC.displayName) != null)
                return true;

            return null;
        }

        object OnCardSwipe(CardReader cardReader, Keycard keycard, BasePlayer player)
        {
            if (player == null || cardReader == null || cardReader.net == null || keycard == null)
                return null;

            SputnikClass sputnikClass = SputnikClass.GetSputnikByCardReaderUid(cardReader.net.ID.Value);

            if (sputnikClass == null)
                return null;

            sputnikClass.OnCardSwipe(keycard, player);
            return true;
        }

        void OnLootSpawn(LootContainer container)
        {
            if (container == null || container.inventory == null)
                return;

            if (_config.customCardConfig.enableSpawnInDefaultCrates && LootManager.GetContainerDataByNetId(container.net.ID.Value) == null)
                ItemSpawner.SpawnSpaceCardInCrate(container);
        }

        object CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            if (player == null || crate == null || crate.net == null)
                return null;

            SputnikClass sputnikClass = SputnikClass.GetSputnikByCrateUid(crate.net.ID.Value);
            StorageContainerData storageContainerData = LootManager.GetContainerDataByNetId(crate.net.ID.Value);

            if (sputnikClass == null || storageContainerData == null)
                return null;

            CrateConfig crateConfig = LootManager.GetCrateConfigByPresetName(storageContainerData.presetName);

            if (!sputnikClass.IsAgressive())
            {
                sputnikClass.OnSputnikAttacked();
                return true;
            }

            if (sputnikClass.IsCardReaderExistAndClosed())
            {
                if (crateConfig.needSpaceCard)
                {
                    NotifyManager.SendMessageToPlayer(player, "NeedUseCard", _config.prefix);
                    return true;
                }
            }

            if (sputnikClass.IsPveModeBlockAction(player))
                return null;

            EconomyManager.AddBalance(player.userID, _config.supportedPluginsConfig.economicsConfig.hackCratePoint);
            crate.Invoke(() => LootManager.UpdateCrateHackTime(crate, storageContainerData.presetName), 1.1f);

            return null;
        }

        object CanLootEntity(BasePlayer player, LootContainer container)
        {
            if (player == null || container == null || container.net == null)
                return null;

            SputnikClass sputnikClass = SputnikClass.GetSputnikByCrateUid(container.net.ID.Value);

            if (sputnikClass == null)
                return null;

            if (!sputnikClass.IsAgressive())
            {
                sputnikClass.OnSputnikAttacked();
                return true;
            }

            if (sputnikClass.IsCardReaderExistAndClosed())
            {
                StorageContainerData storageContainerData = LootManager.GetContainerDataByNetId(container.net.ID.Value);

                if (storageContainerData != null)
                {
                    CrateConfig crateConfig = LootManager.GetCrateConfigByPresetName(storageContainerData.presetName);

                    if (crateConfig != null && crateConfig.needSpaceCard)
                    {
                        NotifyManager.SendMessageToPlayer(player, "NeedUseCard", _config.prefix);
                        return true;
                    }
                }
            }

            return null;
        }

        void OnLootEntity(BasePlayer player, LootContainer container)
        {
            if (player == null || container == null || container.net == null)
                return;

            SputnikClass sputnikClass = SputnikClass.GetSputnikByCrateUid(container.net.ID.Value);

            if (sputnikClass == null)
                return;

            sputnikClass.OnSputnikAttacked();
            sputnikClass.CheckEventPassing();
            LootManager.OnEventCrateLooted(container, player.userID);
        }

        #region OtherPlugins
        object OnCustomNpcTarget(ScientistNPC scientistNPC, BasePlayer player)
        {
            if (eventController == null || scientistNPC == null || scientistNPC.net == null)
                return null;

            SputnikClass sputnikClass = SputnikClass.GetSputnikByNpcUid(scientistNPC.net.ID.Value);

            if (sputnikClass == null)
                return null;

            if (!sputnikClass.IsAgressive() && !_config.agressiveConfig.npcAgressiveMode)
                return false;

            return null;
        }

        object CanEntityTakeDamage(AutoTurret autoTurret, HitInfo info)
        {
            if (eventController == null || autoTurret == null || autoTurret.net == null || info == null)
                return null;

            SputnikClass sputnikClass = SputnikClass.GetSputnikByTurretUid(autoTurret.net.ID.Value);

            if (sputnikClass == null)
                return null;

            if (!info.InitiatorPlayer.IsRealPlayer())
                return false;
            else if (!sputnikClass.IsPlayerCanDealDamage(info.InitiatorPlayer, autoTurret, false) || sputnikClass.IsPveModeBlockAction(info.InitiatorPlayer))
                return false;

            return true;
        }

        object CanEntityTakeDamage(Landmine landmine, HitInfo info)
        {
            if (eventController == null || landmine == null || landmine.net == null || info == null)
                return null;

            SputnikClass sputnikClass = SputnikClass.GetSputnikByLandmineUid(landmine.net.ID.Value);

            if (sputnikClass == null)
                return null;

            if (!info.InitiatorPlayer.IsRealPlayer())
                return false;
            else if (!sputnikClass.IsPlayerCanDealDamage(info.InitiatorPlayer, landmine, false) || sputnikClass.IsPveModeBlockAction(info.InitiatorPlayer))
                return false;

            return true;
        }

        object CanEntityTakeDamage(BasePlayer player, HitInfo hitinfo)
        {
            if (eventController == null || !player.IsRealPlayer() || hitinfo == null)
                return null;

            if (hitinfo.InitiatorPlayer.IsRealPlayer() && !_config.supportedPluginsConfig.pveMode.enable)
            {
                ZoneController zoneController = ZoneController.GetZoneControllerByPlayerUserID(player.userID);

                if (zoneController != null && zoneController.IsPvpZone() && ZoneController.GetZoneControllerByPlayerUserID(hitinfo.InitiatorPlayer.userID) != null)
                    return true;
            }
            else if (hitinfo.Initiator != null && hitinfo.Initiator.net != null)
            {
                if (hitinfo.Initiator is AutoTurret && SputnikClass.GetSputnikByTurretUid(hitinfo.Initiator.net.ID.Value) != null)
                    return true;
                else if (hitinfo.Initiator is Landmine && SputnikClass.GetSputnikByLandmineUid(hitinfo.Initiator.net.ID.Value) != null)
                    return true;
            }

            return null;
        }

        object CanEntityTrapTrigger(Landmine landmine, BasePlayer player)
        {
            if (eventController == null || landmine == null || !player.IsRealPlayer())
                return null;

            if (SputnikClass.GetSputnikByLandmineUid(landmine.net.ID.Value) != null)
                return true;

            return null;
        }

        object CanEntityTrapTrigger(Landmine landmine, ScientistNPC scientistNPC)
        {
            if (eventController == null || scientistNPC == null)
                return null;

            if (NpcSpawnManager.GetNpcConfigByDisplayName(scientistNPC.displayName) != null)
                return false;

            return null;
        }

        object CanEntityBeTargeted(BasePlayer player, AutoTurret turret)
        {
            if (eventController == null || turret == null || turret.net == null)
                return null;

            SputnikClass sputnikClass = SputnikClass.GetSputnikByTurretUid(turret.net.ID.Value);

            if (sputnikClass == null)
                return null;

            if (!player.IsRealPlayer())
                return false;
            else if (!sputnikClass.IsAgressive() && !_config.agressiveConfig.turretAgressiveMode)
                return false;
            else if (!sputnikClass.IsPveModeBlockAction(player))
                return true;

            return null;
        }

        object CanPopulateLoot(ScientistNPC scientistNPC, NPCPlayerCorpse corpse)
        {
            if (eventController == null || scientistNPC == null || scientistNPC.net == null)
                return null;

            NpcConfig npcConfig = NpcSpawnManager.GetNpcConfigByDisplayName(scientistNPC.displayName);

            if (npcConfig != null && !npcConfig.lootTableConfig.isAlphaLoot)
                return true;

            return null;
        }

        object CanPopulateLoot(LootContainer lootContainer)
        {
            if (eventController == null || lootContainer == null || lootContainer.net == null)
                return null;

            StorageContainerData storageContainerData = LootManager.GetContainerDataByNetId(lootContainer.net.ID.Value);

            if (storageContainerData == null)
                return null;

            CrateConfig crateConfig = LootManager.GetCrateConfigByPresetName(storageContainerData.presetName);

            if (!crateConfig.lootTableConfig.isAlphaLoot)
                return true;

            return null;
        }

        object OnCustomLootContainer(NetworkableId netID)
        {
            if (eventController == null || netID == null)
                return null;

            StorageContainerData storageContainerData = LootManager.GetContainerDataByNetId(netID.Value);

            if (storageContainerData == null)
                return null;

            CrateConfig crateConfig = LootManager.GetCrateConfigByPresetName(storageContainerData.presetName);

            if (!crateConfig.lootTableConfig.isCustomLoot)
                return true;

            return null;
        }

        object OnCustomLootNPC(NetworkableId netID)
        {
            if (eventController == null || netID == null)
                return null;

            ScientistNPC scientistNPC = NpcSpawnManager.GetScientistByNetId(netID.Value);

            if (scientistNPC == null)
                return null;

            NpcConfig npcConfig = NpcSpawnManager.GetNpcConfigByDisplayName(scientistNPC.displayName);

            if (npcConfig != null && !npcConfig.lootTableConfig.isCustomLoot)
                return true;

            return null;
        }

        object OnContainerPopulate(LootContainer lootContainer)
        {
            if (eventController == null || lootContainer == null || lootContainer.net == null)
                return null;

            StorageContainerData storageContainerData = LootManager.GetContainerDataByNetId(lootContainer.net.ID.Value);

            if (storageContainerData != null)
            {
                CrateConfig crateConfig = LootManager.GetCrateConfigByPresetName(storageContainerData.presetName);

                if (crateConfig != null && !crateConfig.lootTableConfig.isLootTablePLugin)
                    return true;
            }

            return null;
        }

        object OnCorpsePopulate(NPCPlayerCorpse corpse)
        {
            if (eventController == null || corpse == null)
                return null;

            NpcConfig npcConfig = NpcSpawnManager.GetNpcConfigByDisplayName(corpse.playerName);

            if (npcConfig != null && !npcConfig.lootTableConfig.isLootTablePLugin)
                return true;

            return null;
        }

        object OnRestoreUponDeath(BasePlayer player)
        {
            if (player == null || eventController == null)
                return null;

            if (_config.supportedPluginsConfig.restoreUponDeath.disableRestore)
            {
                ZoneController zoneController = ZoneController.GetZoneControllerByPlayerUserID(player.userID);

                if (zoneController != null)
                    return true;
            }

            return null;
        }

        void SetOwnerPveMode(string eventName, BasePlayer owner)
        {
            if (eventController == null)
                return;

            PveModeController pveModeController = PveModeController.GetPveControllerByZoneName(eventName);

            if (pveModeController != null)
                pveModeController.OnNewOwnerSet(owner);
        }

        void ClearOwnerPveMode(string eventName)
        {
            if (eventController == null)
                return;

            PveModeController pveModeController = PveModeController.GetPveControllerByZoneName(eventName);

            if (pveModeController != null)
                pveModeController.OnOwnerDeleted();
        }

        object OnCreateDynamicPVP(string eventName, PatrolHelicopter patrolHelicopter)
        {
            if (eventController == null || patrolHelicopter == null || patrolHelicopter.net == null)
                return null;

            if (ins._config.supportedPluginsConfig.betterNpcConfig.isHeliNpc)
                return null;

            EventHeli eventHeli = EventHeli.GetEventHeliByNetId(patrolHelicopter.net.ID.Value);

            if (eventHeli != null)
                return true;

            return null;
        }
        #endregion OtherPlugins
        #endregion Hooks

        #region Commands
        [ChatCommand("sputnikstart")]
        void ChatStartCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin)
                return;

            if (arg != null && arg.Length >= 1)
                EventLauncher.DelayStartEvent(false, player, arg[0]);
            else
                EventLauncher.DelayStartEvent(false, player);
        }

        [ChatCommand("sputnikstop")]
        void ChatStopCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin)
                return;

            EventLauncher.StopEvent();
        }

        [ConsoleCommand("sputnikstart")]
        void ConsoleStartCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                return;

            if (arg.Args != null && arg.Args.Length > 0)
                EventLauncher.DelayStartEvent(false, null, arg.Args[0]);
            else
                EventLauncher.DelayStartEvent(false);
        }

        [ConsoleCommand("sputnikstop")]
        void ConsoleStopCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                EventLauncher.StopEvent();
        }

        [ChatCommand("givespacecard")]
        void GiveCustomItemChatCommand(BasePlayer player, string command, string[] arg)
        {
            if (player == null || !player.IsAdmin || arg == null)
                return;

            ItemSpawner.GiveSpaceCardToPlayer(player);
            PrintToChat(player, GetMessage("GetSpaceCard", player.UserIDString, _config.prefix));
        }

        [ConsoleCommand("givespacecard")]
        void GiveCustomItemCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            BasePlayer target = null;

            if (arg.Args.Length >= 1)
            {
                ulong userId = Convert.ToUInt64(arg.Args[0]);
                target = BasePlayer.FindByID(userId);
            }

            if (target == null)
            {
                PrintToConsole(player, "Player not found");
                return;
            }

            ItemSpawner.GiveSpaceCardToPlayer(target);
            PrintToChat(target, GetMessage("GetSpaceCard", target.UserIDString, _config.prefix));
            Puts($"A space card was given to {target.displayName}");
        }

        [ChatCommand("sputnikspawnpoint")]
        void SpawnPointCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin)
                return;

            if (arg.Length <= 0)
            {
                PrintToChat(player, _config.prefix + " Use the command: /sputnikspawnpoint <DebrisPresetName>");
                return;
            }

            string sputnikPresetName = arg[0];

            SputnikDebrisConfig sputnikDebrisConfig = _config.sputnikDebrisConfigs.FirstOrDefault(x => x.presetName == sputnikPresetName);
            if (sputnikDebrisConfig == null)
            {
                PrintToChat(player, _config.prefix + " <color=#ce3f27>Couldn't</color> find the preset");
                return;
            }

            sputnikDebrisConfig.customSpawnPoints.Add(player.transform.position.ToString());
            SaveConfig();
            PrintToChat(player, _config.prefix + " New spawn point <color=#738d43>successfully</color> added");
        }

        [ChatCommand("getlocalposition")]
        void SpawnGetLocalPositonCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin)
                return;

            if (eventController == null)
            {
                NotifyManager.PrintError(player, $"{_config.prefix} The event must be launched!");
                return;
            }

            SputnikClass sputnikClass = SputnikClass.sputnikClasses.Where(x => x != null).Min(x => Vector3.Distance(player.transform.position, x.transform.position));

            if (sputnikClass != null)
            {
                string position = PositionDefiner.GetLocalPosition(sputnikClass.transform, player.transform.position).ToString();
                float rotation = (player.viewAngles - sputnikClass.transform.rotation.eulerAngles).y;
                Debug("Position", position);
                Debug("Rotation", new Vector3(0, rotation, 0));
            }

        }
        #endregion Commands

        #region Methods
        void Unsubscribes(bool includePermanentHooks)
        {
            foreach (string hook in subscribeMethods)
                if (includePermanentHooks || !permanentHooks.Contains(hook))
                    Unsubscribe(hook);
        }

        void UnsubscribesPermanentHooks()
        {
            foreach (string hook in permanentHooks)
                Unsubscribe(hook);
        }

        void Subscribes(bool includePermanentHooks)
        {
            foreach (string hook in subscribeMethods)
                if (includePermanentHooks || !permanentHooks.Contains(hook))
                    Subscribe(hook);
        }

        void UpdateConfig()
        {
            PluginConfig defaultConfig = PluginConfig.DefaultConfig();

            if (_config.version == Version) return;

            if (_config.version.Minor == 0)
            {
                if (_config.version.Patch <= 2)
                {
                    _config.supportedPluginsConfig.zoneManager = new ZoneManagerConfig
                    {
                        enable = false,
                        blockFlags = new HashSet<string>
                        {
                            "eject",
                            "pvegod"
                        }
                    };
                }
                if (_config.version.Patch <= 3)
                {
                    _config.supportedPluginsConfig.raidableBases = new RaidableBasesConfig();
                    _config.mainConfig.destroyAfterLootingTime = 300;
                }
                _config.version = new VersionNumber(1, 1, 0);
            }

            if (_config.version.Minor == 1)
            {
                if (_config.version.Patch <= 2)
                {
                    _config.agressiveConfig = new AgressiveConfig
                    {
                        agressiveSecurityMode = true,
                        agressiveTime = 120,
                        npcAgressiveMode = true,
                        turretAgressiveMode = true,
                        heliAgressiveMode = false,
                    };

                    foreach (SputnikDebrisConfig sputnikDebrisConfig in _config.sputnikDebrisConfigs)
                    {
                        sputnikDebrisConfig.useCustomSpawnPoints = false;
                        sputnikDebrisConfig.customSpawnPoints = new List<string>();
                    }
                }
                _config.version = new VersionNumber(1, 2, 0);
            }

            if (_config.version.Minor == 2)
            {
                if (_config.version.Patch == 0)
                {
                    foreach (HeliConfig heliConfig in _config.heliConfigs)
                    {
                        heliConfig.outsideTime = 60;
                    }
                }
                if (_config.version.Patch <= 1)
                {
                    foreach (SputnikDebrisConfig sputnikDebrisConfig in _config.sputnikDebrisConfigs)
                    {
                        sputnikDebrisConfig.markerConfig.isRingMarker = true;
                        sputnikDebrisConfig.markerConfig.isShopMarker = true;
                    }
                }
                if (_config.version.Patch <= 5)
                {
                    _config.supportedPluginsConfig.zoneManager.blockIDs = new HashSet<string>
                    {
                        "Example"
                    };

                    _config.supportedPluginsConfig.restoreUponDeath = new RestoreUponDeathConfig
                    {
                        disableRestore = false
                    };
                }
                _config.version = new VersionNumber(1, 3, 0);
            }

            if (_config.version.Minor == 3)
            {
                if (_config.version.Patch == 0)
                {
                    _config.supportedPluginsConfig.pveMode.scaleDamage = new Dictionary<string, float>
                    {
                        ["Npc"] = 1f,
                        ["Helicopter"] = 2f,
                        ["Turret"] = 2f,
                    };
                }

                if (_config.version.Patch <= 1)
                {
                    PrefabLootTableConfigs prefabConfigs = new PrefabLootTableConfigs
                    {
                        isEnable = false,
                        prefabs = new List<PrefabConfig>
                        {
                            new PrefabConfig
                            {
                                minLootScale = 1,
                                maxLootScale = 1,
                                prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab"
                            }
                        }
                    };

                    _config.notifyConfig.gameTipConfig = defaultConfig.notifyConfig.gameTipConfig;

                    foreach (CrateConfig crateConfig in _config.crateConfigs)
                    {
                        if (crateConfig.lootTableConfig.items.Any(x => x.shortname == _config.customCardConfig.shortName))
                            crateConfig.lootTableConfig.isRandomItemsEnable = true;

                        crateConfig.lootTableConfig.prefabConfigs = prefabConfigs;
                    }

                    foreach (NpcConfig npcConfig in _config.npcConfigs)
                        npcConfig.lootTableConfig.prefabConfigs = prefabConfigs;

                    foreach (EventConfig eventConfig in _config.eventConfigs)
                    {
                        eventConfig.minTimeAfterWipe = 0;
                        eventConfig.maxTimeAfterWipe = -1;
                    }

                    _config.guiConfig.offsetMinY = defaultConfig.guiConfig.offsetMinY;
                    _config.supportedPluginsConfig.pveMode.cooldownOwner = defaultConfig.supportedPluginsConfig.pveMode.cooldownOwner;

                    _config.mainConfig.maxGroundDamageDistance = defaultConfig.mainConfig.maxGroundDamageDistance;
                    _config.mainConfig.maxHeliDamageDistance = defaultConfig.mainConfig.maxHeliDamageDistance;

                    _config.spawnConfig.isRiverDisbled = true;
                    _config.spawnConfig.isBeachDisbled = true;
                    _config.spawnConfig.isMonumentsDisbled = true;
                    _config.spawnConfig.isNearestPoint = true;
                }

                if (_config.version.Patch <= 4)
                {
                    ins._config.notifyConfig.isChatEnable = true;
                }

                if (_config.version.Patch <= 5)
                {
                    foreach (CrateConfig crateConfig in _config.crateConfigs)
                        foreach (LootItemConfig lootItemConfig in crateConfig.lootTableConfig.items)
                            lootItemConfig.genomes = new List<string>();

                    foreach (NpcConfig npcConfig in _config.npcConfigs)
                        foreach (LootItemConfig lootItemConfig in npcConfig.lootTableConfig.items)
                            lootItemConfig.genomes = new List<string>();
                }

                if (_config.version.Patch <= 7)
                {
                    ins._config.supportedPluginsConfig.pveMode.showEventOwnerNameOnMap = true;
                }

                _config.version = new VersionNumber(1, 4, 0);
            }

            if (_config.version.Minor == 4)
            {
                if (_config.version.Patch == 0)
                {
                    LootTableConfig defaultLootTable = new LootTableConfig
                    {
                        clearDefaultItemList = false,
                        isAlphaLoot = false,
                        isCustomLoot = false,
                        prefabConfigs = new PrefabLootTableConfigs
                        {
                            isEnable = false,
                            prefabs = new List<PrefabConfig>
                            {
                                new PrefabConfig
                                {
                                    minLootScale = 1,
                                    maxLootScale = 1,
                                    prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab"
                                }
                            }
                        },
                        isRandomItemsEnable = false,
                        maxItemsAmount = 1,
                        minItemsAmount = 2,
                        items = new List<LootItemConfig>
                        {
                            new LootItemConfig
                            {
                                shortname = "scrap",
                                minAmount = 100,
                                maxAmount = 200,
                                chance = 100f,
                                isBlueprint = false,
                                skin = 0,
                                name = "",
                                genomes = new List<string>()
                            }
                        }
                    };

                    foreach (HeliConfig heliConfig in ins._config.heliConfigs)
                        heliConfig.baseLootTableConfig = defaultLootTable;

                    _config.supportedPluginsConfig.betterNpcConfig = new BetterNpcConfig
                    {
                        isHeliNpc = false,
                    };
                }

                if (_config.version.Patch <= 1)
                {
                    foreach (SputnikDebrisConfig sputnikDebrisConfig in _config.sputnikDebrisConfigs)
                    {
                        sputnikDebrisConfig.zoneConfig.isColoredBorder = sputnikDebrisConfig.zoneConfig.isDome;
                        sputnikDebrisConfig.zoneConfig.brightness = 5;
                        sputnikDebrisConfig.zoneConfig.borderColor = 2;
                    }
                }

                if (_config.version.Patch <= 3)
                {
                    _config.supportedPluginsConfig.iqDroneConfig = defaultConfig.supportedPluginsConfig.iqDroneConfig;

                    foreach (SputnikDebrisConfig sputnikDebrisConfig in _config.sputnikDebrisConfigs)
                        sputnikDebrisConfig.iqDronePreset = "lite";
                }

                if (_config.version.Patch <= 5)
                {
                    foreach (NpcConfig npcConfig in _config.npcConfigs)
                    {
                        npcConfig.presetName = npcConfig.displayName;
                        npcConfig.lootTableConfig.alphaLootPresetName = string.Empty;
                    }

                    foreach (CrateConfig crateConfig in _config.crateConfigs)
                        crateConfig.lootTableConfig.alphaLootPresetName = string.Empty;

                    foreach (SputnikDebrisConfig sputnikDebrisConfig in _config.sputnikDebrisConfigs)
                        if (sputnikDebrisConfig.iqDronePreset == null)
                            sputnikDebrisConfig.iqDronePreset = "";
                }
            }

            _config.version = Version;
            SaveConfig();
        }

        void UpdateLootTables()
        {
            foreach (CrateConfig crateConfig in _config.crateConfigs)
                UpdateBaseLootTable(crateConfig.lootTableConfig);

            foreach (NpcConfig npcConfig in _config.npcConfigs)
                UpdateBaseLootTable(npcConfig.lootTableConfig);
        }

        void UpdateBaseLootTable(LootTableConfig lootTableConfig)
        {
            for (int i = 0; i < lootTableConfig.items.Count; i++)
            {
                LootItemConfig lootItemConfig = lootTableConfig.items[i];

                if (lootItemConfig.chance <= 0)
                    lootTableConfig.items.RemoveAt(i);
            }

            lootTableConfig.items = lootTableConfig.items.OrderByQuickSort(x => x.chance);

            if (lootTableConfig.maxItemsAmount > lootTableConfig.items.Count)
                lootTableConfig.maxItemsAmount = lootTableConfig.items.Count;

            if (lootTableConfig.minItemsAmount > lootTableConfig.maxItemsAmount)
                lootTableConfig.minItemsAmount = lootTableConfig.maxItemsAmount;
        }

        static void Debug(params object[] arg)
        {
            string result = "";

            foreach (object obj in arg)
                if (obj != null)
                    result += obj.ToString() + " ";

            ins.Puts(result);
        }
        #endregion Methods

        #region Classes
        static class EventLauncher
        {
            static Coroutine autoEventCoroutine;
            static Coroutine delayedEventStartCorountine;

            internal static bool IsEventActive()
            {
                return ins != null && ins.eventController != null;
            }

            internal static void AutoStartEvent()
            {
                if (!ins._config.mainConfig.isAutoEvent)
                    return;

                if (autoEventCoroutine != null)
                    return;

                autoEventCoroutine = ServerMgr.Instance.StartCoroutine(AutoEventCorountine());
            }

            static IEnumerator AutoEventCorountine()
            {
                yield return CoroutineEx.waitForSeconds(5f);
                yield return CoroutineEx.waitForSeconds(UnityEngine.Random.Range(ins._config.mainConfig.minTimeBetweenEvents, ins._config.mainConfig.maxTimeBetweenEvents));
                DelayStartEvent(true);
                autoEventCoroutine = null;
            }

            internal static void DelayStartEvent(bool isAutoActivated = false, BasePlayer activator = null, string presetName = "")
            {
                if (IsEventActive() || delayedEventStartCorountine != null)
                {
                    NotifyManager.PrintError(activator, "EventActive_Exeption");
                    return;
                }

                if (autoEventCoroutine != null)
                    ServerMgr.Instance.StopCoroutine(autoEventCoroutine);

                EventConfig eventConfig = DefineEventConfig(presetName);
                if (eventConfig == null)
                {
                    NotifyManager.PrintError(activator, "ConfigurationNotFound_Exeption");
                    StopEvent(shoudSendEndMessage: false);
                    return;
                }

                delayedEventStartCorountine = ServerMgr.Instance.StartCoroutine(DelayedStartEventCorountine(eventConfig));

                if (!isAutoActivated && ins._config.notifyConfig.preStartTime > 0)
                    NotifyManager.PrintInfoMessage(activator, "SuccessfullyLaunched");
            }

            static EventConfig DefineEventConfig(string eventPresetName = "")
            {
                if (eventPresetName != "")
                    return ins._config.eventConfigs.FirstOrDefault(x => x.presetName == eventPresetName);

                HashSet<EventConfig> suitableEventConfigs = ins._config.eventConfigs.Where(x => x.chance > 0 && IsEventConfigSuitableByTime(x));

                if (suitableEventConfigs == null || suitableEventConfigs.Count == 0)
                    return null;

                float sumChance = 0;
                foreach (EventConfig eventConfig in suitableEventConfigs)
                    sumChance += eventConfig.chance;

                float random = UnityEngine.Random.Range(0, sumChance);

                foreach (EventConfig eventConfig in suitableEventConfigs)
                {
                    random -= eventConfig.chance;

                    if (random <= 0)
                        return eventConfig;
                }

                return null;
            }

            static IEnumerator DelayedStartEventCorountine(EventConfig eventConfig)
            {
                if (ins._config.notifyConfig.preStartTime > 0)
                    NotifyManager.SendMessageToAll("PreStartEvent", ins._config.prefix, eventConfig.displayName, ins._config.notifyConfig.preStartTime);

                yield return CoroutineEx.waitForSeconds(ins._config.notifyConfig.preStartTime);

                StartEvent(eventConfig);
            }

            static void StartEvent(EventConfig eventConfig)
            {
                GameObject gameObject = new GameObject();
                ins.eventController = gameObject.AddComponent<EventController>();
                ins.eventController.Init(eventConfig);

                if (ins._config.mainConfig.enableStartStopLogs)
                    NotifyManager.PrintLogMessage("EventStart_Log", eventConfig.presetName);

                Interface.CallHook("OnSputnikEventStart");
            }

            static bool IsEventConfigSuitableByTime(EventConfig eventConfig)
            {
                if (eventConfig.minTimeAfterWipe <= 0 && eventConfig.maxTimeAfterWipe <= 0)
                    return true;

                int timeScienceWipe = GetTimeScienceLastWipe();

                if (timeScienceWipe < eventConfig.minTimeAfterWipe)
                    return false;
                if (eventConfig.maxTimeAfterWipe > 0 && timeScienceWipe > eventConfig.maxTimeAfterWipe)
                    return false;

                return true;
            }

            static int GetTimeScienceLastWipe()
            {
                DateTime startTime = new DateTime(2019, 1, 1, 0, 0, 0);

                double realTime = DateTime.UtcNow.Subtract(startTime).TotalSeconds;
                double wipeTime = SaveRestore.SaveCreatedTime.Subtract(startTime).TotalSeconds;

                return Convert.ToInt32(realTime - wipeTime);
            }

            internal static void StopEvent(bool isPluginUnloading = false, bool shoudSendEndMessage = true)
            {
                if (IsEventActive())
                {
                    ins.Unsubscribes(false);
                    ins.eventController.KillController();
                    SputnikClass.KillAllSputniks();
                    Interface.CallHook("OnSputnikEventStop");

                    if (shoudSendEndMessage)
                        NotifyManager.SendMessageToAll("EndEvent", ins._config.prefix);

                    if (ins._config.mainConfig.enableStartStopLogs)
                        NotifyManager.PrintLogMessage("EventStop_Log");

                    LootManager.ClearLootData();
                    EconomyManager.OnEventEnd();
                    NpcSpawnManager.ClearData(false);
                    PveModeController.SendCooldownAndClearData();
                    EventHeli.ClearData();

                    if (!isPluginUnloading)
                        AutoStartEvent();
                }

                ZoneController.ClearData();

                if (delayedEventStartCorountine != null)
                {
                    ServerMgr.Instance.StopCoroutine(delayedEventStartCorountine);
                    delayedEventStartCorountine = null;
                }
            }
        }

        class EventController : FacepunchBehaviour
        {
            EventConfig eventConfig;
            Coroutine eventCorountine;
            int eventTime;

            internal int GetEventTime()
            {
                return eventTime;
            }

            internal void Init(EventConfig eventConfig)
            {
                this.eventConfig = eventConfig;
                eventTime = eventConfig.eventTime;
                SpawnPositionFinder.UpdateCachedPoints();

                if (TrySpawnSputniks())
                {
                    NotifyManager.SendMessageToAll("StartEvent", ins._config.prefix, eventConfig.displayName);
                    SputnikClass.StartAllSputniksFalling();
                    SpawnPositionFinder.StartCachingSpawnPoints();
                    eventCorountine = ServerMgr.Instance.StartCoroutine(EventCorountine());
                    ins.Subscribes(true);
                }
                else
                {
                    EventLauncher.StopEvent(shoudSendEndMessage: false);
                }
            }

            bool TrySpawnSputniks()
            {
                if (eventConfig.fixedSputniksPresets != null && eventConfig.fixedSputniksPresets.Count > 0)
                {
                    if (SpawnPositionFinder.GetCountSpawnPoints() < eventConfig.fixedSputniksPresets.Count)
                        return false;

                    Vector3 lastSputnikPosition = Vector3.zero;

                    foreach (string sputnikDebrisPresetName in eventConfig.fixedSputniksPresets)
                    {
                        SputnikDebrisConfig sputnikDebrisConfig = ins._config.sputnikDebrisConfigs.FirstOrDefault(x => x.presetName == sputnikDebrisPresetName);

                        if (sputnikDebrisConfig == null)
                        {
                            ins.PrintError($"Sputnik debris preset not found! (PresetName - {sputnikDebrisPresetName})");
                            continue;
                        }

                        Vector3 sputnikPosition = SpawnPositionFinder.GetSpawnPosition(sputnikDebrisConfig, lastSputnikPosition);

                        if (sputnikPosition == null || sputnikPosition == Vector3.zero)
                        {
                            ins.PrintError("The event could not be started! Increase the number of cached spawn points!");
                            return false;
                        }

                        SputnikClass.CreateSputnik(sputnikDebrisConfig, sputnikPosition);
                        lastSputnikPosition = sputnikPosition;
                    }
                }

                if (SputnikClass.IsAnySputnikAlive())
                    return true;
                else
                    return false;
            }

            IEnumerator EventCorountine()
            {
                while (eventTime > 0 && SputnikClass.IsAnySputnikAlive())
                {
                    eventTime -= 1;

                    if (ins._config.notifyConfig.timeNotifications.Contains(eventTime))
                        NotifyManager.SendMessageToAll("RemainTime", ins._config.prefix, eventConfig.displayName, eventTime);

                    yield return CoroutineEx.waitForSeconds(1);
                }

                EventLauncher.StopEvent();
            }

            internal void KillController()
            {
                if (eventCorountine != null)
                    ServerMgr.Instance.StopCoroutine(eventCorountine);

                GameObject.Destroy(this);
                ins.eventController = null;
            }
        }

        class SputnikClass : FacepunchBehaviour
        {
            internal static HashSet<SputnikClass> sputnikClasses = new HashSet<SputnikClass>();

            internal SputnikDebrisConfig sputnikDebrisConfig;
            internal PveModeController pveModeController;
            EventHeli eventHeli;
            ZoneController zoneController;
            Coroutine sputnikCorountine;
            FallingSputnikEffect fallingSputnikEffect;
            EventMapMarker eventMapMarker;
            CardReader cardReader;
            HashSet<ScientistNPC> npcs = new HashSet<ScientistNPC>();
            HashSet<LootContainer> crates = new HashSet<LootContainer>();
            HashSet<AutoTurret> turrets = new HashSet<AutoTurret>();
            HashSet<Landmine> mines = new HashSet<Landmine>();
            HashSet<BaseEntity> decorEntities = new HashSet<BaseEntity>();
            bool cardReaderOpen = true;
            int destroyTime = 0;
            int agressiveTime = 0;
            bool isEventLooted;

            internal static bool IsAnySputnikAlive()
            {
                return sputnikClasses.Any(x => x != null);
            }

            internal static SputnikClass GetSputnikByCrateUid(ulong netID)
            {
                return sputnikClasses.FirstOrDefault(x => x != null && x.crates.Any(y => y.IsExists() && y.net != null && y.net.ID.Value == netID));
            }

            internal static SputnikClass GetSputnikByCardReaderUid(ulong netID)
            {
                return sputnikClasses.FirstOrDefault(x => x != null && x.cardReader != null && x.cardReader.net.ID.Value == netID);
            }

            internal static SputnikClass GetSputnikByTurretUid(ulong netID)
            {
                return sputnikClasses.FirstOrDefault(x => x != null && x.turrets.Any(y => y.IsExists() && y.net.ID.Value == netID));
            }

            internal static SputnikClass GetSputnikByLandmineUid(ulong netID)
            {
                return sputnikClasses.FirstOrDefault(x => x != null && x.mines.Any(y => y.IsExists() && y.net.ID.Value == netID));
            }

            internal static SputnikClass GetSputnikByNpcUid(ulong netID)
            {
                return sputnikClasses.FirstOrDefault(x => x != null && x.npcs.Any(y => y.IsExists() && y.net.ID.Value == netID));
            }

            internal static void CreateSputnik(SputnikDebrisConfig sputnikDebrisConfig, Vector3 position)
            {
                GameObject gameObject = new GameObject();
                gameObject.layer = (int)Layer.Reserved1;
                gameObject.transform.position = position;
                SputnikClass sputnik = gameObject.AddComponent<SputnikClass>();
                sputnik.Init(sputnikDebrisConfig);
                sputnikClasses.Add(sputnik);
            }

            internal Vector3 GetEventPosition()
            {
                return transform.position;
            }

            internal static void StartAllSputniksFalling()
            {
                foreach (SputnikClass sputnikClass in sputnikClasses)
                    if (sputnikClass != null)
                        sputnikClass.StartFalling();
            }

            internal int GetCountOfUnlootedCrates()
            {
                return crates.Where(x => x.IsExists() && x.FirstLooterId == 0).Count;
            }

            internal int GetCountOfAliveNpc()
            {
                return npcs.Where(x => x.IsExists()).Count;
            }

            internal int GetEventTime()
            {
                return destroyTime;
            }

            internal bool IsPlayerCanDealDamage(BasePlayer player, BaseCombatEntity sputnikEntity, bool shoudSendMessages)
            {
                Vector3 playerGroundPosition = new Vector3(player.transform.position.x, 0, player.transform.position.z);
                Vector3 entityGroundPosition = new Vector3(sputnikEntity.transform.position.x, 0, sputnikEntity.transform.position.z);
                float distance = Vector3.Distance(playerGroundPosition, entityGroundPosition);
                float maxDamageDistance = sputnikEntity is PatrolHelicopter ? ins._config.mainConfig.maxHeliDamageDistance : ins._config.mainConfig.maxGroundDamageDistance;

                if (maxDamageDistance > 0 && distance > maxDamageDistance)
                {
                    if (shoudSendMessages)
                        NotifyManager.SendMessageToPlayer(player, "DamageDistance", ins._config.prefix);

                    return false;
                }

                return true;
            }

            internal bool IsPveModeBlockAction(BasePlayer player)
            {
                return pveModeController != null && pveModeController.IsPveModeBlockAction(player);
            }

            internal bool IsAgressive()
            {
                return ins._config.agressiveConfig.agressiveSecurityMode || agressiveTime > 0;
            }

            internal void OnSputnikAttacked()
            {
                if (ins._config.agressiveConfig.agressiveSecurityMode)
                    return;

                if (!ins._config.agressiveConfig.makeAllSputniksAgressive)
                    UpdateAgressive();
                else
                    foreach (SputnikClass sputnikClass in sputnikClasses)
                        if (sputnikClass != null)
                            sputnikClass.UpdateAgressive();
            }

            void UpdateAgressive()
            {
                if (agressiveTime <= 0)
                    MakeSputnikAgressive();

                agressiveTime = ins._config.agressiveConfig.agressiveTime;
            }

            internal bool IsCardReaderExistAndClosed()
            {
                return cardReader.IsExists() && !cardReaderOpen;
            }

            internal void CheckEventPassing()
            {
                if (isEventLooted)
                    return;

                if (GetCountOfUnlootedCrates() == 0)
                {
                    isEventLooted = true;

                    if (ins._config.mainConfig.destroyAfterLootingTime < destroyTime)
                        destroyTime = ins._config.mainConfig.destroyAfterLootingTime;
                }
            }

            internal void OnCardSwipe(Keycard keycard, BasePlayer player)
            {
                Item keyCardItem = keycard.GetCachedItem();

                if (keyCardItem == null)
                    return;

                if (!IsAgressive())
                {
                    OnSputnikAttacked();
                    return;
                }

                if (keyCardItem.info.shortname == ins._config.customCardConfig.shortName && keyCardItem.skin == ins._config.customCardConfig.skinID)
                {
                    Effect.server.Run(cardReader.accessGrantedEffect.resourcePath, cardReader.audioPosition.position, Vector3.up);
                    keyCardItem.LoseCondition(ins._config.customCardConfig.helthLossScale);
                    cardReaderOpen = true;
                }
                else
                {
                    NotifyManager.SendMessageToPlayer(player, "NeedUseCard", ins._config.prefix);
                    Effect.server.Run(cardReader.accessDeniedEffect.resourcePath, cardReader.audioPosition.position, Vector3.up);
                    cardReader.CancelInvoke(cardReader.GrantCard);
                }
            }

            internal void Init(SputnikDebrisConfig sputnikDebrisConfig)
            {
                this.sputnikDebrisConfig = sputnikDebrisConfig;
            }

            void StartFalling()
            {
                if (transform.position.y >= ins._config.fallindConfig.maxFallHeight)
                    OnSputnikFell();
                else
                    fallingSputnikEffect = FallingSputnikEffect.CreateFallingEffect(this);
            }

            internal void OnSputnikFell()
            {
                if (sputnikCorountine != null)
                    return;

                sputnikCorountine = ServerMgr.Instance.StartCoroutine(SputnikCorountine());
            }

            IEnumerator SputnikCorountine()
            {
                for (int i = 0; i < ins._config.fallindConfig.countEffects && gameObject != null && gameObject.transform != null; i++)
                {
                    CreateCrushEffect();
                    yield return CoroutineEx.waitForSeconds(0.1f);
                }

                BuildSputnik();

                for (int i = 0; i < ins._config.fallindConfig.countEffects && gameObject != null && gameObject.transform != null; i++)
                {
                    CreateCrushEffect();
                    yield return CoroutineEx.waitForSeconds(0.1f);
                }

                if (ins._config.agressiveConfig.agressiveSecurityMode)
                    MakeSputnikAgressive();
                else
                    MakeSputnikNoAgressive();

                NotifyManager.SendMessageToAll("Crash", ins._config.prefix, MapHelper.PositionToString(gameObject.transform.position));
                destroyTime = ins.eventController.GetEventTime();

                while (destroyTime > 0)
                {
                    destroyTime -= 1;

                    if (agressiveTime > 0)
                    {
                        agressiveTime--;

                        if (agressiveTime <= 0)
                            MakeSputnikNoAgressive();
                    }

                    if (destroyTime % 10 == 0)
                        CheckEventPassing();

                    yield return CoroutineEx.waitForSeconds(1);
                }

                KillSputnik();
            }

            void CreateCrushEffect()
            {
                Effect.server.Run("assets/bundled/prefabs/fx/explosions/explosion_03.prefab", gameObject.transform.position + new Vector3(UnityEngine.Random.Range(-7.5f, 7.5f), 0, UnityEngine.Random.Range(-7.5f, 7.5f)));
                Effect.server.Run("assets/bundled/prefabs/fx/explosions/explosion_02.prefab", gameObject.transform.position + new Vector3(UnityEngine.Random.Range(-7.5f, 7.5f), 0, UnityEngine.Random.Range(-7.5f, 7.5f)));
            }

            void MakeSputnikAgressive()
            {
                if (ins._config.agressiveConfig.turretAgressiveMode)
                    return;

                foreach (AutoTurret autoTurret in turrets)
                    if (autoTurret.IsExists())
                        autoTurret.SetPeacekeepermode(false);
            }

            void MakeSputnikNoAgressive()
            {
                if (ins._config.agressiveConfig.turretAgressiveMode)
                    return;

                foreach (AutoTurret autoTurret in turrets)
                    if (autoTurret.IsExists())
                        autoTurret.SetPeacekeepermode(true);
            }

            void BuildSputnik()
            {
                CreateGrounSputnik();
                CreateCardReader();
                CreateNPCs();
                CreateCrates();
                CreateMines();
                CreateHeli();
                CreateTurrets();
                IQDroneManager.SpawnDrones(transform.position, sputnikDebrisConfig.iqDronePreset, (int)(sputnikDebrisConfig.zoneConfig.radius / 1.5f));

                pveModeController = CreatePveModeController();
                zoneController = ZoneController.CreateZone(this, pveModeController, sputnikDebrisConfig.zoneConfig.isPVPZone);
                eventMapMarker = EventMapMarker.CreateMarker(this, pveModeController);
            }

            PveModeController CreatePveModeController()
            {
                if (!PveModeController.IsPveModeReady())
                    return null;

                HashSet<ulong> cratesUIDs = new HashSet<ulong>();

                foreach (LootContainer lootContainer in crates)
                    if (lootContainer.IsExists() && lootContainer.net != null)
                        cratesUIDs.Add(lootContainer.net.ID.Value);

                HashSet<ulong> npcUIDs = new HashSet<ulong>();

                foreach (ScientistNPC scientistNPC in npcs)
                    if (scientistNPC.IsExists() && scientistNPC.net != null)
                        npcUIDs.Add(scientistNPC.net.ID.Value);

                HashSet<ulong> turretsUIDs = new HashSet<ulong>();

                foreach (AutoTurret autoTurret in turrets)
                    if (autoTurret.IsExists() && autoTurret.net != null)
                        turretsUIDs.Add(autoTurret.net.ID.Value);

                ulong heliNetId = eventHeli != null ? eventHeli.GetHeliNetId() : 0;

                return PveModeController.CreatePveModeController(this, npcUIDs, cratesUIDs, turretsUIDs, heliNetId);
            }

            void CreateGrounSputnik()
            {
                HashSet<EntData> entDatas = DataFileManager.GetSputnikData(sputnikDebrisConfig.locationPreset);

                if (entDatas == null)
                {
                    ins.PrintError($"Sputnik debris preset not found! ({sputnikDebrisConfig.locationPreset})");
                    EventLauncher.StopEvent(shoudSendEndMessage: false);
                    return;
                }

                foreach (EntData entData in entDatas)
                {
                    Vector3 localPosition = entData.pos.ToVector3();
                    Vector3 localRotation = entData.rot.ToVector3();

                    Vector3 globalPosition = PositionDefiner.GetGlobalPosition(transform, localPosition);
                    Quaternion globalRotation = PositionDefiner.GetGlobalRotation(transform, localRotation);

                    BaseEntity decorEntity = BuildManager.SpawnDecorEntity(entData.prefab, globalPosition, globalRotation, 0);
                    decorEntity.SetFlag(BaseEntity.Flags.Busy, true);
                    decorEntities.Add(decorEntity);
                }
            }

            void CreateNPCs()
            {
                foreach (var pair in sputnikDebrisConfig.NPCs)
                {
                    foreach (string positionString in pair.Value)
                    {
                        Vector3 globalPosition = PositionDefiner.GetGlobalPosition(gameObject.transform, positionString.ToVector3());
                        Vector3 spawnPosition = PositionDefiner.GetGroundPositionInPoint(globalPosition);

                        if (spawnPosition == Vector3.zero)
                            continue;

                        NavMeshHit navMeshHit;

                        if (PositionDefiner.GetNavmeshInPoint(spawnPosition, 2, out navMeshHit))
                            spawnPosition = navMeshHit.position;

                        ScientistNPC scientistNPC = NpcSpawnManager.SpawnScientistNpc(pair.Key, spawnPosition, 1, false);
                        npcs.Add(scientistNPC);
                    }
                }
            }

            void CreateCrates()
            {
                foreach (var pair in sputnikDebrisConfig.groundCrates)
                    foreach (LocationConfig locationConfig in pair.Value)
                        CreateCrate(pair.Key, PositionDefiner.GetGroundPositionInPoint(PositionDefiner.GetGlobalPosition(gameObject.transform, locationConfig.position.ToVector3())), PositionDefiner.GetGlobalRotation(gameObject.transform, locationConfig.rotation.ToVector3()));

                foreach (var pair in sputnikDebrisConfig.сrates)
                    foreach (LocationConfig locationConfig in pair.Value)
                        CreateCrate(pair.Key, PositionDefiner.GetGlobalPosition(gameObject.transform, locationConfig.position.ToVector3()), PositionDefiner.GetGlobalRotation(gameObject.transform, locationConfig.rotation.ToVector3()));
            }

            void CreateCrate(string cratePresetName, Vector3 position, Quaternion rotation)
            {
                CrateConfig crateConfig = ins._config.crateConfigs.FirstOrDefault(x => x.presetName == cratePresetName);
                if (crateConfig == null)
                {
                    ins.PrintError($"Crate configuration not found! (PresetName - {cratePresetName})");
                    return;
                }

                LootContainer lootContainer = BuildManager.SpawnStaticEntity(crateConfig.prefab, position, rotation) as LootContainer;

                if (lootContainer == null)
                {
                    ins.PrintError($"Failed to create a crate! (PresetName - {cratePresetName})");
                    return;
                }

                LootManager.UpdateLootContainer(lootContainer, crateConfig);
                crates.Add(lootContainer);
            }

            void CreateCardReader()
            {
                if (!sputnikDebrisConfig.enableCardReader)
                    return;

                cardReader = BuildManager.SpawnStaticEntity("assets/prefabs/io/electric/switches/cardreader.prefab", PositionDefiner.GetGlobalPosition(gameObject.transform, sputnikDebrisConfig.cardRaderLocation.position.ToVector3()), PositionDefiner.GetGlobalRotation(gameObject.transform, sputnikDebrisConfig.cardRaderLocation.rotation.ToVector3())) as CardReader;
                cardReader.SetFlag(cardReader.AccessLevel1, false);
                cardReader.SetFlag(cardReader.AccessLevel2, false);
                cardReader.SetFlag(cardReader.AccessLevel3, false);

                if (ins._config.customCardConfig.shortName.Contains("red"))
                    cardReader.SetFlag(cardReader.AccessLevel3, true);
                else if (ins._config.customCardConfig.shortName.Contains("blue"))
                    cardReader.SetFlag(cardReader.AccessLevel2, true);
                else
                    cardReader.SetFlag(cardReader.AccessLevel1, true);

                cardReaderOpen = false;
                cardReader.UpdateFromInput(100, 0);
            }

            void CreateMines()
            {
                foreach (string positionString in sputnikDebrisConfig.mines)
                {
                    Vector3 globalPosition = PositionDefiner.GetGlobalPosition(gameObject.transform, positionString.ToVector3());
                    Vector3 spawnPosition = PositionDefiner.GetGroundPositionInPoint(globalPosition);

                    if (spawnPosition == Vector3.zero)
                        continue;

                    Landmine landmine = BuildManager.SpawnStaticEntity("assets/prefabs/deployable/landmine/landmine.prefab", spawnPosition, Quaternion.identity) as Landmine;
                    mines.Add(landmine);
                }
            }

            void CreateHeli()
            {
                if (sputnikDebrisConfig.heliPresetName == "")
                    return;

                HeliConfig heliConfig = ins._config.heliConfigs.FirstOrDefault(x => x.presetName == sputnikDebrisConfig.heliPresetName);

                if (heliConfig == null)
                {
                    ins.PrintError("Heli configuration not found!");
                    return;
                }

                eventHeli = EventHeli.SpawnHeli(heliConfig, this);
            }

            void CreateTurrets()
            {
                foreach (var turretPair in sputnikDebrisConfig.turrets)
                {
                    TurretConfig turretConfig = ins._config.turretConfigs.FirstOrDefault(x => x.presetName == turretPair.Key);

                    if (turretConfig == null)
                    {
                        ins.PrintError("Turret configuration not found!");
                        continue;
                    }

                    foreach (LocationConfig locationConfig in turretPair.Value)
                        CreateTurret(turretConfig, locationConfig);
                }
            }

            void CreateTurret(TurretConfig turretConfig, LocationConfig locationConfig)
            {
                Vector3 globalPosition = PositionDefiner.GetGlobalPosition(gameObject.transform, locationConfig.position.ToVector3());
                Vector3 spawnPosition = turretConfig.autoHeight ? PositionDefiner.GetGroundPositionInPoint(globalPosition) : globalPosition;

                if (spawnPosition == Vector3.zero)
                    return;

                AutoTurret autoTurret = BuildManager.SpawnStaticEntity("assets/prefabs/npc/autoturret/autoturret_deployed.prefab", spawnPosition, PositionDefiner.GetGlobalRotation(gameObject.transform, locationConfig.rotation.ToVector3()), 3358068268) as AutoTurret;
                ContainerIOEntity containerIO = autoTurret.GetComponent<ContainerIOEntity>();
                containerIO.inventory.Insert(ItemManager.CreateByName(turretConfig.shortNameWeapon));
                containerIO.inventory.Insert(ItemManager.CreateByName(turretConfig.shortNameAmmo, turretConfig.countAmmo));
                containerIO.SendNetworkUpdate();
                autoTurret.InitializeHealth(turretConfig.hp, turretConfig.hp);
                autoTurret.UpdateFromInput(10, 0);
                autoTurret.isLootable = false;
                autoTurret.dropFloats = false;
                autoTurret.dropsLoot = false;

                turrets.Add(autoTurret);

                if (turretConfig.targetLossRange != 0)
                    autoTurret.sightRange = turretConfig.targetLossRange;

                if (turretConfig.targetDetectionRange != 0 && autoTurret.targetTrigger != null)
                {
                    SphereCollider sphereCollider = autoTurret.targetTrigger.GetComponent<SphereCollider>();

                    if (sphereCollider != null)
                        sphereCollider.radius = turretConfig.targetDetectionRange;
                }
            }

            internal static void KillAllSputniks()
            {
                foreach (SputnikClass sputnikClass in sputnikClasses)
                    if (sputnikClass != null)
                        sputnikClass.KillSputnik();

                sputnikClasses.Clear();
            }

            void KillSputnik()
            {
                if (sputnikCorountine != null)
                    ServerMgr.Instance.StopCoroutine(sputnikCorountine);

                if (zoneController != null)
                    zoneController.DeleteZone();

                if (eventMapMarker != null)
                    eventMapMarker.Delete();

                if (pveModeController != null)
                    pveModeController.DeletePveModeZone();

                KillSputnikEntities();

                if (fallingSputnikEffect != null)
                    fallingSputnikEffect.KillFallingSputnik();

                IQDroneManager.KillDrones(transform.position);
                GameObject.Destroy(this);
            }

            void KillSputnikEntities()
            {
                if (cardReader.IsExists())
                    cardReader.Kill();

                if (eventHeli != null)
                    eventHeli.Kill();

                foreach (BaseEntity entity in decorEntities)
                    if (entity.IsExists())
                        entity.Kill();

                foreach (ScientistNPC entity in npcs)
                    if (entity.IsExists())
                        entity.Kill();

                foreach (LootContainer entity in crates)
                    if (entity.IsExists())
                        entity.Kill();

                foreach (Landmine landmine in mines)
                    if (landmine.IsExists())
                        landmine.Kill();

                foreach (AutoTurret autoTurret in turrets)
                {
                    if (autoTurret.IsExists())
                    {
                        AutoTurret.interferenceUpdateList.Remove(autoTurret);
                        autoTurret.Kill();
                    }
                }
            }
        }

        class FallingSputnikEffect : FacepunchBehaviour
        {
            SputnikClass sputnikClass;
            BaseEntity mainFireballEntity;
            Vector3 fallDirection;

            internal static FallingSputnikEffect CreateFallingEffect(SputnikClass sputnikClass)
            {
                Vector3 startPosition = GetStartFallPosition(sputnikClass.transform.position);

                BaseEntity fireBallEntity = BuildManager.SpawnDecorEntity("assets/bundled/prefabs/oilfireballsmall.prefab", startPosition, Quaternion.identity);
                FallingSputnikEffect fallingSputnikEffect = fireBallEntity.gameObject.AddComponent<FallingSputnikEffect>();
                fallingSputnikEffect.Init(fireBallEntity, sputnikClass);
                return fallingSputnikEffect;
            }

            static Vector3 GetStartFallPosition(Vector3 sputnikPosition)
            {
                int counter = 0;
                Vector3 startPosition = Vector3.zero;

                while (counter < 20)
                {
                    float angle = UnityEngine.Random.Range(0, 360);
                    float radius = UnityEngine.Random.Range(ins._config.fallindConfig.minFallOffset, ins._config.fallindConfig.maxFallOffset);
                    float radian = 2f * Mathf.PI * angle / 360;
                    float x = sputnikPosition.x + radius * Mathf.Cos(radian);
                    float z = sputnikPosition.z + radius * Mathf.Sin(radian);
                    float y = UnityEngine.Random.Range(ins._config.fallindConfig.minFallHeight, ins._config.fallindConfig.maxFallHeight);
                    startPosition = new Vector3(x, y, z);
                    Vector3 fallDirection = (sputnikPosition - startPosition).normalized;
                    counter++;

                    if (!Physics.Raycast(startPosition, fallDirection, Vector3.Distance(startPosition, sputnikPosition) - 5, 1 << 16 | 1 << 21))
                        break;
                }

                return startPosition;
            }

            void Init(BaseEntity mainFireballEntity, SputnikClass sputnikClass)
            {
                this.mainFireballEntity = mainFireballEntity;
                this.sputnikClass = sputnikClass;

                fallDirection = (sputnikClass.transform.position - mainFireballEntity.transform.position).normalized;
                SpawnSubFireballs();
            }

            void SpawnSubFireballs()
            {
                float radius = 1.5f;

                for (int angle = 0; angle < 360; angle += 45)
                {
                    float radian = 2f * Mathf.PI * angle / 360;
                    float x = radius * Mathf.Cos(radian);
                    float y = 1;
                    float z = radius * Mathf.Sin(radian);
                    Vector3 localPosition = new Vector3(x, y, z);
                    BuildManager.SpawnChildEntity(mainFireballEntity, "assets/bundled/prefabs/oilfireballsmall.prefab", localPosition, Vector3.zero, 0);
                }
            }

            void FixedUpdate()
            {
                mainFireballEntity.transform.position = mainFireballEntity.transform.position += fallDirection * ins._config.fallindConfig.fallingSpeedScale;

                if (mainFireballEntity.transform.position.y - sputnikClass.transform.position.y < 1)
                {
                    sputnikClass.OnSputnikFell();
                    KillFallingSputnik();
                }
            }

            void OnDestroy()
            {
                if (EventLauncher.IsEventActive())
                    sputnikClass.OnSputnikFell();
            }

            internal void KillFallingSputnik()
            {
                mainFireballEntity.Kill();
            }
        }

        class EventMapMarker : FacepunchBehaviour
        {
            SputnikClass sputnikClass;
            PveModeController pveModeController;
            MapMarkerGenericRadius radiusMarker;
            VendingMachineMapMarker vendingMarker;
            Coroutine updateCounter;

            internal static EventMapMarker CreateMarker(SputnikClass sputnikClass, PveModeController pveModeController)
            {
                if (!sputnikClass.sputnikDebrisConfig.markerConfig.enable)
                    return null;

                GameObject gameObject = new GameObject();
                gameObject.layer = (int)Rust.Layer.Reserved1;
                EventMapMarker eventMapMarker = gameObject.AddComponent<EventMapMarker>();
                eventMapMarker.Init(sputnikClass, pveModeController);
                return eventMapMarker;
            }

            void Init(SputnikClass sputnikClass, PveModeController pveModeController)
            {
                this.sputnikClass = sputnikClass;
                this.pveModeController = pveModeController;

                Vector3 eventPosition = sputnikClass.transform.position;
                CreateRadiusMarker(eventPosition);
                CreateVendingMarker(eventPosition);
                updateCounter = ServerMgr.Instance.StartCoroutine(MarkerUpdateCounter());
            }

            void CreateRadiusMarker(Vector3 position)
            {
                if (!sputnikClass.sputnikDebrisConfig.markerConfig.isRingMarker)
                    return;

                radiusMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", position) as MapMarkerGenericRadius;
                radiusMarker.enableSaving = false;
                radiusMarker.Spawn();
                radiusMarker.radius = sputnikClass.sputnikDebrisConfig.markerConfig.radius;
                radiusMarker.alpha = sputnikClass.sputnikDebrisConfig.markerConfig.alpha;
                radiusMarker.color1 = new Color(sputnikClass.sputnikDebrisConfig.markerConfig.color1.r, sputnikClass.sputnikDebrisConfig.markerConfig.color1.g, sputnikClass.sputnikDebrisConfig.markerConfig.color1.b);
                radiusMarker.color2 = new Color(sputnikClass.sputnikDebrisConfig.markerConfig.color2.r, sputnikClass.sputnikDebrisConfig.markerConfig.color2.g, sputnikClass.sputnikDebrisConfig.markerConfig.color2.b);
            }

            void CreateVendingMarker(Vector3 position)
            {
                if (!sputnikClass.sputnikDebrisConfig.markerConfig.isShopMarker)
                    return;

                vendingMarker = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", position) as VendingMachineMapMarker;
                vendingMarker.Spawn();
                vendingMarker.markerShopName = $"{sputnikClass.sputnikDebrisConfig.markerConfig.displayName} ({NotifyManager.GetTimeMessage(null, ins.eventController.GetEventTime())})";
            }

            IEnumerator MarkerUpdateCounter()
            {
                while (EventLauncher.IsEventActive())
                {
                    Vector3 position = sputnikClass.transform.position;
                    UpdateVendingMarker(position);
                    UpdateRadiusMarker(position);
                    yield return CoroutineEx.waitForSeconds(5f);
                }
            }

            void UpdateRadiusMarker(Vector3 position)
            {
                if (!radiusMarker.IsExists())
                    return;

                radiusMarker.transform.position = position;
                radiusMarker.SendUpdate();
                radiusMarker.SendNetworkUpdate();
            }

            void UpdateVendingMarker(Vector3 position)
            {
                if (!vendingMarker.IsExists())
                    return;

                vendingMarker.transform.position = position;
                BasePlayer pveModeEventOwner = pveModeController != null ? pveModeController.owner : null;
                string displayEventOwnerName = ins._config.supportedPluginsConfig.pveMode.showEventOwnerNameOnMap && pveModeEventOwner != null ? GetMessage("Marker_EventOwner", null, pveModeEventOwner.displayName) : "";
                vendingMarker.markerShopName = $"{sputnikClass.sputnikDebrisConfig.markerConfig.displayName} ({NotifyManager.GetTimeMessage(null, ins.eventController.GetEventTime())}) {displayEventOwnerName}";
                vendingMarker.SetFlag(BaseEntity.Flags.Busy, pveModeEventOwner == null);
                vendingMarker.SendNetworkUpdate();
            }

            internal void Delete()
            {
                if (radiusMarker.IsExists())
                    radiusMarker.Kill();

                if (vendingMarker.IsExists())
                    vendingMarker.Kill();

                if (updateCounter != null)
                    ServerMgr.Instance.StopCoroutine(updateCounter);

                Destroy(this.gameObject);
            }
        }

        class ZoneController : FacepunchBehaviour
        {
            static HashSet<ZoneController> zoneControllers = new HashSet<ZoneController>();

            SputnikClass sputnikClass;
            bool isPVPZone;
            SphereCollider sphereCollider;
            Coroutine zoneUpdateCorountine;
            HashSet<BaseEntity> spheres = new HashSet<BaseEntity>();
            HashSet<BasePlayer> playersInZone = new HashSet<BasePlayer>();

            internal static ZoneController GetZoneControllerByPlayerUserID(ulong userID)
            {
                return zoneControllers.FirstOrDefault(x => x != null && x.playersInZone.Any(y => y != null && y.userID == userID));
            }

            internal bool IsPlayerInZone(ulong userID)
            {
                return playersInZone.Any(x => x != null && x.userID == userID);
            }

            internal bool IsPvpZone()
            {
                return isPVPZone;
            }

            internal void OnPlayerLeaveZone(BasePlayer player)
            {
                playersInZone.Remove(player);
                GuiManager.DestroyGui(player);
                Interface.CallHook($"OnPlayerExit{ins.Name}", player);

                if (sputnikClass.sputnikDebrisConfig.zoneConfig.isPVPZone)
                {
                    if (ins.plugins.Exists("DynamicPVP") && (bool)ins.DynamicPVP.Call("IsPlayerInPVPDelay", (ulong)player.userID))
                        return;

                    NotifyManager.SendMessageToPlayer(player, "ExitPVP", ins._config.prefix);
                }
            }

            internal static ZoneController CreateZone(SputnikClass sputnikClass, PveModeController pveModeManager, bool isPVPZone)
            {
                GameObject gameObject = new GameObject();
                gameObject.transform.position = sputnikClass.transform.position;
                gameObject.layer = (int)Rust.Layer.Reserved1;
                ZoneController zoneController = gameObject.AddComponent<ZoneController>();
                zoneController.Init(sputnikClass, pveModeManager, isPVPZone);
                zoneControllers.Add(zoneController);
                return zoneController;
            }

            void Init(SputnikClass sputnikClass, PveModeController pveModeController, bool isPVPZone)
            {
                this.sputnikClass = sputnikClass;
                this.isPVPZone = isPVPZone;

                CreateTriggerSphere();
                CreateSpheres();

                zoneUpdateCorountine = ServerMgr.Instance.StartCoroutine(ZoneUpdateCorountine());
            }

            void CreateTriggerSphere()
            {
                sphereCollider = gameObject.AddComponent<SphereCollider>();
                sphereCollider.isTrigger = true;
                sphereCollider.radius = sputnikClass.sputnikDebrisConfig.zoneConfig.radius;
            }

            void CreateSpheres()
            {
                if (sputnikClass.sputnikDebrisConfig.zoneConfig.isDome)
                    for (int i = 0; i < sputnikClass.sputnikDebrisConfig.zoneConfig.darkening; i++)
                        CreateSphere("assets/prefabs/visualization/sphere.prefab");

                if (sputnikClass.sputnikDebrisConfig.zoneConfig.isColoredBorder)
                {
                    string spherePrefab = sputnikClass.sputnikDebrisConfig.zoneConfig.borderColor == 0 ? "assets/bundled/prefabs/modding/events/twitch/br_sphere.prefab" : sputnikClass.sputnikDebrisConfig.zoneConfig.borderColor == 1 ? "assets/bundled/prefabs/modding/events/twitch/br_sphere_green.prefab" :
                         sputnikClass.sputnikDebrisConfig.zoneConfig.borderColor == 2 ? "assets/bundled/prefabs/modding/events/twitch/br_sphere_purple.prefab" : "assets/bundled/prefabs/modding/events/twitch/br_sphere_red.prefab";

                    for (int i = 0; i < sputnikClass.sputnikDebrisConfig.zoneConfig.brightness; i++)
                        CreateSphere(spherePrefab);
                }
            }

            void CreateSphere(string prefabName)
            {
                BaseEntity sphere = GameManager.server.CreateEntity(prefabName, gameObject.transform.position);
                SphereEntity entity = sphere.GetComponent<SphereEntity>();
                entity.currentRadius = sputnikClass.sputnikDebrisConfig.zoneConfig.radius * 2;
                entity.lerpSpeed = 0f;
                sphere.enableSaving = false;
                sphere.Spawn();
                spheres.Add(sphere);
            }

            void OnTriggerEnter(Collider other)
            {
                BasePlayer player = other.GetComponentInParent<BasePlayer>();

                if (player.IsRealPlayer())
                {
                    playersInZone.Add(player);

                    Interface.CallHook($"OnPlayerEnter{ins.Name}", player);

                    if (sputnikClass.sputnikDebrisConfig.zoneConfig.isPVPZone)
                        NotifyManager.SendMessageToPlayer(player, "EnterPVP", ins._config.prefix);

                    if (ins._config.guiConfig.isEnable)
                        GuiManager.CreateGui(player, NotifyManager.GetTimeMessage(player.UserIDString, sputnikClass.GetEventTime()), sputnikClass.GetCountOfUnlootedCrates().ToString(), sputnikClass.GetCountOfAliveNpc().ToString());
                }
            }

            void OnTriggerExit(Collider other)
            {
                BasePlayer player = other.GetComponentInParent<BasePlayer>();

                if (player.IsRealPlayer())
                    OnPlayerLeaveZone(player);
            }

            IEnumerator ZoneUpdateCorountine()
            {
                while (sputnikClass != null)
                {
                    int countOfCrates = sputnikClass.GetCountOfUnlootedCrates();
                    int countOfGuardNpc = sputnikClass.GetCountOfAliveNpc();

                    if (ins._config.guiConfig.isEnable)
                        foreach (BasePlayer player in playersInZone)
                            if (player != null)
                                GuiManager.CreateGui(player, NotifyManager.GetTimeMessage(player.UserIDString, sputnikClass.GetEventTime()), countOfCrates.ToString(), countOfGuardNpc.ToString());

                    yield return CoroutineEx.waitForSeconds(1f);
                }
            }

            internal static void ClearData()
            {
                foreach (ZoneController zoneController in zoneControllers)
                    if (zoneController != null)
                        zoneController.DeleteZone();

                zoneControllers.Clear();
            }

            internal void DeleteZone()
            {
                if (zoneUpdateCorountine != null)
                    ServerMgr.Instance.StopCoroutine(zoneUpdateCorountine);

                foreach (BaseEntity sphere in spheres)
                    if (sphere.IsExists())
                        sphere.Kill();

                foreach (BasePlayer player in playersInZone)
                    if (player != null)
                        GuiManager.DestroyGui(player);

                UnityEngine.GameObject.Destroy(gameObject);
            }
        }

        class PveModeController
        {
            static HashSet<PveModeController> pveModeControllers = new HashSet<PveModeController>();
            static HashSet<ulong> owners = new HashSet<ulong>();
            SputnikClass sputnikClass;
            internal BasePlayer owner;
            string zoneName;

            internal static bool IsPveModeReady()
            {
                return ins._config.supportedPluginsConfig.pveMode.enable && ins.plugins.Exists("PveMode");
            }

            internal static PveModeController GetPveControllerByZoneName(string zoneName)
            {
                return pveModeControllers.FirstOrDefault(x => x != null && x.zoneName == zoneName);
            }

            internal static PveModeController CreatePveModeController(SputnikClass sputnikClass, HashSet<ulong> npcs, HashSet<ulong> crates, HashSet<ulong> turrets, ulong heliNetId)
            {
                PveModeController pveModeController = new PveModeController();
                pveModeController.Init(sputnikClass, npcs, crates, turrets, heliNetId);
                pveModeControllers.Add(pveModeController);
                return pveModeController;
            }

            internal void Init(SputnikClass sputnikClass, HashSet<ulong> npcs, HashSet<ulong> crates, HashSet<ulong> turrets, ulong heliNetId)
            {
                this.sputnikClass = sputnikClass;
                zoneName = ins.Name + "_" + sputnikClass.transform.position;

                Dictionary<string, object> config = GetPveModeConfig();
                HashSet<ulong> bradleys = new HashSet<ulong>();
                HashSet<ulong> helicopters = heliNetId != 0 ? new HashSet<ulong> { heliNetId } : new HashSet<ulong>();

                ins.PveMode.Call("EventAddPveMode", zoneName, config, sputnikClass.transform.position, sputnikClass.sputnikDebrisConfig.zoneConfig.radius, crates, npcs, bradleys, helicopters, turrets, new HashSet<ulong>(), null);
            }

            internal BasePlayer GetEventOwner()
            {
                return owner;
            }

            Dictionary<string, object> GetPveModeConfig()
            {
                return new Dictionary<string, object>
                {
                    ["Damage"] = ins._config.supportedPluginsConfig.pveMode.damage,
                    ["ScaleDamage"] = ins._config.supportedPluginsConfig.pveMode.scaleDamage,
                    ["LootCrate"] = ins._config.supportedPluginsConfig.pveMode.lootCrate,
                    ["HackCrate"] = ins._config.supportedPluginsConfig.pveMode.hackCrate,
                    ["LootNpc"] = ins._config.supportedPluginsConfig.pveMode.lootNpc,
                    ["DamageNpc"] = ins._config.supportedPluginsConfig.pveMode.damageNpc,
                    ["DamageTank"] = false,
                    ["DamageHelicopter"] = ins._config.supportedPluginsConfig.pveMode.damageHeli,
                    ["DamageTurret"] = ins._config.supportedPluginsConfig.pveMode.damageTurret,
                    ["TargetNpc"] = ins._config.supportedPluginsConfig.pveMode.targetNpc,
                    ["TargetTank"] = false,
                    ["TargetHelicopter"] = ins._config.supportedPluginsConfig.pveMode.targetHeli,
                    ["TargetTurret"] = ins._config.supportedPluginsConfig.pveMode.targetTurret,
                    ["CanEnter"] = ins._config.supportedPluginsConfig.pveMode.canEnter,
                    ["CanEnterCooldownPlayer"] = ins._config.supportedPluginsConfig.pveMode.canEnterCooldownPlayer,
                    ["TimeExitOwner"] = ins._config.supportedPluginsConfig.pveMode.timeExitOwner,
                    ["AlertTime"] = ins._config.supportedPluginsConfig.pveMode.alertTime,
                    ["RestoreUponDeath"] = false,
                    ["CooldownOwner"] = ins._config.supportedPluginsConfig.pveMode.cooldownOwner,
                    ["Darkening"] = sputnikClass.sputnikDebrisConfig.zoneConfig.isDome ? sputnikClass.sputnikDebrisConfig.zoneConfig.darkening : 0
                };
            }

            internal void OnNewOwnerSet(BasePlayer player)
            {
                owner = player;
            }

            internal void OnOwnerDeleted()
            {
                owner = null;
            }

            internal bool IsPveModeBlockAction(BasePlayer player)
            {
                if (IsPveModeReady())
                    return ins.PveMode.Call("CanActionEvent", ins.Name, player) != null;

                return false;
            }

            internal void DeletePveModeZone()
            {
                if (!IsPveModeReady())
                    return;

                HashSet<ulong> newOwners = (HashSet<ulong>)ins.PveMode.Call("GetEventOwners", zoneName);

                if (newOwners != null)
                    foreach (ulong ownerId in newOwners)
                        if (!owners.Contains(ownerId))
                            owners.Add(ownerId);

                ins.PveMode.Call("EventRemovePveMode", zoneName, false);
            }

            internal static void SendCooldownAndClearData()
            {
                if (owners.Count > 0)
                    ins.PveMode.Call("EventAddCooldown", ins.Name, owners, ins._config.supportedPluginsConfig.pveMode.cooldownOwner);

                owners.Clear();
                pveModeControllers.Clear();
            }
        }

        class EventHeli : FacepunchBehaviour
        {
            static HashSet<EventHeli> eventHelies = new HashSet<EventHeli>();

            internal SputnikClass sputnikClass;
            internal HeliConfig heliConfig;
            PatrolHelicopter patrolHelicopter;
            Vector3 patrolPosition;
            int ounsideTime;
            bool isFollowing;
            internal ulong lastAttackedPlayer;

            internal static EventHeli SpawnHeli(HeliConfig heliConfig, SputnikClass sputnikClass)
            {
                Vector3 position = sputnikClass.GetEventPosition() + Vector3.up * heliConfig.height;

                PatrolHelicopter patrolHelicopter = BuildManager.SpawnRegularEntity("assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab", position, Quaternion.identity) as PatrolHelicopter;
                patrolHelicopter.transform.position = position;
                EventHeli eventHeli = patrolHelicopter.gameObject.AddComponent<EventHeli>();
                eventHeli.Init(heliConfig, patrolHelicopter, sputnikClass);
                eventHelies.Add(eventHeli);
                return eventHeli;
            }

            internal static EventHeli GetEventHeliByNetId(ulong netId)
            {
                return eventHelies.FirstOrDefault(x => x != null && x.patrolHelicopter != null && x.patrolHelicopter.net != null && x.patrolHelicopter.net.ID.Value == netId);
            }

            internal static EventHeli GetClosestHeli(Vector3 position)
            {
                HashSet<EventHeli> aliveHelies = eventHelies.Where(x => x != null);

                if (aliveHelies == null || aliveHelies.Count == 0)
                    return null;

                return aliveHelies.Min(x => Vector3.Distance(position, x.transform.position));
            }

            void Init(HeliConfig heliConfig, PatrolHelicopter patrolHelicopter, SputnikClass sputnikClass)
            {
                this.heliConfig = heliConfig;
                this.patrolHelicopter = patrolHelicopter;
                this.sputnikClass = sputnikClass;
                UpdateHelicopter();
                StartPatrol();
                patrolHelicopter.InvokeRepeating(UpdatePosition, 1, 1);
            }

            void UpdateHelicopter()
            {
                patrolHelicopter.startHealth = heliConfig.hp;
                patrolHelicopter.InitializeHealth(heliConfig.hp, heliConfig.hp);
                patrolHelicopter.maxCratesToSpawn = heliConfig.cratesAmount;
                patrolHelicopter.bulletDamage = heliConfig.bulletDamage;
                patrolHelicopter.bulletSpeed = heliConfig.bulletSpeed;

                var weakspots = patrolHelicopter.weakspots;
                if (weakspots != null && weakspots.Length > 1)
                {
                    weakspots[0].maxHealth = heliConfig.mainRotorHealth;
                    weakspots[0].health = heliConfig.mainRotorHealth;
                    weakspots[1].maxHealth = heliConfig.rearRotorHealth;
                    weakspots[1].health = heliConfig.rearRotorHealth;
                }
            }

            void UpdatePosition()
            {
                patrolHelicopter.myAI.spawnTime = UnityEngine.Time.realtimeSinceStartup;

                if (patrolHelicopter.myAI._currentState == PatrolHelicopterAI.aiState.DEATH || patrolHelicopter.myAI._currentState == PatrolHelicopterAI.aiState.STRAFE)
                    return;

                DoPatrol();
            }

            void DoFollowing()
            {
                Vector3 position = sputnikClass.GetEventPosition() + Vector3.up * heliConfig.height;
                patrolHelicopter.myAI.State_Move_Enter(position);
            }

            void DoPatrol()
            {
                if (patrolHelicopter.myAI.leftGun.HasTarget() || patrolHelicopter.myAI.rightGun.HasTarget())
                {
                    if (Vector3.Distance(patrolPosition, patrolHelicopter.transform.position) > heliConfig.distance)
                    {
                        ounsideTime++;

                        if (ounsideTime > heliConfig.outsideTime)
                            patrolHelicopter.myAI.State_Move_Enter(patrolPosition);
                    }
                    else
                    {
                        ounsideTime = 0;
                    }
                }
                else if (Vector3.Distance(patrolPosition, patrolHelicopter.transform.position) > heliConfig.distance)
                {
                    patrolHelicopter.myAI.State_Move_Enter(patrolPosition);
                    ounsideTime = 0;
                }
                else
                    ounsideTime = 0;
            }

            void StartFollowing()
            {
                isFollowing = true;
            }

            void StartPatrol()
            {
                isFollowing = false;
                ounsideTime = 0;
                patrolPosition = sputnikClass.GetEventPosition() + Vector3.up * heliConfig.height;
            }

            internal ulong GetHeliNetId()
            {
                return patrolHelicopter.net.ID.Value;
            }

            internal bool IsHeliCanTarget()
            {
                return sputnikClass.IsAgressive();
            }

            internal void OnHeliAttacked(ulong userId)
            {
                if (patrolHelicopter.myAI.isDead)
                    return;
                else
                    lastAttackedPlayer = userId;
            }

            internal void Kill()
            {
                if (patrolHelicopter.IsExists())
                    patrolHelicopter.Kill();
            }

            internal static void ClearData()
            {
                eventHelies.Clear();
            }
        }

        static class GuiManager
        {
            static bool isLoadingImageFailed;
            const float tabWidth = 109;
            const float tabHeigth = 25;
            static ImageInfo tabImageInfo = new ImageInfo("Tab_Adem");
            static List<ImageInfo> iconImageInfos = new List<ImageInfo>
            {
                new ImageInfo("Clock_Adem"),
                new ImageInfo("Crates_Adem"),
                new ImageInfo("Astronauts_Adem"),
            };

            internal static void LoadImages()
            {
                ServerMgr.Instance.StartCoroutine(LoadImagesCoroutine());
            }

            static IEnumerator LoadImagesCoroutine()
            {
                yield return LoadTabCoroutine();

                if (!isLoadingImageFailed)
                    yield return LoadIconsCoroutine();
            }

            static IEnumerator LoadTabCoroutine()
            {
                string url = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "Images/" + tabImageInfo.imageName + ".png";

                using (WWW www = new WWW(url))
                {
                    yield return www;

                    if (www.error != null)
                    {
                        OnImageSaveFailed(tabImageInfo.imageName);
                        isLoadingImageFailed = true;
                    }
                    else
                    {
                        Texture2D texture = www.texture;
                        uint imageId = FileStorage.server.Store(texture.EncodeToPNG(), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);
                        tabImageInfo.imageId = imageId.ToString();
                        UnityEngine.Object.DestroyImmediate(texture);
                    }
                }
            }

            static IEnumerator LoadIconsCoroutine()
            {
                for (int i = 0; i < iconImageInfos.Count; i++)
                {
                    ImageInfo imageInfo = iconImageInfos[i];
                    string url = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "Images/" + imageInfo.imageName + ".png";

                    using (WWW www = new WWW(url))
                    {
                        yield return www;

                        if (www.error != null)
                        {
                            OnImageSaveFailed(imageInfo.imageName);
                            break;
                        }
                        else
                        {
                            Texture2D texture = www.texture;
                            uint imageId = FileStorage.server.Store(texture.EncodeToPNG(), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);
                            imageInfo.imageId = imageId.ToString();
                            UnityEngine.Object.DestroyImmediate(texture);
                        }
                    }
                }
            }

            static void OnImageSaveFailed(string imageName)
            {
                NotifyManager.PrintError(null, $"Image {imageName} was not found. Maybe you didn't upload it to the .../oxide/data/Images/ folder");
                Interface.Oxide.UnloadPlugin(ins.Name);
            }

            internal static void CreateGui(BasePlayer player, params string[] args)
            {
                CuiHelper.DestroyUi(player, "Tabs_Adem");
                CuiElementContainer container = new CuiElementContainer();
                float halfWidth = tabWidth / 2 + tabWidth / 2 * (iconImageInfos.Count - 1);

                container.Add(new CuiPanel
                {
                    Image = { Color = "0 0 0 0" },
                    RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = $"{-halfWidth} {ins._config.guiConfig.offsetMinY}", OffsetMax = $"{halfWidth} {ins._config.guiConfig.offsetMinY + tabHeigth}" },
                    CursorEnabled = false,
                }, "Under", "Tabs_Adem");

                float xmin = 0;

                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];
                    DrawTab(ref container, i, arg, xmin);
                    xmin += tabWidth;
                }

                CuiHelper.AddUi(player, container);
            }

            static void DrawTab(ref CuiElementContainer container, int index, string text, float xmin)
            {
                ImageInfo imageInfo = iconImageInfos[index];

                container.Add(new CuiElement
                {
                    Name = $"Tab_{index}_Adem",
                    Parent = "Tabs_Adem",
                    Components =
                    {
                        new CuiRawImageComponent { Png = tabImageInfo.imageId },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = $"{xmin} 0", OffsetMax = $"{xmin + tabWidth} {tabHeigth}" }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = $"Tab_{index}_Adem",
                    Components =
                    {
                        new CuiRawImageComponent { Png = imageInfo.imageId },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "9 5", OffsetMax = "23 19" }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = $"Tab_{index}_Adem",
                    Components =
                    {
                        new CuiTextComponent() { Color = "1 1 1 1", Text = text, Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "robotocondensed-bold.ttf" },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "23 5", OffsetMax = $"{tabWidth - 9} 19" }
                    }
                });
            }

            internal static void DestroyAllGui()
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    if (player != null)
                        DestroyGui(player);
            }

            internal static void DestroyGui(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "Tabs_Adem");
            }

            class ImageInfo
            {
                public string imageName;
                public string imageId;

                internal ImageInfo(string imageName)
                {
                    this.imageName = imageName;
                }
            }
        }

        static class BuildManager
        {
            internal static void HideWirePoints(IOEntity entity)
            {
                entity.inputs = Array.Empty<IOEntity.IOSlot>();

                foreach (IOEntity.IOSlot slot in entity.outputs)
                    slot.type = IOEntity.IOType.Generic;
            }


            internal static void UpdateMeshColliders(BaseEntity entity)
            {
                MeshCollider[] meshColliders = entity.GetComponentsInChildren<MeshCollider>();

                for (int i = 0; i < meshColliders.Length; i++)
                {
                    MeshCollider meshCollider = meshColliders[i];
                }
            }

            internal static BaseEntity SpawnRegularEntity(string prefabName, Vector3 position, Quaternion rotation, ulong skinId = 0, bool enableSaving = false)
            {
                BaseEntity entity = CreateEntity(prefabName, position, rotation, skinId, enableSaving);
                entity.Spawn();
                return entity;
            }

            internal static BaseEntity SpawnStaticEntity(string prefabName, Vector3 position, Quaternion rotation, ulong skinId = 0)
            {
                BaseEntity entity = CreateEntity(prefabName, position, rotation, skinId, false);
                DestroyUnnessesaryComponents(entity);

                StabilityEntity stabilityEntity = entity as StabilityEntity;
                if (stabilityEntity != null)
                    stabilityEntity.grounded = true;

                BaseCombatEntity baseCombatEntity = entity as BaseCombatEntity;
                if (baseCombatEntity != null)
                    baseCombatEntity.pickup.enabled = false;

                entity.Spawn();
                return entity;
            }

            internal static BaseEntity SpawnDecorEntity(string prefabName, Vector3 position, Quaternion rotation, ulong skinId = 0, bool enableSaving = false)
            {
                BaseEntity entity = CreateDecorEntity(prefabName, position, rotation, skinId, enableSaving);
                DestroyUnnessesaryComponents(entity);
                DestroyDecorComponents(entity);
                entity.Spawn();
                return entity;
            }

            internal static BaseEntity SpawnChildEntity(BaseEntity parrentEntity, string prefabName, LocationConfig locationConfig, ulong skinId, bool isDecor)
            {
                Vector3 localPosition = locationConfig.position.ToVector3();
                Vector3 localRotation = locationConfig.rotation.ToVector3();
                return SpawnChildEntity(parrentEntity, prefabName, localPosition, localRotation, skinId, isDecor);
            }

            internal static BaseEntity SpawnChildEntity(BaseEntity parrentEntity, string prefabName, Vector3 localPosition, Vector3 localRotation, ulong skinId = 0, bool isDecor = true, bool enableSaving = false)
            {
                BaseEntity entity = isDecor ? CreateDecorEntity(prefabName, parrentEntity.transform.position, Quaternion.identity, skinId) : CreateEntity(prefabName, parrentEntity.transform.position, Quaternion.identity, skinId, enableSaving);
                SetParent(parrentEntity, entity, localPosition, localRotation);
                DestroyUnnessesaryComponents(entity);
                if (isDecor)
                    DestroyDecorComponents(entity);

                entity.Spawn();
                UpdateMeshColliders(entity);
                return entity;
            }

            internal static void UpdateEntityMaxHealth(BaseCombatEntity baseCombatEntity, float maxHealth)
            {
                baseCombatEntity.startHealth = maxHealth;
                baseCombatEntity.InitializeHealth(maxHealth, maxHealth);
            }

            internal static BaseEntity CreateEntity(string prefabName, Vector3 position, Quaternion rotation, ulong skinId, bool enableSaving)
            {
                BaseEntity entity = GameManager.server.CreateEntity(prefabName, position, rotation);
                entity.enableSaving = enableSaving;
                entity.skinID = skinId;
                return entity;
            }

            static BaseEntity CreateDecorEntity(string prefabName, Vector3 position, Quaternion rotation, ulong skinId = 0, bool enableSaving = false)
            {
                BaseEntity entity = CreateEntity(prefabName, position, rotation, skinId, enableSaving);

                BaseEntity trueBaseEntity = entity.gameObject.AddComponent<BaseEntity>();
                CopySerializableFields(entity, trueBaseEntity);
                UnityEngine.Object.DestroyImmediate(entity, true);
                entity.SetFlag(BaseEntity.Flags.Busy, true);
                entity.SetFlag(BaseEntity.Flags.Locked, true);

                return trueBaseEntity;
            }

            internal static void SetParent(BaseEntity parrentEntity, BaseEntity childEntity, Vector3 localPosition, Vector3 localRotation)
            {
                childEntity.transform.localPosition = localPosition;
                childEntity.transform.localEulerAngles = localRotation;
                childEntity.SetParent(parrentEntity);
            }

            static void DestroyDecorComponents(BaseEntity entity)
            {
                Component[] components = entity.GetComponentsInChildren<Component>();

                for (int i = 0; i < components.Length; i++)
                {
                    Component component = components[i];

                    EntityCollisionMessage entityCollisionMessage = component as EntityCollisionMessage;

                    if (entityCollisionMessage != null || (component != null && component.name != entity.PrefabName))
                    {
                        Transform transform = component as Transform;
                        if (transform != null)
                            continue;

                        Collider collider = component as Collider;
                        if (collider != null && collider is MeshCollider == false)
                            continue;

                        if (component is Model)
                            continue;

                        UnityEngine.GameObject.DestroyImmediate(component as UnityEngine.Object);
                    }
                }
            }

            static void DestroyUnnessesaryComponents(BaseEntity entity)
            {
                DestroyEntityConponent<GroundWatch>(entity);
                DestroyEntityConponent<DestroyOnGroundMissing>(entity);
                DestroyEntityConponent<TriggerHurtEx>(entity);

                if (entity is BradleyAPC == false)
                    DestroyEntityConponent<Rigidbody>(entity);
            }

            internal static void DestroyEntityConponent<TypeForDestroy>(BaseEntity entity)
            {
                if (entity == null)
                    return;

                TypeForDestroy component = entity.GetComponent<TypeForDestroy>();
                if (component != null)
                    UnityEngine.GameObject.DestroyImmediate(component as UnityEngine.Object);
            }

            internal static void DestroyEntityConponents<TypeForDestroy>(BaseEntity entity)
            {
                if (entity == null)
                    return;

                TypeForDestroy[] components = entity.GetComponentsInChildren<TypeForDestroy>();

                for (int i = 0; i < components.Length; i++)
                {
                    TypeForDestroy component = components[i];

                    if (component != null)
                        UnityEngine.GameObject.DestroyImmediate(component as UnityEngine.Object);
                }
            }

            internal static void CopySerializableFields<T>(T src, T dst)
            {
                FieldInfo[] srcFields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (FieldInfo field in srcFields)
                {
                    object value = field.GetValue(src);
                    field.SetValue(dst, value);
                }
            }
        }

        static class SpawnPositionFinder
        {
            static float checkingEntitiesRadius = ins._config.sputnikDebrisConfigs.Max(x => x.zoneConfig.radius).zoneConfig.radius;
            static Coroutine findCorountine;
            static HashSet<Vector3> spawnPositions = new HashSet<Vector3>();
            static bool isFrontierMap;

            internal static void InitialUpdate()
            {
                isFrontierMap = IsFrontierMap();
                StartCachingSpawnPoints();
            }

            static bool IsFrontierMap()
            {
                return BaseNetworkable.serverEntities.OfType<RANDSwitch>().Any(x => x != null && x.transform != null && x.transform.position.x + x.transform.position.y + x.transform.position.z == 557);
            }

            internal static void StartCachingSpawnPoints()
            {
                if (findCorountine != null)
                    return;

                UpdateCachedPoints();
                findCorountine = ServerMgr.Instance.StartCoroutine(FindSpawnPosition());
            }

            internal static void StopCachingSpawnPoints()
            {
                if (findCorountine != null)
                    ServerMgr.Instance.StopCoroutine(findCorountine);
            }

            internal static int GetCountSpawnPoints()
            {
                return spawnPositions.Where(x => IsGroundPositionAvailable(x, false)).Count;
            }

            internal static void UpdateCachedPoints()
            {
                OtherPluginsChecker.CacheData();
                spawnPositions.RemoveWhere(x => !IsGroundPositionAvailable(x, false));
            }

            internal static Vector3 GetSpawnPosition(SputnikDebrisConfig sputnikDebrisConfig, Vector3 lastSputnikPosition)
            {
                Vector3 position = Vector3.zero;

                if (sputnikDebrisConfig.useCustomSpawnPoints && sputnikDebrisConfig.customSpawnPoints.Count > 0)
                {
                    string stringPosition = sputnikDebrisConfig.customSpawnPoints.GetRandom();

                    if (stringPosition == null)
                        ins.PrintError($"Couldn't find a custom position (PresetName - {sputnikDebrisConfig.presetName})");
                    else
                        position = stringPosition.ToVector3();
                }

                if (position == Vector3.zero || position == null)
                {
                    if (ins._config.spawnConfig.isNearestPoint)
                    {
                        if (lastSputnikPosition == Vector3.zero)
                            position = spawnPositions.FirstOrDefault(x => true);
                        else
                            position = spawnPositions.Min(x => Vector3.Distance(lastSputnikPosition, x));
                    }
                    else
                    {
                        position = spawnPositions.FirstOrDefault(x => IsGroundPositionAvailable(x, false));
                    }

                    spawnPositions.Remove(position);
                }

                return position;
            }

            static IEnumerator FindSpawnPosition()
            {
                int maxCyclesCount = ins._config.spawnConfig.countSpawnPoints * 10000;
                int counter = 0;

                while (spawnPositions.Count < ins._config.spawnConfig.countSpawnPoints && counter < maxCyclesCount)
                {
                    TryFindNewSpawnPoint();
                    counter++;

                    if (counter % 100 == 0)
                        yield return null;
                }

                yield return null;
                findCorountine = null;
            }

            static void TryFindNewSpawnPoint()
            {
                Vector3 skyPosition = GetRandomSkyPoint();

                if (!TopologyChecker.IsPointAvailableByTopology(skyPosition))
                    return;

                Vector3 groundPosition = GetGroundPosition(skyPosition);

                if (IsGroundPositionAvailable(groundPosition, true))
                    spawnPositions.Add(groundPosition);
            }

            static Vector3 GetRandomSkyPoint()
            {
                Vector2 random = World.Size * 0.475f * UnityEngine.Random.insideUnitCircle;
                return new Vector3(random.x, 500f, random.y);
            }

            static Vector3 GetGroundPosition(Vector3 position)
            {
                position.y = 500f;
                RaycastHit raycastHit;

                if (Physics.Raycast(position, Vector3.down, out raycastHit, 550f))
                    return raycastHit.point;
                else
                    return Vector3.zero;
            }

            static bool IsGroundPositionAvailable(Vector3 position, bool fullCheck)
            {
                if (fullCheck)
                {
                    if (position == Vector3.zero)
                        return false;

                    if (spawnPositions.Any(x => Vector3.Distance(x, position) < ins._config.spawnConfig.minPointDistance))
                        return false;

                    if (Math.Abs(TerrainMeta.HeightMap.GetHeight(position) - position.y) > 0.5f)
                        return false;

                    if (!IsFlatSurface(position, 5))
                        return false;
                }

                if (IsAnyEntityBlockSpawn(position))
                    return false;

                if (!OtherPluginsChecker.IsPointAvailable(position))
                    return false;

                if (isFrontierMap && position.y > 50)
                    return false;

                return true;
            }

            static bool IsFlatSurface(Vector3 postition, float radius)
            {
                for (int angle = 0; angle < 360; angle += 45)
                {
                    float radian = 2f * Mathf.PI * angle / 360;
                    float x = postition.x + radius * Mathf.Cos(radian);
                    float z = postition.z + radius * Mathf.Sin(radian);
                    Vector3 positionInRadius = GetGroundPosition(new Vector3(x, postition.y, z));

                    if (positionInRadius == Vector3.zero)
                        return false;

                    if (Math.Abs(postition.y - positionInRadius.y) > 0.75f)
                        return false;
                }

                return true;
            }

            static bool IsAnyEntityBlockSpawn(Vector3 position)
            {
                foreach (Collider collider in UnityEngine.Physics.OverlapSphere(position, checkingEntitiesRadius))
                {
                    if (collider.name.Contains("heatSource"))
                        continue;

                    if (collider.name.Contains("Safe") || collider.name.Contains("Trigger (8)"))
                        return true;

                    SphereCollider sphereCollider = collider as SphereCollider;

                    if (sphereCollider != null && sphereCollider.isTrigger)
                        return true;

                    BaseEntity entity = collider.ToBaseEntity();

                    if (entity == null)
                        continue;

                    if (entity.GetBuildingPrivilege() != null)
                        return true;

                    if (entity is BuildingBlock || entity is SimpleBuildingBlock)
                        return true;
                }

                return false;
            }

            static class TopologyChecker
            {
                const int blockedTopologies = (int)(TerrainTopology.Enum.Cliff | TerrainTopology.Enum.Cliffside | TerrainTopology.Enum.Ocean | TerrainTopology.Enum.Oceanside | TerrainTopology.Enum.Building | TerrainTopology.Enum.Road | TerrainTopology.Enum.Roadside | TerrainTopology.Enum.Rail | TerrainTopology.Enum.Railside);
                const int monumentTopologies = (int)(TerrainTopology.Enum.Monument | TerrainTopology.Enum.Building);
                const int beachTopologies = (int)(TerrainTopology.Enum.Beach | TerrainTopology.Enum.Beachside);
                const int riverTopologies = (int)(TerrainTopology.Enum.River | TerrainTopology.Enum.Riverside | TerrainTopology.Enum.Lake | TerrainTopology.Enum.Lakeside);

                internal static bool IsPointAvailableByTopology(Vector3 position)
                {
                    int pointTopologies = TerrainMeta.TopologyMap.GetTopology(position);

                    if ((pointTopologies & blockedTopologies) != 0)
                        return false;

                    if (ins._config.spawnConfig.isMonumentsDisbled && (pointTopologies & monumentTopologies) != 0)
                        return false;

                    if (ins._config.spawnConfig.isBeachDisbled && (pointTopologies & beachTopologies) != 0)
                        return false;

                    if (ins._config.spawnConfig.isRiverDisbled && (pointTopologies & riverTopologies) != 0)
                        return false;

                    return true; ;
                }
            }

            static class OtherPluginsChecker
            {
                static bool isRaidableBasesActive;
                static bool isZoneManagerActive;
                static HashSet<ZoneManagerData> zoneManagerDatas = new HashSet<ZoneManagerData>();

                internal static bool IsPointAvailable(Vector3 position)
                {
                    if (isZoneManagerActive && IsZoneManagerBlockPoint(position))
                        return false;

                    if (isRaidableBasesActive && IsRaidableBasesBlockPoint(position))
                        return false;

                    return true;
                }

                static bool IsZoneManagerBlockPoint(Vector3 position)
                {
                    return zoneManagerDatas.Any(x => Vector3.Distance(x.position, position) < x.radius);
                }

                static bool IsRaidableBasesBlockPoint(Vector3 position)
                {
                    return (bool)ins.RaidableBases.Call("EventTerritory", position, checkingEntitiesRadius);
                }

                internal static void CacheData()
                {
                    isRaidableBasesActive = ins.plugins.Exists("RaidableBases");

                    zoneManagerDatas.Clear();
                    isZoneManagerActive = ins.plugins.Exists("ZoneManager");

                    if (!isZoneManagerActive)
                        return;

                    string[] zoneArray = ins.ZoneManager?.Call("GetZoneIDs") as string[];

                    if (zoneArray == null)
                        return;

                    foreach (string zoneName in zoneArray)
                    {
                        if (ins._config.supportedPluginsConfig.zoneManager.blockIDs.Contains(zoneName) || ins._config.supportedPluginsConfig.zoneManager.blockFlags.Any(x => (bool)ins.ZoneManager.Call("HasFlag", zoneName, x)))
                        {
                            Vector3 zonePosition = (Vector3)ins.ZoneManager.Call("GetZoneLocation", zoneName);
                            float zoneRadius = (float)ins.ZoneManager.Call("GetZoneRadius", zoneName);
                            ZoneManagerData zoneManagerData = new ZoneManagerData(zonePosition, zoneRadius);
                            zoneManagerDatas.Add(zoneManagerData);
                        }
                    }

                    if (zoneManagerDatas.Count == 0)
                        isZoneManagerActive = false;
                }

                class ZoneManagerData
                {
                    internal Vector3 position;
                    internal float radius;

                    internal ZoneManagerData(Vector3 position, float radius)
                    {
                        this.position = position;
                        this.radius = radius;
                    }
                }
            }
        }

        static class PositionDefiner
        {
            internal static Vector3 GetGlobalPosition(Transform parentTransform, Vector3 position)
            {
                return parentTransform.transform.TransformPoint(position);
            }

            internal static Vector3 GetGroundPositionInPoint(Vector3 position)
            {
                position.y = 500;
                RaycastHit raycastHit;

                if (Physics.Raycast(position, Vector3.down, out raycastHit, 500, 1 << 16 | 1 << 23))
                    position.y = raycastHit.point.y;

                return position;
            }

            internal static Quaternion GetGlobalRotation(Transform parentTransform, Vector3 rotation)
            {
                return parentTransform.rotation * Quaternion.Euler(rotation);
            }

            internal static bool GetNavmeshInPoint(Vector3 position, float radius, out NavMeshHit navMeshHit)
            {
                return NavMesh.SamplePosition(position, out navMeshHit, radius, 1);
            }

            internal static Vector3 GetLocalPosition(Transform parentTransform, Vector3 globalPosition)
            {
                return parentTransform.transform.InverseTransformPoint(globalPosition);
            }
        }

        static class DataFileManager
        {
            static Dictionary<string, HashSet<EntData>> saveData;

            internal static bool TryLoadData()
            {
                saveData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, HashSet<EntData>>>(ins.Title);

                if (saveData == null || saveData.Count == 0)
                {
                    ins.PrintError("Data file not found");
                    ins.NextTick(() => Interface.Oxide.UnloadPlugin(ins.Name));
                    return false;
                }

                return true;
            }

            internal static HashSet<EntData> GetSputnikData(string presetName)
            {
                HashSet<EntData> entityDatas;
                saveData.TryGetValue(presetName, out entityDatas);
                return entityDatas;
            }
        }

        public class EntData
        {
            public string prefab;
            public string pos;
            public string rot;
        }

        static class NpcSpawnManager
        {
            internal static HashSet<ScientistNPC> eventNpcs = new HashSet<ScientistNPC>();

            internal static int GetEventNpcCount()
            {
                return eventNpcs.Where(x => x.IsExists() && !x.isMounted).Count;
            }

            internal static void OnEventNpcKill()
            {

            }

            internal static HashSet<ulong> GetEventNpcNetIds()
            {
                HashSet<ulong> result = new HashSet<ulong>();

                foreach (ScientistNPC scientistNPC in eventNpcs)
                    if (scientistNPC != null && scientistNPC.net != null)
                        result.Add(scientistNPC.net.ID.Value);

                return result;
            }

            internal static ScientistNPC GetScientistByNetId(ulong netId)
            {
                return eventNpcs.FirstOrDefault(x => x != null && x.net != null && x.net.ID.Value == netId);
            }

            internal static bool IsNpcSpawnReady()
            {
                if (!ins.plugins.Exists("NpcSpawn"))
                {
                    ins.PrintError("NpcSpawn plugin doesn`t exist! Please read the file ReadMe.txt. NPCs will not spawn!");
                    ins.NextTick(() => Interface.Oxide.UnloadPlugin(ins.Name));
                    return false;
                }
                else
                    return true;
            }

            internal static ScientistNPC SpawnScientistNpc(string npcPresetName, Vector3 position, float healthFraction, bool isStationary, bool isPassive = false)
            {
                NpcConfig npcConfig = GetNpcConfigByPresetName(npcPresetName);
                if (npcConfig == null)
                {
                    NotifyManager.PrintError(null, "PresetNotFound_Exeption", npcPresetName);
                    return null;
                }

                ScientistNPC scientistNPC = SpawnScientistNpc(npcConfig, position, healthFraction, isStationary, isPassive);

                if (isStationary)
                    UpdateClothesWeight(scientistNPC);

                return scientistNPC;
            }

            internal static ScientistNPC SpawnScientistNpc(NpcConfig npcConfig, Vector3 position, float healthFraction, bool isStationary, bool isPassive)
            {
                JObject baseNpcConfigObj = GetBaseNpcConfig(npcConfig, healthFraction, isStationary, isPassive);
                ScientistNPC scientistNPC = (ScientistNPC)ins.NpcSpawn.Call("SpawnNpc", position, baseNpcConfigObj, isPassive);
                eventNpcs.Add(scientistNPC);
                return scientistNPC;
            }

            internal static NpcConfig GetNpcConfigByDisplayName(string displayName)
            {
                return ins._config.npcConfigs.FirstOrDefault(x => x.displayName == displayName);
            }

            static NpcConfig GetNpcConfigByPresetName(string npcPresetName)
            {
                return ins._config.npcConfigs.FirstOrDefault(x => x.presetName == npcPresetName);
            }

            static JObject GetBaseNpcConfig(NpcConfig config, float healthFraction, bool isStationary, bool isPassive)
            {
                return new JObject
                {
                    ["Name"] = config.displayName,
                    ["WearItems"] = new JArray
                    {
                        config.wearItems.Select(x => new JObject
                        {
                            ["ShortName"] = x.shortName,
                            ["SkinID"] = x.skinID
                        })
                    },
                    ["BeltItems"] = isPassive ? new JArray() : new JArray { config.beltItems.Select(x => new JObject { ["ShortName"] = x.shortName, ["Amount"] = x.amount, ["SkinID"] = x.skinID, ["mods"] = new JArray { x.mods.ToHashSet() }, ["Ammo"] = x.ammo }) },
                    ["Kit"] = config.kit,
                    ["Health"] = config.health * healthFraction,
                    ["RoamRange"] = isStationary ? 0 : config.roamRange,
                    ["ChaseRange"] = isStationary ? 0 : config.chaseRange,
                    ["SenseRange"] = config.senseRange,
                    ["ListenRange"] = config.senseRange / 2,
                    ["AttackRangeMultiplier"] = config.attackRangeMultiplier,
                    ["CheckVisionCone"] = true,
                    ["VisionCone"] = config.visionCone,
                    ["HostileTargetsOnly"] = false,
                    ["DamageScale"] = config.damageScale,
                    ["TurretDamageScale"] = config.turretDamageScale,
                    ["AimConeScale"] = config.aimConeScale,
                    ["DisableRadio"] = config.disableRadio,
                    ["CanRunAwayWater"] = false,
                    ["CanSleep"] = false,
                    ["SleepDistance"] = 100f,
                    ["Speed"] = isStationary ? 0 : config.speed,
                    ["AreaMask"] = 1,
                    ["AgentTypeID"] = -1372625422,
                    ["HomePosition"] = string.Empty,
                    ["MemoryDuration"] = config.memoryDuration,
                    ["States"] = isPassive ? new JArray() : isStationary ? new JArray { "IdleState", "CombatStationaryState" } : config.beltItems.Any(x => x.shortName == "rocket.launcher" || x.shortName == "explosive.timed") ? new JArray { "RaidState", "RoamState", "ChaseState", "CombatState" } : new JArray { "RoamState", "ChaseState", "CombatState" }
                };
            }

            static void UpdateClothesWeight(ScientistNPC scientistNPC)
            {
                foreach (Item item in scientistNPC.inventory.containerWear.itemList)
                {
                    ItemModWearable component = item.info.GetComponent<ItemModWearable>();

                    if (component != null)
                        component.weight = 0;
                }
            }

            internal static void ClearData(bool shoudKillNpcs)
            {
                if (shoudKillNpcs)
                    foreach (ScientistNPC scientistNPC in eventNpcs)
                        if (scientistNPC.IsExists())
                            scientistNPC.Kill();

                eventNpcs.Clear();
            }
        }

        static class ItemSpawner
        {
            internal static void GiveSpaceCardToPlayer(BasePlayer player)
            {
                Item item = CreateSpaceCardItem();
                PlayerItemGiver.GiveItemToPLayer(player, item);
            }

            internal static void SpawnSpaceCardInCrate(LootContainer container)
            {
                float chance = 0;

                if (!ins._config.customCardConfig.spawnSetting.TryGetValue(container.PrefabName, out chance) || chance == 0)
                    return;

                if (UnityEngine.Random.Range(0f, 100f) > chance)
                    return;

                container.Invoke(() =>
                {
                    if (container.IsExists())
                    {
                        Item item = CreateSpaceCardItem();

                        if (!item.MoveToContainer(container.inventory))
                            item.Remove();
                    }
                }, 1.1f);
            }

            internal static Item CreateSpaceCardItem()
            {
                Item item = ItemManager.CreateByName(ins._config.customCardConfig.shortName, 1, ins._config.customCardConfig.skinID);

                if (ins._config.customCardConfig.name != "")
                    item.name = ins._config.customCardConfig.name;

                return item;
            }

            static class PlayerItemGiver
            {
                internal static void GiveItemToPLayer(BasePlayer player, Item item)
                {
                    int spaceCountItem = GetMaxItemCount(player, item.info.shortname, item.MaxStackable(), item.skin);
                    int inventoryItemCount;

                    if (spaceCountItem > item.amount)
                        inventoryItemCount = item.amount;
                    else
                        inventoryItemCount = spaceCountItem;

                    if (inventoryItemCount > 0)
                    {
                        Item itemInventory = ItemManager.CreateByName(item.info.shortname, inventoryItemCount, item.skin);

                        if (item.skin != 0)
                            itemInventory.name = item.name;

                        item.amount -= inventoryItemCount;
                        PlayerItemGiver.MoveInventoryItem(player, itemInventory);
                    }

                    if (item.amount > 0)
                        PlayerItemGiver.DropExtraItem(player, item);
                }

                internal static int GetMaxItemCount(BasePlayer player, string shortname, int stack, ulong skinID)
                {
                    int slots = player.inventory.containerMain.capacity + player.inventory.containerBelt.capacity;
                    int taken = player.inventory.containerMain.itemList.Count + player.inventory.containerBelt.itemList.Count;
                    int result = (slots - taken) * stack;

                    List<Item> allItems = Pool.Get<List<Item>>();
                    player.inventory.GetAllItems(allItems);

                    foreach (Item item in allItems)
                        if (item.info.shortname == shortname && item.skin == skinID && item.amount < stack)
                            result += stack - item.amount;

                    Pool.FreeUnmanaged(ref allItems);
                    return result;
                }

                internal static void MoveInventoryItem(BasePlayer player, Item item)
                {
                    if (item.amount <= item.MaxStackable())
                    {
                        List<Item> allItems = Pool.Get<List<Item>>();
                        player.inventory.GetAllItems(allItems);

                        foreach (Item itemInv in allItems)
                        {
                            if (itemInv.info.shortname == item.info.shortname && itemInv.skin == item.skin && itemInv.amount < itemInv.MaxStackable())
                            {
                                if (itemInv.amount + item.amount <= itemInv.MaxStackable())
                                {
                                    Pool.FreeUnmanaged(ref allItems);
                                    itemInv.amount += item.amount;
                                    itemInv.MarkDirty();
                                    return;
                                }
                                else
                                {
                                    item.amount -= itemInv.MaxStackable() - itemInv.amount;
                                    itemInv.amount = itemInv.MaxStackable();
                                }
                            }
                        }

                        Pool.FreeUnmanaged(ref allItems);

                        if (item.amount > 0) player.inventory.GiveItem(item);
                    }
                    else
                    {
                        while (item.amount > item.MaxStackable())
                        {
                            Item thisItem = ItemManager.CreateByName(item.info.shortname, item.MaxStackable(), item.skin);

                            if (item.skin != 0)
                                thisItem.name = item.name;

                            player.inventory.GiveItem(thisItem);
                            item.amount -= item.MaxStackable();
                        }
                        if (item.amount > 0)
                            player.inventory.GiveItem(item);
                    }
                }

                internal static void DropExtraItem(BasePlayer player, Item item)
                {
                    if (item.amount <= item.MaxStackable())
                    {
                        item.Drop(player.transform.position, Vector3.up);
                    }
                    else
                    {
                        while (item.amount > item.MaxStackable())
                        {
                            Item thisItem = ItemManager.CreateByName(item.info.shortname, item.MaxStackable(), item.skin);
                            if (item.skin != 0) thisItem.name = item.name;
                            thisItem.Drop(player.transform.position, Vector3.up);
                            item.amount -= item.MaxStackable();
                        }
                        if (item.amount > 0) item.Drop(player.transform.position, Vector3.up);
                    }
                }
            }

        }

        static class LootManager
        {
            static HashSet<ulong> lootedContainersUids = new HashSet<ulong>();
            static HashSet<StorageContainerData> storageContainers = new HashSet<StorageContainerData>();
            static int countOfUnlootedCrates;

            internal static int GetCountOfUnlootedCrates()
            {
                return countOfUnlootedCrates;
            }

            internal static void UpdateCountOfUnlootedCrates()
            {
                countOfUnlootedCrates = storageContainers.Where(x => x != null && x.storageContainer.IsExists() && x.storageContainer.net != null && !IsCrateLooted(x.storageContainer.net.ID.Value)).Count;
            }

            internal static void OnHeliCrateSpawned(LockedByEntCrate lockedByEntCrate)
            {
                EventHeli eventHeli = EventHeli.GetClosestHeli(lockedByEntCrate.transform.position);

                if (eventHeli == null)
                    return;

                if (Vector3.Distance(lockedByEntCrate.transform.position, eventHeli.transform.position) <= 10)
                {
                    lockedByEntCrate.Invoke(() =>
                    {
                        UpdateBaseLootTable(lockedByEntCrate.inventory, eventHeli.heliConfig.baseLootTableConfig, eventHeli.heliConfig.baseLootTableConfig.clearDefaultItemList);

                        if (eventHeli.heliConfig.instCrateOpen)
                        {
                            lockedByEntCrate.SetLockingEnt(null);
                            lockedByEntCrate.SetLocked(false);
                        }
                    }, 1f);
                }
            }

            internal static void OnEventCrateLooted(StorageContainer storageContainer, ulong userId)
            {
                if (storageContainer.net == null)
                    return;

                if (!IsCrateLooted(storageContainer.net.ID.Value))
                {
                    double cratePoint;

                    if (ins._config.supportedPluginsConfig.economicsConfig.crates.TryGetValue(storageContainer.PrefabName, out cratePoint))
                        EconomyManager.AddBalance(userId, cratePoint);

                    lootedContainersUids.Add(storageContainer.net.ID.Value);
                }

                UpdateCountOfUnlootedCrates();
            }

            internal static bool IsCrateLooted(ulong netID)
            {
                return lootedContainersUids.Contains(netID);
            }

            internal static bool IsEventCrate(ulong netID)
            {
                return GetContainerDataByNetId(netID) != null;
            }

            internal static StorageContainerData GetContainerDataByNetId(ulong netID)
            {
                return storageContainers.FirstOrDefault(x => x != null && x.storageContainer.IsExists() && x.storageContainer.net != null && x.storageContainer.net.ID.Value == netID);
            }

            internal static HashSet<ulong> GetEventCratesNetIDs()
            {
                HashSet<ulong> eventCrates = new HashSet<ulong>();

                foreach (StorageContainerData storageContainerData in storageContainers)
                    if (storageContainerData != null && storageContainerData.storageContainer != null && storageContainerData.storageContainer.net != null)
                        eventCrates.Add(storageContainerData.storageContainer.net.ID.Value);

                return eventCrates;
            }

            internal static CrateConfig GetCrateConfigByPresetName(string presetName)
            {
                return ins._config.crateConfigs.FirstOrDefault(x => x.presetName == presetName);
            }

            internal static void InitialLootManagerUpdate()
            {
                LootPrefabController.FindPrefabs();
                UpdateLootTables();
            }

            static void UpdateLootTables()
            {
                foreach (CrateConfig crateConfig in ins._config.crateConfigs)
                    UpdateBaseLootTable(crateConfig.lootTableConfig);

                foreach (NpcConfig npcConfig in ins._config.npcConfigs)
                    UpdateBaseLootTable(npcConfig.lootTableConfig);

                foreach (HeliConfig heliConfig in ins._config.heliConfigs)
                    UpdateBaseLootTable(heliConfig.baseLootTableConfig);

                ins.SaveConfig();
            }

            static void UpdateBaseLootTable(BaseLootTableConfig baseLootTableConfig)
            {
                for (int i = 0; i < baseLootTableConfig.items.Count; i++)
                {
                    LootItemConfig lootItemConfig = baseLootTableConfig.items[i];

                    if (lootItemConfig.chance <= 0)
                        baseLootTableConfig.items.RemoveAt(i);
                }

                baseLootTableConfig.items = baseLootTableConfig.items.OrderByQuickSort(x => x.chance);

                if (baseLootTableConfig.maxItemsAmount > baseLootTableConfig.items.Count)
                    baseLootTableConfig.maxItemsAmount = baseLootTableConfig.items.Count;

                if (baseLootTableConfig.minItemsAmount > baseLootTableConfig.maxItemsAmount)
                    baseLootTableConfig.minItemsAmount = baseLootTableConfig.maxItemsAmount;
            }

            internal static void UpdateItemContainer(ItemContainer itemContainer, LootTableConfig lootTableConfig, bool deleteItems = false)
            {
                UpdateLootTable(itemContainer, lootTableConfig, deleteItems);
            }

            internal static void UpdateStorageContainer(StorageContainer storageContainer, CrateConfig crateConfig)
            {
                storageContainer.onlyAcceptCategory = ItemCategory.All;
                UpdateLootTable(storageContainer.inventory, crateConfig.lootTableConfig, false);
                storageContainers.Add(new StorageContainerData(storageContainer, crateConfig.presetName));
            }

            internal static void UpdateLootContainer(LootContainer lootContainer, CrateConfig crateConfig)
            {
                HackableLockedCrate hackableLockedCrate = lootContainer as HackableLockedCrate;
                if (hackableLockedCrate != null)
                {
                    if (hackableLockedCrate.mapMarkerInstance.IsExists())
                    {
                        hackableLockedCrate.mapMarkerInstance.Kill();
                        hackableLockedCrate.mapMarkerInstance = null;
                    }

                    hackableLockedCrate.Invoke(() => DelayUpdateHackableLockedCrate(hackableLockedCrate, crateConfig), 1f);
                }

                SupplyDrop supplyDrop = lootContainer as SupplyDrop;
                if (supplyDrop != null)
                {
                    supplyDrop.RemoveParachute();
                    supplyDrop.MakeLootable();
                }

                FreeableLootContainer freeableLootContainer = lootContainer as FreeableLootContainer;
                if (freeableLootContainer != null)
                    freeableLootContainer.SetFlag(BaseEntity.Flags.Reserved8, false);

                lootContainer.Invoke(() => UpdateLootTable(lootContainer.inventory, crateConfig.lootTableConfig, crateConfig.lootTableConfig.clearDefaultItemList), 2f);
                storageContainers.Add(new StorageContainerData(lootContainer, crateConfig.presetName));
            }

            static void DelayUpdateHackableLockedCrate(HackableLockedCrate hackableLockedCrate, CrateConfig crateConfig)
            {
                if (hackableLockedCrate == null || crateConfig.hackTime < 0)
                    return;

                hackableLockedCrate.hackSeconds = HackableLockedCrate.requiredHackSeconds - crateConfig.hackTime;
                UpdateLootTable(hackableLockedCrate.inventory, crateConfig.lootTableConfig, crateConfig.lootTableConfig.clearDefaultItemList);
                hackableLockedCrate.InvokeRepeating(() => hackableLockedCrate.SendNetworkUpdate(), 1f, 1f);
            }

            internal static void UpdateCrateHackTime(HackableLockedCrate hackableLockedCrate, string cratePresetName)
            {
                CrateConfig crateConfig = GetCrateConfigByPresetName(cratePresetName);

                if (crateConfig.hackTime < 0)
                    return;

                hackableLockedCrate.Invoke(() => hackableLockedCrate.hackSeconds = HackableLockedCrate.requiredHackSeconds - crateConfig.hackTime, 1.1f);
            }

            static void UpdateLootTable(ItemContainer itemContainer, LootTableConfig lootTableConfig, bool clearContainer)
            {
                if (itemContainer == null)
                    return;

                UpdateBaseLootTable(itemContainer, lootTableConfig, clearContainer || !string.IsNullOrEmpty(lootTableConfig.alphaLootPresetName));

                if (!string.IsNullOrEmpty(lootTableConfig.alphaLootPresetName))
                {
                    if (ins.plugins.Exists("AlphaLoot") && (bool)ins.AlphaLoot.Call("ProfileExists", lootTableConfig.alphaLootPresetName))
                    {
                        ins.AlphaLoot.Call("PopulateLoot", itemContainer, lootTableConfig.alphaLootPresetName);
                    }
                }
            }

            static void UpdateBaseLootTable(ItemContainer itemContainer, BaseLootTableConfig baseLootTableConfig, bool clearContainer)
            {
                if (itemContainer == null)
                    return;

                if (clearContainer)
                    ClearItemsContainer(itemContainer);

                LootPrefabController.TryAddLootFromPrefabs(itemContainer, baseLootTableConfig.prefabConfigs);
                RandomItemsFiller.TryAddItemsToContainer(itemContainer, baseLootTableConfig);

                if (itemContainer.capacity < itemContainer.itemList.Count)
                    itemContainer.capacity = itemContainer.itemList.Count;
            }

            static void ClearItemsContainer(ItemContainer container)
            {
                for (int i = container.itemList.Count - 1; i >= 0; i--)
                {
                    Item item = container.itemList[i];
                    item.RemoveFromContainer();
                    item.Remove();
                }
            }

            internal static void ClearLootData(bool shoudKillCrates = false)
            {
                if (shoudKillCrates)
                    foreach (StorageContainerData storageContainerData in storageContainers)
                        if (storageContainerData != null && storageContainerData.storageContainer.IsExists())
                            storageContainerData.storageContainer.Kill();

                lootedContainersUids.Clear();
                storageContainers.Clear();
            }

            class LootPrefabController
            {
                static HashSet<LootPrefabController> lootPrefabDatas = new HashSet<LootPrefabController>();

                string prefabName;
                LootContainer.LootSpawnSlot[] lootSpawnSlot;
                LootSpawn lootDefinition;
                int maxDefinitionsToSpawn;
                int scrapAmount;

                internal static void TryAddLootFromPrefabs(ItemContainer itemContainer, PrefabLootTableConfigs prefabLootTableConfig)
                {
                    if (!prefabLootTableConfig.isEnable)
                        return;

                    PrefabConfig prefabConfig = prefabLootTableConfig.prefabs.GetRandom();

                    if (prefabConfig == null)
                        return;

                    int multiplicator = UnityEngine.Random.Range(prefabConfig.minLootScale, prefabConfig.maxLootScale + 1);
                    TryFillContainerByPrefab(itemContainer, prefabConfig.prefabName, multiplicator);
                }

                internal static void FindPrefabs()
                {
                    foreach (CrateConfig crateConfig in ins._config.crateConfigs.Where(x => x.lootTableConfig.prefabConfigs.isEnable))
                        foreach (PrefabConfig prefabConfig in crateConfig.lootTableConfig.prefabConfigs.prefabs)
                            TrySaveLootPrefab(prefabConfig.prefabName);

                    foreach (NpcConfig npcConfig in ins._config.npcConfigs.Where(x => x.lootTableConfig.prefabConfigs.isEnable))
                        foreach (PrefabConfig prefabConfig in npcConfig.lootTableConfig.prefabConfigs.prefabs)
                            TrySaveLootPrefab(prefabConfig.prefabName);

                    foreach (HeliConfig heliConfig in ins._config.heliConfigs.Where(x => x.baseLootTableConfig.prefabConfigs.isEnable))
                        foreach (PrefabConfig prefabConfig in heliConfig.baseLootTableConfig.prefabConfigs.prefabs)
                            TrySaveLootPrefab(prefabConfig.prefabName);
                }

                internal static void TrySaveLootPrefab(string prefabName)
                {
                    if (lootPrefabDatas.Any(x => x.prefabName == prefabName))
                        return;

                    GameObject gameObject = GameManager.server.FindPrefab(prefabName);

                    if (gameObject == null)
                        return;

                    LootContainer lootContainer = gameObject.GetComponent<LootContainer>();

                    if (lootContainer != null)
                    {
                        SaveLootPrefabData(prefabName, lootContainer.LootSpawnSlots, lootContainer.scrapAmount, lootContainer.lootDefinition, lootContainer.maxDefinitionsToSpawn);
                        return;
                    }

                    global::HumanNPC humanNPC = gameObject.GetComponent<global::HumanNPC>();

                    if (humanNPC != null && humanNPC.LootSpawnSlots.Length > 0)
                    {
                        SaveLootPrefabData(prefabName, humanNPC.LootSpawnSlots, 0);
                        return;
                    }

                    ScarecrowNPC scarecrowNPC = gameObject.GetComponent<ScarecrowNPC>();

                    if (scarecrowNPC != null && scarecrowNPC.LootSpawnSlots.Length > 0)
                    {
                        SaveLootPrefabData(prefabName, scarecrowNPC.LootSpawnSlots, 0);
                        return;
                    }
                }

                internal static void SaveLootPrefabData(string prefabName, LootContainer.LootSpawnSlot[] lootSpawnSlot, int scrapAmount, LootSpawn lootDefinition = null, int maxDefinitionsToSpawn = 0)
                {
                    LootPrefabController lootPrefabData = new LootPrefabController
                    {
                        prefabName = prefabName,
                        lootSpawnSlot = lootSpawnSlot,
                        lootDefinition = lootDefinition,
                        maxDefinitionsToSpawn = maxDefinitionsToSpawn,
                        scrapAmount = scrapAmount
                    };

                    lootPrefabDatas.Add(lootPrefabData);
                }

                internal static void TryFillContainerByPrefab(ItemContainer itemContainer, string prefabName, int multiplicator)
                {
                    LootPrefabController lootPrefabData = GetDataForPrefabName(prefabName);

                    if (lootPrefabData != null)
                        for (int i = 0; i < multiplicator; i++)
                            lootPrefabData.SpawnPrefabLootInCrate(itemContainer);
                }

                static LootPrefabController GetDataForPrefabName(string prefabName)
                {
                    return lootPrefabDatas.FirstOrDefault(x => x.prefabName == prefabName);
                }

                void SpawnPrefabLootInCrate(ItemContainer itemContainer)
                {
                    if (lootSpawnSlot != null && lootSpawnSlot.Length > 0)
                    {
                        foreach (LootContainer.LootSpawnSlot lootSpawnSlot in lootSpawnSlot)
                            for (int j = 0; j < lootSpawnSlot.numberToSpawn; j++)
                                if (UnityEngine.Random.Range(0f, 1f) <= lootSpawnSlot.probability)
                                    lootSpawnSlot.definition.SpawnIntoContainer(itemContainer);
                    }
                    else if (lootDefinition != null)
                    {
                        for (int i = 0; i < maxDefinitionsToSpawn; i++)
                            lootDefinition.SpawnIntoContainer(itemContainer);
                    }

                    GenerateScrap(itemContainer);
                }

                void GenerateScrap(ItemContainer itemContainer)
                {
                    if (scrapAmount <= 0)
                        return;

                    Item item = ItemManager.CreateByName("scrap", scrapAmount, 0);

                    if (item == null)
                        return;

                    if (!item.MoveToContainer(itemContainer))
                        item.Remove();
                }
            }

            static class RandomItemsFiller
            {
                static Dictionary<char, GrowableGenetics.GeneType> charToGene = new Dictionary<char, GrowableGenetics.GeneType>
                {
                    ['g'] = GrowableGenetics.GeneType.GrowthSpeed,
                    ['y'] = GrowableGenetics.GeneType.Yield,
                    ['h'] = GrowableGenetics.GeneType.Hardiness,
                    ['w'] = GrowableGenetics.GeneType.WaterRequirement,
                };

                internal static void TryAddItemsToContainer(ItemContainer itemContainer, BaseLootTableConfig baseLootTableConfig)
                {
                    if (!baseLootTableConfig.isRandomItemsEnable)
                        return;

                    HashSet<int> includeItemIndexes = new HashSet<int>();
                    int targetItemsCount = UnityEngine.Random.Range(baseLootTableConfig.minItemsAmount, baseLootTableConfig.maxItemsAmount + 1);

                    while (includeItemIndexes.Count < targetItemsCount)
                    {
                        if (!baseLootTableConfig.items.Any(x => x.chance >= 0.1f && !includeItemIndexes.Contains(baseLootTableConfig.items.IndexOf(x))))
                            break;

                        for (int i = 0; i < baseLootTableConfig.items.Count; i++)
                        {
                            if (includeItemIndexes.Contains(i))
                                continue;

                            LootItemConfig lootItemConfig = baseLootTableConfig.items[i];
                            float chance = UnityEngine.Random.Range(0.0f, 100.0f);

                            if (chance <= lootItemConfig.chance)
                            {
                                Item item = CreateItem(lootItemConfig);
                                includeItemIndexes.Add(i);

                                if (itemContainer.itemList.Count >= itemContainer.capacity)
                                    itemContainer.capacity += 1;

                                if (item == null || !item.MoveToContainer(itemContainer))
                                    item.Remove();

                                if (includeItemIndexes.Count == targetItemsCount)
                                    return;
                            }
                        }
                    }
                }

                internal static Item CreateItem(LootItemConfig lootItemConfig)
                {
                    int amount = UnityEngine.Random.Range((int)(lootItemConfig.minAmount), (int)(lootItemConfig.maxAmount + 1));

                    if (amount <= 0)
                        amount = 1;

                    return CreateItem(lootItemConfig, amount);
                }

                internal static Item CreateItem(LootItemConfig itemConfig, int amount)
                {
                    Item item = null;

                    if (itemConfig.isBlueprint)
                    {
                        item = ItemManager.CreateByName("blueprintbase");
                        item.blueprintTarget = ItemManager.FindItemDefinition(itemConfig.shortname).itemid;
                    }
                    else
                        item = ItemManager.CreateByName(itemConfig.shortname, amount, itemConfig.skin);

                    if (item == null)
                    {
                        ins.PrintWarning($"Failed to create item! ({itemConfig.shortname})");
                        return null;
                    }

                    if (!string.IsNullOrEmpty(itemConfig.name))
                        item.name = itemConfig.name;

                    if (itemConfig.genomes != null && itemConfig.genomes.Count > 0)
                    {
                        string genome = itemConfig.genomes.GetRandom();
                        UpdateGenome(item, genome);
                    }

                    return item;
                }

                static void UpdateGenome(Item item, string genome)
                {
                    genome = genome.ToLower();
                    GrowableGenes growableGenes = new GrowableGenes();

                    for (int i = 0; i < 6 && i < genome.Length; ++i)
                    {
                        GrowableGenetics.GeneType geneType;

                        if (!charToGene.TryGetValue(genome[i], out geneType))
                            geneType = GrowableGenetics.GeneType.Empty;

                        growableGenes.Genes[i].Set(geneType, true);
                        GrowableGeneEncoding.EncodeGenesToItem(GrowableGeneEncoding.EncodeGenesToInt(growableGenes), item);
                    }

                }
            }
        }

        class StorageContainerData
        {
            public StorageContainer storageContainer;
            public string presetName;

            public StorageContainerData(StorageContainer storageContainer, string presetName)
            {
                this.storageContainer = storageContainer;
                this.presetName = presetName;
            }
        }

        static class EconomyManager
        {
            static readonly Dictionary<ulong, double> playersBalance = new Dictionary<ulong, double>();

            internal static void AddBalance(ulong playerId, double balance)
            {
                if (balance == 0 || playerId == 0)
                    return;

                if (playersBalance.ContainsKey(playerId))
                    playersBalance[playerId] += balance;
                else
                    playersBalance.Add(playerId, balance);
            }

            internal static void OnEventEnd()
            {
                DefineEventWinner();

                if (!ins._config.supportedPluginsConfig.economicsConfig.enable || playersBalance.Count == 0)
                {
                    playersBalance.Clear();
                    return;
                }

                SendBalanceToPlayers();
                playersBalance.Clear();
            }

            static void DefineEventWinner()
            {
                var winnerPair = playersBalance.Max(x => (float)x.Value);

                if (winnerPair.Value > 0)
                    Interface.CallHook("OnSputnikEventWin", winnerPair.Key);

                if (winnerPair.Value >= ins._config.supportedPluginsConfig.economicsConfig.minCommandPoint)
                    foreach (string command in ins._config.supportedPluginsConfig.economicsConfig.commands)
                        ins.Server.Command(command.Replace("{steamid}", $"{winnerPair.Key}"));
            }

            static void SendBalanceToPlayers()
            {
                foreach (KeyValuePair<ulong, double> pair in playersBalance)
                    SendBalanceToPlayer(pair.Key, pair.Value);
            }

            static void SendBalanceToPlayer(ulong userID, double amount)
            {
                if (amount < ins._config.supportedPluginsConfig.economicsConfig.minEconomyPiont)
                    return;

                int intAmount = Convert.ToInt32(amount);

                if (intAmount <= 0)
                    return;

                if (ins._config.supportedPluginsConfig.economicsConfig.plugins.Contains("Economics") && ins.plugins.Exists("Economics"))
                    ins.Economics.Call("Deposit", userID.ToString(), amount);

                if (ins._config.supportedPluginsConfig.economicsConfig.plugins.Contains("Server Rewards") && ins.plugins.Exists("ServerRewards"))
                    ins.ServerRewards.Call("AddPoints", userID, intAmount);

                if (ins._config.supportedPluginsConfig.economicsConfig.plugins.Contains("IQEconomic") && ins.plugins.Exists("IQEconomic"))
                    ins.IQEconomic.Call("API_SET_BALANCE", userID, intAmount);

                BasePlayer player = BasePlayer.FindByID(userID);
                if (player != null)
                    NotifyManager.SendMessageToPlayer(player, "SendEconomy", ins._config.prefix, amount);
            }
        }

        static class NotifyManager
        {
            internal static void PrintInfoMessage(BasePlayer player, string langKey, params object[] args)
            {
                if (player == null)
                    ins.PrintWarning(ClearColorAndSize(GetMessage(langKey, null, args)));
                else
                    ins.PrintToChat(player, GetMessage(langKey, player.UserIDString, args));
            }

            internal static void PrintError(BasePlayer player, string langKey, params object[] args)
            {
                if (player == null)
                    ins.PrintError(ClearColorAndSize(GetMessage(langKey, null, args)));
                else
                    ins.PrintToChat(player, GetMessage(langKey, player.UserIDString, args));
            }

            internal static void PrintLogMessage(string langKey, params object[] args)
            {
                for (int i = 0; i < args.Length; i++)
                    if (args[i] is int)
                        args[i] = GetTimeMessage(null, (int)args[i]);

                ins.Puts(ClearColorAndSize(GetMessage(langKey, null, args)));
            }

            internal static void PrintWarningMessage(string langKey, params object[] args)
            {
                ins.PrintWarning(ClearColorAndSize(GetMessage(langKey, null, args)));
            }

            internal static string ClearColorAndSize(string message)
            {
                message = message.Replace("</color>", string.Empty);
                message = message.Replace("</size>", string.Empty);
                while (message.Contains("<color="))
                {
                    int index = message.IndexOf("<color=");
                    message = message.Remove(index, message.IndexOf(">", index) - index + 1);
                }
                while (message.Contains("<size="))
                {
                    int index = message.IndexOf("<size=");
                    message = message.Remove(index, message.IndexOf(">", index) - index + 1);
                }
                return message;
            }

            internal static void SendMessageToAll(string langKey, params object[] args)
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    if (player != null)
                        SendMessageToPlayer(player, langKey, args);

                TrySendDiscordMessage(langKey, args);
            }

            internal static void SendMessageToPlayer(BasePlayer player, string langKey, params object[] args)
            {
                for (int i = 0; i < args.Length; i++)
                    if (args[i] is int)
                        args[i] = GetTimeMessage(player.UserIDString, (int)args[i]);

                string playerMessage = GetMessage(langKey, player.UserIDString, args);

                if (ins._config.notifyConfig.isChatEnable)
                    ins.PrintToChat(player, playerMessage);

                if (ins._config.notifyConfig.gameTipConfig.isEnabled)
                    player.SendConsoleCommand("gametip.showtoast", ins._config.notifyConfig.gameTipConfig.style, ClearColorAndSize(playerMessage), string.Empty);

                if (ins._config.supportedPluginsConfig.guiAnnouncementsConfig.isEnabled && ins.plugins.Exists("guiAnnouncementsConfig"))
                    ins.GUIAnnouncements?.Call("CreateAnnouncement", ClearColorAndSize(playerMessage), ins._config.supportedPluginsConfig.guiAnnouncementsConfig.bannerColor, ins._config.supportedPluginsConfig.guiAnnouncementsConfig.textColor, player, ins._config.supportedPluginsConfig.guiAnnouncementsConfig.apiAdjustVPosition);

                if (ins._config.supportedPluginsConfig.notifyPluginConfig.isEnabled && ins.plugins.Exists("Notify"))
                    ins.Notify?.Call("SendNotify", player, ins._config.supportedPluginsConfig.notifyPluginConfig.type, ClearColorAndSize(playerMessage));
            }

            internal static string GetTimeMessage(string userIDString, int seconds)
            {
                string message = "";

                TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);
                if (timeSpan.Hours > 0) message += $" {timeSpan.Hours} {GetMessage("Hours", userIDString)}";
                if (timeSpan.Minutes > 0) message += $" {timeSpan.Minutes} {GetMessage("Minutes", userIDString)}";
                if (message == "") message += $" {timeSpan.Seconds} {GetMessage("Seconds", userIDString)}";

                return message;
            }

            static void TrySendDiscordMessage(string langKey, params object[] args)
            {
                if (CanSendDiscordMessage(langKey))
                {
                    for (int i = 0; i < args.Length; i++)
                        if (args[i] is int)
                            args[i] = GetTimeMessage(null, (int)args[i]);

                    object fields = new[] { new { name = ins.Title, value = ClearColorAndSize(GetMessage(langKey, null, args)), inline = false } };
                    ins.DiscordMessages?.Call("API_SendFancyMessage", ins._config.supportedPluginsConfig.discordMessagesConfig.webhookUrl, "", ins._config.supportedPluginsConfig.discordMessagesConfig.embedColor, JsonConvert.SerializeObject(fields), null, ins);
                }
            }

            static bool CanSendDiscordMessage(string langKey)
            {
                return ins._config.supportedPluginsConfig.discordMessagesConfig.keys.Contains(langKey) && ins._config.supportedPluginsConfig.discordMessagesConfig.isEnabled && !string.IsNullOrEmpty(ins._config.supportedPluginsConfig.discordMessagesConfig.webhookUrl) && ins._config.supportedPluginsConfig.discordMessagesConfig.webhookUrl != "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
            }
        }

        static class IQDroneManager
        {
            internal static void SpawnDrones(Vector3 position, string presetName, int zoneRadius)
            {
                if (!ins._config.supportedPluginsConfig.iqDroneConfig.isDroneEnable)
                    return;

                if (!ins.plugins.Exists("IQDronePatrol"))
                {
                    ins.PrintError("IQDronePatrol plugin doesn`t exist!");
                    return;
                }

                CustomPatrol customPatrol = GetPresetPatrol(position, presetName, zoneRadius);

                if (customPatrol == null)
                {
                    ins.PrintError("IQDronePatrol the preset is specified incorrectly!");
                    return;
                }

                String json = JsonConvert.SerializeObject(customPatrol);
                ins.IQDronePatrol.Call<Dictionary<Drone, AutoTurret>>("SendPatrolPoint", json, false);
            }

            internal static void KillDrones(Vector3 position)
            {
                if (!ins._config.supportedPluginsConfig.iqDroneConfig.isDroneEnable)
                    return;

                if (!ins.plugins.Exists("IQDronePatrol"))
                    return;

                ins.IQDronePatrol.Call("CancellPatrol", position);
            }

            static CustomPatrol GetPresetPatrol(Vector3 position, string presetName, int zoneRadius)
            {
                IQDronePreset iQDronePreset = ins._config.supportedPluginsConfig.iqDroneConfig.dronePresets.FirstOrDefault(x => x.presetName == presetName);

                CustomPatrol myPatrol = new CustomPatrol
                {
                    pluginName = ins.Name,
                    position = position,
                    settingDrone = new CustomPatrol.DroneSetting
                    {
                        droneCountSpawned = iQDronePreset.droneNumber,
                        droneAttackedCount = iQDronePreset.targetDroneNumber,
                        keyDrones = iQDronePreset.droneKeys
                    },
                    settingPosition = new CustomPatrol.PositionSetting
                    {
                        countSpawnPoint = 200,
                        radiusFindedPoints = zoneRadius
                    },
                };

                return myPatrol;
            }

            class CustomPatrol
            {
                public String pluginName;
                public Vector3 position;
                public PositionSetting settingPosition = new PositionSetting();
                public DroneSetting settingDrone = new DroneSetting();

                internal class DroneSetting
                {
                    public Int32 droneCountSpawned;
                    public Int32 droneAttackedCount;
                    public Dictionary<String, Int32> keyDrones = new Dictionary<String, Int32>();
                }
                internal class PositionSetting
                {
                    public Int32 countSpawnPoint;
                    public Int32 radiusFindedPoints;
                }
            }
        }
        #endregion Classes

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["EventActive_Exeption"] = "Ивент в данный момент активен, сначала завершите текущий ивент (<color=#ce3f27>/sputikstop</color>)!",
                ["PresetNotFound_Exeption"] = "Пресет {0} не найден в конфиге!",

                ["SuccessfullyLaunched"] = "Ивент <color=#738d43>успешно</color> запущен!",
                ["PreStartEvent"] = "{0} <color=#738d43>{1}</color> войдет в атмосферу через <color=#738d43>{2}</color>!",
                ["StartEvent"] = "{0} <color=#738d43>{1}</color> вошел в атмосферу!",
                ["Crash"] = "{0} <color=#738d43>Обломки</color> обнаружены в квадрате <color=#ce3f27>{1}</color>!",
                ["NeedUseCard"] = "{0} Используйте <color=#ce3f27>космическую карту</color> чтобы разблокировать ящик!",
                ["RemainTime"] = "{0} {1} будет уничтожен через <color=#ce3f27>{2}</color>!",
                ["EndEvent"] = "{0} Ивент <color=#ce3f27>окончен</color>!",
                ["GetSpaceCard"] = "{0} Вы получили <color=#ce3f27>космическую карту</color>!",

                ["EnterPVP"] = "{0} Вы <color=#ce3f27>вошли</color> в PVP зону, теперь другие игроки <color=#ce3f27>могут</color> наносить вам урон!",
                ["ExitPVP"] = "{0} Вы <color=#738d43>вышли</color> из PVP зоны, теперь другие игроки <color=#738d43>не могут</color> наносить вам урон!",
                ["GUI"] = "Спутник будет уничтожен через <color=#ce3f27>{0}</color>",

                ["SendEconomy"] = "{0} Вы <color=#738d43>получили</color> <color=#55aaff>{1}</color> баллов в экономику за прохождение ивента",

                ["Hours"] = "ч.",
                ["Minutes"] = "м.",
                ["Seconds"] = "с.",

                ["DamageDistance"] = "{0} Подойдите <color=#ce3f27>ближе</color>!",
            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["EventActive_Exeption"] = "Cet événement est actif maintenant. Pour terminer cet événement (<color=#ce3f27>/sputikstop</color>)!",
                ["PresetNotFound_Exeption"] = "{0} le préréglage n'a pas été trouvé dans la configuration!",

                ["PreStartEvent"] = "{0} <color=#738d43>{1}</color> entrera dans l'atmosphère de <color=#738d43>{2}</color>!",
                ["StartEvent"] = "{0} <color=#738d43>{1}</color> entré dans l'atmosphère!",
                ["Crash"] = "{0} <color=#738d43>Debris</color> détecté sur le réseau <color=#ce3f27>{1}</color>!",
                ["NeedUseCard"] = "{0} Utilisez la <color=#ce3f27>space card</color> pour déverrouiller la caisse!",
                ["RemainTime"] = "{0} {1} sera détruit dans <color=#ce3f27>{2}</color>!",
                ["EndEvent"] = "{0} L'événement est <color=#ce3f27>Terminée</color>!",
                ["GetSpaceCard"] = "{0} Vous avez une <color=#ce3f27>space card</color>!",

                ["EnterPVP"] = "{0} Tu <color=#ce3f27>Entrés dans</color> la zone PVP, maintenant d'autres joueurs <color=#ce3f27>Peuve te tuer</color>!",
                ["ExitPVP"] = "{0} Tu <color=#738d43>Sort de </color> la zone PVP, maintenant les autres joueurs <color=#738d43>Ne peuve plus te tuer</color>!",
                ["GUI"] = "Le spoutnik sera détruit dans <color=#ce3f27>{0}</color>",

                ["SendEconomy"] = "{0} Vous <color=#738d43>avez gagné</color> <color=#55aaff>{1}</color> points en participant à l'événement!",

                ["Hours"] = "h.",
                ["Minutes"] = "m.",
                ["Seconds"] = "s.",

                ["DamageDistance"] = "{0} Rapprochez-vous!",

            }, this, "fr");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["EventActive_Exeption"] = "This event is active now. Finish the current event! (<color=#ce3f27>/sputikstop</color>)!",
                ["PresetNotFound_Exeption"] = "{0} preset was not found in the config!",
                ["ConfigurationNotFound_Exeption"] = "The event configuration <color=#ce3f27>could not</color> be found!",

                ["SuccessfullyLaunched"] = "The event has been <color=#738d43>successfully</color> launched!",
                ["PreStartEvent"] = "{0} <color=#738d43>{1}</color> will enter the atmosphere in <color=#738d43>{2}</color>!",
                ["StartEvent"] = "{0} <color=#738d43>{1}</color> entered the atmosphere!",
                ["Crash"] = "{0} <color=#738d43>Debris</color> detected at grid <color=#ce3f27>{1}</color>!",
                ["NeedUseCard"] = "{0} Use the <color=#ce3f27>space card</color> to unlock the crate!",
                ["RemainTime"] = "{0} {1} will be destroyed in <color=#ce3f27>{2}</color>!",
                ["EndEvent"] = "{0} The event is <color=#ce3f27>over</color>!",
                ["GetSpaceCard"] = "{0} You got a <color=#ce3f27>space card</color>!",
                ["Marker_EventOwner"] = "Event Owner: {0}",

                ["EnterPVP"] = "{0} You <color=#ce3f27>have entered</color> the PVP zone, now other players <color=#ce3f27>can damage</color> you!",
                ["ExitPVP"] = "{0} You <color=#738d43>have gone out</color> the PVP zone, now other players <color=#738d43>can’t damage</color> you!",
                ["GUI"] = "The sputnik will be destroyed in <color=#ce3f27>{0}</color>",

                ["SendEconomy"] = "{0} You <color=#738d43>have earned</color> <color=#55aaff>{1}</color> points in economics for participating in the event",
                ["Marker_EventOwner"] = "Event Owner: {0}",

                ["Hours"] = "h.",
                ["Minutes"] = "m.",
                ["Seconds"] = "s.",

                ["DamageDistance"] = "{0} Come <color=#ce3f27>closer</color>!",

                ["EventStart_Log"] = "The event has begun! (Preset displayName - {0})",
                ["EventStop_Log"] = "The event is over!",

            }, this);
        }

        internal static string GetMessage(string langKey, string userID) => ins.lang.GetMessage(langKey, ins, userID);

        internal static string GetMessage(string langKey, string userID, params object[] args) => (args.Length == 0) ? GetMessage(langKey, userID) : string.Format(GetMessage(langKey, userID), args);
        #endregion Lang

        #region Config
        private PluginConfig _config;

        protected override void LoadDefaultConfig() => _config = PluginConfig.DefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(_config, true);
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        public class MainConfig
        {
            [JsonProperty(en ? "Enable automatic event holding [true/false]" : "Включить автоматическое проведение ивента [true/false]")] public bool isAutoEvent { get; set; }
            [JsonProperty(en ? "Minimum time between events [sec]" : "Минимальное вермя между ивентами [sec]")] public int minTimeBetweenEvents { get; set; }
            [JsonProperty(en ? "Maximum time between events [sec]" : "Максимальное вермя между ивентами [sec]")] public int maxTimeBetweenEvents { get; set; }
            [JsonProperty(en ? "The time until the destruction of the debris after the looting of all the crates (0 - do not destroy) [sec]" : "Время до уничтожения обломков спутника после лутания всех ящиков (0 - не уничтожать) [sec]")] public int destroyAfterLootingTime { get; set; }
            [JsonProperty(en ? "Maximum range for damage to turrets/NPCs/mines (-1 - do not limit)" : "Максимальная дистанция для нанесения урона по турелям/нпс/минам (-1 - не ограничивать)")] public int maxGroundDamageDistance { get; set; }
            [JsonProperty(en ? "Maximum range for damage to heli (-1 - do not limit)" : "Максимальная дистанция для нанесения урона по вертолету (-1 - не ограничивать)")] public int maxHeliDamageDistance { get; set; }
            [JsonProperty(en ? "Enable logging of the start and end of the event? [true/false]" : "Включить логирование начала и окончания ивента? [true/false]")] public bool enableStartStopLogs { get; set; }
        }

        public class AgressiveConfig
        {
            [JsonProperty(en ? "Aggressive mode is active all the time" : "Агрессивный режим активен постоянно")] public bool agressiveSecurityMode { get; set; }
            [JsonProperty(en ? "The time for which the sputnik goes into aggressive mode after receiving damage" : "Время, на которое спутник переходит в агрессивных режим после получения урона")] public int agressiveTime { get; set; }
            [JsonProperty(en ? "NPCs are constantly in aggressive mode" : "НПС постоянно находятся в агрессивном режиме")] public bool npcAgressiveMode { get; set; }
            [JsonProperty(en ? "Turrets are constantly in aggressive mode" : "Турели постоянно находятся в агрессивном режиме")] public bool turretAgressiveMode { get; set; }
            [JsonProperty(en ? "Helicopters are constantly in aggressive mode" : "Вертолеты постоянно находятся в агрессивном режиме")] public bool heliAgressiveMode { get; set; }
            [JsonProperty(en ? "If one of the satellites is attacked, all the satellites will become aggressive (Useful when using settings 'Use the nearest drop points')" : "При атаке одного из спутников все спутники станут агрессивными (Полезно при использовании настройки 'Использовать ближайшие точки падения')")] public bool makeAllSputniksAgressive { get; set; }

        }

        public class SpawnConfig
        {
            [JsonProperty(en ? "Disable spawn on beaches? [true/false]" : "Отключить спавн на пляжах? [true/false]")] public bool isBeachDisbled { get; set; }
            [JsonProperty(en ? "Disable spawn on rivers/lakes? [true/false]" : "Отключить спавн на реках/озерах? [true/false]")] public bool isRiverDisbled { get; set; }
            [JsonProperty(en ? "Disable spawn on monuments? [true/false]" : "Отключить спавн на монументах? [true/false]")] public bool isMonumentsDisbled { get; set; }
            [JsonProperty(en ? "Use the nearest drop points (satellites will fall next to each other)" : "Использовать ближайшие точки падения (спутники будут падать рядом друг с другом)")] public bool isNearestPoint { get; set; }
            [JsonProperty(en ? "Number of cached spawn points" : "Число кэшированных точек спавна")] public int countSpawnPoints { get; set; }
            [JsonProperty(en ? "Minimum distance between event points" : "Минимальное расстояние между точками падения")] public float minPointDistance { get; set; }
        }

        public class FallindConfig
        {
            [JsonProperty(en ? "Falling Speed Multiplier" : "Множитель скорости падения")] public float fallingSpeedScale { get; set; }
            [JsonProperty(en ? "Minimum height of the beginning of the fall" : "Минимальная высота начала падения")] public float minFallHeight { get; set; }
            [JsonProperty(en ? "Maximum height of the beginning of the fall" : "Максимальная высота начала падения")] public float maxFallHeight { get; set; }
            [JsonProperty(en ? "Minimum offset from the vertical axis when falling" : "Минимальное смещение от вертикальной оси при падении")] public float minFallOffset { get; set; }
            [JsonProperty(en ? "Maximum offset from the vertical axis when falling" : "Максимальное смещение от вертикальной оси при падении")] public float maxFallOffset { get; set; }
            [JsonProperty(en ? "Number of effects when falling" : "Количество эффектов при падении")] public int countEffects { get; set; }
        }

        public class EventConfig
        {
            [JsonProperty(en ? "Preset name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty(en ? "Display name" : "Отображаемое имя")] public string displayName { get; set; }
            [JsonProperty(en ? "Duration [sec.]" : "Продолжительность [sec.]")] public int eventTime { get; set; }
            [JsonProperty(en ? "Probability" : "Вероятность")] public float chance { get; set; }
            [JsonProperty(en ? "The minimum time after the server's wipe when this preset can be selected automatically [sec]" : "Минимальное время после вайпа сервера, когда этот пресет может быть выбран автоматически [sec]")] public int minTimeAfterWipe { get; set; }
            [JsonProperty(en ? "The maximum time after the server's wipe when this preset can be selected automatically [sec] (-1 - do not use this parameter)" : "Максимальное время после вайпа сервера, когда этот пресет может быть выбран автоматически [sec] (-1 - не использовать)")] public int maxTimeAfterWipe { get; set; }
            [JsonProperty(en ? "Set of sputniks" : "Набор спутников")] public List<string> fixedSputniksPresets { get; set; }
        }

        public class SputnikDebrisConfig
        {
            [JsonProperty(en ? "Preset name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty(en ? "Location preset (Data file)" : "Пресет локации (Data файл)")] public string locationPreset { get; set; }
            [JsonProperty(en ? "NPC name - locations" : "Имя NPC - расположения")] public Dictionary<string, HashSet<string>> NPCs { get; set; }
            [JsonProperty(en ? "Turn on the card reader spawn? [true/false]" : "Включить спавн считывателя карт? [true/false]")] public bool enableCardReader { get; set; }
            [JsonProperty(en ? "Location of the card reader" : "Расположение считывателя карт")] public LocationConfig cardRaderLocation { get; set; }
            [JsonProperty(en ? "Heli preset name" : "Пресет вертолета")] public string heliPresetName { get; set; }
            [JsonProperty(en ? "Turret preset - locations" : "Пресет турели - расположения")] public Dictionary<string, HashSet<LocationConfig>> turrets { get; set; }
            [JsonProperty(en ? "Locations of crates with automatic ground level detection (Crate preset - locations)" : "Расположения ящиков с автоматическим определением уровня земли (Пресет крейта - расположения)")] public Dictionary<string, HashSet<LocationConfig>> groundCrates { get; set; }
            [JsonProperty(en ? "Locations of crates without automatic ground level detection (Crate preset - locations)" : "Расположения ящиков без автоматического определения уровня земли (Пресет крейта - расположения)")] public Dictionary<string, HashSet<LocationConfig>> сrates { get; set; }
            [JsonProperty(en ? "Locations of mines" : "Расположения мин")] public HashSet<string> mines { get; set; }
            [JsonProperty(en ? "Map marker setting" : "Настройка маркера на карте")] public MarkerConfig markerConfig { get; set; }
            [JsonProperty(en ? "Zone Setting" : "Настройки зоны ивента")] public ZoneConfig zoneConfig { get; set; }
            [JsonProperty(en ? "Use custom spawn points? [true/false]" : "Использовать кастомные точки спавна? [true/false]")] public bool useCustomSpawnPoints { get; set; }
            [JsonProperty(en ? "Custom spawn points" : "Кастомные точки спавна")] public List<string> customSpawnPoints { get; set; }
            [JsonProperty(en ? "IQDrone preset" : "Пресет IQDrone")] public string iqDronePreset { get; set; }
        }

        public class MarkerConfig
        {
            [JsonProperty(en ? "Do you use the Marker? [true/false]" : "Использовать ли маркер? [true/false]")] public bool enable { get; set; }
            [JsonProperty(en ? "Use a vending marker? [true/false]" : "Добавить маркер магазина? [true/false]")] public bool isShopMarker { get; set; }
            [JsonProperty(en ? "Use a circular marker? [true/false]" : "Добавить круговой маркер? [true/false]")] public bool isRingMarker { get; set; }
            [JsonProperty(en ? "Display name" : "Отображаемое имя")] public string displayName { get; set; }
            [JsonProperty(en ? "Radius" : "Радиус")] public float radius { get; set; }
            [JsonProperty(en ? "Alpha" : "Прозрачность")] public float alpha { get; set; }
            [JsonProperty(en ? "Marker color" : "Цвет маркера")] public ColorConfig color1 { get; set; }
            [JsonProperty(en ? "Outline color" : "Цвет контура")] public ColorConfig color2 { get; set; }
        }

        public class CrateConfig
        {
            [JsonProperty(en ? "Preset Name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty("Prefab")] public string prefab { get; set; }
            [JsonProperty(en ? "Do you need to use a space card to open the box? [true/false]" : "Для открытия ящика требуется применить космическую карту? [true/false]")] public bool needSpaceCard { get; set; }
            [JsonProperty(en ? "Time to unlock the crates (LockedCrate) [sec.]" : "Время до открытия заблокированного ящика (LockedCrate) [sec.]")] public float hackTime { get; set; }
            [JsonProperty(en ? "Own loot table" : "Собственная таблица предметов")] public LootTableConfig lootTableConfig { get; set; }
        }

        public class NpcConfig
        {
            [JsonProperty(en ? "Preset Name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty(en ? "Name" : "Название")] public string displayName { get; set; }
            [JsonProperty(en ? "Health" : "Кол-во ХП")] public float health { get; set; }
            [JsonProperty(en ? "Wear items" : "Одежда")] public HashSet<NpcWear> wearItems { get; set; }
            [JsonProperty(en ? "Belt items" : "Быстрые слоты")] public HashSet<NpcBelt> beltItems { get; set; }
            [JsonProperty(en ? "Kit" : "Kit")] public string kit { get; set; }
            [JsonProperty(en ? "Roam Range" : "Дальность патрулирования местности")] public float roamRange { get; set; }
            [JsonProperty(en ? "Chase Range" : "Дальность погони за целью")] public float chaseRange { get; set; }
            [JsonProperty(en ? "Attack Range Multiplier" : "Множитель радиуса атаки")] public float attackRangeMultiplier { get; set; }
            [JsonProperty(en ? "Sense Range" : "Радиус обнаружения цели")] public float senseRange { get; set; }
            [JsonProperty(en ? "Memory duration [sec.]" : "Длительность памяти цели [sec.]")] public float memoryDuration { get; set; }
            [JsonProperty(en ? "Scale damage" : "Множитель урона")] public float damageScale { get; set; }
            [JsonProperty(en ? "Aim Cone Scale" : "Множитель разброса")] public float aimConeScale { get; set; }
            [JsonProperty(en ? "Detect the target only in the NPC's viewing vision cone?" : "Обнаруживать цель только в углу обзора NPC? [true/false]")] public bool checkVisionCone { get; set; }
            [JsonProperty(en ? "Vision Cone" : "Угол обзора")] public float visionCone { get; set; }
            [JsonProperty(en ? "Speed" : "Скорость")] public float speed { get; set; }
            [JsonProperty(en ? "Should remove the corpse?" : "Удалять труп?")] public bool deleteCorpse { get; set; }
            [JsonProperty(en ? "Disable radio effects? [true/false]" : "Отключать эффекты рации? [true/false]")] public bool disableRadio { get; set; }
            [JsonProperty(en ? "Turret damage scale" : "Множитель урона от турелей")] public float turretDamageScale { get; set; }
            [JsonProperty(en ? "Own loot table" : "Собственная таблица лута")] public LootTableConfig lootTableConfig { get; set; }
        }

        public class NpcWear
        {
            [JsonProperty(en ? "ShortName" : "ShortName")] public string shortName { get; set; }
            [JsonProperty(en ? "skinID (0 - default)" : "SkinID (0 - default)")] public ulong skinID { get; set; }
        }

        public class NpcBelt
        {
            [JsonProperty(en ? "ShortName" : "ShortName")] public string shortName { get; set; }
            [JsonProperty(en ? "Amount" : "Кол-во")] public int amount { get; set; }
            [JsonProperty(en ? "skin (0 - default)" : "SkinID (0 - default)")] public ulong skinID { get; set; }
            [JsonProperty(en ? "Mods" : "Модификации на оружие")] public HashSet<string> mods { get; set; }
            [JsonProperty(en ? "Ammo" : "Патроны")] public string ammo { get; set; }
        }

        public class HeliConfig
        {
            [JsonProperty(en ? "Name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty("HP")] public float hp { get; set; }
            [JsonProperty(en ? "HP of the main rotor" : "HP главного винта")] public float mainRotorHealth { get; set; }
            [JsonProperty(en ? "HP of tail rotor" : "HP хвостового винта")] public float rearRotorHealth { get; set; }
            [JsonProperty(en ? "Numbers of crates" : "Количество ящиков")] public int cratesAmount { get; set; }
            [JsonProperty(en ? "Flying height" : "Высота полета")] public float height { get; set; }
            [JsonProperty(en ? "Bullet speed" : "Скорость пуль")] public float bulletSpeed { get; set; }
            [JsonProperty(en ? "Bullet Damage" : "Урон пуль")] public float bulletDamage { get; set; }
            [JsonProperty(en ? "The distance to which the helicopter can move away from the sputnik" : "Дистанция, на которую вертолет может отдаляться от спутника")] public float distance { get; set; }
            [JsonProperty(en ? "The time for which the helicopter can leave the satellite to attack the target [sec.]" : "Время, на которое верталет может покидать спутник для атаки цели [sec.]")] public float outsideTime { get; set; }
            [JsonProperty(en ? "Open the crates immediately after spawn" : "Открывать ящики сразу после спавна")] public bool instCrateOpen { get; set; }
            [JsonProperty(en ? "Own loot table" : "Собственная таблица предметов")] public BaseLootTableConfig baseLootTableConfig { get; set; }
        }

        public class TurretConfig
        {
            [JsonProperty(en ? "Preset Name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty(en ? "Choose the spawn height automatically?" : "Выбирать высоту спавна автоматически?")] public bool autoHeight { get; set; }
            [JsonProperty(en ? "Health" : "Кол-во ХП")] public float hp { get; set; }
            [JsonProperty(en ? "Weapon ShortName" : "ShortName оружия")] public string shortNameWeapon { get; set; }
            [JsonProperty(en ? "Ammo ShortName" : "ShortName патронов")] public string shortNameAmmo { get; set; }
            [JsonProperty(en ? "Number of ammo" : "Кол-во патронов")] public int countAmmo { get; set; }
            [JsonProperty(en ? "Target detection range (0 - do not change)" : "Дальность обнаружения цели (0 - не изменять)")] public float targetDetectionRange { get; set; }
            [JsonProperty(en ? "Target loss range (0 - do not change)" : "Дальность потери цели (0 - не изменять)")] public float targetLossRange { get; set; }
        }

        public class ZoneConfig
        {
            [JsonProperty(en ? "Create a PVP zone? (only for those who use the TruePVE plugin)[true/false]" : "Создавать зону PVP в зоне проведения ивента? (только для тех, кто использует плагин TruePVE) [true/false]")] public bool isPVPZone { get; set; }
            [JsonProperty(en ? "Use the dome? [true/false]" : "Использовать ли купол? [true/false]")] public bool isDome { get; set; }
            [JsonProperty(en ? "Darkening the dome" : "Затемнение купола")] public int darkening { get; set; }
            [JsonProperty(en ? "Use a colored border? [true/false]" : "Использовать цветную границу? [true/false]")] public bool isColoredBorder { get; set; }
            [JsonProperty(en ? "Border color (0 - blue, 1 - green, 2 - purple, 3 - red)" : "Цвет границы (0 - синий, 1 - зеленый, 2 - фиолетовый, 3 - красный)")] public int borderColor { get; set; }
            [JsonProperty(en ? "Brightness of the color border" : "Яркость цветной границы")] public int brightness { get; set; }
            [JsonProperty(en ? "Radius" : "Радиус")] public float radius { get; set; }
            [JsonProperty(en ? "Radiation power" : "Сила радиации")] public float radiation { get; set; }
        }

        public class GUIConfig
        {
            [JsonProperty(en ? "Use the Countdown GUI? [true/false]" : "Использовать ли GUI обратного отсчета? [true/false]")] public bool isEnable { get; set; }
            [JsonProperty(en ? "Vertical offset" : "Смещение по вертикали")] public int offsetMinY { get; set; }
        }

        public class NotifyConfig
        {
            [JsonProperty(en ? "The time from the notification to the start of the event [sec]" : "Время от оповещения до начала ивента [sec]")] public int preStartTime { get; set; }
            [JsonProperty(en ? "Use a Chat? [true/false]" : "Использовать ли чат? [true/false]")] public bool isChatEnable { get; set; }
            [JsonProperty(en ? "The time until the end of the event, when a message is displayed about the time until the end of the event [sec]" : "Время до конца ивента, когда выводится сообщение о сокром окончании ивента [sec]")] public HashSet<int> timeNotifications { get; set; }
            [JsonProperty(en ? "Facepunch Game Tips setting" : "Настройка сообщений Facepunch Game Tip")] public GameTipConfig gameTipConfig { get; set; }

        }

        public class GameTipConfig
        {
            [JsonProperty(en ? "Use Facepunch Game Tips (notification bar above hotbar)? [true/false]" : "Использовать ли Facepunch Game Tip (оповещения над слотами быстрого доступа игрока)? [true/false]")] public bool isEnabled { get; set; }
            [JsonProperty(en ? "Style (0 - Blue Normal, 1 - Red Normal, 2 - Blue Long, 3 - Blue Short, 4 - Server Event)" : "Стиль (0 - Blue Normal, 1 - Red Normal, 2 - Blue Long, 3 - Blue Short, 4 - Server Event)")] public int style { get; set; }
        }

        public class ColorConfig
        {
            [JsonProperty("r")] public float r { get; set; }
            [JsonProperty("g")] public float g { get; set; }
            [JsonProperty("b")] public float b { get; set; }
        }

        public class LocationConfig
        {
            [JsonProperty(en ? "Position" : "Позиция")] public string position { get; set; }
            [JsonProperty(en ? "Rotation" : "Вращение")] public string rotation { get; set; }
        }

        public class LootTableConfig : BaseLootTableConfig
        {
            [JsonProperty(en ? "Allow the AlphaLoot plugin to spawn items in this crate" : "Разрешить плагину AlphaLoot спавнить предметы в этом ящике")] public bool isAlphaLoot { get; set; }
            [JsonProperty(en ? "The name of the loot preset for AlphaLoot" : "Название пресета лута AlphaLoot")] public string alphaLootPresetName { get; set; }
            [JsonProperty(en ? "Allow the CustomLoot plugin to spawn items in this crate" : "Разрешить плагину CustomLoot спавнить предметы в этом ящике")] public bool isCustomLoot { get; set; }
            [JsonProperty(en ? "Allow the Loot Table Stacksize GUI plugin to spawn items in this crate" : "Разрешить плагину Loot Table Stacksize GUI спавнить предметы в этом ящике")] public bool isLootTablePLugin { get; set; }
        }

        public class BaseLootTableConfig
        {
            [JsonProperty(en ? "Clear the standard content of the crate" : "Отчистить стандартное содержимое крейта")] public bool clearDefaultItemList { get; set; }
            [JsonProperty(en ? "Setting up loot from the loot table" : "Настройка лута из лутовой таблицы")] public PrefabLootTableConfigs prefabConfigs { get; set; }
            [JsonProperty(en ? "Enable spawn of items from the list" : "Включить спавн предметов из списка")] public bool isRandomItemsEnable { get; set; }
            [JsonProperty(en ? "Minimum numbers of items" : "Минимальное кол-во элементов")] public int minItemsAmount { get; set; }
            [JsonProperty(en ? "Maximum numbers of items" : "Максимальное кол-во элементов")] public int maxItemsAmount { get; set; }
            [JsonProperty(en ? "List of items" : "Список предметов")] public List<LootItemConfig> items { get; set; }
        }

        public class PrefabLootTableConfigs
        {
            [JsonProperty(en ? "Enable spawn loot from prefabs" : "Включить спавн лута из префабов")] public bool isEnable { get; set; }
            [JsonProperty(en ? "List of prefabs (one is randomly selected)" : "Список префабов (выбирается один рандомно)")] public List<PrefabConfig> prefabs { get; set; }
        }

        public class PrefabConfig
        {
            [JsonProperty(en ? "Prefab displayName" : "Название префаба")] public string prefabName { get; set; }
            [JsonProperty(en ? "Minimum Loot multiplier" : "Минимальный множитель лута")] public int minLootScale { get; set; }
            [JsonProperty(en ? "Maximum Loot multiplier" : "Максимальный множитель лута")] public int maxLootScale { get; set; }
        }

        public class LootItemConfig
        {
            [JsonProperty("ShortName")] public string shortname { get; set; }
            [JsonProperty(en ? "Minimum" : "Минимальное кол-во")] public int minAmount { get; set; }
            [JsonProperty(en ? "Maximum" : "Максимальное кол-во")] public int maxAmount { get; set; }
            [JsonProperty(en ? "Chance [0.0-100.0]" : "Шанс выпадения предмета [0.0-100.0]")] public float chance { get; set; }
            [JsonProperty(en ? "Is this a blueprint? [true/false]" : "Это чертеж? [true/false]")] public bool isBlueprint { get; set; }
            [JsonProperty("SkinID (0 - default)")] public ulong skin { get; set; }
            [JsonProperty(en ? "Name (empty - default)" : "Название (empty - default)")] public string name { get; set; }
            [JsonProperty(en ? "List of genomes" : "Список геномов")] public List<string> genomes { get; set; }
        }

        public class CustomCardConfig
        {
            [JsonProperty("ShortName")] public string shortName { get; set; }
            [JsonProperty(en ? "Name (empty - default)" : "Название (empty - default)")] public string name { get; set; }
            [JsonProperty("SkinID (0 - default)")] public ulong skinID { get; set; }
            [JsonProperty(en ? "Multiplier of card health loss when using" : "Множитель потери прочности карты при использовании")] public float helthLossScale { get; set; }
            [JsonProperty(en ? "Enable spawn in crates" : "Включить спавн в ящиках")] public bool enableSpawnInDefaultCrates { get; set; }
            [JsonProperty(en ? "Setting up spawn in crates (prefab - probability)" : "Настройка спавна в ящиках (префаб - вероятность)")] public Dictionary<string, float> spawnSetting { get; set; }
        }

        public class SupportedPluginsConfig
        {
            [JsonProperty(en ? "PVE Mode Setting" : "Настройка PVE Mode")] public PveModeConfig pveMode { get; set; }
            [JsonProperty(en ? "Economy Setting" : "Настройка экономики")] public EconomyConfig economicsConfig { get; set; }
            [JsonProperty(en ? "GUI Announcements setting" : "Настройка GUI Announcements")] public GUIAnnouncementsConfig guiAnnouncementsConfig { get; set; }
            [JsonProperty(en ? "Notify setting" : "Настройка Notify")] public NotifyPluginConfig notifyPluginConfig { get; set; }
            [JsonProperty(en ? "DiscordMessages setting" : "Настройка DiscordMessages")] public DiscordConfig discordMessagesConfig { get; set; }
            [JsonProperty(en ? "ZoneManager setting" : "Настройка ZoneManager")] public ZoneManagerConfig zoneManager { get; set; }
            [JsonProperty(en ? "RaidableBases setting" : "Настройка RaidableBases")] public RaidableBasesConfig raidableBases { get; set; }
            [JsonProperty(en ? "RestoreUponDeath setting" : "Настройка RestoreUponDeath")] public RestoreUponDeathConfig restoreUponDeath { get; set; }
            [JsonProperty(en ? "BetterNpc setting" : "Настройка BetterNpc")] public BetterNpcConfig betterNpcConfig { get; set; }
            [JsonProperty(en ? "IQDronePatrol setting" : "Настройка IQDronePatrol")] public IQDroneConfig iqDroneConfig { get; set; }
        }

        public class PveModeConfig
        {
            [JsonProperty(en ? "Use the PVE mode of the plugin? [true/false]" : "Использовать PVE режим работы плагина? [true/false]")] public bool enable { get; set; }
            [JsonProperty(en ? "Show the displayName of the event owner on a marker on the map? [true/false]" : "Отображать имя владелца ивента на маркере на карте? [true/false]")] public bool showEventOwnerNameOnMap { get; set; }
            [JsonProperty(en ? "The amount of damage that the player has to do to become the Event Owner" : "Кол-во урона, которое должен нанести игрок, чтобы стать владельцем ивента")] public float damage { get; set; }
            [JsonProperty(en ? "Damage coefficients for calculate to become the Event Owner." : "Коэффициенты урона для подсчета, чтобы стать владельцем события.")] public Dictionary<string, float> scaleDamage { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event loot the crates? [true/false]" : "Может ли не владелец ивента грабить ящики? [true/false]")] public bool lootCrate { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event hack locked crates? [true/false]" : "Может ли не владелец ивента взламывать заблокированные ящики? [true/false]")] public bool hackCrate { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event loot NPC corpses? [true/false]" : "Может ли не владелец ивента грабить трупы NPC? [true/false]")] public bool lootNpc { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event deal damage to the NPC? [true/false]" : "Может ли не владелец ивента наносить урон по NPC? [true/false]")] public bool damageNpc { get; set; }
            [JsonProperty(en ? "Can an Npc attack a non-owner of the event? [true/false]" : "Может ли Npc атаковать не владельца ивента? [true/false]")] public bool targetNpc { get; set; }
            [JsonProperty(en ? "Can Helicopter attack a non-owner of the event? [true/false]" : "Может ли Вертолет атаковать не владельца ивента? [true/false]")] public bool targetHeli { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event do damage to Helicopter? [true/false]" : "Может ли не владелец ивента наносить урон по Вертолету? [true/false]")] public bool damageHeli { get; set; }
            [JsonProperty(en ? "Can Turret attack a non-owner of the event? [true/false]" : "Может ли Турель атаковать не владельца ивента? [true/false]")] public bool targetTurret { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event do damage to Turret? [true/false]" : "Может ли не владелец ивента наносить урон по Турелям? [true/false]")] public bool damageTurret { get; set; }
            [JsonProperty(en ? "Allow the non-owner of the event to enter the event zone? [true/false]" : "Разрешать входить внутрь зоны ивента не владельцу ивента? [true/false]")] public bool canEnter { get; set; }
            [JsonProperty(en ? "Allow a player who has an active cooldown of the Event Owner to enter the event zone? [true/false]" : "Разрешать входить внутрь зоны ивента игроку, у которого активен кулдаун на получение статуса владельца ивента? [true/false]")] public bool canEnterCooldownPlayer { get; set; }
            [JsonProperty(en ? "The time that the Event Owner may not be inside the event zone [sec.]" : "Время, которое владелец ивента может не находиться внутри зоны ивента [сек.]")] public int timeExitOwner { get; set; }
            [JsonProperty(en ? "The time until the end of Event Owner status when it is necessary to warn the player [sec.]" : "Время таймера до окончания действия статуса владельца ивента, когда необходимо предупредить игрока [сек.]")] public int alertTime { get; set; }
            [JsonProperty(en ? "The time that the player can`t become the Event Owner, after the end of the event and the player was its owner [sec.]" : "Время, которое игрок не сможет стать владельцем ивента, после того как ивент окончен и игрок был его владельцем [sec.]")] public double cooldownOwner { get; set; }
        }

        public class EconomyConfig
        {
            [JsonProperty(en ? "Enable economy" : "Включить экономику?")] public bool enable { get; set; }
            [JsonProperty(en ? "Which economy plugins do you want to use? (Economics, Server Rewards, IQEconomic)" : "Какие плагины экономики вы хотите использовать? (Economics, Server Rewards, IQEconomic)")] public HashSet<string> plugins { get; set; }
            [JsonProperty(en ? "The minimum value that a player must collect to get points for the economy" : "Минимальное значение, которое игрок должен заработать, чтобы получить баллы за экономику")] public double minEconomyPiont { get; set; }
            [JsonProperty(en ? "The minimum value that a winner must collect to make the commands work" : "Минимальное значение, которое победитель должен заработать, чтобы сработали команды")] public double minCommandPoint { get; set; }
            [JsonProperty(en ? "Looting of crates" : "Ограбление ящиков")] public Dictionary<string, double> crates { get; set; }
            [JsonProperty(en ? "Killing an NPC" : "Убийство NPC")] public double npcPoint { get; set; }
            [JsonProperty(en ? "Killing an Turret" : "Уничтожение Турели")] public double turretPoint { get; set; }
            [JsonProperty(en ? "Killing an Heli" : "Уничтожение Вертолета")] public double heliPoint { get; set; }
            [JsonProperty(en ? "Hacking a locked crate" : "Взлом заблокированного ящика")] public double hackCratePoint { get; set; }
            [JsonProperty(en ? "List of commands that are executed in the console at the end of the event ({steamid} - the player who collected the highest number of points)" : "Список команд, которые выполняются в консоли по окончанию ивента ({steamid} - игрок, который набрал наибольшее кол-во баллов)")] public HashSet<string> commands { get; set; }
        }

        public class GUIAnnouncementsConfig
        {
            [JsonProperty(en ? "Do you use the GUI Announcements? [true/false]" : "Использовать ли GUI Announcements? [true/false]")] public bool isEnabled { get; set; }
            [JsonProperty(en ? "Banner color" : "Цвет баннера")] public string bannerColor { get; set; }
            [JsonProperty(en ? "Text color" : "Цвет текста")] public string textColor { get; set; }
            [JsonProperty(en ? "Adjust Vertical Position" : "Отступ от верхнего края")] public float apiAdjustVPosition { get; set; }
        }

        public class NotifyPluginConfig
        {
            [JsonProperty(en ? "Do you use the Notify? [true/false]" : "Использовать ли Notify? [true/false]")] public bool isEnabled { get; set; }
            [JsonProperty(en ? "Type" : "Тип")] public int type { get; set; }
        }

        public class DiscordConfig
        {
            [JsonProperty(en ? "Do you use the Discord? [true/false]" : "Использовать ли Discord? [true/false]")] public bool isEnabled { get; set; }
            [JsonProperty("Webhook URL")] public string webhookUrl { get; set; }
            [JsonProperty(en ? "Embed Color (DECIMAL)" : "Цвет полосы (DECIMAL)")] public int embedColor { get; set; }
            [JsonProperty(en ? "Keys of required messages" : "Ключи необходимых сообщений")] public HashSet<string> keys { get; set; }
        }

        public class ZoneManagerConfig
        {
            [JsonProperty(en ? "Do you use the ZoneManager? [true/false]" : "Использовать ли ZoneManager? [true/false]")] public bool enable { get; set; }
            [JsonProperty(en ? "List of zone flags that block spawn" : "Список флагов, при наличии в зоне которого спутник не будет спавниться")] public HashSet<string> blockFlags { get; set; }
            [JsonProperty(en ? "List of zone IDs that block spawn" : "Список ID зон, которые запретят спавн спутника")] public HashSet<string> blockIDs { get; set; }
        }

        public class RaidableBasesConfig
        {
            [JsonProperty(en ? "Do you use the RaidableBases? [true/false]" : "Использовать ли RaidableBases? [true/false]")] public bool enable { get; set; }
        }

        public class RestoreUponDeathConfig
        {
            [JsonProperty(en ? "Prevent the actions of the RestoreUponDeath plugin in the event zone? [true/false]" : "Запрещать работу плагина RestoreUponDeath в зоне действия ивента? [true/false]")] public bool disableRestore { get; set; }
        }

        public class BetterNpcConfig
        {
            [JsonProperty(en ? "Allow Npc spawn after destroying Heli" : "Разрешить спавн Npc после уничтожения Вертолета")] public bool isHeliNpc { get; set; }
        }

        public class IQDroneConfig
        {
            [JsonProperty(en ? "Allow drone spawn?" : "Разрешить спавн дронов")] public bool isDroneEnable { get; set; }
            [JsonProperty(en ? "Presets" : "Пресеты")] public HashSet<IQDronePreset> dronePresets { get; set; }

        }

        public class IQDronePreset
        {
            [JsonProperty(en ? "Preset Name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty(en ? "Number of drones" : "Количество дронов")] public int droneNumber { get; set; }
            [JsonProperty(en ? "The number of drones that can be targeted at the same time" : "Количество дронов, которые могут таргетится одновременно")] public int targetDroneNumber { get; set; }
            [JsonProperty(en ? "Drone Key - probability [0 - 100]" : "Ключ дрона - вероятность [0 - 100]")] public Dictionary<string, int> droneKeys { get; set; }
        }

        private class PluginConfig
        {
            [JsonProperty(en ? "Version" : "Версия")] public VersionNumber version { get; set; }
            [JsonProperty(en ? "Prefix of chat messages" : "Префикс в чате")] public string prefix { get; set; }
            [JsonProperty(en ? "Main Setting" : "Основные настройки")] public MainConfig mainConfig { get; set; }
            [JsonProperty(en ? "Settings of the event aggression" : "Настройка агрессивности ивента")] public AgressiveConfig agressiveConfig { get; set; }
            [JsonProperty(en ? "Settings of the falling sputnik" : "Настройка падения спутника")] public FallindConfig fallindConfig { get; set; }
            [JsonProperty(en ? "Settings of the spawning sputnik debris" : "Настройка спавна обломков спутника")] public SpawnConfig spawnConfig { get; set; }
            [JsonProperty(en ? "Event presets" : "Пресеты ивента")] public HashSet<EventConfig> eventConfigs { get; set; }
            [JsonProperty(en ? "Sputnik Debris Presets" : "Пресеты обломков спутников")] public HashSet<SputnikDebrisConfig> sputnikDebrisConfigs { get; set; }
            [JsonProperty(en ? "Space card setting" : "Настройка космической карты")] public CustomCardConfig customCardConfig { get; set; }
            [JsonProperty(en ? "Crate presets" : "Пресеты ящиков")] public HashSet<CrateConfig> crateConfigs { get; set; }
            [JsonProperty(en ? "NPC presets" : "Пресеты NPC")] public HashSet<NpcConfig> npcConfigs { get; set; }
            [JsonProperty(en ? "Heli presets" : "Пресеты вертолетов")] public HashSet<HeliConfig> heliConfigs { get; set; }
            [JsonProperty(en ? "Turrets presets" : "Пресеты турелей")] public HashSet<TurretConfig> turretConfigs { get; set; }
            [JsonProperty(en ? "Notification Settings" : "Настройки уведомлений")] public NotifyConfig notifyConfig { get; set; }
            [JsonProperty(en ? "GUI Setting" : "Настройки GUI")] public GUIConfig guiConfig { get; set; }
            [JsonProperty(en ? "Supported Plugins" : "Поддерживаемые плагины")] public SupportedPluginsConfig supportedPluginsConfig { get; set; }

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    version = new VersionNumber(1, 4, 8),
                    prefix = "[Sputnik]",
                    mainConfig = new MainConfig
                    {
                        isAutoEvent = true,
                        minTimeBetweenEvents = 7200,
                        maxTimeBetweenEvents = 7200,
                        destroyAfterLootingTime = 300,
                        maxGroundDamageDistance = 100,
                        maxHeliDamageDistance = 250,
                        enableStartStopLogs = false,
                    },
                    agressiveConfig = new AgressiveConfig
                    {
                        agressiveSecurityMode = false,
                        agressiveTime = 120,
                        npcAgressiveMode = true,
                        turretAgressiveMode = true,
                        heliAgressiveMode = false,
                    },
                    fallindConfig = new FallindConfig()
                    {
                        fallingSpeedScale = 1,
                        minFallHeight = 500,
                        maxFallHeight = 1000,
                        minFallOffset = 200,
                        maxFallOffset = 300,
                        countEffects = 10
                    },
                    spawnConfig = new SpawnConfig
                    {
                        isBeachDisbled = true,
                        isRiverDisbled = true,
                        isMonumentsDisbled = true,
                        isNearestPoint = true,
                        countSpawnPoints = 25,
                        minPointDistance = 50,
                    },
                    eventConfigs = new HashSet<EventConfig>
                    {
                        new EventConfig
                        {
                            presetName = "station",
                            displayName = en ? "Fragment of the space station" : "Обломок космической станции",
                            eventTime = 3600,
                            chance = 20,
                            minTimeAfterWipe = 0,
                            maxTimeAfterWipe = 172800,
                            fixedSputniksPresets = new List<string>
                            {
                                "debris_2",
                                "debris_3",
                                "debris_4",
                            }
                        },
                        new EventConfig
                        {
                            presetName = "sputnik",
                            displayName = en ? "Sputnik" : "Спутник",
                            eventTime = 3600,
                            chance = 30,
                            minTimeAfterWipe = 0,
                            maxTimeAfterWipe = -1,
                            fixedSputniksPresets = new List<string>
                            {
                                "sputnik_1",
                                "debris_1",
                            }
                        },
                        new EventConfig
                        {
                            presetName = "spaceship",
                            displayName = en ? "Spaceship" : "Космический корабль",
                            eventTime = 3600,
                            chance = 30,
                            minTimeAfterWipe = 0,
                            maxTimeAfterWipe = -1,
                            fixedSputniksPresets = new List<string>
                            {
                                "sputnik_1",
                                "debris_1",
                                "debris_4",
                            }
                        },
                        new EventConfig
                        {
                            presetName = "big_sputnik",
                            displayName = en ? "Huge sputnik" : "Огромный спутник",
                            eventTime = 3600,
                            chance = 20,
                            minTimeAfterWipe = 0,
                            maxTimeAfterWipe = -1,
                            fixedSputniksPresets = new List<string>
                            {
                                "sputnik_1",
                                "debris_1",
                                "debris_3",
                                "debris_4",
                            }
                        }
                    },
                    sputnikDebrisConfigs = new HashSet<SputnikDebrisConfig>
                    {
                        new SputnikDebrisConfig
                        {
                            presetName = "sputnik_1",
                            locationPreset = "sputnik_1",
                            heliPresetName = "heli_1",
                            turrets = new Dictionary<string, HashSet<LocationConfig>>
                            {
                                ["turret_ak"] = new HashSet<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        position = "(-0.103, 0, 4.888)",
                                        rotation = "(0, 88, 0)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(-3.922, 0, 7.955)",
                                        rotation = "(0, 0, 0)"
                                    }
                                }
                            },
                            enableCardReader = true,
                            cardRaderLocation = new LocationConfig
                            {
                                position = "(-3.69, 0.055, 7.180)",
                                rotation = "(341.285, 38.870, 0.638)"
                            },
                            NPCs = new Dictionary<string, HashSet<string>>
                            {
                                ["cosmonaut"] = new HashSet<string>
                                {
                                    "(4, 0, -1)",
                                    "(5, 0, 2.5)",
                                    "(1.5, 0, -1.5)",
                                    "(0.5, 0, 3.3)",
                                    "(-2.1, 0, 1.1)",
                                    "(-0.15, 0, 5.6)",
                                    "(-6.7, 0, 3.3)",
                                    "(-3.5, 0, 7.8)"
                                }
                            },
                            groundCrates = new Dictionary<string, HashSet<LocationConfig>>
                            {
                                ["crateelite_default"] = new HashSet<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        position = "(0.0, 0.0, 1.9)",
                                        rotation = "(0.0, 0.0, 0.0)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(-2.9, 0.0, 2.0)",
                                        rotation = "(0.0, 115.9, 0.0)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(2.5, 0.0, -3.2)",
                                        rotation = "(0.0, 0.0, 0.0)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(-4.0, 0.0, -1.6)",
                                        rotation = "(0.0, 58.8, 0.0)"
                                    }
                                }
                            },
                            сrates = new Dictionary<string, HashSet<LocationConfig>>
                            {
                                ["chinooklockedcrate_spacecard"] = new HashSet<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        position = "(-3.279, 0.487, 6.231)",
                                        rotation = "(340.056, 38.203, 0)"
                                    }
                                },
                            },
                            mines = new HashSet<string>
                            {
                                "(1.7, 0.0, 4.3)",
                                "(0.3, 0.0, 4.3)",
                                "(2.6, 0.0, 1.0)",
                                "(0.7, 0.0, 2.5)",
                                "(-7.2, 0.0, 6.6)",
                                "(-7.2, 0.0, 4.2)",
                                "(-0.9, 0.0, 6.1)",
                                "(-2.1, 0.0, 7.1)",
                                "(-3.6, 0.0, 8.2)",
                                "(-5.8, 0.0, 6.6)",
                                "(-1.7, 0.0, 0.5)",
                                "(-3.9, 0.0, 0.5)",
                                "(-4.4, 0.0, 2.3)",
                                "(-4.4, 0.0, 4.2)",
                                "(2.6, 0.0, -1.4)",
                                "(4.9, 0.0, -4.0)",
                                "(2.1, 0.0, -6.3)",
                                "(2.0, 0.0, -8.2)",
                                "(-1.7, 0.0, -1.4)",
                                "(-0.2, 0.0, -8.7)",
                                "(-3.4, 0.0, -8.7)",
                                "(-3.9, 0.0, -6.3)",
                                "(-5.4, 0.0, -2.6)",
                                "(-8.6, 0.0, -0.8)"
                            },
                            markerConfig = new MarkerConfig
                            {
                                enable = true,
                                isRingMarker = true,
                                isShopMarker = true,
                                displayName = en ? "Sputnik" : "Спутник",
                                radius = 0.25f,
                                alpha = 0.6f,
                                color1 = new ColorConfig { r = 0.81f, g = 0.25f, b = 0.15f },
                                color2 = new ColorConfig { r = 0f, g = 0f, b = 0f }
                            },
                            zoneConfig = new ZoneConfig
                            {
                                isPVPZone = false,
                                isDome = false,
                                darkening = 5,
                                isColoredBorder = false,
                                brightness = 5,
                                borderColor = 2,
                                radius = 25,
                                radiation = 10
                            },
                            useCustomSpawnPoints = false,
                            customSpawnPoints = new List<string>(),
                            iqDronePreset = ""
                        },
                        new SputnikDebrisConfig
                        {
                            presetName = "debris_1",
                            locationPreset = "debris_1",
                            heliPresetName = "",
                            turrets = new Dictionary<string, HashSet<LocationConfig>>(),
                            enableCardReader = false,
                            cardRaderLocation = new LocationConfig
                            {
                                position = "(0, 0, 0)",
                                rotation = "(0, 0, 0)"
                            },
                            NPCs = new Dictionary<string, HashSet<string>>
                            {
                                ["cosmonaut"] = new HashSet<string>
                                {
                                    "(0.9, 0.0, 3.6)",
                                    "(3.7, 0.0, -0.7)",
                                    "(-1.9, 0.0, -3.2)",
                                    "(-0.1, 0.0, -1.9)",
                                    "(-0.5, -12.1, -9.2)",
                                    "(-4.5, 0.0, -0.5)"
                                }
                            },
                            groundCrates = new Dictionary<string, HashSet<LocationConfig>>
                            {
                            },
                            сrates = new Dictionary<string, HashSet<LocationConfig>>
                            {
                                ["cratenormal_underwater_1"] = new HashSet<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        position = "(2.2, 0.0, 3.0)",
                                        rotation = "(0.0, 344.2, 0.0)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(-1.4, 0.0, -2.2)",
                                        rotation = "(0.0, 333.5, 0.0)"
                                    }
                                },
                                ["cratenormal_underwater_2"] = new HashSet<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        position = "(-0.4, 0.0, 0.2)",
                                        rotation = "(0.0, 0, 0.0)"
                                    }
                                }
                            },
                            mines = new HashSet<string>
                            {
                                "(4.9, 0.0, 2.1)",
                                "(4.7, 0.0, 0.8)",
                                "(3.5, 0.0, 2.6)",
                                "(1.8, 0.0, 3.6)",
                                "(1.3, 0.0, 2.7)",
                                "(0.1, 0.0, 2.4)",
                                "(-1.8, 0.0, 3.2)",
                                "(-4.2, 0.0, 0.7)",
                                "(3.4, 0.0, -0.4)",
                                "(4.4, 0.0, -2.0)",
                                "(2.6, 0.0, -3.0)",
                                "(2.9, 0.0, -4.5)",
                                "(-5.4, 0.0, -2.5)",
                                "(-0.1, 0.0, -5.4)",
                                "(-2.7, 0.0, -4.6)",
                                "(-0.9, 0.0, -2.5)",

                            },
                            markerConfig = new MarkerConfig
                            {
                                enable = true,
                                isRingMarker = true,
                                isShopMarker = true,
                                displayName = en ? "(Space Card) Radioactive space debris" : "(Космическая карта) Радиоактивные космические обломки",
                                radius = 0.2f,
                                alpha = 0.6f,
                                color1 = new ColorConfig { r = 0.81f, g = 0.25f, b = 0.15f },
                                color2 = new ColorConfig { r = 0f, g = 0f, b = 0f }
                            },
                            zoneConfig = new ZoneConfig
                            {
                                isPVPZone = false,
                                isDome = false,
                                darkening = 5,
                                isColoredBorder = false,
                                brightness = 5,
                                borderColor = 2,
                                radius = 25,
                                radiation = 10
                            },
                            useCustomSpawnPoints = false,
                            customSpawnPoints = new List<string>(),
                            iqDronePreset = ""
                        },
                        new SputnikDebrisConfig
                        {
                            presetName = "debris_2",
                            locationPreset = "debris_2",
                            heliPresetName = "",
                            turrets = new Dictionary<string, HashSet<LocationConfig>>(),
                            enableCardReader = false,
                            cardRaderLocation = new LocationConfig
                            {
                                position = "(0, 0, 0)",
                                rotation = "(0, 0, 0)"
                            },
                            NPCs = new Dictionary<string, HashSet<string>>
                            {
                                ["cosmonaut"] = new HashSet<string>
                                {
                                    "(0, 0, -2)",
                                    "(-1, 0, 3)",
                                    "(3, 0, 0)",
                                }
                            },
                            groundCrates = new Dictionary<string, HashSet<LocationConfig>>
                            {
                                ["cratenormal_default"] = new HashSet<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        position = "(-2.2, 0, -1.8)",
                                        rotation = "(0, 50, 0)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(0.75, 0, 2.25)",
                                        rotation = "(0, 50, 0)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(3, 0, -2.25)",
                                        rotation = "(0, 304, 0)"
                                    }
                                },
                            },
                            сrates = new Dictionary<string, HashSet<LocationConfig>>
                            {
                            },
                            mines = new HashSet<string>(),
                            markerConfig = new MarkerConfig
                            {
                                enable = true,
                                isRingMarker = true,
                                isShopMarker = true,
                                displayName = en ? "Space debris" : "Космические обломки",
                                radius = 0.2f,
                                alpha = 0.6f,
                                color1 = new ColorConfig { r = 0.81f, g = 0.25f, b = 0.15f },
                                color2 = new ColorConfig { r = 0f, g = 0f, b = 0f }
                            },
                            zoneConfig = new ZoneConfig
                            {
                                isPVPZone = false,
                                isDome = false,
                                darkening = 5,
                                isColoredBorder = false,
                                brightness = 5,
                                borderColor = 2,
                                radius = 25,
                                radiation = 0
                            },
                            useCustomSpawnPoints = false,
                            customSpawnPoints = new List<string>(),
                            iqDronePreset = ""
                        },
                        new SputnikDebrisConfig
                        {
                            presetName = "debris_3",
                            locationPreset = "debris_3",
                            heliPresetName = "",
                            turrets = new Dictionary<string, HashSet<LocationConfig>>(),
                            enableCardReader = false,
                            cardRaderLocation = new LocationConfig
                            {
                                position = "(0, 0, 0)",
                                rotation = "(0, 0, 0)"
                            },
                            NPCs = new Dictionary<string, HashSet<string>>
                            {
                                ["cosmonaut"] = new HashSet<string>
                                {
                                    "(-1.621, 0, 1.95)",
                                    "(1.08, 0, -2.04)",
                                    "(0.111, 0, 2.941)"
                                }
                            },
                            groundCrates = new Dictionary<string, HashSet<LocationConfig>>
                            {
                                ["cratenormal_default"] = new HashSet<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        position = "(0.13, 0, 1.9)",
                                        rotation = "(0, 50, 0)"
                                    }
                                },
                                ["crateelite_default"] = new HashSet<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        position = "(1.465, 0.0, -0.625)",
                                        rotation = "(0.0, 290, 0.0)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(-0.264, 0.0, -1.919)",
                                        rotation = "(0.0, 0, 0.0)"
                                    }
                                }
                            },
                            сrates = new Dictionary<string, HashSet<LocationConfig>>
                            {
                            },
                            mines = new HashSet<string>(),
                            markerConfig = new MarkerConfig
                            {
                                enable = true,
                                isRingMarker = true,
                                isShopMarker = true,
                                displayName = en ? "Space debris" : "Космические обломки",
                                radius = 0.2f,
                                alpha = 0.6f,
                                color1 = new ColorConfig { r = 0.81f, g = 0.25f, b = 0.15f },
                                color2 = new ColorConfig { r = 0f, g = 0f, b = 0f }
                            },
                            zoneConfig = new ZoneConfig
                            {
                                isPVPZone = false,
                                isDome = false,
                                darkening = 5,
                                isColoredBorder = false,
                                brightness = 5,
                                borderColor = 2,
                                radius = 25,
                                radiation = 0
                            },
                            useCustomSpawnPoints = false,
                            customSpawnPoints = new List<string>(),
                            iqDronePreset = ""
                        },
                        new SputnikDebrisConfig
                        {
                            presetName = "debris_4",
                            locationPreset = "debris_4",
                            heliPresetName = "",
                            turrets = new Dictionary<string, HashSet<LocationConfig>>(),
                            enableCardReader = false,
                            cardRaderLocation = new LocationConfig
                            {
                                position = "(0, 0, 0)",
                                rotation = "(0, 0, 0)"
                            },
                            NPCs = new Dictionary<string, HashSet<string>>
                            {
                                ["cosmonaut"] = new HashSet<string>
                                {
                                    "(1.334, 0, 3.326",
                                    "(1.793, 0, -1.614)"
                                }
                            },
                            groundCrates = new Dictionary<string, HashSet<LocationConfig>>
                            {
                                ["cratenormal_default"] = new HashSet<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        position = "(2.481, 0, 2.517)",
                                        rotation = "(0, 25, 0)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(0.911, 0, 2.207)",
                                        rotation = "(0, 320, 0)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(2.186, 0, -0.275)",
                                        rotation = "(0, 347, 0)"
                                    }
                                },
                            },
                            сrates = new Dictionary<string, HashSet<LocationConfig>>
                            {
                            },
                            mines = new HashSet<string>(),
                            markerConfig = new MarkerConfig
                            {
                                enable = true,
                                isRingMarker = true,
                                isShopMarker = true,
                                displayName = en ? "Space debris" : "Космические обломки",
                                radius = 0.2f,
                                alpha = 0.6f,
                                color1 = new ColorConfig { r = 0.81f, g = 0.25f, b = 0.15f },
                                color2 = new ColorConfig { r = 0f, g = 0f, b = 0f }
                            },
                            zoneConfig = new ZoneConfig
                            {
                                isPVPZone = false,
                                isDome = false,
                                darkening = 5,
                                isColoredBorder = false,
                                brightness = 5,
                                borderColor = 2,
                                radius = 25,
                                radiation = 0
                            },
                            useCustomSpawnPoints = false,
                            customSpawnPoints = new List<string>(),
                            iqDronePreset = ""
                        }
                    },
                    customCardConfig = new CustomCardConfig
                    {
                        shortName = "keycard_green",
                        name = en ? "SPACE CARD" : "КОСМИЧЕСКАЯ КАРТА",
                        skinID = 2841475252,
                        helthLossScale = 1,
                        enableSpawnInDefaultCrates = false,
                        spawnSetting = new Dictionary<string, float>
                        {
                            ["assets/bundled/prefabs/radtown/crate_elite.prefab"] = 5f
                        }
                    },
                    crateConfigs = new HashSet<CrateConfig>
                    {
                        new CrateConfig
                        {
                            presetName = "chinooklockedcrate_spacecard",
                            prefab = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab",
                            needSpaceCard = true,
                            hackTime = 0,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = false,
                                isAlphaLoot = false,
                                alphaLootPresetName = "",
                                isCustomLoot = false,
                                isRandomItemsEnable = false,
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = false,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 1,
                                            maxLootScale = 1,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab"
                                        }
                                    }

                                },
                                minItemsAmount = 1,
                                maxItemsAmount = 2,
                                items = new List<LootItemConfig>
                                {
                                    new LootItemConfig
                                    {
                                        shortname = "scrap",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 100f,
                                        isBlueprint = false,
                                        skin = 0,
                                        name = "",
                                        genomes = new List<string>()
                                    },
                                    new LootItemConfig
                                    {
                                        shortname = "wood",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 100f,
                                        isBlueprint = false,
                                        skin = 0,
                                        name = "",
                                        genomes = new List<string>()
                                    }
                                }
                            }
                        },
                        new CrateConfig
                        {
                            presetName = "chinooklockedcrate_default",
                            prefab = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab",
                            hackTime = 0,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = false,
                                isAlphaLoot = false,
                                alphaLootPresetName = "",
                                isCustomLoot = false,
                                isRandomItemsEnable = false,
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = false,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 1,
                                            maxLootScale = 1,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab"
                                        }
                                    }

                                },
                                minItemsAmount = 1,
                                maxItemsAmount = 2,
                                items = new List<LootItemConfig>
                                {
                                    new LootItemConfig
                                    {
                                        shortname = "scrap",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 100f,
                                        isBlueprint = false,
                                        skin = 0,
                                        name = "",
                                        genomes = new List<string>()
                                    },
                                    new LootItemConfig
                                    {
                                        shortname = "wood",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 100f,
                                        isBlueprint = false,
                                        skin = 0,
                                        name = "",
                                        genomes = new List<string>()
                                    }
                                }
                            }
                        },
                        new CrateConfig
                        {
                            presetName = "crateelite_default",
                            prefab = "assets/bundled/prefabs/radtown/underwater_labs/crate_elite.prefab",
                            hackTime = 0,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = false,
                                isAlphaLoot = false,
                                alphaLootPresetName = "",
                                isCustomLoot = false,
                                isRandomItemsEnable = false,
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = false,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 1,
                                            maxLootScale = 1,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab"
                                        }
                                    }

                                },
                                minItemsAmount = 1,
                                maxItemsAmount = 2,
                                items = new List<LootItemConfig>
                                {
                                    new LootItemConfig
                                    {
                                        shortname = "scrap",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 100f,
                                        isBlueprint = false,
                                        skin = 0,
                                        name = "",
                                        genomes = new List<string>()
                                    },
                                    new LootItemConfig
                                    {
                                        shortname = "wood",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 100f,
                                        isBlueprint = false,
                                        skin = 0,
                                        name = "",
                                        genomes = new List<string>()
                                    }
                                }
                            }
                        },
                        new CrateConfig
                        {
                            presetName = "cratenormal_underwater_1",
                            prefab = "assets/bundled/prefabs/radtown/underwater_labs/crate_normal.prefab",
                            hackTime = 0,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = false,
                                isAlphaLoot = false,
                                alphaLootPresetName = "",
                                isCustomLoot = false,
                                isRandomItemsEnable = false,
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = false,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 1,
                                            maxLootScale = 1,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab"
                                        }
                                    }

                                },
                                minItemsAmount = 1,
                                maxItemsAmount = 2,
                                items = new List<LootItemConfig>
                                {
                                    new LootItemConfig
                                    {
                                        shortname = "scrap",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 100f,
                                        isBlueprint = false,
                                        skin = 0,
                                        name = "",
                                        genomes = new List<string>()
                                    },
                                    new LootItemConfig
                                    {
                                        shortname = "wood",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 100f,
                                        isBlueprint = false,
                                        skin = 0,
                                        name = "",
                                        genomes = new List<string>()
                                    }
                                }
                            }
                        },
                        new CrateConfig
                        {
                            presetName = "cratenormal_underwater_2",
                            prefab = "assets/bundled/prefabs/radtown/underwater_labs/crate_normal_2.prefab",
                            hackTime = 0,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = false,
                                isAlphaLoot = false,
                                alphaLootPresetName = "",
                                isCustomLoot = false,
                                isRandomItemsEnable = true,
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = false,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 1,
                                            maxLootScale = 1,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab"
                                        }
                                    }

                                },
                                minItemsAmount = 1,
                                maxItemsAmount = 1,
                                items = new List<LootItemConfig>
                                {
                                    new LootItemConfig
                                    {
                                        shortname = "keycard_green",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 100f,
                                        isBlueprint = false,
                                        skin = 2841475252,
                                        name = en ? "SPACE CARD" : "КОСМИЧЕСКАЯ КАРТА",
                                        genomes = new List<string>()
                                    }
                                }
                            }
                        },
                        new CrateConfig
                        {
                            presetName = "cratenormal_default",
                            prefab = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                            hackTime = 0,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = false,
                                isAlphaLoot = false,
                                alphaLootPresetName = "",
                                isCustomLoot = false,
                                isRandomItemsEnable = false,
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = false,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 1,
                                            maxLootScale = 1,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab"
                                        }
                                    }

                                },
                                minItemsAmount = 1,
                                maxItemsAmount = 2,
                                items = new List<LootItemConfig>
                                {
                                    new LootItemConfig
                                    {
                                        shortname = "scrap",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 100f,
                                        isBlueprint = false,
                                        skin = 0,
                                        name = "",
                                        genomes = new List<string>()
                                    },
                                    new LootItemConfig
                                    {
                                        shortname = "wood",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 100f,
                                        isBlueprint = false,
                                        skin = 0,
                                        name = "",
                                        genomes = new List<string>()
                                    }
                                }
                            }
                        }
                    },
                    npcConfigs = new HashSet<NpcConfig>
                    {
                        new NpcConfig
                        {
                            presetName = "cosmonaut",
                            displayName = "Cosmonaut",
                            health = 200f,
                            wearItems = new HashSet<NpcWear>
                            {
                                new NpcWear
                                {
                                    shortName = "hazmatsuit",
                                    skinID = 10180
                                }
                            },
                            beltItems = new HashSet<NpcBelt>
                            {
                                new NpcBelt
                                {
                                    shortName = "rifle.lr300",
                                    amount = 1,
                                    skinID = 0,
                                    mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.holosight" },
                                    ammo = ""
                                },
                                new NpcBelt
                                {
                                    shortName = "syringe.medical",
                                    amount = 10,
                                    skinID = 0,
                                    mods = new HashSet<string>(),
                                    ammo = ""
                                },
                                new NpcBelt
                                {
                                    shortName = "grenade.f1",
                                    amount = 10,
                                    skinID = 0,
                                    mods = new HashSet<string>(),
                                    ammo = ""
                                }
                            },
                            kit = "",
                            deleteCorpse = true,
                            disableRadio = false,
                            roamRange = 5f,
                            chaseRange = 15,
                            attackRangeMultiplier = 1f,
                            senseRange = 100,
                            memoryDuration = 60f,
                            damageScale = 1f,
                            aimConeScale = 1f,
                            checkVisionCone = false,
                            visionCone = 135f,
                            speed = 7.5f,
                            turretDamageScale = 0f,
                            lootTableConfig = new LootTableConfig
                            {
                                clearDefaultItemList = false,
                                isAlphaLoot = false,
                                alphaLootPresetName = "",
                                isCustomLoot = false,
                                isRandomItemsEnable = false,
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = false,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 1,
                                            maxLootScale = 1,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab"
                                        }
                                    }

                                },
                                minItemsAmount = 2,
                                maxItemsAmount = 4,
                                items = new List<LootItemConfig>
                                {
                                    new LootItemConfig
                                    {
                                        shortname = "rifle.semiauto",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 5f,
                                        isBlueprint = false,
                                        skin = 0,
                                        name = "",
                                        genomes = new List<string>()
                                    },
                                    new LootItemConfig
                                    {
                                        shortname = "shotgun.pump",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 5f,
                                        isBlueprint = false,
                                        skin = 0,
                                        name = "",
                                        genomes = new List<string>()
                                    },
                                    new LootItemConfig
                                    {
                                        shortname = "pistol.semiauto",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 5f,
                                        isBlueprint = false,
                                        skin = 0,
                                        name = "",
                                        genomes = new List<string>()
                                    },
                                }
                            }
                        }
                    },
                    heliConfigs = new HashSet<HeliConfig>
                    {
                        new HeliConfig
                        {
                            presetName = "heli_1",
                            hp = 10000f,
                            cratesAmount = 3,
                            mainRotorHealth = 750f,
                            rearRotorHealth = 375f,
                            height = 50f,
                            bulletDamage = 20f,
                            bulletSpeed = 250f,
                            distance = 100f,
                            outsideTime = 30,
                            baseLootTableConfig = new BaseLootTableConfig
                            {
                                clearDefaultItemList = false,
                                prefabConfigs = new PrefabLootTableConfigs
                                {
                                    isEnable = false,
                                    prefabs = new List<PrefabConfig>
                                    {
                                        new PrefabConfig
                                        {
                                            minLootScale = 1,
                                            maxLootScale = 1,
                                            prefabName = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab"
                                        }
                                    }
                                },
                                isRandomItemsEnable = false,
                                maxItemsAmount = 1,
                                minItemsAmount = 2,
                                items = new List<LootItemConfig>
                                {
                                    new LootItemConfig
                                    {
                                        shortname = "scrap",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 100f,
                                        isBlueprint = false,
                                        skin = 0,
                                        name = "",
                                        genomes = new List<string>()
                                    }
                                }
                            }
                        }
                    },
                    turretConfigs = new HashSet<TurretConfig>
                    {
                        new TurretConfig
                        {
                            presetName = "turret_ak",
                            autoHeight = true,
                            hp = 250f,
                            shortNameWeapon = "rifle.ak",
                            shortNameAmmo = "ammo.rifle",
                            countAmmo = 200
                        },
                        new TurretConfig
                        {
                            presetName = "turret_m249",
                            autoHeight = false,
                            hp = 300f,
                            shortNameWeapon = "lmg.m249",
                            shortNameAmmo = "ammo.rifle",
                            countAmmo = 400
                        }
                    },
                    notifyConfig = new NotifyConfig
                    {
                        preStartTime = 0,
                        isChatEnable = true,
                        timeNotifications = new HashSet<int>
                        {
                            300,
                            60,
                            30,
                            5
                        },
                        gameTipConfig = new GameTipConfig
                        {
                            isEnabled = false,
                            style = 2,
                        }
                    },
                    guiConfig = new GUIConfig
                    {
                        isEnable = true,
                        offsetMinY = -56
                    },
                    supportedPluginsConfig = new SupportedPluginsConfig
                    {
                        pveMode = new PveModeConfig
                        {
                            enable = false,
                            showEventOwnerNameOnMap = true,
                            damage = 500f,
                            scaleDamage = new Dictionary<string, float>
                            {
                                ["Npc"] = 1f,
                                ["Helicopter"] = 2f,
                                ["Turret"] = 2f,
                            },
                            lootCrate = false,
                            hackCrate = false,
                            lootNpc = false,
                            damageNpc = false,
                            targetNpc = false,
                            damageHeli = false,
                            targetHeli = false,
                            canEnter = false,
                            canEnterCooldownPlayer = true,
                            timeExitOwner = 300,
                            alertTime = 60,
                            cooldownOwner = 86400,
                        },
                        economicsConfig = new EconomyConfig
                        {
                            enable = false,
                            plugins = new HashSet<string> { "Economics", "Server Rewards", "IQEconomic" },
                            minCommandPoint = 0,
                            minEconomyPiont = 0,
                            crates = new Dictionary<string, double>
                            {
                                ["assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab"] = 0.4
                            },
                            npcPoint = 2,
                            hackCratePoint = 5,
                            turretPoint = 2,
                            heliPoint = 5,
                            commands = new HashSet<string>()
                        },
                        guiAnnouncementsConfig = new GUIAnnouncementsConfig
                        {
                            isEnabled = false,
                            bannerColor = "Grey",
                            textColor = "White",
                            apiAdjustVPosition = 0.03f
                        },
                        notifyPluginConfig = new NotifyPluginConfig
                        {
                            isEnabled = false,
                            type = 1
                        },
                        discordMessagesConfig = new DiscordConfig
                        {
                            isEnabled = false,
                            webhookUrl = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks",
                            embedColor = 13516583,
                            keys = new HashSet<string>
                            {
                                "PreStartEvent",
                                "StartEvent",
                                "Crash"
                            }
                        },
                        zoneManager = new ZoneManagerConfig
                        {
                            enable = false,
                            blockFlags = new HashSet<string>
                            {
                                "eject",
                                "pvegod"
                            },
                            blockIDs = new HashSet<string>
                            {
                                "Example"
                            }
                        },
                        raidableBases = new RaidableBasesConfig
                        {

                        },
                        restoreUponDeath = new RestoreUponDeathConfig
                        {
                            disableRestore = false
                        },
                        betterNpcConfig = new BetterNpcConfig
                        {
                            isHeliNpc = false,
                        },
                        iqDroneConfig = new IQDroneConfig
                        {
                            isDroneEnable = false,
                            dronePresets = new HashSet<IQDronePreset>
                            {
                                new IQDronePreset
                                {
                                    presetName = "lite",
                                    droneNumber = 5,
                                    targetDroneNumber = 5,
                                    droneKeys = new Dictionary<string, int>
                                    {
                                        ["LITE_DRONE"] = 100,
                                    },
                                }
                            }
                        }
                    }
                };
            }
        }
        #endregion Config
    }
}

namespace Oxide.Plugins.SputnikExtensionMethods
{
    public static class ExtensionMethods
    {
        public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return true;
            return false;
        }

        public static HashSet<TSource> Where<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            HashSet<TSource> result = new HashSet<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) result.Add(enumerator.Current);
            return result;
        }

        public static TSource FirstOrDefault<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return enumerator.Current;
            return default(TSource);
        }

        public static HashSet<TResult> Select<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> predicate)
        {
            HashSet<TResult> result = new HashSet<TResult>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(predicate(enumerator.Current));
            return result;
        }

        public static List<TResult> Select<TSource, TResult>(this IList<TSource> source, Func<TSource, TResult> predicate)
        {
            List<TResult> result = new List<TResult>();
            for (int i = 0; i < source.Count; i++)
            {
                TSource element = source[i];
                result.Add(predicate(element));
            }
            return result;
        }

        public static HashSet<T> OfType<T>(this IEnumerable<BaseNetworkable> source)
        {
            HashSet<T> result = new HashSet<T>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (enumerator.Current is T) result.Add((T)(object)enumerator.Current);
            return result;
        }

        public static bool IsExists(this BaseNetworkable entity) => entity != null && !entity.IsDestroyed;

        public static void ClearItemsContainer(this ItemContainer container)
        {
            for (int i = container.itemList.Count - 1; i >= 0; i--)
            {
                Item item = container.itemList[i];
                item.RemoveFromContainer();
                item.Remove();
            }
        }

        public static bool IsRealPlayer(this BasePlayer player) => player != null && player.userID.IsSteamId();

        public static List<TSource> OrderBy<TSource>(this IEnumerable<TSource> source, Func<TSource, float> predicate)
        {
            List<TSource> result = source.ToList();
            for (int i = 0; i < result.Count; i++)
            {
                for (int j = 0; j < result.Count - 1; j++)
                {
                    if (predicate(result[j]) > predicate(result[j + 1]))
                    {
                        TSource z = result[j];
                        result[j] = result[j + 1];
                        result[j + 1] = z;
                    }
                }
            }
            return result;
        }

        public static List<TSource> ToList<TSource>(this IEnumerable<TSource> source)
        {
            List<TSource> result = new List<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(enumerator.Current);
            return result;
        }

        public static HashSet<TSource> ToHashSet<TSource>(this IEnumerable<TSource> source)
        {
            HashSet<TSource> result = new HashSet<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(enumerator.Current);
            return result;
        }

        public static TSource Max<TSource>(this IEnumerable<TSource> source, Func<TSource, float> predicate)
        {
            TSource result = source.ElementAt(0);
            float resultValue = predicate(result);
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    TSource element = enumerator.Current;
                    float elementValue = predicate(element);
                    if (elementValue > resultValue)
                    {
                        result = element;
                        resultValue = elementValue;
                    }
                }
            }
            return result;
        }

        public static TSource Min<TSource>(this IEnumerable<TSource> source, Func<TSource, float> predicate)
        {
            TSource result = source.ElementAt(0);
            float resultValue = predicate(result);
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    TSource element = enumerator.Current;
                    float elementValue = predicate(element);
                    if (elementValue < resultValue)
                    {
                        result = element;
                        resultValue = elementValue;
                    }
                }
            }
            return result;
        }

        public static TSource ElementAt<TSource>(this IEnumerable<TSource> source, int index)
        {
            int movements = 0;
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (movements == index) return enumerator.Current;
                    movements++;
                }
            }
            return default(TSource);
        }

        public static List<TSource> OrderByQuickSort<TSource>(this List<TSource> source, Func<TSource, float> predicate)
        {
            return source.QuickSort(predicate, 0, source.Count - 1);
        }

        private static List<TSource> QuickSort<TSource>(this List<TSource> source, Func<TSource, float> predicate, int minIndex, int maxIndex)
        {
            if (minIndex >= maxIndex) return source;

            int pivotIndex = minIndex - 1;
            for (int i = minIndex; i < maxIndex; i++)
            {
                if (predicate(source[i]) < predicate(source[maxIndex]))
                {
                    pivotIndex++;
                    source.Replace(pivotIndex, i);
                }
            }
            pivotIndex++;
            source.Replace(pivotIndex, maxIndex);

            QuickSort(source, predicate, minIndex, pivotIndex - 1);
            QuickSort(source, predicate, pivotIndex + 1, maxIndex);

            return source;
        }

        private static void Replace<TSource>(this IList<TSource> source, int x, int y)
        {
            TSource t = source[x];
            source[x] = source[y];
            source[y] = t;
        }
    }
}