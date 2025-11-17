using System;
using System.Collections.Generic;
using System.Linq;
using Ostranauts.Bit.Interactions;
using UnityEngine;

namespace Ostranauts.Bit.SmarterHauling.Effects
{
    /// <summary>
    /// Handles swapping an EVA suit battery with a charged one from an EVA charger.
    /// Interaction context: ACTSwapEVABattery
    /// </summary>
    public class EVABatteryChargerSwapEffect : EVAMaintenanceEffectBase
    {
        private const string InteractionName = "ACTSwapEVABattery";

        private static readonly Dictionary<Guid, CondOwner> _chargers = new Dictionary<Guid, CondOwner>();
        private static readonly Dictionary<Guid, CondOwner> _targetBatteries = new Dictionary<Guid, CondOwner>();

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

            CondOwner charger = ResolveCharger(interaction);
            CondOwner preferredBattery = ResolvePreferredBattery(interaction);

            if (charger != null)
            {
                _chargers[interaction.id] = charger;
            }

            if (preferredBattery != null)
            {
                _targetBatteries[interaction.id] = preferredBattery;
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
                    Debug.LogWarning("[EVABatteryChargerSwap] Invalid interaction context");
                    return new EffectResult(true);
                }

                // Check if actor is in player's company
                // NPCs not in player's company are handled by the pledge (cheat charge)
                // so this effect should only run for player's company members
                if (!IsInPlayerCompany(interaction.objUs))
                {
                    Debug.Log($"[EVABatteryChargerSwap] {interaction.objUs.strNameFriendly} is not in player's company, skipping");
                    return new EffectResult(true);
                }

                CondOwner charger = ResolveCharger(interaction);
                if (charger == null)
                {
                    Debug.LogWarning("[EVABatteryChargerSwap] Charger not found");
                    return new EffectResult(true);
                }

                CondOwner preferredBattery = null;
                _targetBatteries.TryGetValue(interaction.id, out preferredBattery);

                CondOwner suit = FindEVASuit(interaction.objUs);
                if (suit == null || suit.compSlots == null)
                {
                    Debug.LogWarning("[EVABatteryChargerSwap] EVA suit not found for actor");
                    return new EffectResult(true);
                }

                if (!TryGetSuitBattery(suit, out CondOwner currentBattery, out CondOwner batteryPocket))
                {
                    Debug.LogWarning("[EVABatteryChargerSwap] Suit does not have an accessible battery");
                    return new EffectResult(true);
                }

                CondOwner replacement = SelectReplacementBattery(charger, currentBattery, preferredBattery);
                if (replacement == null)
                {
                    Debug.LogWarning("[EVABatteryChargerSwap] No suitable replacement battery found in charger");
                    return new EffectResult(true);
                }

                // Remove the current (depleted) battery from the suit compartment
                batteryPocket.RemoveCO(currentBattery);
                
                if (batteryPocket.AddCO(replacement, false, true, true) == null)
                {
                    Debug.LogWarning("[EVABatteryChargerSwap] Failed to attach replacement battery to suit");
                    return new EffectResult(true);
                }

                charger.AddCO(currentBattery, false, true, true);

                charger.objContainer.Redraw();
                batteryPocket.objContainer.Redraw();
                Debug.Log($"[EVABatteryChargerSwap] {interaction.objUs.strNameFriendly} swapped to {replacement.strNameFriendly}");
            // Debug information for parent and ship of both current and replacement batteries
                string currentBatteryParent = currentBattery?.objCOParent != null ? currentBattery.objCOParent.strNameFriendly : "null";
                string currentBatteryShip = currentBattery?.ship != null ? currentBattery.ship.publicName : "null";
                string replacementParent = replacement?.objCOParent != null ? replacement.objCOParent.strNameFriendly : "null";
                string replacementShip = replacement?.ship != null ? replacement.ship.publicName : "null";

                Debug.Log($"[EVABatteryChargerSwap] currentBattery parent: {currentBatteryParent}, ship: {currentBatteryShip}");
                Debug.Log($"[EVABatteryChargerSwap] replacement parent: {replacementParent}, ship: {replacementShip}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EVABatteryChargerSwap] Exception: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                _chargers.Remove(interaction.id);
                _targetBatteries.Remove(interaction.id);
            }

            return new EffectResult(true);
        }

        private static CondOwner ResolveCharger(Interaction interaction)
        {
            if (interaction == null)
            {
                return null;
            }

            if (_chargers.TryGetValue(interaction.id, out CondOwner cached) && cached != null)
            {
                return cached;
            }

            CondOwner charger = interaction.objThem;
            if (charger != null && CtBattery.Triggered(charger, null, false))
            {
                charger = charger.objCOParent ?? charger;
            }

            if (charger != null && !charger.HasCond("IsChargerEVA"))
            {
                // If the charger is inside another container, walk up to find the charger body
                CondOwner parent = charger.objCOParent;
                while (parent != null && !parent.HasCond("IsChargerEVA"))
                {
                    parent = parent.objCOParent;
                }

                if (parent != null)
                {
                    charger = parent;
                }
            }

            return charger;
        }

        private static CondOwner ResolvePreferredBattery(Interaction interaction)
        {
            if (interaction == null)
            {
                return null;
            }

            CondOwner battery = interaction.obj3rd;
            if (battery == null && interaction.objThem != null && CtBattery.Triggered(interaction.objThem, null, false))
            {
                battery = interaction.objThem;
            }

            return battery;
        }


        private static CondOwner SelectReplacementBattery(CondOwner charger, CondOwner currentBattery, CondOwner preferred)
        {
            if (charger == null)
            {
                return null;
            }

            List<CondOwner> candidates = charger.GetCOs(true, CtBattery);
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            if (preferred != null && preferred != currentBattery && candidates.Contains(preferred))
            {
                return preferred;
            }

            CondOwner best = null;
            double bestPercent = -1.0;

            foreach (CondOwner battery in candidates)
            {
                if (battery == null || battery == currentBattery)
                {
                    continue;
                }

                double max = battery.GetCondAmount("StatPowerMax");
                if (max <= 0)
                {
                    continue;
                }

                double percent = battery.GetCondAmount("StatPower") / max;
                if (percent > bestPercent)
                {
                    bestPercent = percent;
                    best = battery;
                }
            }

            return best;
        }
    }
}
