#define DEBUG
using Facepunch.Extend;
using Network;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Portals", "LaserHydra/RFC1920", "2.2.5")]
    [Description("Create portals and feel like you're in Star Trek")]
    class Portals : RustPlugin
    {
        #region vars
        private ConfigData configData;
        private List<PortalInfo> portals = new List<PortalInfo>();
        public static Portals Instance = null;

        private const string permPortalsUse = "portals.use";
        private const string permPortalsAdmin = "portals.admin";

        [PluginReference]
        private Plugin SignArtist, NoEscape;

        public bool initialized = false;
        #endregion

        #region Oxide Hooks
        void OnNewSave(string strFilename)
        {
            if (configData.wipeOnNewSave)
            {
                Puts("Map change - wiping portal locations");
                foreach (var portal in portals)
                {
                    portal.Primary.Location.Vector3 = new Vector3();
                    portal.Secondary.Location.Vector3 = new Vector3();
                }
                SaveData();
            }
        }

        private void OnServerInitialized()
        {
            Instance = this;

            AddCovalenceCommand("portal", "CmdPortal");
            permission.RegisterPermission(permPortalsUse, this);
            permission.RegisterPermission(permPortalsAdmin, this);
            LoadConfigVariables();
            LoadData();

            foreach (PortalInfo portal in portals)
            {
                if (!permission.PermissionExists(portal.RequiredPermission, this))
                {
                    permission.RegisterPermission(portal.RequiredPermission, this);
                }
                portal.ReCreate();
            }
            SaveData();
            initialized = true;
        }

        private void OnServerShutdown() => Unload();
        private void Unload()
        {
            SaveData();
            foreach (PortalInfo portal in portals)
            {
                if (portal != null) portal.Remove();
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player.gameObject.GetComponent<PortalPlayerHandler>())
            {
                UnityEngine.Object.Destroy(player.gameObject.GetComponent<PortalPlayerHandler>());
            }
        }

        private object CanPickupEntity(BasePlayer player, BaseEntity entity)
        {
            if (entity == null || player == null) return null;
            if (entity.ShortPrefabName != "spinner.wheel.deployed") return null;

            foreach (PortalInfo p in portals)
            {
                if (p.DeploySpinner)
                {
                    if (p.Primary.Wheel.net.ID == entity.net.ID || p.Secondary.Wheel.net.ID == entity.net.ID)
                    {
#if DEBUG
                        Puts("This is a portal spinner");
#endif
                        return false;
                    }
                }
            }

            return null;
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null || hitInfo == null) return null;
            if (entity.ShortPrefabName != "spinner.wheel.deployed") return null;

            foreach (PortalInfo p in portals)
            {
                if (p.DeploySpinner)
                {
                    if (p.Primary.Wheel.net.ID == entity.net.ID || p.Secondary.Wheel.net.ID == entity.net.ID)
                    {
#if DEBUG
                        Puts("This is a portal spinner");
#endif
                        return false;
                    }
                }
            }

            return null;
        }
        #endregion

        #region inbound hooks
        private bool SpawnEphemeralPortal(BasePlayer player, BaseEntity entity, float time = 10f)
        {
            if (player == null) return false;
            if (entity == null) return false;
            if (time < 5f) return false;
#if DEBUG
            Puts($"Trying to spin up a temporary portal for {player.displayName} to {entity.ShortPrefabName}");
#endif
            var portal = new PortalInfo();
            portal.ID = $"{player.displayName}:TEMP";

            Vector3 primary = player.transform.position + player.transform.forward * 2f;
            if (!AboveFloor(primary))
            {
                primary.y = TerrainMeta.HeightMap.GetHeight(player.transform.position) + 0.1f;
            }
            portal.Primary.Location.Vector3 = primary;

            Vector3 secondary = entity.transform.position + entity.transform.forward * 2f;
            bool ok = false;
            while (!ok)
            {
                ok = !BadLocation(secondary);
                secondary.x -= 0.5f;
                secondary.z += 0.5f;
#if DEBUG
                Puts($"Seeking new location: {secondary.ToString()}");
#endif
            }
            portal.Secondary.Location.Vector3 = secondary;
            if (!AboveFloor(secondary))
            {
                secondary.y = TerrainMeta.HeightMap.GetHeight(entity.transform.position) + 0.1f;
            }

            portal.OneWay = true;
            portal.DeploySpinner = configData.deploySpinner;
            portals.Add(portal);
            portal.ReCreate();
            //            portal.Primary.GameObject.GetComponent<PortalEntity>().ExitEffects(player);
            timer.Once(time, () => KillEphemeralPortal(portal));
            return true;
        }

        private void KillEphemeralPortal(PortalInfo portal)
        {
            portal.Remove();
            portals.Remove(portal);
        }
        #endregion

        #region Loading
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "NoPermission", "You don't have permission to use this command." },
                { "NoPermissionPortal", "You don't have permission to use this portal." },
                { "PortalDoesNotExist", "Portal {0} does not exist." },
                { "PortalPrimarySet", "Primary for portal {0} was set at your current location." },
                { "PortalSecondarySet", "Secondary for portal {0} was set at your current location." },
                { "PortalOneWaySet", "OneWay for portal {0} was set to {1}." },
                { "PortalSpinnerSet", "Spinner for portal {0} was set to {1}." },
                { "PortalPermSet", "Permission for portal {0} was set to {1}." },
                { "PortalTimerSet", "Timer for portal {0} was set to {1}." },
                { "PortalRemoved", "Portal {0} was removed." },
                { "PortalIncomplete", "Portal {0} missing one end." },
                { "Teleporting", "You have entered portal {0}. You will be teleported in {1} seconds." },
                { "TeleportationCancelled", "Teleportation cancelled as you left the portal before the teleportation process finished." },
                { "PortalListEmpty", "There are no portals." },
                { "PortalList", "Portals: {0}" },
                { "seconds", "Seconds" },
                { "perm", "Perm: " },
                { "syntax", "Syntax: /portal <entrance|exit|list|remove|oneway|time|perm> <ID> <true/false> <value>\n  e.g.:\n\n  /portal list\n  /portal entrance portal1\n  /portal time portal1 4\n  /portal oneway portal1 1\n  /portal oneway portal1 false\n  /portal remove portal1\n  /portal perm portal2 special" },
                { "config", "Configuration:{0}" },
                { "deploySpinner", "Deploy spinner at portal points (deploySpinner): {0}" },
                { "nameOnWheel", "Write portal name on spinners (nameOnWheel): {0}" },
                { "bgColor", "Spinner Background Color (bgColor): {0}" },
                { "textColor", "Spinner Text Color (textColor): {0}" },
                { "spinEntrance", "Spin entrance wheel on teleport (spinEntrance): {0}" },
                { "spinExit", "Spin exit wheel on teleport (spinExit): {0}" },
                { "defaultTwoWay", "Set two-way portals by default (defaultTwoWay): {0}" },
                { "teleTimer", "Portal countdown in seconds (teleTimer): {0}" }
            }, this);
        }
        #endregion

        #region Commands
        [Command("portal")]
        private void CmdPortal(IPlayer iplayer, string command, string[] args)
        {
            if (!iplayer.HasPermission(permPortalsAdmin)) { Message(iplayer, "NoPermission"); return; }
            if (args.Length == 0) { Message(iplayer, "syntax"); return; }

            var player = iplayer.Object as BasePlayer;
            string ID;
            PortalInfo portal;

            switch (args[0])
            {
                case "entrance":
                case "pri":
                case "primary":
                case "add":
                case "create":
                    if (args.Length != 2) { Message(iplayer, "syntax"); return; }

                    ID = args[1];
                    portal = PortalInfo.Find(ID);

                    if (portal == null)
                    {
                        portal = new PortalInfo(ID);
                        portals.Add(portal);
                    }

                    Vector3 primary = player.transform.position;
                    //if (!AboveFloor(primary))
                    //{
                    //    primary.y = TerrainMeta.HeightMap.GetHeight(player.transform.position) + 0.1f;
                    //}
                    portal.Primary.Location.Vector3 = primary;
                    portal.OneWay = !configData.defaultTwoWay;
                    portal.DeploySpinner = configData.deploySpinner;
                    portal.ReCreate();

                    SaveData();
                    Message(iplayer, "PortalPrimarySet", args[1]);
                    break;
                case "exit":
                case "sec":
                case "secondary":
                    if (args.Length != 2) { Message(iplayer, "syntax"); return; }

                    ID = args[1];
                    portal = PortalInfo.Find(ID);

                    if (portal == null)
                    {
                        portal = new PortalInfo(ID);
                        portals.Add(portal);
                    }

                    Vector3 secondary = player.transform.position;
                    //if (!AboveFloor(secondary))
                    //{
                    //    secondary.y = TerrainMeta.HeightMap.GetHeight(player.transform.position) + 0.1f;
                    //}
                    portal.Secondary.Location.Vector3 = secondary;
                    portal.ReCreate();

                    SaveData();
                    Message(iplayer, "PortalSecondarySet", args[1]);
                    break;
                case "wipe":
                    portals = new List<PortalInfo>();
                    SaveData();
                    break;
                case "remove":
                case "delete":
                    if (args.Length != 2) { Message(iplayer, "syntax"); return; }

                    ID = args[1];
                    portal = PortalInfo.Find(ID);

                    if (portal == null) { Message(iplayer, "PortalDoesNotExist", args[1]); return; }

                    portal.Remove();
                    portals.Remove(portal);

                    SaveData();
                    Message(iplayer, "PortalRemoved", args[1]);
                    break;
                case "timer":
                case "time":
                    if (args.Length != 3) { Message(iplayer, "syntax"); return; }

                    ID = args[1];
                    portal = PortalInfo.Find(ID);
                    if (portal == null) { Message(iplayer, "PortalDoesNotExist", args[1]); return; }

                    portal.TeleportationTime = float.Parse(args[2]);
                    portal.ReCreate();
                    SaveData();
                    Message(iplayer, "PortalTimerSet", args[1], args[2]);

                    break;
                case "spinner":
                    if (args.Length != 3) { Message(iplayer, "syntax"); return; }

                    ID = args[1];
                    portal = PortalInfo.Find(ID);
                    if (portal == null) { Message(iplayer, "PortalDoesNotExist", args[1]); return; }

                    portal.DeploySpinner = GetBoolValue(args[2]);
                    SaveData();
                    portal.ReCreate();
                    Message(iplayer, "PortalSpinnerSet", ID, args[2]);

                    break;
                case "oneway":
                    if (args.Length != 3) { Message(iplayer, "syntax"); return; }

                    ID = args[1];
                    portal = PortalInfo.Find(ID);
                    if (portal == null) { Message(iplayer, "PortalDoesNotExist", args[1]); return; }

                    portal.OneWay = GetBoolValue(args[2]);
                    portal.ReCreate();
                    SaveData();
                    Message(iplayer, "PortalOneWaySet", ID, args[2]);

                    break;
                case "perm":
                case "permission":
                    Puts($"Args length = {args.Length.ToString()}");
                    if (args.Length != 3) { Message(iplayer, "syntax"); return; }

                    ID = args[1];
                    portal = PortalInfo.Find(ID);
                    if (portal == null) { Message(iplayer, "PortalDoesNotExist", args[1]); return; }

                    portal.RequiredPermission = "portals." + args[2];
                    if (!permission.PermissionExists(portal.RequiredPermission, this))
                    {
                        permission.RegisterPermission(portal.RequiredPermission, this);
                    }
                    portal.ReCreate();
                    SaveData();
                    Message(iplayer, "PortalPermSet", ID, "portals." + args[2]);

                    break;
                case "list":
                    if (args.Length != 1) { Message(iplayer, "syntax"); return; }
                    string portalList = null;
                    if (portals.Count == 0)
                    {
                        portalList = Lang("PortalListEmpty");
                    }
                    else
                    {
                        foreach (PortalInfo p in portals)
                        {
                            var pentrance = p.Primary.Location.Vector3.ToString();
                            var pexit = p.Secondary.Location.Vector3.ToString();
                            var ptime = p.TeleportationTime.ToString() + " " + Lang("seconds");
                            var pperm = Lang("perm") + p.RequiredPermission.Replace("portals.", "");
                            portalList += $"\n<color=#333> - </color><color=#C4FF00>{p.ID} {pentrance} {pexit} [{ptime}, {pperm}]</color>";
                            if (p.OneWay) portalList += "<color=#333> (oneway) </color>";
                            if (p.DeploySpinner) portalList += "<color=#333> (spinner) </color>";
                            portalList += "\n";
                        }
                    }
                    Message(iplayer, "PortalList", portalList);
                    break;
                default:
                    Message(iplayer, "syntax");
                    break;
            }
        }
        #endregion

        #region MonoBehaviour Classes
        private class PortalPlayerHandler : MonoBehaviour
        {
            public Timer timer;
            public BasePlayer player => gameObject.GetComponent<BasePlayer>();

            public void Teleport(PortalEntity portal)
            {
                if (portal.info.CanUse(player))
                {
                    PortalPoint otherPoint = portal.point.PointType == PortalPointType.Primary ? portal.info.Secondary : portal.info.Primary;
                    Instance.Teleport(player, otherPoint.Location.Vector3);

                    if (portal.point.Wheel && Instance.configData.spinEntrance)
                    {
                        (portal.point.Wheel as SpinnerWheel).velocity += Core.Random.Range(2f, 4f);
                    }
                    if (otherPoint.Wheel && Instance.configData.spinExit)
                    {
                        (otherPoint.Wheel as SpinnerWheel).velocity += Core.Random.Range(2f, 4f);
                    }

                    Interface.CallHook("OnPortalUsed", player, JObject.FromObject(portal.info), JObject.FromObject(portal.point));
                }
            }
        }

        private class PortalEntity : MonoBehaviour
        {
            public PortalInfo info = new PortalInfo();
            public PortalPoint point = new PortalPoint();
            private bool isEntered = false;

            public static void Create(PortalInfo info, PortalPoint p)
            {
                p.GameObject = new GameObject();

                PortalEntity portal = p.GameObject.AddComponent<PortalEntity>();
#if DEBUG
                Interface.GetMod().LogWarning("Creating portal object!");
#endif
                p.GameObject.transform.position = p.Location.Vector3;

                portal.info = info;
                portal.point = p;
#if DEBUG
                Interface.GetMod().LogWarning("Creating portal sphere!");
#endif
                p.Sphere = GameManager.server.CreateEntity("assets/prefabs/visualization/sphere.prefab", p.Location.Vector3, new Quaternion(), true).GetComponent<SphereEntity>();
                p.Sphere.currentRadius = 2;
                p.Sphere.lerpSpeed = 0f;
                p.Sphere.Spawn();

                if (info.DeploySpinner)
                {
#if DEBUG
                    Interface.GetMod().LogWarning("Creating portal wheel!");
#endif
                    p.Wheel = GameManager.server.CreateEntity("assets/prefabs/deployable/spinner_wheel/spinner.wheel.deployed.prefab", p.Location.Vector3 + new Vector3(0, 0.02f, 0), new Quaternion(), true) as SpinnerWheel;
                    p.Wheel.Spawn();

                    if (Instance.configData.nameOnWheel)
                    {
                        Instance.NextTick(() =>
                        {
                            Interface.GetMod().LogDebug($"Texture size: {p.Wheel.paintableSources.FirstOrDefault().texWidth.ToString()}");
                            info.ID = info.ID.Trim();
                            int fontsize = Convert.ToInt32(Math.Floor(100f / info.ID.Length / 2));
#if DEBUG
                            Interface.GetMod().LogWarning($"Writing name, {info.ID}, on portal wheel with fontsize {fontsize.ToString()} and color {Instance.configData.spinnerTextColor.ToString()}!");
#endif
                            // Try to paint the sign using one of two versions of SignArtist
                            try
                            {
                                // The version with this call needs to have the function modified to private to work.
                                Instance.SignArtist?.Call("API_SignText", new BasePlayer(), p.Wheel as Signage, info.ID, fontsize, Instance.configData.spinnerTextColor, Instance.configData.spinnerBGColor);
                            }
                            catch
                            {
                                // The versions with this call should work...
                                Instance.SignArtist?.Call("signText", null, p.Wheel as Signage, info.ID, fontsize, Instance.configData.spinnerTextColor, Instance.configData.spinnerBGColor);
                            }
#if DEBUG
                            Interface.GetMod().LogWarning("Writing done, setting flags.");
#endif
                            p.Wheel.SetFlag(BaseEntity.Flags.Busy, true);
                            p.Wheel.SetFlag(BaseEntity.Flags.Reserved3, true);
                        });
                    }
                }
            }

            public void OnTriggerExit(Collider coll)
            {
                if (!Instance.initialized) return;

                GameObject go = coll.gameObject;

                if (go.GetComponent<BasePlayer>())
                {
                    PortalPlayerHandler handler = go.GetComponent<PortalPlayerHandler>();

                    if (handler && handler.timer != null && !handler.timer.Destroyed)
                    {
                        handler.timer.Destroy();
                        Instance.Message(handler.player.IPlayer, "TeleportationCancelled");
                        if (Instance.configData.playEffects) ExitEffects(handler.player);
                    }
                }
                isEntered = false;
            }

            public void OnTriggerEnter(Collider coll)
            {
                if (!Instance.initialized) return;
                isEntered = true;

                GameObject go = coll.gameObject;
                var player = coll.ToBaseEntity() as BasePlayer;

                if (player != null)
                {
                    PortalPlayerHandler handler = player?.gameObject?.GetComponent<PortalPlayerHandler>();
                    if (handler == null)
                    {
                        handler = player.gameObject.AddComponent<PortalPlayerHandler>();
                    }

                    if (player != null && handler != null)
                    {
                        if (point.PointType == PortalPointType.Secondary && info.OneWay) return;
                        if (info.Secondary.Location.Vector3 == Vector3.zero || info.Primary.Location.Vector3 == Vector3.zero)
                        {
                            if (player.IPlayer != null) Instance.Message(player.IPlayer, "PortalIncomplete", info.ID);
                            return;
                        }

                        if (handler.player.IsSleeping()) return;

                        if (!info.CanUse(handler.player))
                        {
                            if (player.IPlayer != null) Instance.Message(player.IPlayer, "NoPermissionPortal");
                            return;
                        }

                        if (Instance.configData.playEffects) EnterEffects(player, info.TeleportationTime);

                        if (player.IPlayer != null) Instance.Message(player.IPlayer, "Teleporting", info.ID, info.TeleportationTime.ToString());
                        handler.timer = Instance.timer.Once(info.TeleportationTime, () => handler.Teleport(this));
                    }
                }
            }

            public void UpdateCollider()
            {
                var coll = gameObject?.transform?.GetOrAddComponent<BoxCollider>(); // FP.Extend
                if (coll == null) return;

                coll.size = new Vector3(1, 2, 1);
                coll.isTrigger = true;
                coll.enabled = true;
            }

            public void Awake()
            {
                gameObject.name = "Portal";
                gameObject.layer = 3;

                var rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
                rigidbody.detectCollisions = true;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;

                UpdateCollider();
            }

            private void SendEffectTo(string effect, BasePlayer player)
            {
                if (player == null) return;
                if (!isEntered) return;

                var EffectInstance = new Effect();
                EffectInstance.Init(Effect.Type.Generic, player, 0, Vector3.up, Vector3.zero);
                EffectInstance.pooledstringid = StringPool.Get(effect);
                NetWrite writer = Net.sv.StartWrite();
                writer.PacketID(Network.Message.Type.Effect);
                EffectInstance.WriteToStream(writer);
                writer.Send(new SendInfo(player.net.connection));
                EffectInstance.Clear(includeNetworkData: true);
            }

            public void EnterEffects(BasePlayer player, float time)
            {
                if (player == null) return;

                SendEffectTo("assets/prefabs/tools/detonator/effects/attack.prefab", player);
                Instance.timer.Once(info.TeleportationTime - 1f, () => SendEffectTo("assets/prefabs/tools/flareold/effects/ignite.prefab", player));
                Instance.timer.Once(info.TeleportationTime - 0.7f, () => SendEffectTo("assets/bundled/prefabs/fx/takedamage_generic.prefab", player));
                Instance.timer.Once(info.TeleportationTime - 0.4f, () => SendEffectTo("assets/prefabs/npc/sam_site_turret/effects/tube_launch.prefab", player));
            }

            public void ExitEffects(BasePlayer player)
            {
                SendEffectTo("assets/prefabs/tools/detonator/effects/deploy.prefab", player);
            }
        }

        private enum PortalPointType
        {
            Primary,
            Secondary
        }
        #endregion

        #region Portal Classes
        private class PortalInfo
        {
            public string ID;
            public readonly PortalPoint Primary = new PortalPoint { PointType = PortalPointType.Primary };
            public readonly PortalPoint Secondary = new PortalPoint { PointType = PortalPointType.Secondary };
            public bool OneWay = true;
            public bool DeploySpinner = false;
            public float TeleportationTime = Instance.configData.defaultCountdown;
            public string RequiredPermission = "portals.use";

            private bool _created;

            private void Update()
            {
                Primary.PointType = PortalPointType.Primary;
                Secondary.PointType = PortalPointType.Secondary;
            }

            public void ReCreate()
            {
                Remove();
                Create();
            }

            public void Create()
            {
                Update();
#if DEBUG
                Interface.GetMod().LogWarning("Creating portal Primary");
#endif
                PortalEntity.Create(this, Primary);
#if DEBUG
                Interface.GetMod().LogWarning("Creating portal Secondary");
#endif
                PortalEntity.Create(this, Secondary);
                _created = true;
            }

            public void Remove()
            {
                if (!_created) return;
#if DEBUG
                Interface.GetMod().LogWarning($"Removing portal {ID}");
#endif
                if (Primary.Wheel != null)
                {
                    if (!Primary.Wheel.IsDestroyed) Primary.Wheel.Kill();
                    if (!Primary.Sphere.IsDestroyed) Primary.Sphere.Kill();
                }
                UnityEngine.Object.Destroy(Primary.GameObject);

                if (Secondary.Wheel != null)
                {
                    if (!Secondary.Wheel.IsDestroyed) Secondary.Wheel.Kill();
                    if (!Secondary.Sphere.IsDestroyed) Secondary.Sphere.Kill();
                }
                UnityEngine.Object.Destroy(Secondary.GameObject);

                List<SpinnerWheel> wheels = new List<SpinnerWheel>();
                Vis.Entities(Primary.Location.Vector3, 0.05f, wheels);
                foreach (var wheel in wheels) wheel.Kill();
                Vis.Entities(Secondary.Location.Vector3, 0.05f, wheels);
                foreach (var wheel in wheels) wheel.Kill();

                _created = false;
            }

            public bool CanUse(BasePlayer player)
            {
                if (!Instance.permission.UserHasPermission(player.UserIDString, RequiredPermission)) return false;
                if (Instance.configData.useNoEscape && Instance.NoEscape != null)
                {
                    if ((bool)Instance.NoEscape?.Call("IsBlocked", player))
                    {
                        Instance.Message(player.IPlayer, "blocked");
                        return false;
                    }
                }
                return true;
            }

            public static PortalInfo Find(string ID) => Instance.portals.Find((p) => p.ID == ID);

            public override int GetHashCode() => ID.GetHashCode();

            public PortalInfo(string ID)
            {
                this.ID = ID;
            }

            public PortalInfo() { }
        }

        private class PortalPoint
        {
            public readonly Location Location = new Location();
            internal PortalPointType PointType;
            internal GameObject GameObject;
            internal SphereEntity Sphere;
            internal SpinnerWheel Wheel;
        }

        private class Location
        {
            public string _location = "0 0 0";

            internal Vector3 Vector3
            {
                get
                {
                    float[] vars = (from var in _location.Split(' ') select Convert.ToSingle(var)).ToArray();
                    return new Vector3(vars[0], vars[1], vars[2]);
                }
                set { _location = $"{value.x} {value.y} {value.z}"; }
            }
        }
        #endregion

        #region Teleportation Helper
        private void Teleport(BasePlayer player, Vector3 position)
        {
            if (player.net?.connection != null)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            }

            player.SetParent(null, true, true);
            player.EnsureDismounted();
            player.Teleport(position);
            player.UpdateNetworkGroup();
            player.StartSleeping();
            player.SendNetworkUpdateImmediate(false);

            if (player.net?.connection != null)
            {
                player.ClientRPC(RpcTarget.Player("StartLoading", player));
            }
        }

        private bool AboveFloor(Vector3 position)
        {
            RaycastHit hit;
            if (Physics.Raycast(position, Vector3.down, out hit, 0.2f, LayerMask.GetMask("Construction")))
            {
                var entity = hit.GetEntity();
                if (entity.PrefabName.Contains("floor") || entity.PrefabName.Contains("foundation"))// || position.y < entity.WorldSpaceBounds().ToBounds().max.y))
                {
                    return true;
                }
            }
            return false;
        }

        private bool BadLocation(Vector3 location)
        {
            // Avoid placing portal in a rock or foundation, water, etc.
            int layerMask = LayerMask.GetMask("Construction", "World");
            RaycastHit hit;
            if (Physics.Raycast(new Ray(location, Vector3.down), out hit, 6f, layerMask))
            {
                return true;
            }
            else if (Physics.Raycast(new Ray(location, Vector3.up), out hit, 6f, layerMask))
            {
                return true;
            }
            else if (Physics.Raycast(new Ray(location, Vector3.forward), out hit, 2f, layerMask))
            {
                return true;
            }
            if ((TerrainMeta.HeightMap.GetHeight(location) - TerrainMeta.WaterMap.GetHeight(location)) >= 0)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region Finding Helper
        private BasePlayer GetPlayer(string searchedPlayer, BasePlayer player)
        {
            foreach (BasePlayer current in BasePlayer.activePlayerList)
            {
                if (string.Equals(current.displayName, searchedPlayer, StringComparison.OrdinalIgnoreCase))
                {
                    return current;
                }
            }

            List<BasePlayer> foundPlayers =
                (from current in BasePlayer.activePlayerList
                 where current.displayName.IndexOf(searchedPlayer, StringComparison.OrdinalIgnoreCase) >= 0
                 select current).ToList();

            switch (foundPlayers.Count)
            {
                case 0:
                    SendReply(player, "The player can not be found.");
                    break;
                case 1:
                    return foundPlayers[0];
                default:
                    List<string> playerNames = (from current in foundPlayers select current.displayName).ToList();
                    string players = string.Join(", ", playerNames.ToArray());
                    SendReply(player, "Multiple matching players found: \n" + players);
                    break;
            }

            return null;
        }
        #endregion

        #region Data Helper
        private static bool GetBoolValue(string bvalue)
        {
            if (bvalue == null) return false;
            bvalue = bvalue.Trim().ToLower();
            switch (bvalue)
            {
                case "t":
                case "true":
                case "1":
                case "yes":
                case "y":
                case "on":
                    return true;
                default:
                    return false;
            }
        }

        private void LoadData()
        {
            portals = Interface.GetMod().DataFileSystem.ReadObject<List<PortalInfo>>(Name);
        }

        private void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject(Name, portals);
        }
        #endregion

        #region messages
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Message(Lang(key, player.Id, args));
        #endregion

        #region config
        public class ConfigData
        {
            public bool wipeOnNewSave = true;
            public bool defaultTwoWay = false;
            public bool playEffects = false;
            public bool useNoEscape = false;
            public float defaultCountdown = 5f;
            public bool deploySpinner = true;
            public bool nameOnWheel = true;
            public bool spinEntrance = true;
            public bool spinExit = true;
            public string spinnerBGColor = "000000";
            public string spinnerTextColor = "00FF00";
            public VersionNumber Version;
        }

        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < new VersionNumber(2, 2, 1) || configData.Version == null)
            {
                try
                {
                    if (Config["Deploy spinner at portal points"] is bool) configData.deploySpinner = (bool)Config["Deploy spinner at portal points"];
                    if (Config["Wipe portals on new save (monthly)"] is bool) configData.wipeOnNewSave = (bool)Config["Wipe portals on new save (monthly)"];
                    if (Config["Write portal name on spinners"] is bool) configData.nameOnWheel = (bool)Config["Write portal name on spinners"];
                    if (Config["Spinner Background Color"] is string) configData.spinnerBGColor = (string)Config["Spinner Background Color"];
                    if (Config["Spinner Text Color"] is string) configData.spinnerTextColor = (string)Config["Spinner Text Color"];
                    if (Config["Spin entrance wheel on teleport"] is bool) configData.spinEntrance = (bool)Config["Spin entrance wheel on teleport"];
                    if (Config["Spin exit wheel on teleport"] is bool) configData.spinExit = (bool)Config["Spin exit wheel on teleport"];
                    if (Config["Set two-way portals by default"] is bool) configData.defaultTwoWay = (bool)Config["Set two-way portals by default"];
                    if (Config["Portal countdown in seconds"] is float) configData.defaultCountdown = (float)Config["Portal countdown in seconds"];
                    if (Config["Play AV effects on teleport"] is bool) configData.playEffects = (bool)Config["Play AV effects on teleport"];
                }
                catch { }
            }

            configData.Version = Version;
            SaveConfig(configData);
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            var config = new ConfigData
            {
                Version = Version
            };
        }

        private void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion
    }
}
