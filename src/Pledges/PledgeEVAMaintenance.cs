using System;
using System.Collections.Generic;
using UnityEngine;
using Ostranauts.Pledges;

namespace Ostranauts.Bit.SmarterHauling.Pledges
{
    /// <summary>
    /// Pledge that manages EVA suit maintenance - automatically swaps low batteries and O2 bottles.
    /// In emergency situations with no replacements available, removes helmet if atmosphere is breathable.
    /// </summary>
    public class PledgeEVAMaintenance : Pledge2
    {
        // Thresholds
        private const double NORMAL_THRESHOLD = 0.25; // 25%
        private const double EMERGENCY_THRESHOLD = 0.05; // 5%
        private const double REPLACEMENT_MIN_THRESHOLD = 0.50; // 50% - minimum for replacement items
        private const double COOLDOWN_TIME = 30.0; // Check every 30 seconds

        // Condition triggers (cached for performance)
        private CondTrigger _ctEVAOn;
        private CondTrigger _ctEVABattery;
        private CondTrigger _ctEVABottle;

        private sealed class BatteryCandidate
        {
            public CondOwner Charger { get; }
            public CondOwner Battery { get; }

            public BatteryCandidate(CondOwner charger, CondOwner battery)
            {
                Charger = charger;
                Battery = battery;
            }
        }

        // Emergency tracking
        private bool _batteryEmergency = false;
        private bool _o2Emergency = false;
        
        // Cooldown tracking
        private double _lastCheckTime = -1000.0;

        private CondTrigger CtEVAOn
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

        private CondTrigger CtEVABattery
        {
            get
            {
                if (_ctEVABattery == null)
                {
                    _ctEVABattery = DataHandler.GetCondTrigger("TIsFitContainerEVABattery");
                }
                return _ctEVABattery;
            }
        }

        private CondTrigger CtEVABottle
        {
            get
            {
                if (_ctEVABottle == null)
                {
                    _ctEVABottle = DataHandler.GetCondTrigger("TIsFitContainerEVABottle");
                }
                return _ctEVABottle;
            }
        }

        public override bool IsEmergency()
        {
            if (base.Us == null || base.Us.HasCond("IsAIManual"))
            {
                return false;
            }

            // Check if battery or O2 is in emergency state
            CheckEVALevels(out double batteryPercent, out double o2Percent);
            
            _batteryEmergency = batteryPercent > 0 && batteryPercent < EMERGENCY_THRESHOLD;
            _o2Emergency = o2Percent > 0 && o2Percent < EMERGENCY_THRESHOLD;

            return _batteryEmergency || _o2Emergency;
        }

