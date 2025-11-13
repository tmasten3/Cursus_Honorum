using System;
using System.Collections;
using System.Collections.Generic;
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
            UpdateLogs();
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
    }
}
