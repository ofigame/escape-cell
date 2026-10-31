# Düello Dövüş Çekirdeği v2 — Faz 0 Teşhis

Bugünkü dövüşün (dal `rutbe-kusami`, b42) koddan ve veriden ölçümü. Kaynaklar: `Resources/Duel/duel_rules.json`,
`Data/DuelPower.cs`, `Data/RobotParts.cs`, `Gameplay/GameManagerHunt.cs` (TryStrikeNow), `Gameplay/GameManagerDuelSkills.cs`,
`Gameplay/GameManagerGuard.cs`, `Visual/WeaponModels.cs` (HeldWeapon).

## 1. Bir nakavt için kaç isabet?

Düello canı lig kademesinin tavanından gelir: `DuelPower.Stats.hp = RobotParts.HpAt(min(Gövde, tavan)) × 2`
(`HpAt = 150 + 40 × seviye`; tavanlar 4/8/12/16/20). Normal darbe `familyBlows[aile × 6 + silah kademesi]`
(silah kademesi tavanı 2..6). Kollar bonusu (+%1/seviye) ve Kabuk zırhı (−%1,25/seviye) birbirini kabaca götürür.

| Lig kademesi | Can | Kılıç | Mızrak | Balta | Ağır |
|---|---|---|---|---|---|
| 1 | 620 | 124 isabet (5) | 103 (6) | 56 (11) | 52 (12) |
| 2 | 940 | 134 (7) | 104 (9) | 63 (15) | 55 (17) |
| 3 | 1260 | 140 (9) | 97 (13) | 60 (21) | 55 (23) |
| 4 | 1580 | 132 (12) | 99 (16) | 56 (28) | 53 (30) |
| 5 | 1900 | 119 (16) | 90 (21) | 53 (36) | 49 (39) |

(parantezde bir isabetin hasarı). **Sonuç:** aynı kademede bir nakavt için **50–140 isabet** gerekiyor; kılıçla
tek bir isabet can çubuğunun %0,7–0,8'i: çubukta neredeyse görünmüyor. Rehberdeki "~180" kılıcın en kötü
durumuna (kalkanla engellenenler, sıyırmalar) yakın.

## 2. Vurulan taraf vurulurken saldırabiliyor ve hareket edebiliyor mu?

**Evet.** Normal isabetin kuralda sersemletmesi yok:
- `familyStagger = [0, 0, 0, 0.15]`: yalnız Ağır silahın isabeti rakibi 0,15 sn durduruyor (ve yalnızca bot rakipte, `hunt.StunFoe`).
- Oyuncuya gelen isabet oyuncunun `strikeCooldown`'ına dokunmuyor: vurulurken vurmaya ve yürümeye devam ediyor.
- Sersemleme yalnız büyük hareketlerde var: GÜÇLÜ 0,4 sn, YERE SER 1,8 sn (`heavyStun` / `upperStun`), kusursuz savunmada saldıran 0,5 sn sendeler (`staggerSeconds`).
- Gösterilen tepkiler (sarsılma, sendeleme) yalnız görsel.

## 3. Takas anları

