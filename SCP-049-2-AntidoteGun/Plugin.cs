using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using Exiled.API.Interfaces;
using Exiled.API.Extensions;
using PlayerRoles;
using UnityEngine;
using System.ComponentModel;

namespace AntidoteGun
{
    public class AntidoteConfig : IConfig
    {
        public bool IsEnabled { get; set; } = true;
        public bool Debug { get; set; } = false;
        public ItemType AntidoteGunType { get; set; } = ItemType.GunRevolver;
        public ushort AntidoteAmmo { get; set; } = 5;
        public bool CancelDamage { get; set; } = true;
        public string PickupHintMessage { get; set; } = "<color=yellow>Antidote Gun</color>\\n<color=grey>Reverts Zombies to their previous role.</color>";
        public float HintDuration { get; set; } = 1.1f;
        public string CureBroadcastMessage { get; set; } = "<color=green>You have been cured with the Antidote!</color>";
        public ushort CureBroadcastDuration { get; set; } = 5;
        public int ShotLimit { get; set; } = 5;
        public string DepletedMessage { get; set; } = "<color=red>The Antidote Gun is depleted and has vanished.</color>";
        public ushort DepletedMessageDuration { get; set; } = 5;
        
        [Description("Should the Antidote Gun spawn in the LCZ Glass Box room (GR18) instead of the LCZ Armory? (Default: false = LCZ Armory)")]
        public bool SpawnInGlassBox { get; set; } = false;
    }

    public class AntidotePlugin : Plugin<AntidoteConfig>
    {
        public override string Name => "Antidote Gun Plugin";
        public override string Author => "MrManatee";
        public override Version Version => new Version(1, 0, 0);
        public override Version RequiredExiledVersion => new Version(9, 5, 1);
        public override PluginPriority Priority => PluginPriority.Highest;

        public static AntidotePlugin Instance { get; private set; }
        private EventHandlers _eventHandlers;
        public Dictionary<int, RoleTypeId> PreviousRoles { get; private set; } = new Dictionary<int, RoleTypeId>();
        public HashSet<ushort> AntidoteGunSerials { get; private set; } = new HashSet<ushort>();
        public Dictionary<ushort, int> ShotsFired { get; private set; } = new Dictionary<ushort, int>();

        public override void OnEnabled()
        {
            Instance = this;
            _eventHandlers = new EventHandlers(this);
            ShotsFired = new Dictionary<ushort, int>();

            Exiled.Events.Handlers.Server.RoundStarted += _eventHandlers.OnRoundStarted;
            Exiled.Events.Handlers.Server.EndingRound += _eventHandlers.OnEndingRound;
            Exiled.Events.Handlers.Player.ChangingRole += _eventHandlers.OnChangingRole;
            Exiled.Events.Handlers.Player.Hurting += _eventHandlers.OnHurting;
            Exiled.Events.Handlers.Player.Left += _eventHandlers.OnPlayerLeft;
            Exiled.Events.Handlers.Player.Shooting += _eventHandlers.OnPlayerShooting;

            Log.Info($"{Name} v{Version} enabled.");
            if(Config.Debug) Log.Debug("Debug logging enabled.");
        }

        public override void OnDisabled()
        {
            Exiled.Events.Handlers.Server.RoundStarted -= _eventHandlers.OnRoundStarted;
            Exiled.Events.Handlers.Server.EndingRound -= _eventHandlers.OnEndingRound;
            Exiled.Events.Handlers.Player.ChangingRole -= _eventHandlers.OnChangingRole;
            Exiled.Events.Handlers.Player.Hurting -= _eventHandlers.OnHurting;
            Exiled.Events.Handlers.Player.Left -= _eventHandlers.OnPlayerLeft;
            Exiled.Events.Handlers.Player.Shooting -= _eventHandlers.OnPlayerShooting;

            _eventHandlers = null;
            PreviousRoles.Clear();
            AntidoteGunSerials.Clear();
            ShotsFired.Clear();
            Instance = null;

            Log.Info($"{Name} disabled.");
        }
    }

    public class EventHandlers
    {
        private readonly AntidotePlugin _plugin;
        private readonly AntidoteConfig _config;

        public EventHandlers(AntidotePlugin plugin)
        {
            _plugin = plugin;
            _config = plugin.Config;
        }

