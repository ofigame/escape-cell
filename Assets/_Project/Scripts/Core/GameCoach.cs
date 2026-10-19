using System;
using System.Collections.Generic;
using System.Linq;
using BrickMuseum.City;
using BrickMuseum.Data;
using BrickMuseum.UI;
using UnityEngine;

namespace BrickMuseum.Core
{
    /// <summary>
    /// Tutorials: a short tip the first time something matters — the first steps for a new player, then every feature
    /// when it first comes near (the city score and why it matters, the level bonuses, how to open the far district, the
    /// fish farm, walking, the car, fishing, the rooftop, free building...). Each is shown once, on its own screen, when
    /// nothing else is open; the ring shows the button it is about.
    /// </summary>
    public sealed partial class Game
    {
        private sealed class Tip
        {
            public string key, title, text;
            public Mode mode;
            public Func<bool> when;
            public Func<RectTransform> target;
            public Action shown;
        }

        private readonly CoachUi coach = new CoachUi();
        private List<Tip> tips;
        private Tip tipShown;
        private float coachWait = 1.5f;
        private bool touring;            // the screenshot tour: tips only when it asks for them

        private static RectTransform R(Component c) => c != null ? (RectTransform)c.transform : null;

        private void WireCoach()
        {
            coach.Build(transform);
            coach.ok.onClick.AddListener(DismissTip);
            tips = new List<Tip>
            {
                // ----- the city: first steps -----
                CityTip("welcome", "Brickville'e hoş geldin!", "Parça parça binalar yap, şehrini büyüt.\nBir EV arsasına dokun ve ilk evini seç.",
                    () => Progress.Data.placements.Count == 0, () => R(cu.help.transform.parent)),
                CityTip("stars", "Yıldızlar ve seviye", "Her bina yıldız kazandırır. Yıldızlar seviyeni yükseltir; yeni arsalar ve mahalleler açılır.",
                    () => Progress.Data.placements.Count >= 1, () => R(cu.level.transform.parent)),
                CityTip("walk", "Şehri gez", "Şehrinde yürü: insanlarla karşılaş, arabana bin, balık tut, gökdelenlerin tepesine çık.",
                    () => Progress.Data.placements.Count >= 2, () => R(cu.walkBtn)),
                CityTip("money", "Malzeme ve para", "Binalar sana kaba malzeme kazandırır. Lira; taksicilik, balık tutma ve balık çiftliğinden gelir. Lirayla özel malzeme, dekor ve arsa alınır.",
                    () => Progress.Data.placements.Count >= 2, () => R(cu.money.transform.parent)),
                CityTip("home", "Kendi evin", "Şehirden kazandığın malzemelerle kendi evini ve arabanı serbestçe yap.",
                    () => Progress.Level >= 2, () => R(cu.buildBtn)),
                CityTip("score", "Şehir puanı neden önemli?", $"Polis, belediye işçisi, personel, çöp konteyneri, belediye araçları ve kamu binaları puanı artırır. Puan {CityScore.HouseGardens}: ev bahçeleri, puan {CityScore.BuildingGardens}: büyük binaların çevresi geliştirilir.",
                    () => Progress.Data.placements.Count >= 3, () => R(cu.scoreBtn)),
                CityTip("bonus", "Seviye bonusu", "Her yeni seviyede bir şehir bonusu kazanırsın. Reklam izleyip topla: bank, lamba, konteyner, trafik lambası ya da belediye ekibi gelir.",
                    () => Progress.Data.bonusPending > 0, () => R(cu.bonusBtn)),
                CityTip("rest", "Yapım molası", "Bir türü bitirince o tür 1 saat dinlenir. Bu sırada başka türden yap ya da reklam izleyip hemen aç.",
                    () => Progress.Data.cooldowns.Any(c => c.until > DateTime.UtcNow.Ticks), null),
                CityTip("inventory", "Envanter", "Bonuslar ve şehirden kaldırdığın binalar burada bekler. Boş bir arsaya dokunup yerleştir.",
                    () => Progress.Data.inventory.Count > 0 && Progress.Data.placements.Count >= 2, () => R(cu.inventoryBtn)),
                CityTip("garden", "Bahçeler açıldı", $"Şehir puanın {CityScore.HouseGardens} oldu! Bir eve dokun ve \"Bahçeyi geliştir\" ile bahçesini kur.",
                    () => CityScore.Total >= CityScore.HouseGardens, () => R(cu.scoreBtn)),
                new Tip { key = "farm", mode = Mode.City, title = "Balık çiftliği", text = $"Sahil Parkı'nın önündeki gölde balık çiftliği satılık. Her 2 saatte {FishFarm.Batch} balık verir, oltadan çok kazandırır. Kafeslere dokun.",
                    when = () => FishFarm.Unlocked && !FishFarm.Owned, shown = FocusFarm },
                CityTip("farmReady", "Balıklar hazır", "Butona dokun, balıkları topla ve Balık Hali'nde sat. Sonraki balıklar kendiliğinden büyür.",
                    () => FishFarm.Ready && cu.farmBtn.gameObject.activeSelf, () => R(cu.farmBtn)),
                CityTip("far", "Uzak semt: Doğu Yakası", $"Şehrin doğusunda imara kapalı bir semt var. Okul, hastane ve polis karakolu yapınca {District.Price} liraya imara açabilirsin.",
                    () => Progress.Level >= 7 && !District.IsOpen, () => R(cu.farBtn)),
                CityTip("farReady", "Uzak semt açılabilir", $"Okul, hastane ve polis karakolu hazır! {District.Price} lira ile Doğu Yakası'nı imara aç; orada her türden bina yeniden yapılabilir.",
                    () => District.Ready && !District.IsOpen, () => R(cu.farBtn)),

                CityTip("sites", "Yarım bırakabilirsin", "Büyük yapılar uzun sürer: istediğin an şehre dön, bina yapıldığı kadarıyla arsasında durur. İnşaya devam et ile kaldığın yerden sürdür.",
                    () => Progress.Data.sites.Count > 0, () => R(cu.resumeBtn)),
                CityTip("homes", "Hangi evden gezersin?", "Arsalarından birine dokun ve Buradan gez de: şehir turu o evin önünden başlar.",
                    () => FreeBuild.Lots.OwnedCount > 1, null),

                CityTip("sport", "Spor sahaları", "Ormanın önündeki yeni sırada futbol, basketbol, tenis ve voleybol sahaları yapabilirsin. Her saha şehir puanına 10 ekler ve sporcular gelir.",
                    () => Progress.Level >= 4, null),

                // ----- the workshop -----
                new Tip { key = "bag", mode = Mode.Workshop, title = "Nasıl yapılır?", text = "Resimdeki modele bak. Çantayı aç, parçayı seç ve parlayan yere koy. Yanlış parça bir hata hakkı götürür.",
                    when = () => current != null && bench.PlacedCount == 0, target = () => R(wu.bagBtn) },
                new Tip { key = "bags", mode = Mode.Workshop, title = "Torbalar", text = "Büyük modeller torba torba gelir: her torbada iki kat kadar parça var. Bir torba bitince sonraki açılır.",
                    when = () => current != null && bags.Count > 1, target = () => R(wu.bagBtn) },
                new Tip { key = "hint", mode = Mode.Workshop, title = "İpucu", text = "Takılırsan ipucu düğmesi sıradaki parçayı gösterir.",
                    when = () => current != null && bench.PlacedCount >= 6, target = () => R(wu.hintBtn) },

                // ----- walking -----
                new Tip { key = "walkMove", mode = Mode.Walk, title = "Yürüme", text = "Sol başparmakla yürü, sağ başparmakla etrafına bak. ZIPLA ile zıpla.",
                    when = () => !driving, target = () => R(tu.jumpBtn) },
                new Tip { key = "car", mode = Mode.Walk, title = "Araban", text = "BİN ile arabana bin. Taksi tabelası olan arabayla yolcu taşıyıp para kazanırsın.",
                    when = () => tu.carBtn.gameObject.activeInHierarchy, target = () => R(tu.carBtn) },
                new Tip { key = "fish", mode = Mode.Walk, title = "Balık tutma", text = "Su kenarında OLTA'ya dokun. Basılı tutup bırakınca olta atılır; balık vurunca ibreyi yeşil alanda tut.",
                    when = () => tu.fishBtn.gameObject.activeInHierarchy, target = () => R(tu.fishBtn) },
                new Tip { key = "sell", mode = Mode.Walk, title = "Balık Hali", text = "Soğutucudaki balıkları burada satıp lira kazan.",
                    when = () => tu.sellBtn.gameObject.activeInHierarchy, target = () => R(tu.sellBtn) },
                new Tip { key = "bench", mode = Mode.Walk, title = "Banklar", text = "Parktaki banklara oturabilirsin: yanına gidip OTUR'a dokun. Yürümeye başlayınca kalkarsın.",
                    when = () => benchNear != null && tu.homeBtn.gameObject.activeInHierarchy, target = () => R(tu.homeBtn) },
                new Tip { key = "roof", mode = Mode.Walk, title = "Gökdelen tepesi", text = "ÇIK ile gökdelenin en tepesine çık ve şehri yukarıdan seyret.",
                    when = () => roofNear != null && tu.homeBtn.gameObject.activeInHierarchy, target = () => R(tu.homeBtn) },
                new Tip { key = "taxi", mode = Mode.Walk, title = "Taksicilik", text = "Taksi düğmesine dokun, yolcuyu al ve şeritteki oku izleyerek götür. Ücreti ve bahşişi sen alırsın.",
                    when = () => tu.taxiBtn.gameObject.activeInHierarchy, target = () => R(tu.taxiBtn) },

                // ----- free building -----
                new Tip { key = "build", mode = Mode.Build, title = "Serbest inşa", text = "Alttan malzeme seç, ızgaraya dokunarak koy. Kat ekle; kapı, pencere ve merdiven koy. Kaba malzeme şehirden gelir, özel malzeme lirayla alınır.",
                    when = () => true, target = null },
            };
        }

