# foi — Düello Dövüş Çekirdeği v2 (10. dosya)

> **Bu dosya kimin için:** Claude Code.
> **Sorun:** Düelloda kimin nasıl vurduğu, darbe alıp almadığı anlaşılmıyor; dövüş zevk vermiyor. Animasyon, efekt, kamera ve çarpışma düzeltmeleri görüntüyü iyileştirdi ama **dövüşün kuralı** "sıra kimde, kim avantajda" sorusuna cevap vermiyor.
> **Amaç:** Mortal Kombat hissi: **ne yaptığını bilen saldıran, darbe aldığını bilen ve ona göre tepki veren savunan, seri saldırıdan kaçış fırsatları, kusursuz kurgulanmış kamera ve ağır çekimler.**
> **Yöntem:** Önce ayrı bir **test sahnesinde prototip**; Windows **.exe** olarak derlenip oyun sahibine verilir; o oynayıp ayarlayıp onaylayınca oyuna taşınır. Prototip aşamasında **canlı oyunun koduna dokunulmaz** (yeni çekirdek bir bayrakla kapalı durur).
>
> Çalışma kuralları aynı: public API'yi koru, her `.cs` dosyasında tek tip, bütün sayılar veriden (bu dosyada çok önemli: oyun sahibi değerleri prototipte canlı ayarlayacak), metinler yerelleştirme anahtarıyla. Ayrı dal: `dovus-cekirdegi-v2`; her faz ayrı commit.

---

## 0. Alınan kararlar

| # | Karar |
|---|---|
| K1 | **Raunt sistemi:** iki raunt kazanan maçı kazanır (en fazla 3 raunt). |
| K2 | **Tempo:** "kıvamında": ne hızlı ne yavaş. Normal vuruşlar akıcı, büyük anlar (güçlü vuruş, güçler, ulti, nakavt) yavaşlatılarak vurgulanır. Bütün süreler veriden; prototipte ayarlanır. |
| K3 | **Ritmik zincir:** doğru anda dokunmak seriyi sürdürür; ekrana boşuna basmak seriyi bitirir. |
| K4 | **Elemental güçler** (kar topu, ateş topu, yıldırım): seri saldırı ve kusursuz savunmayla kazanılır, biriktirilebilir, kullanılınca rakibe **gökten seri hâlinde yağar.** Kaynakları: Bölüm 5.1. |
| K5 | **Birleşik fırtına yok.** |
| K6 | **Platformlar enine 1,5 kat** (önce bu denenir). |
| K7 | **Silahlar karakterin boyuyla orantılı;** görünen boy menzili anlatır. |
| K8 | **Önce test sahnesi + .exe**; oyuna taşıma onaydan sonra. Canlı maç (sunucu) oyuna taşıma aşamasında. |

### Bu dosyanın değiştirdiği eski kurallar (oyuna taşınırken)
- 3 dakika + nakavt sayısı → **raunt sistemi** (Bölüm 3).
- ALTIN NAKAVT uzatması (3. dosya İş 12) → **ALTIN RAUNT** (3.2).
- Aile × kademe darbe tablosu ve 0,12–0,30 sn bekleme süreleri → **hareket süreleri ve yüzde hasar** (Bölüm 2).
- Kombo süresi 1,4 sn, GÜÇLÜ 5'li / YERE SER 8'li → **ritmik zincir** (2.4).
- Ultilerin ve özel hareketlerin "normal darbenin N katı" hasarı → **can yüzdesi** (2.6).
Savunma sistemi (engelleme, kusursuz savunma, kaçış, KIR), hareket seti, lig tavanı, parçalar ve ödüller **aynen kalır.**

---

## 1. Teşhis (Faz 0'da doğrula ve raporla)

