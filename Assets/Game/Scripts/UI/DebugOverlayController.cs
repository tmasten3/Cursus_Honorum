using System;
using UnityEngine;
using Game.Core;
using Game.Systems.CharacterSystem;
using Game.Systems.EventBus;
using Game.Systems.Politics.Elections;
using Game.Systems.Politics.Offices;
using Game.Systems.Time;
using EventBusSystem = Game.Systems.EventBus.EventBus;

namespace Game.UI
{
    [RequireComponent(typeof(DebugOverlayView))]
    public class DebugOverlayController : MonoBehaviour
    {
        [SerializeField, Min(0.1f)]
        private float refreshInterval = 1f;

        [Header("Section Visibility")]
        [SerializeField]
        private bool showSystemLogPanel = true;

        [SerializeField]
        private bool showPopulationPanel = true;

        [SerializeField]
        private bool showDailyEventPanel = true;

        [SerializeField]
        private bool showPoliticalPanel = true;

        [Header("Display Limits")]
        [SerializeField, Min(1)]
        private int maxOfficeEntries = 8;

        [SerializeField, Min(1)]
        private int maxElectionEntries = 6;

        [SerializeField, Min(1)]
        private int maxWinnersPerElection = 3;

        [SerializeField, Min(1)]
        private int electionLookbackYears = 5;

        [SerializeField]
        private GameController controllerOverride;

        private DebugOverlayView view;
        private PopulationStatsPanel populationPanel;
        private DailyEventLogPanel dailyEventPanel;
        private PoliticalSummaryPanel politicalPanel;

        private GameController gameController;
        private GameState gameState;
        private EventBusSystem eventBus;
        private TimeSystem timeSystem;
        private CharacterSystem characterSystem;
        private CharacterRepository characterRepository;
        private OfficeSystem officeSystem;
        private ElectionSystem electionSystem;
        private DebugOverlayDataProvider dataProvider;
        private DebugOverlayDataProvider.DebugOverlayConfiguration providerConfiguration;

        private bool overlayBound;
        private float refreshTimer;
        private bool previousShowSystemLogPanel;
        private bool previousShowPopulationPanel;
        private bool previousShowDailyEventPanel;
        private bool previousShowPoliticalPanel;
        private int lastLogCount = -1;
        private DateTime lastLogTimestamp = DateTime.MinValue;
        private int lastOfficeDisplayHash;
        private int lastElectionDisplayHash;

#if UNITY_EDITOR
        private int lastSeasonValidationKey = int.MinValue;
        private int lastSeasonOfficeHash;
        private int lastSeasonElectionHash;
#endif

        private static GameController cachedController;
        private static bool loggedMissingController;
        private bool controllerSubscribed;

        private void Awake()
        {
            view = GetComponent<DebugOverlayView>();
            if (view == null)
                view = gameObject.AddComponent<DebugOverlayView>();

            view.EnsureInitialized();
            InitializeSections();
            view.EnsureOfficeAndElectionSections();

            previousShowSystemLogPanel = showSystemLogPanel;
            previousShowPopulationPanel = showPopulationPanel;
            previousShowDailyEventPanel = showDailyEventPanel;
            previousShowPoliticalPanel = showPoliticalPanel;

            UpdateConfiguration();
            ApplySectionVisibility(forceRefresh: true);
        }

        private void OnValidate()
        {
            refreshInterval = Mathf.Max(0.1f, refreshInterval);
            UpdateConfiguration();
        }

        private void OnEnable()
        {
            EnsureControllerReady();
        }

        private void OnDisable()
        {
            UnsubscribeFromController();
            UnbindFromGameState();
        }

        private void Update()
        {
            EnsureControllerReady();

            if (CheckVisibilityChanges())
                ApplySectionVisibility();

            if (!overlayBound || dataProvider == null)
                return;

            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer >= refreshInterval)
            {
                refreshTimer = 0f;
                RefreshOverlay();
            }
        }

        private void InitializeSections()
        {
            if (populationPanel != null)
                return;

            Transform parent = view.SectionParent;
            populationPanel = new PopulationStatsPanel(parent);
            dailyEventPanel = new DailyEventLogPanel(parent);
            politicalPanel = new PoliticalSummaryPanel(parent);
        }

        private void UpdateConfiguration()
        {
            int officeEntries = Mathf.Max(1, maxOfficeEntries);
            int electionEntries = Mathf.Max(1, maxElectionEntries);
            int winnersPerElection = Mathf.Max(1, maxWinnersPerElection);
            int lookback = Mathf.Max(1, electionLookbackYears);

            providerConfiguration = new DebugOverlayDataProvider.DebugOverlayConfiguration(
                officeEntries,
                electionEntries,
                winnersPerElection,
                lookback);
        }

