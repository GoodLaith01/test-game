using System.Collections.Generic;
using Hollowmark.Data;
using Hollowmark.Enemies;
using Hollowmark.Visuals;
using UnityEngine;

namespace Hollowmark.World
{
    /// <summary>
    /// Generates a dungeon from its DungeonDef: a winding chain of rooms joined by corridors, enemy rooms,
    /// supply crates, tent camps every few rooms (always one before the boss), and a boss arena at the end.
    /// A new layout is rolled each visit. Adding a dungeon = adding an entry to dungeons.json.
    /// </summary>
    public static class DungeonBuilder
    {
        const float CorridorLength = 8f;
        const float DoorWidth = 4f;

        static readonly Vector2Int[] Dirs = { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, -1) };

        public static Vector3 Build(DungeonDef def, Transform root, bool bossDefeated, out float yaw)
        {
            var cells = Layout(def.roomCount);
            float spacing = def.roomSize * 1.4f + CorridorLength;
            int last = cells.Count - 1;

            for (int i = 0; i < cells.Count; i++)
            {
                bool isBoss = i == last;
                float size = isBoss ? def.roomSize * 1.4f : def.roomSize;
                var center = new Vector3(cells[i].x * spacing, 0f, cells[i].y * spacing);
                var doors = new List<Vector2Int>();
                if (i > 0) doors.Add(cells[i - 1] - cells[i]);
                if (i < last) doors.Add(cells[i + 1] - cells[i]);

                var room = new GameObject(isBoss ? "BossArena" : "Room" + i).transform;
                room.SetParent(root, false);
                room.localPosition = center;
                Room(room, def, size, doors);

                if (i < last) Corridor(root, def, center, cells[i + 1] - cells[i], size, spacing, i + 1 == last ? def.roomSize * 1.4f : def.roomSize);

                bool camp = i > 0 && !isBoss && (i == last - 1 || (def.campEveryRooms > 0 && i % def.campEveryRooms == 0));
                if (i == 0)
                {
                    MakeExitPortal(room, new Vector3(0f, 0f, -size * 0.3f));
                    Prim.Label(room, new Vector3(0f, 4f, -size * 0.3f), "Way home", Color.white, 0.08f);
                }
                else if (isBoss)
                {
                    if (!bossDefeated) SpawnBoss(def, room);
                    else
                    {
                        MakeExitPortal(room, Vector3.zero);
                        Populate(def, room, size, i, last);
                    }
                }
                else if (camp) Camp(room, def);
                else Populate(def, room, size, i, last);
            }

            // Face the first corridor.
            var dir = cells.Count > 1 ? cells[1] - cells[0] : new Vector2Int(0, 1);
            yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            return new Vector3(0f, 0.1f, 0f);
        }

