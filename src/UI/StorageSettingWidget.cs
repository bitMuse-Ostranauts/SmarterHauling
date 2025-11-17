using Ostranauts.Bit;
using Ostranauts.Bit.Items.Categories;
using Ostranauts.Bit.SmarterHauling.Data;
using Ostranauts.Bit.SmarterHauling.Extensions;
using Ostranauts.Core;
using Ostranauts.Components;
using Ostranauts.UI.MegaToolTip;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ostranauts.Bit.SmarterHauling.UI
{
    public class StorageSettingWidget : MonoBehaviour, IEventSystemHandler
    {
        private CondOwner _coTarget;
        private CanvasGroup cg;
        private static StorageSettingWidget _instance;
        private static GameObject _goInstance;
        
        // MegaTooltip positioning
        private RectTransform _megaTooltipRect;
        private RectTransform _megaTooltipContentRect; // The actual content container with dynamic size
        private RectTransform _rectTransform;
        private bool _megaTooltipSearched = false;
        private static CondOwner _lastShownCondOwner; // Track which CondOwner we're showing for
        
        // Debug - track last position and size to only log on changes
        private Vector3 _lastMegaTooltipPosition = Vector3.zero;
        private Vector2 _lastMegaTooltipSize = Vector2.zero;
        private Vector3 _lastWidgetPosition = Vector3.zero;
        private const float POSITION_CHANGE_THRESHOLD = 1f; // Log if position or size changes by more than 1 pixel
        private List<StoragePrefListItem> _storagePrefListItems = new List<StoragePrefListItem>();
        
        /// <summary>
        /// Event fired when storage preferences are changed for a container
        /// </summary>
        public static System.Action<string> OnPreferencesChanged;

        /// <summary>
        /// Property to get/set the CondOwner target
        /// </summary>
        public CondOwner COTarget
        {
            get { return _coTarget; }
            set { _coTarget = value; }
        }

        /// <summary>
        /// Show the StorageSettingWidget for a given CondOwner, centered on screen.
        /// </summary>
        public static void Show(CondOwner condOwner)
        {
            if (_goInstance == null) {
                _goInstance = new GameObject("StorageSettingWidget", typeof(CanvasRenderer), typeof(CanvasGroup), typeof(StorageSettingWidget), typeof(RectTransform));
                _goInstance.transform.SetParent(CanvasManager.instance.goCanvasCrewBar.transform, false);
                _goInstance.layer = LayerMask.NameToLayer("UI");

                // Add the StorageSettingWidget component first (this assigns _instance)
                _instance = _goInstance.GetComponent<StorageSettingWidget>();
                
                // Now add and configure the CanvasGroup
                _instance.cg = _goInstance.GetComponent<CanvasGroup>();
                _instance.cg.alpha = 0f;
                _instance.cg.interactable = false;
                _instance.cg.blocksRaycasts = false;
                
                // Call SetupUI to set up the interface (assuming SetupUI does all required initialization)
                try { 
                    _instance.SetupUI();
                }
                catch (System.Exception ex)
                {
                    Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogError($"[StorageSettingWidget] SetupUI failed: {ex.Message}");
                    Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogError($"[StorageSettingWidget] Stack trace: {ex.StackTrace}");
                }
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo("[StorageSettingWidget] Widget created and SetupUI called");
            }

            _instance.COTarget = condOwner;
            _lastShownCondOwner = condOwner; // Track which CondOwner we're showing for
            _instance.cg.alpha = 1f;
            _instance.cg.interactable = true;
            _instance.cg.blocksRaycasts = true;
            
            // Refresh toggle statuses based on current prefs
            _instance.RefreshAllToggleStatuses();
            
            Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo("[StorageSettingWidget] Widget shown");
        }

        /// <summary>
        /// Hides the StorageSettingWidget, if it exists.
        /// </summary>
        public static void Hide()
        {
            if (_instance == null || _instance.cg == null)
                return;

            _instance.cg.alpha = 0f;
            _instance.cg.interactable = false;
            _instance.cg.blocksRaycasts = false;
        }

        public void SetupUI()
        {
            Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo("[StorageSettingWidget] SetupUI started");

            // Set up RectTransform for positioning (200x400 pixels)
            RectTransform rectTransform = GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(200f, 400f); // 200 wide x 400 tall
            rectTransform.anchoredPosition = Vector2.zero; // Will be updated by UpdatePosition()

            // Image image = gameObject.AddComponent<Image>();
            
            // Cache the RectTransform for later use
            _rectTransform = rectTransform;

            GameObject topBar = new GameObject("TopBar", typeof(RectTransform));
            topBar.transform.SetParent(transform, false);
            topBar.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 1f);  // Top-left
            topBar.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);  // Top-right
            topBar.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 18f); // 18 pixels tall
            
            GameObject topBarBg = new GameObject("TopBarBg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            topBarBg.transform.SetParent(topBar.transform, false);
            topBarBg.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0f);
            topBarBg.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            topBarBg.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            topBarBg.GetComponent<Image>().sprite = Ostranauts.Bit.SpriteUtility.FindSpriteByKeywords(new[] { "background", "panel", "desaturated" });
            topBarBg.GetComponent<Image>().color = new Color(0.424f, 0.459f, 0.490f, 1.000f);

            GameObject topBarBgImage = new GameObject("TopBarBgImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            topBarBgImage.transform.SetParent(topBarBg.transform, false);
            topBarBgImage.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0f);
            topBarBgImage.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            topBarBgImage.GetComponent<RectTransform>().sizeDelta = new Vector2(-2f, -2f);
            topBarBgImage.GetComponent<Image>().sprite = Ostranauts.Bit.SpriteUtility.FindSpriteByKeywords(new[] { "GUIMenuBackgroundSpriteE" });
            topBarBgImage.GetComponent<Image>().color = new Color(0.231f, 0.227f, 0.239f, 1.000f);

            GameObject topBarText = new GameObject("TopBarText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));   
            topBarText.transform.SetParent(topBar.transform, false);
            topBarText.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0f);
            topBarText.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            topBarText.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            topBarText.GetComponent<TextMeshProUGUI>().text = "Storage Settings";
            topBarText.GetComponent<TextMeshProUGUI>().fontSize = 10;
            topBarText.GetComponent<TextMeshProUGUI>().color = Color.white;
            topBarText.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
            topBarText.GetComponent<TextMeshProUGUI>().enableWordWrapping = false;

            // Create CenterPanel
            GameObject centerPanel = new GameObject("CenterPanel", typeof(CanvasRenderer), typeof(RectTransform), typeof(Image));
            centerPanel.layer = LayerMask.NameToLayer("UI");
            centerPanel.transform.SetParent(transform, false);
            RectTransform centerPanelRect = centerPanel.GetComponent<RectTransform>();
            centerPanelRect.anchorMin = new Vector2(0f, 0f);
            centerPanelRect.anchorMax = new Vector2(1f, 1f);
            centerPanelRect.pivot = new Vector2(0.5f, 0.5f);
            centerPanelRect.anchoredPosition = new Vector2(0f, 1f);
            centerPanelRect.sizeDelta = new Vector2(0f, -29f);

            GameObject scrollList = new GameObject("ScrollList", typeof(CanvasRenderer), typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollList.transform.SetParent(centerPanel.transform, false);
            scrollList.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;;
            scrollList.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0f);
            scrollList.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            scrollList.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            scrollList.GetComponent<Image>().sprite = Ostranauts.Bit.SpriteUtility.FindSpriteByKeywords(new[] { "background", "panel", "desaturated" });
            scrollList.GetComponent<Image>().color = new Color(0.231f, 0.227f, 0.239f, 1.000f);
            scrollList.GetComponent<Image>().type = Image.Type.Tiled;

            GameObject scrollMask = new GameObject("ScrollMask", typeof(CanvasRenderer), typeof(RectTransform), typeof(Image), typeof(Mask));
            scrollMask.transform.SetParent(scrollList.transform, false);
            scrollMask.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0f);
            scrollMask.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            scrollMask.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
            scrollMask.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f);
            scrollMask.GetComponent<RectTransform>().sizeDelta = new Vector2(-8f, 0f); // Reserve 8 pixels on the right for scrollbar
            scrollMask.GetComponent<Mask>().showMaskGraphic = false;
            scrollMask.GetComponent<Image>().sprite = Ostranauts.Bit.SpriteUtility.FindSpriteByKeywords(new[] { "100xSquare" });

            GameObject content = new GameObject("Content", typeof(CanvasRenderer), typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(scrollMask.transform, false);
            content.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
            content.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            content.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 1.0f);
            content.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1.0f);
            content.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            content.GetComponent<VerticalLayoutGroup>().spacing = 2f;
            content.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
            content.GetComponent<VerticalLayoutGroup>().childControlWidth = true;
            content.GetComponent<VerticalLayoutGroup>().childControlHeight = false;
            content.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(4, 4, 0, 0);

            // Create scrollbar
            GameObject scrollbar = new GameObject("Scrollbar Vertical", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
            scrollbar.layer = LayerMask.NameToLayer("UI");
            scrollbar.SetActive(true);
            scrollbar.transform.SetParent(scrollList.transform, false);
            scrollbar.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            scrollbar.GetComponent<RectTransform>().sizeDelta = new Vector2(8f, 0f); // 8 pixels wide
            scrollbar.GetComponent<RectTransform>().pivot = Vector2.one;
            scrollbar.GetComponent<RectTransform>().anchorMin = new Vector2(1f, 0f);
            scrollbar.GetComponent<RectTransform>().anchorMax = Vector2.one;
            scrollbar.GetComponent<Scrollbar>().size = 1f;
            scrollbar.GetComponent<Scrollbar>().value = 1f;
            scrollbar.GetComponent<Scrollbar>().numberOfSteps = 0;
            scrollbar.GetComponent<Scrollbar>().direction = Scrollbar.Direction.BottomToTop;
            scrollbar.GetComponent<Image>().sprite = Ostranauts.Bit.SpriteUtility.FindSpriteByKeywords(new[] { "Background" });
            scrollbar.GetComponent<Image>().color = Color.black;
            scrollbar.GetComponent<Image>().type = Image.Type.Sliced;

            // Create BottomFader
            // GameObject bottomFader = new GameObject("BottomFader", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            // bottomFader.layer = LayerMask.NameToLayer("UI");
            // bottomFader.transform.SetParent(scrollbar.transform, false);
            // bottomFader.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            // bottomFader.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 15f);
            // bottomFader.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
            // bottomFader.GetComponent<RectTransform>().anchorMin = new Vector2(-49f, 0f);
            // bottomFader.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0f);
            // bottomFader.GetComponent<Image>().sprite = Ostranauts.Bit.SpriteUtility.FindSpriteByKeywords(new[] { "gradient-upwards" });
            // bottomFader.GetComponent<Image>().color = Color.black;
            // bottomFader.GetComponent<Image>().type = Image.Type.Simple;

            // Create Sliding Area
            GameObject slidingArea = new GameObject("Sliding Area", typeof(RectTransform));
            slidingArea.layer = LayerMask.NameToLayer("UI");
            slidingArea.transform.SetParent(scrollbar.transform, false);
            slidingArea.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            slidingArea.GetComponent<RectTransform>().sizeDelta = new Vector2(-2f, -20f);
            slidingArea.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            slidingArea.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            slidingArea.GetComponent<RectTransform>().anchorMax = Vector2.one;

            // Create Handle
            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handle.layer = LayerMask.NameToLayer("UI");
            handle.SetActive(true);
            handle.transform.SetParent(slidingArea.transform, false);
            handle.GetComponent<RectTransform>().anchoredPosition = new Vector2(-0.5f, 0f);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(3f, 20f);
            handle.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            handle.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            handle.GetComponent<RectTransform>().anchorMax = Vector2.one;
            handle.GetComponent<Image>().sprite = Ostranauts.Bit.SpriteUtility.FindSpriteByKeywords(new[] { "UISprite" });
            handle.GetComponent<Image>().color = new Color(0.212f, 0.212f, 0.212f, 1.000f);
            handle.GetComponent<Image>().type = Image.Type.Sliced;
            
            // Link handle to scrollbar
            scrollbar.GetComponent<Scrollbar>().handleRect = handle.GetComponent<RectTransform>();

            // Finish setting up scrolllist
            scrollList.GetComponent<ScrollRect>().content = content.GetComponent<RectTransform>();
            scrollList.GetComponent<ScrollRect>().viewport = scrollMask.GetComponent<RectTransform>(); 
            scrollList.GetComponent<ScrollRect>().horizontal = false;
            scrollList.GetComponent<ScrollRect>().vertical = true;
            scrollList.GetComponent<ScrollRect>().movementType = ScrollRect.MovementType.Clamped;
            scrollList.GetComponent<ScrollRect>().verticalScrollbar = scrollbar.GetComponent<Scrollbar>();
            scrollList.GetComponent<ScrollRect>().verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scrollList.GetComponent<ScrollRect>().verticalScrollbarSpacing = -3f;
            scrollList.GetComponent<ScrollRect>().scrollSensitivity = 2.4f; // Double the default sensitivity (default is ~1)

            // Making a sample category
            // Create category widget
            // Iterate over top-level categories
            var rootCategories = LaunchControl.Instance.Items.Categories.GetRootCategories();
            foreach (var category in rootCategories)
            {
                // Skip empty root categories
                if (!HasAnyItems(category))
                {
                    Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogDebug($"Skipping empty root category: {category.DisplayName}");
                    continue;
                }
                
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo("Showing category " + category.DisplayName);
                CreateStoragePrefListItem(content.transform, category, 4f);
            }
            
            // Force layout rebuild (layouts can be fucky wucky)
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content.GetComponent<RectTransform>());
            LayoutRebuilder.ForceRebuildLayoutImmediate(centerPanel.GetComponent<RectTransform>());

            // Mark as initialized
            Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo("[StorageSettingWidget] SetupUI completed");
        }

        /// <summary>
        /// Find and cache the MegaTooltip RectTransform for positioning
        /// </summary>
        private void FindMegaTooltip()
        {
            if (_megaTooltipSearched)
                return;
                
            _megaTooltipSearched = true;
            
            try
            {
                // Try to find pnlMegaTooltip via CanvasManager
                GameObject canvasCrewBar = CanvasManager.instance?.goCanvasCrewBar;
                if (canvasCrewBar != null)
                {
                    Transform megaTooltipTransform = canvasCrewBar.transform.Find("pnlMegaTooltip");
                    if (megaTooltipTransform != null)
                    {
                        _megaTooltipRect = megaTooltipTransform.GetComponent<RectTransform>();
                        Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[StorageSettingWidget] Found MegaTooltip via CanvasManager.goCanvasCrewBar");
                    }
                }
                
                // Fallback: try GameObject.Find
                if (_megaTooltipRect == null)
                {
                    GameObject megaTooltipObj = GameObject.Find("pnlMegaTooltip");
                    if (megaTooltipObj != null)
                    {
                        _megaTooltipRect = megaTooltipObj.GetComponent<RectTransform>();
                        Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[StorageSettingWidget] Found MegaTooltip via GameObject.Find");
                    }
                }
                
                if (_megaTooltipRect == null)
                {
                    Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogWarning($"[StorageSettingWidget] Could not find pnlMegaTooltip!");
                }
                else
                {
                    Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[StorageSettingWidget] MegaTooltip cached. AnchoredPos: {_megaTooltipRect.anchoredPosition}, Size: {_megaTooltipRect.sizeDelta}");
                    
                    // Find the actual content container (TooltipSelectionStack) which has the dynamic size
                    Transform contentTransform = _megaTooltipRect.Find("TooltipSelectionStack");
                    if (contentTransform != null)
                    {
                        _megaTooltipContentRect = contentTransform.GetComponent<RectTransform>();
                        Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[StorageSettingWidget] Found TooltipSelectionStack content. Size: {_megaTooltipContentRect.sizeDelta}, Rect: {_megaTooltipContentRect.rect}");
                    }
                    else
                    {
                        Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogWarning($"[StorageSettingWidget] Could not find TooltipSelectionStack child!");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogError($"[StorageSettingWidget] FindMegaTooltip failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Update the widget's position based on MegaTooltip location
        /// Called every frame when visible
        /// </summary>
        private void UpdatePosition()
        {
            if (_rectTransform == null || _megaTooltipRect == null || _megaTooltipContentRect == null)
                return;
            
            // Use position from parent, size from content container
            Vector3 megaTooltipLocalPos = _megaTooltipRect.localPosition;
            Vector2 megaTooltipContentSize = _megaTooltipContentRect.rect.size; // Use content size (dynamic)
            
            // Check if MegaTooltip position or content size changed significantly
            bool positionChanged = Vector3.Distance(megaTooltipLocalPos, _lastMegaTooltipPosition) > POSITION_CHANGE_THRESHOLD;
            bool sizeChanged = Vector2.Distance(megaTooltipContentSize, _lastMegaTooltipSize) > POSITION_CHANGE_THRESHOLD;
            bool shouldLog = positionChanged || sizeChanged || _lastMegaTooltipPosition == Vector3.zero;
            
            if (shouldLog)
            {
                _lastMegaTooltipPosition = megaTooltipLocalPos;
                _lastMegaTooltipSize = megaTooltipContentSize;
            }
            
            if (shouldLog)
            {
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[StorageSettingWidget] === Position Update (pos changed: {positionChanged}, size changed: {sizeChanged}) ===");
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[StorageSettingWidget] MegaTooltip LocalPos: {megaTooltipLocalPos}");
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[StorageSettingWidget] MegaTooltip Parent Rect: {_megaTooltipRect.rect}");
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[StorageSettingWidget] MegaTooltip Content Size: {megaTooltipContentSize}");
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[StorageSettingWidget] MegaTooltip Content Rect: {_megaTooltipContentRect.rect}");
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[StorageSettingWidget] MegaTooltip Content LocalPos: {_megaTooltipContentRect.localPosition}");
            }
            
            // Use the MegaTooltip PARENT's local X (correct for horizontal positioning)
            // But use the CONTENT's actual center Y (parent Y + content offset Y)
            Vector3 contentLocalPos = _megaTooltipContentRect.localPosition;
            float canvasX = megaTooltipLocalPos.x;
            float canvasY = megaTooltipLocalPos.y + contentLocalPos.y; // Add content Y offset to get actual content center
            
            // Get widget and MegaTooltip dimensions
            float widgetWidth = _rectTransform.rect.width;
            float widgetHeight = _rectTransform.rect.height;
            float widgetHalfWidth = widgetWidth / 2f;
            float widgetHalfHeight = widgetHeight / 2f;
            // Use PARENT width for X positioning (stays consistent)
            // Use CONTENT height for Y positioning (changes with Show More/Less)
            float megaTooltipWidth = _megaTooltipRect.rect.width; // Parent width for positioning
            float megaTooltipHeight = Mathf.Abs(megaTooltipContentSize.y); // Content height (dynamic!)
            float megaTooltipHalfWidth = megaTooltipWidth / 2f;
            float megaTooltipHalfHeight = megaTooltipHeight / 2f;
            
            // Calculate target position:
            // X: widget's RIGHT edge should touch MegaTooltip's LEFT edge
            //    MegaTooltip left = canvasX - megaTooltipHalfWidth
            //    Widget right = widget center X + widgetHalfWidth
            //    So: widget center X = MegaTooltip left - widgetHalfWidth
            // Y: widget's TOP edge should align with MegaTooltip's CENTER
            //    Widget top = widget center Y + widgetHalfHeight
            //    So: widget center Y = MegaTooltip center - widgetHalfHeight
            float targetX = canvasX - megaTooltipHalfWidth - widgetHalfWidth;
            float targetY = canvasY - widgetHalfHeight;
            
            if (shouldLog)
            {
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[StorageSettingWidget] Parent X: {megaTooltipLocalPos.x}, Parent Y + Content Offset: {megaTooltipLocalPos.y} + {contentLocalPos.y} = {canvasY}");
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[StorageSettingWidget] Using content center: ({canvasX}, {canvasY})");
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[StorageSettingWidget] Widget size: {widgetWidth}x{widgetHeight}, half: ({widgetHalfWidth}, {widgetHalfHeight})");
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[StorageSettingWidget] Using parent WIDTH ({_megaTooltipRect.rect.width}) x content HEIGHT ({megaTooltipContentSize.y} -> {megaTooltipHeight})");
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[StorageSettingWidget] MegaTooltip half: ({megaTooltipHalfWidth}, {megaTooltipHalfHeight})");
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[StorageSettingWidget] Target X (widget center): {canvasX} - {megaTooltipHalfWidth} - {widgetHalfWidth} = {targetX}");
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[StorageSettingWidget] Target Y (widget center): {canvasY} - {widgetHalfHeight} = {targetY}");
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[StorageSettingWidget] Widget top edge: {targetY + widgetHalfHeight} (should equal content center {canvasY})");
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[StorageSettingWidget] Content spans: top={canvasY + megaTooltipHalfHeight}, center={canvasY}, bottom={canvasY - megaTooltipHalfHeight}");
            }
            
            // targetX and targetY are already the widget's CENTER positions (no pivot adjustment needed)
            Vector3 finalPosition = new Vector3(targetX, targetY, 0f);
            
            // Check if final position changed significantly
            bool finalPositionChanged = Vector3.Distance(finalPosition, _lastWidgetPosition) > POSITION_CHANGE_THRESHOLD;
            
            if (shouldLog || finalPositionChanged)
            {
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[StorageSettingWidget] Widget current local position: {_rectTransform.localPosition}");
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[StorageSettingWidget] Widget new calculated position: {finalPosition}");
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[StorageSettingWidget] Position changed: {finalPositionChanged}");
                _lastWidgetPosition = finalPosition;
            }
            
            // Apply the position
            _rectTransform.localPosition = finalPosition;
        }

        /// <summary>
        /// Unity Update called every frame
        /// </summary>
        private void Update()
        {
            // Find MegaTooltip if we haven't yet (do this regardless of visibility)
            if (!_megaTooltipSearched)
            {
                FindMegaTooltip();
            }
            
            // Always check if we should hide when MegaTooltip is hidden
            // Use ModuleHost.Opened instead of checking alpha (tooltip animates off screen!)
            if (!ModuleHost.Opened)
            {
                // MegaTooltip is not open (hidden or animating away), hide our widget if it's visible
                if (cg != null && cg.alpha > 0f)
                {
                    Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[StorageSettingWidget] Hiding widget because ModuleHost.Opened = false");
                    Hide();
                }
                return;
            }
            
            // Check if the selected CondOwner has changed
            CondOwner currentSelected = GUIMegaToolTip.Selected;
            if (currentSelected != _lastShownCondOwner)
            {
                // Selection changed, hide our widget
                if (cg != null && cg.alpha > 0f)
                {
                    Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[StorageSettingWidget] Hiding widget because selection changed from {_lastShownCondOwner?.strName} to {currentSelected?.strName}");
                    Hide();
                }
                return;
            }
            
            // Only update position when our widget is visible
            if (cg == null || cg.alpha <= 0f)
                return;
            
            // Update position every frame
            UpdatePosition();
        }
        
        /// <summary>
        /// Gets an item icon sprite from an item ID
        /// </summary>
        private Sprite GetItemIconSprite(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return null;
            
            try
            {
                string portraitImg = null;
                
                // Check if this is a COOverlay first (overlays can have different portrait images)
                JsonCOOverlay overlay;
                if (DataHandler.dictCOOverlays.TryGetValue(itemId, out overlay) && !string.IsNullOrEmpty(overlay.strPortraitImg))
                {
                    portraitImg = overlay.strPortraitImg;
                }
                else
                {
                    // Fall back to regular CondOwner definition
                    JsonCondOwner jco = DataHandler.GetCondOwnerDef(itemId);
                    if (jco != null && !string.IsNullOrEmpty(jco.strPortraitImg))
                    {
                        portraitImg = jco.strPortraitImg;
                    }
                }
                
                if (string.IsNullOrEmpty(portraitImg))
                    return null;
                
                // Load the texture
                Texture2D texture2D = DataHandler.LoadPNG(portraitImg + ".png", false, false);
                if (texture2D == null)
                    return null;
                
                // Convert to sprite
                Sprite sprite = Sprite.Create(
                    texture2D, 
                    new Rect(0f, 0f, (float)texture2D.width, (float)texture2D.height), 
                    new Vector2(0.5f, 0.5f)  // Center pivot
                );
                
                return sprite;
            }
            catch (System.Exception ex)
            {
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogWarning($"[GetItemIconSprite] Failed to get icon for item '{itemId}': {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Creates a category widget and sets up its expansion handler
        /// </summary>
        private StoragePrefListItem CreateStoragePrefListItem(Transform parent, Ostranauts.Bit.Items.Categories.ItemCategory category, float margin)
        {
            bool hasChildren = HasAnyItems(category);
            
            Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogDebug($"[CreateCategoryWidget] Category '{category.DisplayName}' has children: {hasChildren} (subcats: {category.Subcategories?.Count ?? 0}, items: {category.Items?.Count ?? 0})");
            
            // Categories use arrows, not item icons
            StoragePrefListItem widget = StoragePrefListItem.Create(parent, category.DisplayName, margin, hasChildren);
            _storagePrefListItems.Add(widget);
            widget.CategoryIds = new string[] { category.Id };
            
            // Set initial toggle status based on current prefs
            if (_coTarget != null)
            {
                var prefs = ContainerExtensions.GetPrefsById(_coTarget.strID);
                var status = CalculateToggleStatus(prefs, widget.CategoryIds);
                widget.SetToggleStatus(status);
            }
            
            // Set up expansion click handler
            if (hasChildren)
            {
                widget.SetExpansionClickHandler(delegate()
                {
                    ToggleCategoryExpansion(widget, category, margin);
                });
            }
            
            // Set up action click handler
            widget.SetActionClickHandler(delegate()
            {
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[CreateStoragePrefListItem] Action clicked for category: {category.DisplayName}, CategoryIds: [{string.Join(", ", widget.CategoryIds)}]");
                HandleCategoryToggle(widget);
            });
            
            return widget;
        }
        
        /// <summary>
        /// Recursively checks if a category or any of its subcategories have items or predicates
        /// </summary>
        private bool HasAnyItems(Ostranauts.Bit.Items.Categories.ItemCategory category)
        {
            // Check direct items
            if (category.Items != null && category.Items.Count > 0)
                return true;
            
            // Check if category has predicates (dynamic matching)
            if (category.Predicates != null && category.Predicates.Count > 0)
                return true;
            
            // Check subcategories recursively
            if (category.Subcategories != null)
            {
                foreach (var subcat in category.Subcategories)
                {
                    if (HasAnyItems(subcat))
                        return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Recursively collapses all children and grandchildren of a widget
        /// </summary>
        private void CollapseChildrenRecursive(StoragePrefListItem widget)
        {
            if (widget == null)
                return;
            
            // Get a copy of the child list to avoid modification during iteration
            var children = new List<GameObject>();
            foreach (var child in widget.GetChildWidgets())
            {
                children.Add(child);
            }
            
            // Recursively collapse each child first
            foreach (var childObj in children)
            {
                if (childObj == null)
                    continue;
                    
                var childWidget = childObj.GetComponent<StoragePrefListItem>();
                if (childWidget != null && childWidget.IsExpanded())
                {
                    // Recursively collapse this child's children
                    CollapseChildrenRecursive(childWidget);
                    childWidget.SetExpanded(false);
                }
            }
            
            // Now clear all children from this widget
            widget.ClearChildren();
        }
        
        /// <summary>
        /// Toggles expansion of a category widget
        /// </summary>
        private void ToggleCategoryExpansion(StoragePrefListItem widget, Ostranauts.Bit.Items.Categories.ItemCategory category, float margin)
        {
            if (widget.IsExpanded())
            {
                // Collapse: recursively collapse and destroy all children
                CollapseChildrenRecursive(widget);
                widget.SetExpanded(false);
            }
            else
            {
                // Expand: create child widgets
                float childMargin = margin + 8f;
                int siblingIndex = widget.transform.GetSiblingIndex() + 1;
                
                // Add subcategories first (only if they have items)
                if (category.Subcategories != null)
                {
                    foreach (var subCategory in category.Subcategories)
                    {
                        // Skip empty categories
                        if (!HasAnyItems(subCategory))
                        {
                            Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogDebug($"[ToggleCategoryExpansion] Skipping empty subcategory '{subCategory.DisplayName}'");
                            continue;
                        }
                        
                        StoragePrefListItem childWidget = CreateStoragePrefListItem(widget.transform.parent, subCategory, childMargin);
                        childWidget.transform.SetSiblingIndex(siblingIndex);
                        siblingIndex++;
                        widget.AddChildWidget(childWidget.gameObject);
                    }
                }
                
                // Add items
                if (category.Items != null)
                {
                    foreach (var itemHintId in category.Items)
                    {
                        // Use the primary ID to get the display name
                        string displayName = GetItemDisplayName(itemHintId.Id);
                        int variantCount = 1 + (itemHintId.AlternativeIds?.Length ?? 0);
                        
                        Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogDebug($"[ToggleCategoryExpansion] Displaying '{displayName}' (represents {variantCount} item variant(s))");
                        
                        GameObject itemWidget = CreateItemWidget(widget.transform.parent, displayName, itemHintId, childMargin);
                        itemWidget.transform.SetSiblingIndex(siblingIndex);
                        siblingIndex++;
                        widget.AddChildWidget(itemWidget);
                    }
                }
                
                widget.SetExpanded(true);
            }
        }
        
        /// <summary>
        /// Creates a simple item widget (without arrow button)
        /// </summary>
        /// <param name="parent">Parent transform</param>
        /// <param name="displayName">Display name for the item</param>
        /// <param name="itemHintId">Item hint ID with all variants</param>
        /// <param name="margin">Left margin</param>
        private GameObject CreateItemWidget(Transform parent, string displayName, Ostranauts.Bit.Items.Categories.ItemHintId itemHintId, float margin)
        {
            // Get item icon sprite
            Sprite itemIcon = GetItemIconSprite(itemHintId.Id);
            
            // Create a CategoryWidget with no children (arrow will be hidden)
            StoragePrefListItem widget = StoragePrefListItem.Create(parent, displayName, margin, hasChildren: false, itemIcon: itemIcon);
            _storagePrefListItems.Add(widget);
            
            // Set category IDs to the primary ID (items use their item ID, not category ID)
            widget.CategoryIds = new string[] { itemHintId.Id };
            
            // Set initial toggle status based on current prefs
            if (_coTarget != null)
            {
                var prefs = ContainerExtensions.GetPrefsById(_coTarget.strID);
                var status = CalculateToggleStatus(prefs, widget.CategoryIds);
                widget.SetToggleStatus(status);
            }

            widget.SetActionClickHandler(delegate()
            {
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[CreateItemWidget] Action clicked for item: {displayName}, CategoryIds: [{string.Join(", ", widget.CategoryIds)}]");
                HandleCategoryToggle(widget);
            });
            
            return widget.gameObject;
        }
        
        /// <summary>
        /// Gets a display name for an item ID, stripping state suffixes
        /// </summary>
        private string GetItemDisplayName(string itemId)
        {
            // Try to get the friendly name from DataHandler
            string friendlyName = DataHandler.GetCOFriendlyName(itemId);
            if (!string.IsNullOrEmpty(friendlyName) && friendlyName != itemId)
            {
                return friendlyName;
            }
            
            // Try short name as fallback
            string shortName = DataHandler.GetCOShortName(itemId);
            if (!string.IsNullOrEmpty(shortName) && shortName != itemId)
            {
                return shortName;
            }
            
            // Final fallback: try to get basic name from dictCOs
            if (DataHandler.dictCOs != null && DataHandler.dictCOs.ContainsKey(itemId))
            {
                var itemData = DataHandler.dictCOs[itemId];
                if (!string.IsNullOrEmpty(itemData.strNameFriendly))
                {
                    return itemData.strNameFriendly;
                }
                if (!string.IsNullOrEmpty(itemData.strName))
                {
                    return itemData.strName;
                }
            }
            
            // Last resort: return item ID
            return itemId;
        }
        
        /// <summary>
        /// Helper method to convert ItemHintId list to string array of IDs (avoiding LINQ for Unity compatibility)
        /// </summary>
        private string[] GetCategoryIdsArray(List<Ostranauts.Bit.Items.Categories.ItemHintId> categories)
        {
            if (categories == null || categories.Count == 0)
            {
                return new string[0];
            }
            
            string[] result = new string[categories.Count];
            for (int i = 0; i < categories.Count; i++)
            {
                result[i] = categories[i].Id;
            }
            return result;
        }
        
        /// <summary>
        /// Helper method to check if a category ID is in the allowed list (avoiding LINQ for Unity compatibility)
        /// </summary>
        private bool IsIdInAllowedCategories(List<Ostranauts.Bit.Items.Categories.ItemHintId> categories, string id)
        {
            if (categories == null || string.IsNullOrEmpty(id))
            {
                return false;
            }
            
            for (int i = 0; i < categories.Count; i++)
            {
                if (categories[i].Id == id)
                {
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// Handles toggling a category/item on/off based on its current status
        /// </summary>
        private void HandleCategoryToggle(StoragePrefListItem widget)
        {
            if (widget == null || widget.CategoryIds == null || widget.CategoryIds.Length == 0)
            {
                return;
            }
            
            if (_coTarget == null)
            {
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogWarning("[HandleCategoryToggle] No target CondOwner");
                return;
            }
            
            // Get or create the storage prefs using the container ID directly
            var prefs = ContainerExtensions.GetOrCreatePrefs(_coTarget.strID);
            if (prefs == null)
            {
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogWarning("[HandleCategoryToggle] Failed to get or create preferences");
                return;
            }
            
            Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[HandleCategoryToggle] ===== Category Toggle Started =====");
            Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[HandleCategoryToggle] Container: {_coTarget.strID}");
            Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[HandleCategoryToggle] Widget CategoryIds: [{string.Join(", ", widget.CategoryIds)}]");
            Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[HandleCategoryToggle] Current Status: {widget.ToggleStatus}");
            Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[HandleCategoryToggle] Prefs BEFORE: [{string.Join(", ", GetCategoryIdsArray(prefs.AllowedCategories))}] (Count: {prefs.AllowedCategories.Count})");
            
            // Determine if this is a category or an item by checking if it exists in the category manager
            string firstId = widget.CategoryIds[0];
            var category = LaunchControl.Instance?.Items?.Categories?.GetCategory(firstId);
            bool isCategory = category != null;
            
            Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[HandleCategoryToggle] Widget type: {(isCategory ? "Category" : "Item")}");
            
            // Handle based on current toggle status
            switch (widget.ToggleStatus)
            {
                case StoragePrefListItem.StoragePrefStatus.All:
                    // Remove this category/item and all children
                    Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[HandleCategoryToggle] Action: REMOVING {(isCategory ? "category and children" : "item")}");
                    if (isCategory)
                    {
                        RemoveCategoriesRecursive(prefs, widget.CategoryIds);
                    }
                    else
                    {
                        // Remove item directly
                        foreach (var itemId in widget.CategoryIds)
                        {
                            prefs.RemoveItem(itemId);
                        }
                    }
                    break;
                    
                case StoragePrefListItem.StoragePrefStatus.Some:
                case StoragePrefListItem.StoragePrefStatus.None:
                    // Enable it (and all children if category)
                    Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[HandleCategoryToggle] Action: ADDING {(isCategory ? "category and children" : "item")} (was {widget.ToggleStatus})");
                    if (isCategory)
                    {
                        AddCategoriesRecursive(prefs, widget.CategoryIds);
                    }
                    else
                    {
                        // Add item directly
                        foreach (var itemId in widget.CategoryIds)
                        {
                            prefs.AddItem(itemId);
                        }
                    }
                    break;
            }
            
            Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[HandleCategoryToggle] Prefs AFTER: [{string.Join(", ", GetCategoryIdsArray(prefs.AllowedCategories))}] (Count: {prefs.AllowedCategories.Count})");
            Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[HandleCategoryToggle] ===== Category Toggle Complete =====");
            
            // Refresh all toggle statuses
            RefreshAllToggleStatuses();
            
            // Notify StorageSettingsModule that preferences changed
            if (OnPreferencesChanged != null)
            {
                OnPreferencesChanged(_coTarget.strID);
            }
        }
        
        /// <summary>
        /// Recursively adds categories, their items, and all their children to the prefs
        /// </summary>
        private void AddCategoriesRecursive(ContainerStoragePrefs prefs, string[] categoryIds)
        {
            if (prefs == null || categoryIds == null)
            {
                return;
            }
            
            foreach (var categoryId in categoryIds)
            {
                // Add this category
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogDebug($"[AddCategoriesRecursive] Adding category: {categoryId}");
                prefs.AddCategory(categoryId);
                
                // Get the category from ItemCategoryManager
                var category = LaunchControl.Instance?.Items?.Categories?.GetCategory(categoryId);
                if (category != null)
                {
                    // Add all items from this category
                    if (category.Items != null && category.Items.Count > 0)
                    {
                        Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogDebug($"[AddCategoriesRecursive] Category {categoryId} has {category.Items.Count} items");
                        foreach (var item in category.Items)
                        {
                            Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogDebug($"[AddCategoriesRecursive] Adding item: {item.Id}");
                            prefs.AddItem(item);
                        }
                    }
                    
                    // Recursively add all subcategories
                    if (category.Subcategories != null && category.Subcategories.Count > 0)
                    {
                        Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogDebug($"[AddCategoriesRecursive] Category {categoryId} has {category.Subcategories.Count} subcategories");
                        foreach (var subCategory in category.Subcategories)
                        {
                            AddCategoriesRecursive(prefs, new string[] { subCategory.Id });
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Recursively removes categories, their items, and all their children from the prefs
        /// </summary>
        private void RemoveCategoriesRecursive(ContainerStoragePrefs prefs, string[] categoryIds)
        {
            if (prefs == null || categoryIds == null)
            {
                return;
            }
            
            foreach (var categoryId in categoryIds)
            {
                // Remove this category
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogDebug($"[RemoveCategoriesRecursive] Removing category: {categoryId}");
                prefs.RemoveCategory(categoryId);
                
                // Get the category from ItemCategoryManager
                var category = LaunchControl.Instance?.Items?.Categories?.GetCategory(categoryId);
                if (category != null)
                {
                    // Remove all items from this category
                    if (category.Items != null && category.Items.Count > 0)
                    {
                        Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogDebug($"[RemoveCategoriesRecursive] Category {categoryId} has {category.Items.Count} items");
                        foreach (var item in category.Items)
                        {
                            Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogDebug($"[RemoveCategoriesRecursive] Removing item: {item.Id}");
                            prefs.RemoveItem(item.Id);
                        }
                    }
                    
                    // Recursively remove all subcategories
                    if (category.Subcategories != null && category.Subcategories.Count > 0)
                    {
                        Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogDebug($"[RemoveCategoriesRecursive] Category {categoryId} has {category.Subcategories.Count} subcategories");
                        foreach (var subCategory in category.Subcategories)
                        {
                            RemoveCategoriesRecursive(prefs, new string[] { subCategory.Id });
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Refreshes the toggle status for all storage pref list items based on the container's current prefs
        /// </summary>
        private void RefreshAllToggleStatuses()
        {
            if (_coTarget == null)
            {
                return;
            }
            
            Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[RefreshAllToggleStatuses] ===== Refresh Started =====");
            Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[RefreshAllToggleStatuses] Container: {_coTarget.strID}");
            Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[RefreshAllToggleStatuses] Total widgets: {_storagePrefListItems.Count}");
            
            // Get the storage prefs using the container ID directly
            var prefs = ContainerExtensions.GetPrefsById(_coTarget.strID);
            
            if (prefs != null)
            {
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[RefreshAllToggleStatuses] Current Prefs: [{string.Join(", ", GetCategoryIdsArray(prefs.AllowedCategories))}] (Count: {prefs.AllowedCategories.Count})");
            }
            else
            {
                Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[RefreshAllToggleStatuses] No prefs found (whitelist is empty)");
            }
            
            // Update each widget
            int updatedCount = 0;
            foreach (var widget in _storagePrefListItems)
            {
                if (widget == null || widget.CategoryIds == null || widget.CategoryIds.Length == 0)
                {
                    continue;
                }
                
                var oldStatus = widget.ToggleStatus;
                var status = CalculateToggleStatus(prefs, widget.CategoryIds);
                widget.SetToggleStatus(status);
                updatedCount++;
                
                if (oldStatus != status)
                {
                    Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogDebug($"[RefreshAllToggleStatuses] Widget [{string.Join(", ", widget.CategoryIds)}]: {oldStatus} -> {status}");
                }
            }
            
            Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[RefreshAllToggleStatuses] Updated {updatedCount} widgets");
            Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogInfo($"[RefreshAllToggleStatuses] ===== Refresh Complete =====");
        }
        
        /// <summary>
        /// Calculates the toggle status for a set of category IDs
        /// </summary>
        private StoragePrefListItem.StoragePrefStatus CalculateToggleStatus(ContainerStoragePrefs prefs, string[] categoryIds)
        {
            if (prefs == null || categoryIds == null || categoryIds.Length == 0)
            {
                return StoragePrefListItem.StoragePrefStatus.None;
            }
            
            // Get all categories (including children) that this widget represents
            var allCategories = new HashSet<string>();
            GetAllCategoryIdsRecursive(categoryIds, allCategories);
            
            if (allCategories.Count == 0)
            {
                return StoragePrefListItem.StoragePrefStatus.None;
            }
            
            // Check how many are enabled in prefs
            int enabledCount = 0;
            foreach (var categoryId in allCategories)
            {
                if (IsIdInAllowedCategories(prefs.AllowedCategories, categoryId))
                {
                    enabledCount++;
                }
            }
            
            // Determine status
            StoragePrefListItem.StoragePrefStatus result;
            if (enabledCount == 0)
            {
                result = StoragePrefListItem.StoragePrefStatus.None;
            }
            else if (enabledCount == allCategories.Count)
            {
                result = StoragePrefListItem.StoragePrefStatus.All;
            }
            else
            {
                result = StoragePrefListItem.StoragePrefStatus.Some;
            }
            
            Ostranauts.Bit.SmarterHauling.SmarterHaulingPlugin.Logger.LogDebug($"[CalculateToggleStatus] Categories: [{string.Join(", ", categoryIds)}] -> AllCategories: {allCategories.Count}, Enabled: {enabledCount}, Status: {result}");
            
            return result;
        }
        
        /// <summary>
        /// Gets all category and item IDs including children recursively
        /// </summary>
        private void GetAllCategoryIdsRecursive(string[] categoryIds, HashSet<string> result)
        {
            if (categoryIds == null || result == null)
            {
                return;
            }
            
            foreach (var categoryId in categoryIds)
            {
                result.Add(categoryId);
                
                // Get the category from ItemCategoryManager
                var category = LaunchControl.Instance?.Items?.Categories?.GetCategory(categoryId);
                if (category != null)
                {
                    // Add all item IDs from this category
                    if (category.Items != null)
                    {
                        foreach (var item in category.Items)
                        {
                            result.Add(item.Id);
                            
                            // Also add alternative IDs
                            if (item.AlternativeIds != null)
                            {
                                foreach (var altId in item.AlternativeIds)
                                {
                                    result.Add(altId);
                                }
                            }
                        }
                    }
                    
                    // Recursively add all subcategories
                    if (category.Subcategories != null)
                    {
                        foreach (var subCategory in category.Subcategories)
                        {
                            GetAllCategoryIdsRecursive(new string[] { subCategory.Id }, result);
                        }
                    }
                }
            }
        }
    }
}