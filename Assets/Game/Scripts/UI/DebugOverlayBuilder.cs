using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Minimal UI factory for the debug overlay. Creates a simple column layout so
    /// the overlay can display placeholder information without relying on editor
    /// prefabs.
    /// </summary>
    public sealed class DebugOverlayBuilder
    {
        private readonly RectTransform root;
        private GameObject contentRoot;

        private TextMeshProUGUI dateText;
        private TextMeshProUGUI tickRateText;
        private TextMeshProUGUI speedText;
        private TextMeshProUGUI pauseStateText;

        private TextMeshProUGUI livingCountText;
        private TextMeshProUGUI familyCountText;
        private TextMeshProUGUI todayStatsText;
        private TextMeshProUGUI populationHistoryText;

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
            CreateLayout();
        }

        public void SetRootActive(bool visible)
        {
            if (contentRoot != null)
                contentRoot.SetActive(visible);
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

            if (populationHistoryText != null)
                populationHistoryText.text = BuildMultilineText(data.HistoryLines, "No population history recorded.");
        }

        public void UpdatePolitics(DebugOverlayDataAdapter.PoliticsData data)
        {
            if (officeHoldersText != null)
                officeHoldersText.text = BuildMultilineText(data.CurrentOfficeLines, "No office information available.");

            if (upcomingElectionsText != null)
                upcomingElectionsText.text = BuildMultilineText(data.UpcomingElectionLines, "No scheduled elections.");

            if (recentResultsText != null)
                recentResultsText.text = BuildMultilineText(data.RecentElectionResults, "No election results recorded.");

            if (recentAppointmentsText != null)
                recentAppointmentsText.text = BuildMultilineText(data.RecentAppointments, "No appointments recorded.");
        }

        private void ConfigureRoot()
        {
            root.gameObject.name = "DebugOverlay";

            if (!root.TryGetComponent(out Image background))
                background = root.gameObject.AddComponent<Image>();

            background.color = new Color(0f, 0f, 0f, 0.5f);
            background.raycastTarget = false;
        }

        private void CreateLayout()
        {
            contentRoot = new GameObject("Content", typeof(RectTransform));
            contentRoot.transform.SetParent(root, false);

            var rect = (RectTransform)contentRoot.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(16f, 16f);
            rect.offsetMax = new Vector2(-16f, -16f);

            var layout = contentRoot.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            BuildSimulationSection(CreateSection("Simulation / Time"));
            BuildPopulationSection(CreateSection("Population"));
            BuildPoliticsSection(CreateSection("Politics"));
        }

        private RectTransform CreateSection(string headerText)
        {
            var section = new GameObject(headerText.Replace(' ', '_'), typeof(RectTransform));
            section.transform.SetParent(contentRoot.transform, false);

            var layout = section.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var background = section.AddComponent<Image>();
            background.color = new Color(0.08f, 0.1f, 0.14f, 0.65f);
            background.raycastTarget = false;

            CreateHeader(section.transform, headerText);
            return section.GetComponent<RectTransform>();
        }

        private void BuildSimulationSection(RectTransform section)
        {
            dateText = CreateValueLabel(section.transform);
            tickRateText = CreateValueLabel(section.transform);
            speedText = CreateValueLabel(section.transform);
            pauseStateText = CreateValueLabel(section.transform);
        }

        private void BuildPopulationSection(RectTransform section)
        {
            livingCountText = CreateValueLabel(section.transform);
            familyCountText = CreateValueLabel(section.transform);
            todayStatsText = CreateValueLabel(section.transform);
            populationHistoryText = CreateMultilineLabel(section.transform);
        }

        private void BuildPoliticsSection(RectTransform section)
        {
            officeHoldersText = CreateMultilineLabel(section.transform);
            upcomingElectionsText = CreateMultilineLabel(section.transform);
            recentResultsText = CreateMultilineLabel(section.transform);
            recentAppointmentsText = CreateMultilineLabel(section.transform);
        }

        private void CreateHeader(Transform parent, string text)
        {
            var headerObject = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI));
            headerObject.transform.SetParent(parent, false);

            var rect = (RectTransform)headerObject.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(0f, 0f);

            var label = headerObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 22f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.9f, 0.95f, 1f, 0.95f);
            label.alignment = TextAlignmentOptions.Left;
            label.raycastTarget = false;
        }

        private TextMeshProUGUI CreateValueLabel(Transform parent)
        {
            var label = CreateText(parent, FontStyles.Normal);
            label.fontSize = 16f;
            return label;
        }

        private TextMeshProUGUI CreateMultilineLabel(Transform parent)
        {
            var label = CreateText(parent, FontStyles.Normal);
            label.fontSize = 15f;
            label.enableWordWrapping = true;

            var element = label.gameObject.AddComponent<LayoutElement>();
            element.flexibleHeight = 1f;
            element.minHeight = 48f;

            return label;
        }

        private TextMeshProUGUI CreateText(Transform parent, FontStyles style)
        {
            var textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.sizeDelta = Vector2.zero;

            var label = textObject.GetComponent<TextMeshProUGUI>();
            label.text = string.Empty;
            label.fontSize = 18f;
            label.fontStyle = style;
            label.color = new Color(0.85f, 0.9f, 1f, 0.95f);
            label.alignment = TextAlignmentOptions.Left;
            label.enableWordWrapping = true;
            label.raycastTarget = false;

            return label;
        }

        private static string BuildMultilineText(IReadOnlyList<string> lines, string fallback)
        {
            if (lines == null || lines.Count == 0)
                return fallback;

            return string.Join("\n", lines);
        }
    }
}
