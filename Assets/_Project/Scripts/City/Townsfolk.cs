using System.Collections.Generic;
using System.Linq;
using BrickMuseum.Build;
using BrickMuseum.Character;
using BrickMuseum.Data;
using UnityEngine;

namespace BrickMuseum.City
{
    /// <summary>
    /// The people of the town. Every house brings two residents, public buildings and service vehicles bring their staff
    /// in uniform (police, firefighters, street cleaners, doctors ...). They stroll round the pavement of their block,
    /// keep to their side, stop now and then and wait when the player stands in their way.
    /// </summary>
    public sealed class Townsfolk
    {
        private const float Ring = CityLayout.Block / 2f - CityLayout.Pavement / 2f;   // pavement centre line
        private const float Lane = 1.3f;                                             // each walking direction keeps to one side
        private const float Child = 1.35f;

        private sealed class Walker
        {
            public string key, role;
            public BrickPerson person;
            public Area area;
            public float f;            // where on the ring: 0..4, one unit per side
            public int dir;            // +1 / -1 round the block
            public float speed, pause, nextPause, moved;
            public System.Random rnd;
            public Vector3 pos;
            public bool shown = true;
        }

        private readonly Transform root;
        private readonly Dictionary<string, Walker> walkers = new Dictionary<string, Walker>();

        /// <summary>A built animal out for a walk: the model itself, waddling round its block's pavement.</summary>
        private sealed class Animal
        {
            public ModelDef model;
            public Transform t;
            public Area area;
            public float f, speed, pause, nextPause, phase;
            public int dir;
            public System.Random rnd;
            public Vector3 pos;
            public bool shown = true;
        }

        private readonly Dictionary<string, Animal> animals = new Dictionary<string, Animal>();

        public Townsfolk(Transform cityRoot)
        {
            root = new GameObject("People").transform;
            root.SetParent(cityRoot, false);
        }

        public int Count => walkers.Count;

        // ---------- who lives where ----------

        /// <summary>The people a placed model brings: role ids (see <see cref="Dress"/>); the city score counts them too.</summary>
        public static string[] RolesOf(ModelDef m) => RolesFor(m);

        private static string[] RolesFor(ModelDef m)
        {
            switch (m.kind)
            {
                case "house": return new[] { "resident", "resident" };
                case "big": return new[] { "office", "office" };
                case "sport": return new[] { "player", "player", "player" };
                case "hotel": return new[] { "tourist", "tourist", "office" };
                case "golf": return new[] { "tourist", "player" };
                case "civic":
                    switch (m.id)
                    {
                        case "okul": return new[] { "teacher", "child", "child" };
                        case "belediye": return new[] { "police", "cleaner", "office" };
                        case "hastane": return new[] { "doctor", "nurse" };
                        case "itfaiyeist": return new[] { "firefighter", "firefighter" };
                        case "kutuphane": return new[] { "librarian", "resident" };
                        case "balikhali": return new[] { "shopkeeper", "resident" };
                        case "karakol": return new[] { "police", "police", "office" };
                        default: return new[] { "office" };
                    }
                case "historic": return new[] { m.id == "kale" ? "soldier" : m.id == "saray" ? "guard" : "shopkeeper", "tourist" };
                case "landmark": return new[] { "tourist", "tourist" };
                case "animal": return new[] { "keeper" };
                case "vehicle":
                    switch (m.id)
                    {
                        case "polis": return new[] { "police" };
                        case "cop": return new[] { "cleaner" };
                        case "itfaiye": return new[] { "firefighter" };
                        case "ambulans": return new[] { "paramedic" };
                        case "kepce": return new[] { "worker" };
                        default: return new string[0];
                    }
                default: return new string[0];
            }
        }

        public static string RoleName(string role)
        {
            switch (role)
            {
                case "police": return "Polis";
                case "firefighter": return "İtfaiyeci";
                case "cleaner": return "Temizlik görevlisi";
                case "doctor": return "Doktor";
                case "nurse": return "Hemşire";
                case "paramedic": return "Sağlık görevlisi";
                case "teacher": return "Öğretmen";
                case "child": return "Öğrenci";
                case "office": return "Memur";
                case "librarian": return "Kütüphaneci";
                case "soldier": return "Kale muhafızı";
                case "guard": return "Saray muhafızı";
                case "shopkeeper": return "Esnaf";
                case "tourist": return "Turist";
                case "keeper": return "Bakıcı";
                case "worker": return "İşçi";
                case "player": return "Sporcu";
                case "farmer": return "Çiftçi";
                default: return "Komşu";
            }
        }

