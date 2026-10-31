# Dövüş v2 — Faz 10 raporu (oyuna taşıma)

Dal `dovus-cekirdegi-v2` · iOS test derlemesi **appstore-1.0-b43** (test kilidi açık: her şey açık) · İlerleme: `DovusV2_Ilerleme.md` (10a–10g)

## Ne değişti

| Alan | Şimdi |
|---|---|
| Bilgisayara karşı düello | v2 kuralları: raunt sistemi (2 raunt alan kazanır), ritmik zincir, yüzde hasar, elemental yağmurlar, v2 botu (oyunun bot ayarından ustalık ve kişilik), 1,5 kat geniş platform, v2 kamerası ve sesleri |
| Canlı düello | Sunucu v2 kurallarını koşturur (protokol 10). Oyunlar girdilerini gönderir, sunucunun durumunu gösterir. Sizin foi'niz her zaman solda |
| Ekran | Yeni uGUI düello ekranı. Dokun = vur, kaydır = yürü, yukarı = zıpla, aşağı = kaç. Düğmeler: VUR, KALKAN (basılı tut), ÖZEL, ULTİ, KIR/GÜÇLÜ/YERE SER, KALP, güç 1–3, duraklat. 8 dil |
| Ödüller | Raunt skoru eski "nakavt skoru"nun yerinde (2–0, 2–1). Görevler olaylardan sayılır: 3'lü set = kombo; YERE SER = bitiriş; kusursuz savunma, kaçılan ulti, özel, ulti, kalp. Geri dönüş, altın raunt bandı, kusursuz raunt başına +25 XP |
| Haftalık / turnuva | Ani Ölüm: tek raunt, 30 sn. Hızlı Ulti: çubuk 2. Kalpsiz. Kalkan Savaşı: ×2 dayanıklılık. Kombo Çılgınlığı. Turnuvada raunt 45 sn |
| İlk düello (öğretici) | Çok kolay rakip, ekranda sırayla 4 ipucu |

## Sunucu güncellemesi (sizin yüklemeniz gerekiyor)

Paket: `OfigameWeb/publish/foi-api-v2.zip`. İçinde `Ofigame.FoiApi.dll` ve `duel_rules.json` var.

1. Plesk'te foi-api klasörüne bu iki dosyayı yükleyin (eskilerin üstüne).
2. Uygulamayı yeniden başlatın.

**Dikkat:** sunucu güncellenince mağazadaki sürümler (iOS b42, Play 26101250) canlı rakip bulamaz. Bu sürümler "güncelleme gerekli" yanıtı alır ve bilgisayara karşı oynar; oyun bozulmaz. Yeni sürüm sunucu güncellenmeden de çalışır: eski sunucu v2 işareti göndermediği için canlı maçı bırakır, bilgisayara karşı oynar. Seçenekler:

- **Canlı maçı iPhone'da denemek için:** sunucuyu şimdi güncelleyin. Mağaza oyuncuları bir süre yalnız bilgisayara karşı oynar.
- **Mağaza oyuncularını etkilememek için:** sunucuyu yeni sürümlerle aynı gün güncelleyin. iPhone testi o zamana kadar bilgisayara karşı olur.

## Ölçümler

- **500 bot maçı:** raundu bitiren isabet 19,4; aileler %40–59; spam botu %4.
- **Oyunun kendi akışı bilgisayara karşı** (kendini sınama `-v2Game`): menü → rakip bulma → sahne çarkı → v2 maç → sonuç ekranı (YENİLGİ 0–2, XP, ustalık, sezon yolu). Hata yok.
- **Canlı, yerel sunucuda** (test oyuncusuna karşı):
  - Protokol 9 geri çevrildi.
  - Eşleşme, başlama, saniyede 20 durum oldu; durum ~550 bayt, saniyede ~11 KB (1 dakikalık maç ~0,6 MB).
  - 38 isabet / 10 engel sayıldı.
  - Sonuç iki tarafa ters gitti, kupa işlendi.
  - Oyun canlı maçı 2–0 bitirip ödül ekranına geçti.

## Bilinen eksikler

1. **Antrenman odası ve geliştirici laboratuvarı eski çekirdekte.** Kendi panelleri eski hareket setine bağlı.
2. **Canlı maçta kusursuz savunma daha zor.** Gecikme payı yalnız zincir penceresine verildi (gidiş-dönüş süresi + 25 ms, en çok 200 ms).
3. **Bazı şeyler gerçek cihazda denenmedi:**
   - Telefonda dokunma ve çoklu parmak (kalkanı tutarken vurmak).
   - Ekran yerleşimi farklı oranlarda.
   - Gerçek internet gecikmesi.
4. **Eski sunucu odası (protokol 9) dosyada duruyor** ama kullanılmıyor. Silmek için sizden onay bekliyorum.
5. **Android (Play / Huawei):** siz iOS'ta denedikten sonra.
