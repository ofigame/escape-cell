using SquashBot.Data;
using TMPro;
using UnityEngine;

namespace SquashBot.UI
{
    /// <summary>
    /// Once, on the menu after the garage was renewed (Cosmetics.MigrateGarage): "Garage renewed", what is gone (hats,
    /// armour paints, capes, arm and leg gear), that the rank gear comes with the rank, and the gold and core parts paid
    /// back for what the player had. A popup from the kit (UiKit.Popup) with OK under it.
    /// </summary>
    public class GarageRenewedCard : MonoBehaviour
    {
        private UiScreen screen;
        private TextMeshProUGUI body, paid;

        public static GarageRenewedCard Create(Transform canvasRoot)
        {
            var s = UiScreen.Create("GarageRenewed", canvasRoot, out var root);
            var c = root.gameObject.AddComponent<GarageRenewedCard>();
            c.screen = s;
            c.Build(root);
            return c;
        }

        private void Build(RectTransform root)
        {
            var st = UiKit.Style;
            var card = UiKit.Popup(root, screen, Loc.T("refund.title"), new Vector2(880f, 820f));
            body = Flat.Label(card, "", new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(780f, 300f), st.bodySize, st.text, TextAlignmentOptions.Center);
            body.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.enableAutoSizing = true; body.fontSizeMin = st.bodySize * 0.6f; body.fontSizeMax = st.bodySize;
            paid = Flat.Label(card, "", new Vector2(0.5f, 1f), new Vector2(0f, -510f), new Vector2(780f, 110f), st.titleSize * 0.8f, st.gold, TextAlignmentOptions.Center, bold: true);
            paid.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            paid.enableAutoSizing = true; paid.fontSizeMin = 28f; paid.fontSizeMax = st.titleSize * 0.8f;
            var ok = UiKit.PrimaryButton(card, Loc.T("refund.ok"), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(480f, 130f), () => screen.Hide());
            ((RectTransform)ok.transform).pivot = new Vector2(0.5f, 0.5f);
        }

        public bool IsOpen => screen.IsVisible;

        /// <summary>Tells of the renewal and what was paid back (<paramref name="cores"/> 0: the gold alone).</summary>
        public void Show(int gold, int cores)
        {
            body.text = Loc.T("refund.body");
            paid.text = cores > 0 ? Loc.F("refund.paidCores", gold, cores) : Loc.F("refund.paid", gold);
            screen.Show();
            transform.SetAsLastSibling();
        }
    }
}
