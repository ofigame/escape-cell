using System;
using UnityEngine;

namespace SquashBot.Combat
{
    /// <summary>
    /// Where each weapon is held (Docs/RUTBE_KUSAMI_TAC_SAC_BIRLESIK.md, section 5): the main hand's place on the grip
    /// (Grip_Main, a share of the weapon's length from its butt) and, for the two-handed families, the second hand's
    /// (Grip_Off), and how far the fingers close (a thin sword grip, a thick axe haft). By family, with a row per weapon
    /// where one needs its own. Resources/Combat/WeaponGripData (an asset made from this, when there is one) or these.
    /// </summary>
    [CreateAssetMenu(menuName = "foi/Combat/Weapon Grip Data", fileName = "WeaponGripData")]
    public class WeaponGripData : ScriptableObject
    {
        [Serializable]
        public class Grip
        {
            [Tooltip("A family (Sword, Spear, Axe, Heavy) or a weapon's id")]
            public string key;
            [Tooltip("The main hand's place: a share of the weapon's length from its butt")]
            [Range(0f, 1f)] public float main = 0.12f;
            [Tooltip("Held in both hands: the second hand on the grip too")]
            public bool twoHanded;
            [Tooltip("The second hand's place (two-handed): a share of the length from the butt")]
            [Range(0f, 1f)] public float off = 0.4f;
            [Tooltip("How far the fingers close round the grip (1: a full fist; less: a thick haft)")]
            [Range(0.5f, 1f)] public float curl = 1f;
        }

        public Grip[] grips =
        {
            new Grip { key = "Sword", main = 0.12f, twoHanded = false, curl = 1f },
            new Grip { key = "Axe", main = 0.1f, twoHanded = true, off = 0.36f, curl = 0.9f },
            new Grip { key = "Heavy", main = 0.1f, twoHanded = true, off = 0.34f, curl = 0.88f },
            new Grip { key = "Spear", main = 0.3f, twoHanded = true, off = 0.56f, curl = 0.95f },
        };

        private static WeaponGripData current;

        public static WeaponGripData Current
        {
            get
            {
                if (current == null) current = Resources.Load<WeaponGripData>("Combat/WeaponGripData");
                if (current == null) { current = CreateInstance<WeaponGripData>(); current.hideFlags = HideFlags.DontSave; }
                return current;
            }
        }

        /// <summary>The grip of weapon <paramref name="id"/> (its own row, else its family's, else a one-handed default).</summary>
        public Grip For(string id, string family)
        {
            Grip byFamily = null;
            foreach (var g in grips)
            {
                if (g.key == id) return g;
                if (g.key == family) byFamily = g;
            }
            return byFamily ?? new Grip { key = "default" };
        }
    }
}
