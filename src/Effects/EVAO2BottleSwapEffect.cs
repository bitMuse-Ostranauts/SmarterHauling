using System;
using System.Collections.Generic;
using System.Linq;
using Ostranauts.Bit.Interactions;
using UnityEngine;

namespace Ostranauts.Bit.SmarterHauling.Effects
{
    /// <summary>
    /// Handles swapping an EVA suit O2 bottle with a charged one from stockpile zones.
    /// Interaction context: ACTSwapEVAO2Bottle
    /// </summary>
    public class EVAO2BottleSwapEffect : EVAMaintenanceEffectBase
    {
        private const string InteractionName = "ACTSwapEVAO2Bottle";

        private static readonly Dictionary<Guid, CondOwner> _targetBottles = new Dictionary<Guid, CondOwner>();

        public override bool ShouldPrepare(Interaction interaction)
        {
            return interaction != null && interaction.strName == InteractionName;
        }

        public override void Prepare(Interaction interaction)
        {
            if (interaction == null)
            {
                return;
            }

            CondOwner preferredBottle = ResolvePreferredBottle(interaction);

            if (preferredBottle != null)
            {
                _targetBottles[interaction.id] = preferredBottle;
            }
        }

        public override bool ShouldExecute(Interaction interaction)
        {
            return ShouldPrepare(interaction);
        }

        public override EffectResult Execute(Interaction interaction)
        {
            try
            {
                if (interaction?.objUs == null)
                {
                    Debug.LogWarning("[EVAO2BottleSwap] Invalid interaction context");
                    return new EffectResult(true);
                }

                // Check if actor is in player's company
                // NPCs not in player's company are handled by the pledge (cheat charge)
                // so this effect should only run for player's company members
                if (!IsInPlayerCompany(interaction.objUs))
                {
                    Debug.Log($"[EVAO2BottleSwap] {interaction.objUs.strNameFriendly} is not in player's company, skipping");
                    return new EffectResult(true);
                }

                CondOwner preferredBottle = null;
                _targetBottles.TryGetValue(interaction.id, out preferredBottle);

                CondOwner suit = FindEVASuit(interaction.objUs);
                if (suit == null || suit.compSlots == null)
                {
                    Debug.LogWarning("[EVAO2BottleSwap] EVA suit not found for actor");
                    return new EffectResult(true);
                }

                if (!TryGetSuitO2Bottle(suit, out CondOwner currentBottle, out CondOwner bottlePocket))
                {
                    Debug.LogWarning("[EVAO2BottleSwap] Suit does not have an accessible O2 bottle");
                    return new EffectResult(true);
                }

                CondOwner replacement = SelectReplacementBottle(interaction.objUs, currentBottle, preferredBottle);
                if (replacement == null)
                {
                    Debug.LogWarning("[EVAO2BottleSwap] No suitable replacement O2 bottle found");
                    return new EffectResult(true);
                }

                // Remove the current (depleted) bottle from the suit compartment
                bottlePocket.RemoveCO(currentBottle);
                
                if (bottlePocket.AddCO(replacement, false, true, true) == null)
                {
                    Debug.LogWarning("[EVAO2BottleSwap] Failed to attach replacement O2 bottle to suit");
                    return new EffectResult(true);
                }

                // Place the depleted bottle where the replacement was found
                CondOwner replacementParent = replacement.objCOParent;
                if (replacementParent != null)
                {
                    replacementParent.AddCO(currentBottle, false, true, true);
                }
                else
                {
                    // If no parent, place it on the ship at the same location
                    replacement.ship?.AddCO(currentBottle, true);
                    currentBottle.tf.position = replacement.tf.position;
                }

                bottlePocket.objContainer.Redraw();
                Debug.Log($"[EVAO2BottleSwap] {interaction.objUs.strNameFriendly} swapped to {replacement.strNameFriendly}");

                // Debug information for parent and ship of both current and replacement bottles
                string currentBottleParent = currentBottle?.objCOParent != null ? currentBottle.objCOParent.strNameFriendly : "null";
                string currentBottleShip = currentBottle?.ship != null ? currentBottle.ship.publicName : "null";
                string replacementParentName = replacement?.objCOParent != null ? replacement.objCOParent.strNameFriendly : "null";
                string replacementShip = replacement?.ship != null ? replacement.ship.publicName : "null";

                Debug.Log($"[EVAO2BottleSwap] currentBottle parent: {currentBottleParent}, ship: {currentBottleShip}");
                Debug.Log($"[EVAO2BottleSwap] replacement parent: {replacementParentName}, ship: {replacementShip}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EVAO2BottleSwap] Exception: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                _targetBottles.Remove(interaction.id);
            }

            return new EffectResult(true);
        }

        private static CondOwner ResolvePreferredBottle(Interaction interaction)
        {
            if (interaction == null)
            {
                return null;
            }

            CondOwner bottle = interaction.obj3rd;
            if (bottle == null && interaction.objThem != null && CtEVABottle.Triggered(interaction.objThem, null, false))
            {
                bottle = interaction.objThem;
            }

            return bottle;
        }

        private static CondOwner SelectReplacementBottle(CondOwner actor, CondOwner currentBottle, CondOwner preferred)
        {
            if (actor?.ship == null)
            {
                return null;
            }

            // If a preferred bottle was specified and is valid, use it
            if (preferred != null && preferred != currentBottle)
            {
                double o2Max = preferred.GetCondAmount("StatRef");
                if (o2Max > 0)
                {
                    double o2 = preferred.GetCondAmount("StatGasMolO2");
                    double percent = o2 / o2Max;
                    if (percent > 0.5) // At least 50% charge
                    {
                        return preferred;
                    }
                }
            }

            // Search for O2 bottles in stockpile zones
            List<JsonZone> zones = actor.ship.GetZones("IsZoneStockpile", actor, true, false);
            if (zones == null || zones.Count == 0)
            {
                Debug.Log($"[EVAO2BottleSwap] No stockpile zones found on {actor.ship.strRegID}");
                return null;
            }

            CondOwner best = null;
            double bestPercent = -1.0;

            foreach (JsonZone zone in zones)
            {
                List<CondOwner> bottlesInZone = actor.ship.GetCOsInZone(zone, CtEVABottle, false, true);
                
                foreach (CondOwner bottle in bottlesInZone)
                {
                    if (bottle == null || bottle == currentBottle)
                    {
                        continue;
                    }

                    // Skip bottles that are being carried or are in containers
                    if (bottle.objCOParent != null)
                    {
                        continue;
                    }

                    double o2Max = bottle.GetCondAmount("StatRef");
                    if (o2Max <= 0)
                    {
                        continue;
                    }

                    double o2 = bottle.GetCondAmount("StatGasMolO2");
                    double percent = o2 / o2Max;
                    
                    if (percent > bestPercent)
                    {
                        bestPercent = percent;
                        best = bottle;
                    }
                }
            }

            if (best != null)
            {
                Debug.Log($"[EVAO2BottleSwap] Found best O2 bottle: {best.strNameFriendly} with {bestPercent * 100:F1}% charge");
            }
            else
            {
                Debug.Log($"[EVAO2BottleSwap] No suitable O2 bottles found in stockpile zones");
            }

            return best;
        }
    }
}

