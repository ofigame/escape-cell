using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// The bot-against-bot balance report (Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md 8, Faz 8): matches played by the rules alone
    /// (no pictures, a fixed step), every family against every family at the same tier, then the Spam bot against a
    /// thinking one. It counts the rounds' length, the hits that finish a round (the winner's clean hits, the target 15–25),
    /// the families' wins, how rounds end (knock-out, time, golden), and the moves used.
    /// </summary>
    public static class DuelBenchV2
    {
        private const float Step = 1f / 60f;

        private class Tally
        {
            public int matches, rounds, ko, time, golden, flawless;
            public float roundSeconds;
            public int winnerHits, loserHits;
            public readonly List<int> finishHits = new List<int>();
            public int breaks, perfects, powers, ultis, specials, clashes, whiffs, wasted, taps, chainsBroken;
            public float minGap = 9f;
            public int rainDrops;
        }

        public static string Run(int matches, int spamMatches, int tier, int seed)
        {
            var s = DuelCoreSettings.Current;
            var rng = new System.Random(seed);
            var all = new Tally();
            var famWins = new int[4]; var famPlayed = new int[4];
            var pair = new int[4, 4]; var pairPlayed = new int[4, 4];
            for (int m = 0; m < matches; m++)
            {
                int a = m % 4, b = (m / 4) % 4;
                float skillA = 0.35f + 0.5f * (float)rng.NextDouble(), skillB = 0.35f + 0.5f * (float)rng.NextDouble();
                var pa = (BotPersonality)rng.Next(3); var pb = (BotPersonality)rng.Next(3);
                int w = Play(s, a, b, tier, skillA, skillB, pa, pb, rng.Next(), all);
                famPlayed[a]++; famPlayed[b]++;
                famWins[w == 0 ? a : b]++;
                pairPlayed[a, b]++; if (w == 0) pair[a, b]++;
            }
            // the Spam bot (a finger that never stops) against a thinking bot of middle skill
            var spam = new Tally(); int spamWins = 0;
            for (int m = 0; m < spamMatches; m++)
            {
                int a = m % 4, b = (m / 4) % 4;
                int w = Play(s, a, b, tier, 0.5f, 0.5f, BotPersonality.Spam, (BotPersonality)rng.Next(3), rng.Next(), spam);
                if (w == 0) spamWins++;
            }
            return Report(all, famWins, famPlayed, pair, pairPlayed, spam, spamWins, spamMatches, tier, matches);
        }

        private static int Play(DuelCoreSettings s, int famA, int famB, int tier, float skillA, float skillB, BotPersonality pa, BotPersonality pb, int seed, Tally t)
        {
            var sim = new DuelSim(s, new[] { famA, famB }, new[] { tier, tier }, 5.5f * s.platformWidth, 5.2f);
            var botA = new DuelBotV2(skillA, pa, seed);
            var botB = new DuelBotV2(skillB, pb, seed * 7 + 1);
            var roundHits = new int[2];
            float roundStart = 0f;
            int guard = 0;
            while (sim.Phase != RoundPhase.MatchOver && guard++ < 60 * 60 * 12)
            {
                var ia = botA.Think(sim, sim.F[0], sim.F[1]);
                var ib = botB.Think(sim, sim.F[1], sim.F[0]);
                sim.Tick(Step, ia, ib);
                foreach (var e in sim.Events)
                {
                    switch (e.kind)
                    {
                        case DuelEventKind.Result:
                            if (e.result == HitResult.Hit) roundHits[e.who]++;
                            break;
                        case DuelEventKind.UltiHit: roundHits[e.who]++; t.ultis++; break;
                        case DuelEventKind.RainDrop: if (e.result == HitResult.Hit) t.rainDrops++; break; // (a rain is not a blow: counted apart)
                        case DuelEventKind.RoundFight: roundStart = sim.Time; roundHits[0] = roundHits[1] = 0; break;
                        case DuelEventKind.RoundOver:
                            t.rounds++;
                            t.roundSeconds += sim.Time - roundStart;
                            if (e.why.StartsWith("ko")) t.ko++; else if (e.why.StartsWith("golden")) t.golden++; else t.time++;
                            if (e.why.Contains("flawless")) t.flawless++;
                            t.winnerHits += roundHits[e.who]; t.loserHits += roundHits[1 - e.who];
                            t.finishHits.Add(roundHits[e.who]);
                            break;
                        case DuelEventKind.BreakUsed: t.breaks++; break;
                        case DuelEventKind.RainStart: t.powers++; break;
                        case DuelEventKind.Special: t.specials++; break;
                        case DuelEventKind.ChainBroken: t.chainsBroken++; break;
                    }
                }
                sim.Events.Clear();
            }
            t.matches++;
            foreach (var f in sim.F) { t.perfects += f.perfects; t.clashes += f.clashes; t.whiffs += f.whiffs; t.wasted += f.wastedTaps; t.taps += f.taps; t.minGap = Mathf.Min(t.minGap, f.minGap); }
            return sim.MatchWinner < 0 ? 0 : sim.MatchWinner;
        }

        private static readonly string[] Fam = { "Kılıç", "Mızrak", "Balta", "Ağır" };

        private static string Report(Tally t, int[] famWins, int[] famPlayed, int[,] pair, int[,] pairPlayed, Tally spam, int spamWins, int spamMatches, int tier, int matches)
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Bot – bot denge raporu (" + matches + " maç, kademe " + tier + ", adım 1/60 sn)");
            sb.AppendLine();
            float rs = t.rounds > 0 ? t.roundSeconds / t.rounds : 0f;
            t.finishHits.Sort();
            float med = t.finishHits.Count > 0 ? t.finishHits[t.finishHits.Count / 2] : 0;
            float avgFinish = t.rounds > 0 ? (float)t.winnerHits / t.rounds : 0f;
            sb.AppendLine("| Ölçü | Değer |");
            sb.AppendLine("|---|---|");
            sb.AppendLine("| Raunt sayısı | " + t.rounds + " (maç başı " + (t.rounds / (float)Mathf.Max(1, t.matches)).ToString("0.00") + ") |");
            sb.AppendLine("| Ortalama raunt süresi | " + rs.ToString("0.0") + " sn |");
            sb.AppendLine("| Raundu bitiren isabet (kazananın temiz isabeti) | ortalama " + avgFinish.ToString("0.0") + ", ortanca " + med + " (hedef 15–25) |");
            sb.AppendLine("| Kaybedenin isabeti | ortalama " + (t.rounds > 0 ? (float)t.loserHits / t.rounds : 0f).ToString("0.0") + " |");
            sb.AppendLine("| Raunt sonu | nakavt %" + Pct(t.ko, t.rounds) + " · süre %" + Pct(t.time, t.rounds) + " · altın raunt %" + Pct(t.golden, t.rounds) + " · kusursuz %" + Pct(t.flawless, t.rounds) + " |");
            sb.AppendLine("| Maç başı | KIR " + Per(t.breaks, t.matches) + " · kusursuz savunma " + Per(t.perfects, t.matches) + " · güç " + Per(t.powers, t.matches) + " · ulti " + Per(t.ultis, t.matches) + " · özel " + Per(t.specials, t.matches) + " · çarpışma " + Per(t.clashes, t.matches) + " · ıska " + Per(t.whiffs, t.matches) + " · isabet eden yağmur damlası " + Per(t.rainDrops, t.matches) + " |");
            sb.AppendLine("| 3'lü set boşluğu (en kısa) | " + (t.minGap < 9f ? Mathf.RoundToInt(t.minGap * 1000f) + " ms" : "-") + " (hedef ≥ 150 ms) |");
            sb.AppendLine();
            sb.AppendLine("### Aile kazanma oranları");
            sb.AppendLine();
            sb.AppendLine("| Aile | Maç | Kazanma |");
            sb.AppendLine("|---|---|---|");
            for (int f = 0; f < 4; f++) sb.AppendLine("| " + Fam[f] + " | " + famPlayed[f] + " | %" + Pct(famWins[f], famPlayed[f]) + " |");
            sb.AppendLine();
            sb.AppendLine("Satır ailesinin sütun ailesine karşı kazanma oranı:");
            sb.AppendLine();
            sb.AppendLine("| | Kılıç | Mızrak | Balta | Ağır |");
            sb.AppendLine("|---|---|---|---|---|");
            for (int a = 0; a < 4; a++)
            {
                sb.Append("| " + Fam[a] + " ");
                for (int b = 0; b < 4; b++) sb.Append("| " + (pairPlayed[a, b] > 0 ? "%" + Pct(pair[a, b], pairPlayed[a, b]) : "-") + " ");
                sb.AppendLine("|");
            }
            sb.AppendLine();
            sb.AppendLine("### Spam botu");
            sb.AppendLine();
            sb.AppendLine("Durmadan basan bot, orta ustalıkta düşünen bota karşı " + spamMatches + " maçta **%" + Pct(spamWins, spamMatches) + "** kazandı (hedef < %20). Spam'in dokunuşlarının %" + Pct(spam.wasted, spam.taps) + "'i boşa gitti (pencere dışı / zincir kırıldı: " + spam.chainsBroken + " kez); ıska maç başı " + Per(spam.whiffs, spam.matches) + ".");
            return sb.ToString();
        }

        private static string Pct(int a, int b) => b > 0 ? Mathf.RoundToInt(100f * a / b).ToString() : "-";
        private static string Per(int a, int b) => b > 0 ? (a / (float)b).ToString("0.0") : "-";
    }
}
