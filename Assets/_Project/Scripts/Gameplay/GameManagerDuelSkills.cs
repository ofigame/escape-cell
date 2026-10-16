using System.Collections;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.UI;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The duel's fighting moves, Mortal Kombat style. A power bar in three parts fills with every blow landed (and a
    /// little with every blow taken). Blows landed one after another make a combo: three open a power hit, five a
    /// knockdown, for a moment. One part of the bar throws foi's special (each build its own: foi a fireball, Volt a
    /// lightning orb — fast — Kaya a boulder — slow and heavy — Zip a blade wave), dodged by stepping out of its way;
    /// one part breaks the rival's combo when caught in it; the whole bar unleashes the ultimate (slow motion, the
    /// camera close, a flurry and a blast). After a knock-down: the finish (a show of sparks and bolts). The AI rival
    /// uses them all too; in a live match the server checks every move and keeps both bars.
    /// </summary>
    public partial class GameManager
    {
        private DuelSkillBar skillBar;
        private float myMeter, rivalMeter;
        private int myCombo, rivalCombo, myComboMove;
        private float myComboAt, rivalComboAt, comboMoveUntil, finishUntil, rivalThinkAt;
        private Vector3 lastFoeAt;
        private Vector3 finishAt;
        private bool specialStrike, ultimateOn, finishing;
        private int rivalHero;

        private const float MeterGain = 0.22f, MeterHurt = 0.12f, ComboGap = 1.4f, ComboMoveTime = 2.6f;
        /// <summary>What each move takes off (times one of the mover's own blows).</summary>
        private const float SpecialTimes = 1.8f, HeavyHit = 1.8f, UpperHit = 2.5f, UltimateHit = 6f; // (as the server: Rules)

        private class DuelShot
        {
            public Transform view;
            public Vector3 pos, dir;
            public float speed, radius, travelled;
            public bool mine;
            public int kind;
        }

        private readonly List<DuelShot> duelShots = new List<DuelShot>();
        private readonly List<Material> shotMats = new List<Material>();

        private static readonly Color[] SpecialColour = { new Color(1f, 0.5f, 0.12f), new Color(0.45f, 0.85f, 1f), new Color(0.55f, 0.4f, 0.28f), new Color(0.75f, 0.4f, 1f) };
        private static float SpecialSpeed(int kind) => kind switch { 1 => 18f, 2 => 7f, 3 => 14f, _ => 11f };
        private static float SpecialRadius(int kind) => kind switch { 1 => 0.6f, 2 => 1.05f, 3 => 0.95f, _ => 0.75f };

        // ---------- Start and end ----------

        private bool duelSkillsWired;

        private void BeginDuelSkills()
        {
            if (!duelSkillsWired)
            {
                duelSkillsWired = true;
                hunt.ChallengeFoeStruck += OnFoeStruck;
                hunt.FoiStruckByFoe += OnFoiStruck;
            }
            myMeter = rivalMeter = 0f;
            ResetCombos();
            finishUntil = 0f;
            rivalHero = Mathf.Abs((rival.name ?? "").GetHashCode()) % 4;
            if (skillBar != null) Destroy(skillBar.gameObject);
            skillBar = DuelSkillBar.Create(chUi.BattleHud, Loc.T("duel.sp." + Mathf.Clamp(SaveData.Hero, 0, 3)));
            skillBar.Pressed += OnSkill;
            skillBar.SetMeters(0f, 0f);
        }

        private void ResetCombos()
        {
            myCombo = rivalCombo = myComboMove = 0;
            comboMoveUntil = 0f;
        }

        private void EndDuelSkills()
        {
            if (skillBar != null) Destroy(skillBar.gameObject);
            skillBar = null;
            foreach (var s in duelShots) if (s.view != null) Destroy(s.view.gameObject);
            duelShots.Clear();
            foreach (var m in shotMats) if (m != null) Destroy(m);
            shotMats.Clear();
            if (ultimateOn) { ultimateOn = false; Time.timeScale = 1f; }
        }

        // ---------- Blows landing ----------

        /// <summary>foi's blow landed on the rival: the bar fills, the combo grows (three: a power hit; five: a knockdown).</summary>
        private void OnFoeStruck()
        {
            if (specialStrike || !challengeRun || chPhase != ChPhase.Duel) return;
            myMeter = Mathf.Min(3f, myMeter + MeterGain);
            if (!LiveActive) rivalMeter = Mathf.Min(3f, rivalMeter + MeterHurt);
            myCombo = Time.time - myComboAt < ComboGap ? myCombo + 1 : 1;
            myComboAt = Time.time;
            rivalCombo = 0;
            if (skillBar != null) skillBar.ShowCombo(myCombo);
            if (myCombo == 3 || myCombo == 5) { myComboMove = myCombo; comboMoveUntil = Time.time + ComboMoveTime; }
            if (colosseum != null && myCombo >= 3) colosseum.Cheer(0, 0.4f + myCombo * 0.08f);
        }

        /// <summary>The rival's blow landed on foi: its bar and combo grow, foi's combo is broken.</summary>
        private void OnFoiStruck()
        {
            if (!challengeRun || chPhase != ChPhase.Duel) return;
            myMeter = Mathf.Min(3f, myMeter + MeterHurt);
            if (!LiveActive) rivalMeter = Mathf.Min(3f, rivalMeter + MeterGain);
            rivalCombo = Time.time - rivalComboAt < ComboGap ? rivalCombo + 1 : 1;
            rivalComboAt = Time.time;
            myCombo = 0;
            myComboMove = 0;
            if (colosseum != null && rivalCombo >= 3) colosseum.Cheer(1, 0.4f + rivalCombo * 0.08f);
        }

        // ---------- Each frame ----------

        private void UpdateDuelSkills(float dt)
        {
            if (skillBar == null) return;
            if (hunt.ChallengeFoeAt is Vector3 seen) lastFoeAt = seen;
            if (myCombo > 0 && Time.time - myComboAt > ComboGap) myCombo = 0;
            if (rivalCombo > 0 && Time.time - rivalComboAt > ComboGap) rivalCombo = 0;
            if (myComboMove > 0 && Time.time > comboMoveUntil) myComboMove = 0;
            DuelSkillBar.Move? ctx = null;
            if (Time.time < finishUntil) ctx = DuelSkillBar.Move.Finish;
            else if (chPhase == ChPhase.Duel)
            {
                if (rivalCombo >= 2 && myMeter >= 1f) ctx = DuelSkillBar.Move.Breaker;
                else if (myComboMove == 5) ctx = DuelSkillBar.Move.Upper;
                else if (myComboMove == 3) ctx = DuelSkillBar.Move.Heavy;
            }
            skillBar.SetContext(ctx);
            skillBar.SetMeters(myMeter, rivalMeter);
            UpdateShots(dt);
            if (!LiveActive && chPhase == ChPhase.Duel && !ultimateOn) RivalMoves();
        }

        // ---------- The player's moves ----------

        private void OnSkill(DuelSkillBar.Move move)
        {
            if (!challengeRun) return;
            if (move == DuelSkillBar.Move.Finish) { DoFinish(); return; }
            if (chPhase != ChPhase.Duel || ultimateOn || !hunt.ChallengeFoeTile.HasValue) return;
            var foeTile = hunt.ChallengeFoeTile.Value;
            var foeAt = hunt.ChallengeFoeAt ?? robot.transform.position;
            float dist = Flat(foeAt - robot.transform.position).magnitude;
            switch (move)
            {
                case DuelSkillBar.Move.Special:
                    if (myMeter < 1f) return;
                    myMeter -= 1f;
                    FireShot(true, Mathf.Clamp(SaveData.Hero, 0, 3), robot.transform.position, foeAt);
                    if (LiveActive) LiveNet.Special(1);
                    break;
                case DuelSkillBar.Move.Heavy:
                case DuelSkillBar.Move.Upper:
                {
                    bool upper = move == DuelSkillBar.Move.Upper;
                    if (myComboMove != (upper ? 5 : 3) || HuntSystem.Chebyshev(robot.Position, foeTile) > StrikeReach + 1) return;
                    myComboMove = 0;
                    myCombo = 0;
                    robot.Strike(foeAt);
                    SlashFx.Create(robot.transform.position, foeAt, upper ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.7f, 0.25f), upper ? 2.4f : 1.8f);
                    Shockwave.Create(foeAt, upper ? 2.6f : 1.8f, upper ? new Color(1f, 0.35f, 0.25f) : new Color(1f, 0.75f, 0.35f));
                    cameraRig.Shake(upper ? 0.7f : 0.45f);
                    AudioManager.PlaySfx(Sfx.Impact, 1f, upper ? 0.7f : 0.85f);
                    skillBar.Shout(Loc.T(upper ? "duel.upper" : "duel.heavy") + "!", new Color(1f, 0.6f, 0.25f));
                    if (LiveActive) LiveNet.Special(upper ? 3 : 2);
                    else
                    {
                        HitFoe(upper ? UpperHit : HeavyHit);
                        hunt.StunFoe(upper ? 1.4f : 0.8f);
                    }
                    if (colosseum != null) colosseum.Cheer(0, 0.9f);
                    break;
                }
                case DuelSkillBar.Move.Breaker:
                    if (myMeter < 1f || rivalCombo < 2) return;
                    myMeter -= 1f;
                    rivalCombo = 0;
                    Shockwave.Create(robot.transform.position, 2.4f, new Color(0.4f, 0.8f, 1f));
                    fx.Burst(robot.transform.position + Vector3.up * 0.6f, new Color(0.5f, 0.85f, 1f), new Color(1f, 2f, 3f), 30, 6f);
                    AudioManager.PlaySfx(Sfx.Shield, 1f, 0.8f);
                    skillBar.Shout(Loc.T("duel.broken"), new Color(0.5f, 0.85f, 1f));
                    if (LiveActive) LiveNet.Special(5);
                    else hunt.StunFoe(1f);
                    break;
                case DuelSkillBar.Move.Ultimate:
                    if (myMeter < 3f || dist > 4.5f) { if (myMeter >= 3f) chUi.SetStatus(Loc.T("duel.ultNear")); return; }
                    myMeter = 0f;
                    if (LiveActive) LiveNet.Special(4);
                    cameraRig.StartCoroutine(Ultimate(true));
                    break;
            }
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>A move's damage on the AI rival: through its shield and health like a blow, but no combo or bar for it.</summary>
        private void HitFoe(float times)
        {
            if (!hunt.ChallengeFoeTile.HasValue) return;
            var w = weapon ?? Armory.Equipped;
            int damage = Mathf.RoundToInt(Challenge.RankBoost(Armory.Damage(w)) * times);
            specialStrike = true;
            hunt.Strike(hunt.ChallengeFoeTile.Value, Mathf.Max(1, damage));
            specialStrike = false;
        }

        /// <summary>A move's damage on foi from the AI rival: its own blow times this, through foi's shield like a blow.</summary>
        private void HurtMe(float times)
        {
            float share = hunt.ChallengeFoeShare * times;
            if (share <= 0f) return;
            hitShareOverride = share;
            OnBlockImpact(robot.Position);
            hitShareOverride = -1f;
        }

        // ---------- The specials in flight ----------

        private void FireShot(bool mine, int kind, Vector3 from, Vector3 to)
        {
            var dir = Flat(to - from);
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();
            var colour = SpecialColour[kind];
            var mat = MaterialFactory.Create(colour, colour * 2.2f);
            shotMats.Add(mat);
            var view = new GameObject("Special").transform;
            view.position = from + Vector3.up * 0.7f + dir * 0.6f;
            switch (kind)
            {
                case 2: Shapes.Rounded("Boulder", view, Vector3.zero, new Vector3(0.75f, 0.7f, 0.75f), 0.25f, mat); break;
                case 3: Shapes.Rounded("Wave", view, Vector3.zero, new Vector3(1.4f, 0.5f, 0.12f), 0.06f, mat).transform.localRotation = Quaternion.LookRotation(dir); break;
                default: Shapes.Primitive(PrimitiveType.Sphere, "Orb", view, Vector3.zero, Vector3.one * (kind == 1 ? 0.42f : 0.55f), mat); break;
            }
            duelShots.Add(new DuelShot { view = view, pos = view.position, dir = dir, speed = SpecialSpeed(kind), radius = SpecialRadius(kind), mine = mine, kind = kind });
            fx.Burst(view.position, colour, colour * 2f, 16, 4f);
            AudioManager.PlaySfx(kind == 1 ? Sfx.Shield : Sfx.Impact, 0.8f, kind == 2 ? 0.6f : 1.3f);
        }

        private void UpdateShots(float dt)
        {
            for (int i = duelShots.Count - 1; i >= 0; i--)
            {
                var s = duelShots[i];
                if (s.view == null) { duelShots.RemoveAt(i); continue; }
                float step = s.speed * dt;
                s.pos += s.dir * step;
                s.travelled += step;
                s.view.position = s.pos;
                if (s.kind == 2) s.view.Rotate(Vector3.Cross(Vector3.up, s.dir), step * 120f, Space.World);
                if (Random.value < dt * 20f) fx.Burst(s.pos, SpecialColour[s.kind], SpecialColour[s.kind] * 1.5f, 3, 1.5f);
                Vector3? target = s.mine ? hunt.ChallengeFoeAt : robot.transform.position;
                bool hit = target.HasValue && Flat(target.Value - s.pos).magnitude < s.radius;
                if (!hit && s.travelled < 16f) continue;
                if (hit) ShotLands(s);
                Destroy(s.view.gameObject);
                duelShots.RemoveAt(i);
            }
        }

        private void ShotLands(DuelShot s)
        {
            var colour = SpecialColour[s.kind];
            fx.Burst(s.pos, colour, colour * 2.5f, 34, 7f);
            Shockwave.Create(new Vector3(s.pos.x, robot.transform.position.y, s.pos.z), 1.8f, colour);
            AudioManager.PlaySfx(Sfx.Impact, 1f, s.kind == 2 ? 0.6f : 1f);
            cameraRig.Shake(s.kind == 2 ? 0.6f : 0.35f);
            if (s.mine)
            {
                if (LiveActive) LiveNet.SpecialHit();
                else HitFoe(SpecialTimes);
                if (colosseum != null) colosseum.Cheer(0, 0.7f);
            }
            else if (!LiveActive) HurtMe(SpecialTimes); // a live rival's: the server says what it did
        }

        // ---------- The ultimate ----------

        /// <summary>Slow motion, the camera close: four quick blows and a blast (the mover's: foi's or the rival's).</summary>
        private IEnumerator Ultimate(bool mine)
        {
            ultimateOn = true;
            skillBar?.Shout(Loc.T("duel.ultTitle"), mine ? new Color(0.8f, 0.5f, 1f) : new Color(1f, 0.4f, 0.4f));
            AudioManager.PlaySfx(Sfx.Win, 1f, 0.6f);
            if (colosseum != null) colosseum.Cheer(mine ? 0 : 1, 1f);
            Time.timeScale = 0.35f;
            if (mine) hunt.StunFoe(3f);
            else strikeCooldown = Mathf.Max(strikeCooldown, 1.2f); // foi reels under it
            for (int i = 0; i < 5; i++)
            {
                yield return new WaitForSecondsRealtime(i == 4 ? 0.35f : 0.2f);
                if (!challengeRun || chPhase != ChPhase.Duel) break;
                var foeAt = hunt.ChallengeFoeAt ?? robot.transform.position;
                var from = mine ? robot.transform.position : foeAt;
                var to = mine ? foeAt : robot.transform.position;
                var colour = mine ? new Color(0.8f, 0.5f, 1f) : new Color(1f, 0.35f, 0.3f);
                if (mine) robot.Strike(to);
                SlashFx.Create(from, to, colour, i == 4 ? 3f : 1.6f);
                fx.Burst(to + Vector3.up * 0.6f, colour, colour * 2.5f, i == 4 ? 60 : 20, i == 4 ? 9f : 5f);
                if (i == 4) Shockwave.Create(to, 3.2f, colour);
                cameraRig.Shake(i == 4 ? 0.9f : 0.3f);
                AudioManager.PlaySfx(Sfx.Impact, 1f, i == 4 ? 0.55f : 1.1f);
                if (!LiveActive)
                {
                    float part = i == 4 ? UltimateHit * 0.5f : UltimateHit * 0.125f;
                    if (mine) HitFoe(part); else HurtMe(part);
                }
            }
            if (State == GameState.Playing) Time.timeScale = 1f;
            ultimateOn = false;
        }

        // ---------- The finish ----------

        /// <summary>After foi's knock-down: the finish is offered for a moment.</summary>
        private void OfferFinish(Vector3 at)
        {
            finishAt = at;
            finishUntil = Time.time + 2.2f;
        }

        /// <summary>The finish: the fallen rival goes up in sparks and flying bolts (robots don't bleed: they come apart).</summary>
        private void DoFinish()
        {
            if (Time.time > finishUntil || finishing) return;
            finishUntil = 0f;
            if (LiveActive) LiveNet.Special(6);
            cameraRig.StartCoroutine(Finisher(finishAt, true));
        }

        private IEnumerator Finisher(Vector3 at, bool mine)
        {
            finishing = true;
            skillBar?.Shout(Loc.T("duel.finished"), mine ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.4f, 0.4f));
            if (colosseum != null) colosseum.Cheer(mine ? 0 : 1, 1f);
            var bolt = new Color(0.62f, 0.64f, 0.7f);
            for (int i = 0; i < 4; i++)
            {
                fx.Burst(at + Vector3.up * (0.3f + i * 0.25f), new Color(1f, 0.85f, 0.4f), new Color(3f, 2.4f, 0.8f), 30, 6f + i);
                fx.Burst(at + Vector3.up * 0.5f, bolt, bolt * 1.4f, 14, 7f);
                Shockwave.Create(at, 1.2f + i * 0.7f, new Color(1f, 0.8f, 0.4f));
                cameraRig.Shake(0.4f + i * 0.15f);
                AudioManager.PlaySfx(i == 3 ? Sfx.Impact : Sfx.Blocked, 1f, 1.3f - i * 0.15f);
                yield return new WaitForSeconds(0.18f);
            }
            finishing = false;
        }

        // ---------- The AI rival's moves ----------

        private void RivalMoves()
        {
            if (Time.time < rivalThinkAt || !hunt.ChallengeFoeAt.HasValue) return;
            rivalThinkAt = Time.time + 0.45f;
            var foeAt = hunt.ChallengeFoeAt.Value;
            float dist = Flat(foeAt - robot.transform.position).magnitude;
            float bold = Mathf.Clamp01(rival.boldness);
            if (rivalMeter >= 3f && dist <= 3.5f && Random.value < 0.35f + 0.3f * bold)
            {
                rivalMeter = 0f;
                cameraRig.StartCoroutine(Ultimate(false));
                return;
            }
            if (rivalMeter >= 1f && myCombo >= 4 && Random.value < 0.3f)
            {
                rivalMeter -= 1f;
                myCombo = 0;
                myComboMove = 0;
                strikeCooldown = Mathf.Max(strikeCooldown, 0.9f); // foi's swing knocked aside
                Shockwave.Create(foeAt, 2.4f, new Color(1f, 0.45f, 0.4f));
                AudioManager.PlaySfx(Sfx.Shield, 1f, 0.75f);
                skillBar?.Shout(Loc.T("duel.broken"), new Color(1f, 0.45f, 0.4f));
                return;
            }
            if (rivalCombo >= 3 && dist <= 2.2f && Random.value < 0.5f)
            {
                rivalCombo = 0;
                SlashFx.Create(foeAt, robot.transform.position, new Color(1f, 0.35f, 0.3f), 1.8f);
                Shockwave.Create(robot.transform.position, 1.8f, new Color(1f, 0.4f, 0.3f));
                cameraRig.Shake(0.45f);
                HurtMe(HeavyHit);
                return;
            }
            if (rivalMeter >= 1f && dist >= 3f && Random.value < 0.22f + 0.15f * bold)
            {
                rivalMeter -= 1f;
                FireShot(false, rivalHero, foeAt, robot.transform.position);
            }
        }

        // ---------- A live rival's moves (the server's word) ----------

        /// <summary>The server's bars (when it sends them).</summary>
        private void LiveMeters(float mine, float theirs)
        {
            if (mine >= 0f) myMeter = Mathf.Clamp(mine, 0f, 3f);
            if (theirs >= 0f) rivalMeter = Mathf.Clamp(theirs, 0f, 3f);
        }

        /// <summary>A move of the live rival, as the server passes it on: shown here (its damage comes with the health).</summary>
        private void LiveRivalMove(int kind)
        {
            if (!challengeRun) return;
            var foeAt = hunt.ChallengeFoeAt ?? robot.transform.position;
            switch (kind)
            {
                case 1: FireShot(false, rivalHero, foeAt, robot.transform.position); break;
                case 2:
                case 3:
                    SlashFx.Create(foeAt, robot.transform.position, new Color(1f, 0.35f, 0.3f), kind == 3 ? 2.4f : 1.8f);
                    Shockwave.Create(robot.transform.position, kind == 3 ? 2.6f : 1.8f, new Color(1f, 0.4f, 0.3f));
                    cameraRig.Shake(kind == 3 ? 0.7f : 0.45f);
                    break;
                case 4: cameraRig.StartCoroutine(Ultimate(false)); break;
                case 5:
                    myCombo = 0; myComboMove = 0;
                    strikeCooldown = Mathf.Max(strikeCooldown, 0.9f);
                    Shockwave.Create(foeAt, 2.4f, new Color(1f, 0.45f, 0.4f));
                    skillBar?.Shout(Loc.T("duel.broken"), new Color(1f, 0.45f, 0.4f));
                    break;
                case 6: cameraRig.StartCoroutine(Finisher(robot.transform.position, false)); break;
            }
        }
    }
}
