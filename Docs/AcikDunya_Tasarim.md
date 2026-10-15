# foi | cell — Açık Dünya: "Kızıl Çöl" Tasarım Belgesi

Sürüm 1 · 2026-10-15 · Kullanıcıyla kararlaştırılanlar + önerilenler

---

## 1. Özet

250. bölümün sonunda foi kaçışını tamamlar ve **Kızıl Çöl**'e, Mars'ı andıran, hurda yığınlarıyla dolu terk edilmiş bir savaş alanına çıkar. Burası **tek oyunculu, kalıcı ve gerçek zamanlı** bir dünyadır:

- **Çiftlik döngüsü (sakin, sürekli giriş):** ev, tarla, ahır, hayvan → hasat, süt, yumurta → fazlası kooperatife → para → gelişim.
- **Savaş döngüsü (aksiyon):** haydutlar bölgeleri ele geçirip yayılır → foi kurtarma operasyonları yapar, çiftliğini top, kule, sur ve askerle korur.

Gerçek oyuncu yoktur. Komşu çiftlikler yapay zekâ çiftçilerdir. Düşmanlar **haydutlar**dır.

---

## 2. Giriş ve para

| Konu | Kural |
|---|---|
| Açılma | 250. bölüm kazanılınca "Özgürlük" sahnesi → Kızıl Çöl açılır, menüde **AÇIK DÜNYA** butonu belirir |
| İlk giriş | Tüm altınlar **paraya** çevrilir: **10 altın = 1 para** (tek sefer, geri dönüş yok) |
| Sonradan kazanılan altın | Bölüm tekrarı, düello ve serbest savaştan gelen altın açık dünyaya **4'te 1 değerle** taşınır: **40 altın = 1 para** (açık dünya kapısında "Altını taşı" butonu) |
| Para birimi | Yalnızca açık dünyada geçer; atölyede (altın dükkânı) kullanılmaz |
| Eski modlar | Bölümler, düello, serbest savaş, ordu ekranı açık kalır |

---

## 3. Harita

- **7 × 7 = 49 bölge**, her bölge 48 × 48 kare. Başlangıç bölgesi ortada.
- Bölge durumları:
  - 🟩 **Güvenli:** yapay zekâ çiftlikleri, kooperatif, akın yok.
  - 🟨 **Sınır:** çiftlik kurulabilir, akın alabilir.
  - 🟥 **Haydut:** kamp, sur, kule; çiftlik kurulamaz.
- Başlangıç: merkez + 4 komşu güvenli, 12 sınır, kalan 32 haydut.
- **İki kuşak:**
  - **Varış çölü (merkez):** foi'nin çıktığı yer. Kızıl kum, hurda yığınları, robot hurdaları, kırık kuleler, kum fırtınası, iki ay. Burada çiftlik kurulmaz; kooperatif merkezi ve yol kavşağı burada.
  - **Kırsal kuşak (çölden uzaklaştıkça):** kum yerini toprağa ve çimene bırakır, çalılar, dereler, ağaçlar. **Çiftlikler burada kurulur:** tarla, ağaç dikimi, ahır.
  - Geçiş yavaş: çöl kenarında kum ile çimen karışık, en dıştaki bölgeler en yeşili.
- Haydut bölgeleri iki kuşakta da olabilir; çöldekiler hurda kalelerinde, kırsaldakiler ele geçirilmiş çiftliklerde.
- **Teknik:** sadece foi'nin bulunduğu ve komşu bölgeler yüklü (bölge akışı), uzak bölgeler basit düşük detaylı kabuk. Kaya, hurda ve ekin örnekli (instanced) çizim. Hedef: eski Android'de 30 FPS, yenilerde 60.
- **Kamera:** TPS (yatay) veya izometrik (dikey), oyuncu istediği an değiştirir. Oyunda ikisi de zaten var.

---

## 4. Çiftlik

### 4.1 Ev ve arazi

