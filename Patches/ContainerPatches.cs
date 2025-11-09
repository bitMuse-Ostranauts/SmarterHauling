using HarmonyLib;
using Ostranauts.Bit.SmarterHauling.Extensions;

namespace Ostranauts.Bit.SmarterHauling.Patches
{
    /// <summary>
    /// Patches for Container to add whitelist functionality
    /// </summary>
    [HarmonyPatch(typeof(Container))]
    public class ContainerPatches
    {
        /// <summary>
        /// Patch Container.Destroy to clean up whitelist data when container is destroyed
        /// </summary>
        [HarmonyPatch("Destroy")]
        [HarmonyPrefix]
        public static void Destroy_Prefix(Container __instance)
        {
            if (__instance == null || __instance.CO == null)
            {
                return;
            }

            // Remove whitelist when container is destroyed
            if (__instance.HasWhitelist())
            {
                SmarterHaulingPlugin.Logger.LogDebug(
                    $"[ContainerWhitelist] Removing whitelist for destroyed container {__instance.CO.strNameFriendly}"
                );
                __instance.RemoveWhitelist();
            }
        }
    }
}