        public void OnRoundStarted()
        {
            _plugin.PreviousRoles.Clear();
            _plugin.AntidoteGunSerials.Clear();
            _plugin.ShotsFired.Clear();
            if (_config.Debug) Log.Debug("Round Started: Cleared previous roles, antidote gun serials, and shot counts.");
            
            RoomType targetRoomType = _config.SpawnInGlassBox ? RoomType.LczGlassBox : RoomType.LczArmory;
            string roomName = targetRoomType == RoomType.LczGlassBox ? "LCZ Glass Box (GR18)" : "LCZ Armory";

            Room spawnRoom = Room.Get(targetRoomType);
            if (spawnRoom == null)
            {
                Log.Warn($"{roomName} room not found, cannot spawn Antidote Gun.");
                return;
            }

            try
            {
                Pickup antidotePickup = Pickup.Create(_config.AntidoteGunType);
                if (antidotePickup is FirearmPickup firearmPickup)
                {
                    firearmPickup.Ammo = _config.AntidoteAmmo;
                }
                else
                {
                     Log.Warn($"Item {_config.AntidoteGunType} created pickup is not a FirearmPickup. Cannot set initial ammo.");
                }

                Vector3 spawnPos = spawnRoom.Position + Vector3.up;
                antidotePickup.Position = spawnPos;
                antidotePickup.Rotation = Quaternion.identity;
                antidotePickup.Spawn();

                _plugin.AntidoteGunSerials.Add(antidotePickup.Serial);
                _plugin.ShotsFired[antidotePickup.Serial] = 0;
                
                if (_config.Debug) Log.Debug($"Spawned Antidote Gun (Serial: {antidotePickup.Serial}, Type: {antidotePickup.Type}) in {roomName}. Initialized shot count to 0.");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to spawn Antidote Gun: {ex}");
            }
        }

        public void OnChangingRole(Exiled.Events.EventArgs.Player.ChangingRoleEventArgs ev)
        {
            RoleTypeId previousRole = ev.Player.PreviousRole;
            if (_config.Debug) Log.Debug($"OnChangingRole: Player {ev.Player.Nickname} (ID: {ev.Player.Id}). New Role: {ev.NewRole}. Previous Role: {previousRole}.");

            if (ev.NewRole == RoleTypeId.Scp0492)
            {
                if (_config.Debug) Log.Debug($" Player is becoming SCP-049-2. Checking previous role {previousRole}...");
                if (previousRole != RoleTypeId.None && previousRole != RoleTypeId.Spectator && !previousRole.IsScp())
                {
                    _plugin.PreviousRoles[ev.Player.Id] = previousRole;
                    if (_config.Debug) Log.Debug($"  Storing previous role {previousRole} for Player ID {ev.Player.Id}. PreviousRoles count: {_plugin.PreviousRoles.Count}");
                }
                else
                {
                    if (_config.Debug) Log.Debug($"  Previous role {previousRole} is not valid to store.");
                }
            }
            else
            {
                if (_plugin.PreviousRoles.Remove(ev.Player.Id))
                {
                     if (_config.Debug) Log.Debug($" Player {ev.Player.Nickname} (ID: {ev.Player.Id}) changed to {ev.NewRole}. Removed previous role tracking. PreviousRoles count: {_plugin.PreviousRoles.Count}");
                }
            }
        }

        public void OnPlayerShooting(Exiled.Events.EventArgs.Player.ShootingEventArgs ev)
        {
            if (ev.Firearm == null || !_plugin.AntidoteGunSerials.Contains(ev.Firearm.Serial))
                return;

            ushort serial = ev.Firearm.Serial;
            int currentShots = _plugin.ShotsFired.TryGetValue(serial, out int shots) ? shots : 0;
            currentShots++;
            _plugin.ShotsFired[serial] = currentShots;

             if (_config.Debug) Log.Debug($"Player {ev.Player.Nickname} fired Antidote Gun (Serial: {serial}). Shot count: {currentShots}/{_config.ShotLimit}");

            if (currentShots >= _config.ShotLimit)
            {
                 if (_config.Debug) Log.Debug($"Antidote Gun (Serial: {serial}) reached shot limit. Removing item.");

                ev.Player.RemoveItem(ev.Firearm, true);
                _plugin.AntidoteGunSerials.Remove(serial);
                _plugin.ShotsFired.Remove(serial);
                ev.Player.Broadcast(_config.DepletedMessageDuration, _config.DepletedMessage);
            }
        }

