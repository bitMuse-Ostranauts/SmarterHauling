using System;
using System.Collections.Generic;
using Ostranauts.Core;
using UnityEngine;

namespace Ostranauts.Bit.SmarterHauling.Commands
{
    /// <summary>
    /// Command to search for items by name or ID
    /// Usage: searchitems <search_term>
    /// </summary>
    public class SearchItemsCommand : MonoBehaviour
    {
        private void Start()
        {
            // Register the searchitems command
            LaunchControl.Instance.Commands.RegisterCommand("searchitems", OnSearchItemsCommand);
            SmarterHaulingPlugin.Logger.LogInfo("SearchItems command registered");
        }

        private void OnSearchItemsCommand(string input)
        {
            try
            {
                // Parse the search term from the input
                string searchTerm = ExtractSearchTerm(input);
                
                if (string.IsNullOrEmpty(searchTerm))
                {
                DevConsole.Output("<color=yellow>Usage: searchitems <search_term> [--all]</color>");
                DevConsole.Output("<color=yellow>Example: searchitems trencher</color>");
                DevConsole.Output("<color=yellow>        searchitems sneaker --all  (searches all CO types, not just items)</color>");
                    return;
                }

                // Check for --all flag
                bool searchAll = input.ToLower().Contains("--all");
                string searchType = searchAll ? "all COs" : "items";
                
                SmarterHaulingPlugin.Logger.LogInfo($"Searching for {searchType} matching: {searchTerm}");
                DevConsole.Output($"<color=cyan>Searching for {searchType} matching: '{searchTerm}'...</color>");

                // Check if DataHandler is available
                if (DataHandler.dictCOs == null)
                {
                    DevConsole.Output("<color=red>Error: DataHandler not initialized!</color>");
                    return;
                }

                // Search through all items
                List<SearchResult> results = SearchItems(searchTerm, searchAll);

                // Display results
                if (results.Count == 0)
                {
                    DevConsole.Output($"<color=yellow>No items found matching '{searchTerm}'</color>");
                }
                else
                {
                    DevConsole.Output($"<color=green>Found {results.Count} item(s):</color>");
                    
                    // Limit to first 50 results to avoid console spam
                    int displayCount = Math.Min(results.Count, 50);
                    for (int i = 0; i < displayCount; i++)
                    {
                        var result = results[i];
                        string categoryInfo = GetCategoryInfo(result.Id);
                        string conditionsInfo = GetConditionsInfo(result.JsonData);
                        string typeInfo = "";
                        
                        if (result.IsOverlay)
                        {
                            typeInfo = " <color=magenta>(Overlay)</color>";
                            if (!string.IsNullOrEmpty(result.BaseCOId))
                            {
                                typeInfo += $" <color=gray>Base: {result.BaseCOId}</color>";
                            }
                        }
                        else if (searchAll && result.JsonData != null && !string.IsNullOrEmpty(result.JsonData.strType))
                        {
                            typeInfo = $" <color=cyan>({result.JsonData.strType})</color>";
                        }
                        else
                        {
                            typeInfo = " <color=cyan>(Item)</color>";
                        }
                        
                        DevConsole.Output($"  <color=white>{result.DisplayName}</color> <color=gray>[{result.Id}]</color>{typeInfo}");
                        
                        if (!string.IsNullOrEmpty(categoryInfo))
                        {
                            DevConsole.Output($"    {categoryInfo}");
                        }
                        if (!string.IsNullOrEmpty(conditionsInfo))
                        {
                            DevConsole.Output($"    {conditionsInfo}");
                        }
                        if (result.IsOverlay && result.OverlayData != null)
                        {
                            string overlayInfo = $"<color=magenta>Overlay for: {result.BaseCOId ?? "Unknown"}</color>";
                            if (!string.IsNullOrEmpty(result.OverlayData.strImg))
                            {
                                overlayInfo += $" <color=gray>Img: {result.OverlayData.strImg}</color>";
                            }
                            DevConsole.Output($"    {overlayInfo}");
                        }
                    }
                    
                    if (results.Count > 50)
                    {
                        DevConsole.Output($"<color=yellow>... and {results.Count - 50} more. Refine your search to see all results.</color>");
                    }
                }
            }
            catch (Exception ex)
            {
                SmarterHaulingPlugin.Logger.LogError($"Error executing searchitems command: {ex.Message}");
                SmarterHaulingPlugin.Logger.LogError(ex.StackTrace);
                DevConsole.Output($"<color=red>Error: {ex.Message}</color>");
            }
        }

