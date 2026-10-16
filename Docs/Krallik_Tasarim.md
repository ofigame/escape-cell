# foi'nin Krallığı — Tasarım (Age of Empires IV tarzı)

Açık dünyanın yerini alır. Bölümler (dikey) ile krallık (yatay) birbirini besler.

## Kesinleşen kararlar
- **Açılış:** 10. bölüm. foi yıkık bir köy bulur; kısa öğretici (saray, köylü, ilk kaynaklar).
- **Ekran:** krallık yatay; bölümler dikey kalır.
- **Kamera:** AoE4 gibi serbest RTS kamerası. Eğik bakış (~55°), varsayılan kuzey, sınırlı yakınlaştırma; tek parmak kaydırır, iki parmak yakınlaştırır/döndürür. Kamera kimseyi takip etmez.
- **foi:** sarayda tahtında oturur. TPS düğmesiyle kalkıp krallıkta dolaşır (gezinti modu; RTS komutları kapalı).
- **Ekonomi (AoE4):** yemek, odun, taş, altın. Köylüler toplar ve en yakın teslim noktasına taşır (saray, değirmen, kereste kampı, maden kampı). Böğürtlen → koyun/av → tarlalar (sınırsız, değirmen çevresi bonus). Evler nüfus açar.
- **İnşa (AoE4):** yeşil/kırmızı hayalet ile yerleştirme, iskele, köylüler inşa eder (çok köylü = hızlı). Sur sürükleyerek çizilir (çit → taş sur), kapı, kule, kale (Keep).
- **Ordu (AoE4):** kışla, okçu alanı, ahır, atölye; üretim kuyruğu, toplanma bayrağı; seçme (dokun, kutu, çift dokun = aynı tür, "tüm ordu", gruplar), saldırarak ilerle; mızrakçı > süvari > okçu > mızrakçı; kuşatma olmadan sur yıkılmaz.
- **Çağlar:** I Karanlık → II Feodal → III Kale → IV İmparatorluk; her çağda iki anıttan biri seçilir (bölümlere de farklı bonus verir).
- **Ortak asker havuzu:** askerler yalnızca krallıkta eğitilir; bölümlere krallıktan ordu seçilerek girilir; bölümde ölen asker ölür.
- **İki yönlü bonus:** krallık binaları bölümlerde küçük bonuslar verir (demirci: vuruş, manastır: ek kalp, pazar: altın, kışla: hazır asker).
- **Bağımsız ekonomi:** krallık kendi üretir; bölümler kolay para kazandırır; **yeni dünya = krallıkta bölge kilidi + büyük hediye** (anıt parçası, plan, büyük sandık, özel asker).
- **Rakipler gerçek oyuncular (eşzamansız):** rakibin krallığı birebir görünür; savunmasını yapay zekâ oynar. Saldıran ganimeti hemen alır; savunanın ekranında saldırının kaydı oyuna girince oynatılır ve sonuç o an işlenir. Ganimet sınırı, koruma kalkanı, eşleştirme, intikam, sunucu kontrolü.
- **Çevrimdışı:** krallık yalnızca kendi gelişir (üretim, inşaat, eğitim süreleri işler).

## Aşamalar
1. **İskelet:** yatay mod, RTS kamerası, arazi + bölge kilitleri (dünya → bölge), krallık kaydı (`sb_km`), saray + taht + foi, kaynak çubuğu, 10. bölüm açılışı.
2. **Ekonomi:** kaynak düğümleri (orman, çalı, taş, altın), köylüler, seçme/emir, yol bulma, topla-taşı.
3. **İnşa:** bina menüsü, hayalet yerleştirme, inşaat, ev/nüfus, kamplar, değirmen, tarlalar, çit ve sur.
4. **Ordu:** askerî binalar, kuyruk, ortak asker havuzu, savaş, haydut kampları (PvE).
5. **Çağlar ve ödüller:** anıtlar, bölüm/dünya ödülleri, bölümlere bonuslar.
6. **TPS:** tahttan kalkıp dolaşma.
7. **Oyuncuya karşı oyuncu:** krallık kopyaları, rakip bul, saldırı, kayıt oynatma, kalkan.

## Açık sorular (sonra karar verilecek)
1. Haydutlar: kurtarılacak PvE bölgeler olarak kalsın, çevrimdışı haydut baskınları kalksın mı?
2. Rakip seçimi: listeden mi, "rakip bul" ile mi?
3. Kupa/lig: düello kupalarıyla birleşsin mi?
4. Saldırıda oyuncu askerleri kendisi mi indirir, yoksa yalnızca izler mi?
5. Şifahane/manastır ağır yaralıları kurtarsın mı?
