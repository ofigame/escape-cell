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
    /// The duel's end (the design's §5): no panel at first — the result drops in big at the top (VICTORY!, DEFEAT,
    /// DRAW, the score under it) while the winner dances; the special bands pop in under it one by one (the day's first
    /// win ×3, a streak, a comeback, the streak breaker); then the rewards panel slides up over only the bottom 55% of
    /// the screen (the two foi stay in view) and its rows open one after another, each its own small celebration: rank
    /// points counting up, the XP bar (a burst on a level up), the weapon's mastery with the points to its next level,
    /// gold and core parts side by side (the coins fly up to the counter), the chest dropping in, the season road, the
    /// quests. A tap makes it all three times as fast, a second tap finishes it at once. PLAY AGAIN can be pressed from
    /// the start (the next rival is looked for meanwhile); REMATCH for a live rival (or a computer rival asking), MENU,
    /// SHARE. A win: gold confetti and a doubled gold for an ad; a draw: calm blue and a word of cheer; a loss: soft grey,
    /// the WHY YOU LOST tip on top, the points lost shown plainly, the losing streak's consolation.
    /// </summary>
    public class DuelRewardsScreen : MonoBehaviour
    {
        private UiScreen screen;
        private RectTransform root, panel, list, bands, coinPill;
        private TextMeshProUGUI title, scoreText, searchText, coinText;
        private Button replay, rematch, menu, share, addRival;
        private string shareText, rivalToAdd;
        private Action<string> addFriend;
        private Action closed, revenged;
        private DuelEndActions actions;
        private bool pendingRevenge, skip;
        private float speed = 1f, y, panelK;
        private int taps, coinsShown;
        private const float PanelShare = 0.55f, ListWidth = 940f;

        /// <summary>The share line and (a live rival) its name for "add as friend", for the next Show.</summary>
        public void SetExtras(string text, string rivalName, Action<string> add)
        {
            shareText = text;
            rivalToAdd = rivalName;
            addFriend = add;
        }

        public bool IsOpen => screen.IsVisible;

        /// <summary>Closed from outside (the duel left another way: the menu, a new duel): no choice made.</summary>
        public void Hide()
        {
            if (!screen.IsVisible) return;
            StopAllCoroutines();
            screen.Hide(true);
        }

        public static DuelRewardsScreen Create(Transform canvasRoot)
        {
            var s = UiScreen.Create("DuelRewards", canvasRoot, out var root);
            var d = root.gameObject.AddComponent<DuelRewardsScreen>();
            d.screen = s;
            d.root = root;
            d.Build(root);
            return d;
        }

        private void Build(RectTransform r)
        {
            // a see-through layer over the whole screen: taps on it speed the rows up (the scene shows through)
            var tapLayer = UiFactory.Fill(UiFactory.Stretch("Tap", r), new Color(0f, 0f, 0f, 0.001f));
            tapLayer.gameObject.AddComponent<IgnoreSafeArea>();
            var tap = tapLayer.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(Tapped);
            // the result at the top, big
            title = UiFactory.OutlinedText("Title", r, new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(1000f, 190f), "", 150f, new Color(0.3f, 0.15f, 0f));
            title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            scoreText = UiFactory.OutlinedText("Score", r, new Vector2(0.5f, 1f), new Vector2(0f, -460f), new Vector2(800f, 100f), "", 80f, new Color(0.1f, 0.08f, 0.2f));
            scoreText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            bands = UiFactory.Box("Bands", r, new Vector2(0.5f, 1f), new Vector2(0f, -560f), new Vector2(1000f, 80f));
            bands.pivot = new Vector2(0.5f, 1f);
            // the gold counter at the top right (the coins fly to it)
            coinPill = UiFactory.Pill("Coins", r, new Vector2(1f, 1f), new Vector2(-30f, -40f), new Vector2(300f, 84f), UiFactory.PillColor);
            var coin = UiFactory.Box("Coin", coinPill, new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(56f, 56f));
            coin.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(coin, Palette.UiGold, UiSprites.Circle).raycastTarget = false;
            coinText = UiFactory.TextBox("Value", coinPill, new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(200f, 70f), "", 40f, Palette.UiText, title: true, align: TextAlignmentOptions.Right);
            coinText.rectTransform.pivot = new Vector2(1f, 0.5f);

            // the rewards panel: the bottom 55% (it slides up)
            panel = UiFactory.Rect("Panel", r, new Vector2(0f, 0f), new Vector2(1f, PanelShare), Vector2.zero, Vector2.zero);
            UiFactory.Fill(panel, new Color(0.05f, 0.08f, 0.2f, 0.94f), UiSprites.Rounded, 1.2f);
            var rim = UiFactory.Rect("Rim", panel, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -6f), Vector2.zero);
            UiFactory.Fill(rim, new Color(1f, 0.8f, 0.35f, 0.9f)).raycastTarget = false;
            list = UiFactory.Box("List", panel, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(ListWidth, 700f));
            list.pivot = new Vector2(0.5f, 1f);
            // the buttons along its bottom: MENU · PLAY AGAIN · SHARE, the REMATCH over them
            replay = UiFactory.MakeButton(panel, Loc.T("rw.replay"), Kind.Primary, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(560f, 150f), () => Act(actions?.replay), 52f);
            ((RectTransform)replay.transform).pivot = new Vector2(0.5f, 0.5f);
            searchText = UiFactory.TextBox("Search", panel, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(700f, 50f), "", 30f, new Color(1f, 1f, 1f, 0.75f));
            searchText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            menu = UiFactory.MakeButton(panel, Loc.T("btn.menu"), Kind.Secondary, new Vector2(0f, 0f), new Vector2(130f, 120f), new Vector2(210f, 120f), () => { Act(actions?.menu ?? closed); }, 36f);
            ((RectTransform)menu.transform).pivot = new Vector2(0.5f, 0.5f);
            share = UiFactory.MakeButton(panel, Loc.T("rw.share"), Kind.Secondary, new Vector2(1f, 0f), new Vector2(-130f, 120f), new Vector2(210f, 120f), () => { if (!string.IsNullOrEmpty(shareText)) Data.Share.Text(shareText); }, 30f);
            ((RectTransform)share.transform).pivot = new Vector2(0.5f, 0.5f);
            rematch = UiFactory.MakeButton(panel, Loc.T("rw.rematch"), Kind.Gold, new Vector2(0.5f, 0f), new Vector2(0f, 270f), new Vector2(480f, 104f), () => Act(pendingRevenge ? revenged : actions?.rematch), 40f);
            ((RectTransform)rematch.transform).pivot = new Vector2(0.5f, 0.5f);
            addRival = UiFactory.MakeButton(panel, Loc.T("rw.addRival"), Kind.Secondary, new Vector2(1f, 0f), new Vector2(-130f, 270f), new Vector2(210f, 90f), () =>
            {
                if (string.IsNullOrEmpty(rivalToAdd)) return;
                addFriend?.Invoke(rivalToAdd);
                addRival.interactable = false;
            }, 24f);
            ((RectTransform)addRival.transform).pivot = new Vector2(0.5f, 0.5f);
        }

        private void Act(Action a)
        {
            screen.Hide();
            StopAllCoroutines();
            (a ?? closed)?.Invoke();
        }

        private void Tapped()
        {
            taps++;
            if (taps == 1) speed = 3f; // first tap: three times as fast
            else skip = true;         // second: all at once
        }

        /// <summary>The rewards with CONTINUE only (the old way: a friend copy's revenge, or the caller's close).</summary>
        public void Show(GameManager.DuelRewards r, bool won, bool draw, Action onClosed, string revengeOn = null, Action onRevenge = null) =>
            Show(r, won, draw, new DuelEndActions(), onClosed, revengeOn, onRevenge);

        public void Show(GameManager.DuelRewards r, bool won, bool draw, DuelEndActions a, Action onClosed, string revengeOn = null, Action onRevenge = null)
        {
            actions = a ?? new DuelEndActions();
            closed = onClosed;
            revenged = onRevenge;
            pendingRevenge = !string.IsNullOrEmpty(revengeOn) && onRevenge != null;
            skip = false;
            speed = 1f;
            taps = 0;
            panelK = 0f;
            for (int i = list.childCount - 1; i >= 0; i--) Destroy(list.GetChild(i).gameObject);
            for (int i = bands.childCount - 1; i >= 0; i--) Destroy(bands.GetChild(i).gameObject);
            y = 0f;
            list.localScale = Vector3.one;
            // the tone by the result: gold, calm blue, soft grey
            title.text = Loc.T(won ? "rw.victory" : draw ? "ch.draw" : "ch.defeat");
            title.color = won ? new Color(1f, 0.82f, 0.25f) : draw ? new Color(0.6f, 0.82f, 1f) : new Color(0.78f, 0.8f, 0.85f);
            scoreText.text = actions.score ?? "";
            panel.anchoredPosition = new Vector2(0f, -2000f);
            replay.gameObject.SetActive(actions.replay != null);
            menu.gameObject.SetActive(true);
            share.gameObject.SetActive(!string.IsNullOrEmpty(shareText));
            rematch.gameObject.SetActive(pendingRevenge || actions.rematch != null);
            if (rematch.gameObject.activeSelf) rematch.GetComponentInChildren<TextMeshProUGUI>().text = Loc.T(pendingRevenge ? "riv.revenge" : actions.botAsks ? "rw.botRematch" : "rw.rematch");
            addRival.gameObject.SetActive(!string.IsNullOrEmpty(rivalToAdd));
            addRival.interactable = true;
            searchText.text = "";
            coinsShown = SaveData.Coins - Mathf.Max(0, r != null ? r.gold : 0);
            coinText.text = coinsShown.ToString("N0");
            Time.timeScale = 1f; // (a slow-motion moment at the duel's end never holds the screen)
            screen.Show();
            transform.SetAsLastSibling();
            StopAllCoroutines();
            StartCoroutine(Safe(Play(r, won, draw)));
            StartCoroutine(Search());
            if (won) StartCoroutine(Confetti());
        }

        /// <summary>The rows' play, a fault in one never leaving the screen without its buttons (they are there from the start).</summary>
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
            panel.anchoredPosition = Vector2.zero;
        }

        /// <summary>The background search's word under PLAY AGAIN: looking for a rival … ready.</summary>
        private IEnumerator Search()
        {
            while (true)
            {
                if (actions.searchState != null && actions.replay != null)
                    searchText.text = Loc.T(actions.searchState() >= 1 ? "rw.rivalReady" : "rw.searching");
                yield return new WaitForSecondsRealtime(0.25f);
            }
        }

        private IEnumerator Wait(float s)
        {
            for (float t = 0f; t < s && !skip; t += Time.unscaledDeltaTime * speed) yield return null;
        }

        private IEnumerator Play(GameManager.DuelRewards r, bool won, bool draw)
        {
            // 1. the result's moment: the title drops in, bounces and settles (no panel yet)
            yield return Drop(title.rectTransform, -330f);
            if (!string.IsNullOrEmpty(scoreText.text)) StartCoroutine(PopIn(scoreText.transform));
            if (won) AudioManager.PlaySfx(Sfx.Win, 1f, 1f);
            yield return Wait(0.9f);
            // 2. the special bands, one by one
            if (r != null)
            {
                float bx = 0f;
                var list2 = new List<(string text, Color c)>();
                if (r.firstWin) list2.Add((Loc.T("rw.firstWinBand"), Palette.UiGold));
                if (r.streak >= 2) list2.Add((Loc.F("rw.streakBand", r.streak), new Color(1f, 0.55f, 0.25f)));
                if (r.comeback) list2.Add((Loc.T("rw.comebackBand"), new Color(0.5f, 1f, 0.6f)));
                if (r.goldenKo) list2.Add((Loc.T("rw.goldenKoBand"), Palette.UiGold));
                if (r.streakBreaker) list2.Add((Loc.T("rw.breakerBand"), new Color(1f, 0.4f, 0.35f)));
                float total = list2.Count * 250f;
                foreach (var (text, c) in list2)
                {
                    Band(text, c, -total * 0.5f + 125f + bx);
                    bx += 250f;
                    AudioManager.PlaySfx(Sfx.Coin, 0.7f, 1.3f);
                    yield return Wait(0.15f);
                }
            }
            yield return Wait(0.35f);
            // 3. the panel slides up
            for (panelK = 0f; panelK < 1f && !skip; panelK += Time.unscaledDeltaTime * speed / 0.35f)
            {
                float k = 1f - Mathf.Pow(1f - panelK, 3f);
                panel.anchoredPosition = new Vector2(0f, Mathf.Lerp(-PanelHeight, 0f, k));
                yield return null;
            }
            panel.anchoredPosition = Vector2.zero;
            if (r == null) yield break;
            // the loss's tip on top (and the draw's word of cheer)
            if (!won && !draw && !string.IsNullOrEmpty(actions.tip)) TipRow(actions.tip, actions.tipHow);
            else if (draw) { Row(Loc.T("rw.drawCheer"), "", new Color(0.6f, 0.82f, 1f), Sfx.Click); yield return Wait(0.2f); }
            // rank points: counting up (a loss: shown plainly)
            if (r.trophiesKnown) yield return RankRow(r.trophies, won || draw);
            yield return Wait(0.2f);
            yield return XpRow(r);
            yield return Wait(0.2f);
            if (r.weapon != null) { yield return MasteryRow(r); yield return Wait(0.2f); }
            if (r.gold > 0 || r.cores > 0) { yield return GoldCores(r, won); yield return Wait(0.25f); }
            if (r.chest.HasValue) { yield return ChestRow(r.chest.Value); yield return Wait(0.25f); }
            yield return SeasonRow(r);
            yield return Wait(0.2f);
            QuestRow(r);
            if (!string.IsNullOrEmpty(r.eventLine)) { yield return Wait(0.2f); Row(r.eventLine, "", new Color(1f, 0.75f, 0.35f), Sfx.Win); }
            if (r.consolation) { yield return Wait(0.2f); Row(Loc.T("rw.consolation"), "", new Color(0.75f, 0.85f, 1f), Sfx.Click); }
        }

        private float PanelHeight => ((RectTransform)root).rect.height * PanelShare + 40f;

        // ---------- The rows ----------

        private RectTransform NewRow(float height)
        {
            var row = UiFactory.Box("Row", list, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(ListWidth, height));
            row.pivot = new Vector2(0.5f, 1f);
            y -= height + 10f;
            Fit();
            UiFactory.Framed(row, Flat.Card, 1.8f).raycastTarget = false;
            StartCoroutine(PopIn(row));
            return row;
        }

        /// <summary>A reward's row: what it is on the left, how much on the right (popping in, with its sound).</summary>
        private RectTransform Row(string what, string value, Color colour, Sfx? sfx = Sfx.Click, float height = 76f)
        {
            var row = NewRow(height);
            float mid = height > 80f ? 30f : 0f; // (a row with a bar: the words a little over its middle, the bar under them)
            var l = UiFactory.TextBox("What", row, new Vector2(0f, 0.5f), new Vector2(26f, mid), new Vector2(value.Length > 0 ? 620f : 880f, Mathf.Min(64f, height - 8f)), what, 30f, Palette.UiText, FontStyles.Normal, align: TextAlignmentOptions.Left);
            l.rectTransform.pivot = new Vector2(0f, 0.5f);
            l.textWrappingMode = TextWrappingModes.Normal;
            l.enableAutoSizing = true; l.fontSizeMin = 18f; l.fontSizeMax = 30f;
            if (value.Length > 0)
            {
                var v = UiFactory.TextBox("Value", row, new Vector2(1f, 0.5f), new Vector2(-26f, mid), new Vector2(280f, Mathf.Min(64f, height - 8f)), value, 42f, colour, title: true, align: TextAlignmentOptions.Right);
                v.rectTransform.pivot = new Vector2(1f, 0.5f);
                v.enableAutoSizing = true; v.fontSizeMin = 20f; v.fontSizeMax = 42f;
            }
            else l.color = colour;
            if (sfx.HasValue) AudioManager.PlaySfx(sfx.Value, 0.8f, 1.15f);
            return row;
        }

        /// <summary>A bar along a row's bottom (0..1), returning its fill to grow; a small note under the label.</summary>
        private RectTransform Bar(RectTransform row, float k, Color colour, string note = null)
        {
            var back = UiFactory.Box("Bar", row, new Vector2(0f, 0f), new Vector2(26f, 14f), new Vector2(ListWidth - 52f, 18f));
            back.pivot = new Vector2(0f, 0.5f);
            UiFactory.Fill(back, new Color(0f, 0f, 0f, 0.45f), UiSprites.Rounded, 10f).raycastTarget = false;
            var fill = UiFactory.Box("Fill", back, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(Mathf.Max(20f, (ListWidth - 52f) * Mathf.Clamp01(k)), 20f));
            fill.pivot = new Vector2(0f, 0.5f);
            UiFactory.Fill(fill, colour, UiSprites.Rounded, 10f).raycastTarget = false;
            if (note != null)
            {
                var n = UiFactory.TextBox("Note", row, new Vector2(0f, 0f), new Vector2(26f, 44f), new Vector2(700f, 32f), note, 22f, new Color(1f, 1f, 1f, 0.7f), FontStyles.Normal, align: TextAlignmentOptions.Left);
                n.rectTransform.pivot = new Vector2(0f, 0.5f);
                n.enableAutoSizing = true; n.fontSizeMin = 16f; n.fontSizeMax = 24f;
            }
            return fill;
        }

        /// <summary>Many rows: the list shrinks to stay above the buttons.</summary>
        private void Fit()
        {
            float room = Mathf.Max(200f, PanelHeight - 40f - 360f);
            list.localScale = Vector3.one * Mathf.Min(1f, room / Mathf.Max(1f, -y));
        }

        private void TipRow(string tip, Action how)
        {
            var row = NewRow(124f);
            UiFactory.Fill(UiFactory.Stretch("Glow", row), new Color(1f, 0.75f, 0.3f, 0.12f)).raycastTarget = false;
            var head = UiFactory.TextBox("Head", row, new Vector2(0f, 1f), new Vector2(26f, -22f), new Vector2(640f, 36f), Loc.T("tip.title"), 26f, Palette.UiGold, title: true, align: TextAlignmentOptions.Left);
            head.rectTransform.pivot = new Vector2(0f, 0.5f);
            var t = UiFactory.TextBox("Tip", row, new Vector2(0f, 1f), new Vector2(26f, -78f), new Vector2(how != null ? 700f : 880f, 76f), tip, 26f, Palette.UiText, FontStyles.Normal, align: TextAlignmentOptions.Left);
            t.rectTransform.pivot = new Vector2(0f, 0.5f);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.enableAutoSizing = true; t.fontSizeMin = 16f; t.fontSizeMax = 26f;
            if (how != null)
            {
                var b = UiFactory.MakeButton(row, Loc.T("tip.how"), Kind.Gold, new Vector2(1f, 0.5f), new Vector2(-90f, 0f), new Vector2(160f, 80f), () => Act(how), 28f);
                ((RectTransform)b.transform).pivot = new Vector2(0.5f, 0.5f);
            }
            AudioManager.PlaySfx(Sfx.Click, 0.8f, 0.9f);
        }

        /// <summary>Rank points: counting up with ticks (a loss: written at once, small and calm), the points to the next rank.</summary>
        private IEnumerator RankRow(int change, bool countUp)
        {
            int trophies = Challenge.Trophies, rank = Challenge.Rank;
            string note = rank + 1 < Challenge.RankAt.Length ? Loc.F("rw.toRank", Challenge.RankName(rank + 1), Challenge.RankAt[rank + 1] - trophies) : null;
            var row = Row(Loc.T("rw.trophies"), (change >= 0 ? "+" : "") + (countUp ? 0 : change), change >= 0 ? new Color(0.5f, 1f, 0.6f) : new Color(0.85f, 0.7f, 0.7f), Sfx.Click, 126f);
            int from = rank > 0 ? Challenge.RankAt[rank] : 0, to = rank + 1 < Challenge.RankAt.Length ? Challenge.RankAt[rank + 1] : from + 1;
            Bar(row, (trophies - from) / (float)Mathf.Max(1, to - from), new Color(0.5f, 0.9f, 0.6f), note);
            if (!countUp || change <= 0) yield break;
            var v = row.Find("Value").GetComponentInChildren<TextMeshProUGUI>();
            int shown = 0;
            for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime * speed / 0.5f)
            {
                int now = Mathf.RoundToInt(change * t);
                if (now != shown) { shown = now; v.text = "+" + now; AudioManager.PlaySfx(Sfx.Click, 0.35f, 1.6f); }
                yield return null;
            }
            v.text = "+" + change;
        }

        private IEnumerator XpRow(GameManager.DuelRewards r)
        {
            var row = Row(Loc.F("rw.level", r.levelBefore), "+" + r.xp + " XP", new Color(0.65f, 0.75f, 1f), Sfx.Click, 126f);
            var label = row.Find("What").GetComponentInChildren<TextMeshProUGUI>();
            var fill = Bar(row, r.xpBefore / (float)DuelLevel.Need(r.levelBefore), new Color(0.55f, 0.65f, 1f));
            int level = r.levelBefore;
            float from = r.xpBefore / (float)DuelLevel.Need(level);
            foreach (var up in r.levelUps)
            {
                yield return Grow(fill, from, 1f);
                if (label == null) yield break; // (the screen shown again meanwhile)
                level = up.level;
                label.text = Loc.F("rw.level", level);
                AudioManager.PlaySfx(Sfx.Win, 1f, 1.3f);
                StartCoroutine(PopIn(row));
                string gift = "+" + up.gold + " " + Loc.T("ch.gold") + (up.chest ? " · " + DuelChestScreen.TierName(ChestTier.Mid) : "") + (up.title != null ? " · " + Loc.T(up.title) : "") + (up.move != null ? " · " + Loc.T(up.move) : "") + (Emotes.ForLevel(up.level) >= 0 ? " · " + Loc.F("rw.emote", Loc.T("duel.em." + Emotes.ForLevel(up.level))) : "") + (FaceTaunts.ForLevel(up.level) >= 0 ? " · " + Loc.F("rw.emote", Loc.T("taunt." + FaceTaunts.All[FaceTaunts.ForLevel(up.level)])) : "");
                Row(Loc.F("rw.levelUp", level), gift, Palette.UiGold, Sfx.Win);
                from = 0f;
            }
            yield return Grow(fill, from, r.xpAfter / (float)DuelLevel.Need(level));
        }

        /// <summary>The mastery's bar fills by the points between its last level and its next (the points to it in words).</summary>
        private IEnumerator MasteryRow(GameManager.DuelRewards r)
        {
            string name = Loc.T("weapon." + r.weapon.id);
            int Prev(int level) => level == 0 ? 0 : Mastery.NextAt(level - 1);
            float K(int points, int level) => level >= Mastery.Max ? 1f : (points - Prev(level)) / (float)Mathf.Max(1, Mastery.NextAt(level) - Prev(level));
            string note = r.masteryAfter >= Mastery.Max ? null : Loc.F("rw.toMastery", r.masteryAfter + 1, Mastery.NextAt(r.masteryAfter) - r.masteryPointsAfter);
            var row = Row(Loc.F("rw.mastery", name, r.masteryBefore), "+" + (r.masteryPointsAfter - r.masteryPointsBefore), new Color(1f, 0.75f, 0.35f), Sfx.Click, 126f);
            var label = row.Find("What").GetComponentInChildren<TextMeshProUGUI>();
            var fill = Bar(row, K(r.masteryPointsBefore, r.masteryBefore), new Color(1f, 0.7f, 0.3f), note);
            float from = K(r.masteryPointsBefore, r.masteryBefore);
            for (int l = r.masteryBefore + 1; l <= r.masteryAfter; l++)
            {
                yield return Grow(fill, from, 1f);
                if (label == null) yield break;
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

        /// <summary>Gold and core parts in two boxes side by side, counting up; the coins fly to the counter at the top.
        /// A win: "▶ 2×" on the gold (an ad doubles it, after a card that says so, with a NO THANKS).</summary>
        private IEnumerator GoldCores(GameManager.DuelRewards r, bool won)
        {
            var row = NewRow(120f);
            var gold = Box(row, 0, Loc.T("rw.gold"), Palette.UiGold, out var goldValue);
            var cores = Box(row, 1, Loc.T("rw.cores"), new Color(0.45f, 0.92f, 1f), out var coreValue);
            AudioManager.PlaySfx(Sfx.Coin, 0.8f, 1.1f);
            if (won && actions.doubleGold != null && r.gold > 0)
            {
                Button dbl = null;
                dbl = UiFactory.MakeButton(gold, "▶ 2×", Kind.Gold, new Vector2(1f, 0.5f), new Vector2(-70f, 0f), new Vector2(120f, 70f), () =>
                {
                    dbl.interactable = false;
                    actions.doubleGold(ok =>
                    {
                        if (!ok) { dbl.interactable = true; return; }
                        r.gold *= 2;
                        goldValue.text = "+" + r.gold;
                        dbl.gameObject.SetActive(false);
                        StartCoroutine(FlyCoins(gold, r.gold / 2));
                    });
                }, 30f);
                ((RectTransform)dbl.transform).pivot = new Vector2(0.5f, 0.5f);
            }
            for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime * speed / 0.5f)
            {
                goldValue.text = "+" + Mathf.RoundToInt(r.gold * t);
                coreValue.text = "+" + Mathf.RoundToInt(r.cores * t);
                yield return null;
            }
            goldValue.text = "+" + r.gold;
            coreValue.text = "+" + r.cores;
            if (r.gold > 0) StartCoroutine(FlyCoins(gold, r.gold));
        }

        private RectTransform Box(RectTransform row, int side, string label, Color c, out TextMeshProUGUI value)
        {
            var box = UiFactory.Box(side == 0 ? "Gold" : "Cores", row, new Vector2(side == 0 ? 0.25f : 0.75f, 0.5f), Vector2.zero, new Vector2(ListWidth * 0.5f - 20f, 108f));
            box.pivot = new Vector2(0.5f, 0.5f);
            var l = UiFactory.TextBox("Label", box, new Vector2(0f, 1f), new Vector2(24f, -28f), new Vector2(300f, 40f), label, 26f, new Color(1f, 1f, 1f, 0.8f), FontStyles.Normal, align: TextAlignmentOptions.Left);
            l.rectTransform.pivot = new Vector2(0f, 0.5f);
            value = UiFactory.TextBox("Value", box, new Vector2(0f, 0f), new Vector2(24f, 36f), new Vector2(260f, 60f), "+0", 50f, c, title: true, align: TextAlignmentOptions.Left);
            value.rectTransform.pivot = new Vector2(0f, 0.5f);
            return box;
        }

        /// <summary>8–12 coins fly from <paramref name="from"/> to the gold counter; the counter bumps at each and counts up.</summary>
        private IEnumerator FlyCoins(RectTransform from, int amount)
        {
            int n = Mathf.Clamp(amount / 40, 8, 12);
            int target = SaveData.Coins;
            int start = coinsShown;
            for (int i = 0; i < n; i++)
            {
                StartCoroutine(OneCoin(from, i, n, start + (target - start) * (i + 1) / n));
                yield return new WaitForSecondsRealtime(0.05f / speed);
            }
        }

        private IEnumerator OneCoin(RectTransform from, int i, int n, int counterAfter)
        {
            var c = UiFactory.Box("FlyCoin", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 46f));
            c.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(c, Palette.UiGold, UiSprites.Circle).raycastTarget = false;
            var a = (Vector2)root.InverseTransformPoint(from.position) + UnityEngine.Random.insideUnitCircle * 40f;
            var b = (Vector2)root.InverseTransformPoint(coinPill.position) + new Vector2(-100f, 0f);
            var mid = (a + b) * 0.5f + new Vector2((i - n * 0.5f) * 30f, 160f);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * Mathf.Max(1f, speed) / 0.55f)
            {
                var p = Vector2.Lerp(Vector2.Lerp(a, mid, t), Vector2.Lerp(mid, b, t), t);
                c.anchoredPosition = p;
                yield return null;
            }
            Destroy(c.gameObject);
            coinsShown = counterAfter;
            coinText.text = coinsShown.ToString("N0");
            coinPill.localScale = Vector3.one * 1.12f;
            StartCoroutine(Settle(coinPill));
            AudioManager.PlaySfx(Sfx.Coin, 0.4f, 1.4f + 0.04f * i);
        }

        private static IEnumerator Settle(Transform t)
        {
            for (float k = 0f; k < 1f && t != null; k += Time.unscaledDeltaTime / 0.15f) { t.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, k); yield return null; }
            if (t != null) t.localScale = Vector3.one;
        }

        /// <summary>The chest drops in from above and shakes twice; it waits among the chests.</summary>
        private IEnumerator ChestRow(ChestTier tier)
        {
            var row = Row(Loc.F("rw.chestTo", DuelChestScreen.TierName(tier)), "", ChestModels.StrapColour(tier), null, 96f);
            var chest = UiFactory.Box("Chest", row, new Vector2(1f, 0.5f), new Vector2(-90f, 0f), new Vector2(90f, 70f));
            chest.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(chest, ChestModels.StrapColour(tier), UiSprites.Rounded, 4f).raycastTarget = false;
            var lid = UiFactory.Box("Lid", chest, new Vector2(0.5f, 1f), new Vector2(0f, 6f), new Vector2(96f, 26f));
            UiFactory.Fill(lid, ChestModels.StrapColour(tier) * 0.8f, UiSprites.Rounded, 4f).raycastTarget = false;
            for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime * speed / 0.3f)
            {
                chest.anchoredPosition = new Vector2(-90f, Mathf.Lerp(260f, 0f, t * t));
                yield return null;
            }
            chest.anchoredPosition = new Vector2(-90f, 0f);
            AudioManager.PlaySfx(Sfx.Bump, 1f, 0.6f);
            for (int s = 0; s < 2 && !skip; s++)
                for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime * speed / 0.18f)
                {
                    chest.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(t * Mathf.PI * 2f) * 12f);
                    yield return null;
                }
            chest.localEulerAngles = Vector3.zero;
        }

        private IEnumerator SeasonRow(GameManager.DuelRewards r)
        {
            int per = SeasonRoad.PerStep;
            int before = r.seasonBefore, after = Mathf.Min(SeasonRoad.Steps * per, before + r.seasonPoints);
            var row = Row(Loc.F("rw.seasonStep", Mathf.Min(SeasonRoad.Steps, after / per + 1)), "+" + r.seasonPoints, new Color(1f, 0.7f, 0.3f), Sfx.Click, 126f);
            var fill = Bar(row, (before % per) / (float)per, new Color(1f, 0.6f, 0.25f));
            bool stepped = after / per > before / per;
            if (stepped) { yield return Grow(fill, (before % per) / (float)per, 1f); AudioManager.PlaySfx(Sfx.Win, 0.8f, 1.5f); StartCoroutine(PopIn(row)); yield return Grow(fill, 0f, (after % per) / (float)per); }
            else yield return Grow(fill, (before % per) / (float)per, (after % per) / (float)per);
        }

        /// <summary>One line for the quests: the day's first one still open and the week's (glowing when one is done).</summary>
        private void QuestRow(GameManager.DuelRewards r)
        {
            ChallengeQuests.Quest? day = null, week = null;
            bool anyDone = false;
            for (int i = 0; i < ChallengeQuests.Count; i++)
            {
                var d = ChallengeQuests.Get(false, i);
                if (d.Done && !d.claimed) anyDone = true;
                if (day == null && !d.claimed) day = d;
                var w = ChallengeQuests.Get(true, i);
                if (w.Done && !w.claimed) anyDone = true;
                if (week == null && !w.claimed) week = w;
            }
            if (day == null && week == null) return;
            string text = (day.HasValue ? Loc.F("rw.questDay", ChallengeQuests.Text(day.Value), day.Value.progress, day.Value.target) : "")
                          + (day.HasValue && week.HasValue ? "  ·  " : "")
                          + (week.HasValue ? Loc.F("rw.questWeek", week.Value.progress, week.Value.target) : "");
            var row = Row(text, "", anyDone ? Palette.UiGold : new Color(1f, 1f, 1f, 0.85f), anyDone ? Sfx.Win : Sfx.Click);
            if (anyDone) UiFactory.Fill(UiFactory.Stretch("Glow", row), new Color(1f, 0.8f, 0.3f, 0.15f)).raycastTarget = false;
        }

        private void Band(string text, Color c, float x)
        {
            var b = UiFactory.Box("Band", bands, new Vector2(0.5f, 1f), new Vector2(x, 0f), new Vector2(236f, 70f));
            b.pivot = new Vector2(0.5f, 1f);
            UiFactory.Fill(b, new Color(c.r * 0.35f, c.g * 0.3f, c.b * 0.3f, 0.92f), UiSprites.Rounded, 2.4f).raycastTarget = false;
            var t = UiFactory.TextBox("Text", b, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 60f), text, 26f, c, title: true);
            t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            t.enableAutoSizing = true; t.fontSizeMin = 14f; t.fontSizeMax = 26f;
            StartCoroutine(PopIn(b));
        }

        // ---------- Motion ----------

        /// <summary>The title drops from above, overshoots and bounces into place.</summary>
        private IEnumerator Drop(RectTransform t, float at)
        {
            for (float k = 0f; k < 1f && !skip; k += Time.unscaledDeltaTime * speed / 0.45f)
            {
                float e = k < 0.7f ? Mathf.Lerp(420f, -40f, k / 0.7f) : Mathf.Lerp(-40f, 0f, (k - 0.7f) / 0.3f);
                t.anchoredPosition = new Vector2(0f, at + e);
                t.localScale = Vector3.one * (k < 0.7f ? 1f : 1f + 0.08f * Mathf.Sin((k - 0.7f) / 0.3f * Mathf.PI));
                yield return null;
            }
            t.anchoredPosition = new Vector2(0f, at);
            t.localScale = Vector3.one;
            AudioManager.PlaySfx(Sfx.Bump, 0.9f, 0.8f);
        }

        /// <summary>Gold confetti falling over the scene for a moment (a win).</summary>
        private IEnumerator Confetti()
        {
            var pieces = new List<(RectTransform r, Vector2 v, float spin)>();
            var size = ((RectTransform)root).rect.size;
            Color[] colours = { Palette.UiGold, new Color(1f, 0.45f, 0.4f), new Color(0.45f, 0.8f, 1f), new Color(0.55f, 1f, 0.55f), Color.white };
            for (int i = 0; i < 46; i++)
            {
                var p = UiFactory.Box("Confetti", root, new Vector2(0.5f, 1f), new Vector2(UnityEngine.Random.Range(-size.x * 0.5f, size.x * 0.5f), UnityEngine.Random.Range(0f, 300f)), new Vector2(18f, 30f));
                p.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(p, colours[i % colours.Length]).raycastTarget = false;
                pieces.Add((p, new Vector2(UnityEngine.Random.Range(-60f, 60f), -UnityEngine.Random.Range(280f, 520f)), UnityEngine.Random.Range(-400f, 400f)));
            }
            for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime)
            {
                foreach (var (r, v, spin) in pieces)
                {
                    if (r == null) continue;
                    r.anchoredPosition += v * Time.unscaledDeltaTime;
                    r.localEulerAngles += new Vector3(0f, 0f, spin * Time.unscaledDeltaTime);
                    r.localScale = new Vector3(Mathf.Cos(t * 6f + v.x), 1f, 1f);
                }
                yield return null;
            }
            foreach (var (r, _, _) in pieces) if (r != null) Destroy(r.gameObject);
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
            float w = ListWidth - 52f;
            for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime * speed / 0.5f)
            {
                if (fill == null) yield break;
                fill.sizeDelta = new Vector2(Mathf.Max(18f, w * Mathf.Lerp(a, b, t)), 18f);
                yield return null;
            }
            if (fill == null) yield break;
            fill.sizeDelta = new Vector2(Mathf.Max(18f, w * b), 18f);
        }
    }
}