        private static readonly string[] Skins = { "cream", "skin", "skin", "skin2", "brown" };
        private static readonly string[] Hairs = { "brown", "brown", "black", "black", "yellow", "dred", "gray", "orange" };
        private static readonly string[] Shirts = { "blue", "red", "white", "green", "yellow", "orange", "mint", "lilac", "black", "lblue", "rose" };
        private static readonly string[] Pants = { "dblue", "dblue", "black", "sand", "brown", "gray", "dgreen" };
        private static readonly string[] Shoes = { "black", "black", "brown", "white", "red" };

        private static T Pick<T>(System.Random r, params T[] a) => a[r.Next(a.Length)];

        /// <summary>Everyday clothes, then the uniform of the role on top.</summary>
        private static Outfit Dress(string role, System.Random r)
        {
            var o = new Outfit
            {
                hairStyle = r.NextDouble() < 0.1 ? "bald" : Pick(r, "short", "short", "long", "bangs"),
                hair = Pick(r, Hairs), skin = Pick(r, Skins), shirt = Pick(r, Shirts),
                pattern = r.NextDouble() < 0.2 ? "stripe" : "plain", pants = Pick(r, Pants), shoes = Pick(r, Shoes),
            };
            if (r.NextDouble() < 0.12) { o.hat = "cap"; o.hatColor = Pick(r, "red", "blue", "green", "black", "white"); }
            switch (role)
            {
                case "police": o.shirt = "lblue"; o.pattern = "tie"; o.trim = "dblue"; o.pants = "dblue"; o.shoes = "black"; o.hat = "cap"; o.hatColor = "dblue"; break;
                case "firefighter": o.shirt = "dred"; o.pattern = "stripe"; o.trim = "yellow"; o.pants = "black"; o.shoes = "black"; o.hat = "helmet"; o.hatColor = "red"; break;
                case "cleaner": o.shirt = "orange"; o.pattern = "stripe"; o.trim = "white"; o.pants = "dblue"; o.hat = "cap"; o.hatColor = "orange"; break;
                case "doctor": o.shirt = "white"; o.pattern = "tie"; o.trim = "blue"; o.pants = "gray"; o.shoes = "white"; o.hat = "none"; break;
                case "nurse": o.shirt = "lblue"; o.pattern = "plain"; o.pants = "lblue"; o.shoes = "white"; o.hat = "none"; break;
                case "paramedic": o.shirt = "red"; o.pattern = "stripe"; o.trim = "white"; o.pants = "dblue"; o.hat = "none"; break;
                case "teacher": o.pattern = "tie"; o.trim = ""; o.pants = Pick(r, "gray", "sand", "black"); o.hat = "none"; break;
                case "office": o.shirt = Pick(r, "white", "lblue", "white"); o.pattern = "tie"; o.trim = Pick(r, "red", "dblue", "black"); o.pants = Pick(r, "black", "gray", "dblue"); o.shoes = "black"; o.hat = "none"; break;
                case "librarian": o.pattern = "plain"; o.pants = "brown"; o.hat = "none"; break;
                case "soldier": o.shirt = "gray"; o.pattern = "plain"; o.pants = "black"; o.shoes = "black"; o.hat = "helmet"; o.hatColor = "gray"; break;
                case "guard": o.shirt = "red"; o.pattern = "stripe"; o.trim = "gold"; o.pants = "dblue"; o.shoes = "black"; o.hat = "top"; o.hatColor = "dred"; break;
                case "tourist": o.hat = "cap"; o.hatColor = Pick(r, "white", "yellow", "red"); o.shirt = Pick(r, "yellow", "orange", "mint", "rose", "white"); o.pants = Pick(r, "sand", "dblue"); break;
                case "keeper": o.shirt = "dgreen"; o.pattern = "plain"; o.pants = "sand"; o.shoes = "brown"; o.hat = "cap"; o.hatColor = "dgreen"; break;
                case "worker": o.shirt = "blue"; o.pattern = "stripe"; o.trim = "orange"; o.pants = "dblue"; o.hat = "helmet"; o.hatColor = "yellow"; break;
                case "child": o.hat = r.NextDouble() < 0.3 ? "cap" : "none"; o.pattern = "plain"; break;
                case "player": o.shirt = Pick(r, "red", "blue", "yellow", "white"); o.pattern = "stripe"; o.trim = "white"; o.pants = "white"; o.shoes = Pick(r, "black", "white"); o.hat = "none"; break;
                case "farmer": o.shirt = Pick(r, "red", "green", "lblue"); o.pattern = "stripe"; o.trim = "black"; o.pants = "dblue"; o.shoes = "brown"; o.hat = "cap"; o.hatColor = Pick(r, "yellow", "sand", "red"); break;
            }
            return o;
        }

