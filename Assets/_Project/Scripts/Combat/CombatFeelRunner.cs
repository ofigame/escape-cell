using UnityEngine;

namespace SquashBot.Combat
{
    /// <summary>Keeps <see cref="CombatFeel"/> ticking while a blow waits for its weapon's contact (made when first needed).</summary>
    public class CombatFeelRunner : MonoBehaviour
    {
        private static CombatFeelRunner instance;

        public static void Ensure()
        {
            if (instance != null) { instance.enabled = true; return; }
            var go = new GameObject("CombatFeel");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CombatFeelRunner>();
        }

        private void Update()
        {
            if (!CombatFeel.Tick()) enabled = false;
        }
    }
}
