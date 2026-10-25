using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SquashBot.Visual
{
    /// <summary>
    /// The fans as made people (Tripo spectators: a young man, a young woman, a man with a cap): each type's idle, cheer
    /// and clap baked into 8 still poses (Resources/Crowd/crowd_&lt;type&gt;.fbx, see E:/CLAUDE/scripts/bl_crowd_bake.py),
    /// drawn many at once; their t-shirts (the meshes' second part) in their side's colour. Idle while their side is
    /// quiet, cheering or clapping when it cheers. Without the models the old block fans stay.
    /// </summary>
    public partial class DuelStands
    {
        private static readonly string[] FanTypes = { "genc_erkek", "genc_kadin", "amca" };
        private const int FanIdle = 0, FanCheer = 1, FanClap = 2, FanPoses = 8;
        /// <summary>Each animation's poses a second (its loop's length over the 8 poses), and a fan's height (times the crowd size).</summary>
        private static readonly float[] FanPoseRate = { 3f, 3.6f, 6.5f };
        private const float FanHeight = 1.05f;
        /// <summary>How the made fan is turned so its face looks where its facing says.</summary>
        private const float FanTurn = 0f;

        /// <summary>[type][anim][pose], null: no models (the block fans).</summary>
        private static Mesh[][][] fanMeshes;
        private static Texture2D[] fanTextures;
        private static bool fanLoaded;

        private struct Fan { public Vector3 pos; public float facing, phase, speed; public int team, type; public bool claps; }
        private readonly List<Fan> fans = new List<Fan>();
        private Material[] fanBody;         // [type]
        private Material[,] fanShirt;       // [type, team]
        private readonly Dictionary<int, List<Matrix4x4>> fanDraw = new Dictionary<int, List<Matrix4x4>>();

        private static bool FanModels
        {
            get
            {
                if (fanLoaded) return fanMeshes != null;
                fanLoaded = true;
                var all = new Mesh[FanTypes.Length][][];
                var tex = new Texture2D[FanTypes.Length];
                for (int t = 0; t < FanTypes.Length; t++)
                {
                    var model = Resources.Load<GameObject>("Crowd/crowd_" + FanTypes[t]);
                    tex[t] = Resources.Load<Texture2D>("Crowd/crowd_" + FanTypes[t] + "_tex");
                    if (model == null || tex[t] == null) return false;
                    var byName = new Dictionary<string, Mesh>();
                    foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true)) byName[mf.name] = mf.sharedMesh;
                    all[t] = new Mesh[3][];
                    string[] anims = { "idle", "cheer", "clap" };
                    for (int a = 0; a < 3; a++)
                    {
                        all[t][a] = new Mesh[FanPoses];
                        for (int i = 0; i < FanPoses; i++)
                            if (!byName.TryGetValue(FanTypes[t] + "_" + anims[a] + "_" + i, out all[t][a][i]) || all[t][a][i] == null) return false;
                    }
                }
                fanMeshes = all;
                fanTextures = tex;
                return true;
            }
        }

        /// <summary>One made fan standing at <paramref name="at"/> (the seats its side fills), false: no models.</summary>
        private bool AddMadeFan(Vector3 at, int team, float facing, float phase, System.Random rng)
        {
            if (!FanModels) return false;
            if (fanBody == null)
            {
                fanBody = new Material[FanTypes.Length];
                fanShirt = new Material[FanTypes.Length, 2];
                for (int t = 0; t < FanTypes.Length; t++)
                {
                    fanBody[t] = FanMaterial(fanTextures[t], Color.white);
                    fanShirt[t, 0] = FanMaterial(fanTextures[t], new Color(0.35f, 0.6f, 1.25f));  // (the grey shirt times blue)
                    fanShirt[t, 1] = FanMaterial(fanTextures[t], new Color(1.3f, 0.38f, 0.34f));  // (and red)
                }
            }
            fans.Add(new Fan { pos = at, facing = facing, phase = phase, speed = 0.85f + (float)rng.NextDouble() * 0.3f, team = team, type = rng.Next(FanTypes.Length), claps = rng.NextDouble() < 0.4 });
            return true;
        }

        private Material FanMaterial(Texture2D tex, Color tint)
        {
            var m = MaterialFactory.CreateSurface(tint, Color.black, 0.05f, 0f);
            m.SetTexture("_BaseMap", tex); m.SetTexture("_MainTex", tex);
            m.EnableKeyword("_EMISSION");
            m.SetTexture("_EmissionMap", tex);
            m.SetColor("_EmissionColor", tint * 0.18f); // (a little of their own light: never dark blobs in the stands)
            m.enableInstancing = true;
            mats.Add(m);
            return m;
        }

        /// <summary>Each frame: every made fan's pose (its side quiet: idle; cheering: cheer or clap), drawn many at once.</summary>
        private void DrawMadeFans(float time)
        {
            if (fans.Count == 0) return;
            foreach (var l in fanDraw.Values) l.Clear();
            float scale = FanHeight * crowdSize;
            foreach (var f in fans)
            {
                float ex = excite[f.team];
                bool up = ex > 0.3f || (wave && Mathf.Pow(Mathf.Max(0f, Mathf.Cos(f.pos.x * 0.4f - waveT)), 8f) > 0.4f);
                int anim = up ? (f.claps ? FanClap : FanCheer) : FanIdle;
                int pose = (int)(time * FanPoseRate[anim] * f.speed + f.phase * FanPoses) % FanPoses;
                float jump = up ? Mathf.Max(0f, Mathf.Sin(time * (6f + ex * 4f) + f.phase)) * ex * 0.15f * crowdSize : 0f;
                var m = Matrix4x4.TRS(f.pos + Vector3.up * jump, Quaternion.Euler(0f, f.facing + FanTurn, 0f), Vector3.one * scale);
                int key = ((f.type * 3 + anim) * FanPoses + pose) * 2 + f.team;
                if (!fanDraw.TryGetValue(key, out var list)) fanDraw[key] = list = new List<Matrix4x4>();
                list.Add(m);
            }
            foreach (var kv in fanDraw)
            {
                if (kv.Value.Count == 0) continue;
                int key = kv.Key, team = key % 2, pose = (key / 2) % FanPoses, anim = (key / 2 / FanPoses) % 3, type = key / 2 / FanPoses / 3;
                var mesh = fanMeshes[type][anim][pose];
                for (int k = 0; k < kv.Value.Count; k += 1023)
                {
                    var part = k == 0 && kv.Value.Count <= 1023 ? kv.Value : kv.Value.GetRange(k, Mathf.Min(1023, kv.Value.Count - k));
                    Graphics.DrawMeshInstanced(mesh, 0, fanBody[type], part, null, ShadowCastingMode.Off, true);
                    if (mesh.subMeshCount > 1) Graphics.DrawMeshInstanced(mesh, 1, fanShirt[type, team], part, null, ShadowCastingMode.Off, true);
                }
            }
        }
    }
}
