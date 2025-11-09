using Ostranauts.Bit.PersistentData;
using Ostranauts.Bit.SmarterHauling.Extensions;
using System.Collections.Generic;
using System.Linq;

namespace Ostranauts.Bit.SmarterHauling.Data
{
    /// <summary>
    /// Persistent data handler for SmarterHauling container whitelist preferences.
    /// Saves/loads container storage preferences with game saves using the generic list handler.
    /// </summary>
    public class SmarterHaulingDataHandler : ListPersistentDataHandler<ContainerStoragePrefs>
    {
        public override string ModuleName => "smarterhauling";

        protected override string FileName => "container_whitelists.json";

        public override bool CanSave() => true;

        public override bool CanLoad() => true;

        /// <summary>
        /// Get the list of container storage preferences to save.
        /// Converts the dictionary to a list for serialization.
        /// </summary>
        protected override List<ContainerStoragePrefs> GetDataList()
        {
            var prefsDict = ContainerExtensions.GetAllWhitelists();
            if (prefsDict == null || prefsDict.Count == 0)
            {
                LogInfo("No whitelists to save (dictionary is empty)");
                return null;
            }
            
            // Convert to list manually (avoiding LINQ for Unity compatibility)
            var result = new List<ContainerStoragePrefs>();
            foreach (var kvp in prefsDict)
            {
                result.Add(kvp.Value);
                LogInfo($"Saving whitelist for container {kvp.Key} with {kvp.Value.AllowedCategories?.Count ?? 0} categories");
            }
            LogInfo($"GetDataList returning {result.Count} container preferences");
            return result;
        }

        /// <summary>
        /// Load the container storage preferences from the list.
        /// Converts the list back to a dictionary for the extension system.
        /// </summary>
        protected override void SetDataList(List<ContainerStoragePrefs> dataList)
        {
            if (dataList == null || dataList.Count == 0)
            {
                LogInfo("SetDataList received null or empty list");
                return;
            }

            LogInfo($"SetDataList received {dataList.Count} container preferences");

            // Convert list to dictionary keyed by ContainerId
            var prefsDict = new Dictionary<string, ContainerStoragePrefs>();
            foreach (var prefs in dataList)
            {
                if (!string.IsNullOrEmpty(prefs.ContainerId))
                {
                    int categoryCount = prefs.AllowedCategories?.Count ?? 0;
                    LogInfo($"Loading whitelist for container {prefs.ContainerId} with {categoryCount} categories");
                    if (categoryCount > 0 && prefs.AllowedCategories != null)
                    {
                        for (int i = 0; i < prefs.AllowedCategories.Count; i++)
                        {
                            var cat = prefs.AllowedCategories[i];
                            LogInfo($"  Category {i}: Id={cat?.Id ?? "NULL"}, AlternativeIds count={cat?.AlternativeIds?.Length ?? 0}");
                        }
                    }
                    prefsDict[prefs.ContainerId] = prefs;
                }
                else
                {
                    LogWarning("Skipping preference with null or empty ContainerId");
                }
            }

            LogInfo($"Loading {prefsDict.Count} whitelists into extension system");

            // Load into the extension system
            ContainerExtensions.LoadWhitelists(prefsDict);
            
            int loadedCount = ContainerExtensions.GetWhitelistCount();
            LogInfo($"After loading, ContainerExtensions has {loadedCount} whitelists");
        }

        /// <summary>
        /// Clear existing whitelists before loading.
        /// </summary>
        protected override void ClearData()
        {
            int countBeforeClear = ContainerExtensions.GetWhitelistCount();
            LogInfo($"ClearData called - clearing {countBeforeClear} existing whitelists");
            ContainerExtensions.ClearAllWhitelists();
            int countAfterClear = ContainerExtensions.GetWhitelistCount();
            LogInfo($"After clear, {countAfterClear} whitelists remain (should be 0)");
        }

        /// <summary>
        /// Validate that a container preference has a valid ContainerId.
        /// </summary>
        protected override bool ValidateItem(ContainerStoragePrefs item)
        {
            return item != null && !string.IsNullOrEmpty(item.ContainerId);
        }
    }
}

