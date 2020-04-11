# Portals
Teleportation portals for Rust

Portals gives you the opportunity to place portals in your world.

## How to use

  - go to the place where the entrance should be
  - write /portal entrance portal1 (portal1 is an example name for aportal)
  - go to the place where the exit should be
  - write /portal exit portal1
  - open the Datafile and change other details of the Portal, for example the radius, in which it teleports people.

## Permissions
This plugin uses Oxide's permission system. To assign a permission, use oxide.grant <user or group> <name or steam id> <permission>. To remove a permission, use oxide.revoke <user or group> <name or steam id> <permission>.

   - portals.admin - Required for /portal command
   - portals.use - Standard permission for using portals (can be customized per portal in oxide/data/Portals.json)

## Chat Commands

   - /portal entrance <name> - Set the entrance to a portal
   - /portal exit <name> - Set the exit for a portal
   - /portal remove <name> - Remove a portal
   - /portal list - List existing portals

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
