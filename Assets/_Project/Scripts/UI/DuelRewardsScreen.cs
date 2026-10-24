using System;
using System.Collections;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Gameplay;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Kind = SquashBot.UI.UiFactory.ButtonKind;

namespace SquashBot.UI
{
    /// <summary>
    /// The duel's rewards, opened one after another (the scenario's §4.5), each its own small celebration: rank points →
    /// the XP bar (a burst on a level up) → the weapon's mastery → gold → core parts → the chest → the match's challenge
    /// and the special prizes. A tap skips to the end; CONTINUE closes it.
    /// </summary>
    public class DuelRewardsScreen : MonoBehaviour
    {
        private UiScreen screen;
        private RectTransform card, list;
        private TextMeshProUGUI title;
        private Button go, revenge, share, addRival;
        private string shareText, rivalToAdd;
        private Action<string> addFriend;

        /// <summary>The share line and (a live rival) its name for "add as friend", for the next Show.</summary>
        public void SetExtras(string text, string rivalName, Action<string> add)
        {
            shareText = text;
            rivalToAdd = rivalName;
            addFriend = add;
        }
        private Action closed, revenged;
        private bool skip, pendingRevenge;
        private float y;

        public bool IsOpen => screen.IsVisible;

        public static DuelRewardsScreen Create(Transform canvasRoot)
        {
            var s = UiScreen.Create("DuelRewards", canvasRoot, out var root);
            var d = root.gameObject.AddComponent<DuelRewardsScreen>();
            d.screen = s;
            d.Build(root);
            return d;
        }

        private void Build(RectTransform root)
        {
            var dim = UiFactory.Dim(root, new Color(0.02f, 0.02f, 0.08f, 0.78f));
            dim.gameObject.AddComponent<IgnoreSafeArea>();
            var tap = dim.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(() => skip = true);
            card = UiFactory.Card("Card", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 1640f));
            card.pivot = new Vector2(0.5f, 0.5f);
            screen.SetPopTarget(card);
            title = UiFactory.TextBox("Title", card, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(860f, 110f), "", 72f, Palette.UiGold, title: true);
            list = UiFactory.Box("List", card, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(860f, 1300f));
            list.pivot = new Vector2(0.5f, 1f);
            go = UiFactory.MakeButton(card, Loc.T("rw.continue"), Kind.Primary, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(560f, 130f), () => { screen.Hide(); closed?.Invoke(); }, 50f);
            ((RectTransform)go.transform).pivot = new Vector2(0.5f, 0.5f);
            // after a lost friend match: revenge (the friend's copy again)
            revenge = UiFactory.MakeButton(card, Loc.T("riv.revenge"), Kind.Gold, new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(560f, 120f), () => { screen.Hide(); revenged?.Invoke(); }, 44f);
            ((RectTransform)revenge.transform).pivot = new Vector2(0.5f, 0.5f);
            // share the result (with the friend code), add a live rival as a friend
            share = UiFactory.MakeButton(card, Loc.T("rw.share"), Kind.Secondary, new Vector2(0f, 0f), new Vector2(170f, 230f), new Vector2(300f, 100f), () => { if (!string.IsNullOrEmpty(shareText)) Data.Share.Text(shareText); }, 34f);
            ((RectTransform)share.transform).pivot = new Vector2(0.5f, 0.5f);
            addRival = UiFactory.MakeButton(card, Loc.T("rw.addRival"), Kind.Secondary, new Vector2(1f, 0f), new Vector2(-170f, 230f), new Vector2(300f, 100f), () =>
            {
                if (string.IsNullOrEmpty(rivalToAdd)) return;
                addFriend?.Invoke(rivalToAdd);
                addRival.interactable = false;
            }, 30f);
            ((RectTransform)addRival.transform).pivot = new Vector2(0.5f, 0.5f);
        }

