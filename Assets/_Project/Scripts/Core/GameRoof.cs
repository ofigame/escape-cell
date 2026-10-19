using BrickMuseum.City;
using BrickMuseum.UI;
using UnityEngine;

namespace BrickMuseum.Core
{
    /// <summary>
    /// The rooftop of a skyscraper: at the door of a tall building "ÇIK" takes the walker to the very top, where they can
    /// walk round the roof and watch the city from above; "İN" brings them back down to the door.
    /// </summary>
    public sealed partial class Game
    {
        private bool onRoof;
        private CityMap.Obstacle roof;
        private Vector3 roofDoor;

        private void WireRoof() => tu.homeBtn.onClick.AddListener(() => { if (sitting) StandUp(); else if (benchNear != null) SitOn(benchNear.Value); else if (onRoof) LeaveRoof(); else if (roofNear != null) ClimbRoof(roofNear.Value); });

        private CityMap.Obstacle? roofNear;

        private static bool IsTower(CityMap.Obstacle o) => o.model != null && o.model.kind == "big" && o.top > 8f;

        private void ClimbRoof(CityMap.Obstacle o)
        {
            roof = o;
            roofDoor = walkPos;
            onRoof = true;
            walkPos = new Vector3(o.center.x, o.top, o.center.y);
            walkVy = 0f; grounded = true;
            walkCamDist = 16f; camPitch = 28f;
            ShadowDistance(220f);
            Sfx.Play("win", 0.5f);
            Toast($"{o.model.name} tepesindesin · şehri seyret", 2.5f);
        }

        private void LeaveRoof()
        {
            if (!onRoof) return;
            onRoof = false;
            walkPos = roofDoor;
            walkPos.y = GroundAt(walkPos);
            walkVy = 0f; grounded = true;
            walkCamDist = 6.5f; camPitch = 14f;
            ShadowDistance(90f);
        }

        /// <summary>On the roof the walker stays inside its edge.</summary>
        private Vector3 RoofClamp(Vector3 p)
        {
            var half = roof.half - Vector2.one * Mathf.Min(0.8f, Mathf.Min(roof.half.x, roof.half.y) * 0.3f);
            p.x = Mathf.Clamp(p.x, roof.center.x - half.x, roof.center.x + half.x);
            p.z = Mathf.Clamp(p.z, roof.center.y - half.y, roof.center.y + half.y);
            return p;
        }

        /// <summary>The ÇIK / İN button; true while on a roof (nothing else to show then).</summary>
        private bool ShowRoofNearby(CityMap.Obstacle? best, float bestD)
        {
            if (onRoof)
            {
                roofNear = null;
                tu.SetHomeLabel("AŞAĞI");
                tu.homeBtn.gameObject.SetActive(true);
                tu.carBtn.gameObject.SetActive(false);
                tu.nearChip.SetActive(true);
                tu.nearText.text = $"{roof.model.name} · tepe";
                return true;
            }
            roofNear = best != null && bestD < 2.5f && IsTower(best.Value) ? best : null;
            if (roofNear != null) tu.SetHomeLabel("ÇIK");
            tu.homeBtn.gameObject.SetActive(roofNear != null);
            return false;
        }
    }
}
