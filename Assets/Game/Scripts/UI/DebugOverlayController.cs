using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.Systems.CharacterSystem;
using Game.Systems.Politics.Elections;
using Game.Systems.Politics.Offices;
using Game.Systems.Time;

namespace Game.UI
{
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    [DisallowMultipleComponent]
    public class DebugOverlayController : MonoBehaviour
    {
        [SerializeField, Min(0.1f)]
        private float refreshInterval = 1f;

        private Canvas overlayCanvas;
        private CanvasScaler scaler;
        private TMP_Text dateText;
        private TMP_Text livingText;
        private TMP_Text familyText;
        private TMP_Text officeHeaderText;
        private TMP_Text officeListText;
        private TMP_Text electionHeaderText;
        private TMP_Text electionListText;
        private TMP_Text logText;
        private ScrollRect logScroll;

        private GameController gameController;
        private GameState gameState;
        private TimeSystem timeSystem;
        private CharacterSystem characterSystem;
        private CharacterRepository characterRepository;
        private OfficeSystem officeSystem;
        private ElectionSystem electionSystem;
        private bool overlayBound;
        private Coroutine bindingRoutine;
        private bool loggedMissingTimeSystem;
        private bool loggedMissingCharacterSystem;

        private float refreshTimer;
        private readonly StringBuilder builder = new();
        private int lastLogCount;
        private DateTime lastLogTimestamp;
        [SerializeField, Min(1)]
        private int maxOfficeEntries = 8;
        [SerializeField, Min(1)]
        private int maxElectionEntries = 6;
        [SerializeField, Min(1)]
        private int maxWinnersPerElection = 3;
        [SerializeField, Min(1)]
        private int electionLookbackYears = 5;
        private int lastOfficeDisplayHash;
        private int lastElectionDisplayHash;
#if UNITY_EDITOR
        private int lastSeasonValidationKey = int.MinValue;
        private int lastSeasonOfficeHash;
        private int lastSeasonElectionHash;
#endif

        private void Awake()
        {
            overlayCanvas = GetComponent<Canvas>();
            if (overlayCanvas == null)
                overlayCanvas = gameObject.AddComponent<Canvas>();

            scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = gameObject.AddComponent<CanvasScaler>();

            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            ConfigureCanvas();
            BuildUI();
        }

        private void OnEnable()
        {
            if (bindingRoutine == null && isActiveAndEnabled)
                bindingRoutine = StartCoroutine(WaitForGameState());
        }

        private void Update()
        {
            if (!overlayBound)
                return;

            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer >= refreshInterval)
            {
                refreshTimer = 0f;
                RefreshOverlay();
            }
        }

        private void ConfigureCanvas()
        {
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = short.MaxValue;

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
        }

        private void OnDisable()
        {
            if (bindingRoutine != null)
            {
                StopCoroutine(bindingRoutine);
                bindingRoutine = null;
            }

            UnsubscribeFromController();

            overlayBound = false;
            gameController = null;
            gameState = null;
            timeSystem = null;
            characterSystem = null;
            characterRepository = null;
            officeSystem = null;
            electionSystem = null;
            loggedMissingTimeSystem = false;
            loggedMissingCharacterSystem = false;
        }

        private void UnsubscribeFromController()
        {
            if (gameController == null)
                return;

            gameController.GameStateShuttingDown -= OnGameStateShuttingDown;
        }

        private void BuildUI()
        {
            var rectTransform = (RectTransform)transform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(transform, false);

            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(16f, -16f);
            panelRect.sizeDelta = new Vector2(420f, 360f);

            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.65f);

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperLeft;

            dateText = CreateLabel(panel.transform, "DateText", "Date: --");
            livingText = CreateLabel(panel.transform, "LivingText", "Living: 0");
            familyText = CreateLabel(panel.transform, "FamilyText", "Families: 0");

            var officeSection = CreateSection(panel.transform, "OfficesHeader", "Offices", "OfficesList");
            officeHeaderText = officeSection.header;
            officeListText = officeSection.body;

            var electionSection = CreateSection(panel.transform, "ElectionsHeader", "Recent Elections", "ElectionsList");
            electionHeaderText = electionSection.header;
            electionListText = electionSection.body;

