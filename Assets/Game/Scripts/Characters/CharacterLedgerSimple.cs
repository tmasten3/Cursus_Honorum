using System;
using System.Collections;
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

    private void Awake()
    {
        if (listContainer == null)
            Logger.Warn("Safety", "[CharacterLedger] List container reference missing.");
        if (rowPrefab == null)
            Logger.Warn("Safety", "[CharacterLedger] Row prefab reference missing.");
    }

    private void OnEnable()
    {
        if (!initializationAttempted)
        {
            StartCoroutine(InitializeWhenReady());
        }
        else if (characterSystem != null)
        {
            Refresh();
        }
        else
        {
            StartCoroutine(InitializeWhenReady());
        }
    }

    private IEnumerator InitializeWhenReady()
    {
        initializationAttempted = true;

        // Wait one frame for GameController initialization
        yield return null;

        var controller = FindFirstObjectByType<GameController>();
        if (controller == null)
        {
            Logger.Warn("Safety", "[CharacterLedger] No GameController found in scene.");
            yield break;
        }

        if (controller.GameState == null)
        {
            Logger.Warn("Safety", "[CharacterLedger] GameState not initialized on GameController.");
            yield break;
        }

        characterSystem = controller.GameState.GetSystem<CharacterSystem>();
        eventBus = controller.GameState.GetSystem<EventBus>();

        if (characterSystem == null || eventBus == null)
        {
            Logger.Warn("Safety", "[CharacterLedger] Required systems not found.");
            yield break;
        }

        // Subscribe to population and birth/death events
        eventBus.Subscribe<OnCharacterBorn>(e => Refresh());
        eventBus.Subscribe<OnCharacterDied>(e => Refresh());
        eventBus.Subscribe<OnCharacterMarried>(e => Refresh());
        eventBus.Subscribe<OnPopulationTick>(e => Refresh());

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
            Logger.Warn("Safety", "[CharacterLedger] CharacterSystem unavailable during refresh.");
            return;
        }

        try
        {
            foreach (Transform child in listContainer)
                Destroy(child.gameObject);
        }
        catch (Exception ex)
        {
            Logger.Warn("Safety", $"[CharacterLedger] Failed to clear existing rows: {ex.Message}");
        }

        IReadOnlyList<Character> characters;
        try
        {
            characters = characterSystem.GetAllLiving();
        }
        catch (Exception ex)
        {
            Logger.Error("Safety", $"[CharacterLedger] Unable to fetch living characters: {ex.Message}");
            return;
        }

        foreach (var character in characters)
        {
            if (character == null)
            {
                Logger.Warn("Safety", "[CharacterLedger] Encountered null character while building list.");
                continue;
            }

            TMP_Text rowInstance = null;
            try
            {
                rowInstance = Instantiate(rowPrefab, listContainer);
            }
            catch (Exception ex)
            {
                Logger.Error("Safety", $"[CharacterLedger] Failed to instantiate row prefab: {ex.Message}");
                continue;
            }

            string fullName;
            try
            {
                if (character.RomanName == null)
                    character.RomanName = RomanNamingRules.GenerateRomanName(character.Gender, character.Family, character.Class);

                fullName = RomanNamingRules.GetFullName(character);
                if (string.IsNullOrWhiteSpace(fullName) || string.Equals(fullName, "Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    character.RomanName = RomanNamingRules.GenerateRomanName(character.Gender, character.Family, character.Class);
                    fullName = RomanNamingRules.GetFullName(character);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Safety", $"[CharacterLedger] Failed to resolve name for character #{character.ID}: {ex.Message}");
                fullName = $"Character #{character.ID}";
            }

            try
            {
                rowInstance.text = $"{fullName} — Age {character.Age} — {character.Class}";
            }
            catch (Exception ex)
            {
                Logger.Warn("Safety", $"[CharacterLedger] Failed to assign row text for character #{character.ID}: {ex.Message}");
            }
        }

        Logger.Info("Ledger", $"Auto-refreshed list with {characters.Count} characters.");
    }
}
