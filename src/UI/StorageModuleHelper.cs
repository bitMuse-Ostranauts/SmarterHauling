using Ostranauts.Bit.SmarterHauling.Extensions;

namespace Ostranauts.Bit.SmarterHauling.UI
{
    /// <summary>
    /// Helper class for determining if storage settings module should be shown
    /// </summary>
    public static class StorageModuleHelper
    {
        /// <summary>
        /// List of portable container IDs that are allowed to show storage settings even when loose.
        /// Includes smart crates, toolboxes, backpacks, and totes.
        /// </summary>
        private static readonly string[] AllowedPortableContainers = new string[]
        {
            // Crates (smart crates)
            "ItmCrate01",
            "ItmCrate01Lock",
            
            // Toolboxes (Super Handy, etc.)
            "ItmToolBox01",
            "ItmToolBox02",
            
            // Backpacks and Totes
            "ItmBackpack02",
            "ItmBackpack03"
        };

        /// <summary>
        /// Checks if a CondOwner is an allowed portable container.
        /// </summary>
        /// <param name="co">The CondOwner to check</param>
        /// <returns>True if the CondOwner is an allowed portable container</returns>
        private static bool IsAllowedPortableContainer(CondOwner co)
        {
            if (co == null || string.IsNullOrEmpty(co.strCODef))
            {
                return false;
            }

            string itemId = co.strCODef;
            foreach (string allowedId in AllowedPortableContainers)
            {
                if (itemId == allowedId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines if the storage settings module should be shown for a given CondOwner.
        /// The module is shown if:
        /// - The CondOwner has a Container component
        /// - The CondOwner is not carried (doesn't have IsCarried condition)
        /// - The CondOwner is not damaged (doesn't have IsDamaged condition)
        /// - The CondOwner is not loose (doesn't match TIsLoose condition trigger), UNLESS it's an allowed portable container
        /// - The CondOwner is not locked (doesn't have Locked condition)
        /// - The CondOwner is not a trader NPC (doesn't have IsTraderNPC condition)
        /// </summary>
        /// <param name="co">The CondOwner to check</param>
        /// <returns>True if the module should be shown, false otherwise</returns>
        public static bool ShouldShowStorageModule(CondOwner co)
        {
            // Basic null check
            if (co == null)
            {
                return false;
            }

            // Must have a container
            Container container = co.GetContainer();
            if (container == null)
            {
                return false;
            }

            // Don't show if item is carried
            if (co.HasCond("IsCarried"))
            {
                if (SmarterHaulingPlugin.EnableDebugLogging)
                {
                    SmarterHaulingPlugin.Logger.LogDebug(
                        $"[StorageModule] Hiding module for {co.strNameFriendly} - item is carried"
                    );
                }
                return false;
            }

            // Don't show if item is being dragged
            if (co.slotNow != null && co.slotNow.strName == "drag")
            {
                if (SmarterHaulingPlugin.EnableDebugLogging)
                {
                    SmarterHaulingPlugin.Logger.LogDebug(
                        $"[StorageModule] Hiding module for {co.strNameFriendly} - item is being dragged"
                    );
                }
                return false;
            }

            // Don't show if item is damaged
            if (co.HasCond("IsDamaged"))
            {
                if (SmarterHaulingPlugin.EnableDebugLogging)
                {
                    SmarterHaulingPlugin.Logger.LogDebug(
                        $"[StorageModule] Hiding module for {co.strNameFriendly} - item is damaged"
                    );
                }
                return false;
            }

            // Don't show if item is loose (not installed), UNLESS it's an allowed portable container
            CondTrigger ctLoose = DataHandler.GetCondTrigger("TIsLoose");
            if (ctLoose != null && ctLoose.Triggered(co, null, false))
            {
                // Check if this is an allowed portable container
                if (IsAllowedPortableContainer(co))
                {
                    if (SmarterHaulingPlugin.EnableDebugLogging)
                    {
                        SmarterHaulingPlugin.Logger.LogDebug(
                            $"[StorageModule] Allowing module for portable container {co.strNameFriendly} (def:{co.strCODef}) even though it's loose"
                        );
                    }
                    // Continue with other checks - don't return false here
                }
                else
                {
                    if (SmarterHaulingPlugin.EnableDebugLogging)
                    {
                        SmarterHaulingPlugin.Logger.LogDebug(
                            $"[StorageModule] Hiding module for {co.strNameFriendly} - item is loose"
                        );
                    }
                    return false;
                }
            }

            // Don't show if item is locked
            if (co.HasCond("Locked"))
            {
                if (SmarterHaulingPlugin.EnableDebugLogging)
                {
                    SmarterHaulingPlugin.Logger.LogDebug(
                        $"[StorageModule] Hiding module for {co.strNameFriendly} - item is locked"
                    );
                }
                return false;
            }

            // Don't show if this is a trader NPC container
            if (co.HasCond("IsTraderNPC"))
            {
                if (SmarterHaulingPlugin.EnableDebugLogging)
                {
                    SmarterHaulingPlugin.Logger.LogDebug(
                        $"[StorageModule] Hiding module for {co.strNameFriendly} - is trader NPC"
                    );
                }
                return false;
            }

            // All checks passed - show the module
            return true;
        }
    }
}

