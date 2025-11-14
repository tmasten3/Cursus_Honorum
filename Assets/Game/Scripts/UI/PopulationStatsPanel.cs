using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Systems.EventBus;
using Game.Systems.CharacterSystem;

namespace Game.UI
{
    internal sealed class PopulationStatsPanel : IDisposable
    {
        private readonly GameObject root;
        private readonly TMP_Text summaryText;
        private readonly TMP_Text dailySummaryText;

        private EventBus eventBus;
        private CharacterSystem characterSystem;
        private CharacterRepository repository;
        private bool subscribed;

        private int dailyBirths;
        private int dailyDeaths;
        private int dailyMarriages;
        private int lastYear = -1;
        private int lastMonth = -1;
        private int lastDay = -1;

        private readonly StringBuilder builder = new();

        public PopulationStatsPanel(Transform parent)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            root = new GameObject("PopulationStatsPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            root.transform.SetParent(parent, false);

            var image = root.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.45f);

            var layout = root.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperLeft;

            var layoutElement = root.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 140f;
            layoutElement.flexibleHeight = 0f;

            CreateHeader();

            summaryText = CreateContentText("PopulationSummary", 18f);
            dailySummaryText = CreateContentText("PopulationDaily", 17f);
        }

        public void Bind(EventBus bus, CharacterSystem characterSystem, CharacterRepository repository)
        {
            eventBus = bus;
            this.characterSystem = characterSystem;
            this.repository = repository;

            ResetDailyState();
            Subscribe();
            RefreshTotals();
            UpdateDailyText();
        }

        public void Dispose()
        {
            Unsubscribe();
            ResetDailyState();
            eventBus = null;
            characterSystem = null;
            repository = null;
            ResetDailyCounters();
            ResetDisplayedText();
        }

        public void SetVisible(bool visible)
        {
            if (root != null)
                root.SetActive(visible);
        }

        public void RefreshImmediate()
        {
            RefreshTotals();
            UpdateDailyText();
        }

        private void ResetDailyCounters()
        {
            dailyBirths = 0;
            dailyDeaths = 0;
            dailyMarriages = 0;
            lastYear = -1;
            lastMonth = -1;
            lastDay = -1;
        }

        private void ResetDisplayedText()
        {
            if (summaryText != null)
                summaryText.text = "Living Citizens: 0\nFamilies: 0";

            if (dailySummaryText != null)
                dailySummaryText.text = "Daily Summary (pending)\nBirths: 0  |  Deaths: 0  |  Marriages: 0";
        }

        private void CreateHeader()
        {
            var header = CreateContentText("PopulationHeader", 20f, FontStyles.Bold);
            header.text = "Population";
        }

        private TMP_Text CreateContentText(string name, float fontSize, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(root.transform, false);

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(0f, 24f);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = new Color(0.9f, 0.95f, 1f, 0.95f);
            text.fontStyle = style;
            text.enableWordWrapping = true;
            text.alignment = TextAlignmentOptions.TopLeft;

            return text;
        }

        private void Subscribe()
        {
            if (eventBus == null || subscribed)
                return;

            eventBus.Subscribe<OnNewDayEvent>(OnNewDay);
            eventBus.Subscribe<OnPopulationTick>(OnPopulationTick);
            eventBus.Subscribe<OnCharacterBorn>(OnCharacterBorn);
            eventBus.Subscribe<OnCharacterDied>(OnCharacterDied);
            eventBus.Subscribe<OnCharacterMarried>(OnCharacterMarried);

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (eventBus == null || !subscribed)
                return;

            eventBus.Unsubscribe<OnNewDayEvent>(OnNewDay);
            eventBus.Unsubscribe<OnPopulationTick>(OnPopulationTick);
            eventBus.Unsubscribe<OnCharacterBorn>(OnCharacterBorn);
            eventBus.Unsubscribe<OnCharacterDied>(OnCharacterDied);
            eventBus.Unsubscribe<OnCharacterMarried>(OnCharacterMarried);

            subscribed = false;
        }

        private void ResetDailyState()
        {
            dailyBirths = 0;
            dailyDeaths = 0;
            dailyMarriages = 0;
            lastYear = -1;
            lastMonth = -1;
            lastDay = -1;
        }

        private void OnNewDay(OnNewDayEvent e)
        {
            lastYear = e.Year;
            lastMonth = e.Month;
            lastDay = e.Day;

            dailyBirths = 0;
            dailyDeaths = 0;
            dailyMarriages = 0;

            RefreshTotals();
            UpdateDailyText();
        }

        private void OnPopulationTick(OnPopulationTick e)
        {
            lastYear = e.Year;
            lastMonth = e.Month;
            lastDay = e.Day;

            dailyBirths = e.Births;
            dailyDeaths = e.Deaths;
            dailyMarriages = e.Marriages;

            RefreshTotals();
            UpdateDailyText();
        }

        private void OnCharacterBorn(OnCharacterBorn e)
        {
            dailyBirths++;
            RefreshTotals();
            UpdateDailyText();
        }

        private void OnCharacterDied(OnCharacterDied e)
        {
            dailyDeaths++;
            RefreshTotals();
            UpdateDailyText();
        }

        private void OnCharacterMarried(OnCharacterMarried e)
        {
            dailyMarriages++;
            RefreshTotals();
            UpdateDailyText();
        }

        private void RefreshTotals()
        {
            if (summaryText == null)
                return;

            int living = characterSystem?.CountAlive() ?? repository?.AliveCount ?? 0;
            int families = repository?.FamilyCount ?? characterSystem?.GetFamilyCount() ?? 0;

            builder.Clear();
            builder.Append("Living Citizens: ").Append(living).Append('\n');
            builder.Append("Families: ").Append(families);
            summaryText.text = builder.ToString();
        }

        private void UpdateDailyText()
        {
            if (dailySummaryText == null)
                return;

            builder.Clear();
            if (lastYear > 0)
            {
                builder.Append("Daily Summary (")
                    .Append(lastYear.ToString("D4"))
                    .Append('-')
                    .Append(lastMonth.ToString("D2"))
                    .Append('-')
                    .Append(lastDay.ToString("D2"))
                    .Append(")\n");
            }
            else
            {
                builder.Append("Daily Summary (pending)\n");
            }

            builder.Append("Births: ").Append(dailyBirths)
                .Append("  |  Deaths: ").Append(dailyDeaths)
                .Append("  |  Marriages: ").Append(dailyMarriages);

            dailySummaryText.text = builder.ToString();
        }
    }
}