        private void EnsureControllerReady()
        {
            if (gameController != null)
            {
                SubscribeToController();

                if (ShouldBindToGameState(gameController))
                {
                    BindToGameState(gameController.GameState);
                }

                return;
            }

            LocateGameController();
            SubscribeToController();

            if (ShouldBindToGameState(gameController))
            {
                BindToGameState(gameController.GameState);
            }
        }

        private bool ShouldBindToGameState(GameController controller)
        {
            return controller != null
                && controller.IsInitialized
                && controller.GameState != null
                && (!overlayBound || !ReferenceEquals(gameState, controller.GameState));
        }

        private void LocateGameController()
        {
            if (controllerOverride != null)
            {
                cachedController = controllerOverride;
            }
            else if (cachedController == null)
            {
                cachedController = FindFirstObjectByType<GameController>();

                if (cachedController == null)
                {
                    if (!loggedMissingController)
                    {
                        Logger.Warn("UI", "[DebugOverlay] GameController not found during initialization.");
                        loggedMissingController = true;
                    }

                    return;
                }

                loggedMissingController = false;
            }

            gameController = cachedController;
        }

        private void SubscribeToController()
        {
            if (gameController == null || controllerSubscribed)
                return;

            gameController.GameStateInitialized -= OnGameStateInitialized;
            gameController.GameStateInitialized += OnGameStateInitialized;
            gameController.GameStateShuttingDown -= OnGameStateShuttingDown;
            gameController.GameStateShuttingDown += OnGameStateShuttingDown;
            controllerSubscribed = true;
        }

        private void UnsubscribeFromController()
        {
            if (gameController == null || !controllerSubscribed)
                return;

            gameController.GameStateInitialized -= OnGameStateInitialized;
            gameController.GameStateShuttingDown -= OnGameStateShuttingDown;
            controllerSubscribed = false;
        }

        private void OnGameStateInitialized(GameState state)
        {
            BindToGameState(state);
        }

        private void OnGameStateShuttingDown()
        {
            UnbindFromGameState();
        }

        private void BindToGameState(GameState state)
        {
            if (state == null)
                return;

            if (overlayBound && ReferenceEquals(gameState, state))
                return;

            var resolvedTimeSystem = state.GetSystem<TimeSystem>();
            if (resolvedTimeSystem == null)
            {
                Logger.Error("Safety", "[DebugOverlay] TimeSystem unavailable during binding.");
                return;
            }

            var resolvedCharacterSystem = state.GetSystem<CharacterSystem>();
            if (resolvedCharacterSystem == null)
            {
                Logger.Error("Safety", "[DebugOverlay] CharacterSystem unavailable during binding.");
                return;
            }

            CharacterRepository resolvedRepository = null;
            if (resolvedCharacterSystem.TryGetRepository(out var repository))
            {
                resolvedRepository = repository;
            }
            else
            {
                Logger.Warn("Safety", "[DebugOverlay] Character repository unavailable.");
            }

            var resolvedOfficeSystem = state.GetSystem<OfficeSystem>();
            var resolvedElectionSystem = state.GetSystem<ElectionSystem>();
            var resolvedEventBus = state.GetSystem<EventBusSystem>();
            if (resolvedEventBus == null)
            {
                Logger.Error("Safety", "[DebugOverlay] EventBus unavailable during binding.");
                return;
            }

            gameState = state;
            timeSystem = resolvedTimeSystem;
            characterSystem = resolvedCharacterSystem;
            characterRepository = resolvedRepository;
            officeSystem = resolvedOfficeSystem;
            electionSystem = resolvedElectionSystem;
            eventBus = resolvedEventBus;
            dataProvider = new DebugOverlayDataProvider(timeSystem, characterSystem, characterRepository, officeSystem, electionSystem);

            overlayBound = true;
            refreshTimer = 0f;
            lastLogCount = -1;
            lastLogTimestamp = DateTime.MinValue;
            lastOfficeDisplayHash = 0;
            lastElectionDisplayHash = 0;

            BindSections();
            RefreshOverlay();
        }

        private void UnbindFromGameState()
        {
            if (!overlayBound)
                return;

            overlayBound = false;
            refreshTimer = 0f;
            dataProvider = null;
            gameState = null;
            timeSystem = null;
            characterSystem = null;
            characterRepository = null;
            officeSystem = null;
            electionSystem = null;
            eventBus = null;
            lastLogCount = -1;
            lastLogTimestamp = DateTime.MinValue;
            lastOfficeDisplayHash = 0;
            lastElectionDisplayHash = 0;

            DisposeSections();
        }

        private void BindSections()
        {
            if (eventBus == null)
                return;

            populationPanel?.Bind(eventBus, characterSystem, characterRepository);
            dailyEventPanel?.Bind(eventBus, characterSystem);
            politicalPanel?.Bind(eventBus, electionSystem, officeSystem, characterSystem);

            ApplySectionVisibility(forceRefresh: true);
        }

