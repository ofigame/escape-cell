# Düello Dövüş Çekirdeği v2: Prototip Raporu (Faz 0–9)

Belge: `Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md` · Dal: `dovus-cekirdegi-v2` (SquashBot) · İlerleme: `Docs/DovusV2_Ilerleme.md`

## Teslim

- **Oyun:** `SquashBot/Builds/DuelPrototypeV2.zip` (~145 MB). Klasörü çıkarın ve `foiDuelV2.exe`'yi açın. Kurulum gerekmez. Windows 64 bit, pencere dikey açılır, Alt+Enter tam ekrana geçirir.
- **OKUBENI.txt** zip'in içinde. Kontrolleri, F1–F7 tuşlarını, menüyü ve ayar dosyasını anlatır.
- **Ayar dosyası:** F1 panelinde "Kaydet" denince `DuelCoreSettings.json` exe'nin yanına yazılır. Oyun açılışta bu dosyayı okur; dosya silinirse varsayılanlar geri gelir.
- **Kaynak:**
  - Kodlar `Assets/_Project/Scripts/DuelV2/` altında.
  - Sahne `Scenes/Dev/DuelPrototypeV2.unity`.
  - Derleme `Tools/build-duelv2.sh`, ölçüm `Tools/bench-duelv2.sh [maç] [çıktı.md]`.
- **Canlı oyun değişmedi.** Prototip, eski düelloyu `DuelCoreV2.Running` ile yalnız kendi sahnesinde durdurur. Mevcut koda dokunan yerler:
  - `GameManager` Update ve LateUpdate: başlarına birer satır eklendi.
  - `DuelStands.LineWidth`: oyunda 1 olarak kalır.
  - `GameManagerDuelV2.cs`: yeni partial dosya.
  - `Loc`: `v2.*` anahtarları eklendi.

## Ne yapıldı (faz faz)

