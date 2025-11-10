using Ostranauts.Bit;
using Ostranauts.Bit.SmarterHauling.Data;
using Ostranauts.Bit.SmarterHauling.Extensions;
using Ostranauts.UI.MegaToolTip.DataModules;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ostranauts.Bit.SmarterHauling.UI
{
    /// <summary>
    /// MegaToolTip module for displaying storage settings with a chip and settings button
    /// </summary>
    public class StorageSettingsModule : ModuleBase, IPointerEnterHandler, IPointerExitHandler
    {
        private GameObject _chipContainer;
        private Image _chipBackground;
        private TMP_Text _chipText;
        private Button _settingsButton;
        private Container _container;
        private CondOwner _condOwner;
        private string _containerId; // Cache container ID for event matching

        private new void Awake()
        {
            base.Awake();
            // Subscribe to preference changes
            StorageSettingWidget.OnPreferencesChanged += OnPreferencesChanged;
        }

        private void OnDestroy()
        {
            // Unsubscribe from preference changes
            StorageSettingWidget.OnPreferencesChanged -= OnPreferencesChanged;
        }

        /// <summary>
        /// Called when storage preferences change for any container
        /// </summary>
        private void OnPreferencesChanged(string containerId)
        {
            // Only update if this module is for the container that changed
            if (!string.IsNullOrEmpty(containerId) && containerId == _containerId)
            {
                UpdateChipText();
            }
        }

        /// <summary>
        /// Set data for the module
        /// </summary>
        public override void SetData(CondOwner co)
        {
            if (co == null)
            {
                _IsMarkedForDestroy = true;
                return;
            }

            // Store the CondOwner
            _condOwner = co;

            // Check if this CondOwner has a Container component
            _container = co.GetContainer();
            if (_container == null)
            {
                _IsMarkedForDestroy = true;
                return;
            }

            // Cache container ID for event matching
            _containerId = co.strID;

            // UI should already be setup by SetupUI during template creation
            // Find the components if they haven't been cached yet
            if (_chipContainer == null)
            {
                _chipContainer = transform.Find("StorageChip")?.gameObject;
                _chipBackground = _chipContainer?.GetComponent<Image>();
                _chipText = _chipContainer?.GetComponentInChildren<TextMeshProUGUI>();
                _settingsButton = transform.Find("SettingsButton")?.GetComponent<Button>();
            }

            // Update chip text based on whitelist
            UpdateChipText();

            // Add click listener to settings button
            if (_settingsButton != null)
            {
                _settingsButton.onClick.RemoveAllListeners();
                _settingsButton.onClick.AddListener(() =>
                {
                    StorageSettingWidget.Show(_condOwner);
                });
            }

            // Force layout rebuild
            LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
        }

        /// <summary>
        /// Static method to setup UI for this module (called during template creation)
        /// </summary>
        public static void SetupUI(GameObject go)
        {
            // Load sprites using BitLib's SpriteUtility (searching all loaded sprites by keywords)
            // Chip sprite: "Rounded Corner Rect POT 9-Sliced x32 _ gradient bordered"
            Sprite chipSprite = SpriteUtility.FindSpriteByKeywords(new[] { "gradient", "bordered" });
            if (chipSprite == null)
            {
                SmarterHaulingPlugin.Logger.LogWarning("[StorageSettings] Failed to load chip sprite with keywords: gradient, bordered");
            }
            
            // Button background sprite: GUICrewBarCircButtonMid
            Sprite buttonBgSprite = SpriteUtility.FindSpriteByKeywords(new[] { "crewbar", "circbutton" });
            if (buttonBgSprite == null)
            {
                SmarterHaulingPlugin.Logger.LogWarning("[StorageSettings] Failed to load button background sprite (GUICrewBarCircButtonMid)");
            }
            
            // Settings icon sprite: GUIScrew01
            Sprite settingsIconSprite = SpriteUtility.FindSpriteByKeywords(new[] { "screw" });
            if (settingsIconSprite == null)
            {
                SmarterHaulingPlugin.Logger.LogWarning("[StorageSettings] Failed to load settings icon sprite (GUIScrew01)");
            }
            
            // Add background image to main module (like ValueModule)
            Image moduleBackground = go.AddComponent<Image>();
            moduleBackground.color = new Color(0f, 0f, 0f, 0.278f); // Semi-transparent black, matching ValueModule
            
            // Create horizontal layout group with padding (similar to ValueModule's -10 inset)
            HorizontalLayoutGroup horizontalLayout = go.AddComponent<HorizontalLayoutGroup>();
            horizontalLayout.spacing = 8f;
            horizontalLayout.childAlignment = TextAnchor.MiddleLeft;
            horizontalLayout.childControlWidth = false;
            horizontalLayout.childControlHeight = false;
            horizontalLayout.childForceExpandWidth = false;
            horizontalLayout.childForceExpandHeight = false;
            horizontalLayout.padding = new RectOffset(5, 5, 5, 5); // Add 5px padding on all sides

            // Set up RectTransform
            RectTransform rectTransform = go.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = go.AddComponent<RectTransform>();
            }
            rectTransform.anchorMin = new Vector2(0, 1);
            rectTransform.anchorMax = new Vector2(1, 1);
            rectTransform.pivot = new Vector2(0.5f, 1);
            rectTransform.sizeDelta = new Vector2(0, 32); // Height matching ValueModule

            // Create chip container (first element)
            GameObject chipContainer = new GameObject("StorageChip");
            chipContainer.transform.SetParent(go.transform, false);

            RectTransform chipRect = chipContainer.AddComponent<RectTransform>();
            chipRect.sizeDelta = new Vector2(120, 24);
            chipRect.pivot = new Vector2(0, 0.5f);
            chipRect.anchorMin = new Vector2(0, 0.5f);
            chipRect.anchorMax = new Vector2(0, 0.5f);

            // Add background image
            Image chipBackground = chipContainer.AddComponent<Image>();
            chipBackground.color = new Color(0.5f, 0.7f, 1f, 1f); // Light blue
            chipBackground.type = Image.Type.Sliced;

            if (chipSprite != null)
            {
                chipBackground.sprite = chipSprite;
            }

            // Add text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(chipContainer.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6, 2);
            textRect.offsetMax = new Vector2(-6, -2);

            TextMeshProUGUI chipText = textObj.AddComponent<TextMeshProUGUI>();
            chipText.text = "None";
            chipText.fontSize = 12;
            chipText.color = Color.white;
            chipText.alignment = TextAlignmentOptions.Center;
            chipText.enableWordWrapping = false;

            // Create settings button (anchored to right, styled like Show More button)
            GameObject buttonObj = new GameObject("SettingsButton");
            buttonObj.transform.SetParent(go.transform, false);

            RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
            // Anchor to right side
            buttonRect.anchorMin = new Vector2(1, 0.5f);
            buttonRect.anchorMax = new Vector2(1, 0.5f);
            buttonRect.pivot = new Vector2(1, 0.5f);
            buttonRect.sizeDelta = new Vector2(100, 22); // Wider to fit "SETTINGS" text
            buttonRect.anchoredPosition = new Vector2(-5, 0); // 5px from right edge

            // Add button background image
            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.type = Image.Type.Sliced;
            // Dark background color like Show More button
            buttonImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            
            if (buttonBgSprite != null)
            {
                buttonImage.sprite = buttonBgSprite;
            }

            // Add button component
            Button settingsButton = buttonObj.AddComponent<Button>();
            
            // Button colors (matching Show More style)
            ColorBlock colors = settingsButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            settingsButton.colors = colors;

            // Container for icon and text
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(buttonObj.transform, false);
            
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = new Vector2(4, 0);
            contentRect.offsetMax = new Vector2(-4, 0);
            
            HorizontalLayoutGroup contentLayout = contentObj.AddComponent<HorizontalLayoutGroup>();
            contentLayout.spacing = 4f;
            contentLayout.childAlignment = TextAnchor.MiddleCenter;
            contentLayout.childControlWidth = false;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandWidth = false;
            contentLayout.childForceExpandHeight = false;

            // Add settings icon if available
            if (settingsIconSprite != null)
            {
                GameObject iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(contentObj.transform, false);
                
                Image iconImage = iconObj.AddComponent<Image>();
                iconImage.sprite = settingsIconSprite;
                iconImage.color = Color.white;
                
                RectTransform iconRect = iconObj.GetComponent<RectTransform>();
                iconRect.sizeDelta = new Vector2(12, 12);
            }

            // Add button text
            GameObject buttonTextObj = new GameObject("Text");
            buttonTextObj.transform.SetParent(contentObj.transform, false);

            TextMeshProUGUI buttonText = buttonTextObj.AddComponent<TextMeshProUGUI>();
            buttonText.text = "SETTINGS"; // Uppercase like Show More
            buttonText.fontSize = 10;
            buttonText.color = Color.white;
            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.enableWordWrapping = false;
            buttonText.fontStyle = FontStyles.Bold;
            
            RectTransform buttonTextRect = buttonTextObj.GetComponent<RectTransform>();
            buttonTextRect.sizeDelta = new Vector2(70, 20);

            // Note: Click listener will be added in SetData when we have the CondOwner instance
        }

        /// <summary>
        /// Update the chip text based on preference state
        /// </summary>
        private void UpdateChipText()
        {
            if (_chipText == null || _container == null)
            {
                return;
            }

            ContainerStoragePrefs prefs = _container.GetPrefs();
            if (prefs == null || prefs.AllowedCategories == null || prefs.AllowedCategories.Count == 0)
            {
                _chipText.text = "None";
            }
            else if (prefs.AllowedCategories.Count == 1)
            {
                // Show the single category name
                string categoryId = prefs.AllowedCategories[0];
                var category = BitLib.Instance?.Items?.Categories?.GetCategory(categoryId);
                if (category != null)
                {
                    _chipText.text = category.DisplayName;
                }
                else
                {
                    // Not a category -- try resolving as item using Ostranauts' DataHandler
                    string friendlyName = DataHandler.GetCOFriendlyName(categoryId);
                    if (!string.IsNullOrEmpty(friendlyName))
                    {
                        _chipText.text = friendlyName;
                    }
                    else
                    {
                        _chipText.text = categoryId;
                    }
                }
            }
            else
            {
                // Multiple categories
                _chipText.text = "Multiple";
            }
        }

        /// <summary>
        /// Called when mouse enters the module
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_condOwner == null)
            {
                return;
            }

            string title = "Storage Settings";
            string body = BuildTooltipText();

            GUITooltip2.SetToolTip(title, body, true);
        }

        /// <summary>
        /// Called when mouse exits the module
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            GUITooltip2.SetToolTip(string.Empty, string.Empty, false);
        }

        /// <summary>
        /// Build the tooltip text based on current preference state
        /// </summary>
        private string BuildTooltipText()
        {
            if (_condOwner == null)
            {
                return string.Empty;
            }

            Container container = _condOwner.GetContainer();
            if (container == null)
            {
                return string.Empty;
            }

            ContainerStoragePrefs prefs = container.GetWhitelist();
            string itemName = _condOwner.strName;

            if (prefs == null || prefs.AllowedCategories == null || prefs.AllowedCategories.Count == 0)
            {
                return string.Format("The {0} isn't configured to store anything in particular.", itemName);
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendFormat("The {0} is set to store:\n\n", itemName);

            var categoryManager = BitLib.Instance?.Items?.Categories;
            int count = System.Math.Min(10, prefs.AllowedCategories.Count);
            for (int i = 0; i < count; i++)
            {
                string categoryId = prefs.AllowedCategories[i];
                var category = categoryManager?.GetCategory(categoryId);
                string friendlyName = category?.DisplayName ?? categoryId;
                sb.AppendLine("• " + friendlyName);
            }

            if (prefs.AllowedCategories.Count > 10)
            {
                int remaining = prefs.AllowedCategories.Count - 10;
                sb.AppendFormat("\n...and {0} more", remaining);
                if (remaining > 1)
                {
                    sb.Append(" categories");
                }
                else
                {
                    sb.Append(" category");
                }
            }

            return sb.ToString();
        }


    }
}


