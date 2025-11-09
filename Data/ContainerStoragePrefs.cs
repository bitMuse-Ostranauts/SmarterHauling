using System;
using System.Collections.Generic;
using System.Linq;
using Ostranauts.Bit.Items.Categories;
using Ostranauts.Bit;

namespace Ostranauts.Bit.SmarterHauling.Data
{
    /// <summary>
    /// Defines which item categories a container accepts
    /// </summary>
    [Serializable]
    public class ContainerStoragePrefs
    {
        /// <summary>
        /// ID of the container this preference belongs to
        /// </summary>
        public string ContainerId;

        /// <summary>
        /// List of allowed item category hint IDs
        /// </summary>
        public List<ItemHintId> AllowedCategories;

        /// <summary>
        /// Default constructor for JSON deserialization
        /// </summary>
        public ContainerStoragePrefs()
        {
            ContainerId = string.Empty;
            AllowedCategories = new List<ItemHintId>();
        }

        public ContainerStoragePrefs(string containerId)
        {
            ContainerId = containerId;
            AllowedCategories = new List<ItemHintId>();
        }

        /// <summary>
        /// Add a category to the storage preferences
        /// </summary>
        public void AddCategory(ItemHintId categoryId)
        {
            if (categoryId == null || string.IsNullOrEmpty(categoryId.Id))
            {
                return;
            }

            // Verify category exists in ItemCategoryManager
            if (BitLib.Instance?.Items?.Categories?.GetCategory(categoryId) == null)
            {
                return;
            }

            // Check if already exists (avoiding LINQ for Unity compatibility)
            bool alreadyExists = false;
            for (int i = 0; i < AllowedCategories.Count; i++)
            {
                if (AllowedCategories[i].Id == categoryId.Id)
                {
                    alreadyExists = true;
                    break;
                }
            }
            
            if (!alreadyExists)
            {
                AllowedCategories.Add(categoryId);
            }
        }

        /// <summary>
        /// Add a category by string ID (implicit conversion)
        /// </summary>
        public void AddCategory(string categoryId)
        {
            if (!string.IsNullOrEmpty(categoryId))
            {
                AddCategory(new ItemHintId(categoryId));
            }
        }

        /// <summary>
        /// Remove a category from the storage preferences
        /// </summary>
        public void RemoveCategory(ItemHintId categoryId)
        {
            if (categoryId != null)
            {
                AllowedCategories.RemoveAll(c => c.Id == categoryId.Id);
            }
        }

        /// <summary>
        /// Remove a category by string ID
        /// </summary>
        public void RemoveCategory(string categoryId)
        {
            if (!string.IsNullOrEmpty(categoryId))
            {
                AllowedCategories.RemoveAll(c => c.Id == categoryId);
            }
        }

        /// <summary>
        /// Add an item hint ID to the storage preferences
        /// </summary>
        public void AddItem(ItemHintId itemHintId)
        {
            if (itemHintId == null || string.IsNullOrEmpty(itemHintId.Id))
            {
                return;
            }

            // Check if already exists (avoiding LINQ for Unity compatibility)
            bool alreadyExists = false;
            for (int i = 0; i < AllowedCategories.Count; i++)
            {
                if (AllowedCategories[i].Id == itemHintId.Id)
                {
                    alreadyExists = true;
                    break;
                }
            }
            
            if (!alreadyExists)
            {
                AllowedCategories.Add(itemHintId);
            }
        }

        /// <summary>
        /// Add an item by string ID (implicit conversion)
        /// </summary>
        public void AddItem(string itemId)
        {
            if (!string.IsNullOrEmpty(itemId))
            {
                AddItem(new ItemHintId(itemId));
            }
        }

        /// <summary>
        /// Remove an item from the storage preferences
        /// </summary>
        public void RemoveItem(ItemHintId itemHintId)
        {
            if (itemHintId != null)
            {
                AllowedCategories.RemoveAll(c => c.Id == itemHintId.Id);
            }
        }

        /// <summary>
        /// Remove an item by string ID
        /// </summary>
        public void RemoveItem(string itemId)
        {
            if (!string.IsNullOrEmpty(itemId))
            {
                AllowedCategories.RemoveAll(c => c.Id == itemId);
            }
        }

        /// <summary>
        /// Check if an item is allowed based on the storage preferences
        /// Checks both direct item matches and category matches
        /// </summary>
        public bool IsItemAllowed(CondOwner item)
        {
            if (item == null || AllowedCategories.Count == 0)
            {
                return false; // Empty whitelist allows nothing
            }

            var categoryManager = BitLib.Instance?.Items?.Categories;
            if (categoryManager == null)
            {
                return false;
            }

            // First check for direct item ID matches (including alternative IDs)
            string itemId = item.strCODef ?? item.strItemDef;
            if (!string.IsNullOrEmpty(itemId))
            {
                foreach (var allowedItem in AllowedCategories)
                {
                    // Check primary ID
                    if (allowedItem.Id == itemId)
                    {
                        return true;
                    }
                    
                    // Check alternative IDs
                    if (allowedItem.AlternativeIds != null)
                    {
                        foreach (var altId in allowedItem.AlternativeIds)
                        {
                            if (altId == itemId)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            // Then check category matches using ItemCategoryManager
            // Convert to string array (avoiding LINQ for Unity compatibility)
            string[] categoryIds = new string[AllowedCategories.Count];
            for (int i = 0; i < AllowedCategories.Count; i++)
            {
                categoryIds[i] = AllowedCategories[i].Id;
            }
            var matcher = categoryManager.CreateMatcher(categoryIds);
            
            return matcher(item);
        }

        /// <summary>
        /// Serialize to JSON using Unity's JsonUtility
        /// </summary>
        public string ToJson()
        {
            return UnityEngine.JsonUtility.ToJson(this);
        }

        /// <summary>
        /// Deserialize from JSON using Unity's JsonUtility
        /// </summary>
        public static ContainerStoragePrefs FromJson(string json)
        {
            try
            {
                return UnityEngine.JsonUtility.FromJson<ContainerStoragePrefs>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