        private string ExtractSearchTerm(string input)
        {
            // Remove the command name and trim
            string searchTerm = input.Trim();
            
            // Find first space and get everything after it
            int spaceIndex = searchTerm.IndexOf(' ');
            if (spaceIndex > 0 && spaceIndex < searchTerm.Length - 1)
            {
                string term = searchTerm.Substring(spaceIndex + 1).Trim();
                // Remove --all flag if present
                term = term.Replace("--all", "").Replace("--ALL", "").Trim();
                return term;
            }
            
            return string.Empty;
        }

        private List<SearchResult> SearchItems(string searchTerm, bool searchAll = false)
        {
            List<SearchResult> results = new List<SearchResult>();
            string searchLower = searchTerm.ToLower();

            // Search base CO definitions
            List<string> keys = new List<string>();
            foreach (var key in DataHandler.dictCOs.Keys)
            {
                keys.Add(key);
            }

            foreach (string key in keys)
            {
                if (!DataHandler.dictCOs.ContainsKey(key))
                {
                    continue;
                }

                JsonCondOwner jsonCO = DataHandler.dictCOs[key];

                // Only search items (not crew, ships, etc.) unless --all flag is used
                if (!searchAll && (jsonCO.strType == null || jsonCO.strType.ToLower() != "item"))
                {
                    continue;
                }

                // Get display name
                string displayName = GetDisplayName(key, jsonCO);
                
                // Check if item matches search term
                bool matches = false;
                
                // Match ID
                if (key.ToLower().Contains(searchLower))
                {
                    matches = true;
                }
                // Match display name
                else if (displayName.ToLower().Contains(searchLower))
                {
                    matches = true;
                }
                // Match friendly name
                else if (!string.IsNullOrEmpty(jsonCO.strNameFriendly) && 
                         jsonCO.strNameFriendly.ToLower().Contains(searchLower))
                {
                    matches = true;
                }
                // Match short name
                else if (!string.IsNullOrEmpty(jsonCO.strName) && 
                         jsonCO.strName.ToLower().Contains(searchLower))
                {
                    matches = true;
                }

                if (matches)
                {
                    results.Add(new SearchResult
                    {
                        Id = key,
                        DisplayName = displayName,
                        JsonData = jsonCO,
                        IsOverlay = false,
                        BaseCOId = null
                    });
                }
            }

            // Search COOverlays if DataHandler is initialized
            if (DataHandler.dictCOOverlays != null)
            {
                List<string> overlayKeys = new List<string>();
                foreach (var key in DataHandler.dictCOOverlays.Keys)
                {
                    overlayKeys.Add(key);
                }

                foreach (string overlayKey in overlayKeys)
                {
                    if (!DataHandler.dictCOOverlays.ContainsKey(overlayKey))
                    {
                        continue;
                    }

                    JsonCOOverlay overlay = DataHandler.dictCOOverlays[overlayKey];
                    
                    // Check if the base CO is an item (only search item overlays unless --all)
                    if (!string.IsNullOrEmpty(overlay.strCOBase))
                    {
                        JsonCondOwner baseCO = null;
                        if (DataHandler.dictCOs.TryGetValue(overlay.strCOBase, out baseCO))
                        {
                            if (!searchAll && (baseCO.strType == null || baseCO.strType.ToLower() != "item"))
                            {
                                continue;
                            }
                        }
                    }

                    // Get display name for overlay
                    string overlayDisplayName = GetOverlayDisplayName(overlayKey, overlay);
                    
                    // Check if overlay matches search term
                    bool matches = false;
                    
                    // Match overlay ID
                    if (overlayKey.ToLower().Contains(searchLower))
                    {
                        matches = true;
                    }
                    // Match overlay friendly name
                    else if (!string.IsNullOrEmpty(overlay.strNameFriendly) && 
                             overlay.strNameFriendly.ToLower().Contains(searchLower))
                    {
                        matches = true;
                    }
                    // Match overlay name
                    else if (!string.IsNullOrEmpty(overlay.strName) && 
                             overlay.strName.ToLower().Contains(searchLower))
                    {
                        matches = true;
                    }
                    // Match base CO name
                    else if (!string.IsNullOrEmpty(overlay.strCOBase) && 
                             overlay.strCOBase.ToLower().Contains(searchLower))
                    {
                        matches = true;
                    }

                    if (matches)
                    {
                        results.Add(new SearchResult
                        {
                            Id = overlayKey,
                            DisplayName = overlayDisplayName,
                            JsonData = null, // Overlays don't have JsonCondOwner
                            IsOverlay = true,
                            BaseCOId = overlay.strCOBase,
                            OverlayData = overlay
                        });
                    }
                }
            }

            // Sort results alphabetically by display name
            results.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));