        public override bool Do()
        {
            if (base.Us == null || base.Us.HasCond("IsAIManual"))
            {
                return false;
            }

            if (this.Finished())
            {
                Debug.Log($"[EVAMaintenance] {base.Us?.strNameFriendly} - Pledge finished");
                return true;
            }

            if (base.Us.ship == null)
            {
                Debug.Log($"[EVAMaintenance] {base.Us?.strNameFriendly} - No ship");
                return false;
            }

            // Check for combat or if busy with another interaction
            if (base.Us.HasCond("IsInCombat") || base.Us.GetInteractionCurrent() != null)
            {
                return false;
            }

            // Check if wearing EVA suit
            if (!IsWearingEVASuit())
            {
                Debug.Log($"[EVAMaintenance] {base.Us?.strNameFriendly} - Not wearing EVA suit");
                return false;
            }

            // Check cooldown timer (only check every 30 seconds)
            double currentTime = StarSystem.fEpoch;
            if (currentTime - _lastCheckTime < COOLDOWN_TIME)
            {
                return false;
            }
            
            _lastCheckTime = currentTime;
            Debug.Log($"[EVAMaintenance] {base.Us?.strNameFriendly} - Starting EVA maintenance check");

            // Check current battery and O2 levels
            CheckEVALevels(out double batteryPercent, out double o2Percent);
            
            Debug.Log($"[EVAMaintenance] {base.Us?.strNameFriendly} - Battery: {(batteryPercent >= 0 ? $"{batteryPercent * 100:F1}%" : "N/A")}, O2: {(o2Percent >= 0 ? $"{o2Percent * 100:F1}%" : "N/A")}");

            // Check for emergency
            bool batteryEmergency = batteryPercent > 0 && batteryPercent < EMERGENCY_THRESHOLD;
            bool o2Emergency = o2Percent > 0 && o2Percent < EMERGENCY_THRESHOLD;

            // Priority 1: Handle battery emergency
            if (batteryEmergency)
            {
                Debug.Log($"[EVAMaintenance] {base.Us?.strNameFriendly} - BATTERY EMERGENCY! Searching for replacement...");
                BatteryCandidate replacementBattery = FindReplacementBattery();
                if (replacementBattery != null)
                {
                    Debug.Log($"[EVAMaintenance] {base.Us?.strNameFriendly} - Found replacement battery: {replacementBattery.Battery.strNameFriendly}");
                    QueueBatterySwap(replacementBattery);
                    return true;
                }
                else if (IsAtmosphereBreathable())
                {
                    Debug.Log($"[EVAMaintenance] {base.Us?.strNameFriendly} - No battery found, atmosphere breathable, removing helmet");
                    RemoveHelmet();
                    return true;
                }
                else
                {
                    Debug.Log($"[EVAMaintenance] {base.Us?.strNameFriendly} - No battery found and atmosphere NOT breathable!");
                }
            }

            // Priority 2: Handle O2 emergency
            if (o2Emergency)
            {
                Debug.Log($"[EVAMaintenance] {base.Us?.strNameFriendly} - O2 EMERGENCY! Searching for replacement...");
                CondOwner replacementO2 = FindReplacementO2Bottle();
                if (replacementO2 != null)
                {
                    Debug.Log($"[EVAMaintenance] {base.Us?.strNameFriendly} - Found replacement O2: {replacementO2.strNameFriendly}");
                    QueueO2Swap(replacementO2);
                    return true;
                }
                else if (IsAtmosphereBreathable())
                {
                    Debug.Log($"[EVAMaintenance] {base.Us?.strNameFriendly} - No O2 found, atmosphere breathable, removing helmet");
                    RemoveHelmet();
                    return true;
                }
                else
                {
                    Debug.Log($"[EVAMaintenance] {base.Us?.strNameFriendly} - No O2 found and atmosphere NOT breathable!");
                }
            }

            // Priority 3: Handle low battery (not emergency)
            if (batteryPercent > 0 && batteryPercent < NORMAL_THRESHOLD)
            {
                Debug.Log($"[EVAMaintenance] {base.Us?.strNameFriendly} - Battery low (<25%), searching for replacement...");
                BatteryCandidate replacementBattery = FindReplacementBattery();
                if (replacementBattery != null)
                {
                    Debug.Log($"[EVAMaintenance] {base.Us?.strNameFriendly} - Found replacement battery: {replacementBattery.Battery.strNameFriendly}");
                    QueueBatterySwap(replacementBattery);
                    return true;
                }
                else
                {
                    Debug.Log($"[EVAMaintenance] {base.Us?.strNameFriendly} - No suitable replacement battery found");
                }
            }

            // Priority 4: Handle low O2 (not emergency)
            if (o2Percent > 0 && o2Percent < NORMAL_THRESHOLD)
            {
                Debug.Log($"[EVAMaintenance] {base.Us?.strNameFriendly} - O2 low (<25%), searching for replacement...");
                CondOwner replacementO2 = FindReplacementO2Bottle();
                if (replacementO2 != null)
                {
                    Debug.Log($"[EVAMaintenance] {base.Us?.strNameFriendly} - Found replacement O2: {replacementO2.strNameFriendly}");
                    QueueO2Swap(replacementO2);
                    return true;
                }
                else
                {
                    Debug.Log($"[EVAMaintenance] {base.Us?.strNameFriendly} - No suitable replacement O2 found");
                }
            }

            Debug.Log($"[EVAMaintenance] {base.Us?.strNameFriendly} - EVA levels OK, no action needed");
            return false;
        }

        /// <summary>
        /// Check if the character is wearing an EVA suit
        /// </summary>
        private bool IsWearingEVASuit()
        {
            if (base.Us == null || base.Us.compSlots == null)
            {
                return false;
            }

            // Check for EVA suit in the "shirt_out" slot
            List<CondOwner> evaSuits = base.Us.compSlots.GetCOs("shirt_out", false, CtEVAOn);
            return evaSuits != null && evaSuits.Count > 0;
        }

