using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Ostranauts.Bit.SmarterHauling.Extensions;
using Ostranauts.Bit.SmarterHauling.Data;
using Ostranauts.Bit.SmarterHauling.Effects;
using Ostranauts.Bit.SmarterHauling.UI;

namespace Ostranauts.Bit.SmarterHauling.Patches
{
    [HarmonyPatch(typeof(CondOwner))]
    public class HaulingPatches
    {
        private static bool CanFitItem(CondOwner character, CondOwner item)
        {
            if (character == null || item == null)
            {
                return false;
            }

            if (character.objContainer != null && character.objContainer.CanFit(item, true, false))
            {
                return true;
            }

            List<Slot> slots = character.GetSlots(false, Slots.SortOrder.HELD_FIRST);
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].bHoldSlot && slots[i].CanFit(item, true, true))
                {
                    return true;
                }
            }

            return false;
        }

        public static Container FindBestHaulDestination(CondOwner item, Ship ship)
        {
            if (item == null || ship == null)
            {
                SmarterHaulingPlugin.Logger.LogWarning($"[FindBestHaulDestination] Null parameters - item:{item != null}, ship:{ship != null}");
                return null;
            }

            SmarterHaulingPlugin.Logger.LogInfo($"[FindBestHaulDestination] Searching for container for item: {item.strNameFriendly} (def:{item.strCODef})");

            List<CondOwner> allCOs = ship.GetCOs(null, true, false, true);
            
            if (allCOs == null || allCOs.Count == 0)
            {
                SmarterHaulingPlugin.Logger.LogWarning($"[FindBestHaulDestination] No COs found on ship");
                return null;
            }

            SmarterHaulingPlugin.Logger.LogInfo($"[FindBestHaulDestination] Checking {allCOs.Count} potential containers");

            List<Container> matchingContainers = new List<Container>();
            int checkedCount = 0;
            int carriedCount = 0;
            int hiddenCount = 0;
            int noContainerCount = 0;
            int noPrefsCount = 0;
            int notAllowedCount = 0;
            int fullCount = 0;

            foreach (CondOwner co in allCOs)
            {
                checkedCount++;
                
                if (co.HasCond("IsCarried"))
                {
                    carriedCount++;
                    continue;
                }
                
                // Use the centralized helper to check if this container should be considered
                // This checks for container existence, damaged state, and loose state
                if (!StorageModuleHelper.ShouldShowStorageModule(co))
                {
                    hiddenCount++;
                    continue;
                }
                
                Container container = co.objContainer;
                if (container == null)
                {
                    noContainerCount++;
                    continue;
                }

                // Note: We don't check LOS from character's current position here because
                // the character will walk to a tile adjacent to the container.
                // LOS will be checked from the destination tile in ContainerDropEffect.

                ContainerStoragePrefs prefs = container.GetPrefs();
                
                if (prefs != null && prefs.AllowedCategories.Count > 0)
                {
                    if (prefs.IsItemAllowed(item))
                    {
                        SmarterHaulingPlugin.Logger.LogInfo($"[FindBestHaulDestination] Container {co.strNameFriendly} allows item {item.strNameFriendly}");
                        
                        // Check if the item can stack with existing items in the container
                        bool canAcceptItem = false;
                        
                        // First, check if any items in the container can stack with this item
                        List<CondOwner> containerItems = container.GetCOs(true, null);
                        if (containerItems != null && containerItems.Count > 0)
                        {
                            foreach (CondOwner existingItem in containerItems)
                            {
                                if (existingItem.CanStackOnItem(item) > 0)
                                {
                                    canAcceptItem = true;
                                    SmarterHaulingPlugin.Logger.LogInfo($"[FindBestHaulDestination] Container {co.strNameFriendly} can stack with existing item");
                                    break;
                                }
                            }
                        }
                        
                        // If no stacking option, check if there's empty space
                        if (!canAcceptItem && container.CanFit(item, true, false))
                        {
                            canAcceptItem = true;
                            SmarterHaulingPlugin.Logger.LogInfo($"[FindBestHaulDestination] Container {co.strNameFriendly} has empty space");
                        }
                        
                        if (canAcceptItem)
                        {
                            matchingContainers.Add(container);
                            SmarterHaulingPlugin.Logger.LogInfo($"[FindBestHaulDestination] ✓ MATCH: {co.strNameFriendly}");
                        }
                        else
                        {
                            fullCount++;
                            SmarterHaulingPlugin.Logger.LogInfo($"[FindBestHaulDestination] Container {co.strNameFriendly} matches preferences but is FULL (no space and no stacking)");
                        }
                    }
                    else
                    {
                        notAllowedCount++;
                    }
                }
                else
                {
                    noPrefsCount++;
                }
            }
            
            // Log summary
            SmarterHaulingPlugin.Logger.LogInfo($"[FindBestHaulDestination] Summary for {item.strNameFriendly}:");
            SmarterHaulingPlugin.Logger.LogInfo($"  - Total COs checked: {checkedCount}");
            SmarterHaulingPlugin.Logger.LogInfo($"  - Carried: {carriedCount}");
            SmarterHaulingPlugin.Logger.LogInfo($"  - Hidden (damaged/loose/locked/etc): {hiddenCount}");
            SmarterHaulingPlugin.Logger.LogInfo($"  - No container component: {noContainerCount}");
            SmarterHaulingPlugin.Logger.LogInfo($"  - No preferences set: {noPrefsCount}");
            SmarterHaulingPlugin.Logger.LogInfo($"  - Item not allowed by prefs: {notAllowedCount}");
            SmarterHaulingPlugin.Logger.LogInfo($"  - Container full: {fullCount}");
            SmarterHaulingPlugin.Logger.LogInfo($"  - Matching containers: {matchingContainers.Count}");

            if (matchingContainers.Count > 0)
            {
                Container bestContainer = null;
                int maxEmptySlots = -1;
                
                for (int i = 0; i < matchingContainers.Count; i++)
                {
                    int emptySlots = GetEmptySlotCount(matchingContainers[i].gridLayout);
                    if (emptySlots > maxEmptySlots)
                    {
                        maxEmptySlots = emptySlots;
                        bestContainer = matchingContainers[i];
                    }
                }
                
                if (bestContainer != null && SmarterHaulingPlugin.EnableDebugLogging)
                {
                    SmarterHaulingPlugin.Logger.LogDebug($"[ContainerPrefs] Selected container {bestContainer.CO.strNameFriendly} with {maxEmptySlots} empty slots");
                }
                
                return bestContainer;
            }

            return null;
        }

        private static int GetEmptySlotCount(GridLayout gridLayout)
        {
            if (gridLayout == null)
            {
                return 0;
            }

            int emptySlots = 0;
            for (int x = 0; x < gridLayout.gridMaxX; x++)
            {
                for (int y = 0; y < gridLayout.gridMaxY; y++)
                {
                    if (string.IsNullOrEmpty(gridLayout.gridID[x, y]))
                    {
                        emptySlots++;
                    }
                }
            }
            return emptySlots;
        }

        [HarmonyPatch(typeof(WorkManager), "HaulZone")]
        [HarmonyPrefix]
        public static bool HaulZone_Prefix(WorkManager __instance, CondOwner coHauler, Task2 task, CondOwner coTarget, ref Interaction __result)
        {
            SmarterHaulingPlugin.Logger.LogInfo($"[HaulZone_Prefix] Called for {coHauler?.strNameFriendly} hauling {coTarget?.strNameFriendly}");
            
            if (task == null || coTarget == null || coHauler == null || !WorkManager.CTHaul.Triggered(coTarget, null, true))
            {
                SmarterHaulingPlugin.Logger.LogInfo($"[HaulZone_Prefix] Early exit - null checks or CTHaul failed");
                return true;
            }

            SmarterHaulingPlugin.Logger.LogInfo($"[HaulZone_Prefix] Current task.nTile={task.nTile}, task.strTileShip={task.strTileShip}");

            // Check if we have a container with preferences that wants this item
            Container bestContainer = FindBestHaulDestination(coTarget, coHauler.ship);
            
            if (bestContainer != null && bestContainer.CO != null)
            {
                SmarterHaulingPlugin.Logger.LogInfo($"[HaulZone_Prefix] Found best container: {bestContainer.CO.strNameFriendly} at pos {bestContainer.CO.tf.position}");
                
                Tile containerTile = coHauler.ship.GetTileAtWorldCoords1(
                    bestContainer.CO.tf.position.x, 
                    bestContainer.CO.tf.position.y, 
                    true, 
                    true
                );
                
                SmarterHaulingPlugin.Logger.LogInfo($"[HaulZone_Prefix] Container tile: {(containerTile != null ? $"Index={containerTile.Index}, Passable={containerTile.bPassable}" : "NULL")}");
                
                Tile walkTargetTile = null;
                
                if (containerTile != null)
                {
                    // Get character's current position
                    Tile characterTile = coHauler.ship.GetTileAtWorldCoords1(
                        coHauler.tf.position.x, 
                        coHauler.tf.position.y, 
                        true, 
                        true
                    );
                    
                    Tile[] surroundingTiles = TileUtils.GetSurroundingTiles(containerTile, true, false);
                    if (surroundingTiles != null)
                    {
                        SmarterHaulingPlugin.Logger.LogInfo($"[HaulZone_Prefix] Found {surroundingTiles.Length} surrounding tiles");
                        
                        // Find the closest walkable tile to the character's current position
                        float closestDistance = float.MaxValue;
                        
                        for (int i = 0; i < surroundingTiles.Length; i++)
                        {
                            Tile adjTile = surroundingTiles[i];
                            if (adjTile != null && adjTile.bPassable && !adjTile.IsWall)
                            {
                                // Check if this tile is actually walkable for the character
                                if (coHauler.Pathfinder != null)
                                {
                                    PathResult pathCheck = new PathResult(false);
                                    if (!adjTile.IsWalkable(coHauler, pathCheck))
                                    {
                                        SmarterHaulingPlugin.Logger.LogInfo($"[HaulZone_Prefix] Tile {adjTile.Index} at ({adjTile.tf.position.x}, {adjTile.tf.position.y}) not walkable (forbidden/blocked)");
                                        continue;
                                    }
                                }
                                
                                // Calculate distance from character's current position
                                float distance = float.MaxValue;
                                if (characterTile != null)
                                {
                                    distance = Vector3.Distance(characterTile.tf.position, adjTile.tf.position);
                                }
                                
                                if (distance < closestDistance)
                                {
                                    closestDistance = distance;
                                    walkTargetTile = adjTile;
                                }
                                
                                SmarterHaulingPlugin.Logger.LogInfo($"[HaulZone_Prefix] Candidate tile: Index={adjTile.Index} at ({adjTile.tf.position.x}, {adjTile.tf.position.y}), distance={distance}");
                            }
                        }
                        
                        if (walkTargetTile != null)
                        {
                            SmarterHaulingPlugin.Logger.LogInfo($"[HaulZone_Prefix] Selected closest walkable tile: Index={walkTargetTile.Index} at ({walkTargetTile.tf.position.x}, {walkTargetTile.tf.position.y}), distance={closestDistance}");
                        }
                    }
                    
                    if (walkTargetTile == null)
                    {
                        // If no adjacent walkable tiles found, try the container tile itself
                        if (containerTile.bPassable && !containerTile.IsWall)
                        {
                            PathResult pathCheck = new PathResult(false);
                            if (coHauler.Pathfinder != null && containerTile.IsWalkable(coHauler, pathCheck))
                            {
                                walkTargetTile = containerTile;
                                SmarterHaulingPlugin.Logger.LogInfo($"[HaulZone_Prefix] Using container tile itself: Index={containerTile.Index}");
                            }
                        }
                        
                        if (walkTargetTile == null)
                        {
                            SmarterHaulingPlugin.Logger.LogWarning($"[HaulZone_Prefix] No walkable tile found near container {bestContainer.CO.strNameFriendly}");
                        }
                    }
                }
                else
                {
                    SmarterHaulingPlugin.Logger.LogWarning($"[HaulZone_Prefix] Could not get tile at container position!");
                }

                if (walkTargetTile != null)
                {
                    // Set the task tile and ship directly
                    task.nTile = walkTargetTile.Index;
                    task.strTileShip = walkTargetTile.coProps.ship.strRegID;
                    
                    // Track the haul job for the drop effect
                    string itemDef = coTarget.strCODef;
                    int quantity = coTarget.StackCount;
                    ContainerDropEffect.TrackHaulJob(coHauler.strID, itemDef, quantity, bestContainer.CO.strID);
                    
                    // Return the interaction and skip the original method
                    __result = DataHandler.GetInteraction(task.strInteraction, null, false);
                    
                    SmarterHaulingPlugin.Logger.LogInfo($"[HaulZone_Prefix] Set container destination and returning interaction: char={coHauler.strNameFriendly}, item={coTarget.strNameFriendly} (def:{itemDef}, qty:{quantity}) -> container={bestContainer.CO.strNameFriendly} at tile.Index {walkTargetTile.Index}");
                    
                    return false; // Skip original method
                }
                else
                {
                    SmarterHaulingPlugin.Logger.LogWarning($"[HaulZone_Prefix] Could not find valid walkable tile for container {bestContainer.CO.strNameFriendly}, falling back to default behavior");
                }
            }
            else
            {
                SmarterHaulingPlugin.Logger.LogInfo($"[HaulZone_Prefix] No container with preferences found for {coTarget.strNameFriendly}, using default haul zone");
            }
            
            return true; // Run original method
        }


    }
}
