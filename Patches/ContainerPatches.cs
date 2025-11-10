using HarmonyLib;
using Ostranauts.Bit.SmarterHauling.Extensions;

namespace Ostranauts.Bit.SmarterHauling.Patches
{
    /// <summary>
    /// Patches for Container to add preference functionality
    /// </summary>
    [HarmonyPatch(typeof(Container))]
    public class ContainerPatches
    {
        /// <summary>
        /// Patch Container.Destroy to clean up preference data when container is destroyed
        /// </summary>
        [HarmonyPatch("Destroy")]
        [HarmonyPrefix]
        public static void Destroy_Prefix(Container __instance)
        {
            if (__instance == null || __instance.CO == null)
            {
                return;
            }

            // Remove preferences when container is destroyed
            if (__instance.HasPrefs())
            {
                SmarterHaulingPlugin.Logger.LogDebug(
                    $"[ContainerPrefs] Removing preferences for destroyed container {__instance.CO.strNameFriendly}"
                );
                __instance.RemovePrefs();
            }
        }
    }
}

