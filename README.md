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

   - /portal list - List existing portals

## Configuration

```json
{
  "Set two-way portals by default": true,
  "Portal countdown in seconds": 5.0,
  "Deploy spinner at portal points": true,
  "Write portal name on spinners": true
  "Spin entrance wheel on teleport": false,
  "Spin exit wheel on teleport": true,
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
