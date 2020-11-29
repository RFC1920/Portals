# Portals
Teleportation portals for Rust - this is a partial rewrite of the old Portals plugin by LaserHydra.

Portals gives you the opportunity to place teleportation portals in the Rust world.

![](https://i.imgur.com/6OJHsGz.jpg)

## How to use

  - Go to the place where the entrance should be.
  - Type /portal entrance portal1 (portal1 is an example name for a portal).
  - Go to the place where the exit should be.
  - Type /portal exit portal1

Portals can be unidirectional or bidirectional.  See below.

By default, a spinner wheel will be placed at the entrance and exit.  If you have SignArtist installed, it can also write the portal name on the wheel.  See Configuration below.

## Recommendations

It is recommended that you set the following configs for SignArtist to try to lessen the chance of spinners not being painted:

```json
{
  "Time in seconds between download requests (0 to disable)": 0,
  "Maximum concurrent downloads": 50,
  ...
}
```

There are additional configs for SignArtist, but what is above has impact on its use by Portals.

   - On load, Portals will try to draw to every spinner within a few seconds at most.  This can exceed the default limits in SignArtist for "Time in seconds between...".
   - If you have 5 portals, 10 spinners will need to be painted.  So, a limit of 10 might just allow Portals to finish its work.  Set this to allow all portals as well as any signs you may have actively using it.

## Permissions
This plugin uses Oxide's permission system. To assign a permission, use oxide.grant <user or group> <name or steam id> <permission>. To remove a permission, use oxide.revoke <user or group> <name or steam id> <permission>.

   - portals.admin - Required for /portal command
   - portals.use - Standard permission for using portals (can be customized per portal in oxide/data/Portals.json)

## Commands

Commands can be used in chat or console.

   - /portal entrance <name> - Set the entrance to a portal
     - You can substitute pri|primary|add|create for entrance

   - /portal exit <name> - Set the exit for a portal
     - You can substitute sec|secondary for exit

   - /portal timer <NAME> <numberofseconds>
     - You can substitute time for timer

   - /portal remove <name> - Remove a portal

   - /portal oneway <NAME> <truefalse>

   - /portal perm/permission <NAME> <PERMISSION>
     - Ex. /portal perm cp1 checkpoint1 -- Registers a new permission, portals.checkpoint1, for use with portal cp1.

   - /portal list - List existing portals

## Configuration

```json
{
  "Set two-way portals by default": true,
  "Portal countdown in seconds": 5.0,
  "Deploy spinner at portal points": true,
  "Write portal name on spinners": true
  "Spinner Background Color": "000000",
  "Spinner Text Color": "FFFF00",
  "Spin entrance wheel on teleport": false,
  "Spin exit wheel on teleport": true,
  "Play AV effects on teleport": false
}


```

## Stored Data

Portals are saved in oxide/data/Portals.json.

## For Developers

- void OnPortalUsed(BasePlayer player, JObject portal, JObject point)

```json
    JObject portal: the portal object; Example:
    {
        "ID": "1",
        "Entrance": {
            "Location": {
                "_location": "16.76093 75.57893 10.72905"
            }
        },
        "Exit": {
            "Location": {
                "_location": "3.850618 72.05898 22.37546"
            }
        },
        "OneWay": true,
        "TeleportationTime": 0.0,
        "RequiredPermission": "portals.use"
    }
```

```json
    JObject point: exit or entrance point, player is being teleported to; Example:
    {
        "Location": {
            "_location": "16.76093 75.57893 10.72905"
        }
    }
```

```cs
    SpawnEphemeralPortal(BasePlayer player, BaseEntity entity, float time = 10f)
```

Spawns a temporary portal in front of a user and target entity.  It will be automatically destroyed after "time".  Default name is currently player.displayName:TEMP.
