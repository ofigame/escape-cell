using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>A shield foi can carry on its left arm: its model, its price (or a chest's), how soon it is ready again after a block.</summary>
    public class ShieldDef
    {
        public string id, model;
        public int price;
        /// <summary>Only from the golden chests.</summary>
        public bool chest;
        /// <summary>Seconds before it can take the next blow.</summary>
        public float recharge;
        /// <summary>How wide the front it covers (degrees either side of where foi faces).</summary>
        public float arc;
        /// <summary>How big it is carried (the golden ones bigger).</summary>
        public float size = 1f;
    }

    /// <summary>
    /// The shields. Carried on foi's left arm (on the menu, in the workshop, on the floors and in the duels); a blow
    /// that comes from the front, while the shield is ready, lands on it instead: no health lost, the shield needs a
    /// moment before the next. Better shields cover more and are ready sooner. The plain wooden one is foi's from the
    /// start; the steel one is bought; the two golden ones come only from the chests.
    /// </summary>
    public static class Shields
    {
        public static readonly List<ShieldDef> All = new List<ShieldDef>
        {
            new ShieldDef { id = "shield.wood", model = "basit_baslangic_kalkan", price = 0, recharge = 3.2f, arc = 55f },
            new ShieldDef { id = "shield.steel", model = "ikinci_seviye_kalkan", price = 6500, recharge = 2.4f, arc = 65f },
            new ShieldDef { id = "shield.stag", model = "altin_kasa_kalkan", chest = true, recharge = 1.7f, arc = 75f, size = 1.25f },
            new ShieldDef { id = "shield.amethyst", model = "altin_kasa_kalkan_2", chest = true, recharge = 1.5f, arc = 80f, size = 1.3f },
        };

        public static ShieldDef Get(string id) => All.Find(s => s.id == id);

        public static bool Owned(ShieldDef s) => s != null && (s.price == 0 && !s.chest || Prefs.GetInt("sb_shield_own_" + s.id, 0) == 1);

        public static void Grant(ShieldDef s)
        {
            if (s == null) return;
            Prefs.SetInt("sb_shield_own_" + s.id, 1);
            Prefs.Save();
        }

        /// <summary>The shield carried (the wooden one unless another was chosen; null only if none is owned).</summary>
        public static ShieldDef Equipped
        {
            get
            {
                var s = Get(Prefs.GetString("sb_shield_eq", "shield.wood"));
                return Owned(s) ? s : Get("shield.wood");
            }
        }

        public static void Equip(ShieldDef s)
        {
            if (!Owned(s)) return;
            Prefs.SetString("sb_shield_eq", s.id);
            Prefs.Save();
        }

        /// <summary>Buys a shield from the shop (its price in gold); true when done.</summary>
        public static bool TryBuy(ShieldDef s)
        {
            if (s == null || s.chest || Owned(s) || !Shop.Spend(s.price)) return false;
            Grant(s);
            Equip(s);
            return true;
        }

        /// <summary>The golden chests' shields not owned yet.</summary>
        public static List<ShieldDef> ChestLeft() => All.FindAll(s => s.chest && !Owned(s));

        // ---------- The block ----------

        private static float readyAt;

        /// <summary>Whether the shield takes a blow coming from <paramref name="from"/> at foi standing at <paramref name="at"/>
        /// facing <paramref name="facing"/>: a ready shield and a blow from its front. Takes the shield's time to be ready again.</summary>
        public static bool Blocks(Vector3 from, Vector3 at, Vector3 facing)
        {
            var s = Equipped;
            if (s == null || Time.time < readyAt) return false;
            var d = from - at; d.y = 0f; facing.y = 0f;
            if (d.sqrMagnitude < 1e-4f || facing.sqrMagnitude < 1e-4f) return false;
            if (Vector3.Angle(facing, d) > s.arc) return false;
            readyAt = Time.time + s.recharge;
            return true;
        }

        /// <summary>0..1: how ready the shield is again.</summary>
        public static float Readiness => Equipped == null ? 0f : Mathf.Clamp01(1f - (readyAt - Time.time) / Equipped.recharge);

        /// <summary>A new fight: the shield ready.</summary>
        public static void Ready() => readyAt = 0f;
    }
}