        /// <summary>Everyday clothes for someone in town (a taxi passenger).</summary>
        public static Outfit Resident(System.Random r) => Dress("resident", r);

        private static int Seed(string s) { unchecked { int h = (int)2166136261; foreach (char c in s) h = (h ^ c) * 16777619; return h; } }

        // ---------- keeping the crowd in step with the city ----------

        /// <summary>Adds the people of newly placed models and sends away those whose model left.</summary>
        public void Sync()
        {
            var want = new HashSet<string>();
            foreach (var p in Progress.Data.placements)
            {
                var s = CityLayout.Find(p.slot);
                var m = ModelCatalog.Find(p.id);
                if (s == null || m == null || s.area.creek) continue;   // nobody lives on the bridge
                var roles = RolesFor(m);
                for (int i = 0; i < roles.Length; i++)
                {
                    string key = $"{p.slot}|{p.id}|{i}";
                    want.Add(key);
                    if (!walkers.ContainsKey(key)) walkers[key] = Spawn(key, roles[i], s);
                }
            }
            // the animals built: out of their pens, strolling round the block
            var wantAnimals = new HashSet<string>();
            foreach (var p in Progress.Data.placements)
            {
                var s = CityLayout.Find(p.slot);
                var m = ModelCatalog.Find(p.id);
                if (s == null || m == null || m.kind != "animal") continue;
                string key = $"{p.slot}|{p.id}";
                wantAnimals.Add(key);
                if (!animals.ContainsKey(key)) animals[key] = SpawnAnimal(key, m, s);
            }
            foreach (var k in animals.Keys.Where(k => !wantAnimals.Contains(k)).ToList()) { Object.Destroy(animals[k].t.gameObject); animals.Remove(k); }
            // the staff the city bonuses brought, walking round the public blocks
            var beats = new[] { "Kamu Binaları", "Şehir Parkı", "Merkez", "Türk Mahallesi", "Sahil Parkı" }
                .Select(CityLayout.AreaNamed).Where(a => a != null && a.slots.Count > 0).ToList();
            int beat = 0;
            foreach (var (role, n) in new[] { ("police", Progress.Data.bonusPolice), ("cleaner", Progress.Data.bonusWorkers), ("office", Progress.Data.bonusStaff) })
                for (int i = 0; i < n && beats.Count > 0; i++)
                {
                    string key = $"bonus|{role}|{i}";
                    want.Add(key);
                    if (!walkers.ContainsKey(key)) walkers[key] = Spawn(key, role, beats[beat % beats.Count].slots[0]);
                    beat++;
                }
            // the village's farmers, once it is open: one for every two fields, one more for each farm building
            if (District.IsOpen)
            {
                var village = CityLayout.Areas.Where(a => a.far && a.slots.Count > 0).ToList();
                int farmers = 1 + Progress.Data.fields.Count(f => f.owned) / 2 + Progress.Data.barns.Count(b => b.owned);
                for (int i = 0; i < farmers && village.Count > 0; i++)
                {
                    string key = $"farmer|{i}";
                    want.Add(key);
                    if (!walkers.ContainsKey(key)) walkers[key] = Spawn(key, "farmer", village[i % village.Count].slots[0]);
                }
            }
            var gone = new List<string>();
            foreach (var k in walkers.Keys) if (!want.Contains(k)) gone.Add(k);
            foreach (var k in gone) { Object.Destroy(walkers[k].person.root.gameObject); walkers.Remove(k); }
        }

        private Walker Spawn(string key, string role, Slot s)
        {
            var r = new System.Random(Seed(key));
            var w = new Walker
            {
                key = key, role = role, area = s.area, rnd = r, dir = r.Next(2) == 0 ? 1 : -1,
                speed = role == "child" ? 1.9f : (float)(1.15 + r.NextDouble() * 0.6),
                nextPause = (float)(3 + r.NextDouble() * 12),
            };
            w.person = new BrickPerson(root, Dress(role, r), role == "child" ? Child : BrickPerson.Height, true);
            w.person.root.name = RoleName(role);
            // start on the pavement next to where the model stands, a little apart from the household
            w.f = Mathf.Repeat(RingParam(s.local - s.area.center) + (float)(r.NextDouble() - 0.5) * 0.25f, 4f);
            Place(w, 0f);
            return w;
        }

