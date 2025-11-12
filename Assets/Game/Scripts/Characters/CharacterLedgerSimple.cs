using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Systems.CharacterSystem;
using Game.Systems.EventBus;
using Game.Data.Characters;

public class CharacterLedgerSimple : MonoBehaviour
{
    [Header("UI References")]
    public Transform listContainer;   // ScrollView/Viewport/Content
    public TMP_Text rowPrefab;        // CharacterRow prefab

    private CharacterSystem characterSystem;
    private EventBus eventBus;

    private void OnEnable()
    {
        StartCoroutine(InitializeWhenReady());
    }

    private IEnumerator InitializeWhenReady()
    {
        // Wait one frame for GameController initialization
        yield return null;

        var controller = FindFirstObjectByType<GameController>();
        if (controller == null)
        {
            Debug.LogError("[Ledger] No GameController found in scene!");
            yield break;
        }

        if (controller.GameState == null)
        {
            Debug.LogError("[Ledger] GameState not initialized on GameController!");
            yield break;
        }

        characterSystem = controller.GameState.GetSystem<CharacterSystem>();
        eventBus = controller.GameState.GetSystem<EventBus>();

        if (characterSystem == null || eventBus == null)
        {
            Debug.LogError("[Ledger] Required systems not found!");
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
        // Clear existing rows
        foreach (Transform child in listContainer)
            Destroy(child.gameObject);

        var chars = characterSystem.GetAllLiving();

        foreach (var c in chars)
        {
            var row = Instantiate(rowPrefab, listContainer);

            // Ensure RomanName exists
            if (c.RomanName == null)
                c.RomanName = RomanNamingRules.GenerateRomanName(c.Gender, c.Family, c.Class);

            string fullName = RomanNamingRules.GetFullName(c);
            if (string.IsNullOrWhiteSpace(fullName) || fullName == "Unknown")
            {
                c.RomanName = RomanNamingRules.GenerateRomanName(c.Gender, c.Family, c.Class);
                fullName = RomanNamingRules.GetFullName(c);
            }

            row.text = $"{fullName} — Age {c.Age} — {c.Class}";
        }

        Debug.Log($"[Ledger] Auto-refreshed list with {chars.Count} characters.");
    }
}