        private void DisposeSections()
        {
            populationPanel?.Dispose();
            dailyEventPanel?.Dispose();
            politicalPanel?.Dispose();
        }

        private bool CheckVisibilityChanges()
        {
            bool changed = false;

            if (previousShowSystemLogPanel != showSystemLogPanel)
            {
                previousShowSystemLogPanel = showSystemLogPanel;
                changed = true;
            }

            if (previousShowPopulationPanel != showPopulationPanel)
            {
                previousShowPopulationPanel = showPopulationPanel;
                changed = true;
            }

            if (previousShowDailyEventPanel != showDailyEventPanel)
            {
                previousShowDailyEventPanel = showDailyEventPanel;
                changed = true;
            }

            if (previousShowPoliticalPanel != showPoliticalPanel)
            {
                previousShowPoliticalPanel = showPoliticalPanel;
                changed = true;
            }

            return changed;
        }

        private void ApplySectionVisibility(bool forceRefresh = false)
        {
            view.SetLogVisibility(showSystemLogPanel);

            if (populationPanel != null)
            {
                populationPanel.SetVisible(showPopulationPanel);
                if ((forceRefresh || showPopulationPanel) && overlayBound)
                    populationPanel.RefreshImmediate();
            }

            if (dailyEventPanel != null)
            {
                dailyEventPanel.SetVisible(showDailyEventPanel);
                if (forceRefresh || showDailyEventPanel)
                    dailyEventPanel.RefreshImmediate();
            }

            if (politicalPanel != null)
            {
                politicalPanel.SetVisible(showPoliticalPanel);
                if ((forceRefresh || showPoliticalPanel) && overlayBound)
                    politicalPanel.RefreshImmediate();
            }
        }

        private void RefreshOverlay()
        {
            if (!overlayBound || dataProvider == null)
                return;

            var snapshot = dataProvider.CreateSnapshot(providerConfiguration);

            view.SetDate(snapshot.Date);
            view.SetLivingPopulation(snapshot.CharacterCountText);
            view.SetFamilyCount(snapshot.FamilyCountText);
            view.SetOfficeSection(snapshot.OfficeHeader, snapshot.OfficeBody);
            view.SetElectionSection(snapshot.ElectionHeader, snapshot.ElectionBody);

            UpdateLogs(snapshot);

            lastOfficeDisplayHash = snapshot.OfficeDisplayHash;
            lastElectionDisplayHash = snapshot.ElectionDisplayHash;

            if (populationPanel != null && showPopulationPanel)
                populationPanel.RefreshImmediate();

            if (dailyEventPanel != null && showDailyEventPanel)
                dailyEventPanel.RefreshImmediate();

            if (politicalPanel != null && showPoliticalPanel)
                politicalPanel.RefreshImmediate();

            ValidateSeasonalSections();
        }

        private void UpdateLogs(DebugOverlayDataProvider.DebugOverlaySnapshot snapshot)
        {
            if (snapshot.LogCount == 0)
            {
                if (lastLogCount != 0)
                    view.SetLogText(string.Empty);

                lastLogCount = 0;
                lastLogTimestamp = DateTime.MinValue;
                return;
            }

            if (snapshot.LogCount == lastLogCount && snapshot.LatestLogTimestamp == lastLogTimestamp)
                return;

            lastLogCount = snapshot.LogCount;
            lastLogTimestamp = snapshot.LatestLogTimestamp;

            view.SetLogText(snapshot.LogText);
            Canvas.ForceUpdateCanvases();
            view.ScrollLogsToBottom();
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

            if (officeSystem != null)
            {
                if (lastSeasonValidationKey != int.MinValue && current.month == 1 && lastSeasonOfficeHash == lastOfficeDisplayHash)
                {
                    Logger.Warn("UI", "[DebugOverlay] Office section did not update at the new year. Verify bindings.");
                }

                lastSeasonOfficeHash = lastOfficeDisplayHash;
            }

            if (electionSystem != null)
            {
                bool inElectionSeason = current.month >= 6 && current.month <= 7;
                if (inElectionSeason && view != null && !view.HasElectionEntries)
                {
                    Logger.Warn("UI", "[DebugOverlay] Election section empty during election season.");
                }

                if (lastSeasonValidationKey != int.MinValue && current.month == 7 && lastSeasonElectionHash == lastElectionDisplayHash)
                {
                    Logger.Warn("UI", "[DebugOverlay] Election results section did not refresh after elections.");
                }

                lastSeasonElectionHash = lastElectionDisplayHash;
            }

            lastSeasonValidationKey = seasonKey;
#endif
        }
    }
}