        public void OnHurting(Exiled.Events.EventArgs.Player.HurtingEventArgs ev)
        {
             if (_config.Debug) Log.Debug($"OnHurting: Attacker={ev.Attacker?.Nickname ?? "null"}, Victim={ev.Player?.Nickname ?? "null"}, Amount={ev.Amount}");

            if (ev.Attacker == null || ev.Player == null || ev.Attacker.CurrentItem == null)
                return;

             if (_config.Debug) Log.Debug($" Attacker item: {ev.Attacker.CurrentItem.Type}, Serial: {ev.Attacker.CurrentItem.Serial}");

            bool isAntidoteGun = _plugin.AntidoteGunSerials.Contains(ev.Attacker.CurrentItem.Serial);
             if (_config.Debug) Log.Debug($" Is item an Antidote Gun? {isAntidoteGun}");

            if (!isAntidoteGun)
                return;

             if (_config.Debug) Log.Debug($"{ev.Attacker.Nickname} shot {ev.Player.Nickname} with Antidote Gun (Serial: {ev.Attacker.CurrentItem.Serial})");

            bool isVictimZombie = ev.Player.Role.Type == RoleTypeId.Scp0492;
             if (_config.Debug) Log.Debug($" Is victim an SCP-049-2? {isVictimZombie}");

            if (isVictimZombie)
            {
                bool hasPreviousRole = _plugin.PreviousRoles.TryGetValue(ev.Player.Id, out RoleTypeId previousRole);
                 if (_config.Debug) Log.Debug($" Does victim (ID: {ev.Player.Id}) have a stored previous role? {hasPreviousRole}. {(hasPreviousRole ? $"Previous role: {previousRole}" : $"Current PreviousRoles Count: {_plugin.PreviousRoles.Count}")}");

                if (hasPreviousRole)
                {
                    Log.Info($"Reverting {ev.Player.Nickname} (ID: {ev.Player.Id}) from SCP-049-2 back to {previousRole} using Antidote Gun.");
                    try
                    {
                         if (_config.Debug) Log.Debug($"  Attempting RoleManager.ServerSetRole to {previousRole}...");
                        ev.Player.RoleManager.ServerSetRole(previousRole, RoleChangeReason.RemoteAdmin, RoleSpawnFlags.All);
                         if (_config.Debug) Log.Debug($"  ServerSetRole called successfully.");

                        _plugin.PreviousRoles.Remove(ev.Player.Id);
                         if (_config.Debug) Log.Debug($"  Removed Player ID {ev.Player.Id} from PreviousRoles dictionary. PreviousRoles count: {_plugin.PreviousRoles.Count}");

                        ev.Player.Broadcast(_config.CureBroadcastDuration, _config.CureBroadcastMessage);
                         if (_config.Debug) Log.Debug($"  Sent cure broadcast to {ev.Player.Nickname}.");

                        if (_config.CancelDamage)
                        {
                            ev.IsAllowed = false;
                             if (_config.Debug) Log.Debug($"  Antidote Gun damage cancelled (CancelDamage: true).");
                        }
                        else
                        {
                             if (_config.Debug) Log.Debug($"  Damage allowed (CancelDamage: false). Amount: {ev.Amount}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Failed to change role for {ev.Player.Nickname} (ID: {ev.Player.Id}): {ex}");
                    }
                }
                else
                {
                    Log.Debug($"{ev.Player.Nickname} (ID: {ev.Player.Id}) is SCP-049-2, but no previous role was stored for Antidote Gun effect.");
                    if (_config.CancelDamage)
                    {
                        ev.IsAllowed = false;
                         if (_config.Debug) Log.Debug($"  Damage cancelled (CancelDamage: true).");
                    }
                     else
                     {
                         if (_config.Debug) Log.Debug($"  Damage allowed (CancelDamage: false). Amount: {ev.Amount}");
                     }
                }
            }
            else
            {
                 if (_config.Debug) Log.Debug($" Victim is not SCP-049-2 ({ev.Player.Role.Type}). Checking CancelDamage...");
                 if (_config.CancelDamage)
                 {
                     ev.IsAllowed = false;
                     if (_config.Debug) Log.Debug($"  Antidote Gun damage cancelled (CancelDamage: true).");
                 }
                 else
                 {
                     if (_config.Debug) Log.Debug($"  Damage allowed (CancelDamage: false). Amount: {ev.Amount}");
                 }
            }
        }

        public void OnPlayerLeft(Exiled.Events.EventArgs.Player.LeftEventArgs ev)
        {
            if (_plugin.PreviousRoles.Remove(ev.Player.Id))
            {
                 if (_config.Debug) Log.Debug($"Cleaned up previous role data for disconnected player {ev.Player.Nickname} (ID: {ev.Player.Id}).");
            }
        }

        public void OnEndingRound(Exiled.Events.EventArgs.Server.EndingRoundEventArgs ev)
        {
            _plugin.PreviousRoles.Clear();
            _plugin.AntidoteGunSerials.Clear();
            _plugin.ShotsFired.Clear();
            Log.Debug("Cleared Antidote Gun plugin data at round end.");
        }
    }
}