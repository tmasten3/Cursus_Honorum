using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Systems.EventBus;
using Game.Systems.Politics.Elections;
using Game.Systems.Politics.Offices;
using Game.Systems.CharacterSystem;

namespace Game.UI
{
    internal sealed class PoliticalSummaryPanel : IDisposable
    {
        private readonly struct OfficeSnapshot
        {
            public readonly string Id;
            public readonly string Name;
            public readonly OfficeAssembly Assembly;
            public readonly int Seats;

            public OfficeSnapshot(string id, string name, OfficeAssembly assembly, int seats)
            {
                Id = id;
                Name = name;
                Assembly = assembly;
                Seats = seats;
            }
        }

        private readonly struct ResultSnapshot
        {
            public readonly string OfficeName;
            public readonly List<string> Winners;

            public ResultSnapshot(string officeName, List<string> winners)
            {
                OfficeName = officeName;
                Winners = winners;
            }
        }

        private const int MaxRecentAppointments = 6;

        private readonly GameObject root;
        private readonly TMP_Text summaryText;

        private readonly List<OfficeSnapshot> openOffices = new();
        private readonly List<ResultSnapshot> latestResults = new();
        private readonly Queue<string> recentAppointments = new();
        private readonly StringBuilder builder = new();

        private EventBus eventBus;
        private ElectionSystem electionSystem;
        private OfficeSystem officeSystem;
        private CharacterSystem characterSystem;
        private bool subscribed;
        private int currentYear = -1;
        private int lastElectionYear = -1;

        public PoliticalSummaryPanel(Transform parent)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            root = new GameObject("PoliticalSummaryPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            root.transform.SetParent(parent, false);

            var image = root.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.42f);

            var layout = root.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperLeft;

            var layoutElement = root.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 200f;
            layoutElement.flexibleHeight = 1f;

            var header = CreateContentText("PoliticalHeader", 20f, FontStyles.Bold);
            header.text = "Political Overview";

            summaryText = CreateContentText("PoliticalSummary", 17f);
            summaryText.text = "Election data unavailable.";
        }

        public void Bind(EventBus bus, ElectionSystem electionSystem, OfficeSystem officeSystem, CharacterSystem characterSystem)
        {
            eventBus = bus;
            this.electionSystem = electionSystem;
            this.officeSystem = officeSystem;
            this.characterSystem = characterSystem;

            Subscribe();
            RefreshImmediate();
        }

        public void Dispose()
        {
            Unsubscribe();
            eventBus = null;
            electionSystem = null;
            officeSystem = null;
            characterSystem = null;
        }

        public void SetVisible(bool visible)
        {
            if (root != null)
                root.SetActive(visible);
        }

        public void RefreshImmediate()
        {
            UpdateSummaryText();
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

            eventBus.Subscribe<OnNewYearEvent>(OnNewYear);
            eventBus.Subscribe<ElectionSeasonOpenedEvent>(OnElectionSeasonOpened);
            eventBus.Subscribe<ElectionSeasonCompletedEvent>(OnElectionSeasonCompleted);
            eventBus.Subscribe<OfficeAssignedEvent>(OnOfficeAssigned);

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (eventBus == null || !subscribed)
                return;

            eventBus.Unsubscribe<OnNewYearEvent>(OnNewYear);
            eventBus.Unsubscribe<ElectionSeasonOpenedEvent>(OnElectionSeasonOpened);
            eventBus.Unsubscribe<ElectionSeasonCompletedEvent>(OnElectionSeasonCompleted);
            eventBus.Unsubscribe<OfficeAssignedEvent>(OnOfficeAssigned);

            subscribed = false;
        }

        private void OnNewYear(OnNewYearEvent e)
        {
            currentYear = e?.Year ?? currentYear;
            UpdateSummaryText();
        }

        private void OnElectionSeasonOpened(ElectionSeasonOpenedEvent e)
        {
            lastElectionYear = e?.ElectionYear ?? lastElectionYear;
            openOffices.Clear();

            if (e?.Offices != null)
            {
                foreach (var office in e.Offices)
                {
                    openOffices.Add(new OfficeSnapshot(
                        office.OfficeId,
                        office.OfficeName,
                        office.Assembly,
                        office.SeatsAvailable));
                }
            }

            UpdateSummaryText();
        }

