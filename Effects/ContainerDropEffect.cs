using Ostranauts.Bit.Interactions;
using Ostranauts.Bit.SmarterHauling.Extensions;
using System;
using System.Collections.Generic;

namespace Ostranauts.Bit.SmarterHauling.Effects
{
    /// <summary>
    /// Tracks a haul job: character needs to deliver a certain quantity of an item to a container
    /// </summary>
    public class HaulJob
    {
        public string CharacterID { get; set; }
        public string ItemDefID { get; set; }  // The strCODef of the item (e.g. "ItmSteel")
        public int QuantityRemaining { get; set; }
        public string TargetContainerID { get; set; }
    }

    /// <summary>
    /// Custom effect that handles dropping items into containers with preferences
    /// </summary>
    public class ContainerDropEffect : IEffect
    {
        // Track active haul jobs by character ID
        // Key: character ID, Value: HaulJob
        private static Dictionary<string, HaulJob> _activeHaulJobs = new Dictionary<string, HaulJob>();
        
        // Store the item being dropped (since we'll clear objThem in Prepare)
        // Key: interaction ID, Value: the item (objThem)
        private static Dictionary<System.Guid, CondOwner> _savedItems = new Dictionary<System.Guid, CondOwner>();

        public bool ShouldPrepare(Interaction interaction)
        {
            // Only prepare DropItemStack interactions for characters with active haul jobs
            if (interaction == null || interaction.strName != "DropItemStack")
            {
                return false;
            }

            CondOwner character = interaction.objUs;
            if (character == null)
            {
                return false;
            }

            // Check if this character has an active haul job
            return _activeHaulJobs.ContainsKey(character.strID);
        }

        public void Prepare(Interaction interaction)
        {
            // DON'T clear objThem - ApplyEffects checks for null and aborts early!
            // Instead, save it and prevent the default drop/destroy behavior
            if (interaction.objThem != null)
            {
                _savedItems[interaction.id] = interaction.objThem;
            }
            
            // CRITICAL: Prevent item destruction! bDestroyItem causes the game to destroy the item after the interaction
            interaction.bDestroyItem = false;
            
            // Clear all item movement mechanisms to prevent default behavior
            // String-based loot definitions
            interaction.strLootCTsGive = null;
            interaction.strLootCTsTake = null;
            interaction.strLootCTsRemoveUs = null;
            interaction.strLootItmRemoveThem = null;
            interaction.strLootItmAddUs = null;
            interaction.strLootItmAddThem = null;
            interaction.strLootCTsUse = null;
            interaction.strLootCTsLacks = null;
            interaction.strLootItmInputs = null;
            
            // Loot object references (CRITICAL - these are what ApplyEffects actually uses!)
            interaction.LootCTsUs = null;
            interaction.LootCTsThem = null;
            interaction.LootCTs3rd = null;
            interaction.LootCondsUs = null;
            interaction.LootCondsThem = null;
            interaction.LootConds3rd = null;
            
            // Item contracts
            interaction.aLootItemGiveContract = null;
            interaction.aLootItemTakeContract = null;
            interaction.aLootItemRemoveContract = null;
            interaction.aLootItemUseContract = null;
            
            // Set bHardCode to false to prevent hardcoded behavior
            interaction.bHardCode = false;
            
            // Now dump the JSON AFTER we've made all our changes
            try
            {
                var interactionData = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "strName", interaction.strName },
                    { "strTitle", interaction.strTitle },
                    { "strDesc", interaction.strDesc },
                    { "objUs", interaction.objUs?.strNameFriendly ?? "null" },
                    { "objUs_ID", interaction.objUs?.strID ?? "null" },
                    { "objThem", interaction.objThem?.strNameFriendly ?? "null" },
                    { "objThem_ID", interaction.objThem?.strID ?? "null" },
                    { "obj3rd", interaction.obj3rd?.strNameFriendly ?? "null" },
                    { "bHardCode", interaction.bHardCode },
                    { "bEquip", interaction.bEquip },
                    { "bLot", interaction.bLot },
                    { "bDestroyItem", interaction.bDestroyItem },
                    { "bGiveWholeStack", interaction.bGiveWholeStack },
                    { "bRemoveWholeStack", interaction.bRemoveWholeStack },
                    { "strLootItmAddUs", interaction.strLootItmAddUs ?? "null" },
                    { "strLootItmAddThem", interaction.strLootItmAddThem ?? "null" },
                    { "strLootItmRemoveThem", interaction.strLootItmRemoveThem ?? "null" },
                    { "strLootCTsGive", interaction.strLootCTsGive ?? "null" },
                    { "strLootCTsTake", interaction.strLootCTsTake ?? "null" },
                    { "strLootCTsRemoveUs", interaction.strLootCTsRemoveUs ?? "null" },
                    { "strLootCTsUse", interaction.strLootCTsUse ?? "null" },
                    { "strLootCTsLacks", interaction.strLootCTsLacks ?? "null" },
                    { "strLootItmInputs", interaction.strLootItmInputs ?? "null" },
                    { "LootCTsUs", interaction.LootCTsUs?.strName ?? "null" },
                    { "LootCTsThem", interaction.LootCTsThem?.strName ?? "null" },
                    { "LootCTs3rd", interaction.LootCTs3rd?.strName ?? "null" },
                    { "LootCondsUs", interaction.LootCondsUs?.strName ?? "null" },
                    { "LootCondsThem", interaction.LootCondsThem?.strName ?? "null" },
                    { "LootConds3rd", interaction.LootConds3rd?.strName ?? "null" },
                    { "aLootItemGiveContract_Count", interaction.aLootItemGiveContract?.Count ?? 0 },
                    { "aLootItemTakeContract_Count", interaction.aLootItemTakeContract?.Count ?? 0 },
                    { "aLootItemRemoveContract_Count", interaction.aLootItemRemoveContract?.Count ?? 0 },
                    { "aLootItemUseContract_Count", interaction.aLootItemUseContract?.Count ?? 0 },
                    { "strChainStart", interaction.strChainStart ?? "null" },
                    { "strChainOwner", interaction.strChainOwner ?? "null" },
                    { "fDuration", interaction.fDuration }
                };

