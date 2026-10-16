using System;
using System.Collections.Generic;
using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Kingdom
{
    /// <summary>What a building is (the palace first; the rest come with the building stage).</summary>
    public enum KmKind { Palace = 0 }

    /// <summary>One building standing in the kingdom: its kind, where, which way, how far built.</summary>
    [Serializable]
    public class KmBuilding
    {
        public int kind;
        public float x, z, rot;
        public int level = 1;
        /// <summary>0 a bare plot … 1 finished.</summary>
        public float built = 1f;
    }

    /// <summary>
    /// The kingdom as the game keeps it: its four resources (food, wood, stone, gold), its buildings, where the camera
    /// last looked. JSON under one key, so the whole-game cloud copy (CloudSave) carries it with everything else.
    /// </summary>
    [Serializable]
    public class KmSave
    {
        private const string Key = "sb_km";

        public int version;
        public int food = 200, wood = 200, stone = 100, gold = 100;
        public List<KmBuilding> buildings = new List<KmBuilding>();
        public float camX = -1f, camZ = -1f, camDist = 34f, camYaw;
        /// <summary>The kingdom was founded (the palace raised, the first visit's welcome shown).</summary>
        public bool founded;

        private static KmSave current;

        public static KmSave Current
        {
            get
            {
                if (current != null) return current;
                string json = Prefs.GetString(Key, "");
                if (json.Length > 0) { try { current = JsonUtility.FromJson<KmSave>(json); } catch (Exception) { current = null; } }
                if (current == null) current = new KmSave();
                if (current.buildings == null) current.buildings = new List<KmBuilding>();
                return current;
            }
        }

        public void Save()
        {
            version++;
            Prefs.SetString(Key, JsonUtility.ToJson(this));
            Prefs.Save();
        }

        /// <summary>The first visit: the palace in the middle of the first region.</summary>
        public void Found()
        {
            if (founded) return;
            founded = true;
            var mid = KmRegions.Centre(KmRegions.Start);
            buildings.Add(new KmBuilding { kind = (int)KmKind.Palace, x = mid.x, z = mid.y, rot = 0f });
            camX = mid.x;
            camZ = mid.y - 6f;
            Save();
        }
    }

    /// <summary>
    /// The kingdom's land: 7 × 7 regions. The first (the middle one) is the kingdom's from the start; every world of
    /// floors won opens the next one, round and round outwards from the middle.
    /// </summary>
    public static class KmRegions
    {
        public const int N = 7;
        public const float Size = 48f;
        public const float World = N * Size;
        public static readonly Vector2Int Start = new Vector2Int(N / 2, N / 2);

        private static List<Vector2Int> order;

        /// <summary>The regions in the order they open: the middle, then ring by ring, each ring round from the south.</summary>
        public static List<Vector2Int> Order
        {
            get
            {
                if (order != null) return order;
                order = new List<Vector2Int>();
                for (int x = 0; x < N; x++)
                    for (int y = 0; y < N; y++) order.Add(new Vector2Int(x, y));
                order.Sort((a, b) =>
                {
                    int ra = Ring(a), rb = Ring(b);
                    if (ra != rb) return ra.CompareTo(rb);
                    return Angle(a).CompareTo(Angle(b));
                });
                return order;
            }
        }

        public static int Ring(Vector2Int r) => Mathf.Max(Mathf.Abs(r.x - Start.x), Mathf.Abs(r.y - Start.y));
        private static float Angle(Vector2Int r) => Mathf.Repeat(Mathf.Atan2(r.x - Start.x, -(r.y - Start.y)) * Mathf.Rad2Deg, 360f);

        public static Vector2 Centre(Vector2Int r) => new Vector2((r.x + 0.5f) * Size, (r.y + 0.5f) * Size);
        public static Vector2Int Of(Vector2 p) => new Vector2Int(Mathf.Clamp(Mathf.FloorToInt(p.x / Size), 0, N - 1), Mathf.Clamp(Mathf.FloorToInt(p.y / Size), 0, N - 1));

        /// <summary>How many regions are the kingdom's: the first, and one for every world of floors won (all in test builds).</summary>
        public static int OpenCount
        {
            get
            {
                if (SaveData.TestMode) return N * N;
                int worlds = 0;
                for (int w = 0; w < LevelCatalog.WorldCount; w++) if (Army.WorldDone(w)) worlds++;
                return Mathf.Clamp(1 + worlds, 1, N * N);
            }
        }

        public static bool IsOpen(Vector2Int r)
        {
            int n = OpenCount;
            var o = Order;
            for (int i = 0; i < n; i++) if (o[i] == r) return true;
            return false;
        }

        /// <summary>The smallest rectangle round every open region (the camera stays over it).</summary>
        public static Rect OpenBounds()
        {
            int n = OpenCount;
            var o = Order;
            float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                x0 = Mathf.Min(x0, o[i].x * Size); z0 = Mathf.Min(z0, o[i].y * Size);
                x1 = Mathf.Max(x1, (o[i].x + 1) * Size); z1 = Mathf.Max(z1, (o[i].y + 1) * Size);
            }
            return Rect.MinMaxRect(x0, z0, x1, z1);
        }
    }
}
