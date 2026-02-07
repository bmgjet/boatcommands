using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("boatcommands", "bmgjet", "1.0.0")]
    [Description("Boat control UI")]
    public class boatcommands : RustPlugin
    {
        private const string BoatUI = "BoatCommandsUI";
        private const string CannonUI = "BoatCannonsUI";
        private const string PermUse = "boatcommands.use";

        private const float CommandCooldown = 1f;
        private const float UIRefreshInterval = 3f;
        private const float AnchorDelay = 3f;

        private readonly Dictionary<ulong, float> lastUse = new();
        private readonly Dictionary<ulong, Timer> refreshTimers = new();

        private class ManCannonData
        {
            public ulong OwnerId;
            public PlayerBoat Boat;
            public Cannon Cannon;
            public BasePlayer Bot;
        }

        private readonly Dictionary<int, ManCannonData> manCannons = new();
        private int cannonUidCounter = 0;

        #region Oxide

        void Init()
        {
            permission.RegisterPermission(PermUse, this);
        }

        void Unload()
        {
            if (manCannons.Count != 0)
            {
                foreach (var b in manCannons)
                {
                    if (b.Value != null)
                    {
                        if (b.Value.Cannon != null)
                        {
                            b.Value.Cannon.DismountAllPlayers();
                        }
                        if (b.Value.Bot != null)
                        {
                            b.Value.Bot.Kill();
                        }
                    }
                }
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                StopRefresh(player);
                DestroyBoatUI(player);
            }
        }

        #endregion

        private void KitPlayer(BasePlayer npc)
        {
            Item item = ItemManager.CreateByName("hazmatsuit", 1, 0);
            timer.Once(1, () =>
            {
                if (item != null && npc != null && npc.net != null && npc.net.group != null && npc?.inventory?.containerWear != null)
                {
                    npc.inventory.containerWear.onItemAddedRemoved = null;
                    npc.inventory.containerWear.Insert(item);
                    npc?.SendNetworkUpdateImmediate();
                    npc.inventory.ServerUpdate(0f);
                    item.MarkDirty();
                }
            });
        }

        private int CreateNPC(Cannon cannon, BasePlayer player, PlayerBoat boat)
        {
            if (cannon.IsMounted()) { return 0; }
            var bot = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", cannon.mountAnchor.position, cannon.mountAnchor.rotation) as BasePlayer;
            if (bot == null) { return 0; }
            bot.enableSaving = false;
            bot.Spawn();
            bot.displayName = RandomUsernames.Get(bot.userID);
            KitPlayer(bot);
            cannon.MountPlayer(bot);
            cannon.AdminReload(1);
            cannon.SendNetworkUpdate();
            int uid = ++cannonUidCounter;
            manCannons[uid] = new ManCannonData
            {
                OwnerId = player.userID,
                Boat = boat,
                Cannon = cannon,
                Bot = bot
            };
            return uid;
        }

        #region Commands

        [ConsoleCommand("boat.firecannon")]
        private void CmdFireCannon(ConsoleSystem.Arg arg)
        {
            CannonFire(arg.Player(), "firecannon", arg.Args);
        }

        [ChatCommand("manallcannons")]
        void ManAllCannons(BasePlayer player, string cmd, string[] args)
        {
            if (!player.IsAdmin) { return; }
            if (player.GetParentEntity() is PlayerBoat boat)
            {
                foreach (var ent in boat.Deployables.Cached)
                {
                    if (ent is Cannon cannon) { CreateNPC(cannon, player, boat); }
                }
            }
        }

        [ChatCommand("mancannon")]
        void ManCannon(BasePlayer player, string cmd, string[] args)
        {
            if (!player.IsAdmin) { return; }

            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 4f))
            {
                player.ChatMessage("Look at a cannon");
                return;
            }

            Cannon cannon = hit.GetEntity() as Cannon;
            if (cannon == null)
            {
                player.ChatMessage("Look at a cannon");
                return;
            }

            if (!(cannon.GetParentEntity() is PlayerBoat boat))
            {
                player.ChatMessage("Cannon not on a player boat");
                return;
            }

            if (cannon.IsMounted())
            {
                player.ChatMessage("This cannon already has a gunner");
                return;
            }
            var uid = CreateNPC(cannon, player, boat);
            if (uid != 0) { player.ChatMessage($"Man cannon installed. Fire with /firecannon {uid}"); }
        }

        [ChatCommand("firecannon")]
        void CannonFire(BasePlayer player, string cmd, string[] args)
        {
            if (!player.IsAdmin) { return; }

            if (args.Length == 0 || !int.TryParse(args[0], out int uid))
            {
                return;
            }

            if (!manCannons.TryGetValue(uid, out var data))
            {
                return;
            }

            if (!data.Cannon.IsLoaded())
            {
                return;
            }

            if (data.Bot == null || data.Bot.IsDead())
            {
                manCannons.Remove(uid);
                player.ChatMessage("Cannon Man Dead");
                return;
            }

            ServerProjectile serverProjectile;
            Cannon cannon = data.Cannon;
            if (cannon.FireProjectile(cannon.AmmoPrefab, cannon.FirePoint.position, cannon.FirePoint.forward, player, 0.25f, 100f, out serverProjectile))
            {
                cannon.SERVER_OnProjectileFired(player.Connection, player);
            }
            if (data.Cannon != null && !data.Cannon.IsDestroyed)
            {
                data.Cannon.AdminReload(1);
                data.Cannon.SendNetworkUpdate();
            }
        }

        [ChatCommand("engine")]
        void ToggleEngine(BasePlayer player, string cmd, string[] args)
        {
            if (!CanUse(player)) return;
            if (!GetBoat(player, out PlayerBoat boat)) return;
            bool engineOn = boat.EnginesOn();

            boat.SetAllEnginesOn(!engineOn);
            RefreshUI(player);
        }

        [ChatCommand("sails")]
        void ToggleSails(BasePlayer player, string cmd, string[] args)
        {
            if (!CanUse(player)) return;
            if (!GetBoat(player, out PlayerBoat boat)) return;

            boat.SetAllSailsOpen(!boat.SailsOpen());
            RefreshUI(player);
        }

        [ChatCommand("reverse")]
        void ToggleReverse(BasePlayer player, string cmd, string[] args)
        {
            if (!CanUse(player)) return;
            if (!GetBoat(player, out PlayerBoat boat)) return;

            bool newReverse = !boat.AnyEngineReverse();

            // Engines
            foreach (var engine in boat.Engines.Cached)
                engine.SetFlag(BaseEntity.Flags.Reserved3, newReverse);

            // Sails rotate with reverse
            foreach (var sail in boat.Sails.Cached)
            {
                if (sail.CanRotate(player))
                {
                    sail.RotateSail(player);
                }
            }

            RefreshUI(player);
        }

        [ChatCommand("boatanchor")]
        void ToggleAnchor(BasePlayer player, string cmd, string[] args)
        {
            if (!CanUse(player)) return;
            if (!GetBoat(player, out PlayerBoat boat)) return;

            foreach (var anchor in boat.Anchors.Cached)
            {
                if (anchor.Lowered)
                    anchor.RaiseAnchor(player);
                else
                    anchor.LowerAnchor(player, false);
            }

            timer.Once(AnchorDelay, () =>
            {
                if (player != null && player.IsConnected)
                    RefreshUI(player);
            });
        }

        #endregion

        #region UI

        void CreateBoatUI(BasePlayer player)
        {
            DestroyBoatUI(player);
            if (!HasPerm(player)) return;
            if (!GetBoat(player, out PlayerBoat boat)) return;

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.35" },
                RectTransform =
                {
                    AnchorMin = "0.925 0.855",
                    AnchorMax = "0.995 0.995"
                }
            }, "Overlay", BoatUI);

            AddBtn(container, BoatUI, "ENGINE", "engine", boat.EnginesOn(), 0, "0.2 0.7 0.2 0.9");
            AddBtn(container, BoatUI, "SAILS", "sails", boat.SailsOpen(), 1, "0.2 0.6 0.9 0.9");
            AddBtn(container, BoatUI, "REVERSE", "reverse", boat.AnyEngineReverse(), 2, "0.9 0.6 0.1 0.9");
            AddBtn(container, BoatUI, "ANCHOR", "boatanchor", boat.AnchorLowered(), 3, "0.2 0.5 0.8 0.9");

            if (player.IsAdmin)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = "0 0 0 0.35" },
                    RectTransform =
                {
                    AnchorMin = "0.20 0.001",
                    AnchorMax = "0.80 0.020"
                }
                }, "Overlay", CannonUI);

                var cannons = manCannons
                    .Where(x => x.Value.Boat == boat)
                    .OrderBy(x => x.Key)
                    .ToList();

                float width = cannons.Count > 0 ? 1f / cannons.Count : 1f;

                for (int i = 0; i < cannons.Count; i++)
                {
                    float xmin = i * width;
                    float xmax = xmin + width;

                    container.Add(new CuiButton
                    {
                        Button =
                    {
                        Color = "0.75 0.25 0.25 0.9",
                        Command = $"boat.firecannon {cannons[i].Key}"
                    },
                        RectTransform =
                    {
                        AnchorMin = $"{xmin} 0.05",
                        AnchorMax = $"{xmax} 0.95"
                    },
                        Text =
                    {
                        Text = $"CANNON {cannons[i].Key}",
                        FontSize = 9,
                        Align = TextAnchor.MiddleCenter
                    }
                    }, CannonUI);
                }
            }
            CuiHelper.AddUi(player, container);
        }

        void AddBtn(CuiElementContainer c, string p, string t, string cmd, bool active, int i, string onColor)
        {
            float h = 0.17f;
            float spacing = 0.055f;
            float anchorMaxY = 0.93f;
            float yMax = anchorMaxY - i * (h + spacing); float yMin = yMax - h;
            c.Add(new CuiButton { Button = { Color = active ? onColor : "0.2 0.2 0.2 0.8", Command = $"chat.say /{cmd}" }, RectTransform = { AnchorMin = $"0.07 {yMin}", AnchorMax = $"0.93 {yMax}" }, Text = { Text = t, FontSize = 11, Align = TextAnchor.MiddleCenter } }, p);
        }

        void DestroyBoatUI(BasePlayer p)
        {
            CuiHelper.DestroyUi(p, BoatUI);
            CuiHelper.DestroyUi(p, CannonUI);
        }

        void RefreshUI(BasePlayer p) => CreateBoatUI(p);



        #endregion

        #region Hooks

        void OnEntityMounted(BaseMountable e, BasePlayer p)
        {
            if (e.prefabID != 1346716961) return;
            CreateBoatUI(p);
            StartRefresh(p);
        }

        void OnEntityDismounted(BaseMountable e, BasePlayer p)
        {
            if (e.prefabID != 1346716961) return;
            StopRefresh(p);
            DestroyBoatUI(p);
        }

        #endregion

        #region Refresh Loop

        void StartRefresh(BasePlayer p)
        {
            StopRefresh(p);
            refreshTimers[p.userID] = timer.Every(UIRefreshInterval, () =>
            {
                if (p == null || !p.IsConnected || !GetBoat(p, out _))
                {
                    StopRefresh(p);
                    return;
                }
                RefreshUI(p);
            });
        }

        void StopRefresh(BasePlayer p)
        {
            if (refreshTimers.TryGetValue(p.userID, out var t))
                t.Destroy();
            refreshTimers.Remove(p.userID);
        }

        #endregion

        #region Helpers

        bool HasPerm(BasePlayer p) =>
            permission.UserHasPermission(p.UserIDString, PermUse);

        bool CanUse(BasePlayer p)
        {
            float now = Time.realtimeSinceStartup;
            if (lastUse.TryGetValue(p.userID, out float last) && now - last < CommandCooldown)
                return false;

            lastUse[p.userID] = now;
            return true;
        }

        bool GetBoat(BasePlayer p, out PlayerBoat boat)
        {
            boat = p?.GetMounted()?.GetParentEntity() as PlayerBoat;
            return boat != null && boat.IsPlayerAuthed(p, true);
        }

        #endregion
    }

    static class BoatExtensions
    {
        public static bool EnginesOn(this PlayerBoat b)
        {
            foreach (var e in b.Engines.Cached)
                if (e.IsOn()) return true;
            return false;
        }

        public static bool AnyEngineReverse(this PlayerBoat b)
        {
            foreach (var e in b.Engines.Cached)
                if (e.InReverse) return true;
            return false;
        }

        public static bool SailsOpen(this PlayerBoat b)
        {
            foreach (var s in b.Sails.Cached)
                if (s.Lowered) return true;
            return false;
        }

        public static bool AnchorLowered(this PlayerBoat b)
        {
            foreach (var a in b.Anchors.Cached)
                if (a.Lowered) return true;
            return false;
        }
    }
}