        /// <summary>
        /// Get the current battery and O2 levels from the EVA suit
        /// </summary>
        private void CheckEVALevels(out double batteryPercent, out double o2Percent)
        {
            batteryPercent = -1;
            o2Percent = -1;

            if (base.Us == null || base.Us.compSlots == null)
            {
                return;
            }

            // Get EVA suit
            List<CondOwner> evaSuits = base.Us.compSlots.GetCOs("shirt_out", false, CtEVAOn);
            if (evaSuits == null || evaSuits.Count == 0)
            {
                return;
            }

            CondOwner evaSuit = evaSuits[0];
            List<CondOwner> components = evaSuit.GetCOs(false, null);

            if (components == null)
            {
                return;
            }

            // Check each component
            foreach (CondOwner component in components)
            {
                // Check for battery
                if (CtEVABattery.Triggered(component, null, false))
                {
                    // Use StatPowerMax directly to avoid triggering Powered component updates
                    double powerMax = component.GetCondAmount("StatPowerMax");
                    
                    if (powerMax > 0)
                    {
                        double power = component.GetCondAmount("StatPower");
                        batteryPercent = power / powerMax;
                    }
                }
                // Check for O2 bottle
                else if (CtEVABottle.Triggered(component, null, false))
                {
                    double o2Max = component.GetCondAmount("StatRef");
                    if (o2Max > 0)
                    {
                        double o2 = component.GetCondAmount("StatGasMolO2");
                        o2Percent = o2 / o2Max;
                    }
                }
            }
        }

