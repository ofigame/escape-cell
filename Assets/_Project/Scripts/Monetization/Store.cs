using System;
using System.Collections.Generic;
using SquashBot.Data;
using UnityEngine;
#if FOI_IAP
using UnityEngine.Purchasing;
#endif

namespace SquashBot.Monetization
{
    /// <summary>
    /// In-app purchases (the scenario's phase 6): remove ads (interstitials and the banner go; rewarded ads stay a
    /// choice), the season road's paid track (the month's pass) and cosmetic packs. Core parts are never sold (the duel
    /// stays fair). The store itself is Unity IAP, compiled in with the FOI_IAP define once the package is installed and
    /// the products exist in Google Play and App Store Connect; without it every product shows as "soon".
    /// </summary>
    public static class Store
    {
        public const string RemoveAds = "foi.removeads", SeasonPass = "foi.seasonpass", PackHeroes = "foi.pack.heroes", PackCapes = "foi.pack.capes";

        public static readonly string[] Products = { RemoveAds, SeasonPass, PackHeroes, PackCapes };

        /// <summary>Purchases on in the game (off for now: the package is in, the products stay out of sight).</summary>
        public const bool Enabled = false;

        /// <summary>Whether a product can be bought on this build (the store is in and it answered).</summary>
        public static bool Available { get; private set; }

        // ---------- What was bought ----------

        public static bool NoAds => Prefs.GetInt("sb_iap_noads", 0) == 1;

        /// <summary>The season road's paid track this month.</summary>
        public static bool HasPass => Prefs.GetString("sb_iap_pass", "") == DateTime.Now.ToString("yyyyMM");

        private static void Grant(string id)
        {
            switch (id)
            {
                case RemoveAds: Prefs.SetInt("sb_iap_noads", 1); break;
                case SeasonPass: Prefs.SetString("sb_iap_pass", DateTime.Now.ToString("yyyyMM")); break;
                case PackHeroes:
                    foreach (var c in Cosmetics.InSlot(Slot.Suit)) if (!c.IsDefault && !Cosmetics.Owns(c) && c.rank <= 0) Cosmetics.Give(c.id);
                    break;
                case PackCapes:
                    foreach (var c in Cosmetics.InSlot(Slot.Back)) if (c.id.StartsWith("back.cape") && !c.IsDefault && !Cosmetics.Owns(c) && c.rank <= 0) Cosmetics.Give(c.id);
                    break;
            }
            Prefs.Save();
            Bought?.Invoke(id);
        }

        /// <summary>A purchase went through (its product id).</summary>
        public static event Action<string> Bought;

        /// <summary>The price as the store shows it (null: not available here).</summary>
        public static string Price(string id)
        {
#if FOI_IAP
            var p = controller?.products.WithID(id);
            return p != null && p.availableToPurchase ? p.metadata.localizedPriceString : null;
#else
            return null;
#endif
        }

        public static bool Owns(string id) => id == RemoveAds ? NoAds : id == SeasonPass ? HasPass : false;

        /// <summary>Starts a purchase; <paramref name="done"/> gets true when it went through.</summary>
        public static void Buy(string id, Action<bool> done)
        {
#if FOI_IAP
            if (controller == null || !Available) { done?.Invoke(false); return; }
            pending = done;
            controller.InitiatePurchase(id);
#else
            done?.Invoke(false);
#endif
        }

        /// <summary>The store started (once, at the game's start).</summary>
        public static void Init()
        {
#if FOI_IAP
            if (listener != null) return;
            listener = new Listener();
            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            builder.AddProduct(RemoveAds, ProductType.NonConsumable);
            builder.AddProduct(SeasonPass, ProductType.Consumable);
            builder.AddProduct(PackHeroes, ProductType.NonConsumable);
            builder.AddProduct(PackCapes, ProductType.NonConsumable);
            UnityPurchasing.Initialize(listener, builder);
#endif
        }

#if FOI_IAP
        private static IStoreController controller;
        private static Listener listener;
        private static Action<bool> pending;

        private class Listener : IStoreListener
        {
            public void OnInitialized(IStoreController c, IExtensionProvider e)
            {
                controller = c;
                Available = true;
                // what was bought before (a reinstall): the non-consumables are given again
                foreach (var p in c.products.all) if (p.hasReceipt && p.definition.type == ProductType.NonConsumable) Grant(p.definition.id);
            }

            public void OnInitializeFailed(InitializationFailureReason error) => Available = false;
            public void OnInitializeFailed(InitializationFailureReason error, string message) => Available = false;

            public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
            {
                Grant(args.purchasedProduct.definition.id);
                pending?.Invoke(true);
                pending = null;
                return PurchaseProcessingResult.Complete;
            }

            public void OnPurchaseFailed(Product product, PurchaseFailureReason reason)
            {
                pending?.Invoke(false);
                pending = null;
            }
        }
#endif
    }
}
