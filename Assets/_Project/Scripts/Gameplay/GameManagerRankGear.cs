using System.Collections;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The rank gear's ceremony (Docs/RUTBE_KUSAMI_TAC_SAC_BIRLESIK.md, section 2.6): at the rank that opens a new tier,
    /// after the duel's dance, the old crown rises away, the new one comes down shining, then the bracers, pauldrons and
    /// kneepads go on one by one, and NEW GEAR! shows.
    /// </summary>
    public partial class GameManager
    {
        private IEnumerator RankGearCeremony()
        {
            int tier = RankGear.TakeNewTier();
            if (tier <= 0 || robot == null || robot.Visual == null) yield break;
            if (RankGear.Worn != tier) yield break; // (they chose to wear a lower one: no ceremony over it)
            var skin = robot.Visual.GetComponentInChildren<SkinnedHero>();
            if (skin == null) yield break;
            Transform head = null;
            foreach (var t in skin.GetComponentsInChildren<Transform>(true)) if (t.name == "Hero_Head") { head = t; break; }
            if (head == null) yield break;

            // the old crown rises and goes
            var old = head.Find("Mount_Crown");
            if (old != null)
            {
                var oc = old.GetComponent<RankCrown>();
                if (oc != null) oc.enabled = false;
                var from = old.localPosition;
                for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
                {
                    old.localPosition = from + Vector3.up * Mathf.SmoothStep(0f, 1.2f, t / 0.6f);
                    old.localScale *= 0.985f;
                    yield return null;
                }
                if (oc != null) oc.enabled = true; // (it puts its hair back as it goes)
                RankCrown.Put(head, 0, 0);
            }

            // the new one comes down shining
            AudioManager.PlaySfx(Sfx.Shield, 0.9f, 1.4f);
            var crown = RankCrown.Put(head, SkinnedHero.HeadNumber(Cosmetics.Equipped(Slot.Head).id), tier);
            if (crown != null) { crown.Descend(0.8f); crown.Shine(); }
            yield return new WaitForSecondsRealtime(0.9f);

            // then the plates, one kind at a time
            RankGearParts.Put(skin.transform, tier);
            var parts = new System.Collections.Generic.List<GameObject>();
            foreach (var t in skin.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Gear_Rank")) { parts.Add(t.gameObject); t.gameObject.SetActive(false); }
            foreach (var kind in new[] { "Bracer", "Pauldron", "Knee" })
            {
                foreach (var p in parts) if (p.name.EndsWith(kind)) p.SetActive(true);
                AudioManager.PlaySfx(Sfx.Clang, 0.6f, 1.3f);
                Haptics.Light();
                yield return new WaitForSecondsRealtime(0.3f);
            }
            WearLook.Apply(skin.transform, BattleWear.Level); // (the wear reaches the new plates too)
            ui.Float(new Vector2(Screen.width * 0.5f, Screen.height * 0.62f), Loc.T("rank.newGear"), Palette.UiGold, 120f);
            AudioManager.PlaySfx(Sfx.Win, 0.8f, 1.1f);
            yield return new WaitForSecondsRealtime(1.4f);
        }
    }
}
