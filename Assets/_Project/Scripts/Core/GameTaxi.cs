using System.Collections.Generic;
using BrickMuseum.Character;
using BrickMuseum.City;
using BrickMuseum.FreeBuild;
using UnityEngine;

namespace BrickMuseum.Core
{
    /// <summary>
    /// Taxi work, in a car of the player's own with a taxi sign on it. "MÜŞTERİ": someone waits on a pavement with an
    /// arrow bobbing over them, a strip of arrows on the streets leads there; stop beside them and they get in; the
    /// strip then leads to where they are going, with an arrow over the spot; stop there and they pay — by the distance
    /// along the streets, plus a tip for a quick ride.
    /// </summary>
    public sealed partial class Game
    {
        private enum TaxiState { Off, ToPassenger, ToDestination }

        private TaxiState taxi;
        private RouteLine route;
        private BrickPerson fare;
        private Vector2 fareStand, fareStreet, dropStand, dropStreet;
        private float routeTimer, rideLength, rideStart, routeLeft;
        private int ridePrice;
        private readonly System.Random taxiRnd = new System.Random();
        private const float PickUpRange = 7f, StopSpeed = 3f;

        private void WireTaxi()
        {
            tu.taxiBtn.onClick.AddListener(() => { if (taxi == TaxiState.Off) StartTaxiJob(); else CancelTaxi(true); });
        }

        private bool TaxiAvailable => driving && carIsTaxi;

        private void StartTaxiJob()
        {
            route ??= new RouteLine(city.root);
            var carPlan = CityMap.PlanOf(car.position);
            // somebody at least a little way off
            for (int i = 0; i < 30; i++)
            {
                (fareStand, fareStreet) = StreetGraph.RandomKerb(taxiRnd);
                if (StreetGraph.Length(StreetGraph.Route(carPlan, fareStreet)) > 70f) break;
            }
            if (fare != null) Destroy(fare.root.gameObject);
            fare = new BrickPerson(city.root, Townsfolk.Resident(taxiRnd), BrickPerson.Height, true);
            var stand = city.WorldAt(fareStand, 0f);
            stand.y = city.GroundAt(stand);
            fare.root.position = stand;
            var face = city.WorldAt(fareStreet, 0f) - stand; face.y = 0;
            fare.root.rotation = Quaternion.LookRotation(face);
            route.Mark(fareStand, stand.y - CityMap.Center.y);
            taxi = TaxiState.ToPassenger;
            routeTimer = 0f;
            tu.SetTaxiLabel("İPTAL");
            Toast("Bir müşteri seni bekliyor");
            Sfx.Play("pick");
        }

        private void CancelTaxi(bool told)
        {
            if (taxi == TaxiState.Off) return;
            taxi = TaxiState.Off;
            route?.Hide();
            if (fare != null) { Destroy(fare.root.gameObject); fare = null; }
            tu.SetTaxiLabel("MÜŞTERİ");
            if (told) Toast("Taksi işi iptal");
        }

        /// <summary>Called every frame while driving: the strip, the pick-up and the drop-off.</summary>
        private void TaxiUpdate(float dt)
        {
            tu.taxiBtn.gameObject.SetActive(TaxiAvailable);
            if (taxi == TaxiState.Off) return;
            route.Tick(time);
            fare?.Animate(0f, false, dt);
            var carPlan = CityMap.PlanOf(car.position);
            var goal = taxi == TaxiState.ToPassenger ? fareStreet : dropStreet;
            routeTimer -= dt;
            if (routeTimer <= 0f)
            {
                var path = StreetGraph.Route(carPlan, goal);
                path.Insert(0, carPlan);
                routeLeft = StreetGraph.Length(path);
                route.Show(path);
                routeTimer = 0.5f;
            }
            var stand = taxi == TaxiState.ToPassenger ? fareStand : dropStand;
            bool there = Vector2.Distance(carPlan, stand) < PickUpRange + CityLayout.Road / 2f && Mathf.Abs(carSpeed) < StopSpeed;
            if (taxi == TaxiState.ToPassenger)
            {
                tu.nearText.text = $"Müşteriye git · {Mathf.RoundToInt(routeLeft * 0.9f)} m";
                if (!there) return;
                // in they get, and they say where to
                fare.root.gameObject.SetActive(false);
                for (int i = 0; i < 30; i++)
                {
                    (dropStand, dropStreet) = StreetGraph.RandomKerb(taxiRnd);
                    rideLength = StreetGraph.Length(StreetGraph.Route(fareStreet, dropStreet));
                    if (rideLength > 160f) break;
                }
                ridePrice = 15 + Mathf.RoundToInt(rideLength * 0.9f / 1000f * 45f);
                rideStart = Time.time;
                var drop = city.WorldAt(dropStand, 0f);
                route.Mark(dropStand, city.GroundAt(drop) - CityMap.Center.y);
                taxi = TaxiState.ToDestination;
                routeTimer = 0f;
                Toast($"Yolcu bindi · ücret {ridePrice} lira");
                Sfx.Play("place");
            }
            else
            {
                tu.nearText.text = $"Hedefe git · {Mathf.RoundToInt(routeLeft * 0.9f)} m · {ridePrice} lira";
                if (!there) return;
                // a quick ride earns a tip
                float expected = rideLength / 9f;
                int tip = Time.time - rideStart < expected ? Mathf.RoundToInt(ridePrice * 0.25f) : 0;
                Wallet.Earn(ridePrice + tip);
                var drop = city.WorldAt(dropStand, 0f);
                drop.y = city.GroundAt(drop);
                fare.root.position = drop;
                fare.root.gameObject.SetActive(true);
                var leftBehind = fare;
                fare = null;
                Destroy(leftBehind.root.gameObject, 5f);
                taxi = TaxiState.Off;
                route.Hide();
                tu.SetTaxiLabel("MÜŞTERİ");
                Toast(tip > 0 ? $"+{ridePrice} lira · bahşiş +{tip}" : $"+{ridePrice} lira", 2.4f);
                Sfx.Play("win");
            }
        }
    }
}