Önce bugünkü dövüşü ölç ve `Docs/DovusV2_Teshis.md` dosyasına yaz:
1. Ortalama bir nakavt için kaç isabet gerekiyor (aynı kademede, her aile için)? (Rehbere göre ~180 isabet: her vuruş can çubuğunda görünmüyor.)
2. Vurulan taraf vurulurken saldırabiliyor ve hareket edebiliyor mu? (Sersemleme süresi var mı, ne kadar?)
3. İki tarafın aynı anda birbirine vurduğu "takas" anları bir maçta kaç kez oluyor?
4. Saldırının hazırlık / vuruş / toparlanma evreleri kuralda var mı, yoksa dokunuş anında mı hasar oluyor?
5. Ekrana durmadan basan bir "spam botu", düşünerek oynayan bota karşı yüzde kaç kazanıyor? (Hedef: v2'de belirgin şekilde kaybetmeli.)

---

## 2. Dövüş çekirdeği

### 2.1 Saldırının evreleri (kural, yalnız görsel değil)
Her saldırının üç evresi **oyun kuralıdır** (istemci ve ileride sunucu aynı tabloyu kullanır):
- **Hazırlık:** saldırı başladı, henüz vurmuyor. Rakip bunu görüp tepki verebilir.
- **Vuruş:** bu sürede menzildeki rakibe isabet eder.
- **Toparlanma:** saldıran savunmasız; yeni bir şey yapamaz (zincir penceresi hariç, 2.4).

### 2.2 Hareket süreleri (başlangıç değerleri, ms)

| Aile | Hazırlık | Vuruş | Toparlanma | Toplam | İsabette rakibin sersemlemesi | Engellemede rakibin geri itilmesi |
|---|---|---|---|---|---|---|
| Kılıç | 90 | 50 | 160 | 300 | 260 | 180 |
| Mızrak | 120 | 60 | 200 | 380 | 290 | 190 |
| Balta | 160 | 70 | 240 | 470 | 330 | 200 |
| Ağır | 190 | 80 | 280 | 550 | 360 | 210 |

- Iskalanan saldırının toparlanması **+120 ms** (ceza fırsatı).
- Bütün değerler `DuelCoreSettings` (ScriptableObject) içinde; prototipte canlı ayarlanabilir (Bölüm 9).
- **Gecikme kuralı:** Rakibin tepki verebileceği hiçbir pencere **150 ms'den kısa olamaz** (ileride canlı maçta internet gecikmesine dayanıklılık için).

### 2.3 Avantaj sistemi: sıra kimde?

| Sonuç | Ne olur | Sıra |
|---|---|---|
| **İsabet** | Rakip sersemler (tabloda); bu sürede hareket edemez, saldıramaz, savunamaz. Saldıranın toparlanması rakibin sersemlemesinden kısa → saldıran zincire devam edebilir | **Saldıranda** |
| **Engellendi** | Rakip kısa geri itilir; saldıranın toparlanması rakibin geri itilmesinden uzun | **Savunana geçer** (yaklaşık 100 ms avantaj) |
| **Iskaladı** | Saldıranın uzun toparlanması | **Savunanda** (cezalandırma fırsatı) |
| **Kusursuz savunma** | Saldıran 500 ms sendeler (mevcut kural) | **Savunanda** (büyük fırsat) |
| **Takas** (aynı anda iki isabet) | Kuralla engellenir: vuruş evresine **önce giren** kazanır; aynı karedeyse ikisi de hafif sendeler ve "ÇARPIŞMA" efekti (silah çarpışması) | — |

Oyuncu sıranın kimde olduğunu **görmeli**: isabet alan sersemlerken üstünde kısa bir sersemleme parıltısı, avantajlı tarafın silahında hafif bir parıltı (prototipte açılıp kapatılabilir ipucu).

### 2.4 Ritmik zincir

- Bir isabetten sonra, saldıranın vuruş evresinin bitişinden **60 ms sonra** açılan ve toparlanma sonuna kadar süren bir **zincir penceresi** vardır. Bu pencerede yapılan dokunuş, toparlanmayı keserek zincirin sonraki vuruşunu başlatır. (Sonraki vuruşun hazırlığı, rakibin sersemlemesi bitmeden vuracak şekilde ayarlıdır → zincir rakip tarafından kesilemez.)
- **Girdi önbelleği:** pencere açılmadan önceki son **120 ms** içinde yapılan **tek** dokunuş saklanır ve pencere açılınca çalışır (8. dosya 3.3).
- **Spam koruması:** Bir vuruş sırasında pencere dışında **2 veya daha fazla** dokunuş yapılırsa zincir o vuruştan sonra **biter** (karakter toparlanmayı tamamlar). Ekranda saldıranın silahında küçük bir "kırılma" titreşimi.
- **Ritim işareti:** Zincir penceresi açıldığı an silah ucunda kısa bir parıltı ve (ayardan açılabilir) hafif titreşim. Oyuncu ritmi öğrenir.
- **Zincir yapısı ve kaçış boşluğu:**
  - Zincir **3 vuruşluk** setlerden oluşur.
  - **3. vuruştan sonra** rakibin sersemlemesi kısalır (180 ms) ve zincir penceresi kapanır. Saldıranın yeni bir vuruşu rakip toparlandıktan sonra gelir → rakibin **en az 150 ms'lik bir boşluğu** olur: kalkan, kaçış, KIR ya da kendi saldırısı.
  - 3. vuruştan sonra saldıranın seçenekleri: **yeni zincir başlatmak** (rakip boşlukta kaçabilir), **GÜÇLÜ** (beliren düğme, zincir bitirici, rakibi savurur), serinin 6. isabetinde **YERE SER** (beliren düğme, havaya kaldırır; 4. dosya 4.4 havada vurma kuralları).
- **Seri:** Engellenmeden art arda gelen isabetlerin sayısı (zincirler arasında 1,2 sn'den uzun boşluk olursa seri sıfırlanır). Seri ekranda büyük sayıyla gösterilir ("3 SERİ!", "6 SERİ!").
- **Seri hasar azalması:** Bir serideki her isabetin hasarı bir öncekinin **%90'ı**, en düşük %50. Uzun seri sonsuz hasar vermez.

### 2.5 Hasar dengesi

**Hedef:** Aynı kademede bir raunt **15–25 isabetle** biter; her normal isabet can çubuğunda görünür bir parça götürür.

- Hasar, savunanın lig kademesine ait **referans cana** (o kademenin tavan düello canı) oranla tanımlanır. Böylece Gövde parçası yine önemlidir (gerçek can referanstan yüksekse daha çok dayanır).

| Saldırı | Referans canın yüzdesi |
|---|---|
| Kılıç normal vuruş | %4,0 |
| Mızrak normal vuruş | %4,8 |
| Balta normal vuruş | %6,0 |
| Ağır normal vuruş | %6,6 |
| GÜÇLÜ | %10 |
| YERE SER | %12 (+ havada vurma) |
| Özel hareketler (hasar verenler) | %6–9 (hareket başına tabloda) |
| Elemental yağmur (toplam) | %13 |
| Ulti | %22–28 (ulti başına tabloda) |

- Kollar bonusu, Kabuk zırhı, savunma, seri azalması, pasifler bu değerlerin üstüne mevcut kurallarla uygulanır.
- Kalp: canın %30'u, **maç başına 3** (raunt başına yenilenmez; oyuncu raunda dağıtır). Rakibin kalan kalpleri görünür (9. dosya 3.2).
- Hasar sayıları büyük ve okunaklı; can çubuğunda "hayalet çubuk" (kaybedilen kısım 0,4 sn sonra erir).

### 2.6 Hareketlerin yeri

| Katman | Hareketler | Kaynak |
|---|---|---|
| Zincir | Dokunarak 3'lü setler (aileye göre vuruşlar, 5. dosya animasyon ataması) | Sınırsız, ritimle |
| Beliren düğme | GÜÇLÜ (3. vuruştan sonra), YERE SER (6 seri), KIR (rakip zincirdeyken, 1 parça güç) | Zincirden |
| Özel hareket | ÖZEL düğmesi; bekleme süreli (9. dosya Bölüm 4) | Bekleme süresi |
| Elemental güçler | Kar topu, ateş topu, yıldırım (Bölüm 5) | Seri ve kusursuz savunma |
| Ulti | ULTİ düğmesi | Güç çubuğu (3 parça) |

---

## 3. Raunt sistemi

### 3.1 Akış
1. **Maç girişi:** iki foi, isimler, rütbe ve taç; "RAUNT 1".
2. **Raunt başı:** iki foi başlangıç noktalarında, tam can, kalkan dolu; "3 – 2 – 1 – DÖVÜŞ!".
3. **Raunt süresi:** 60 sn (veriden). Can 0 olunca **NAKAVT** (Bölüm 6'daki kurgu).
4. **Raunt sonu:** kazanan kısa zafer pozu (1,5 sn), kaybeden yerde; ekranda raunt göstergesi (her oyuncu için 2 nokta, kazanılan raunt dolar).
5. **Sonraki raunt girişi** (2 sn): "RAUNT 2", sonra "SON RAUNT" (1–1 ise).
6. İki raunt kazanan maçı kazanır. Toplam maç süresi hedefi 1,5–3,5 dk.

### 3.2 Süre biterse
- Süre bittiğinde can **yüzdesi** yüksek olan raundu kazanır ("SÜRE DOLDU").
- Can yüzdeleri eşitse (±%1): **ALTIN RAUNT:** 15 sn, ilk isabet raundu kazanır; o da olmazsa canı fazla olan, o da eşitse yazı-tura değil, son isabeti yapan.

### 3.3 Raunt arası
| Şey | Raunt arası |
|---|---|
| Can, kalkan dayanıklılığı | Tam dolar |
| Güç çubuğu (ulti) | **Korunur** |
| Elemental güçler | **Sıfırlanır** (biriktirip saklamak yerine kullanmaya teşvik) |
| Kalpler | Korunur (maç başına 3) |
| Özel hareket bekleme süresi | Sıfırlanır |
| Seri | Sıfırlanır |

### 3.4 Özel anlar ve ödüllere etkisi (oyuna taşınırken)
- **KUSURSUZ RAUNT:** hiç hasar almadan kazanılan raunt → büyük yazı + ek XP.
- **GERİ DÖNÜŞ:** 0–1'den 2–1 kazanmak → mevcut geri dönüş ödülü.
- Rütbe puanı, altın, eşya ve diğer ödüller maç sonucuna göre (galibiyet / yenilgi) aynen.
- "Nakavt yap" görevleri ve maç hedefleri → "raunt kazan" / "nakavt yap" olarak gözden geçirilir.
- Haftalık "Ani Ölüm" kuralı → tek raunt, 30 sn. Turnuva maçları → aynı raunt sistemi, raunt 45 sn.

---

## 4. Seri saldırıdan kaçış fırsatları (özet)

Savunan her zaman bir çıkış yoluna sahip olmalı:
1. **3'lü zincir boşluğu** (2.4): en az 150 ms; kalkan, kaçış, KIR ya da karşı saldırı.
2. **KIR:** rakip zincirdeyken 1 parça güçle zinciri keser.
3. **Kusursuz savunma:** okuyup zamanında basan zinciri kırar ve sırayı alır (+ yıldırım gücü, 5.1).
4. **Kaçış:** 0,25 sn dokunulmaz, 1 kare geri.
5. **Yerden kalkış dokunulmazlığı:** yere düşen kalkarken **0,5 sn** dokunulmaz (sonsuz yer serisi yok).
6. **Elemental yağmurun hedef işareti** (5.3).
7. **Seri hasar azalması:** uzun seri bile raundu tek başına bitiremez.

---

## 5. Elemental güçler

### 5.1 Kaynak (karar: seri ve savunmaya bağlı, rastgele değil)

| Güç | Nasıl kazanılır | Neden |
|---|---|---|
| **Kar topu** | Engellenmeden **4 isabetlik seri** | En kolay kazanılan, en hafif güç |
| **Ateş topu** | Engellenmeden **6 isabetlik seri** (4'te kar topu, 6'da ayrıca ateş topu) | Uzun seri daha büyük ödül |
| **Yıldırım** | **Kusursuz savunma** | Savunmayı ödüllendirir, geride kalana geri dönüş şansı verir |

- Bir seride en fazla 1 kar topu ve 1 ateş topu kazanılır.
- **En çok 3 güç birikir.** Dolu iken kazanılan güç kaybolur ("ÇANTA DOLU" değil, yuvalarda kısa bir parlama).
- İki güç kullanımı arasında **1,5 sn** bekleme.
- Raunt sonunda sıfırlanır (3.3).

### 5.2 Yağmur

| Güç | Yağmur | Her isabet | Son isabet |
|---|---|---|---|
| **Kar topu** | 1,5 sn boyunca 6 kar topu, hızlı ve hafif | Küçük hasar, rakip her darbede biraz yavaşlar | Büyük kar topu: rakip donar ve yere düşer |
| **Ateş topu** | 1,5 sn boyunca 4 büyük ateş topu, ağır | Orta hasar, yerde kısa süre yanan iz | En büyüğü: patlama, rakip havaya savrulup yere düşer |
| **Yıldırım** | 1,2 sn boyunca 5 hızlı şimşek | Küçük hasar, rakip titrer | Kalın son şimşek: rakip sersemleyip yere yığılır |

- Toplam hasar referans canın **%13'ü** (seri azalması uygulanmaz; savunmayla azalır).
- Son isabetten sonra rakip **yere düşer** (4. dosya ayak kesilmesi / havaya fırlama tepkileri), **0,5 sn kalkış dokunulmazlığı.**

### 5.3 Kaçış ve savunma
- **Hedef işareti:** Yağmur başında rakibin bulunduğu yere bir daire düşer (kar 0,8 sn, ateş 0,6 sn, yıldırım 0,4 sn önceden). Alan **kar ve ateşte kilitlenir;** **yıldırımda rakibi yavaşça takip eder** (en zor kaçılan).
- **Kaçış:** alanın dışına çıkan oyuncu kalan isabetlerden kurtulur.
- **Kalkan:** her isabet dayanıklılık düşürür (kar 8, ateş 15, yıldırım 10), engelleyen yere **düşmez** ve hasarın %20'sini alır. Kalkan yağmur ortasında kırılırsa kalan isabetler tam gelir.
- İsabetler arasında küçük boşluklar (usta oyuncu kaçışı zamanlayabilir).
- Yağmur sırasında saldıran **serbesttir** (saldırabilir); ama yağmur alanındaki rakibe zincir vuruşu yağmur isabetleriyle aynı karede gelirse yalnız biri sayılır (çift sayım yok).

### 5.4 Gösteri
1. **Çağırma (0,4 sn):** saldıran elini gökyüzüne kaldırır, gökyüzü elementin rengine bürünür (kar: soğuk mavi; ateş: turuncu-kızıl; yıldırım: kararan bulutlar, şimşek parıltıları).
2. **Yağmur:** gökten iniş izi, her isabette patlama efekti ve rakip tepkisi (sarsılma, donmaya başlama, titreme).
3. **Son isabet:** 0,25 sn ağır çekim, büyük patlama, ekran sarsıntısı, rakip yere düşer.

### 5.5 Arayüz
- KALKAN ve ULTİ'nin yanında **3 küçük güç yuvası**; kazanılan güç kendi simgesiyle (mavi kar tanesi, turuncu alev, sarı şimşek) dolar. Oyuncu hangisini kullanacaksa ona dokunur.
- Güç kazanılınca yuva parlar ve kısa bir ses; ekranda "KAR TOPU KAZANDIN!" gibi kısa bir yazı.
- **Rakibin biriken güçleri** rakibin can çubuğunun altında küçük simgelerle görünür.
- Klavye (prototip): 1, 2, 3 tuşları.

---

## 6. Kamera ve ağır çekim kurgusu

### 6.1 Takip kamerası (1,5 kat geniş platformda)
- Kamera yandan, iki foi'yi her zaman kadrajda tutar; aralarındaki mesafeye göre yakınlaşır ve uzaklaşır (en yakın ve en uzak sınırları veriden). Hareketler yumuşatılmış, ani sıçrama yok.
- Dikey ekranda karakterler ekranın %30–60 yükseklik bandında (2. dosya 2.4). Platformun tamamı görünmek zorunda değil.
- Karakterler kadrajdan asla çıkmaz; kenara sıkışan foi için kamera hafifçe kayar.

### 6.2 Olay kurgusu

| Olay | Kamera | Zaman | Diğer |
|---|---|---|---|
| Normal isabet | — | Vuruş donması (aileye göre 20–65 ms) | Hasar sayısı |
| Zincirin 3. isabeti | Hafif yakınlaşma (%5) | Donma 70 ms | Seri sayısı büyür |
| GÜÇLÜ | Darbe yönünde sarsıntı | Donma 90 ms | |
| YERE SER | Aşağıdan yukarı sarsıntı, havadaki rakibi izleme | 0,3 sn ağır çekim | |
| Kusursuz savunma | Kısa beyaz parlama | 0,15 sn ağır çekim | "KARŞILADI!" |
| Elemental yağmur | Geri çekilip yükselme (gökyüzü görünsün), son isabette rakibe yaklaşma | Son isabette 0,25 sn ağır çekim | Gökyüzü rengi |
| Ulti | Sinematik (1. ve 4. dosya) | Ulti boyunca | Arka plan kararır |
| **Raundu bitiren nakavt** | Rakibe yaklaşma | **0,6 sn ağır çekim** | "NAKAVT!" büyük yazı, tribün |
| **Maçı bitiren nakavt** | Ağır çekimin ardından **2 sn yakın açıdan tekrar gösterimi** (atlanabilir) | | Sonra zafer dansı ve maç sonu ekranı |
| Son 10 sn | Hafif yakınlaşma | — | Kalp atışı sesi |

- Ağır çekimler yalnız görseldir; canlı maçta oyun saati durmaz (ileride).
- Ayarlarda "Ekran sarsıntısı" ve "Tekrar gösterimi" aç/kapa.

---

## 7. Platform ve silah oranları

### 7.1 Platform: enine 1,5 kat
1. **Önce hızlı deneme:** Bir platformda yalnız enine ölçekleme (×1,5); dokular dünyaya göre döşenen (triplanar) materyalle gerilmeden. Ekran görüntüsü.
2. **Görünüş kötüyse:** modeli ortadan kesip iki yarıyı ayırarak araya düz parça ekle ("stadyum" şekli; süsler yassılaşmaz). Bilgisayarda Blender varsa komut satırından Blender betiğiyle, yoksa Unity editör aracıyla. Aynı araç 7 platformun hepsine uygulanır.
3. Platform modellerinin **asıl dosyalarına dokunma;** değiştirilmiş kopyalar üret.
4. Oyundaki kare sayısı, kenar sınırları, başlangıç noktaları, tribün ve dekor yerleşimi yeni genişliğe göre güncellenir. Kenara sıkışma (köşe): kenara savrulan rakip sekip öne düşer (6. dosya).
5. Prototipte genişlik ayardan değiştirilebilir (×1,25 / ×1,5 / ×2) ki oyun sahibi deneyebilsin.

### 7.2 Silah oranları (karakter boyuna göre)

| Aile | Görünen uzunluk |
|---|---|
| Kılıç | Boyun %55–65'i |
| Balta, gürz, çekiç | Boyun %50–60'ı; baş kısmı kafadan küçük |
| Mızrak | Boyun %100–110'u |
| Kalkan | Gövdeyi kapatır, kafayı kapatmaz |

- Kural: **silahın görünen ucu, oyundaki menzilin ötesine taşmaz ve menzilden belirgin kısa kalmaz.** Görünen boy oyuncuya menzili anlatır.
- 30 el silahının ve kalkanların hepsi bu oranlara getirilir (ölçek verisi; modeller değiştirilmez). Kavrama noktaları (9. dosya Bölüm 5) yeni ölçeğe göre güncellenir.
- Ekran görüntüleri: her silah karakterin elinde, bekleme ve saldırı karesinde.

---

## 8. Botlar

Bot yeni çekirdeği kullanarak "düşünen" bir rakip olmalı:
- **İsabeti görüp devam etme:** zincirin ilk vuruşu isabet ederse devam eder, engellenirse durur (rütbe yükseldikçe daha tutarlı).
- **Ceza:** rakibin ıskaladığı ya da engellenen saldırısından sonraki boşlukta saldırır.
- **Boşluk kullanımı:** 3'lü zincir boşluğunda kalkan / kaçış / KIR (kişiliğe göre).
- **Elemental güçler:** kazanınca kullanır; Savunmacı kişilik saklar, Saldırgan hemen kullanır. Yağmur alanından kaçar (Er %10 → Mareşal %60).
- **Ritim:** bot da zincir penceresine uyar; insan gibi bazen erken basıp zinciri kırar (düşük rütbede daha sık).
- **Spam botu** (yalnız test için): ekrana durmadan basan bot; düşünen bota karşı belirgin şekilde kaybetmeli (Faz 0 ölçümüyle karşılaştır).
- Dinamik zorluk ve kişilikler mevcut kurallarla.

---

## 9. Prototip ve .exe

### 9.1 Test sahnesi
- Yeni sahne: `Scenes/Dev/DuelPrototypeV2.unity`. Yeni çekirdek yalnız bu sahnede açık (`DuelCoreV2` bayrağı); oyunun kendisi değişmez.
- Mevcut karakter, animasyon, efekt, savunma, hareket seti, kamera ve platform varlıkları kullanılır.

### 9.2 Prototip menüsü (sahne başında)
- **Mod:** Oyuncu – Bot · Bot – Bot (izleme) · Oyuncu – Kukla (antrenman)
- **Silah ailesi ve kademe** (her iki taraf için ayrı)
- **Bot zorluğu ve kişiliği**
- **Platform** (7 sahneden biri) ve **genişlik** (×1,25 / ×1,5 / ×2)
- **Raunt süresi** ve **raunt sayısı**

### 9.3 Canlı ayar paneli (F1)
Oyun sırasında açılan panel; değişiklik anında uygulanır:
- Hareket süreleri (aile başına hazırlık / vuruş / toparlanma), sersemleme ve geri itilme süreleri
- Zincir penceresi, girdi önbelleği, spam eşiği, 3'lü zincir boşluğu
- Hasar yüzdeleri, seri azalması
- Elemental güç eşikleri, yağmur süresi / sayısı / hasarı, hedef işaret süreleri
- Vuruş donmaları, ağır çekim süreleri, kamera yakınlaşma sınırları
- Genel **tempo çarpanı** (bütün süreleri birlikte ×0,8 – ×1,2)
- **Kaydet / Yükle:** ayarlar `DuelCoreSettings.json` dosyasına; oyun sahibi beğendiği ayarı kaydedip gönderebilsin. "Varsayılana dön" düğmesi.

### 9.4 Yardımcı araçlar
- **Ağır çekim** (0,5× / 0,25×), **duraklat ve kare kare ilerlet** (F2, F3)
- **Vuruş günlüğü** (son 10 vuruş: sonuç ve sebebi; 8. dosya 3.4)
- **Avantaj göstergesi:** ekranın üstünde, sıranın kimde olduğunu ve kaç ms avantaj olduğunu gösteren küçük çubuk (açılıp kapatılabilir)
- **Kutu görselleştirme** (6. dosya)

### 9.5 Kontroller (bilgisayarda)
| Hareket | Fare | Klavye |
|---|---|---|
| Vur | Sol tık (boş yere) | Boşluk |
| Yürü | Sürükle | A / D |
| Zıpla | Yukarı sürükle | W |
| Kaç | Aşağı hızlı sürükle | S |
| Çömel | Aşağı sürükle ve tut | S basılı |
| Kalkan / kusursuz savunma | Ekrandaki düğme | Shift |
| Özel | Düğme | Q |
| Ulti | Düğme | E |
| KIR / GÜÇLÜ / YERE SER (beliren) | Düğme | R |
| Elemental güçler | Yuvalar | 1 / 2 / 3 |

Dokunmatik düğme yerleşimi de ekranda durur (mobil his için), fareyle tıklanabilir.

### 9.6 Derleme
- Windows 64 bit **.exe**, pencereli mod, dikey oran (ör. 540×1170) ve tam ekran seçeneği.
- Çıktı: proje dışında bir klasöre (ör. `Builds/DuelPrototypeV2/`), tek bir **.zip** hâlinde.
- Klasörde kısa bir `OKUBENI.txt`: kontroller, ayar paneli, ayar dosyasının yeri.
- İsteğe bağlı: aynı sahnenin Android test yapısı (.apk), telefonda dokunmatik deneme için.

---

## 10. Fazlar

| Faz | İş | Dur? |
|---|---|---|
| 0 | Teşhis (Bölüm 1), mevcut kodun incelenmesi, uygulama planı | Hayır |
| 1 | Prototip sahnesi, menü, ayar paneli iskeleti, araçlar (9.1–9.4) | Hayır |
| 2 | Dövüş çekirdeği: evreler, süreler, avantaj, takas kuralı, ritmik zincir, hasar dengesi (Bölüm 2) | Hayır |
| 3 | Kaçış fırsatları ve savunma entegrasyonu (Bölüm 4) | Hayır |
| 4 | Raunt sistemi (Bölüm 3) | Hayır |
| 5 | Elemental güçler (Bölüm 5) | Hayır |
| 6 | Kamera ve ağır çekim kurgusu (Bölüm 6) | Hayır |
| 7 | Platform 1,5 kat ve silah oranları (Bölüm 7) | Hayır |
| 8 | Botlar (Bölüm 8); bot – bot 500 maç denge raporu: raunt süresi, isabet sayısı, aile kazanma oranları, spam botu sonucu | Hayır |
| 9 | **.exe derlemesi** (9.6) ve `Docs/DovusV2_Rapor.md` (ne yapıldı, ölçümler, bilinen sorunlar) | **Evet: oyun sahibi oynayıp ayarlayacak** |
| 10 | (Onaydan sonra) Oyuna taşıma: canlı oyun kodu, ödüller ve görevler (3.4), haftalık modlar ve turnuva, **sunucu** (raunt, hareket süreleri, hasar, elemental güçler; yeni protokol; 150 ms pencere kuralı), test planı (TEST_VE_DENETIM_PLANI.md) ile tam test | Bitince rapor |

Her faz ayrı commit; ilerleme `Docs/DovusV2_Ilerleme.md` dosyasına. Bağlam dolarsa bu dosyadan devam.

### Faz 9 kabul kriterleri
- Bir raunt aynı kademede ortalama 15–25 isabette bitiyor (bot – bot ölçümü).
- Spam botu düşünen bota karşı %20'nin altında kazanıyor.
- Her vuruşun sonucu (isabet / engel / karşılandı / kaçtı / ıska) ekranda ayırt ediliyor.
- 3'lü zincir boşluğunda kalkan ve kaçış her zaman mümkün (en az 150 ms).
- Elemental yağmurlar hedef işaretiyle geliyor, kaçılabiliyor, son isabette rakip yere düşüyor.
- Raunt akışı, süre dolması ve ALTIN RAUNT çalışıyor.
- Ayar paneliyle her değer canlı değişiyor ve kaydedilip yükleniyor.
- .exe başka bir Windows bilgisayarda kurulumsuz açılıyor.
