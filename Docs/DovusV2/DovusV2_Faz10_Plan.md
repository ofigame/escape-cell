# Dövüş v2: Faz 10 planı (oyuna taşıma)

Sahibinin kararı (2026-11-01): **tam v2 çekirdeği** canlı oyuna; sonra iOS **test** derlemesi, iOS'ta denenince Android.
Dal: `dovus-cekirdegi-v2`. Her alt faz ayrı commit; ilerleme `Docs/DovusV2_Ilerleme.md` (satır 10a, 10b …).

## Mimari

- **Kural tek yerde:** `Scripts/DuelV2/DuelSim*.cs`, `Fighter.cs`, `DuelCoreSettings.cs` ve küçük tipler. Aynı dosyalar
  sunucu projesine `<Compile Link>` ile bağlanır (sunucuda `UnityEngine` yerine küçük bir uyum katmanı: `Mathf`,
  `ScriptableObject`, `Tooltip`/`Header`). Kod içi varsayılanlar iki tarafın da kaynağıdır; maçta ayar özeti (hash) eşleşir.
- **Bot maçı (çevrimdışı):** istemci `DuelSim` + `DuelBotV2` koşturur; sonuç bugünkü gibi `SendResult` ile.
- **Canlı maç:** sunucu belirleyici. Oda `DuelSim`'i 60 Hz koşturur, iki istemciden **girdi** alır (`in`), 20 Hz
  **durum + olay** gönderir (`v2`). İstemci kendi kopyasını koşturmaz; sunucunun durumunu gösterir (konum yumuşatılır),
  olaylardan görüntü/ses/kamera. Protokol **10**; sunucu v<10'u 426 ile geri çevirir (eski sürümler bot maçına düşer).
- **Oyun içi denetleyici:** `DuelFightV2` prototipten ayrılır: `DuelMatchV2` (aileler, kademeler, bot ustalığı/kişiliği,
  raunt süresi/sayısı, genişlik, mod: bot / canlı / kukla, haftalık kural ayarları) + bitince geri çağrı. Prototip
  araçları (F1–F7, panel) yalnız prototip sahnesinde.
- **GameManager:** `DuelCoreV2.Running` iken yalnız düello mantığı durur; geri tuşu, duraklatma, canlı bağlantı sürer.
- **Arayüz:** uGUI (`DuelHudV2Ui`, koddan kurulur), 8 dil; dokunma: dokun = vur, yana kaydır = yürü, yukarı = zıpla,
  aşağı = kaç; düğmeler: KALKAN (basılı), ÖZEL, ULTİ, bağlam (KIR / GÜÇLÜ / YERE SER), KALP, güç yuvaları 1–3.

## Alt fazlar

| Alt faz | İş |
|---|---|
| 10a | Denetleyiciyi prototipten ayır (`DuelMatchV2`, geri çağrılar, araçlar yalnız prototipte); GameManager'da ince kapı |
| 10b | Oyunda bot maçı v2 ile: düello kurulunca v2 devralır; sonuç → `ChallengeFinish` (raunt skoru, geri dönüş, altın raunt, kusursuz raunt XP) |
| 10c | uGUI HUD + dokunmatik girdi + eski savaş HUD'unu v2'de gizle; 8 dil `v2.*` anahtarları |
| 10d | Ödüller/görevler (3.4): Combo5 → 3'lü set, Finish/Special/Ultimate/Perfect olaylardan, hedef "2 raunt", haftalık modlar (Ani Ölüm: 1 raunt 30 sn, turnuva: raunt 45 sn, QuickUlti/ComboFrenzy/Heartless/ShieldWar eşlemesi) |
| 10e | Sunucu: uyum katmanı + bağlı dosyalar, oda v2 (girdi → sim → durum/olay), sonuç `Settle`, protokol 10, v<10 → 426 |
| 10f | İstemci canlı: `LiveNet` protokol 10, girdi gönderme, durum uygulama, kopma/yeniden bağlanma |
| 10g | Öğretici / antrenman / laboratuvar kapıları (v2 ile ya da kapalı), temizlik, ölçüm (bench), rapor |
| 10h | iOS test derlemesi (sahibi dener) → sonra Android |

Sunucu zip'ini sahibi yükler (Plesk). Canlı maç sunucu yüklenmeden çalışmaz: yeni istemci o zamana kadar bot maçına düşer.
