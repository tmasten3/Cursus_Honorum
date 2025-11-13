using System;
using UnityEngine;
using Game.Core;
using Game.Systems.Time;

public class GameController : MonoBehaviour
{
    [Header("Simulation Controls")]
    [Tooltip("How many real seconds each in-game day lasts.")]
    [SerializeField, Range(0.1f, 10f)]
    private float secondsPerDay = 2f;

    [Tooltip("Time speed multiplier (1x = normal speed).")]
    [SerializeField, Range(0.1f, 10f)]
    private float speedMultiplier = 1f;

    [Tooltip("Pause or resume the simulation.")]
    [SerializeField]
    private bool paused = false;

    [Header("Persistence")]
    [Tooltip("Name of the save file written under persistentDataPath.")]
    [SerializeField]
    private string saveFileName = SaveService.DefaultFileName;

    [Tooltip("Attempt to load the save file automatically when the simulation starts.")]
    [SerializeField]
    private bool autoLoadOnStart = false;

    [Tooltip("Hotkey used for saving the current game state.")]
    [SerializeField]
    private KeyCode quickSaveKey = KeyCode.F5;

    [Tooltip("Hotkey used for loading the saved game state.")]
    [SerializeField]
    private KeyCode quickLoadKey = KeyCode.F9;

    private GameState gameState;
    private TimeSystem timeSystem;
    private bool isInitialized;
    private SaveService saveService;

    public GameState GameState => gameState;
    public bool IsInitialized => isInitialized;

    public event Action<GameState> GameStateInitialized;
    public event Action GameStateShuttingDown;

    void Awake()
    {
        saveService = new SaveService();
    }

    void Start()
    {
        if (isInitialized)
            return;

        try
        {
            gameState = new GameState();
            gameState.Initialize();

            timeSystem = gameState.GetSystem<TimeSystem>();
            if (timeSystem == null)
                throw new InvalidOperationException("TimeSystem failed to initialize.");

            // Apply inspector defaults
            timeSystem.SetGameSpeed(speedMultiplier);
            timeSystem.SetSecondsPerDay(secondsPerDay);
            if (paused) timeSystem.Pause();

            if (autoLoadOnStart && saveService != null && saveService.HasSave(saveFileName))
            {
                if (!saveService.LoadInto(gameState, saveFileName))
                {
                    Game.Core.Logger.Warn("GameController", "Auto-load failed; continuing with freshly initialized state.");
                }
            }

            isInitialized = true;

            if (GameStateInitialized != null)
            {
                try
                {
                    GameStateInitialized.Invoke(gameState);
                }
                catch (Exception ex)
                {
                    Game.Core.Logger.Error("GameController", $"GameStateInitialized listener failed: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Game.Core.Logger.Error("GameController", $"Failed to initialize GameState: {ex.Message}");
            enabled = false;
        }
    }

    void Update()
    {
        if (!isInitialized || gameState == null)
            return;

        if (quickSaveKey != KeyCode.None && Input.GetKeyDown(quickSaveKey))
            SaveGameToDisk();

        if (quickLoadKey != KeyCode.None && Input.GetKeyDown(quickLoadKey))
            LoadGameFromDisk();

        gameState.Update();

        // Live sync inspector changes
        if (timeSystem != null)
        {
            timeSystem.SetGameSpeed(speedMultiplier);
            timeSystem.SetSecondsPerDay(secondsPerDay);

            if (paused && !timeSystem.IsPaused)
                timeSystem.Pause();
            else if (!paused && timeSystem.IsPaused)
                timeSystem.Resume();
        }
    }

    void OnDestroy()
    {
        if (gameState != null)
        {
            if (GameStateShuttingDown != null)
            {
                try
                {
                    GameStateShuttingDown.Invoke();
                }
                catch (Exception ex)
                {
                    Game.Core.Logger.Error("GameController", $"GameStateShuttingDown listener failed: {ex.Message}");
                }
            }
            gameState.Shutdown();
            gameState = null;
            timeSystem = null;
            isInitialized = false;
        }
    }

    public void SaveGameToDisk()
    {
        if (!isInitialized || gameState == null)
        {
            Game.Core.Logger.Warn("GameController", "Cannot save before the game state is initialized.");
            return;
        }

        if (saveService == null)
        {
            Game.Core.Logger.Error("GameController", "SaveService is not available.");
            return;
        }

        var path = saveService.Save(gameState, saveFileName);
        if (string.IsNullOrEmpty(path))
        {
            Game.Core.Logger.Error("GameController", "Save operation failed.");
        }
    }

    public void LoadGameFromDisk()
    {
        if (!isInitialized || gameState == null)
        {
            Game.Core.Logger.Warn("GameController", "Cannot load before the game state is initialized.");
            return;
        }

        if (saveService == null)
        {
            Game.Core.Logger.Error("GameController", "SaveService is not available.");
            return;
        }

        if (!saveService.LoadInto(gameState, saveFileName))
        {
            Game.Core.Logger.Warn("GameController", "Load operation failed.");
        }
    }

}