| Ev | Fiyat (para) | Arazi | Tarla sınırı | Ahır sınırı |
|---|---|---|---|---|
| Çadır | 0 (başlangıç) | 12×12 | 4 | — |
| Kulübe | 300 | 16×16 | 8 | 1 |
| Ev | 1.500 | 20×20 | 14 | 2 |
| Çiftlik evi | 6.000 | 26×26 | 22 | 3 |
| Malikâne | 20.000 | 32×32 | 32 | 4 |

### 4.2 Ürünler (gerçek zamanlı büyür)

| Ürün | Tohum | Süre | Hasat | Kooperatif fiyatı | Açılış |
|---|---|---|---|---|---|
| Buğday | 1 | 2 dk | 3 | 1 | başlangıç |
| Mısır | 3 | 10 dk | 3 | 3 | başlangıç |
| Havuç | 6 | 30 dk | 3 | 6 | Kulübe |
| Domates | 12 | 1 sa | 3 | 12 | Kulübe |
| Ayçiçeği | 25 | 3 sa | 3 | 28 | Ev |
| Kaktüs meyvesi | 50 | 8 sa | 4 | 45 | Çiftlik evi |
| Kızıl balkabağı | 90 | 12 sa | 4 | 80 | Malikâne |

### 4.2b Ağaçlar (meyve bahçesi)

Kırsal kuşakta dikilir, tarladan yavaş ama kalıcıdır: bir kez dikilir, her süre dolunca yeniden ürün verir.

| Ağaç | Fidan | Büyüme (ilk ürün) | Ürün / süre | Kooperatif fiyatı | Açılış |
|---|---|---|---|---|---|
| Elma | 60 | 6 sa | Elma ×4 / 3 sa | 9 | Kulübe |
| Portakal | 120 | 10 sa | Portakal ×4 / 5 sa | 16 | Ev |
| Zeytin | 250 | 18 sa | Zeytin ×6 / 8 sa | 22 | Çiftlik evi |
| Ceviz | 400 | 24 sa | Ceviz ×6 / 12 sa | 35 | Malikâne |

Ağaçlar ayrıca gölge verir: yanlarındaki tarlalar %10 hızlı büyür. Haydutlar ağaçları kesmez ama meyvesini yağmalar.

### 4.3 Hayvanlar

| Hayvan | Fiyat | Bina | Yem | Ürün / süre | Kooperatif fiyatı |
|---|---|---|---|---|---|
| Tavuk | 40 | Kümes (150) | 1 buğday | Yumurta / 20 dk | 4 |
| İnek | 250 | Ahır (600) | 2 mısır | Süt / 1 sa (sağma) | 22 |
| Koyun | 400 | Ağıl (900) | 2 havuç | Yün / 3 sa (kırkma) | 50 |
| Keçi | 600 | Ahır | 1 domates | Keçi sütü / 2 sa | 45 |
| Arı kovanı | 800 | — | — (ayçiçeği yakınında hızlanır) | Bal / 4 sa | 70 |

- Hayvan beslenmezse ürün vermez (ölmez).
- Sağma ve kırkma kısa dokunmatik eylemlerdir (birkaç saniyelik animasyon).

### 4.4 Ambar

Ürünler ambarda durur. Ambar kapasitesi 50 / 120 / 250 / 500, yükseltilebilir. Dolarsa hasat yapılamaz.

---

## 5. Kooperatif

- Güvenli bölgelerde. Fiyatlar tablodaki taban fiyat.
- **Günlük siparişler:** 3 sipariş, taban fiyatın 1,5 katı. Örnek: "20 süt + 10 yumurta → 600 para".
- **Talep:** aynı ürün çok satılırsa fiyatı o gün %20'ye kadar düşer, ertesi gün toparlanır.

---

## 6. Ordu