        public void Show(GameManager.DuelRewards r, bool won, bool draw, Action onClosed, string revengeOn = null, Action onRevenge = null)
        {
            closed = onClosed;
            revenged = onRevenge;
            revenge.gameObject.SetActive(false);
            share.gameObject.SetActive(false);
            addRival.gameObject.SetActive(false);
            addRival.interactable = true;
            pendingRevenge = !string.IsNullOrEmpty(revengeOn) && onRevenge != null;
            skip = false;
            for (int i = list.childCount - 1; i >= 0; i--) Destroy(list.GetChild(i).gameObject);
            y = 0f;
            title.text = Loc.T(won ? "ch.victory" : draw ? "ch.draw" : "ch.defeat");
            title.color = won ? Palette.UiGold : draw ? new Color(0.8f, 0.85f, 1f) : new Color(1f, 0.5f, 0.45f);
            go.gameObject.SetActive(false);
            list.localScale = Vector3.one;
            Time.timeScale = 1f; // (a slow-motion moment at the duel's end never holds the screen)
            screen.Show();
            transform.SetAsLastSibling();
            StopAllCoroutines();
            StartCoroutine(Safe(Play(r)));
            StartCoroutine(Watchdog());
        }

        /// <summary>The rows' play, a fault in one never leaving the screen without its CONTINUE.</summary>
        private IEnumerator Safe(IEnumerator play)
        {
            while (true)
            {
                object step;
                try
                {
                    if (!play.MoveNext()) break;
                    step = play.Current;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    break;
                }
                yield return step;
            }
            Finish();
        }

        /// <summary>CONTINUE comes in 8 seconds at the latest, whatever the rows do.</summary>
        private IEnumerator Watchdog()
        {
            for (float t = 0f; t < 8f; t += Time.unscaledDeltaTime) yield return null;
            Finish();
        }

        /// <summary>The end of the rows: CONTINUE (and revenge, or share and add) shown once.</summary>
        private void Finish()
        {
            if (go.gameObject.activeSelf) return;
            go.gameObject.SetActive(true);
            StartCoroutine(PopIn(go.transform));
            if (pendingRevenge) { revenge.gameObject.SetActive(true); StartCoroutine(PopIn(revenge.transform)); }
            else
            {
                share.gameObject.SetActive(!string.IsNullOrEmpty(shareText));
                addRival.gameObject.SetActive(!string.IsNullOrEmpty(rivalToAdd));
            }
        }

        private IEnumerator Wait(float s)
        {
            for (float t = 0f; t < s && !skip; t += Time.unscaledDeltaTime) yield return null;
        }