        private void OnElectionSeasonCompleted(ElectionSeasonCompletedEvent e)
        {
            lastElectionYear = e?.ElectionYear ?? lastElectionYear;
            latestResults.Clear();

            if (e?.Results != null)
            {
                foreach (var result in e.Results)
                {
                    if (result == null)
                        continue;

                    var winners = new List<string>();
                    if (result.Winners != null)
                    {
                        foreach (var winner in result.Winners)
                        {
                            if (winner == null)
                                continue;

                            string name = !string.IsNullOrWhiteSpace(winner.CharacterName)
                                ? winner.CharacterName
                                : ResolveCharacterName(winner.CharacterId);
                            winners.Add($"{name} (seat {winner.SeatIndex + 1})");
                        }
                    }

                    latestResults.Add(new ResultSnapshot(result.OfficeName, winners));
                }
            }

            UpdateSummaryText();
        }

        private void OnOfficeAssigned(OfficeAssignedEvent e)
        {
            if (e == null)
                return;

            string name = !string.IsNullOrWhiteSpace(e.CharacterName)
                ? e.CharacterName
                : ResolveCharacterName(e.CharacterId);
            string summary = $"{name} → {e.OfficeName} seat {e.SeatIndex + 1} ({e.TermStartYear}-{e.TermEndYear})";

            recentAppointments.Enqueue(summary);
            while (recentAppointments.Count > MaxRecentAppointments)
                recentAppointments.Dequeue();

            UpdateSummaryText();
        }

        private string ResolveCharacterName(int id)
        {
            var character = characterSystem?.Get(id);
            if (character != null && !string.IsNullOrWhiteSpace(character.FullName))
                return character.FullName;

            return $"Character #{id}";
        }

        private void UpdateSummaryText()
        {
            if (summaryText == null)
                return;

            builder.Clear();
            builder.AppendLine(currentYear > 0 ? $"Current Year: {currentYear}" : "Current Year: --");
            if (officeSystem != null)
                builder.AppendLine($"Magistracies Tracked: {officeSystem.TotalOfficesCount}");

            if (lastElectionYear > 0)
            {
                builder.AppendLine($"Tracking Election Year: {lastElectionYear}");
                if (electionSystem != null)
                {
                    var declarations = electionSystem.GetDeclarationsForYear(lastElectionYear);
                    if (declarations != null)
                        builder.AppendLine($"Declared Candidates: {declarations.Count}");
                }
            }
            else
                builder.AppendLine("Tracking Election Year: --");

            if (openOffices.Count > 0)
            {
                builder.AppendLine("Open Offices:");
                foreach (var office in openOffices)
                {
                    builder.Append(" • ")
                        .Append(office.Name)
                        .Append(" (")
                        .Append(office.Assembly)
                        .Append(") — Seats: ")
                        .Append(office.Seats)
                        .AppendLine();
                }
            }
            else
            {
                builder.AppendLine("Open Offices: none announced");
            }

            if (latestResults.Count > 0)
            {
                builder.AppendLine("Latest Results:");
                foreach (var result in latestResults)
                {
                    builder.Append(" • ").Append(result.OfficeName);
                    if (result.Winners.Count == 0)
                    {
                        builder.AppendLine(" — no winners recorded");
                    }
                    else
                    {
                        builder.Append(" — ");
                        builder.Append(string.Join(", ", result.Winners));
                        builder.AppendLine();
                    }
                }
            }
            else
            {
                builder.AppendLine("Latest Results: pending");
            }

            if (recentAppointments.Count > 0)
            {
                builder.AppendLine("Recent Appointments:");
                foreach (var appointment in recentAppointments)
                    builder.Append(" • ").AppendLine(appointment);
            }
            else
            {
                builder.Append("Recent Appointments: none");
            }

            summaryText.text = builder.ToString();
        }
    }
}