- Ordu ekranındaki 6 birlik (hafif/ağır okçu, kılıççı, mızrakçı) ve güç/vuruş/zırh gelişimleri burada da geçerli.
- **Asker sayısı parayla artırılır:** bir asker 50 para, birlik başına en fazla 30 + ev seviyesi × 10.
- **Askerler çiftlikten beslenir:** asker başına günde 1 buğday + 1 süt ya da yumurta. Yemek yoksa güçleri %40 düşer.
- Görevler: **nöbet** (çiftlikte, akında savaşır) veya **operasyon** (foi ile gider).

---

## 7. Savunma

| Yapı | Fiyat | Özellik |
|---|---|---|
| Ahşap çit | 5 / kare | Yavaşlatır, kolay kırılır |
| Taş sur | 20 / kare | Dayanıklı, toplarla yıkılabilir |
| Kapı | 80 | Askerler geçer, haydutlar kırmaya çalışır |
| Okçu kulesi | 400 | Hızlı atış, tek hedef, menzil 7 |
| Ateş kulesi | 1.200 | Alev, alan hasarı, menzil 5 |
| **Top** | 900 | **İstenen noktaya kurulur.** Uzun menzil (12), alan hasarı. Surları, evleri ve haydutları vurur. Kurtarma operasyonunda haydut kampını döver |
| Ağır top | 3.000 | Menzil 15, sur yıkıcı |
| Gözetleme kulesi | 600 | Akını 30 dk erken haber verir, kulelerin menzili +2 |

Tüm yapıların 3 seviyesi vardır (hasar ve dayanıklılık +%40 / seviye). Yapılar akında hasar alır; onarım ücretlidir.

---

## 8. Haydutlar

| Tür | Özellik |
|---|---|
| Yağmacı | Hızlı, tarla ve ambarı soyar |
| Haydut okçu | Uzaktan, kulelere ateş eder |
| Bombacı | Surlara koşar, patlar |
| Kuşatma arabası | Yavaş, surları ve kapıları kırar |
| Haydut topçusu | Kampta top kullanır |
| Haydut beyi | Bölge patronu; ölünce bölge kurtulur |

Haydut güçleri bölgeye göre artar: merkezden uzaklaştıkça daha güçlü.

---

## 9. Yayılma ve akın

### 9.1 Yayılma

- Her **6 saatte** bir, kurtarılmamış her haydut bölgesi komşu bir **sınır** bölgesini ele geçirmeye çalışır.
- Şans %25; bölgede foi'nin kulesi veya askeri varsa %10.
- Ele geçirilen sınır bölgesindeki çiftlik **akın** alır.

### 9.2 Akın

| Kural | Değer |
|---|---|
| Uyarı | Akından **2 saat önce** bildirim ("Haydutlar çiftliğine yaklaşıyor!"); gözetleme kulesi varsa 2,5 saat |
| Sıklık sınırı | Günde en fazla **2** akın |
| Koruma | Her akından sonra **6 saat** dokunulmazlık |
| Akın gücü | Bölgenin haydut seviyesi × çiftliğin değeri |

**Çevrimiçiyken akın başlarsa** oyuncu seçer:

1. **Kal ve savaş:** akın canlı oynanır. foi + askerler + kuleler + toplar haydutlara karşı.
2. **Sat ve taşın:** her şey (ev, yapılar, hayvanlar, ürün) **değerinin yarısına** satılır. foi seçtiği bir güvenli bölgede baştan kurar.

**Çevrimdışıyken akın olursa:** sunucu, kuleler + toplar + nöbetçi askerler ile haydutlar arasında savaşı hesaplar. Oyuncu girişte bir **akın raporu** görür: "Gece 14 haydut saldırdı, 9'u öldü. 2 tarla yağmalandı, okçu kulesi hasarlı."

**Kayıp (bölge kaybı):**
- Haydutlar kazanırsa tarlalardaki ürünler ve ambarın %30'u yağmalanır.
- Yapılar hasar alır (yıkılmaz, onarılır).
- Bölge 🟥 haydut olur.
- Çiftlik yerinde kalır ama yeniden akın almaya devam eder. Oyuncu bölgeyi geri alana ya da taşınana dek yeni tarla kuramaz.

