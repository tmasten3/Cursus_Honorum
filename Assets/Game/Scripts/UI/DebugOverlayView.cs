using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public sealed class DebugOverlayView : MonoBehaviour
    {
        [Header("Canvas")]
        [SerializeField]
        private Canvas overlayCanvas;

        [SerializeField]
        private CanvasScaler canvasScaler;

        [SerializeField]
        private GraphicRaycaster raycaster;

        [Header("Layout")]
        [SerializeField]
        private RectTransform panelRect;

        [SerializeField]
        private VerticalLayoutGroup panelLayout;

        [Header("Primary Labels")]
        [SerializeField]
        private TMP_Text dateText;

        [SerializeField]
        private TMP_Text livingText;

        [SerializeField]
        private TMP_Text familyText;

        [Header("Logs")]
        [SerializeField]
        private GameObject logContainer;

        [SerializeField]
        private TMP_Text logText;

        [SerializeField]
        private ScrollRect logScrollRect;

        [Header("Sections")]
        [SerializeField]
        private Transform sectionParent;

        [SerializeField]
        private TMP_Text officeHeaderText;

        [SerializeField]
        private TMP_Text officeListText;

        [SerializeField]
        private TMP_Text electionHeaderText;

        [SerializeField]
        private TMP_Text electionListText;

        private bool initialized;

        public Canvas OverlayCanvas => overlayCanvas;
        public CanvasScaler Scaler => canvasScaler;
        public ScrollRect LogScrollRect => logScrollRect;
        public GameObject LogContainer => logContainer;
        public Transform SectionParent => sectionParent != null ? sectionParent : transform;
        public bool HasElectionEntries => electionListText != null && !string.IsNullOrEmpty(electionListText.text);

        public void EnsureInitialized()
        {
            if (initialized)
                return;

            overlayCanvas = overlayCanvas != null ? overlayCanvas : GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
            canvasScaler = canvasScaler != null ? canvasScaler : GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
            raycaster = raycaster != null ? raycaster : GetComponent<GraphicRaycaster>() ?? gameObject.AddComponent<GraphicRaycaster>();

            ConfigureCanvas();
            BuildLayoutIfNeeded();

            initialized = true;
        }

        public void SetDate(string text)
        {
            if (dateText != null)
                dateText.text = text ?? string.Empty;
        }

        public void SetLivingPopulation(string text)
        {
            if (livingText != null)
                livingText.text = text ?? string.Empty;
        }

        public void SetFamilyCount(string text)
        {
            if (familyText != null)
                familyText.text = text ?? string.Empty;
        }

        public void SetOfficeSection(string header, string body)
        {
            if (officeHeaderText != null)
                officeHeaderText.text = header ?? string.Empty;

            if (officeListText != null)
                officeListText.text = body ?? string.Empty;
        }

        public void SetElectionSection(string header, string body)
        {
            if (electionHeaderText != null)
                electionHeaderText.text = header ?? string.Empty;

            if (electionListText != null)
                electionListText.text = body ?? string.Empty;
        }

        public void SetLogText(string text)
        {
            if (logText != null)
                logText.text = text ?? string.Empty;
        }

        public void ScrollLogsToBottom()
        {
            if (logScrollRect != null)
                logScrollRect.verticalNormalizedPosition = 0f;
        }

        public void SetLogVisibility(bool visible)
        {
            if (logContainer != null)
                logContainer.SetActive(visible);
        }

        private void ConfigureCanvas()
        {
            if (overlayCanvas == null)
                return;

            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = short.MaxValue;

            if (canvasScaler != null)
            {
                canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
                canvasScaler.matchWidthOrHeight = 1f;
            }
        }

        private void BuildLayoutIfNeeded()
        {
            if (panelRect != null && dateText != null && livingText != null && familyText != null && logText != null)
                return;

            var rectTransform = (RectTransform)transform;
            if (rectTransform != null)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
            }

            var panel = panelRect == null
                ? CreateChild("Panel", transform, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup))
                : panelRect.gameObject;
            panelRect = (RectTransform)panel.transform;

            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(16f, -16f);
            panelRect.sizeDelta = new Vector2(480f, 720f);

            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.65f);

            panelLayout = panel.GetComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(12, 12, 12, 12);
            panelLayout.spacing = 8f;
            panelLayout.childAlignment = TextAnchor.UpperLeft;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            dateText = dateText != null ? dateText : CreateLabel(panel.transform, "DateText", "Date: --");
            livingText = livingText != null ? livingText : CreateLabel(panel.transform, "LivingText", "Living: 0");
            familyText = familyText != null ? familyText : CreateLabel(panel.transform, "FamilyText", "Families: 0");

            if (logText == null)
            {
                logText = CreateLogArea(panel.transform, out logContainer, out logScrollRect);
            }

            if (sectionParent == null)
                sectionParent = panel.transform;

        }

        public void EnsureOfficeAndElectionSections()
        {
            EnsureInitialized();

            var target = sectionParent != null ? sectionParent : transform;

            if (officeHeaderText == null)
                officeHeaderText = CreateLabel(target, "OfficeHeader", "Offices");

            if (officeListText == null)
            {
                officeListText = CreateLabel(target, "OfficeList", string.Empty);
                officeListText.textWrappingMode = TextWrappingModes.Normal;
                officeListText.alignment = TextAlignmentOptions.TopLeft;
            }

            if (electionHeaderText == null)
                electionHeaderText = CreateLabel(target, "ElectionHeader", "Recent Elections");

            if (electionListText == null)
            {
                electionListText = CreateLabel(target, "ElectionList", string.Empty);
                electionListText.textWrappingMode = TextWrappingModes.Normal;
                electionListText.alignment = TextAlignmentOptions.TopLeft;
            }
        }

        private static GameObject CreateChild(string name, Transform parent, params Type[] components)
        {
            var go = new GameObject(name, components);
            go.transform.SetParent(parent, false);
            return go;
        }

        private TMP_Text CreateLabel(Transform parent, string name, string initialText)
        {
            var go = CreateChild(name, parent, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(0f, 28f);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = initialText;
            text.fontSize = 22f;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.alignment = TextAlignmentOptions.Left;

            return text;
        }

        private TMP_Text CreateLogArea(Transform parent, out GameObject container, out ScrollRect scrollRect)
        {
            container = CreateChild("LogArea", parent, typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(ScrollRect));

            var rect = (RectTransform)container.transform;
            rect.sizeDelta = new Vector2(0f, 200f);

            var layoutElement = container.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 200f;
            layoutElement.flexibleHeight = 1f;

            var bg = container.GetComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.05f);

            scrollRect = container.GetComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            var viewport = CreateChild("Viewport", container.transform, typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            var viewportRect = (RectTransform)viewport.transform;
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

            var content = CreateChild("Content", viewport.transform, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(ContentSizeFitter));
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0f, 1f);
            contentRect.offsetMin = new Vector2(8f, 0f);
            contentRect.offsetMax = new Vector2(-8f, 0f);
            contentRect.sizeDelta = Vector2.zero;

            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var text = content.GetComponent<TextMeshProUGUI>();
            text.text = "Logs will appear here.";
            text.fontSize = 18f;
            text.color = new Color(0.85f, 0.9f, 1f);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;

            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;

            return text;
        }
    }
}