        /// <summary>
        /// Find a replacement battery with at least 50% charge
        /// </summary>
        private BatteryCandidate FindReplacementBattery()
        {
            if (base.Us == null || base.Us.ship == null)
            {
                return null;
            }

            List<Ship> shipsToSearch = new List<Ship>();
            
            // Always search current ship
            shipsToSearch.Add(base.Us.ship);
            Debug.Log($"[EVAMaintenance] {base.Us?.strNameFriendly} - Searching for battery on {base.Us.ship.strRegID}");

            // In emergency, also search owned docked ships
            if (_batteryEmergency)
            {
                foreach (Ship ship in base.Us.ship.GetAllDockedShips())
                {
                    if (base.Us.OwnsShip(ship.strRegID))
                    {
                        shipsToSearch.Add(ship);
                        Debug.Log($"[EVAMaintenance] {base.Us?.strNameFriendly} - Also searching owned ship {ship.strRegID}");
                    }
                }
            }

            // Search for EVA batteries that are sitting in chargers
            foreach (Ship ship in shipsToSearch)
            {
                List<CondOwner> batteries = ship.GetCOs(CtEVABattery, true, false, true);
                Debug.Log($"[EVAMaintenance] {base.Us?.strNameFriendly} - Found {batteries?.Count ?? 0} EVA batteries on {ship.strRegID}");

                foreach (CondOwner battery in batteries)
                {
                    string parentName = battery.objCOParent?.strNameFriendly ?? "null";
                    string parentCode = battery.objCOParent?.strCODef ?? "null";
                    Debug.Log($"[EVAMaintenance]    Candidate battery: {battery.strNameFriendly} ({battery.strCODef}) parent={parentName} ({parentCode})");

                    if (!IsValidReplacement(battery))
                    {
                        Debug.Log($"[EVAMaintenance]    -> rejected: not valid replacement (likely being carried)");
                        continue;
                    }

                    CondOwner parent = battery.objCOParent;

                    // Battery must have a parent charger/container
                    if (parent == null)
                    {
                        Debug.Log($"[EVAMaintenance]    -> rejected: no parent container");
                        continue;
                    }

                    // Skip batteries already installed in EVA suits or worn
                    if (CtEVAOn != null && CtEVAOn.Triggered(parent, null, false))
                    {
                        Debug.Log($"[EVAMaintenance]    -> rejected: parent appears to be an EVA suit component");
                        continue;
                    }

                    // Ensure the battery is sitting in an EVA charger we have access to
                    if (!IsEVACharger(parent))
                    {
                        Debug.Log($"[EVAMaintenance]    -> rejected: parent not recognised as EVA charger");
                        continue;
                    }

                    // Check battery charge level (use StatPowerMax to avoid triggering updates)
                    double powerMax = battery.GetCondAmount("StatPowerMax");

                    if (powerMax > 0)
                    {
                        double power = battery.GetCondAmount("StatPower");
                        double percent = power / powerMax;

                        CondOwner targetForPath = parent ?? battery;

                        bool reachable = IsReachable(base.Us, targetForPath);
                        if (percent >= REPLACEMENT_MIN_THRESHOLD && reachable)
                        {
                            Debug.Log($"[EVAMaintenance]    -> accepted: {percent * 100:F1}% charged and reachable");
                            return new BatteryCandidate(parent, battery);
                        }
                        else
                        {
                            Debug.Log($"[EVAMaintenance]    -> rejected: percent={percent * 100:F1}% reachable={reachable}");
                        }
                    }
                    else
                    {
                        Debug.Log($"[EVAMaintenance]    -> rejected: StatPowerMax <= 0");
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Find a replacement O2 bottle with at least 50% capacity
        /// </summary>
        private CondOwner FindReplacementO2Bottle()
        {
            if (base.Us == null || base.Us.ship == null)
            {
                return null;
            }

            List<Ship> shipsToSearch = new List<Ship>();
            
            // Always search current ship
            shipsToSearch.Add(base.Us.ship);

            // In emergency, also search owned docked ships
            if (_o2Emergency)
            {
                foreach (Ship ship in base.Us.ship.GetAllDockedShips())
                {
                    if (base.Us.OwnsShip(ship.strRegID))
                    {
                        shipsToSearch.Add(ship);
                    }
                }
            }

            // Search for O2 bottles in containers/racks
            foreach (Ship ship in shipsToSearch)
            {
                List<CondOwner> bottles = ship.GetCOs(CtEVABottle, true, false, false);
                foreach (CondOwner bottle in bottles)
                {
                    if (!IsValidReplacement(bottle))
                        continue;

                    // Check O2 level
                    double o2Max = bottle.GetCondAmount("StatRef");
                    if (o2Max > 0)
                    {
                        double o2 = bottle.GetCondAmount("StatGasMolO2");
                        double percent = o2 / o2Max;

                        if (percent >= REPLACEMENT_MIN_THRESHOLD && IsReachable(base.Us, bottle))
                        {
                            return bottle;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Check if an item is a valid replacement (not currently being used, not carried by someone, etc.)
        /// </summary>
        private bool IsValidReplacement(CondOwner item)
        {
            if (item == null)
                return false;

            // Check if item is already being carried/worn
            CondTrigger ctNotCarried = DataHandler.GetCondTrigger("TIsNotCarried");
            if (ctNotCarried != null && !ctNotCarried.Triggered(item, null, true))
                return false;

            return true;
        }

        private bool IsEVACharger(CondOwner condOwner)
        {
            if (condOwner == null)
            {
                Debug.Log($"[EVAMaintenance]    -> charger check failed: null parent");
                return false;
            }

            // Check by code definition
            if (!string.IsNullOrEmpty(condOwner.strCODef) &&
                condOwner.strCODef.IndexOf("ChargerBattEVA", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Debug.Log($"[EVAMaintenance]    -> charger check success via strCODef: {condOwner.strCODef}");
                return true;
            }

            // Fallback: check for identifying conditions
            if (condOwner.HasCond("IsEVACharger") || condOwner.HasCond("IsChargerBattEVA"))
            {
                Debug.Log($"[EVAMaintenance]    -> charger check success via condition");
                return true;
            }

            // Also check friendly name as last resort
            if (!string.IsNullOrEmpty(condOwner.strNameFriendly) &&
                condOwner.strNameFriendly.IndexOf("EVA", StringComparison.OrdinalIgnoreCase) >= 0 &&
                condOwner.strNameFriendly.IndexOf("Charger", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Debug.Log($"[EVAMaintenance]    -> charger check success via friendly name: {condOwner.strNameFriendly}");
                return true;
            }

            Debug.Log($"[EVAMaintenance]    -> charger check failed for parent {condOwner.strNameFriendly} ({condOwner.strCODef})");
            return false;
        }

        /// <summary>
        /// Check if a target is reachable via pathfinding
        /// </summary>
        private bool IsReachable(CondOwner objUs, CondOwner objThem)
        {
            Pathfinder pathfinder = objUs.Pathfinder;
            if (pathfinder != null && objThem != null)
            {
                Vector2 pos = objThem.GetPos("use", false);
                Tile tileAtWorldCoords = objUs.ship.GetTileAtWorldCoords1(pos.x, pos.y, true, true);
                bool bAllowAirlocks = objUs.HasAirlockPermission(false);
                PathResult pathResult = pathfinder.CheckGoal(tileAtWorldCoords, 0f, objThem, bAllowAirlocks);
                return pathResult.HasPath;
            }
            return false;
        }

        /// <summary>
        /// Check if the current atmosphere is breathable (safe to remove helmet)
        /// </summary>
        private bool IsAtmosphereBreathable()
        {
            if (base.Us == null || base.Us.ship == null)
            {
                return false;
            }

            // Get the current tile/room
            Vector2 pos = base.Us.GetPos("use", false);
            Tile tile = base.Us.ship.GetTileAtWorldCoords1(pos.x, pos.y, true, true);
            
            if (tile == null || tile.room == null)
            {
                return false;
            }

            Room room = tile.room;

            // Check for safe oxygen levels
            if (!room.CO.HasCond("DcGasPpO2"))
            {
                return false;
            }

            // Check for dangerous gases
            if (room.CO.HasCond("DcGasPpCO2") || room.CO.HasCond("DcGasPpH2SO4") || 
                room.CO.HasCond("DcGasPpCH4") || room.CO.HasCond("DcGasPpNH3"))
            {
                return false;
            }

            // Check temperature
            if (!room.CO.HasCond("DcGasTemp02"))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Queue the battery swap interaction
        /// </summary>
        private void QueueBatterySwap(BatteryCandidate candidate)
        {
            if (candidate?.Battery == null || base.Us == null)
                return;

            Interaction interaction = DataHandler.GetInteraction("ACTSwapEVABattery", null, false);
            if (interaction == null)
            {
                Debug.LogError($"[EVAMaintenance] {base.Us?.strNameFriendly} - ACTSwapEVABattery interaction not found!");
                return;
            }

            CondOwner charger = candidate.Charger ?? candidate.Battery.objCOParent;

            // interaction.objUs = base.Us;
            // interaction.objThem = charger ?? candidate.Battery;
            // interaction.obj3rd = candidate.Battery;

            Debug.Log($"[EVAMaintenance] {base.Us.strNameFriendly} - Queuing ACTSwapEVABattery interaction targeting {(interaction.objThem?.strNameFriendly ?? "null")}");

            if (!base.Us.QueueInteraction(candidate.Battery, interaction, true))
            {
                Debug.LogWarning($"[EVAMaintenance] {base.Us.strNameFriendly} - Failed to queue ACTSwapEVABattery");
                DataHandler.ReleaseTrackedInteraction(interaction);
            }
        }

        /// <summary>
        /// Queue the O2 bottle swap interaction
        /// </summary>
        private void QueueO2Swap(CondOwner replacementO2)
        {
            if (replacementO2 == null)
                return;

            Interaction interaction = DataHandler.GetInteraction("SeekEVAO2", null, false);
            if (interaction != null)
            {
                interaction.objUs = base.Us;
                interaction.objThem = replacementO2.objCOParent ?? replacementO2;
                interaction.obj3rd = replacementO2;

                Debug.Log($"[EVAMaintenance] {base.Us?.strNameFriendly} - Queuing SeekEVAO2 interaction for {replacementO2.strNameFriendly}");
                if (!base.Us.QueueInteraction(interaction.objThem, interaction, true))
                {
                    Debug.LogWarning($"[EVAMaintenance] {base.Us?.strNameFriendly} - Failed to queue SeekEVAO2 interaction");
                    DataHandler.ReleaseTrackedInteraction(interaction);
                }
            }
            else
            {
                Debug.LogError($"[EVAMaintenance] {base.Us?.strNameFriendly} - SeekEVAO2 interaction not found!");
            }
        }

        /// <summary>
        /// Emergency: Remove helmet if atmosphere is breathable
        /// </summary>
        private void RemoveHelmet()
        {
            if (base.Us == null)
                return;

            // Get the EVA suit helmet
            if (base.Us.compSlots == null)
                return;

            List<CondOwner> helmets = base.Us.compSlots.GetCOs("head_out", false, null);
            if (helmets == null || helmets.Count == 0)
                return;

            CondOwner helmet = helmets[0];
            
            // Queue unequip interaction
            Interaction interaction = DataHandler.GetInteraction("ACTUnequip", null, false);
            if (interaction != null)
            {
                base.Us.QueueInteraction(helmet, interaction, true);
            }
        }
    }
}

