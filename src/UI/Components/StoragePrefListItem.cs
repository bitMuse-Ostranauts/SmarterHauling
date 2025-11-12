using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Ostranauts.Bit.SmarterHauling.UI
{
    public class StoragePrefListItem : MonoBehaviour
    {
        // Prefixes to strip from item display names
        private static readonly string[] DisplayNamePrefixes = new string[]
        {
            "Shoe: ",
            "Street Food: ",
            "Food: ",
            "Pill: ",
            "Battery: "
        };

        /// <summary>
        /// Strip known prefixes from display names
        /// </summary>
        public static string StripDisplayNamePrefix(string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
                return displayName;

            foreach (string prefix in DisplayNamePrefixes)
            {
                if (displayName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return displayName.Substring(prefix.Length);
                }
            }

            return displayName;
        }

        private GameObject categoryExpansionButton;
        private GameObject categoryArrow;
        private GameObject categoryText;
        private GameObject categoryActionButton;
        private GameObject categoryActionIcon;
        private TextMeshProUGUI textComponent;
        private Image arrowImage;
        private Image actionIconImage;
        private Button expansionButtonComponent;
        private Button actionButtonComponent;

        public string[] CategoryIds { get; set; }

        public StoragePrefStatus ToggleStatus {
            get {
                return _toggleStatus;
            }
        }

        private StoragePrefStatus _toggleStatus = StoragePrefStatus.None;
        public enum StoragePrefStatus
        {
            All,
            Some,
            None,
        }
        
        // Track child widgets for easy cleanup
        private List<GameObject> childWidgets = new List<GameObject>();
        private bool isExpanded = false;
        private bool isUsingItemIcon = false;

        public static StoragePrefListItem Create(Transform parent, string categoryName, float margin = 4f, bool hasChildren = true, Sprite itemIcon = null)
        {
            GameObject categoryGo = new GameObject("Category", typeof(CanvasRenderer), typeof(RectTransform), typeof(LayoutElement), typeof(CanvasGroup));
            categoryGo.transform.SetParent(parent, false);
            categoryGo.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            categoryGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 14f);
            categoryGo.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            categoryGo.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            categoryGo.GetComponent<RectTransform>().anchorMax = Vector2.zero;
            categoryGo.GetComponent<LayoutElement>().preferredHeight = 14f;
            categoryGo.GetComponent<CanvasGroup>().alpha = 1f;
            categoryGo.GetComponent<CanvasGroup>().interactable = true;
            categoryGo.GetComponent<CanvasGroup>().blocksRaycasts = true;

            StoragePrefListItem widget = categoryGo.AddComponent<StoragePrefListItem>();
            widget.SetToggleStatus(StoragePrefStatus.None);
            widget.Initialize(categoryGo, categoryName, margin, hasChildren, itemIcon);

            return widget;
        }

        public void SetToggleStatus(StoragePrefStatus status) {
            _toggleStatus = status;
            
            // Update the icon based on status
            if (actionIconImage != null)
            {
                switch (status)
                {
                    case StoragePrefStatus.All:
                        // Green checkmark
                        actionIconImage.sprite = Ostranauts.Bit.SpriteUtility.FindSpriteByKeywords(new[] { "GUICheckmarkBox" });
                        actionIconImage.color = new Color(0.2f, 0.8f, 0.2f, 1.0f); // Green
                        break;
                        
                    case StoragePrefStatus.Some:
                        // Yellow partial
                        actionIconImage.sprite = Ostranauts.Bit.SpriteUtility.FindSpriteByKeywords(new[] { "100xSquare" });
                        actionIconImage.color = new Color(0.8f, 0.8f, 0.2f, 1.0f); // Yellow
                        break;
                        
                    case StoragePrefStatus.None:
                        // Red X or empty
                        actionIconImage.sprite = Ostranauts.Bit.SpriteUtility.FindSpriteByKeywords(new[] { "100xSquare" });
                        actionIconImage.color = new Color(0.8f, 0.2f, 0.2f, 1.0f); // Red
                        break;
                }
            }
        }

        private void Initialize(GameObject categoryGo, string categoryName, float margin, bool hasChildren, Sprite itemIcon)
        {
            CreateExpansionButton(categoryGo, margin);
            CreateArrow(hasChildren, itemIcon);
            CreateText(categoryName);
            CreateActionButton(categoryGo);
        }

        private void CreateExpansionButton(GameObject categoryGo, float margin)
        {
            categoryExpansionButton = new GameObject("CategoryExpansionButton", typeof(CanvasRenderer), typeof(RectTransform), typeof(Button));
            categoryExpansionButton.transform.SetParent(categoryGo.transform, false);
            categoryExpansionButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(margin, 0f); // Left margin with left pivot
            categoryExpansionButton.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 14f); // Give button a proper size
            categoryExpansionButton.GetComponent<RectTransform>().pivot = new Vector2(0f, 0.5f); // Pivot at left-center for easier child positioning
            categoryExpansionButton.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0.5f); // Anchor to left-center of parent
            categoryExpansionButton.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 0.5f);
            
            expansionButtonComponent = categoryExpansionButton.GetComponent<Button>();
            expansionButtonComponent.onClick.AddListener(() => {
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo("[CategoryWidget] CategoryExpansionButton clicked");
            });
            expansionButtonComponent.interactable = true;
        }

        private void CreateArrow(bool hasChildren, Sprite itemIcon)
        {
            categoryArrow = new GameObject("CategoryArrow", typeof(CanvasRenderer), typeof(RectTransform), typeof(Image));
            categoryArrow.transform.SetParent(categoryExpansionButton.transform, false);
            categoryArrow.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f); // Anchor at left edge (no padding, button has margin)
            categoryArrow.GetComponent<RectTransform>().sizeDelta = new Vector2(10f, 10f);
            categoryArrow.GetComponent<RectTransform>().pivot = new Vector2(0f, 0.5f); // Pivot at left-center
            categoryArrow.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0.5f); // Anchor to left-center of button
            categoryArrow.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 0.5f);
            
            arrowImage = categoryArrow.GetComponent<Image>();
            
            // Use item icon if provided, otherwise use arrow graphics
            if (itemIcon != null)
            {
                arrowImage.sprite = itemIcon;
                arrowImage.color = Color.white;
                arrowImage.preserveAspect = true; // Preserve aspect ratio while fitting in the icon area
                isUsingItemIcon = true;
            }
            else if (hasChildren)
            {
                // Show arrow only if category has children and no item icon provided
                arrowImage.sprite = Ostranauts.Bit.SpriteUtility.FindSpriteByKeywords(new[] { "GUIArrowR" });
                arrowImage.color = Color.white;
                isUsingItemIcon = false;
            }
            else
            {
                arrowImage.enabled = false; // Hide arrow for leaf nodes
                isUsingItemIcon = false;
            }
        }

        private void CreateText(string categoryName)
        {
            categoryText = new GameObject("CategoryText", typeof(CanvasRenderer), typeof(RectTransform), typeof(TextMeshProUGUI));
            categoryText.transform.SetParent(categoryExpansionButton.transform, false);
            categoryText.GetComponent<RectTransform>().anchoredPosition = new Vector2(14f + 4f, 0f); // Arrow width (14px) + 4px gap
            categoryText.GetComponent<RectTransform>().sizeDelta = new Vector2(80f, 14f); // Give text proper width
            categoryText.GetComponent<RectTransform>().pivot = new Vector2(0f, 0.5f); // Pivot at left-center for left alignment
            categoryText.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0.5f); // Anchor to left-center of button
            categoryText.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 0.5f);
            
            textComponent = categoryText.GetComponent<TextMeshProUGUI>();
            // Strip prefixes before displaying
            string displayName = StripDisplayNamePrefix(categoryName);
            textComponent.text = displayName.Length > 24 ? displayName.Substring(0, 24) + "..." : displayName;
            textComponent.fontSize = 10;
            textComponent.color = Color.white;
            textComponent.alignment = TextAlignmentOptions.Left;
            textComponent.enableWordWrapping = false;
        }

        private void CreateActionButton(GameObject categoryGo)
        {
            categoryActionButton = new GameObject("CategoryActionButton", typeof(CanvasRenderer), typeof(RectTransform), typeof(Button));
            categoryActionButton.transform.SetParent(categoryGo.transform, false);
            categoryActionButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(166f, 0f);
            categoryActionButton.GetComponent<RectTransform>().sizeDelta = new Vector2(14f, 14f);
            categoryActionButton.GetComponent<RectTransform>().pivot = new Vector2(0f, 0.5f); // Pivot at left-center like other elements
            categoryActionButton.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0.5f); // Anchor to left-center of parent
            categoryActionButton.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 0.5f);
            
            actionButtonComponent = categoryActionButton.GetComponent<Button>();
            actionButtonComponent.onClick.AddListener(() => {
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo("[CategoryWidget] CategoryActionButton clicked");
            });
            actionButtonComponent.interactable = true;

            categoryActionIcon = new GameObject("CategoryActionIcon", typeof(CanvasRenderer), typeof(RectTransform), typeof(Image));
            categoryActionIcon.transform.SetParent(categoryActionButton.transform, false);
            categoryActionIcon.GetComponent<RectTransform>().anchoredPosition = Vector2.zero; // Centered in button
            categoryActionIcon.GetComponent<RectTransform>().sizeDelta = new Vector2(14f, 14f);
            categoryActionIcon.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            categoryActionIcon.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0.5f); // Anchor to center
            categoryActionIcon.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);
            
            actionIconImage = categoryActionIcon.GetComponent<Image>();
            actionIconImage.sprite = Ostranauts.Bit.SpriteUtility.FindSpriteByKeywords(new[] { "100xSquare" });
            actionIconImage.color = Color.white;
        }

        // Public API for controlling the widget
        public void SetCategoryName(string name)
        {
            if (textComponent != null)
            {
                // Strip prefixes before displaying
                string displayName = StripDisplayNamePrefix(name);
                textComponent.text = displayName.Length > 24 ? displayName.Substring(0, 24) + "..." : displayName;
            }
        }

        public void SetExpanded(bool expanded)
        {
            isExpanded = expanded;
            
            // Only change arrow direction if we're using arrows, not item icons
            if (!isUsingItemIcon && arrowImage != null && arrowImage.enabled)
            {
                if (expanded)
                {
                    arrowImage.sprite = Ostranauts.Bit.SpriteUtility.FindSpriteByKeywords(new[] { "GUIArrowD" });
                }
                else
                {
                    arrowImage.sprite = Ostranauts.Bit.SpriteUtility.FindSpriteByKeywords(new[] { "GUIArrowR" });
                }
            }
        }
        
        public bool IsExpanded()
        {
            return isExpanded;
        }
        
        public void AddChildWidget(GameObject childWidget)
        {
            if (childWidget != null)
            {
                childWidgets.Add(childWidget);
            }
        }
        
        public List<GameObject> GetChildWidgets()
        {
            return childWidgets;
        }
        
        public void ClearChildren()
        {
            foreach (var child in childWidgets)
            {
                if (child != null)
                {
                    Destroy(child);
                }
            }
            childWidgets.Clear();
        }

        public void SetExpansionClickHandler(UnityEngine.Events.UnityAction callback)
        {
            if (expansionButtonComponent != null)
            {
                expansionButtonComponent.onClick.RemoveAllListeners();
                expansionButtonComponent.onClick.AddListener(callback);
            }
        }

        public void SetActionClickHandler(UnityEngine.Events.UnityAction callback)
        {
            if (actionButtonComponent != null)
            {
                actionButtonComponent.onClick.RemoveAllListeners();
                actionButtonComponent.onClick.AddListener(callback);
            }
        }

        /// <summary>
        /// Show or hide the arrow icon
        /// </summary>
        public void SetArrowVisible(bool visible)
        {
            if (arrowImage != null)
            {
                arrowImage.enabled = visible;
            }
            
            // If hiding arrow, also disable expansion button functionality
            if (!visible && expansionButtonComponent != null)
            {
                expansionButtonComponent.interactable = false;
            }
        }

        /// <summary>
        /// Set the action button icon sprite
        /// </summary>
        public void SetActionIcon(Sprite sprite)
        {
            if (actionIconImage != null)
            {
                actionIconImage.sprite = sprite;
            }
        }

        /// <summary>
        /// Set the action button icon color
        /// </summary>
        public void SetActionIconColor(Color color)
        {
            if (actionIconImage != null)
            {
                actionIconImage.color = color;
            }
        }
    }
}