Kuralda engelleyen bir şey yok: iki taraf da bekleme süresi dolar dolmaz vurabiliyor ve isabet sersemletmediği
için iki tarafın darbesi aynı yarım saniyede karşılıklı işliyor. Yakın mesafede durup basan iki oyuncu arasında
**neredeyse her darbe bir takas**; bir maçta yüzlerce kez. (Bot da oyuncunun saldırısından sonra hemen karşılık
veriyor; `RivalMoves` en geç ~0,45 sn'de bir düşünüyor.)

## 4. Saldırının evreleri kuralda var mı?

**Hayır, kural tek anda.** `TryStrikeNow` dokunuşta hasarı hesaplıyor. Hazırlık/vuruş/toparlanma yalnız
`HeldWeapon`'ın çiziminde (AttackMotionData: anticipation / active / recovery ms). Düz darbelerde
(`PlainDuelBlows = true`) hasar **dokunduğun anda** işliyor (`sync` kapalı); düello animasyonlu modda bile kural
"dokunuşta sayıldı, temas anında gösterildi". Saldıranın dokunuş sonrası tek kısıtı silah ailesinin bekleme süresi
(0,12–0,30 sn).

## 5. Spam botu

Ölçüm analitik (bugünkü kodda ayrı bir spam botu yok): bekleme süresi dolar dolmaz basmak en yüksek hasarı veren
oyun. Iskanın, engellenmenin ya da boşa basmanın cezası yok (engellenen darbe bile kalkan dayanıklılığını düşürüyor),
sersemleme olmadığı için "sırayı kaybetmek" diye bir şey yok. Dolayısıyla durmadan basan bir oyuncu, düşünerek
oynayana karşı **en az eşit, çoğu zaman üstün**: savunma (kalkan, kusursuz savunma) yalnız zamanlaması tutarsa
fark yaratıyor. Hedef v2'de spam'in %20'nin altında kazanması: Faz 8'deki bot–bot ölçümü bunu sayıyla verecek.

## Özet

| Soru | Bugün |
|---|---|
| Nakavt için isabet | 50–140 (kılıç en çok) |
| İsabette sersemleme | Yok (Ağır 0,15 sn) |
| Takas | Sürekli |
| Saldırı evreleri kuralda | Yok: hasar dokunuşta |
| Spam | En iyi strateji |

Bu beş madde birlikte "kim vurdu, sıra kimde" sorusunu cevapsız bırakıyor: v2 çekirdeği tam olarak bunları kurala
bağlıyor (evreler, sersemleme, avantaj, takas kuralı, ritmik zincir, yüzde hasar).

---

# Uygulama planı

**Yer:** Yeni çekirdek ayrı bir ad alanında (`SquashBot.DuelV2`, `Scripts/DuelV2/`); oyunun mevcut kodu yalnız
bir bayrakla (`DuelCoreV2.Running`) ve prototip sahnesine özel bir giriş noktasıyla (`GameManagerDuelV2.cs`,
yeni dosya) değişir. Bayrak kapalıyken oyun bit bit aynı.

**Sahne:** `Scenes/Dev/DuelPrototypeV2.unity`. Oyunun kendi kurulumu (arena, seyirci, ışık, iki foi, silahlar,
taçlar) olduğu gibi kullanılır: prototip, düelloyu mevcut kodla kurar; kurulum bitince yeni çekirdek iki foi'yi
devralır, eski düello mantığı (girdi, bot, saat, eski kamera, HUD) bayrakla durur.

**Katmanlar:**
1. `DuelCoreSettings` (ScriptableObject + JSON kaydet/yükle): bütün sayılar.
2. `DuelSim`: saf kural (görselden bağımsız; Faz 8'de görüntüsüz 500 maç için de aynı kod): iki `Fighter`,
   şerit üzerinde sürekli konum, durum makinesi (boşta, yürü, saldırı evreleri, sersem, engel, kaçış, yerde,
   kalkış, zıplama, çömelme, özel, ulti, çağırma), isabet çözümü, takas kuralı, zincir/seri, hasar, raunt,
   elemental güçler, olay kuyruğu (görsel/kamera/ses buradan beslenir).
3. `FighterView`: kural durumunu foi'ye çevirir (konum, silah savuruşu evrelere göre, tepkiler, kalkan, yere düşme).
4. `DuelCameraV2`: takip ve olay kurgusu.
5. `DuelHudV2` (IMGUI, prototip): can + hayalet çubuk, raunt noktaları, saat, seri, güç yuvaları, düğmeler,
   sonuç yazıları, avantaj çubuğu, vuruş günlüğü, F1 ayar paneli, F2/F3.
6. `DuelBotV2`: düşünen bot + spam botu.

**Fazlar** belgedeki gibi (0–9); her faz ayrı commit, ilerleme `Docs/DovusV2_Ilerleme.md`.
