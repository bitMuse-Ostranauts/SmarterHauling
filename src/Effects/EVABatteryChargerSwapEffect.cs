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
    public class EVABatteryChargerSwapEffect : IEffect
    {
        private const string InteractionName = "ACTSwapEVABattery";
        private const string SuitBatterySlot = "pocket_EVABatt";

        private static readonly Dictionary<Guid, CondOwner> _chargers = new Dictionary<Guid, CondOwner>();
        private static readonly Dictionary<Guid, CondOwner> _targetBatteries = new Dictionary<Guid, CondOwner>();
        private static CondTrigger _ctBattery;
        private static CondTrigger _ctEVAOn;

        private static CondTrigger CtBattery
        {
            get
            {
                if (_ctBattery == null)
                {
                    _ctBattery = DataHandler.GetCondTrigger("TIsFitContainerEVABattery");
                }
                return _ctBattery;
            }
        }

        private static CondTrigger CtEVAOn
        {
            get
            {
                if (_ctEVAOn == null)
                {
                    _ctEVAOn = DataHandler.GetCondTrigger("TIsEVAOn");
                }
                return _ctEVAOn;
            }
        }

        public bool ShouldPrepare(Interaction interaction)
        {
            return interaction != null && interaction.strName == InteractionName;
        }

        public void Prepare(Interaction interaction)
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

        public bool ShouldExecute(Interaction interaction)
        {
            return ShouldPrepare(interaction);
        }

        public EffectResult Execute(Interaction interaction)
        {
            try
            {
                if (interaction?.objUs == null)
                {
                    Debug.LogWarning("[EVABatteryChargerSwap] Invalid interaction context");
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

        private static CondOwner FindEVASuit(CondOwner actor)
        {
            if (actor?.compSlots == null)
            {
                return null;
            }

            List<CondOwner> suits = actor.compSlots.GetCOs("shirt_out", false, CtEVAOn);
            return suits?.FirstOrDefault();
        }

        private static bool TryGetSuitBattery(CondOwner suit, out CondOwner battery, out CondOwner batteryPocket)
        {
            battery = null;
            batteryPocket = null;

            if (suit == null || suit.compSlots == null)
            {
                return false;
            }

            foreach (Slot slot in suit.GetSlots(true))
            {
                foreach (CondOwner pocket in slot.aCOs) 
                {
                    if (!pocket.HasSubCOs)
                        continue;

                    foreach (CondOwner candidate in pocket.GetCOs(true, CtBattery)) 
                    {
                        battery = candidate;
                        batteryPocket = pocket;
                        return true;
                    }
                }
            }

            return false;
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
