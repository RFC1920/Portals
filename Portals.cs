//#define DEBUG
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using System.Linq;
using Facepunch.Extend;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Portals", "LaserHydra/RFC1920", "2.1.5", ResourceId = 1234)]
    [Description("Create portals and feel like in Star Trek")]
    class Portals : RustPlugin
    {
        #region Global Declaration
        private List<PortalInfo> portals = new List<PortalInfo>();
        public static Portals Instance = null;

        private const string permPortalsUse = "portals.use";
        private const string permPortalsAdmin = "portals.admin";

        private bool deploySpinner = true;
        private bool defaultTwoWay = false;
        private bool nameOnWheel = true;
        private string bgColor = "000000";
        private string textColor = "00FF00";
        private bool spinEntrance = true;
        private bool spinExit = true;
        private float teleTimer;

        [PluginReference]
        private Plugin SignArtist;

        public bool initialized = false;
        #endregion

        #region Oxide Hooks
        void OnNewSave(string strFilename)
        {
            Puts("Map change - wiping portal locations");
            foreach(var portal in portals)
            {
                portal.Primary.Location.Vector3 = new Vector3();
                portal.Secondary.Location.Vector3 = new Vector3();
            }
            SaveData();
        }

        private void OnServerInitialized()
        {
            Instance = this;

            LoadVariables();
            LoadData();
            LoadMessages();

            AddCovalenceCommand("portal", "CmdPortal");

            permission.RegisterPermission(permPortalsUse, this);
            permission.RegisterPermission(permPortalsAdmin, this);

            foreach(PortalInfo portal in portals)
            {
                if(!permission.PermissionExists(portal.RequiredPermission, this))
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
            foreach(PortalInfo portal in portals)
            {
                if(portal != null) portal.Remove();
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if(player.gameObject.GetComponent<PortalPlayerHandler>())
            {
                UnityEngine.Object.Destroy(player.gameObject.GetComponent<PortalPlayerHandler>());
            }
        }

        private object CanPickupEntity(BasePlayer player, BaseEntity entity)
        {
            if(entity == null || player == null) return null;
            if(entity.ShortPrefabName != "spinner.wheel.deployed") return null;

            foreach(PortalInfo p in portals)
            {
                if(deploySpinner)
                {
                    if(p.Primary.Wheel.net.ID == entity.net.ID || p.Secondary.Wheel.net.ID == entity.net.ID)
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
            if(entity == null || hitInfo == null) return null;
            if(entity.ShortPrefabName != "spinner.wheel.deployed") return null;

            foreach(PortalInfo p in portals)
            {
                if(deploySpinner)
                {
                    if(p.Primary.Wheel.net.ID == entity.net.ID || p.Secondary.Wheel.net.ID == entity.net.ID)
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

        #region Loading
        private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "NoPermission", "You don't have permission to use this command." },
                { "NoPermissionPortal", "You don't have permission to use this portal." },
                { "PortalDoesNotExist", "Portal {0} does not exist." },
                { "PortalPrimarySet", "Primary for portal {0} was set at your current location." },
                { "PortalSecondarySet", "Secondary for portal {0} was set at your current location." },
                { "PortalOneWaySet", "OneWay for portal {0} was set to {1}." },
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
            if(!iplayer.HasPermission(permPortalsAdmin)) { Message(iplayer, "NoPermission"); return; }
            if(args.Length == 0) { Message(iplayer, "syntax"); return; }

            var player = iplayer.Object as BasePlayer;
            string ID;
            PortalInfo portal;

            switch(args[0])
            {
                case "entrance":
                case "pri":
                case "primary":
                case "add":
                case "create":
                    if(args.Length != 2) { Message(iplayer, "syntax"); return; }

                    ID = args[1];
                    portal = PortalInfo.Find(ID);

                    if(portal == null)
                    {
                        portal = new PortalInfo(ID);
                        portals.Add(portal);
                    }

                    portal.Primary.Location.Vector3 = player.transform.position;
                    portal.OneWay = !defaultTwoWay;
                    portal.ReCreate();

                    SaveData();
                    Message(iplayer, "PortalPrimarySet", args[1]);
                    break;
                case "exit":
                case "sec":
                case "secondary":
                    if(args.Length != 2) { Message(iplayer, "syntax"); return; }

                    ID = args[1];
                    portal = PortalInfo.Find(ID);

                    if(portal == null)
                    {
                        portal = new PortalInfo(ID);
                        portals.Add(portal);
                    }

                    portal.Secondary.Location.Vector3 = player.transform.position;
                    portal.ReCreate();

                    SaveData();
                    Message(iplayer, "PortalSecondarySet", args[1]);
                    break;
                case "remove":
                case "delete":
                    if(args.Length != 2) { Message(iplayer, "syntax"); return; }

                    ID = args[1];
                    portal = PortalInfo.Find(ID);

                    if(portal == null) { Message(iplayer, "PortalDoesNotExist", args[1]); return; }

                    portal.Remove();
                    portals.Remove(portal);

                    SaveData();
                    Message(iplayer, "PortalRemoved", args[1]);
                    break;
                case "timer":
                case "time":
                    if(args.Length != 3) { Message(iplayer, "syntax"); return; }

                    ID = args[1];
                    portal = PortalInfo.Find(ID);
                    if(portal == null) { Message(iplayer, "PortalDoesNotExist", args[1]); return; }

                    portal.TeleportationTime = float.Parse(args[2]);
                    portal.ReCreate();
                    SaveData();
                    Message(iplayer, "PortalTimerSet", args[1], args[2]);

                    break;
                case "oneway":
                    if(args.Length != 3) { Message(iplayer, "syntax"); return; }

                    ID = args[1];
                    portal = PortalInfo.Find(ID);
                    if(portal == null) { Message(iplayer, "PortalDoesNotExist", args[1]); return; }

                    portal.OneWay = GetBoolValue(args[2]);
                    portal.ReCreate();
                    SaveData();
                    Message(iplayer, "PortalOneWaySet", ID, args[2]);

                    break;
                case "perm":
                case "permission":
                    Puts($"Args length = {args.Length.ToString()}");
                    if(args.Length != 3) { Message(iplayer, "syntax"); return; }

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
                    if(args.Length != 1) { Message(iplayer, "syntax"); return; }
                    string portalList = null;
                    if(portals.Count == 0)
                    {
                        portalList = Lang("PortalListEmpty");
                    }
                    else
                    {
                        foreach(PortalInfo p in portals)
                        {
                            var pentrance = p.Primary.Location.Vector3.ToString();
                            var pexit = p.Secondary.Location.Vector3.ToString();
                            var ptime = p.TeleportationTime.ToString() + " " + Lang("seconds");
                            var pperm = Lang("perm") + p.RequiredPermission.Replace("portals.", "");
                            portalList += $"\n<color=#333> - </color><color=#C4FF00>{p.ID} {pentrance} {pexit} [{ptime}, {pperm}]</color>";
                            if(p.OneWay) portalList += "<color=#333> (oneway) </color>";
                            portalList += "\n";
                        }
                    }
                    Message(iplayer, "PortalList", portalList);
                    break;
//                case "config":
//                    if(args.Length == 1)
//                    {
//                        //display config
//                        string showconfig  = "\n  " + Lang("cDeploySpinner", null, deploySpinner.ToString());
//                        showconfig += "\n  " + Lang("cNameOnWheel", null, nameOnWheel.ToString());
//                        showconfig += "\n  " + Lang("cBgColor", null, bgColor);
//                        showconfig += "\n  " + Lang("cTextColor", null, textColor);
//                        showconfig += "\n  " + Lang("cSpinEntrance", null, spinEntrance.ToString());
//                        showconfig += "\n  " + Lang("cSpinExit", null, spinExit.ToString());
//                        showconfig += "\n  " + Lang("cDefaultTwoWay", null, defaultTwoWay.ToString());
//                        showconfig += "\n  " + Lang("cTeleTimer", null, teleTimer.ToString());
//                        Message(iplayer, "config", showconfig);
//                    }
//                    else if(args.Length != 3)
//                    {
//                        Message(iplayer, "syntax");
//                        return;
//                    }
//                    else
//                    {
//                        // /portal config teleTimer 7
//                    }
//                    break;
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
                if(portal.info.CanUse(player))
                {
                    PortalPoint otherPoint = portal.point.PointType == PortalPointType.Primary ? portal.info.Secondary : portal.info.Primary;
                    Instance.Teleport(player, otherPoint.Location.Vector3);

                    if(portal.point.Wheel && Instance.spinEntrance)
                    {
                        (portal.point.Wheel as SpinnerWheel).velocity += Core.Random.Range(2f, 4f);
                    }
                    if(otherPoint.Wheel && Instance.spinExit)
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

            public static void Create(PortalInfo info, PortalPoint p)
            {
                p.GameObject = new GameObject();

                PortalEntity portal = p.GameObject.AddComponent<PortalEntity>();
#if DEBUG
                Interface.Oxide.LogWarning($"Creating portal object!");
#endif
                p.GameObject.transform.position = p.Location.Vector3;

                portal.info = info;
                portal.point = p;
#if DEBUG
                Interface.Oxide.LogWarning($"Creating portal sphere!");
#endif
                p.Sphere = GameManager.server.CreateEntity("assets/prefabs/visualization/sphere.prefab", p.Location.Vector3, new Quaternion(), true).GetComponent<SphereEntity>();
                p.Sphere.currentRadius = 2;
                p.Sphere.lerpSpeed = 0f;
                p.Sphere.Spawn();

                if(Instance.deploySpinner)
                {
#if DEBUG
                    Interface.Oxide.LogWarning($"Creating portal wheel!");
#endif
                    p.Wheel = GameManager.server.CreateEntity("assets/prefabs/deployable/spinner_wheel/spinner.wheel.deployed.prefab", p.Location.Vector3 + new Vector3(0, 0.02f, 0), new Quaternion(), true) as SpinnerWheel;
                    p.Wheel.Spawn();

                    if (Instance.nameOnWheel)
                    {
                        Instance.NextTick(() =>
                        {
#if DEBUG
                            Interface.Oxide.LogWarning($"Writing name, {info.ID}, on portal wheel!");
#endif
                            int fontsize = Convert.ToInt32(Math.Floor(285f / info.ID.Length));
                            Instance.SignArtist?.Call("signText", null, p.Wheel as Signage, info.ID, fontsize, Instance.textColor, Instance.bgColor);

                            p.Wheel.SetFlag(BaseEntity.Flags.Busy, true);
                            p.Wheel.SetFlag(BaseEntity.Flags.Reserved3, true);
                        });
                    }
                }
            }

            public void OnTriggerExit(Collider coll)
            {
                if(!Instance.initialized) return;

                GameObject go = coll.gameObject;

                if(go.GetComponent<BasePlayer>())
                {
                    PortalPlayerHandler handler = go.GetComponent<PortalPlayerHandler>();

                    if(handler && handler.timer != null && !handler.timer.Destroyed)
                    {
                        handler.timer.Destroy();
                        Instance.Message(handler.player.IPlayer, "TeleportationCancelled");
                    }
                }
            }

            public void OnTriggerEnter(Collider coll)
            {
                if(!Instance.initialized) return;

                GameObject go = coll.gameObject;
                var player = coll.ToBaseEntity() as BasePlayer;

                if(player != null)
                {
                    PortalPlayerHandler handler = player?.gameObject?.GetComponent<PortalPlayerHandler>();
                    if(handler == null)
                    {
                        handler = player.gameObject.AddComponent<PortalPlayerHandler>();
                    }

                    if(player != null && handler != null)
                    {
                        if(point.PointType == PortalPointType.Secondary && info.OneWay) return;
                        if(info.Secondary.Location.Vector3 == Vector3.zero || info.Primary.Location.Vector3 == Vector3.zero)
                        {
                            if(player.IPlayer != null) Instance.Message(player.IPlayer, "PortalIncomplete", info.ID);
                            return;
                        }

                        if(handler.player.IsSleeping()) return;

                        if(!info.CanUse(handler.player))
                        {
                            if(player.IPlayer != null) Instance.Message(player.IPlayer, "NoPermissionPortal");
                            return;
                        }

                        if(player.IPlayer != null) Instance.Message(player.IPlayer, "Teleporting", info.ID, info.TeleportationTime.ToString());
                        handler.timer = Instance.timer.Once(info.TeleportationTime, () => handler.Teleport(this));
                    }
                }
            }

            public void UpdateCollider()
            {
                var coll = gameObject?.transform?.GetOrAddComponent<BoxCollider>(); // FP.Extend
                if(coll == null) return;

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
            public float TeleportationTime = Instance.teleTimer;
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
                Interface.Oxide.LogWarning($"Creating portal primary");
#endif
                PortalEntity.Create(this, Primary);
#if DEBUG
                Interface.Oxide.LogWarning($"Creating portal secondary");
#endif
                PortalEntity.Create(this, Secondary);
                _created = true;
            }

            public void Remove()
            {
                if(!_created) return;
#if DEBUG
                Interface.Oxide.LogWarning($"Removing portal {ID}");
#endif
                List<SpinnerWheel> wheels = new List<SpinnerWheel>();

                Primary.Wheel.Kill();
                Vis.Entities<SpinnerWheel>(Primary.Location.Vector3, 0.05f, wheels);
                foreach (var wheel in wheels) wheel.Kill();
                Primary.Sphere.Kill();
                UnityEngine.Object.Destroy(Primary.GameObject);

                Secondary.Wheel.Kill();
                Vis.Entities<SpinnerWheel>(Secondary.Location.Vector3, 0.05f, wheels);
                foreach (var wheel in wheels) wheel.Kill();
                Secondary.Sphere.Kill();
                UnityEngine.Object.Destroy(Secondary.GameObject);

                _created = false;
            }

            public bool CanUse(BasePlayer player) => Instance.permission.UserHasPermission(player.UserIDString, RequiredPermission);

            public static PortalInfo Find(string ID) => Instance.portals.Find((p) => p.ID == ID);

            public override int GetHashCode() => ID.GetHashCode();

            public PortalInfo(string ID)
            {
                this.ID = ID;
            }

            public PortalInfo() {}
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
            if(player.net?.connection != null)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            }

            player.SetParent(null, true, true);
            player.EnsureDismounted();
            player.Teleport(position);
            player.UpdateNetworkGroup();
            player.StartSleeping();
            player.SendNetworkUpdateImmediate(false);

            if(player.net?.connection != null)
            {
                player.ClientRPCPlayer(null, player, "StartLoading");
            }
        }
        #endregion

        #region Finding Helper
        private BasePlayer GetPlayer(string searchedPlayer, BasePlayer player)
        {
            foreach(BasePlayer current in BasePlayer.activePlayerList)
            {
                if(current.displayName.ToLower() == searchedPlayer.ToLower())
                {
                    return current;
                }
            }

            List<BasePlayer> foundPlayers =
                (from current in BasePlayer.activePlayerList
                 where current.displayName.ToLower().Contains(searchedPlayer.ToLower())
                 select current).ToList();

            switch(foundPlayers.Count)
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
            if(bvalue == null) return false;
            bvalue = bvalue.Trim().ToLower();
            switch(bvalue)
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
            portals = Interface.Oxide.DataFileSystem.ReadObject<List<PortalInfo>>(Name);
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, portals);
        }
        #endregion

        #region Message Helper
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Message(Lang(key, player.Id, args));
        #endregion

        #region config
        protected override void LoadDefaultConfig()
        {
#if DEBUG
            Puts("Creating a new config file...");
#endif
            deploySpinner = true;
            nameOnWheel = true;
            bgColor = "000000";
            textColor = "00FF00";
            spinEntrance = true;
            spinExit = true;
            defaultTwoWay = false;
            teleTimer = 5f;

            LoadVariables();
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        private void LoadConfigVariables()
        {
            CheckCfg<bool>("Deploy spinner at portal points", ref deploySpinner);
            CheckCfg<bool>("Write portal name on spinners", ref nameOnWheel);
            CheckCfg<string>("Spinner Background Color", ref bgColor);
            CheckCfg<string>("Spinner Text Color", ref textColor);
            CheckCfg<bool>("Spin entrance wheel on teleport", ref spinEntrance);
            CheckCfg<bool>("Spin exit wheel on teleport", ref spinExit);
            CheckCfg<bool>("Set two-way portals by default", ref defaultTwoWay);
            CheckCfgFloat("Portal countdown in seconds", ref teleTimer);
        }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if(Config[Key] is T)
            {
                var = (T)Config[Key];
            }
            else
            {
                Config[Key] = var;
            }
        }

        private void CheckCfgFloat(string Key, ref float var)
        {
            if(Config[Key] != null)
            {
                var = Convert.ToSingle(Config[Key]);
            }
            else
            {
                Config[Key] = var;
            }
        }

        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if(data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
            }

            object value;
            if(!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
            }
            return value;
        }
        #endregion
    }
}
