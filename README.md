# Antidote Gun Plugin for SCP: Secret Laboratory (Exiled)

## Description

This Exiled plugin introduces a special "Antidote Gun" item into the game. When an SCP-049-2 (Zombie) instance is shot with this specific gun, they are reverted back to the human role they held before being converted by SCP-049. The plugin features configurable settings for the gun type, ammo, shot limits, and messages.

## Features

* **Antidote Gun Spawning:** Spawns a designated firearm (default: Revolver) in the Light Containment Zone Armory at the start of each round. This specific instance acts as the Antidote Gun.
* **Zombie Reversion:** Successfully shooting an SCP-049-2 with the Antidote Gun changes their role back to what it was previously (e.g., Class-D, Scientist).
* **Role Tracking:** Accurately tracks the role a player had before becoming SCP-049-2.
* **Finite Shots:** The Antidote Gun has a configurable, limited number of shots (default: 5). After the limit is reached, the gun is removed from the player's inventory.
* **Damage Control:** Option to configure whether the Antidote Gun deals health damage upon hitting a target.
* **Player Notifications:** Configurable broadcast messages are sent to players when they are cured or when the Antidote Gun runs out of shots.
* **Configuration:** Most core features like the gun type, ammo count, shot limit, and messages are configurable.

*(Note: A hint-on-hover feature for the pickup was developed but is currently commented out in the code due to potential project setup issues with Unity's Physics/Raycast system needed for it to function reliably.)*

## Installation

1.  Ensure you have Exiled installed on your SCP:SL server.
2.  Download the `AntidoteGun.dll` file from the [Releases page](https://github.com/MrMrManatee/SCP-SL-AntidoteGun/releases). 3.  Place the `AntidoteGun.dll` file into your server's `EXILED\Plugins` folder.
4.  Restart the server or run the `reload plugins` command in the server console.
5.  Configure the plugin settings in the generated file located at `EXILED\Configs\<port>-config.yml` under the `antidote_gun:` section.

## Configuration

The following options are available in the plugin's configuration file (`EXILED\Configs\<port>-config.yml`):

```yaml
antidote_gun:
  # Is the plugin enabled?
  is_enabled: true
  # Enable debug logs in the server console? (Requires EXILED debug mode enabled too)
  debug: false
  # Which ItemType should be used for the Antidote Gun? (Default: GunRevolver)
  antidote_gun_type: GunRevolver
  # How much ammo should the Antidote Gun spawn with?
  antidote_ammo: 5
  # Should shooting the gun deal normal weapon damage in addition to the effect? (true = no damage, false = deals damage)
  cancel_damage: true
  # How many total shots can the Antidote Gun fire before being destroyed?
  shot_limit: 5
  # The broadcast message shown to a player when they are reverted from SCP-049-2.
  cure_broadcast_message: "<color=green>You have been cured with the Antidote!</color>"
  # How long (in seconds) the cure broadcast message is shown.
  cure_broadcast_duration: 5
  # The broadcast message shown to the player when the Antidote Gun runs out of shots and is removed.
  depleted_message: "<color=red>The Antidote Gun is depleted and has vanished.</color>"
  # How long (in seconds) the depleted message is shown.
  depleted_message_duration: 5
  # --- Configs for commented-out Hint feature ---
  # pickup_hint_message: "<color=yellow>Antidote Gun</color>\n<color=grey>Reverts Zombies to their previous role.</color>"
  # hint_duration: 1.1