            logText = CreateLogArea(panel.transform, out logScroll);
        }

        private TMP_Text CreateLabel(Transform parent, string name, string initialText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(0f, 28f);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = initialText;
            text.fontSize = 22f;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;

            return text;
        }

        private (TMP_Text header, TMP_Text body) CreateSection(Transform parent, string headerName, string headerText, string bodyName)
        {
            var header = CreateLabel(parent, headerName, headerText);
            header.fontSize = 20f;
            header.fontStyle = FontStyles.Bold;

            var go = new GameObject(bodyName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(0f, 64f);

            var body = go.GetComponent<TextMeshProUGUI>();
            body.text = "No data.";
            body.fontSize = 18f;
            body.color = new Color(0.85f, 0.9f, 1f);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.enableWordWrapping = true;

            return (header, body);
        }

        private TMP_Text CreateLogArea(Transform parent, out ScrollRect scrollRect)
        {
            var container = new GameObject("LogArea", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(ScrollRect));
            container.transform.SetParent(parent, false);

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

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(container.transform, false);
            var viewportRect = (RectTransform)viewport.transform;
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

            var content = new GameObject("Content", typeof(RectTransform), typeof(TextMeshProUGUI));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0f, 1f);
            contentRect.offsetMin = new Vector2(8f, 0f);
            contentRect.offsetMax = new Vector2(-8f, 0f);

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

        private void OnGameStateShuttingDown()
        {
            if (!overlayBound)
                return;

            overlayBound = false;

            UnsubscribeFromController();
            gameController = null;
            gameState = null;
            timeSystem = null;
            characterSystem = null;
            characterRepository = null;
            officeSystem = null;
            electionSystem = null;

            refreshTimer = 0f;
            lastLogCount = -1;
            lastLogTimestamp = DateTime.MinValue;
            loggedMissingTimeSystem = false;
            loggedMissingCharacterSystem = false;

            if (bindingRoutine == null && isActiveAndEnabled)
                bindingRoutine = StartCoroutine(WaitForGameState());
        }

        private IEnumerator WaitForGameState()
        {
            try
            {
                while (true)
                {
                    GameController controller = FindFirstObjectByType<GameController>();

                    while (controller == null)
                    {
                        yield return null;
                        controller = FindFirstObjectByType<GameController>();
                    }

                    while (controller != null)
                    {
                        if (TryBindSystems(controller))
                            yield break;

                        if (controller == null)
                            break;

                        yield return null;
                    }

                    yield return null;
                }
            }
            finally
            {
                bindingRoutine = null;
            }
        }

        private bool TryBindSystems(GameController controller)
        {
            var state = controller.GameState;
            if (state == null)
                return false;

            var resolvedTimeSystem = state.GetSystem<TimeSystem>();
            if (resolvedTimeSystem == null)
            {
                if (!loggedMissingTimeSystem)
                {
                    loggedMissingTimeSystem = true;
                    Game.Core.Logger.Error("Safety", "[DebugOverlay] TimeSystem unavailable during binding.");
                }

                return false;
            }

            loggedMissingTimeSystem = false;

            var resolvedCharacterSystem = state.GetSystem<CharacterSystem>();
            if (resolvedCharacterSystem == null)
            {
                if (!loggedMissingCharacterSystem)
                {
                    loggedMissingCharacterSystem = true;
                    Game.Core.Logger.Error("Safety", "[DebugOverlay] CharacterSystem unavailable during binding.");
                }

                return false;
            }

            loggedMissingCharacterSystem = false;

            CharacterRepository repository = null;
            if (resolvedCharacterSystem.TryGetRepository(out var resolvedRepository))
                repository = resolvedRepository;
            else
            {
                Game.Core.Logger.Warn("Safety", "[DebugOverlay] Character repository unavailable.");
            }

            var resolvedOfficeSystem = state.GetSystem<OfficeSystem>();
            var resolvedElectionSystem = state.GetSystem<ElectionSystem>();

            gameController = controller;
            gameController.GameStateShuttingDown -= OnGameStateShuttingDown;
            gameController.GameStateShuttingDown += OnGameStateShuttingDown;

            gameState = state;
            timeSystem = resolvedTimeSystem;
            characterSystem = resolvedCharacterSystem;
            characterRepository = repository;
            officeSystem = resolvedOfficeSystem;
            electionSystem = resolvedElectionSystem;

            overlayBound = true;
            refreshTimer = 0f;
            lastLogCount = -1;
            lastLogTimestamp = DateTime.MinValue;

            RefreshOverlay();
            return true;
        }

        private void ForceRefresh()
        {
            if (!overlayBound)
                return;

            refreshTimer = 0f;
            lastLogCount = -1;
            lastLogTimestamp = DateTime.MinValue;
            RefreshOverlay();
        }

        private void RefreshOverlay()
        {
            if (!overlayBound)
                return;

            UpdateDate();
            UpdatePopulation();
            UpdateOfficeAssignments();
            UpdateElectionResults();
            UpdateLogs();
            ValidateSeasonalSections();
        }

        private void UpdateDate()
        {
            string dateString = timeSystem != null ? timeSystem.GetCurrentDateString() : "Date unavailable";
            dateText.text = $"Date: {dateString}";
        }

        private void UpdatePopulation()
        {
            int living = characterSystem?.CountAlive() ?? 0;
            int families = characterRepository?.FamilyCount ?? characterSystem?.GetFamilyCount() ?? 0;

            livingText.text = $"Living: {living}";
            familyText.text = $"Families: {families}";
        }

        private void UpdateLogs()
        {
            IReadOnlyList<Game.Core.Logger.LogEntry> entries = Game.Core.Logger.GetRecentEntries();
            if (entries == null || entries.Count == 0)
            {
                lastLogCount = 0;
                lastLogTimestamp = DateTime.MinValue;
                if (logText != null && logText.text.Length > 0)
                    logText.text = string.Empty;
                return;
            }

            var newestEntry = entries[entries.Count - 1];
            if (entries.Count == lastLogCount && newestEntry.Timestamp == lastLogTimestamp)
                return;

            lastLogCount = entries.Count;
            lastLogTimestamp = newestEntry.Timestamp;

            builder.Clear();
            foreach (var entry in entries)
            {
                builder.Append('[').Append(entry.Timestamp.ToString("HH:mm:ss"));
                builder.Append("] [").Append(entry.Category).Append("] ");
                builder.Append(entry.Message).Append('\n');
            }

            logText.text = builder.ToString();
            Canvas.ForceUpdateCanvases();
            if (logScroll != null)
                logScroll.verticalNormalizedPosition = 0f;
        }

        private void UpdateOfficeAssignments()
        {
            if (officeHeaderText == null || officeListText == null)
                return;

            if (officeSystem == null || characterSystem == null)
            {
                officeHeaderText.text = "Offices: unavailable";
                officeListText.text = string.Empty;
                lastOfficeDisplayHash = 0;
                return;
            }

            var definitions = officeSystem.GetAllDefinitions();
            var living = characterSystem.GetAllLiving();
            if (definitions == null || definitions.Count == 0 || living == null || living.Count == 0)
            {
                officeHeaderText.text = "Offices: none";
                officeListText.text = "No active office holders.";
                lastOfficeDisplayHash = officeListText.text.GetHashCode();
                return;
            }

            var definitionsById = definitions
                .Where(d => d != null && !string.IsNullOrEmpty(d.Id))
                .ToDictionary(d => d.Id, d => d, StringComparer.OrdinalIgnoreCase);

            var rows = new List<OfficeDisplayRow>();
            for (int i = 0; i < living.Count; i++)
            {
                var character = living[i];
                if (character == null)
                    continue;

                var holdings = officeSystem.GetCurrentHoldings(character.ID);
                if (holdings == null || holdings.Count == 0)
                    continue;

                for (int j = 0; j < holdings.Count; j++)
                {
                    var seat = holdings[j];
                    if (seat == null || !seat.HolderId.HasValue)
                        continue;

                    var holderName = !string.IsNullOrEmpty(character.FullName) ? character.FullName : $"#{seat.HolderId.Value}";
                    definitionsById.TryGetValue(seat.OfficeId ?? string.Empty, out var definition);

                    string pendingName = null;
                    if (seat.PendingHolderId.HasValue)
                    {
                        var pending = characterSystem.Get(seat.PendingHolderId.Value);
                        pendingName = !string.IsNullOrEmpty(pending?.FullName)
                            ? pending.FullName
                            : $"#{seat.PendingHolderId.Value}";
                    }

                    rows.Add(new OfficeDisplayRow
                    {
                        OfficeId = seat.OfficeId,
                        OfficeName = definition?.Name ?? seat.OfficeId ?? "Office",
                        Rank = definition?.Rank ?? int.MinValue,
                        SeatIndex = seat.SeatIndex,
                        HolderName = holderName,
                        StartYear = seat.StartYear,
                        EndYear = seat.EndYear,
                        PendingName = pendingName,
                        PendingStartYear = seat.PendingStartYear
                    });
                }
            }

            if (rows.Count == 0)
            {
                officeHeaderText.text = "Offices: none";
                officeListText.text = "No active office holders.";
                lastOfficeDisplayHash = officeListText.text.GetHashCode();
                return;
            }

            rows.Sort((a, b) =>
            {
                int rankCompare = b.Rank.CompareTo(a.Rank);
                if (rankCompare != 0)
                    return rankCompare;

                int nameCompare = string.Compare(a.OfficeName, b.OfficeName, StringComparison.OrdinalIgnoreCase);
                if (nameCompare != 0)
                    return nameCompare;

                int seatCompare = a.SeatIndex.CompareTo(b.SeatIndex);
                if (seatCompare != 0)
                    return seatCompare;

                return string.Compare(a.HolderName, b.HolderName, StringComparison.OrdinalIgnoreCase);
            });

            int total = rows.Count;
            int displayCount = Mathf.Clamp(maxOfficeEntries, 1, total);

            builder.Clear();
            for (int i = 0; i < displayCount; i++)
            {
                var row = rows[i];
                builder.Append(row.OfficeName);
                if (row.SeatIndex >= 0)
                {
                    builder.Append(" #").Append(row.SeatIndex + 1);
                }

                builder.Append(':').Append(' ').Append(row.HolderName);

                if (row.StartYear != 0 || row.EndYear != 0)
                {
                    builder.Append(" (");
                    if (row.StartYear == row.EndYear)
                        builder.Append(row.StartYear);
                    else
                        builder.Append(row.StartYear).Append('-').Append(row.EndYear);
                    builder.Append(')');
                }

                if (!string.IsNullOrEmpty(row.PendingName))
                {
                    builder.Append(" → ").Append(row.PendingName);
                    if (row.PendingStartYear > 0)
                        builder.Append(" (from ").Append(row.PendingStartYear).Append(')');
                }

                if (i < displayCount - 1)
                    builder.Append('\n');
            }

            if (total > displayCount)
            {
                builder.Append('\n').Append('+').Append(total - displayCount).Append(" more…");
            }

            string text = builder.ToString();
            officeListText.text = text;
            officeHeaderText.text = total > displayCount
                ? $"Offices ({displayCount}/{total})"
                : $"Offices ({total})";
            lastOfficeDisplayHash = text.GetHashCode();
            builder.Clear();
        }

        private void UpdateElectionResults()
        {
            if (electionHeaderText == null || electionListText == null)
                return;

            if (electionSystem == null || timeSystem == null)
            {
                electionHeaderText.text = "Recent Elections: unavailable";
                electionListText.text = string.Empty;
                lastElectionDisplayHash = 0;
                return;
            }

            var currentDate = timeSystem.GetCurrentDate();
            int year = currentDate.year;
            if (year == 0)
            {
                electionHeaderText.text = "Recent Elections";
                electionListText.text = "No election timeline available.";
                lastElectionDisplayHash = electionListText.text.GetHashCode();
                return;
            }

            var entries = GatherRecentElectionRows(year);
            if (entries.Count == 0)
            {
                electionHeaderText.text = "Recent Elections";
                electionListText.text = "No election results recorded.";
                lastElectionDisplayHash = electionListText.text.GetHashCode();
                return;
            }

            int total = entries.Count;
            int displayCount = Mathf.Clamp(maxElectionEntries, 1, total);

            builder.Clear();
            for (int i = 0; i < displayCount; i++)
            {
                var entry = entries[i];
                builder.Append(entry.Year).Append(':').Append(' ').Append(entry.OfficeName).Append(" — ");

                if (entry.Winners == null || entry.Winners.Count == 0)
                {
                    builder.Append("No winners recorded");
                }
                else
                {
                    int winnerCount = Mathf.Clamp(maxWinnersPerElection, 1, entry.Winners.Count);
                    for (int j = 0; j < winnerCount; j++)
                    {
                        var winner = entry.Winners[j];
                        string name = !string.IsNullOrEmpty(winner.CharacterName)
                            ? winner.CharacterName
                            : $"#{winner.CharacterId}";
                        int seatIndex = winner.SeatIndex >= 0 ? winner.SeatIndex + 1 : winner.SeatIndex;
                        builder.Append(name);
                        if (seatIndex > 0)
                        {
                            builder.Append(" (seat ").Append(seatIndex).Append(')');
                        }

                        if (!string.IsNullOrEmpty(winner.Notes))
                        {
                            builder.Append(" [").Append(winner.Notes).Append(']');
                        }

                        if (j < winnerCount - 1)
                            builder.Append(", ");
                    }

                    if (entry.Winners.Count > winnerCount)
                    {
                        builder.Append(", +").Append(entry.Winners.Count - winnerCount).Append(" more");
                    }
                }

                if (i < displayCount - 1)
                    builder.Append('\n');
            }

            if (total > displayCount)
            {
                builder.Append('\n').Append('+').Append(total - displayCount).Append(" more…");
            }

            string text = builder.ToString();
            electionListText.text = text;
            electionHeaderText.text = total > displayCount
                ? $"Recent Elections ({displayCount}/{total})"
                : $"Recent Elections ({total})";
            lastElectionDisplayHash = text.GetHashCode();
            builder.Clear();
        }

        private List<ElectionDisplayRow> GatherRecentElectionRows(int startYear)
        {
            var rows = new List<ElectionDisplayRow>();
            if (electionSystem == null)
                return rows;

            int yearsToCheck = Mathf.Max(1, electionLookbackYears);
            for (int year = startYear; year >= startYear - yearsToCheck; year--)
            {
                var records = electionSystem.GetResultsForYear(year);
                if (records == null || records.Count == 0)
                    continue;

                for (int i = records.Count - 1; i >= 0; i--)
                {
                    var record = records[i];
                    if (record == null)
                        continue;

                    rows.Add(new ElectionDisplayRow
                    {
                        Year = record.Year,
                        OfficeName = record.Office?.Name ?? record.Office?.Id ?? "Office",
                        Rank = record.Office?.Rank ?? int.MinValue,
                        Winners = record.Winners ?? new List<ElectionWinnerSummary>()
                    });
                }
            }

            rows.Sort((a, b) =>
            {
                int yearCompare = b.Year.CompareTo(a.Year);
                if (yearCompare != 0)
                    return yearCompare;

                int rankCompare = b.Rank.CompareTo(a.Rank);
                if (rankCompare != 0)
                    return rankCompare;

                return string.Compare(a.OfficeName, b.OfficeName, StringComparison.OrdinalIgnoreCase);
            });

            return rows;
        }

        private void ValidateSeasonalSections()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying || timeSystem == null)
                return;

            var current = timeSystem.GetCurrentDate();
            int seasonKey = (current.year * 100) + current.month;
            if (seasonKey == lastSeasonValidationKey)
                return;

            if (officeSystem != null && officeListText != null)
            {
                if (lastSeasonValidationKey != int.MinValue && current.month == 1 && lastSeasonOfficeHash == lastOfficeDisplayHash)
                {
                    Game.Core.Logger.Warn("UI", "[DebugOverlay] Office section did not update at the new year. Verify bindings.");
                }

                lastSeasonOfficeHash = lastOfficeDisplayHash;
            }

            if (electionSystem != null && electionListText != null)
            {
                bool inElectionSeason = current.month >= 6 && current.month <= 7;
                if (inElectionSeason && string.IsNullOrEmpty(electionListText.text))
                {
                    Game.Core.Logger.Warn("UI", "[DebugOverlay] Election section empty during election season.");
                }

                if (lastSeasonValidationKey != int.MinValue && current.month == 7 && lastSeasonElectionHash == lastElectionDisplayHash)
                {
                    Game.Core.Logger.Warn("UI", "[DebugOverlay] Election results section did not refresh after elections.");
                }

                lastSeasonElectionHash = lastElectionDisplayHash;
            }

            lastSeasonValidationKey = seasonKey;
#endif
        }

        private struct OfficeDisplayRow
        {
            public string OfficeId;
            public string OfficeName;
            public int Rank;
            public int SeatIndex;
            public string HolderName;
            public int StartYear;
            public int EndYear;
            public string PendingName;
            public int PendingStartYear;
        }

        private struct ElectionDisplayRow
        {
            public int Year;
            public string OfficeName;
            public int Rank;
            public List<ElectionWinnerSummary> Winners;
        }
    }
}