        private Tip CityTip(string key, string title, string text, Func<bool> when, Func<RectTransform> target) =>
            new Tip { key = key, mode = Mode.City, title = title, text = text, when = when, target = target };

        private bool SeenTip(string key) => Progress.Data.tips.Contains(key);

        /// <summary>Is the screen free for a tip (no panel, list or other overlay open)?</summary>
        private bool CoachFree()
        {
            switch (mode)
            {
                case Mode.City: return !cu.panel.activeSelf && !cu.list.activeSelf && !cu.levelUp.activeSelf;
                case Mode.Workshop: return !busy && !wu.win.activeSelf && !wu.ad.activeSelf;
                case Mode.Walk: return !decorating && fishing == FishState.Off;
                default: return true;
            }
        }

        private void CoachTick(float dt)
        {
            coach.Tick(time);
            if (tips == null || coach.Shown || touring) return;
            coachWait -= dt;
            if (coachWait > 0f) return;
            coachWait = 0.5f;
            if (!CoachFree()) return;
            var tip = tips.FirstOrDefault(t => t.mode == mode && !SeenTip(t.key) && t.when());
            if (tip != null) ShowTip(tip);
        }

        private void ShowTip(Tip tip)
        {
            tipShown = tip;
            tip.shown?.Invoke();
            coach.Show(tip.title, tip.text, $"{tips.Count(t => SeenTip(t.key)) + 1}/{tips.Count}", tip.target?.Invoke());
        }

        private void DismissTip()
        {
            if (tipShown != null && !SeenTip(tipShown.key)) { Progress.Data.tips.Add(tipShown.key); Progress.Save(); }
            tipShown = null;
            coach.Hide();
            coachWait = 6f;    // a breath between tips
        }

        /// <summary>All tips again from the start (the language screen offers it).</summary>
        private void ReplayTips()
        {
            Progress.Data.tips.Clear();
            Progress.Save();
            Toast("Eğitimler baştan gösterilecek");
        }
    }
}
