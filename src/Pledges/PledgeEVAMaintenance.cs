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
    public class PledgeEVAMaintenance : BasePledgeFindItem
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
        private CondTrigger _ctEVACharger;
        private CondTrigger _ctContainer;

        // Emergency tracking
        private bool _batteryEmergency = false;
        private bool _o2Emergency = false;
        
        // Cooldown tracking
        private double _lastCheckTime = -1000.0;

        protected override CondTrigger EmergencyConditions
        {
            get
            {
                // Custom emergency logic - see IsEmergency() method
                return null;
            }
        }

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

        private CondTrigger CtEVACharger
        {
            get
            {
                if (_ctEVACharger == null)
                {
                    _ctEVACharger = DataHandler.GetCondTrigger("TIsEVACharger");
                }
                return _ctEVACharger;
            }
        }

        private CondTrigger CtContainer
        {
            get
            {
                if (_ctContainer == null)
                {
                    _ctContainer = DataHandler.GetCondTrigger("TIsContainer");
                }
                return _ctContainer;
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
                return true;
            }

            if (base.Us.ship == null)
            {
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
                return false;
            }

            // Check cooldown timer (only check every 30 seconds)
            double currentTime = StarSystem.fEpoch;
            if (currentTime - _lastCheckTime < COOLDOWN_TIME)
            {
                return false;
            }
            
            _lastCheckTime = currentTime;

            // Check current battery and O2 levels
            CheckEVALevels(out double batteryPercent, out double o2Percent);

            // Check for emergency
            bool batteryEmergency = batteryPercent > 0 && batteryPercent < EMERGENCY_THRESHOLD;
            bool o2Emergency = o2Percent > 0 && o2Percent < EMERGENCY_THRESHOLD;

            // Priority 1: Handle battery emergency
            if (batteryEmergency)
            {
                CondOwner replacementBattery = FindReplacementBattery();
                if (replacementBattery != null)
                {
                    QueueBatterySwap(replacementBattery);
                    return true;
                }
                else if (IsAtmosphereBreathable())
                {
                    // Remove helmet as emergency measure
                    RemoveHelmet();
                    return true;
                }
            }

            // Priority 2: Handle O2 emergency
            if (o2Emergency)
            {
                CondOwner replacementO2 = FindReplacementO2Bottle();
                if (replacementO2 != null)
                {
                    QueueO2Swap(replacementO2);
                    return true;
                }
                else if (IsAtmosphereBreathable())
                {
                    // Remove helmet as emergency measure
                    RemoveHelmet();
                    return true;
                }
            }

            // Priority 3: Handle low battery (not emergency)
            if (batteryPercent > 0 && batteryPercent < NORMAL_THRESHOLD)
            {
                CondOwner replacementBattery = FindReplacementBattery();
                if (replacementBattery != null)
                {
                    QueueBatterySwap(replacementBattery);
                    return true;
                }
            }

            // Priority 4: Handle low O2 (not emergency)
            if (o2Percent > 0 && o2Percent < NORMAL_THRESHOLD)
            {
                CondOwner replacementO2 = FindReplacementO2Bottle();
                if (replacementO2 != null)
                {
                    QueueO2Swap(replacementO2);
                    return true;
                }
            }

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
        private CondOwner FindReplacementBattery()
        {
            if (base.Us == null || base.Us.ship == null)
            {
                return null;
            }

            List<Ship> shipsToSearch = new List<Ship>();
            
            // Always search current ship
            shipsToSearch.Add(base.Us.ship);

            // In emergency, also search owned docked ships
            if (_batteryEmergency)
            {
                foreach (Ship ship in base.Us.ship.GetAllDockedShips())
                {
                    if (base.Us.OwnsShip(ship.strRegID))
                    {
                        shipsToSearch.Add(ship);
                    }
                }
            }

            // Search for EVA chargers with good batteries
            foreach (Ship ship in shipsToSearch)
            {
                List<CondOwner> chargers = ship.GetCOs(CtEVACharger, true, false, false);
                foreach (CondOwner charger in chargers)
                {
                    if (charger == null)
                        continue;

                    // Get batteries from the charger
                    List<CondOwner> batteries = charger.GetCOs(false, CtEVABattery);
                    if (batteries == null)
                        continue;

                    foreach (CondOwner battery in batteries)
                    {
                        if (!IsValidReplacement(battery))
                            continue;

                        // Check battery charge level (use StatPowerMax to avoid triggering updates)
                        double powerMax = battery.GetCondAmount("StatPowerMax");

                        if (powerMax > 0)
                        {
                            double power = battery.GetCondAmount("StatPower");
                            double percent = power / powerMax;

                            if (percent >= REPLACEMENT_MIN_THRESHOLD && IsReachable(base.Us, battery))
                            {
                                return battery;
                            }
                        }
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
        private void QueueBatterySwap(CondOwner replacementBattery)
        {
            if (replacementBattery == null)
                return;

            Interaction interaction = DataHandler.GetInteraction("SeekEVABattery", null, false);
            if (interaction != null)
            {
                base.Us.QueueInteraction(replacementBattery, interaction, true);
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
                base.Us.QueueInteraction(replacementO2, interaction, true);
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

