using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class DebugOverlayBuilder
    {
        private readonly RectTransform root;

        private RectTransform layoutRoot;
        private GameObject contentContainer;

        private TextMeshProUGUI dateText;
        private TextMeshProUGUI tickRateText;
        private TextMeshProUGUI speedText;
        private TextMeshProUGUI pauseStateText;

        private TextMeshProUGUI livingCountText;
        private TextMeshProUGUI familyCountText;
        private TextMeshProUGUI todayStatsText;
        private RectTransform populationHistoryContent;
        private ScrollRect populationHistoryScroll;
        private readonly List<TextMeshProUGUI> populationHistoryRows = new();

        private TextMeshProUGUI officeHoldersText;
        private TextMeshProUGUI upcomingElectionsText;
        private TextMeshProUGUI recentResultsText;
        private TextMeshProUGUI recentAppointmentsText;

        public DebugOverlayBuilder(RectTransform root)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public void Build()
        {
            ConfigureRoot();
            BuildColumns();
        }

        public void SetRootActive(bool visible)
        {
            if (contentContainer != null)
                contentContainer.SetActive(visible);
        }

        public void UpdateSimulation(DebugOverlayDataAdapter.SimulationData data)
        {
            if (dateText != null)
                dateText.text = data.DateLine;

            if (tickRateText != null)
                tickRateText.text = data.TickRateLine;

            if (speedText != null)
                speedText.text = data.SpeedLine;

            if (pauseStateText != null)
                pauseStateText.text = data.StateLine;
        }

        public void UpdatePopulation(DebugOverlayDataAdapter.PopulationData data)
        {
            if (livingCountText != null)
                livingCountText.text = data.LivingLine;

            if (familyCountText != null)
                familyCountText.text = data.FamilyLine;

            if (todayStatsText != null)
                todayStatsText.text = data.TodayLine;

            UpdatePopulationHistory(data.HistoryLines);
        }

        public void UpdatePolitics(DebugOverlayDataAdapter.PoliticsData data)
        {
            if (officeHoldersText != null)
                officeHoldersText.text = BuildMultilineText(data.CurrentOfficeLines, "No active office holders.");

            if (upcomingElectionsText != null)
                upcomingElectionsText.text = BuildMultilineText(data.UpcomingElectionLines, "No scheduled elections.");

            if (recentResultsText != null)
                recentResultsText.text = BuildMultilineText(data.RecentElectionResults, "No recent election results.");

            if (recentAppointmentsText != null)
                recentAppointmentsText.text = BuildMultilineText(data.RecentAppointments, "No recent appointments.");
        }

        private void ConfigureRoot()
        {
            root.gameObject.name = "DebugOverlay";

            if (!root.TryGetComponent(out Image background))
            {
                background = root.gameObject.AddComponent<Image>();
            }

            background.color = new Color(0f, 0f, 0f, 0.55f);
            background.raycastTarget = false;
        }

        private void BuildColumns()
        {
            layoutRoot = CreateChild("ColumnLayout", root);
            layoutRoot.offsetMin = new Vector2(12f, 12f);
            layoutRoot.offsetMax = new Vector2(-12f, -12f);

            var layoutGroup = layoutRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.spacing = 16f;
            layoutGroup.padding = new RectOffset(16, 16, 16, 16);
            layoutGroup.childAlignment = TextAnchor.UpperLeft;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;

            contentContainer = layoutRoot.gameObject;

            BuildSimulationColumn(CreateColumn("SimulationColumn"));
            BuildPopulationColumn(CreateColumn("PopulationColumn"));
            BuildPoliticsColumn(CreateColumn("PoliticsColumn"));
        }

        private RectTransform CreateColumn(string name)
        {
            var column = CreateChild(name, layoutRoot);
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var element = column.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            element.minWidth = 0f;

            var background = column.gameObject.AddComponent<Image>();
            background.color = new Color(0.08f, 0.1f, 0.14f, 0.75f);
            background.raycastTarget = false;

            return column;
        }

        private void BuildSimulationColumn(RectTransform column)
        {
            CreateSectionHeader(column, "Simulation / Time");
            dateText = CreateBodyText(column);
            tickRateText = CreateBodyText(column);
            speedText = CreateBodyText(column);
            pauseStateText = CreateBodyText(column);
        }

        private void BuildPopulationColumn(RectTransform column)
        {
            CreateSectionHeader(column, "Population");
            livingCountText = CreateBodyText(column);
            familyCountText = CreateBodyText(column);
            todayStatsText = CreateBodyText(column);
            CreateSubHeader(column, "Last 30 Daily Population Ticks");
            populationHistoryContent = CreateScrollArea(column, 200f, out populationHistoryScroll);
        }

        private void BuildPoliticsColumn(RectTransform column)
        {
            CreateSectionHeader(column, "Politics");
            CreateSubHeader(column, "Current Office Holders");
            officeHoldersText = CreateBodyText(column, flexible: true);

            CreateSubHeader(column, "Upcoming Elections");
            upcomingElectionsText = CreateBodyText(column, flexible: true);

            CreateSubHeader(column, "Recent Election Results");
            recentResultsText = CreateBodyText(column, flexible: true);

            CreateSubHeader(column, "Recent Office Appointments");
            recentAppointmentsText = CreateBodyText(column, flexible: true);
        }

        private TextMeshProUGUI CreateSectionHeader(Transform parent, string text)
        {
            var label = CreateText(parent, FontStyles.Bold);
            label.fontSize = 24f;
            label.text = text;
            return label;
        }

        private TextMeshProUGUI CreateSubHeader(Transform parent, string text)
        {
            var label = CreateText(parent, FontStyles.Bold);
            label.fontSize = 18f;
            label.text = text;
            return label;
        }

        private TextMeshProUGUI CreateBodyText(Transform parent, bool flexible = false)
        {
            var text = CreateText(parent, FontStyles.Normal);
            text.fontSize = 16f;
            text.lineSpacing = 2f;
            text.enableWordWrapping = true;

            if (flexible)
            {
                var element = text.gameObject.AddComponent<LayoutElement>();
                element.flexibleHeight = 1f;
                element.minHeight = 60f;
            }

            return text;
        }

        private TextMeshProUGUI CreateText(Transform parent, FontStyles style)
        {
            var go = new GameObject("TMP_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(0f, 0f);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = string.Empty;
            text.fontSize = 18f;
            text.fontStyle = style;
            text.color = new Color(0.85f, 0.9f, 1f, 0.95f);
            text.alignment = TextAlignmentOptions.Left;
            text.enableWordWrapping = true;
            text.raycastTarget = false;

            return text;
        }

        private RectTransform CreateScrollArea(Transform parent, float minHeight, out ScrollRect scrollRect)
        {
            var scrollRoot = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollRoot.transform.SetParent(parent, false);

            var rect = (RectTransform)scrollRoot.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(0f, minHeight);

            var image = scrollRoot.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.35f);
            image.raycastTarget = true;

            var layoutElement = scrollRoot.AddComponent<LayoutElement>();
            layoutElement.minHeight = minHeight;
            layoutElement.flexibleHeight = 1f;

            scrollRect = scrollRoot.GetComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 20f;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollRoot.transform, false);
            var viewportRect = (RectTransform)viewport.transform;
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportRect.pivot = new Vector2(0f, 1f);

            var viewportImage = viewport.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 0f);

            var contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 4f;
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;

            populationHistoryContent = contentRect;
            populationHistoryRows.Clear();

            return contentRect;
        }

        private RectTransform CreateChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        private void UpdatePopulationHistory(IReadOnlyList<string> entries)
        {
            if (populationHistoryContent == null)
                return;

            int desired = entries?.Count ?? 0;
            EnsurePopulationHistoryCapacity(desired);

            for (int i = 0; i < populationHistoryRows.Count; i++)
            {
                var row = populationHistoryRows[i];
                bool active = i < desired;
                row.gameObject.SetActive(active);

                if (active)
                    row.text = entries[i];
                else
                    row.text = string.Empty;
            }

            if (populationHistoryScroll != null && desired > 0)
            {
                populationHistoryScroll.verticalNormalizedPosition = 0f;
            }
        }

        private void EnsurePopulationHistoryCapacity(int desired)
        {
            while (populationHistoryRows.Count < desired)
            {
                var entry = CreateText(populationHistoryContent, FontStyles.Normal);
                entry.fontSize = 15f;
                entry.lineSpacing = 1.5f;
                populationHistoryRows.Add(entry);
            }
        }

        private static string BuildMultilineText(IReadOnlyList<string> lines, string fallback)
        {
            if (lines == null || lines.Count == 0)
                return fallback;

            return string.Join("\n", lines);
        }
    }
}
