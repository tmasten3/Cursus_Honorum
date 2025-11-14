using System;
using Game.Core;
using Game.Systems.CharacterSystem;
using Game.Systems.EventBus;
using Game.Systems.Politics.Elections;
using Game.Systems.Politics.Offices;
using Game.Systems.Time;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.CharacterDetail
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public sealed class CharacterDetailPanel : MonoBehaviour
    {
        private Canvas canvas;
        private GraphicRaycaster raycaster;
        private CharacterDetailBuilder builder;
        private CharacterDetailDataAdapter dataAdapter;

        private GameController controller;
        private GameState gameState;
        private EventBus eventBus;
        private CharacterSystem characterSystem;
        private OfficeSystem officeSystem;
        private ElectionSystem electionSystem;
        private TimeSystem timeSystem;

        private bool systemsBound;
        private bool subscriptionsActive;
        private bool isVisible;

        private int? selectedCharacterId;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            raycaster = GetComponent<GraphicRaycaster>();

            ConfigureCanvas();

            builder = new CharacterDetailBuilder((RectTransform)transform);
            builder.Build();
            builder.SetRootActive(false);
            builder.AssignCloseAction(() => HidePanel(true));
            builder.AssignMaskCloseAction(() => HidePanel(true));
        }

        private void OnEnable()
        {
            EnsureSystems();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            systemsBound = false;
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void ConfigureCanvas()
        {
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 2500;
                canvas.enabled = false;
            }

            var scaler = GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }
        }

        private bool EnsureSystems()
        {
            if (systemsBound && dataAdapter != null)
                return true;

            controller ??= FindFirstObjectByType<GameController>();
            if (controller == null)
                return false;

            gameState = controller.GameState;
            if (gameState == null)
                return false;

            eventBus ??= gameState.GetSystem<EventBus>();
            characterSystem ??= gameState.GetSystem<CharacterSystem>();
            officeSystem ??= gameState.GetSystem<OfficeSystem>();
            electionSystem ??= gameState.GetSystem<ElectionSystem>();
            timeSystem ??= gameState.GetSystem<TimeSystem>();

            if (eventBus == null || characterSystem == null)
                return false;

            dataAdapter ??= new CharacterDetailDataAdapter(characterSystem, officeSystem, electionSystem, timeSystem);

            CharacterSelection.Bind(eventBus, timeSystem);

            SubscribeEvents();
            systemsBound = true;
            return true;
        }

        private void SubscribeEvents()
        {
            if (subscriptionsActive || eventBus == null)
                return;

            CharacterSelection.Subscribe(eventBus, HandleCharacterSelected);
            eventBus.Subscribe<OnNewDayEvent>(HandleTick);
            eventBus.Subscribe<OnCharacterBorn>(HandleCharacterEvent);
            eventBus.Subscribe<OnCharacterDied>(HandleCharacterEvent);
            eventBus.Subscribe<OnCharacterMarried>(HandleCharacterEvent);
            eventBus.Subscribe<OnCharacterAmbitionChanged>(HandleCharacterEvent);
            eventBus.Subscribe<OnCharacterRetired>(HandleCharacterEvent);
            eventBus.Subscribe<OnCharacterTraitAdvanced>(HandleCharacterEvent);
            eventBus.Subscribe<OnCharacterCareerMilestoneRecorded>(HandleCharacterEvent);
            eventBus.Subscribe<OfficeAssignedEvent>(HandleOfficeEvent);
            eventBus.Subscribe<ElectionSeasonCompletedEvent>(HandleElectionEvent);

            subscriptionsActive = true;
        }

        private void UnsubscribeEvents()
        {
            if (!subscriptionsActive || eventBus == null)
                return;

            CharacterSelection.Unsubscribe(eventBus, HandleCharacterSelected);
            eventBus.Unsubscribe<OnNewDayEvent>(HandleTick);
            eventBus.Unsubscribe<OnCharacterBorn>(HandleCharacterEvent);
            eventBus.Unsubscribe<OnCharacterDied>(HandleCharacterEvent);
            eventBus.Unsubscribe<OnCharacterMarried>(HandleCharacterEvent);
            eventBus.Unsubscribe<OnCharacterAmbitionChanged>(HandleCharacterEvent);
            eventBus.Unsubscribe<OnCharacterRetired>(HandleCharacterEvent);
            eventBus.Unsubscribe<OnCharacterTraitAdvanced>(HandleCharacterEvent);
            eventBus.Unsubscribe<OnCharacterCareerMilestoneRecorded>(HandleCharacterEvent);
            eventBus.Unsubscribe<OfficeAssignedEvent>(HandleOfficeEvent);
            eventBus.Unsubscribe<ElectionSeasonCompletedEvent>(HandleElectionEvent);

            subscriptionsActive = false;
        }

        private void HandleCharacterSelected(CharacterSelectedEvent e)
        {
            if (!EnsureSystems())
                return;

            if (e.CharacterId <= 0)
            {
                HidePanel(true);
                return;
            }

            selectedCharacterId = e.CharacterId;
            if (TryRefreshSnapshot())
                ShowPanel();
            else
                HidePanel(true);
        }

        private void HandleTick(OnNewDayEvent _)
        {
            RefreshIfVisible();
        }

        private void HandleCharacterEvent(GameEvent _)
        {
            RefreshIfVisible();
        }

        private void HandleOfficeEvent(OfficeAssignedEvent _)
        {
            RefreshIfVisible();
        }

        private void HandleElectionEvent(ElectionSeasonCompletedEvent _)
        {
            RefreshIfVisible();
        }

        private void RefreshIfVisible()
        {
            if (!isVisible || !selectedCharacterId.HasValue)
                return;

            if (!EnsureSystems())
                return;

            if (!TryRefreshSnapshot())
                HidePanel(true);
        }

        private bool TryRefreshSnapshot()
        {
            if (!selectedCharacterId.HasValue || dataAdapter == null)
                return false;

            if (!dataAdapter.TryBuildSnapshot(selectedCharacterId.Value, out var snapshot))
                return false;

            ApplySnapshot(snapshot);
            return true;
        }

        private void ApplySnapshot(CharacterDetailSnapshot snapshot)
        {
            var meta = BuildMetaLine(snapshot);
            var color = ResolveClassColor(snapshot.SocialClass);
            builder.UpdateHeader(snapshot.FullName, meta, color, snapshot.IsAlive);
            builder.UpdateIdentity(snapshot.IdentitySummary);
            builder.UpdateFamily(snapshot.FamilySummary);
            builder.UpdateOffices(snapshot.OfficesSummary);
            builder.UpdateTraits(snapshot.TraitsSummary);
            builder.UpdateElections(snapshot.ElectionsSummary);
        }

        private string BuildMetaLine(CharacterDetailSnapshot snapshot)
        {
            var parts = new System.Collections.Generic.List<string>(4);

            if (snapshot.Age > 0)
                parts.Add($"Age {snapshot.Age}");

            parts.Add(snapshot.SocialClass.ToString());

            if (!string.IsNullOrWhiteSpace(snapshot.FamilyName))
                parts.Add(snapshot.FamilyName);

            var birth = FormatBirth(snapshot.BirthYear, snapshot.BirthMonth, snapshot.BirthDay);
            if (!string.IsNullOrWhiteSpace(birth))
                parts.Add($"Born {birth}");

            return string.Join(" • ", parts);
        }

        private string FormatBirth(int year, int month, int day)
        {
            if (year == 0 && month == 0 && day == 0)
                return string.Empty;

            var yearText = FormatYear(year);
            if (month <= 0 && day <= 0)
                return yearText;

            string monthText = month > 0 ? month.ToString("00") : "??";
            string dayText = day > 0 ? day.ToString("00") : "??";
            return $"{dayText}/{monthText}/{yearText}";
        }

        private string FormatYear(int year)
        {
            if (year == 0)
                return "?";
            if (year < 0)
                return $"{Math.Abs(year)} BCE";
            return $"{year} CE";
        }

        private Color ResolveClassColor(Game.Data.Characters.SocialClass socialClass)
        {
            return socialClass switch
            {
                Game.Data.Characters.SocialClass.Patrician => new Color(0.78f, 0.68f, 0.32f, 1f),
                Game.Data.Characters.SocialClass.Equestrian => new Color(0.45f, 0.64f, 0.88f, 1f),
                Game.Data.Characters.SocialClass.Plebeian => new Color(0.52f, 0.82f, 0.52f, 1f),
                _ => new Color(0.7f, 0.7f, 0.7f, 1f)
            };
        }

        private void ShowPanel()
        {
            if (isVisible)
                return;

            isVisible = true;
            builder.SetRootActive(true);
            if (canvas != null)
                canvas.enabled = true;
            if (raycaster != null)
                raycaster.enabled = true;
        }

        private void HidePanel(bool clearSelection)
        {
            if (!isVisible && !clearSelection)
                return;

            if (clearSelection)
                selectedCharacterId = null;

            isVisible = false;
            builder.SetRootActive(false);
            if (canvas != null)
                canvas.enabled = false;
            if (raycaster != null)
                raycaster.enabled = false;
        }
    }
}