                string json = LitJson.JsonMapper.ToJson(interactionData);
                SmarterHaulingPlugin.Logger.LogInfo($"[ContainerDropEffect.Prepare] ===== INTERACTION JSON AFTER PREPARE =====");
                SmarterHaulingPlugin.Logger.LogInfo(json);
                SmarterHaulingPlugin.Logger.LogInfo($"[ContainerDropEffect.Prepare] ===== END JSON =====");
            }
            catch (System.Exception ex)
            {
                SmarterHaulingPlugin.Logger.LogError($"[ContainerDropEffect.Prepare] Failed to serialize: {ex.Message}");
            }
        }

        public bool ShouldExecute(Interaction interaction)
        {
            // Only execute for DropItemStack interactions
            if (interaction == null || interaction.strName != "DropItemStack")
            {
                return false;
            }

            CondOwner character = interaction.objUs;
            CondOwner item = interaction.objThem;
            
            if (character == null || item == null)
            {
                return false;
            }

            // Check if this character has an active haul job for this item type
            if (!_activeHaulJobs.TryGetValue(character.strID, out HaulJob job))
            {
                return false;
            }

            // Check if the item being dropped matches the job's item definition
            bool matches = item.strCODef == job.ItemDefID;
            
            if (matches)
            {
                SmarterHaulingPlugin.Logger.LogInfo($"[ContainerDropEffect.ShouldExecute] YES - {character.strNameFriendly} dropping {item.strNameFriendly} (def:{item.strCODef}, stack:{item.StackCount}) for haul job (need {job.QuantityRemaining} more)");
            }
            else
            {
                SmarterHaulingPlugin.Logger.LogInfo($"[ContainerDropEffect.ShouldExecute] NO - {character.strNameFriendly} dropping {item.strNameFriendly} (def:{item.strCODef}) but job is for def:{job.ItemDefID}");
            }
            
            return matches;
        }

        public EffectResult Execute(Interaction interaction)
        {
            CondOwner character = interaction.objUs;
            CondOwner item = interaction.objThem;

            // Clean up saved item (if we stored it)
            _savedItems.Remove(interaction.id);

            if (item == null || character == null)
            {
                SmarterHaulingPlugin.Logger.LogWarning($"[ContainerDropEffect.Execute] Null item or character");
                return new EffectResult(true);
            }

            // Get the active haul job for this character
            if (!_activeHaulJobs.TryGetValue(character.strID, out HaulJob job))
            {
                SmarterHaulingPlugin.Logger.LogWarning($"[ContainerDropEffect.Execute] No haul job found for {character.strNameFriendly}");
                return new EffectResult(true);
            }
            
            SmarterHaulingPlugin.Logger.LogInfo($"[ContainerDropEffect.Execute] {character.strNameFriendly} triggered drop of {item.StackCount}x {item.strNameFriendly}, job needs {job.QuantityRemaining} total");

            try
            {
                CondOwner containerCO = null;
                if (!DataHandler.mapCOs.TryGetValue(job.TargetContainerID, out containerCO) || containerCO == null)
                {
                    SmarterHaulingPlugin.Logger.LogWarning($"[ContainerDropEffect] Container {job.TargetContainerID} not found, cancelling job");
                    _activeHaulJobs.Remove(character.strID);
                    return new EffectResult(true);
                }

                Container targetContainer = containerCO.objContainer;
                if (targetContainer == null)
                {
                    SmarterHaulingPlugin.Logger.LogWarning($"[ContainerDropEffect] Container component not found on {containerCO.strNameFriendly}, cancelling job");
                    _activeHaulJobs.Remove(character.strID);
                    return new EffectResult(true);
                }

                // Check line of sight - character must be able to see the container
                if (!Visibility.IsCondOwnerLOSVisible(character, containerCO))
                {
                    if (SmarterHaulingPlugin.EnableDebugLogging)
                    {
                        SmarterHaulingPlugin.Logger.LogDebug(
                            $"[ContainerDropEffect] No line of sight from {character.strNameFriendly} to {containerCO.strNameFriendly}, cancelling job"
                        );
                    }
                    _activeHaulJobs.Remove(character.strID);
                    return new EffectResult(true);
                }

                // Verify item is still allowed (container settings might have changed)
                var prefs = targetContainer.GetPrefs();
                if (prefs != null && !prefs.IsItemAllowed(item))
                {
                    SmarterHaulingPlugin.Logger.LogInfo($"[ContainerDropEffect] Item {item.strNameFriendly} no longer allowed in {containerCO.strNameFriendly}, cancelling job");
                    _activeHaulJobs.Remove(character.strID);
                    return new EffectResult(true);
                }

                // Deliver the item that triggered this interaction first
                DeliverItemToContainer(item, targetContainer, containerCO, character);
                job.QuantityRemaining -= item.StackCount;

                // Now keep looking for more items of the same definition in the character's inventory
                // and deliver them until we've fulfilled the job
                while (job.QuantityRemaining > 0)
                {
                    // Find more items of this definition in the character's inventory
                    CondOwner nextItem = FindItemInCharacterInventory(character, job.ItemDefID);
                    
                    if (nextItem == null)
                    {
                        SmarterHaulingPlugin.Logger.LogInfo($"[ContainerDropEffect] No more {job.ItemDefID} found in {character.strNameFriendly}'s inventory, {job.QuantityRemaining} still needed");
                        break;
                    }

                    // If we need less than the full stack, split it
                    if (nextItem.StackCount > job.QuantityRemaining)
                    {
                        SmarterHaulingPlugin.Logger.LogInfo($"[ContainerDropEffect] Found {nextItem.StackCount}x but only need {job.QuantityRemaining}, splitting stack");
                        
                        // Pop items from the stack one by one until we have what we need
                        // PopHeadFromStack() removes the TOP item and returns the REMAINING stack
                        int amountNeeded = job.QuantityRemaining;
                        for (int i = 0; i < amountNeeded; i++)
                        {
                            CondOwner remainingStack = nextItem.PopHeadFromStack();
                            // nextItem is now the single popped item, deliver it
                            DeliverItemToContainer(nextItem, targetContainer, containerCO, character);
                            job.QuantityRemaining--;
                            
                            // Update nextItem to point to the remaining stack for the next iteration
                            nextItem = remainingStack;
                            if (nextItem == null)
                            {
                                break; // No more items in stack
                            }
                        }
                        
                        int remaining = (nextItem != null) ? nextItem.StackCount : 0;
                        SmarterHaulingPlugin.Logger.LogInfo($"[ContainerDropEffect] Delivered {amountNeeded}x (split from stack), leaving {remaining}x with character");
                        break;
                    }
                    else
                    {
                        // We need the whole stack
                        SmarterHaulingPlugin.Logger.LogInfo($"[ContainerDropEffect] Found {nextItem.StackCount}x {nextItem.strNameFriendly} in inventory, delivering all");
                        
                        DeliverItemToContainer(nextItem, targetContainer, containerCO, character);
                        job.QuantityRemaining -= nextItem.StackCount;
                    }
                }

                SmarterHaulingPlugin.Logger.LogInfo($"[ContainerDropEffect] Delivery complete. Job has {job.QuantityRemaining} remaining");

                // If job is complete, remove it
                if (job.QuantityRemaining <= 0)
                {
                    SmarterHaulingPlugin.Logger.LogInfo($"[ContainerDropEffect] Job complete for {character.strNameFriendly}");
                    _activeHaulJobs.Remove(character.strID);
                }

                try
                {
                    var afterData = new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "objThem", interaction.objThem?.strNameFriendly ?? "null" },
                        { "objThem_ID", interaction.objThem?.strID ?? "null" },
                        { "objThem_objCOParent", interaction.objThem?.objCOParent?.strNameFriendly ?? "null" },
                        { "objThem_objCOParent_ID", interaction.objThem?.objCOParent?.strID ?? "null" },
                        { "objThem_slotNow", interaction.objThem?.slotNow?.strName ?? "null" },
                        { "objThem_ship", interaction.objThem?.ship?.strRegID ?? "null" },
                        { "job_remaining", job.QuantityRemaining },
                        { "job_complete", job.QuantityRemaining <= 0 }
                    };

                    string json = LitJson.JsonMapper.ToJson(afterData);
                    SmarterHaulingPlugin.Logger.LogInfo($"[ContainerDropEffect.Execute] ===== AFTER JSON =====");
                    SmarterHaulingPlugin.Logger.LogInfo(json);
                    SmarterHaulingPlugin.Logger.LogInfo($"[ContainerDropEffect.Execute] ===== END JSON =====");
                }
                catch (System.Exception dumpEx)
                {
                    SmarterHaulingPlugin.Logger.LogError($"[ContainerDropEffect.Execute] Failed to dump after state: {dumpEx.Message}");
                }

                // CRITICAL: Null objThem so nothing else tries to move it after we've placed it in the container
                // Also null objUs to prevent any character-related cleanup
                interaction.objThem = null;
                interaction.objUs = null;
                SmarterHaulingPlugin.Logger.LogInfo($"[ContainerDropEffect.Execute] Nulled interaction.objThem and objUs to prevent any post-processing");

                // We handled the drop, stop other effects from processing
                return new EffectResult(false);
            }
            catch (Exception ex)
            {
                SmarterHaulingPlugin.Logger.LogError($"[ContainerDropEffect] Error: {ex.Message}");
                SmarterHaulingPlugin.Logger.LogError(ex.StackTrace);
                _activeHaulJobs.Remove(character.strID);
                return new EffectResult(true);
            }
        }

        private void DeliverItemToContainer(CondOwner item, Container targetContainer, CondOwner containerCO, CondOwner character)
        {
            SmarterHaulingPlugin.Logger.LogInfo($"[DeliverItemToContainer] Attempting to deliver {item.StackCount}x {item.strNameFriendly} (ID:{item.strID}) to {containerCO.strNameFriendly}");
            SmarterHaulingPlugin.Logger.LogInfo($"[DeliverItemToContainer] Item current parent: {item.objCOParent?.strNameFriendly ?? "NULL"}, ship: {item.ship?.strRegID ?? "NULL"}");
            SmarterHaulingPlugin.Logger.LogInfo($"[DeliverItemToContainer] Container AllowedCO check: {targetContainer.AllowedCO(item)}");
            
            item.RemoveFromCurrentHome(false);
            
            SmarterHaulingPlugin.Logger.LogInfo($"[DeliverItemToContainer] After RemoveFromCurrentHome - Item parent: {item.objCOParent?.strNameFriendly ?? "NULL"}, ship: {item.ship?.strRegID ?? "NULL"}");
            SmarterHaulingPlugin.Logger.LogInfo($"[DeliverItemToContainer] Container AllowedCO check AFTER remove: {targetContainer.AllowedCO(item)}");
            
            // Use AddCO which automatically stacks items together
            CondOwner leftover = targetContainer.AddCO(item);
            
            SmarterHaulingPlugin.Logger.LogInfo($"[DeliverItemToContainer] After AddCO - Item parent: {item.objCOParent?.strNameFriendly ?? "NULL"}, ship: {item.ship?.strRegID ?? "NULL"}, Visible: {item.Visible}");
            
            if (leftover == null)
            {
                // Successfully added (and possibly stacked)
                // CRITICAL: Remove the item from the ship's item list since it's now in a container
                // Container.AddCO adds it to ship, but that might cause validation issues
                if (item.ship != null)
                {
                    item.ship.RemoveCO(item, false);
                    SmarterHaulingPlugin.Logger.LogInfo($"[DeliverItemToContainer] Removed item from ship's item list to prevent validation issues");
                }
                
                SmarterHaulingPlugin.Logger.LogInfo($"[ContainerDropEffect] Delivered {item.StackCount}x {item.strNameFriendly} to {containerCO.strNameFriendly} - item now at parent: {item.objCOParent?.strNameFriendly ?? "NULL"}");
            }
            else
            {
                // Container couldn't take it, check why
                SmarterHaulingPlugin.Logger.LogWarning($"[ContainerDropEffect] AddCO returned leftover! leftover={leftover.strNameFriendly} (ID:{leftover.strID}, stack:{leftover.StackCount})");
                SmarterHaulingPlugin.Logger.LogWarning($"[ContainerDropEffect] Leftover parent: {leftover.objCOParent?.strNameFriendly ?? "NULL"}, same as item? {leftover == item}");
                character.ship.AddCO(leftover, true);
            }
        }

        private CondOwner FindItemInCharacterInventory(CondOwner character, string itemDefID)
        {
            // Check all slots on the character
            foreach (Slot slot in character.GetSlots(false, Slots.SortOrder.HELD_FIRST))
            {
                if (slot == null || slot.aCOs == null)
                    continue;

                foreach (CondOwner co in slot.aCOs)
                {
                    if (co != null && co.strCODef == itemDefID)
                    {
                        return co;
                    }
                }
            }

            // Check containers (like backpacks)
            List<CondOwner> allItems = character.GetCOs(true, null);
            if (allItems != null)
            {
                foreach (CondOwner co in allItems)
                {
                    if (co != null && co.strCODef == itemDefID)
                    {
                        return co;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a haul job for a character to deliver a quantity of an item to a container
        /// Called from HaulZone_Prefix when picking up items for hauling
        /// </summary>
        public static void TrackHaulJob(string characterID, string itemDefID, int quantity, string containerID)
        {
            if (string.IsNullOrEmpty(characterID) || string.IsNullOrEmpty(itemDefID) || string.IsNullOrEmpty(containerID))
            {
                SmarterHaulingPlugin.Logger.LogWarning($"[ContainerDropEffect.TrackHaulJob] Invalid parameters - char:{characterID ?? "null"}, def:{itemDefID ?? "null"}, container:{containerID ?? "null"}");
                return;
            }

            var job = new HaulJob
            {
                CharacterID = characterID,
                ItemDefID = itemDefID,
                QuantityRemaining = quantity,
                TargetContainerID = containerID
            };

            _activeHaulJobs[characterID] = job;
            
            SmarterHaulingPlugin.Logger.LogInfo($"[ContainerDropEffect.TrackHaulJob] Created job: char={characterID}, itemDef={itemDefID}, qty={quantity}, container={containerID}");
        }

        /// <summary>
        /// Cancels an active haul job for a character (used for cleanup if haul is cancelled)
        /// </summary>
        public static void CancelHaulJob(string characterID)
        {
            if (_activeHaulJobs.ContainsKey(characterID))
            {
                _activeHaulJobs.Remove(characterID);
                SmarterHaulingPlugin.Logger.LogInfo($"[ContainerDropEffect.CancelHaulJob] Cancelled job for character {characterID}");
            }
        }
    }
}

