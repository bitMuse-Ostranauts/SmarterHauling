using System;
using System.Collections.Generic;
using System.Linq;
using Ostranauts.Bit.Interactions;
using Ostranauts.Bit.SmarterHauling.Utilities;
using UnityEngine;

namespace Ostranauts.Bit.SmarterHauling.Effects
{
    /// <summary>
    /// Base class for EVA maintenance effects (battery and O2 bottle swapping).
    /// Provides shared functionality like suit detection, company checks, and common helpers.
    /// </summary>
    public abstract class EVAMaintenanceEffectBase : IEffect
    {
        protected const string SuitBatterySlot = "pocket_EVABatt";
        protected const string SuitO2Slot = "PocketEVAO201";

        protected static CondTrigger _ctBattery;
        protected static CondTrigger _ctEVAOn;
        protected static CondTrigger _ctEVABottle;

        protected static CondTrigger CtBattery
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

        protected static CondTrigger CtEVAOn
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

        protected static CondTrigger CtEVABottle
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

        /// <summary>
        /// Check if the actor is in the player's company.
        /// </summary>
        protected static bool IsInPlayerCompany(CondOwner actor) => EVAUtils.IsInPlayerCompany(actor);

        /// <summary>
        /// Find the EVA suit worn by the actor.
        /// </summary>
        protected static CondOwner FindEVASuit(CondOwner actor) => EVAUtils.FindEVASuit(actor);

        /// <summary>
        /// Get the battery from the EVA suit.
        /// </summary>
        protected static bool TryGetSuitBattery(CondOwner suit, out CondOwner battery, out CondOwner batteryPocket)
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

        /// <summary>
        /// Get the O2 bottle from the EVA suit.
        /// </summary>
        protected static bool TryGetSuitO2Bottle(CondOwner suit, out CondOwner bottle, out CondOwner bottlePocket)
        {
            bottle = null;
            bottlePocket = null;

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

                    foreach (CondOwner candidate in pocket.GetCOs(true, CtEVABottle)) 
                    {
                        bottle = candidate;
                        bottlePocket = pocket;
                        return true;
                    }
                }
            }

            return false;
        }

        public abstract bool ShouldPrepare(Interaction interaction);
        public abstract void Prepare(Interaction interaction);
        public abstract bool ShouldExecute(Interaction interaction);
        public abstract EffectResult Execute(Interaction interaction);
    }
}