        private Animal SpawnAnimal(string key, ModelDef m, Slot s)
        {
            var r = new System.Random(Seed(key));
            var a = new Animal
            {
                model = m, area = s.area, rnd = r, dir = r.Next(2) == 0 ? 1 : -1,
                speed = (float)(0.7 + r.NextDouble() * 0.6) * Mathf.Clamp(m.height * m.scale / 2f, 0.6f, 1.6f),
                nextPause = (float)(4 + r.NextDouble() * 10), phase = (float)r.NextDouble() * 10f,
                f = Mathf.Repeat(RingParam(s.local - s.area.center), 4f),
            };
            a.t = new GameObject(m.name).transform;
            a.t.SetParent(root, false);
            var body = PieceFactory.BuildModel(m).transform;
            body.SetParent(a.t, false);
            body.localRotation = Quaternion.Euler(0, -90f, 0);          // (the models' fronts point to +x)
            body.localScale = Vector3.one * m.scale;
            body.name = "Body";
            return a;
        }

        /// <summary>The ring position (0..4) nearest to a point given relative to the block centre.</summary>
        private static float RingParam(Vector2 p)
        {
            // sides: 0 south (+z) east→west, 1 west (-x) south→north, 2 north (-z) west→east, 3 east (+x) north→south
            float ax = Mathf.Abs(p.x), az = Mathf.Abs(p.y);
            float t(float v) => Mathf.Clamp01((v + Ring) / (2f * Ring));
            if (az >= ax) return p.y >= 0 ? 0f + t(-p.x) : 2f + t(p.x);
            return p.x < 0 ? 1f + t(-p.y) : 3f + t(p.y);
        }

        private static readonly Vector2[] Corners = { new Vector2(Ring, Ring), new Vector2(-Ring, Ring), new Vector2(-Ring, -Ring), new Vector2(Ring, -Ring) };

        /// <summary>Point on the ring (relative to the block centre) and the way forward along it.</summary>
        private static (Vector2 at, Vector2 along) OnRing(float f, float radius)
        {
            int side = Mathf.FloorToInt(f) % 4;
            float k = f - Mathf.Floor(f);
            var a = Corners[side] * (radius / Ring);
            var b = Corners[(side + 1) % 4] * (radius / Ring);
            return (Vector2.Lerp(a, b, k), (b - a).normalized);
        }

        private void Place(Walker w, float stepYaw)
        {
            float radius = Ring + Lane * w.dir;   // clockwise walkers on the outer side, the others on the inner side
            var (at, along) = OnRing(w.f, radius);
            var c = w.area.center + at;
            w.pos = new Vector3(c.x, 0f, c.y);                                    // on the plan; shown on the shaped ground
            var fwd = along * w.dir;
            if (w.pause > 0f) fwd = Quaternion.Euler(0, 0, 90f * w.dir) * (Vector3)fwd;  // stops facing the street or the houses
            w.person.root.localPosition = CityShape.Point(c, CityMap.Lawn + 0.03f);
            var want = Quaternion.Euler(0, CityShape.Angle(c), 0) * Quaternion.LookRotation(new Vector3(fwd.x, 0, fwd.y));
            w.person.root.localRotation = stepYaw <= 0f ? want : Quaternion.RotateTowards(w.person.root.localRotation, want, stepYaw);
        }

