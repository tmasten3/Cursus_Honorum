using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Game.Core;
using Game.Systems.CharacterSystem;
using Game.Systems.EventBus;
using Game.Data.Characters;

[DisallowMultipleComponent]
public class CharacterLedgerSimple : MonoBehaviour
{
    [Header("UI References")]
    public Transform listContainer;   // ScrollView/Viewport/Content
    public TMP_Text rowPrefab;        // CharacterRow prefab

    private CharacterSystem characterSystem;
    private EventBus eventBus;
    private bool initializationAttempted;
    private bool handlersBound;

    private Action<OnCharacterBorn> onCharacterBornHandler;
    private Action<OnCharacterDied> onCharacterDiedHandler;
    private Action<OnCharacterMarried> onCharacterMarriedHandler;
    private Action<OnPopulationTick> onPopulationTickHandler;

    private int refreshCount;

#if UNITY_EDITOR
    private bool editorCycleActive;
    private int editorRefreshCountAtEnable;
    private int editorLastCycleRefreshCount = -1;
#endif

    private void Awake()
    {
        if (listContainer == null)
            Game.Core.Logger.Warn("Safety", "[CharacterLedger] List container reference missing.");
        if (rowPrefab == null)
            Game.Core.Logger.Warn("Safety", "[CharacterLedger] Row prefab reference missing.");
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        editorCycleActive = Application.isPlaying;
        if (editorCycleActive)
            editorRefreshCountAtEnable = refreshCount;
#endif

        if (!initializationAttempted || characterSystem == null || eventBus == null)
        {
            StartCoroutine(InitializeWhenReady());
            return;
        }

        EnsureEventHandlersBound();
        Refresh();
    }

    private void OnDisable()
    {
        if (eventBus != null && handlersBound)
        {
            eventBus.Unsubscribe(onCharacterBornHandler);
            eventBus.Unsubscribe(onCharacterDiedHandler);
            eventBus.Unsubscribe(onCharacterMarriedHandler);
            eventBus.Unsubscribe(onPopulationTickHandler);
            handlersBound = false;
        }

#if UNITY_EDITOR
        if (editorCycleActive)
        {
            var cycleRefreshCount = refreshCount - editorRefreshCountAtEnable;
            if (editorLastCycleRefreshCount >= 0 && cycleRefreshCount != editorLastCycleRefreshCount)
            {
                Game.Core.Logger.Warn("Validation", $"[CharacterLedger] Play mode toggle detected inconsistent refresh count: previous {editorLastCycleRefreshCount}, current {cycleRefreshCount}.");
            }
            else
            {
                Game.Core.Logger.Info("Validation", $"[CharacterLedger] Play mode toggle refresh count verified at {cycleRefreshCount}.");
            }

            editorLastCycleRefreshCount = cycleRefreshCount;
            editorCycleActive = false;
        }
#endif
    }

    private IEnumerator InitializeWhenReady()
    {
        initializationAttempted = true;

        // Wait one frame for GameController initialization
        yield return null;

        var controller = FindFirstObjectByType<Game.Core.GameController>();
        if (controller == null)
        {
            Game.Core.Logger.Warn("Safety", "[CharacterLedger] No GameController found in scene.");
            yield break;
        }

        if (controller.GameState == null)
        {
            Game.Core.Logger.Warn("Safety", "[CharacterLedger] GameState not initialized on GameController.");
            yield break;
        }

        characterSystem = controller.GameState.GetSystem<CharacterSystem>();
        eventBus = controller.GameState.GetSystem<EventBus>();

        if (characterSystem == null || eventBus == null)
        {
            Game.Core.Logger.Warn("Safety", "[CharacterLedger] Required systems not found.");
            yield break;
        }

        EnsureEventHandlersBound();

        BuildList();
    }

    private void Refresh()
    {
        // Refresh safely from Unity thread
        if (isActiveAndEnabled)
            BuildList();
    }

    private void BuildList()
    {
        if (listContainer == null || rowPrefab == null)
            return;

        if (characterSystem == null)
        {
            Game.Core.Logger.Warn("Safety", "[CharacterLedger] CharacterSystem unavailable during refresh.");
            return;
        }

        refreshCount++;

        try
        {
            foreach (Transform child in listContainer)
                Destroy(child.gameObject);
        }
        catch (Exception ex)
        {
            Game.Core.Logger.Warn("Safety", $"[CharacterLedger] Failed to clear existing rows: {ex.Message}");
        }

        IReadOnlyList<Character> characters;
        try
        {
            characters = characterSystem.GetAllLiving();
        }
        catch (Exception ex)
        {
            Game.Core.Logger.Error("Safety", $"[CharacterLedger] Unable to fetch living characters: {ex.Message}");
            return;
        }

        foreach (var character in characters)
        {
            if (character == null)
            {
                Game.Core.Logger.Warn("Safety", "[CharacterLedger] Encountered null character while building list.");
                continue;
            }

            TMP_Text rowInstance = null;
            try
            {
                rowInstance = Instantiate(rowPrefab, listContainer);
            }
            catch (Exception ex)
            {
                Game.Core.Logger.Error("Safety", $"[CharacterLedger] Failed to instantiate row prefab: {ex.Message}");
                continue;
            }

            string fullName;
            try
            {
                if (character.RomanName == null)
                    character.RomanName = RomanNamingRules.GenerateRomanName(character.Gender, character.Family, character.Class);

                fullName = character.RomanName?.GetFullName();
                if (string.IsNullOrWhiteSpace(fullName) || string.Equals(fullName, "Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    character.RomanName = RomanNamingRules.GenerateRomanName(character.Gender, character.Family, character.Class);
                    fullName = character.RomanName?.GetFullName();
                }

                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = $"Character #{character.ID}";
                }
            }
            catch (Exception ex)
            {
                Game.Core.Logger.Warn("Safety", $"[CharacterLedger] Failed to resolve name for character #{character.ID}: {ex.Message}");
                fullName = $"Character #{character.ID}";
            }

            try
            {
                rowInstance.text = $"{fullName} — Age {character.Age} — {character.Class}";
            }
            catch (Exception ex)
            {
                Game.Core.Logger.Warn("Safety", $"[CharacterLedger] Failed to assign row text for character #{character.ID}: {ex.Message}");
            }
        }

        Game.Core.Logger.Info("Ledger", $"Auto-refreshed list with {characters.Count} characters.");
    }

    private void EnsureEventHandlersBound()
    {
        if (eventBus == null || handlersBound)
            return;

        if (onCharacterBornHandler == null)
            onCharacterBornHandler = HandleCharacterBorn;
        if (onCharacterDiedHandler == null)
            onCharacterDiedHandler = HandleCharacterDied;
        if (onCharacterMarriedHandler == null)
            onCharacterMarriedHandler = HandleCharacterMarried;
        if (onPopulationTickHandler == null)
            onPopulationTickHandler = HandlePopulationTick;

        eventBus.Subscribe(onCharacterBornHandler);
        eventBus.Subscribe(onCharacterDiedHandler);
        eventBus.Subscribe(onCharacterMarriedHandler);
        eventBus.Subscribe(onPopulationTickHandler);

        handlersBound = true;
    }

    private void HandleCharacterBorn(OnCharacterBorn _)
    {
        Refresh();
    }

    private void HandleCharacterDied(OnCharacterDied _)
    {
        Refresh();
    }

    private void HandleCharacterMarried(OnCharacterMarried _)
    {
        Refresh();
    }

    private void HandlePopulationTick(OnPopulationTick _)
    {
        Refresh();
    }
}