        /// <summary>Random walk on a grid that never revisits a cell, biased forward so the dungeon winds deeper.</summary>
        static List<Vector2Int> Layout(int count)
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                var cells = new List<Vector2Int> { Vector2Int.zero };
                var used = new HashSet<Vector2Int> { Vector2Int.zero };
                while (cells.Count < count)
                {
                    var options = new List<Vector2Int>();
                    foreach (var d in Dirs)
                    {
                        var c = cells[cells.Count - 1] + d;
                        if (used.Contains(c)) continue;
                        options.Add(c);
                        if (d.y == 1) options.Add(c); // bias north
                    }
                    if (options.Count == 0) break;
                    var next = options[Random.Range(0, options.Count)];
                    cells.Add(next);
                    used.Add(next);
                }
                if (cells.Count == count) return cells;
            }
            var line = new List<Vector2Int>();
            for (int i = 0; i < count; i++) line.Add(new Vector2Int(0, i));
            return line;
        }

        static void Room(Transform room, DungeonDef def, float size, List<Vector2Int> doors)
        {
            float h = def.wallHeight, t = 1f;
            Prim.Box(room, new Vector3(0f, -0.5f, 0f), new Vector3(size, 1f, size), def.Floor, true, "Floor");
            foreach (var d in Dirs)
            {
                bool door = doors.Contains(d);
                Vector3 c = new Vector3(d.x, 0f, d.y) * (size / 2f);
                bool alongX = d.y != 0;
                if (!door)
                {
                    Prim.Box(room, c + Vector3.up * h / 2f, alongX ? new Vector3(size + t, h, t) : new Vector3(t, h, size + t), def.Wall, true, "Wall");
                    continue;
                }
                float seg = (size - DoorWidth) / 2f;
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 offset = (alongX ? Vector3.right : Vector3.forward) * s * (DoorWidth / 2f + seg / 2f + t / 4f);
                    Prim.Box(room, c + offset + Vector3.up * h / 2f, alongX ? new Vector3(seg + t / 2f, h, t) : new Vector3(t, h, seg + t / 2f), def.Wall, true, "Wall");
                }
                Prim.Box(room, c + Vector3.up * (h - 0.5f), alongX ? new Vector3(DoorWidth, 1f, t) : new Vector3(t, 1f, DoorWidth), def.Wall * 0.85f, true, "Lintel");
            }

            // Pillars give creatures places to hide and the player cover from embers.
            int pillars = Random.Range(2, 5);
            for (int i = 0; i < pillars; i++)
            {
                var p = new Vector3(Random.Range(-size * 0.3f, size * 0.3f), 0f, Random.Range(-size * 0.3f, size * 0.3f));
                if (p.magnitude < 3f) continue;
                Prim.Box(room, p + Vector3.up * h / 2f, new Vector3(1.2f, h, 1.2f), def.Wall * 0.9f, true, "Pillar");
            }
            // Rubble and bones.
            for (int i = 0; i < 6; i++)
            {
                var p = new Vector3(Random.Range(-size * 0.45f, size * 0.45f), 0.1f, Random.Range(-size * 0.45f, size * 0.45f));
                Prim.Make(PrimitiveType.Cube, room, p, Vector3.one * Random.Range(0.2f, 0.6f), Prim.Vary(def.Wall, 0.15f), "Rubble")
                    .transform.localRotation = Random.rotation;
            }
            // Torches in the dungeon's accent colour.
            for (int i = 0; i < 2; i++)
            {
                var corner = new Vector3((i == 0 ? -1f : 1f) * (size / 2f - 1f), 2.5f, (i == 0 ? 1f : -1f) * (size / 2f - 1f));
                Prim.Make(PrimitiveType.Cube, room, corner, new Vector3(0.15f, 0.4f, 0.15f), def.Accent, "Torch", false, true);
                Prim.PointLight(room, corner + Vector3.up * 0.3f, def.Accent, size * 0.75f, 1.6f);
            }
        }

        static void Corridor(Transform root, DungeonDef def, Vector3 from, Vector2Int dir, float fromSize, float spacing, float toSize)
        {
            var d3 = new Vector3(dir.x, 0f, dir.y);
            float start = fromSize / 2f, end = spacing - toSize / 2f;
            float len = end - start + 0.5f;
            var mid = from + d3 * (start + end) / 2f;
            bool alongZ = dir.y != 0;
            var c = new GameObject("Corridor").transform;
            c.SetParent(root, false);
            c.localPosition = mid;
            Prim.Box(c, new Vector3(0f, -0.5f, 0f), alongZ ? new Vector3(DoorWidth + 2f, 1f, len) : new Vector3(len, 1f, DoorWidth + 2f), def.Floor * 0.9f, true, "Floor");
            for (int s = -1; s <= 1; s += 2)
            {
                var side = (alongZ ? Vector3.right : Vector3.forward) * s * (DoorWidth / 2f + 0.5f);
                Prim.Box(c, side + Vector3.up * def.wallHeight / 2f, alongZ ? new Vector3(1f, def.wallHeight, len) : new Vector3(len, def.wallHeight, 1f), def.Wall, true, "Wall");
            }
            Prim.PointLight(c, Vector3.up * 3f, def.Accent, 8f, 0.8f);
        }

        static void Populate(DungeonDef def, Transform room, float size, int index, int last)
        {
            if (def.enemies == null || def.enemies.Length == 0) return;
            // Deeper rooms are more crowded.
            float depth = (float)index / Mathf.Max(1, last);
            int count = Mathf.RoundToInt(Mathf.Lerp(def.enemiesPerRoomMin, def.enemiesPerRoomMax, depth)) + Random.Range(0, 2);
            for (int i = 0; i < count; i++)
            {
                var e = GameDatabase.Enemy(def.enemies[Random.Range(0, def.enemies.Length)]);
                if (e == null) continue;
                var p = room.position + new Vector3(Random.Range(-size * 0.35f, size * 0.35f), 0.3f, Random.Range(-size * 0.35f, size * 0.35f));
                EnemyAI.Spawn(e, p, def.tier, room);
            }
            if (Random.value < 0.5f) Cache(room, def, new Vector3(size * 0.38f, 0f, size * 0.38f));
        }

        static void Cache(Transform room, DungeonDef def, Vector3 local)
        {
            var go = new GameObject("SupplyCrate");
            go.transform.SetParent(room, false);
            go.transform.localPosition = local;
            Prim.Box(go.transform, new Vector3(0f, 0.4f, 0f), new Vector3(1f, 0.8f, 0.8f), new Color(0.45f, 0.32f, 0.18f), true, "Crate");
            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.4f;
            go.AddComponent<SupplyCache>().Items = def.supplyItems;
        }

        /// <summary>A safe camp: tents to sleep in, a fire to warm up by, and a crate of supplies.</summary>
        static void Camp(Transform room, DungeonDef def)
        {
            for (int i = 0; i < 2; i++)
            {
                var tent = new GameObject("Tent");
                tent.transform.SetParent(room, false);
                tent.transform.localPosition = new Vector3(i == 0 ? -3.5f : 3.5f, 0f, 2f);
                tent.transform.localRotation = Quaternion.Euler(0f, i == 0 ? 20f : -20f, 0f);
                var canvas = new Color(0.75f, 0.68f, 0.5f);
                var l = Prim.Box(tent.transform, new Vector3(-0.6f, 0.8f, 0f), new Vector3(0.08f, 2f, 2.6f), canvas, false, "Canvas");
                l.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
                var r = Prim.Box(tent.transform, new Vector3(0.6f, 0.8f, 0f), new Vector3(0.08f, 2f, 2.6f), canvas, false, "Canvas");
                r.transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
                Prim.Box(tent.transform, new Vector3(0f, 0.05f, 0f), new Vector3(1.4f, 0.1f, 2.4f), new Color(0.4f, 0.2f, 0.15f), false, "Bedroll");
                var trigger = tent.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(2.5f, 2f, 3.2f);
                trigger.center = Vector3.up;
                tent.AddComponent<Tent>();
            }
            var fire = new GameObject("Campfire");
            fire.transform.SetParent(room, false);
            fire.transform.localPosition = new Vector3(0f, 0f, -1f);
            for (int i = 0; i < 4; i++)
                Prim.Make(PrimitiveType.Cylinder, fire.transform, new Vector3(0f, 0.1f, 0f), new Vector3(0.12f, 0.5f, 0.12f), new Color(0.3f, 0.2f, 0.1f), "Log")
                    .transform.localRotation = Quaternion.Euler(80f, i * 45f, 0f);
            Prim.Make(PrimitiveType.Sphere, fire.transform, new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.6f, 0.5f), new Color(1f, 0.55f, 0.15f), "Flame", false, true);
            Prim.PointLight(fire.transform, Vector3.up, new Color(1f, 0.6f, 0.3f), 12f, 2.5f);
            var ft = fire.AddComponent<SphereCollider>();
            ft.isTrigger = true;
            ft.radius = 1.5f;
            fire.AddComponent<Campfire>();
            Cache(room, def, new Vector3(0f, 0f, 4.5f));
            Prim.Label(room, new Vector3(0f, 4f, 2f), "Camp (safe to sleep)", new Color(1f, 0.9f, 0.6f), 0.08f);
        }

        static void SpawnBoss(DungeonDef def, Transform room)
        {
            var boss = GameDatabase.Boss(def.bossId);
            if (boss == null) return;
            var ai = BossAI.SpawnBoss(boss, room.position + new Vector3(0f, 0.3f, 4f), def.id, def.tier, room);
            GameManager.Instance.ActiveBoss = ai;
            Prim.Label(room, new Vector3(0f, boss.height + 2.5f, 4f), boss.name + "\n" + boss.title, new Color(1f, 0.4f, 0.3f), 0.1f);
        }

        public static GameObject MakeExitPortal(Transform parent, Vector3 local)
        {
            var go = new GameObject("ExitPortal");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            if (parent == GameManager.Instance.WorldRoot) go.transform.position = local; // world position given
            Prim.Make(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.05f, 0f), new Vector3(2.2f, 0.05f, 2.2f), new Color(0.5f, 0.8f, 1f), "PortalRing", false, true);
            Prim.Make(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 1.5f, 0f), new Vector3(1.2f, 1.5f, 1.2f), new Color(0.4f, 0.7f, 1f) * 0.6f, "PortalGlow", false, true);
            Prim.PointLight(go.transform, Vector3.up * 1.5f, new Color(0.5f, 0.8f, 1f), 8f, 2f);
            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.6f;
            trigger.center = Vector3.up;
            go.AddComponent<ExitPortal>();
            return go;
        }
    }
}
