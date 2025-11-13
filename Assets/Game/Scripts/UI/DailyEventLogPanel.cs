using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Systems.EventBus;
using Game.Systems.CharacterSystem;
using Game.Systems.Politics.Elections;

namespace Game.UI
{
    internal sealed class DailyEventLogPanel : IDisposable
    {
        private const int MaxEntries = 50;

        private readonly GameObject root;
        private readonly TMP_Text logText;

        private readonly List<string> entries = new();
        private readonly StringBuilder builder = new();

        private EventBus eventBus;
        private CharacterSystem characterSystem;
        private bool subscribed;

        public DailyEventLogPanel(Transform parent)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            root = new GameObject("DailyEventLogPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            root.transform.SetParent(parent, false);

            var image = root.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.4f);

            var layout = root.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperLeft;

            var layoutElement = root.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 220f;
            layoutElement.flexibleHeight = 1f;

            CreateHeader();
            logText = CreateContentText("DailyEventLogText", 17f);
            logText.text = "Daily events will appear here.";
        }

        public void Bind(EventBus bus, CharacterSystem characterSystem)
        {
            eventBus = bus;
            this.characterSystem = characterSystem;

            Subscribe();
            RefreshImmediate();
        }

        public void Dispose()
        {
            Unsubscribe();
            eventBus = null;
            characterSystem = null;
        }

        public void SetVisible(bool visible)
        {
            if (root != null)
                root.SetActive(visible);
        }

        public void RefreshImmediate()
        {
            RebuildText();
        }

        private void CreateHeader()
        {
            var header = CreateContentText("DailyEventHeader", 20f, FontStyles.Bold);
            header.text = "Daily Events";
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
            eventBus.Subscribe<OnCharacterBorn>(OnCharacterBorn);
            eventBus.Subscribe<OnCharacterDied>(OnCharacterDied);
            eventBus.Subscribe<OnCharacterMarried>(OnCharacterMarried);
            eventBus.Subscribe<ElectionSeasonOpenedEvent>(OnElectionOpened);
            eventBus.Subscribe<ElectionSeasonCompletedEvent>(OnElectionCompleted);
            eventBus.Subscribe<OfficeAssignedEvent>(OnOfficeAssigned);

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (eventBus == null || !subscribed)
                return;

            eventBus.Unsubscribe<OnNewDayEvent>(OnNewDay);
            eventBus.Unsubscribe<OnCharacterBorn>(OnCharacterBorn);
            eventBus.Unsubscribe<OnCharacterDied>(OnCharacterDied);
            eventBus.Unsubscribe<OnCharacterMarried>(OnCharacterMarried);
            eventBus.Unsubscribe<ElectionSeasonOpenedEvent>(OnElectionOpened);
            eventBus.Unsubscribe<ElectionSeasonCompletedEvent>(OnElectionCompleted);
            eventBus.Unsubscribe<OfficeAssignedEvent>(OnOfficeAssigned);

            subscribed = false;
        }

        private void OnNewDay(OnNewDayEvent e)
        {
            AddEntry($"New day begins: {FormatDate(e)}", e, true);
        }

        private void OnCharacterBorn(OnCharacterBorn e)
        {
            string child = ResolveName(e.ChildID);
            string mother = ResolveName(e.MotherID);
            string father = e.FatherID.HasValue ? ResolveName(e.FatherID.Value) : "Unknown";
            AddEntry($"Birth: {child} to {mother} and {father}", e);
        }

        private void OnCharacterDied(OnCharacterDied e)
        {
            string name = ResolveName(e.CharacterID);
            AddEntry($"Death: {name} ({e.Cause})", e);
        }

        private void OnCharacterMarried(OnCharacterMarried e)
        {
            string spouseA = ResolveName(e.SpouseA);
            string spouseB = ResolveName(e.SpouseB);
            AddEntry($"Marriage: {spouseA} weds {spouseB}", e);
        }

        private void OnElectionOpened(ElectionSeasonOpenedEvent e)
        {
            if (e?.Offices == null || e.Offices.Count == 0)
            {
                AddEntry($"Election season opened for {e?.ElectionYear ?? 0}, but no offices require votes.", e);
                return;
            }

            var officeBuilder = new StringBuilder();
            foreach (var office in e.Offices)
            {
                officeBuilder.Append(office.OfficeName)
                    .Append(" (")
                    .Append(office.SeatsAvailable)
                    .Append(office.SeatsAvailable == 1 ? " seat" : " seats")
                    .Append(")");
                officeBuilder.Append(", ");
            }

            if (officeBuilder.Length >= 2)
                officeBuilder.Length -= 2;

            AddEntry($"Election season opens: {officeBuilder.ToString()}", e);
        }

        private void OnElectionCompleted(ElectionSeasonCompletedEvent e)
        {
            if (e?.Results == null || e.Results.Count == 0)
            {
                AddEntry($"Election season {e?.ElectionYear ?? 0} concluded with no recorded winners.", e);
                return;
            }

            foreach (var result in e.Results)
            {
                if (result?.Winners == null || result.Winners.Count == 0)
                {
                    AddEntry($"Election: {result?.OfficeName ?? "Unknown office"} had no winners.", e);
                    continue;
                }

                var winners = new StringBuilder();
                foreach (var winner in result.Winners)
                {
                    winners.Append(string.IsNullOrWhiteSpace(winner.CharacterName) ? ResolveName(winner.CharacterId) : winner.CharacterName);
                    winners.Append(" (seat ").Append(winner.SeatIndex + 1).Append(")");
                    winners.Append(", ");
                }

                if (winners.Length >= 2)
                    winners.Length -= 2;

                AddEntry($"Election: {result.OfficeName} winners — {winners.ToString()}", e);
            }
        }

        private void OnOfficeAssigned(OfficeAssignedEvent e)
        {
            string name = !string.IsNullOrWhiteSpace(e.CharacterName) ? e.CharacterName : ResolveName(e.CharacterId);
            AddEntry($"Appointment: {name} takes {e.OfficeName} seat {e.SeatIndex + 1} ({e.TermStartYear}-{e.TermEndYear})", e);
        }

        private void AddEntry(string message, GameEvent context, bool isHeader = false)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            string prefix = context != null ? FormatDate(context) : "----";
            string formatted = isHeader ? $"=== {message} ===" : $"[{prefix}] {message}";

            entries.Add(formatted);
            if (entries.Count > MaxEntries)
                entries.RemoveAt(0);

            RebuildText();
        }

        private void RebuildText()
        {
            if (logText == null)
                return;

            builder.Clear();
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                builder.AppendLine(entries[i]);
            }

            logText.text = builder.Length > 0 ? builder.ToString() : "No events recorded yet.";
        }

        private string ResolveName(int id)
        {
            var character = characterSystem?.Get(id);
            if (character != null && !string.IsNullOrWhiteSpace(character.FullName))
                return character.FullName;

            return $"Character #{id}";
        }

        private static string FormatDate(GameEvent e)
        {
            return e == null ? "----" : $"{e.Year:D4}-{e.Month:D2}-{e.Day:D2}";
        }
    }
}
