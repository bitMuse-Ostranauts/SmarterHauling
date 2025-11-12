using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Ostranauts.Bit.SmarterHauling.Data;
using Ostranauts.Bit.SmarterHauling.Extensions;
using Ostranauts.Bit;
using Ostranauts.Bit.SmarterHauling.UI;
using Ostranauts.Bit.SmarterHauling.Effects;
using Ostranauts.Bit.Items.Categories;
using UnityEngine;

namespace Ostranauts.Bit.SmarterHauling
{
    /// <summary>
    /// SmarterHauling - Makes characters with storage containers fill their inventory
    /// with multiple items before delivering when hauling. Also adds preference functionality
    /// to containers to filter what items can be stored.
    /// </summary>
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("com.ostranauts.LaunchControl", BepInDependency.DependencyFlags.HardDependency)]
    public class SmarterHaulingPlugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
        internal static bool EnableDebugLogging = false;
        
        private Harmony _harmony;
        private ConfigEntry<bool> _configDebugLogging;

        private void Awake()
        {
            Logger = base.Logger;
            
            // Setup configuration
            SetupConfiguration();
            
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} v{PluginInfo.PLUGIN_VERSION} is loading...");

            // Initialize preference system
            InitializePreferenceSystem();

            // Register persistent data handler for save/load
            RegisterPersistentDataHandler();

            // Register MegaToolTip modules via BitLib
            RegisterMegaTooltipModules();

            // Register custom interaction effects
            RegisterInteractionEffects();

            // Register console commands
            RegisterCommands();

            // Register custom pledge types
            RegisterPledgeTypes();

            // Apply Harmony patches (excludes MegaToolTipPatches - now handled by BitLib)
            _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            _harmony.PatchAll(typeof(SmarterHaulingPlugin).Assembly);
            
            Logger.LogInfo("SmarterHauling patches applied successfully!");
        }

        private void SetupConfiguration()
        {
            _configDebugLogging = Config.Bind(
                "Debug",
                "EnableDebugLogging",
                false,
                "Enable verbose debug logging for troubleshooting"
            );

            EnableDebugLogging = _configDebugLogging.Value;

            if (EnableDebugLogging)
            {
                Logger.LogInfo("Debug logging is ENABLED");
            }
        }

        private void InitializePreferenceSystem()
        {
            try
            {
                // Log available categories from bitlib's category system
                if (LaunchControl.Instance?.Items?.Categories != null)
                {
                    var categories = LaunchControl.Instance.Items.Categories.GetAllCategories();
                    int categoryCount = 0;
                    foreach (var category in categories)
                    {
                        categoryCount++;
                        if (EnableDebugLogging)
                        {
                            Logger.LogDebug($"  - {category.DisplayName} ({category.Id}): {category.Description}");
                        }
                    }
                    Logger.LogInfo($"Preference system initialized with {categoryCount} categories from bitlib");
                }
                else
                {
                    Logger.LogWarning("BitLib item categories not yet available during initialization");
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error initializing preference system: {ex.Message}");
            }
        }

        private void RegisterPersistentDataHandler()
        {
            try
            {
                if (LaunchControl.Instance == null)
                {
                    Logger.LogError("LaunchControl.Instance is null, cannot register persistent data handler");
                    return;
                }

                if (LaunchControl.Instance.PersistentData == null)
                {
                    Logger.LogError("LaunchControl.Instance.PersistentData is null, cannot register handler");
                    return;
                }

                // Create and register the data handler for saving/loading container preferences
                var handler = new SmarterHaulingDataHandler
                {
                    Logger = Logger
                };

                LaunchControl.Instance.PersistentData.RegisterHandler("smarterhauling", handler);
                
                Logger.LogInfo("Registered persistent data handler for container preferences");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error registering persistent data handler: {ex.Message}");
                Logger.LogError(ex.StackTrace);
            }
        }

        private void RegisterMegaTooltipModules()
        {
            try
            {
                // Register StorageSettingsModule for item tooltips
                // This module shows preference settings on containers
                // Only show if container is valid, not damaged, and not loose
                LaunchControl.RegisterItemModule(typeof(StorageSettingsModule), StorageSettingsModule.SetupUI)
                    .OnlyShowIf(StorageModuleHelper.ShouldShowStorageModule)
                    .InsertAfter("ValueModule");

                Logger.LogInfo("Registered StorageSettingsModule with BitLib MegaTooltip system");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error registering MegaTooltip modules: {ex.Message}");
                Logger.LogError(ex.StackTrace);
            }
        }

        private void RegisterInteractionEffects()
        {
            try
            {
                if (LaunchControl.Instance == null || LaunchControl.Instance.Interactions == null)
                {
                    Logger.LogError("BitLib interactions not available, cannot register effects");
                    return;
                }

                // Register ContainerDropEffect to handle smart container drops
                LaunchControl.Instance.Interactions.RegisterEffect(new ContainerDropEffect());

                Logger.LogInfo("Registered ContainerDropEffect with BitLib Interactions system");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error registering interaction effects: {ex.Message}");
                Logger.LogError(ex.StackTrace);
            }
        }

        private void RegisterCommands()
        {
            try
            {
                if (LaunchControl.Instance == null || LaunchControl.Instance.Commands == null)
                {
                    Logger.LogError("BitLib commands not available, cannot register commands");
                    return;
                }

                // Create GameObject to hold command components
                var commandsObject = new GameObject("SmarterHaulingCommands");
                UnityEngine.Object.DontDestroyOnLoad(commandsObject);
                
                // Register SearchItems command
                commandsObject.AddComponent<Commands.SearchItemsCommand>();
                
                Logger.LogInfo("Registered SmarterHauling commands");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error registering commands: {ex.Message}");
                Logger.LogError(ex.StackTrace);
            }
        }

        private void RegisterPledgeTypes()
        {
            try
            {
                if (LaunchControl.Instance == null || LaunchControl.Instance.Pledges == null)
                {
                    Logger.LogError("LaunchControl pledge system not available");
                    return;
                }

                // Register EVA Maintenance pledge
                bool success = LaunchControl.RegisterPledgeType("evamaintenance", typeof(Pledges.PledgeEVAMaintenance));
                
                if (success)
                {
                    Logger.LogInfo("Registered custom pledge types: EVA Maintenance");
                }
                else
                {
                    Logger.LogError("Failed to register EVA Maintenance pledge type");
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error registering pledge types: {ex.Message}");
                Logger.LogError(ex.StackTrace);
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            Logger.LogInfo("SmarterHauling unloaded");
        }
    }

    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.ostranauts.smarterhauling";
        public const string PLUGIN_NAME = "Smarter Hauling";
        public const string PLUGIN_VERSION = "2.0.0";
    }
}

