# Düello Dövüş Çekirdeği v2 — İlerleme

Belge: `Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md` · Dal: `dovus-cekirdegi-v2` (SquashBot deposu) · Her faz ayrı commit.
Kopyalar depoda: `SquashBot/Docs/DovusV2/`.

| Faz | Durum | Commit | Not |
|---|---|---|---|
| 0 | Bitti | (bu commit) | Teşhis: `Docs/DovusV2_Teshis.md` (50–140 isabet/nakavt, sersemleme yok, takas sürekli, hasar dokunuşta, spam en iyi strateji) + uygulama planı |
| 1 | Bitti | (bu commit) | `Scenes/Dev/DuelPrototypeV2.unity` (Main'in kopyası + `DuelPrototypeV2`), `Editor/DuelV2Setup` (sahne, .exe), `Tools/build-duelv2.sh` (exe + zip). Oyun düelloyu kendi kurar (`GameManagerDuelV2.V2SetupDuel`: arena, seyirci, iki foi, aile×kademe silah, rakibe kalkan), sonra `DuelCoreV2.Running` ile eski düello durur (GameManager.Update/LateUpdate başında tek satır). Menü (mod, iki tarafın ailesi/kademesi, bot ustalığı/kişiliği, sahne, genişlik, raunt süresi/sayısı), F1 canlı ayar paneli (DuelCoreSettings'in bütün alanları yansımayla; Kaydet/Yükle/Varsayılan → `DuelCoreSettings.json` exe'nin yanında), F2 ağır çekim ×0,5/×0,25, F3 duraklat + F4 bir kare, F5/F6/F7 günlük/avantaj/kutu anahtarları, kendi kamerası (`DuelCameraV2`, temel takip), kendini sınama modu (`-v2Auto dir sn`). Metinler `v2.*` anahtarları (en, tr). |