            return results;
        }

        private string GetDisplayName(string id, JsonCondOwner jsonCO)
        {
            // Try friendly name first
            if (!string.IsNullOrEmpty(jsonCO.strNameFriendly))
            {
                return jsonCO.strNameFriendly;
            }
            
            // Try short name
            if (!string.IsNullOrEmpty(jsonCO.strName))
            {
                return jsonCO.strName;
            }
            
            // Fall back to ID
            return id;
        }

        private string GetOverlayDisplayName(string overlayId, JsonCOOverlay overlay)
        {
            // Try friendly name first
            if (!string.IsNullOrEmpty(overlay.strNameFriendly))
            {
                return overlay.strNameFriendly;
            }
            
            // Try overlay name
            if (!string.IsNullOrEmpty(overlay.strName))
            {
                return overlay.strName;
            }
            
            // Fall back to ID
            return overlayId;
        }

        private string GetCategoryInfo(string itemId)
        {
            try
            {
                // Check if item is in any categories
                if (LaunchControl.Instance?.Items?.Categories == null)
                {
                    return "";
                }

                var categoryManager = LaunchControl.Instance.Items.Categories;
                var categories = categoryManager.GetAllCategories();
                
                List<string> categoryNames = new List<string>();
                foreach (var category in categories)
                {
                    // Check if the itemId is in this category's item list
                    if (IsItemInCategory(category, itemId))
                    {
                        categoryNames.Add(category.DisplayName);
                    }
                }

                if (categoryNames.Count > 0)
                {
                    // Convert list to array manually (avoiding LINQ for Unity compatibility)
                    string[] categoryNamesArray = new string[categoryNames.Count];
                    for (int i = 0; i < categoryNames.Count; i++)
                    {
                        categoryNamesArray[i] = categoryNames[i];
                    }
                    return $"<color=cyan>Categories: {string.Join(", ", categoryNamesArray)}</color>";
                }
                
                return "<color=gray>Categories: [None]</color>";
            }
            catch (Exception ex)
            {
                SmarterHaulingPlugin.Logger.LogWarning($"Error getting category info for {itemId}: {ex.Message}");
                return "";
            }
        }

        private string GetConditionsInfo(JsonCondOwner jsonCO)
        {
            try
            {
                if (jsonCO == null)
                {
                    return "";
                }

                List<string> conditionParts = new List<string>();

                // Add starting conditions
                if (jsonCO.aStartingConds != null && jsonCO.aStartingConds.Length > 0)
                {
                    string[] condArray = new string[jsonCO.aStartingConds.Length];
                    for (int i = 0; i < jsonCO.aStartingConds.Length; i++)
                    {
                        condArray[i] = jsonCO.aStartingConds[i];
                    }
                    conditionParts.Add($"<color=yellow>Conditions: {string.Join(", ", condArray)}</color>");
                }

                // Add condition rules
                if (jsonCO.aStartingCondRules != null && jsonCO.aStartingCondRules.Length > 0)
                {
                    string[] ruleArray = new string[jsonCO.aStartingCondRules.Length];
                    for (int i = 0; i < jsonCO.aStartingCondRules.Length; i++)
                    {
                        ruleArray[i] = jsonCO.aStartingCondRules[i];
                    }
                    conditionParts.Add($"<color=orange>Cond Rules: {string.Join(", ", ruleArray)}</color>");
                }

                if (conditionParts.Count > 0)
                {
                    // Join all parts with newlines
                    string result = "";
                    for (int i = 0; i < conditionParts.Count; i++)
                    {
                        if (i > 0) result += "\n    ";
                        result += conditionParts[i];
                    }
                    return result;
                }

                return "";
            }
            catch (Exception ex)
            {
                SmarterHaulingPlugin.Logger.LogWarning($"Error getting conditions info: {ex.Message}");
                return "";
            }
        }

        private bool IsItemInCategory(Ostranauts.Bit.Items.Categories.ItemCategory category, string itemId)
        {
            // Check direct items
            if (category.Items != null)
            {
                foreach (var item in category.Items)
                {
                    if (item.Id == itemId)
                    {
                        return true;
                    }
                    
                    // Check alternative IDs
                    if (item.AlternativeIds != null)
                    {
                        foreach (var altId in item.AlternativeIds)
                        {
                            if (altId == itemId)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            
            // Check subcategories recursively
            if (category.Subcategories != null)
            {
                foreach (var subcat in category.Subcategories)
                {
                    if (IsItemInCategory(subcat, itemId))
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }

        private class SearchResult
        {
            public string Id { get; set; }
            public string DisplayName { get; set; }
            public JsonCondOwner JsonData { get; set; }
            public bool IsOverlay { get; set; }
            public string BaseCOId { get; set; }
            public JsonCOOverlay OverlayData { get; set; }
        }
    }
}

