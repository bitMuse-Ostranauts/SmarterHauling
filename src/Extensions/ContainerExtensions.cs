using System;
using System.Collections.Generic;
using Ostranauts.Bit.SmarterHauling.Data;

namespace Ostranauts.Bit.SmarterHauling.Extensions
{
    /// <summary>
    /// Extension methods for Container to support storage preferences
    /// </summary>
    public static class ContainerExtensions
    {
        /// <summary>
        /// Static dictionary to store storage preferences for containers
        /// Key: Container CondOwner ID (strID)
        /// Value: ContainerStoragePrefs
        /// Note: No locking needed as Unity gameplay runs on single thread
        /// </summary>
        private static readonly Dictionary<string, ContainerStoragePrefs> containerStoragePrefs = new Dictionary<string, ContainerStoragePrefs>();

        /// <summary>
        /// Get the storage preferences for a container
        /// </summary>
        /// <param name="container">The container</param>
        /// <returns>ContainerStoragePrefs if exists, null otherwise</returns>
        public static ContainerStoragePrefs GetPrefs(this Container container)
        {
            if (container == null || container.CO == null)
            {
                return null;
            }

            string id = container.CO.strID;
            if (containerStoragePrefs.TryGetValue(id, out ContainerStoragePrefs prefs))
            {
                return prefs;
            }

            return null;
        }

        /// <summary>
        /// Set the storage preferences for a container
        /// </summary>
        /// <param name="container">The container</param>
        /// <param name="prefs">The storage preferences to set</param>
        public static void SetPrefs(this Container container, ContainerStoragePrefs prefs)
        {
            if (container == null || container.CO == null)
            {
                return;
            }

            string id = container.CO.strID;
            
            if (prefs == null)
            {
                // Remove preferences if null
                containerStoragePrefs.Remove(id);
            }
            else
            {
                // Set container ID if not already set
                if (string.IsNullOrEmpty(prefs.ContainerId))
                {
                    prefs.ContainerId = id;
                }
                
                containerStoragePrefs[id] = prefs;
            }
        }

        /// <summary>
        /// Check if container has storage preferences
        /// </summary>
        /// <param name="container">The container</param>
        /// <returns>True if container has storage preferences</returns>
        public static bool HasPrefs(this Container container)
        {
            if (container == null || container.CO == null)
            {
                return false;
            }

            return containerStoragePrefs.ContainsKey(container.CO.strID);
        }

        /// <summary>
        /// Remove the storage preferences from a container
        /// </summary>
        /// <param name="container">The container</param>
        public static void RemovePrefs(this Container container)
        {
            if (container == null || container.CO == null)
            {
                return;
            }

            containerStoragePrefs.Remove(container.CO.strID);
        }

        /// <summary>
        /// Check if an item is allowed in the container based on storage preferences
        /// </summary>
        /// <param name="container">The container</param>
        /// <param name="item">The item to check</param>
        /// <returns>True if allowed or no preferences, false if blocked by preferences</returns>
        public static bool IsItemAllowedByPrefs(this Container container, CondOwner item)
        {
            ContainerStoragePrefs prefs = container.GetPrefs();
            
            // If no preferences, allow all items
            if (prefs == null)
            {
                return true;
            }

            return prefs.IsItemAllowed(item);
        }

        /// <summary>
        /// Get all containers with storage preferences
        /// </summary>
        /// <returns>Dictionary of container IDs to storage preferences</returns>
        public static Dictionary<string, ContainerStoragePrefs> GetAllPrefs()
        {
            // Return a copy to prevent external modification
            return new Dictionary<string, ContainerStoragePrefs>(containerStoragePrefs);
        }

        /// <summary>
        /// Clear all storage preferences (useful for cleanup or new game)
        /// </summary>
        public static void ClearAllPrefs()
        {
            int countBefore = containerStoragePrefs.Count;
            containerStoragePrefs.Clear();
            UnityEngine.Debug.Log($"[ContainerExtensions] ClearAllPrefs cleared {countBefore} preferences");
        }

        /// <summary>
        /// Load storage preferences from serialized data
        /// </summary>
        /// <param name="prefsData">Dictionary of container IDs to storage preferences</param>
        public static void LoadPrefs(Dictionary<string, ContainerStoragePrefs> prefsData)
        {
            if (prefsData == null)
            {
                UnityEngine.Debug.Log("[ContainerExtensions] LoadPrefs called with null dictionary");
                return;
            }

            UnityEngine.Debug.Log($"[ContainerExtensions] LoadPrefs called with {prefsData.Count} items");
            
            foreach (var kvp in prefsData)
            {
                int catCount = kvp.Value?.AllowedCategories?.Count ?? 0;
                UnityEngine.Debug.Log($"[ContainerExtensions] Adding preferences for {kvp.Key} with {catCount} categories");
                containerStoragePrefs[kvp.Key] = kvp.Value;
            }
            
            UnityEngine.Debug.Log($"[ContainerExtensions] After LoadPrefs, total count is {containerStoragePrefs.Count}");
        }

        /// <summary>
        /// Get container by CondOwner
        /// </summary>
        /// <param name="co">The CondOwner</param>
        /// <returns>Container component or null</returns>
        public static Container GetContainer(this CondOwner co)
        {
            if (co == null)
            {
                return null;
            }

            return co.GetComponent<Container>();
        }

        /// <summary>
        /// Check if a CondOwner has a Container component with storage preferences
        /// </summary>
        /// <param name="co">The CondOwner</param>
        /// <returns>True if has container with storage preferences</returns>
        public static bool HasContainerWithPrefs(this CondOwner co)
        {
            Container container = co.GetContainer();
            return container != null && container.HasPrefs();
        }

        /// <summary>
        /// Get the number of active storage preferences
        /// </summary>
        public static int GetPrefsCount()
        {
            return containerStoragePrefs.Count;
        }

        /// <summary>
        /// Get storage preferences by container ID
        /// </summary>
        /// <param name="containerId">The container ID</param>
        /// <returns>ContainerStoragePrefs if exists, null otherwise</returns>
        public static ContainerStoragePrefs GetPrefsById(string containerId)
        {
            if (string.IsNullOrEmpty(containerId))
            {
                return null;
            }

            if (containerStoragePrefs.TryGetValue(containerId, out ContainerStoragePrefs prefs))
            {
                return prefs;
            }

            return null;
        }

        /// <summary>
        /// Set storage preferences by container ID
        /// </summary>
        /// <param name="containerId">The container ID</param>
        /// <param name="prefs">The storage preferences to set</param>
        public static void SetPrefsById(string containerId, ContainerStoragePrefs prefs)
        {
            if (string.IsNullOrEmpty(containerId))
            {
                return;
            }

            if (prefs == null)
            {
                // Remove preferences if null
                containerStoragePrefs.Remove(containerId);
            }
            else
            {
                // Set container ID if not already set
                if (string.IsNullOrEmpty(prefs.ContainerId))
                {
                    prefs.ContainerId = containerId;
                }
                
                containerStoragePrefs[containerId] = prefs;
            }
        }

        /// <summary>
        /// Get or create storage preferences by container ID
        /// </summary>
        /// <param name="containerId">The container ID</param>
        /// <returns>ContainerStoragePrefs (existing or newly created)</returns>
        public static ContainerStoragePrefs GetOrCreatePrefs(string containerId)
        {
            if (string.IsNullOrEmpty(containerId))
            {
                return null;
            }

            var prefs = GetPrefsById(containerId);
            if (prefs == null)
            {
                prefs = new ContainerStoragePrefs(containerId);
                SetPrefsById(containerId, prefs);
            }

            return prefs;
        }
    }
}

