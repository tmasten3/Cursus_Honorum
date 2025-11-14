using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.UI.CharacterDetail
{
    public sealed class CharacterDetailBuilder
    {
        private readonly RectTransform root;

        private GameObject maskObject;
        private Button maskButton;
        private RectTransform panel;
        private Button closeButton;
        private Image classIconImage;
        private TextMeshProUGUI nameText;
        private TextMeshProUGUI metaText;

        private TextMeshProUGUI identityText;
        private TextMeshProUGUI familyText;
        private TextMeshProUGUI officesText;
        private TextMeshProUGUI traitsText;
        private TextMeshProUGUI electionsText;

        public CharacterDetailBuilder(RectTransform root)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public void Build()
        {
            ConfigureRoot();
            BuildMask();
            BuildPanel();
        }

        public void SetRootActive(bool visible)
        {
            if (maskObject != null)
                maskObject.SetActive(visible);
        }

        public void AssignCloseAction(UnityAction action)
        {
            if (closeButton == null)
                return;

            closeButton.onClick.RemoveAllListeners();
            if (action != null)
                closeButton.onClick.AddListener(action);
        }

        public void AssignMaskCloseAction(UnityAction action)
        {
            if (maskButton == null)
                return;

            maskButton.onClick.RemoveAllListeners();
            if (action != null)
                maskButton.onClick.AddListener(action);
        }

        public void UpdateHeader(string name, string meta, Color classColor, bool isAlive)
        {
            if (nameText != null)
                nameText.text = string.IsNullOrWhiteSpace(name) ? "Unknown Citizen" : name;

            if (metaText != null)
            {
                metaText.text = meta ?? string.Empty;
                metaText.color = isAlive ? new Color(0.82f, 0.86f, 0.92f, 1f) : new Color(0.92f, 0.55f, 0.55f, 1f);
            }

            if (classIconImage != null)
            {
                classIconImage.color = classColor;
            }
        }

        public void UpdateIdentity(string text)
        {
            SetSectionText(identityText, text, "No identity information available.");
        }

        public void UpdateFamily(string text)
        {
            SetSectionText(familyText, text, "Family details unavailable.");
        }

        public void UpdateOffices(string text)
        {
            SetSectionText(officesText, text, "No recorded offices.");
        }

        public void UpdateTraits(string text)
        {
            SetSectionText(traitsText, text, "No trait data available.");
        }

        public void UpdateElections(string text)
        {
            SetSectionText(electionsText, text, "No election participation recorded.");
        }

        private void ConfigureRoot()
        {
            root.gameObject.name = "CharacterDetailCanvas";
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
        }

        private void BuildMask()
        {
            var maskRect = CreateChild("Mask", root);
            maskObject = maskRect.gameObject;

            var image = maskObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.65f);
            image.raycastTarget = true;

            maskButton = maskObject.AddComponent<Button>();
            maskButton.transition = Selectable.Transition.ColorTint;
            maskButton.targetGraphic = image;
            var colors = maskButton.colors;
            colors.normalColor = new Color(0f, 0f, 0f, 0.65f);
            colors.highlightedColor = new Color(0f, 0f, 0f, 0.7f);
            colors.pressedColor = new Color(0f, 0f, 0f, 0.75f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0f, 0f, 0f, 0.5f);
            maskButton.colors = colors;
        }

        private void BuildPanel()
        {
            panel = CreateChild("Panel", maskObject.transform as RectTransform);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(840f, 940f);

            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.12f, 0.16f, 0.95f);
            panelImage.raycastTarget = true;

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.padding = new RectOffset(28, 28, 28, 32);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            BuildHeaderSection(panel);
            BuildScrollSection(panel);
        }

        private void BuildHeaderSection(RectTransform parent)
        {
            var header = CreateChild("Header", parent);
            var layout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var headerElement = header.gameObject.AddComponent<LayoutElement>();
            headerElement.minHeight = 110f;

            var iconContainer = CreateChild("ClassIcon", header);
            var iconLayout = iconContainer.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = 72f;
            iconLayout.preferredHeight = 72f;
            iconLayout.minWidth = 72f;
            iconLayout.minHeight = 72f;

            classIconImage = iconContainer.gameObject.AddComponent<Image>();
            classIconImage.color = new Color(0.76f, 0.65f, 0.3f, 1f);
            classIconImage.raycastTarget = false;
            classIconImage.sprite = null;

            var nameContainer = CreateChild("NameBlock", header);
            var nameLayoutElement = nameContainer.gameObject.AddComponent<LayoutElement>();
            nameLayoutElement.flexibleWidth = 1f;
            var nameLayout = nameContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            nameLayout.spacing = 6f;
            nameLayout.childAlignment = TextAnchor.UpperLeft;
            nameLayout.childControlWidth = true;
            nameLayout.childControlHeight = true;
            nameLayout.childForceExpandWidth = true;
            nameLayout.childForceExpandHeight = false;

            nameText = CreateText("Name", nameContainer, FontStyles.Bold);
            nameText.fontSize = 38f;

            metaText = CreateText("Meta", nameContainer, FontStyles.Normal);
            metaText.fontSize = 20f;

            var closeContainer = CreateChild("Close", header);
            var closeLayout = closeContainer.gameObject.AddComponent<LayoutElement>();
            closeLayout.preferredWidth = 132f;
            closeLayout.minHeight = 48f;

            var closeImage = closeContainer.gameObject.AddComponent<Image>();
            closeImage.color = new Color(0.42f, 0.18f, 0.18f, 0.95f);
            closeImage.raycastTarget = true;

            closeButton = closeContainer.gameObject.AddComponent<Button>();
            closeButton.transition = Selectable.Transition.ColorTint;
            closeButton.targetGraphic = closeImage;
            var closeColors = closeButton.colors;
            closeColors.normalColor = new Color(0.42f, 0.18f, 0.18f, 0.95f);
            closeColors.highlightedColor = new Color(0.55f, 0.24f, 0.24f, 0.95f);
            closeColors.pressedColor = new Color(0.3f, 0.12f, 0.12f, 0.95f);
            closeColors.selectedColor = closeColors.normalColor;
            closeColors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);
            closeButton.colors = closeColors;

            var closeLabel = CreateText("Label", closeContainer, FontStyles.Bold);
            closeLabel.alignment = TextAlignmentOptions.Center;
            closeLabel.fontSize = 22f;
            closeLabel.text = "Close";
            closeLabel.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        }

        private void BuildScrollSection(RectTransform parent)
        {
            var scrollRoot = CreateChild("ScrollView", parent);
            var layoutElement = scrollRoot.gameObject.AddComponent<LayoutElement>();
            layoutElement.flexibleHeight = 1f;
            layoutElement.minHeight = 400f;

            var scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 45f;

            var viewport = CreateChild("Viewport", scrollRoot);
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = CreateChild("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);
            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 20f;
            contentLayout.padding = new RectOffset(6, 6, 6, 12);
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            scrollRect.viewport = viewport;
            scrollRect.content = content;

            identityText = CreateSection(content, "Identity");
            familyText = CreateSection(content, "Family");
            officesText = CreateSection(content, "Offices");
            traitsText = CreateSection(content, "Traits");
            electionsText = CreateSection(content, "Election History");
        }

        private TextMeshProUGUI CreateSection(RectTransform parent, string title)
        {
            var header = CreateText(title + "Header", parent, FontStyles.Bold);
            header.fontSize = 26f;
            header.text = title;

            var body = CreateText(title + "Body", parent, FontStyles.Normal);
            body.fontSize = 18f;
            body.color = new Color(0.88f, 0.9f, 0.94f, 1f);

            CreateSpacing(parent, 8f);
            return body;
        }

        private void SetSectionText(TextMeshProUGUI field, string text, string fallback)
        {
            if (field == null)
                return;

            field.text = string.IsNullOrWhiteSpace(text) ? fallback : text;
        }

        private RectTransform CreateChild(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            return rect;
        }

        private TextMeshProUGUI CreateText(string name, Transform parent, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18f;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.margin = new Vector4(0f, 0f, 0f, 0f);
            return text;
        }

        private void CreateSpacing(RectTransform parent, float height)
        {
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(parent, false);
            var layout = spacer.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
        }
    }
}
