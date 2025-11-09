using Ostranauts.Bit.SmarterHauling.Extensions;

namespace Ostranauts.Bit.SmarterHauling.UI
{
    /// <summary>
    /// Helper class for determining if storage settings module should be shown
    /// </summary>
    public static class StorageModuleHelper
    {
        /// <summary>
        /// Determines if the storage settings module should be shown for a given CondOwner.
        /// The module is shown if:
        /// - The CondOwner has a Container component
        /// - The CondOwner is not carried (doesn't have IsCarried condition)
        /// - The CondOwner is not damaged (doesn't have IsDamaged condition)
        /// - The CondOwner is not loose (doesn't match TIsLoose condition trigger)
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

            // Don't show if item is loose (not installed)
            CondTrigger ctLoose = DataHandler.GetCondTrigger("TIsLoose");
            if (ctLoose != null && ctLoose.Triggered(co, null, false))
            {
                if (SmarterHaulingPlugin.EnableDebugLogging)
                {
                    SmarterHaulingPlugin.Logger.LogDebug(
                        $"[StorageModule] Hiding module for {co.strNameFriendly} - item is loose"
                    );
                }
                return false;
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