        private IEnumerator Play(GameManager.DuelRewards r)
        {
            yield return Wait(0.35f);
            if (r.trophiesKnown)
            {
                Row(Loc.T("rw.trophies"), (r.trophies >= 0 ? "+" : "") + r.trophies, r.trophies >= 0 ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.5f, 0.45f));
                yield return Wait(0.45f);
            }
            // the XP bar: filling, a burst at each level up
            yield return XpRow(r);
            yield return Wait(0.3f);
            if (r.weapon != null)
            {
                yield return MasteryRow(r);
                yield return Wait(0.3f);
            }
            if (r.gold > 0) { Row(Loc.T("rw.gold"), "+" + r.gold, Palette.UiGold, Sfx.Coin); yield return Wait(0.4f); }
            if (r.cores > 0) { Row(Loc.T("rw.cores"), "+" + r.cores, new Color(0.45f, 0.92f, 1f), Sfx.Coin); yield return Wait(0.4f); }
            if (r.seasonPoints > 0) { Row(Loc.T("sr.title"), "+" + r.seasonPoints, new Color(1f, 0.7f, 0.3f), Sfx.Click); yield return Wait(0.3f); }
            if (r.chest.HasValue) { Row(Loc.T("rw.chest"), DuelChestScreen.TierName(r.chest.Value), ChestModels.StrapColour(r.chest.Value), Sfx.Win); yield return Wait(0.45f); }
            Row(r.goalText, r.goalDone ? Loc.T("rw.done") : Loc.T("rw.notDone"), r.goalDone ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 1f, 1f, 0.45f), r.goalDone ? Sfx.Win : (Sfx?)null);
            yield return Wait(0.35f);
            if (!string.IsNullOrEmpty(r.eventLine)) { Row(r.eventLine, "", new Color(1f, 0.75f, 0.35f), Sfx.Win); yield return Wait(0.35f); }
            if (r.firstWin) { Row(Loc.T("rw.firstWin"), "×3", Palette.UiGold, Sfx.Win); yield return Wait(0.35f); }
            if (r.comeback) { Row(Loc.T("rw.comeback"), "+" + DuelLevel.Comeback + " XP", new Color(1f, 0.7f, 0.3f), Sfx.Win); yield return Wait(0.35f); }
            if (r.streakBreaker) { Row(Loc.T("rw.breaker"), "+200", new Color(1f, 0.45f, 0.35f), Sfx.Win); yield return Wait(0.35f); }
        }

        /// <summary>A reward's row: what it is on the left, how much on the right (popping in, with its sound).</summary>
        private RectTransform Row(string what, string value, Color colour, Sfx? sfx = Sfx.Click)
        {
            var row = UiFactory.Box("Row", list, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(860f, 94f));
            row.pivot = new Vector2(0.5f, 1f);
            y -= 104f;
            Fit();
            UiFactory.Framed(row, Flat.Card, 1.8f).raycastTarget = false;
            var l = UiFactory.TextBox("What", row, new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(560f, 90f), what, 32f, Palette.UiText, FontStyles.Normal, align: TextAlignmentOptions.Left);
            l.rectTransform.pivot = new Vector2(0f, 0.5f);
            l.textWrappingMode = TextWrappingModes.Normal;
            l.enableAutoSizing = true; l.fontSizeMin = 20f; l.fontSizeMax = 32f;
            var v = UiFactory.TextBox("Value", row, new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(260f, 90f), value, 44f, colour, title: true, align: TextAlignmentOptions.Right);
            v.rectTransform.pivot = new Vector2(1f, 0.5f);
            v.enableAutoSizing = true; v.fontSizeMin = 22f; v.fontSizeMax = 44f;
            if (sfx.HasValue) AudioManager.PlaySfx(sfx.Value, 0.8f, 1.15f);
            StartCoroutine(PopIn(row));
            return row;
        }

        /// <summary>A bar under a row (0..1), returning its fill to grow.</summary>
        private RectTransform Bar(RectTransform row, float k, Color colour)
        {
            row.sizeDelta = new Vector2(860f, 124f);
            y -= 30f;
            Fit();
            var back = UiFactory.Box("Bar", row, new Vector2(0f, 0f), new Vector2(28f, 22f), new Vector2(804f, 24f));
            back.pivot = new Vector2(0f, 0.5f);
            UiFactory.Fill(back, new Color(0f, 0f, 0f, 0.45f), UiSprites.Rounded, 12f).raycastTarget = false;
            var fill = UiFactory.Box("Fill", back, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(Mathf.Max(24f, 804f * k), 24f));
            fill.pivot = new Vector2(0f, 0.5f);
            UiFactory.Fill(fill, colour, UiSprites.Rounded, 12f).raycastTarget = false;
            return fill;
        }

        /// <summary>Many rows (level ups, mastery): the list shrinks to stay above CONTINUE.</summary>
        private void Fit() => list.localScale = Vector3.one * Mathf.Min(1f, 1050f / Mathf.Max(1f, -y));

        private IEnumerator XpRow(GameManager.DuelRewards r)
        {
            var row = Row(Loc.F("rw.level", r.levelBefore), "+" + r.xp + " XP", new Color(0.65f, 0.75f, 1f));
            var label = row.Find("What").GetComponent<TextMeshProUGUI>();
            var fill = Bar(row, r.xpBefore / (float)DuelLevel.Need(r.levelBefore), new Color(0.55f, 0.65f, 1f));
            int level = r.levelBefore;
            float from = r.xpBefore / (float)DuelLevel.Need(level);
            foreach (var up in r.levelUps)
            {
                yield return Grow(fill, from, 1f);
                level = up.level;
                label.text = Loc.F("rw.level", level);
                AudioManager.PlaySfx(Sfx.Win, 1f, 1.3f);
                StartCoroutine(PopIn(row));
                string gift = "+" + up.gold + " " + Loc.T("ch.gold") + (up.chest ? " · " + DuelChestScreen.TierName(ChestTier.Mid) : "") + (up.title != null ? " · " + Loc.T(up.title) : "") + (up.move != null ? " · " + Loc.T(up.move) : "") + (Emotes.ForLevel(up.level) >= 0 ? " · " + Loc.F("rw.emote", Loc.T("duel.em." + Emotes.ForLevel(up.level))) : "");
                Row(Loc.F("rw.levelUp", level), gift, Palette.UiGold, Sfx.Win);
                from = 0f;
            }
            yield return Grow(fill, from, r.xpAfter / (float)DuelLevel.Need(level));
        }

        private IEnumerator MasteryRow(GameManager.DuelRewards r)
        {
            string name = Loc.T("weapon." + r.weapon.id);
            var row = Row(Loc.F("rw.mastery", name, r.masteryBefore), "+" + (r.masteryPointsAfter - r.masteryPointsBefore), new Color(1f, 0.75f, 0.35f));
            var label = row.Find("What").GetComponent<TextMeshProUGUI>();
            float K(int points, int level) => level >= Mastery.Max ? 1f : (points - (level == 0 ? 0 : Mastery.NextAt(level - 1))) / (float)Mathf.Max(1, Mastery.NextAt(level) - (level == 0 ? 0 : Mastery.NextAt(level - 1)));
            var fill = Bar(row, K(r.masteryPointsBefore, r.masteryBefore), new Color(1f, 0.7f, 0.3f));
            float from = K(r.masteryPointsBefore, r.masteryBefore);
            for (int l = r.masteryBefore + 1; l <= r.masteryAfter; l++)
            {
                yield return Grow(fill, from, 1f);
                label.text = Loc.F("rw.mastery", name, l);
                var g = Mastery.GiftOf(l);
                var parts = new List<string>();
                if (g.gold > 0) parts.Add("+" + g.gold + " " + Loc.T("ch.gold"));
                if (g.cores > 0) parts.Add(Loc.F("login.cores", "+" + g.cores));
                if (l == 7) parts.Add(Loc.T("rw.badge"));
                if (l == 10) parts.Add(Loc.T(Mastery.TitleKey(r.weapon)));
                Row(Loc.F("rw.masteryUp", name, l), string.Join(" · ", parts), new Color(1f, 0.75f, 0.35f), Sfx.Win);
                from = 0f;
            }
            yield return Grow(fill, from, K(r.masteryPointsAfter, r.masteryAfter));
        }

        private static IEnumerator PopIn(Transform t)
        {
            for (float k = 0f; k < 1f && t != null; k += Time.unscaledDeltaTime / 0.25f)
            {
                t.localScale = Vector3.one * (k < 0.6f ? Mathf.Lerp(0.6f, 1.06f, k / 0.6f) : Mathf.Lerp(1.06f, 1f, (k - 0.6f) / 0.4f));
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
        }

        private IEnumerator Grow(RectTransform fill, float a, float b)
        {
            for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / 0.6f)
            {
                fill.sizeDelta = new Vector2(Mathf.Max(24f, 804f * Mathf.Lerp(a, b, t)), 24f);
                yield return null;
            }
            fill.sizeDelta = new Vector2(Mathf.Max(24f, 804f * b), 24f);
        }
    }
}