---

## 10. Kurtarma operasyonları

- Haydut bölgesine gidilir. Hedef: kamp surları, kuleler, haydut topları ve sonunda **haydut beyi**.
- foi + operasyon askerleri + kurulan toplar ile savaş.
- Kazanınca:
  - Bölge 🟨 sınır olur.
  - Ödül: 100–2.000 para.
  - Bölgede yeni ürün ve hayvan türleri açılır.
  - Haydut beyi ganimeti: nadir eşya (süs, top kaplaması).
- Zorluk merkezden uzaklaştıkça artar. Son bölge: **Haydut Kalesi** (açık dünyanın büyük patronu).

---

## 11. Açılış sahnesi (oyun başlarken)

Oyun motorunda kodla oynatılır (video dosyası değil). Süre ~22 sn, **"Geç" butonu** var. **Sadece ilk açılışta tam** oynar; sonraki açılışlarda logodan sonra 3 sn'lik kısa sürüm. Ayarlarda "Açılış sahnesini izle" seçeneği bulunur.

| Saniye | Kare |
|---|---|
| 0–3 | Karanlık; cam hücrede uyuyan foi, gözleri yanar |
| 3–6 | Hücre camı çatlar, kırılır; foi dışarı fırlar |
| 6–14 | Hızlı kesitler: kale sahnesinde robotlara vuruş, köprüden geçiş, vanG'nin kükremesi ve yenilmesi, tünelde kayış |
| 14–18 | Son kapı açılır, ışık patlar |
| 18–22 | Kızıl Çöl: hurda yığınları, iki ay, foi ufka bakar; "foi CELL" logosu |

Haritada 250. bölümden sonra çöle uzanan geniş bir **Kızıl Çöl** alanı ve kapıdan geçiş animasyonu yer alır.

---

## 12. Teknik notlar

- **Gerçek zamanlı süreler:** tarla, hayvan, akın ve yayılma zamanları **sunucu saatine** göre tutulur (telefon saati değiştirilerek hile yapılamaz). Sunucuya küçük bir "saat + kayıt" uç noktası eklenir.
- **Kayıt:** dünya durumu telefonda ve sunucuda yedekli (telefon değişince kaybolmaz).
- **Çevrimdışı akın:** sunucuda hesaplanır, oyuncu girince rapor gelir.
- **Bildirimler:** hasat hazır / akın uyarısı / akın raporu. Telefonda yerel bildirim paketi (Unity Mobile Notifications) gerekir. İndirmeden önce onay istenecek.
- **Performans:** bölge akışı, örnekli çizim, uzak bölgeler için düşük detay, hurda ve kaya için ortak malzemeler.

---

## 13. Geliştirme aşamaları

| Aşama | İçerik | Sonuç |
|---|---|---|
| **A** | Açılış sahnesi + haritanın sonuna Kızıl Çöl alanı ve geçiş | Oyuna hemen eklenebilir |
| **B** | Açık dünya çekirdeği: harita, bölgeler, kamera, ev, tarla, hayvan, ambar, kooperatif, para dönüşümü | Çiftlik oynanabilir |
| **C** | Savunma: çit, sur, kule, top; haydut türleri; çevrimiçi akın ("kal ve savaş / sat ve taşın") | Savaş oynanabilir |
| **D** | Sunucu saati ve kayıt, çevrimdışı akın hesabı, akın raporu, bildirimler | Gerçek zamanlı dünya |
| **E** | Yayılma, kurtarma operasyonları, haydut beyleri, Haydut Kalesi | Tam döngü |
| **F** | Denge, 8 dile çeviri, test, mağaza görselleri | Yayın |

Her aşamadan sonra test ve senin onayın; sonra sıradaki aşama.
