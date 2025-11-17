using System.Collections.Generic;
using System.Linq;

namespace Ostranauts.Bit.SmarterHauling.Utilities
{
    /// <summary>
    /// Utility methods for EVA suit maintenance operations.
    /// </summary>
    public static class EVAUtils
    {
        private static CondTrigger _ctEVAOn;

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

        /// <summary>
        /// Check if the actor is in the player's company.
        /// </summary>
        public static bool IsInPlayerCompany(CondOwner actor)
        {
            if (actor == null)
            {
                return false;
            }

            if (CrewSim.coPlayer == null)
            {
                return false;
            }

            if (actor.Company == null)
            {
                return false;
            }

            return actor.Company == CrewSim.coPlayer.Company;
        }

        /// <summary>
        /// Check if the character is wearing an EVA suit.
        /// </summary>
        public static bool IsWearingEVASuit(CondOwner actor)
        {
            if (actor?.compSlots == null)
            {
                return false;
            }

            // Check for EVA suit in the "shirt_out" slot
            List<CondOwner> evaSuits = actor.compSlots.GetCOs("shirt_out", false, CtEVAOn);
            return evaSuits != null && evaSuits.Count > 0;
        }

        /// <summary>
        /// Find the EVA suit worn by the actor.
        /// </summary>
        public static CondOwner FindEVASuit(CondOwner actor)
        {
            if (actor?.compSlots == null)
            {
                return null;
            }

            List<CondOwner> suits = actor.compSlots.GetCOs("shirt_out", false, CtEVAOn);
            return suits?.FirstOrDefault();
        }
    }
}