| Faz | Özet |
|---|---|
| 0 | **Teşhis** (`DovusV2_Teshis.md`): nakavt için 50–140 isabet gerekiyordu, sersemleme yoktu, takas sürekli oluyordu, hasar dokunuşta uygulanıyordu ve en iyi strateji spamdi. |
| 1 | **Prototip sahnesi ve araçlar:** menü, F1 canlı ayar paneli (JSON kaydet/yükle), ağır çekim, duraklat/kare kare, günlük/avantaj/kutu araçları, kendini sınama modu. |
| 2 | **Saf kural katmanı `DuelSim`:** saldırının üç evresi, sersemleme/engel/ıska/kusursuz, takas ve ÇARPIŞMA, ritmik zincir (pencere, önbellek, spam koruması, 3'lü setler), GÜÇLÜ ve YERE SER, seri azalması, yüzde hasar, özel, ulti, kalp. |
| 3 | **Kaçış yolları:** set boşluğu ≥150 ms, KIR, kusursuz savunma, kaçış, yerden kalkma dokunulmazlığı, duvardan sekme. |
| 4 | **Raunt sistemi:** giriş, 3-2-1, süre, nakavt, SÜRE DOLDU, ALTIN RAUNT, 2 raunt alan maçı alır, KUSURSUZ / GERİ DÖNÜŞ, maç sonu paneli. |
| 5 | **Elemental yağmurlar:** kar, ateş ve yıldırım. Seriyle ya da kusursuz savunmayla kazanılır. Hedef dairesinden kaçılabilir; son damla yere düşürür. |
| 6 | **Kamera ve anlar:** karakter boyuna göre kadraj, aileye göre vuruş donması, sarsıntı, ağır çekim, nakavt yakın açısı, maç sonu tekrarı, son 10 sn yakınlaşma. |
| 7 | **Platform ve silah:** platform genişliği ×1 / ×1,25 / ×1,5 / ×2. Menzil silahın görünen boyundan hesaplanır (görünen uç = menzil). Görsel örnek: `silahlar_30.png`. |
| 8 | **Düşünen botlar:** 4 kişilik × ustalık, insan gecikmesi. 500 maçlık görüntüsüz ölçümle başlangıç ayarları belirlendi. |
| 9 | **Derleme:** exe, zip, OKUBENI ve bu rapor. Derlemede klavyenin hiç okunmadığı yakalandı ve düzeltildi (aşağıda). |

## Ölçümler (500 bot–bot maçı, kademe 3, `DovusV2_Denge500.md`)

| Ölçü | Sonuç | Hedef |
|---|---|---|
| Raundu bitiren isabet | ortalama **19,1**, ortanca 17 | 15–25 ✓ |
| Spam botunun kazanma oranı | **%8** | < %20 ✓ |
| 3'lü set boşluğu (en kısa) | **150 ms** | ≥ 150 ms ✓ |
| Aile kazanma oranları | Kılıç %51 · Mızrak %59 · Balta %47 · Ağır %43 | dengeli (ilk ölçüm %24–84 arasındaydı) |
| Raunt sonu | nakavt %98 · süre %2 · altın raunt %0 | — |
| Ortalama raunt | 26,5 sn (maç başı 2,33 raunt) | — |
| Maç başı | KIR 6,5 · kusursuz 5,1 · güç 11,5 · özel 8,1 · ulti 0,3 · ıska 9,0 | — |

Teşhisle karşılaştırma: eskiden nakavt için 50–140 isabet gerekiyordu ve spam en iyi stratejiydi. Şimdi yaklaşık 19 isabet yetiyor ve spam %8 kazanıyor.

## Kabul ölçütleri

| Ölçüt | Durum |
|---|---|
| Raunt 15–25 isabette biter | ✓ ölçüldü (19,1) |
| Spam < %20 | ✓ ölçüldü (%8) |
| Her sonuç ekranda ayırt edilir | ✓ İSABET / ENGEL / KARŞILADI / KAÇTI / ISKA / ÇARPIŞMA / DOKUNULMAZ yazıları, renkler ve sesler var |
| Set sonu boşluğu ≥ 150 ms ve görünür | ✓ kural ölçüyor, "BOŞLUK!" ipucu çıkıyor |
| Yağmur işaretli, kaçılabilir, son damla düşürür | ✓ olay kaydında doğrulandı |
| Raunt akışı, süre dolması, altın raunt | ✓ kodda var. Altın raunt 500 maçta hiç oluşmadı (eşitlik ±%1 nadir) |
| Panel canlı değiştirir, kaydeder, yükler | ✓ exe'de denendi: dosya exe'nin yanına yazıldı ve geri okundu |
| Exe kurulumsuz çalışır | ✓ bu PC'de denendi. Başka bir PC'de denenmedi |
| Klavye ve fare | ✓ exe'de Input System üzerinden denendi: 45 dokunuş, 9 isabet, yürüme. Hata kaydı 0 |

## Bilinen sorunlar ve eksikler

1. **Faz 9'da yakalanan hata:** proje yalnız Input System kullanıyor, prototip ise eski `Input` sınıfını okuyordu. Sonuç olarak exe'de tuşlar çalışmıyordu ve saniyede yüzlerce hata yazılıyordu. Yeni `KeysV2` ile düzeltildi; Editor'de bu sorun görünmüyordu.
2. **Güçler sık çıkıyor:** maç başına ~11,5 yağmur ve ~39 isabet eden damla var. Gerekirse `snowSeries`, `fireSeries` ya da yağmur hasarı düşürülebilir.
3. **KIR sık kullanılıyor:** maç başına 6,5 (bot açgözlü olabilir). Daha az görmek için `breakCost` artırılabilir.
4. **Mızrak hafif önde** (%59, Ağır'a karşı %71). Mızrağın `weaponLength` (1,0) ya da `windupMs` (170) değeri oynanarak ayarlanabilir.
5. **Altın raunt** ölçümde hiç oluşmadı; yalnız kodda ve kısa raunt denemesinde görüldü.
6. **Maç sonu tekrarı** görüntü olarak doğrulanmadı (zamanlama düzeltildi, ama ekranda izlenmedi). Kapatmak için F1 → `replay`.
7. **Görünüm:**
   - Bazı ağır silahların başı kafadan büyük.
   - Kalkan boyu değişmedi.
   - Çömelmenin kural etkisi yok.
   - Platform ×2 genişlikte doku kontrol edilmedi.
8. **Arayüz:**
   - Arayüz IMGUI ile yazıldı (prototip için); oyuna taşınırken uGUI'ye geçecek.
   - Metinler yalnız İngilizce ve Türkçe.
   - Dokunmatik düğmeler fareyle denendi, telefonda denenmedi.
9. **Otomatik tuş testi:** Windows'tan dışarıdan gönderilen (SendKeys) tuşları Input System görmedi. Deneme, Input System'e içeriden gönderilen tuşlarla (`-v2Keys`) yapıldı. Gerçek klavyede çalışması beklenir, ama elle denenmesi gerekir.

## Nasıl ayarlanır

- **Canlı ayar:** Oyunda F1 paneli açar. Değişiklik anında uygulanır.
  - Ağır çekim (F2) ve kare kare ilerletme (F3 / F4), süreleri gözle görmek için.
  - Vuruş günlüğü (F5) her vuruşun sonucunu ve sebebini gösterir.
  - Avantaj çubuğu (F6) sıranın kimde olduğunu gösterir.
  - Kutular (F7) menzili gösterir.
- **Değerleri saklama:** Beğendiğiniz değerleri "Kaydet" ile JSON'a yazın ve dosyayı bana gönderin. Değerleri kodun varsayılanlarına işleyip ölçümü (`bench-duelv2.sh`) yeniden çalıştırırım.
- **Ayar başlıkları:**

| Başlık | Ne ayarlar |
|---|---|
| Tempo | `tempo` (bütün süreler) |
| Aile tablosu | `windupMs` / `activeMs` / `recoveryMs` / `hitStunMs` / `blockPushMs` / `blowPct` / `weaponLength` (dizi sırası: kılıç, mızrak, balta, ağır) |
| Zincir | `chainLength`, `chainOpenMs`, `bufferMs`, `spamTaps`, `lastHitStunMs`, `seriesDecay` |
| Raunt | `roundSeconds`, `roundsToWin` |
| Kamera | `camNear` / `camFar` / `camBand` / `camFeetAt` / `camPitch` |

## Sonraki adım (onayınızı bekliyor)

**Faz 10 yapılmadı:** oyuna taşıma, sunucu tarafı kural doğrulaması, uGUI, 8 dil, ödüller ve görevler. Sizin oynayıp ayarlamanızdan ve onayınızdan sonra başlar.
