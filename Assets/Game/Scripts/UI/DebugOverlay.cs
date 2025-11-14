using System;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.Systems.CharacterSystem;
using Game.Systems.EventBus;
using Game.Systems.Politics.Elections;
using Game.Systems.Politics.Offices;
using Game.Systems.Time;

namespace Game.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public sealed class DebugOverlay : MonoBehaviour
    {
        private const float MinimumRefreshInterval = 0.1f;

        [SerializeField, Range(MinimumRefreshInterval, 5f)]
        private float refreshInterval = 0.5f;

        private Canvas canvas;
        private GraphicRaycaster raycaster;
        private DebugOverlayBuilder builder;
        private DebugOverlayDataAdapter dataAdapter;

        private GameController controller;
        private GameState gameState;
        private TimeSystem timeSystem;
        private CharacterSystem characterSystem;
        private CharacterRepository characterRepository;
        private OfficeSystem officeSystem;
        private ElectionSystem electionSystem;
        private EventBus eventBus;

        private float refreshTimer;
        private bool adapterInitialized;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            raycaster = GetComponent<GraphicRaycaster>();

            ConfigureCanvas();

            builder = new DebugOverlayBuilder((RectTransform)transform);
            builder.Build();
            builder.SetRootActive(true);
        }

        private void OnEnable()
        {
            refreshTimer = refreshInterval;

            if (canvas != null)
                canvas.enabled = true;

            if (raycaster != null)
                raycaster.enabled = true;

            if (builder != null)
                builder.SetRootActive(true);

            EnsureSystemsBound();
        }

        private void OnDisable()
        {
            if (dataAdapter != null)
            {
                dataAdapter.Dispose();
                dataAdapter = null;
            }

            adapterInitialized = false;
        }

        private void OnDestroy()
        {
            if (dataAdapter != null)
            {
                dataAdapter.Dispose();
                dataAdapter = null;
            }
        }

        private void OnValidate()
        {
            refreshInterval = Mathf.Max(MinimumRefreshInterval, refreshInterval);
        }

        private void Update()
        {
            if (!EnsureSystemsBound())
                return;

            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer < refreshInterval)
                return;

            refreshTimer = 0f;
            RefreshOverlay();
        }

        private void ConfigureCanvas()
        {
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000;
            }

            var scaler = GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            var rectTransform = (RectTransform)transform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
        }

        private bool EnsureSystemsBound()
        {
            if (adapterInitialized && dataAdapter != null)
                return true;

            controller = controller != null ? controller : UnityEngine.Object.FindFirstObjectByType<GameController>();
            if (controller == null || !controller.IsInitialized)
                return false;

            gameState = controller.GameState;
            if (gameState == null)
                return false;

            timeSystem ??= gameState.GetSystem<TimeSystem>();
            characterSystem ??= gameState.GetSystem<CharacterSystem>();
            officeSystem ??= gameState.GetSystem<OfficeSystem>();
            electionSystem ??= gameState.GetSystem<ElectionSystem>();
            eventBus ??= gameState.GetSystem<EventBus>();

            if (characterRepository == null && characterSystem != null)
            {
                if (!characterSystem.TryGetRepository(out characterRepository))
                    characterRepository = null;
            }

            if (timeSystem == null || characterSystem == null || characterRepository == null || officeSystem == null || electionSystem == null || eventBus == null)
                return false;

            dataAdapter = new DebugOverlayDataAdapter(timeSystem, characterSystem, characterRepository, officeSystem, electionSystem, eventBus);
            dataAdapter.Initialize();
            adapterInitialized = true;
            refreshTimer = refreshInterval;
            RefreshOverlay();
            return true;
        }

        private void RefreshOverlay()
        {
            if (builder == null || dataAdapter == null)
                return;

            var snapshot = dataAdapter.CreateSnapshot();
            builder.UpdateSimulation(snapshot.Simulation);
            builder.UpdatePopulation(snapshot.Population);
            builder.UpdatePolitics(snapshot.Politics);
        }
    }
}
