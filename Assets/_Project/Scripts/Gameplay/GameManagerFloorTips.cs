using SquashBot.Data;
using SquashBot.UI;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The floors' hidden moves made plain (the fixes design's job 14b): on the third floor Bip shows the spin (a hand
    /// drawing a circle until foi spins), on the sixth the flight (a hand held down until foi flies), each once. A setting
    /// shows a SPIN and a FLY button instead: SPIN whirls at once; FLY lifts foi, a finger on the floor picks where it
    /// lands, FLY again (or the jet running out) brings it down.
    /// </summary>
    public partial class GameManager
    {
        private enum FloorTip { None, Spin, Hover }

        private FloorTip floorTip;
        private float floorTipUntil;
        private TutorialHand floorHand;
        private bool floorButtonsWired, flyStartPressed, flyEndPressed, hoverFromButton;

        private const string SpinTipKey = "sb_tut_spin", HoverTipKey = "sb_tut_hover";
        private const float FloorTipSeconds = 15f;

        /// <summary>foi broke out of its cell: the third floor's spin lesson, the sixth's flight lesson (once each).</summary>
        private void OfferFloorTip()
        {
            if (levelIndex >= 5 && Prefs.GetInt(HoverTipKey, 0) == 0) StartFloorTip(FloorTip.Hover, HoverTipKey, "tut.hoverTitle", "tut.hover", TutorialHand.Gesture.Hold);
            else if (levelIndex >= 2 && Prefs.GetInt(SpinTipKey, 0) == 0) StartFloorTip(FloorTip.Spin, SpinTipKey, "spin.tipTitle", "tut.spin", TutorialHand.Gesture.Circle);
        }

        private void StartFloorTip(FloorTip tip, string key, string title, string text, TutorialHand.Gesture gesture)
        {
            Prefs.SetInt(key, 1);
            Prefs.Save();
            floorTip = tip;
            floorTipUntil = Time.time + FloorTipSeconds;
            ui.ShowIntro(Loc.T(title), Loc.T(text));
            if (floorHand == null)
            {
                var parent = (ui.transform.Find("Canvas/SafeArea") ?? ui.transform.Find("Canvas") ?? ui.transform) as RectTransform;
                if (parent != null) floorHand = TutorialHand.Create(parent);
            }
            if (floorHand != null) { floorHand.Show(gesture, new Vector2(0f, -260f)); floorHand.transform.SetAsLastSibling(); }
        }

        /// <summary>The lesson's move made (or the floor left): the hand goes.</summary>
        private void FloorTipDone(FloorTip tip)
        {
            if (floorTip == FloorTip.None || (tip != FloorTip.None && tip != floorTip)) return;
            floorTip = FloorTip.None;
            if (floorHand != null) floorHand.Show(TutorialHand.Gesture.None, Vector2.zero);
        }

        /// <summary>Each frame: the lesson's time, the SPIN and FLY buttons shown on a floor fight when the setting is on.</summary>
        private void UpdateFloorTips()
        {
            if (ui == null) return;
            if (!floorButtonsWired)
            {
                floorButtonsWired = true;
                ui.FloorSpinPressed += () => { if (PowerFloor) SpinAttack(); };
                ui.FloorFlyPressed += () => { if (!PowerFloor) return; if (hovering) flyEndPressed = true; else flyStartPressed = true; };
            }
            if (floorTip != FloorTip.None && (Time.time > floorTipUntil || State != GameState.Playing)) FloorTipDone(FloorTip.None);
            ui.SetFloorSkillButtons(SaveData.FloorSkillButtons && PowerFloor && !levelTps);
        }

        /// <summary>The FLY button pressed to lift off (once).</summary>
        private bool TakeFlyStart()
        {
            if (!flyStartPressed) return false;
            flyStartPressed = false;
            return true;
        }

        /// <summary>The FLY button pressed again to come down (once).</summary>
        private bool TakeFlyEnd()
        {
            if (!flyEndPressed) return false;
            flyEndPressed = false;
            return true;
        }

        /// <summary>Flying from the button: a finger (or the mouse) on the floor picks where foi lands.</summary>
        private void FlyButtonTarget()
        {
            Vector2? at = null;
            if (Input.touchCount > 0) at = Input.GetTouch(Input.touchCount - 1).position;
            else if (Input.GetMouseButton(0)) at = Input.mousePosition;
            if (at.HasValue && !ui.IsOverTool(at.Value) && TryScreenToGrid(at.Value, out var cell)) hoverTarget = cell;
        }
    }
}