        /// <summary>
        /// Moves everyone. Only the people within <paramref name="radius"/> of <paramref name="focus"/> (world) are drawn and
        /// animated; the rest keep walking unseen. <paramref name="player"/> blocks the way when given.
        /// </summary>
        public void Tick(float dt, Vector3 focus, float radius, Vector3? player = null)
        {
            var fp = CityMap.PlanOf(focus);
            var localFocus = new Vector3(fp.x, 0, fp.y);
            Vector3? localPlayer = null;
            if (player.HasValue) { var pp = CityMap.PlanOf(player.Value); localPlayer = new Vector3(pp.x, 0, pp.y); }
            foreach (var w in walkers.Values)
            {
                float speed = w.speed;
                if (w.pause > 0f) { w.pause -= dt; speed = 0f; }
                else if ((w.nextPause -= dt) <= 0f)
                {
                    w.pause = (float)(1.5 + w.rnd.NextDouble() * 3.5);
                    w.nextPause = (float)(6 + w.rnd.NextDouble() * 14);
                    if (w.rnd.NextDouble() < 0.15) w.dir = -w.dir;   // turns back now and then
                }
                if (localPlayer.HasValue && speed > 0f)
                {
                    var ahead = localPlayer.Value - w.pos;
                    ahead.y = 0;
                    var fwd = w.person.root.localRotation * Vector3.forward;
                    if (ahead.magnitude < 1.8f && Vector3.Dot(ahead, fwd) > 0f) speed = 0f;   // waits for the player
                }
                float radiusHere = Ring + Lane * w.dir;
                w.f = Mathf.Repeat(w.f + w.dir * speed * dt / (2f * radiusHere), 4f);
                w.moved = speed;

                var d = new Vector2(w.pos.x - localFocus.x, w.pos.z - localFocus.z);
                bool show = d.sqrMagnitude < radius * radius;
                if (show != w.shown) { w.person.root.gameObject.SetActive(show); w.shown = show; }
                if (!show) { w.pos = new Vector3(w.area.center.x, 0, w.area.center.y); continue; }
                Place(w, 360f * dt);
                w.person.Animate(speed, false, dt);
            }
            TickAnimals(dt, localFocus, radius);
        }

        /// <summary>The animals walk on (slower near the player), waddle and bob, stop to look round now and then.</summary>
        private void TickAnimals(float dt, Vector3 localFocus, float radius)
        {
            foreach (var a in animals.Values)
            {
                float speed = a.speed;
                if (a.pause > 0f) { a.pause -= dt; speed = 0f; }
                else if ((a.nextPause -= dt) <= 0f)
                {
                    a.pause = (float)(1.5 + a.rnd.NextDouble() * 3);
                    a.nextPause = (float)(5 + a.rnd.NextDouble() * 12);
                    if (a.rnd.NextDouble() < 0.25) a.dir = -a.dir;
                }
                float rad = Ring - Lane * 0.5f * a.dir;
                a.f = Mathf.Repeat(a.f + a.dir * speed * dt / (2f * rad), 4f);
                var (at, along) = OnRing(a.f, rad);
                var c = a.area.center + at;
                a.pos = new Vector3(c.x, 0, c.y);
                var d = new Vector2(a.pos.x - localFocus.x, a.pos.z - localFocus.z);
                bool show = radius <= 0f || d.sqrMagnitude < radius * radius;
                if (show != a.shown) { a.t.gameObject.SetActive(show); a.shown = show; }
                if (!show) continue;
                a.phase += dt * (speed > 0f ? 9f : 2f);
                var fwd = along * a.dir;
                a.t.localPosition = CityShape.Point(c, CityMap.Lawn + 0.03f + (speed > 0f ? Mathf.Abs(Mathf.Sin(a.phase)) * 0.12f : 0f));
                var want = Quaternion.Euler(0, CityShape.Angle(c), 0) * Quaternion.LookRotation(new Vector3(fwd.x, 0, fwd.y));
                a.t.localRotation = Quaternion.RotateTowards(a.t.localRotation, want, 240f * dt) * Quaternion.Euler(0, 0, speed > 0f ? Mathf.Sin(a.phase) * 5f : 0f);
            }
        }

        /// <summary>Where the first animal out walking is (world), for the tour.</summary>
        public Vector3? FirstAnimal() => animals.Count > 0 ? animals.Values.First().t.position : (Vector3?)null;

        public int AnimalCount => animals.Count;

        /// <summary>Who stands closest to a world point (within <paramref name="within"/>), for the walk mode's name chip.</summary>
        public string Nearest(Vector3 world, float within, out float dist)
        {
            var lp = CityMap.PlanOf(world);
            var l = new Vector3(lp.x, 0, lp.y);
            string best = null;
            dist = within;
            foreach (var w in walkers.Values)
            {
                if (!w.shown) continue;
                float d = new Vector2(w.pos.x - l.x, w.pos.z - l.z).magnitude;
                if (d < dist) { dist = d; best = RoleName(w.role); }
            }
            foreach (var a in animals.Values)
            {
                if (!a.shown) continue;
                float d = new Vector2(a.pos.x - l.x, a.pos.z - l.z).magnitude;
                if (d < dist) { dist = d; best = a.model.name; }
            }
            return best;
        }
    }
}
