using System;
using UnityEngine;
using Game.Core;
using Game.Systems.Time;
using Game.Core.Save;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

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
    private SaveRepository saveRepository;
    private SaveSerializer saveSerializer;

    public GameState GameState => gameState;
    public bool IsInitialized => isInitialized;

    public event Action<GameState> GameStateInitialized;
    public event Action GameStateShuttingDown;

    void Awake()
    {
        saveRepository = new SaveRepository();
        saveSerializer = new SaveSerializer();
        saveService = new SaveService(null, saveRepository, saveSerializer);
    }

    void Start()
    {
        if (isInitialized)
            return;

        try
        {
            gameState = new GameState();
            gameState.Initialize();

            saveService.BindGameState(gameState);

            timeSystem = gameState.GetSystem<TimeSystem>();
            if (timeSystem == null)
                throw new InvalidOperationException("TimeSystem failed to initialize.");

            // Apply inspector defaults
            timeSystem.SetGameSpeed(speedMultiplier);
            timeSystem.SetSecondsPerDay(secondsPerDay);
            if (paused) timeSystem.Pause();

            if (autoLoadOnStart && saveService != null && saveService.HasSave(saveFileName))
            {
                var loadResult = saveService.LoadGame(saveFileName);
                if (!loadResult.Success)
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

        if (quickSaveKey != KeyCode.None && IsKeyActivationTriggered(quickSaveKey))
        {
            SaveGameToDisk();
        }

        if (quickLoadKey != KeyCode.None && IsKeyActivationTriggered(quickLoadKey))
        {
            LoadGameFromDisk();
        }

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

        var result = saveService.SaveGame(saveFileName);
        if (!result.Success)
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

        var result = saveService.LoadGame(saveFileName);
        if (!result.Success)
        {
            Game.Core.Logger.Warn("GameController", "Load operation failed.");
        }
    }

#if ENABLE_INPUT_SYSTEM
    private static readonly Dictionary<KeyCode, Func<Keyboard, KeyControl>> KeyboardControlResolvers;

    static GameController()
    {
        KeyboardControlResolvers = new Dictionary<KeyCode, Func<Keyboard, KeyControl>>();

        void Map(KeyCode key, Func<Keyboard, KeyControl> resolver)
        {
            if (!KeyboardControlResolvers.ContainsKey(key))
                KeyboardControlResolvers.Add(key, resolver);
        }

        Map(KeyCode.Return, keyboard => keyboard.enterKey);
        Map(KeyCode.KeypadEnter, keyboard => keyboard.numpadEnterKey);
        Map(KeyCode.LeftArrow, keyboard => keyboard.leftArrowKey);
        Map(KeyCode.RightArrow, keyboard => keyboard.rightArrowKey);
        Map(KeyCode.UpArrow, keyboard => keyboard.upArrowKey);
        Map(KeyCode.DownArrow, keyboard => keyboard.downArrowKey);
        Map(KeyCode.LeftControl, keyboard => keyboard.leftCtrlKey);
        Map(KeyCode.RightControl, keyboard => keyboard.rightCtrlKey);
        Map(KeyCode.LeftShift, keyboard => keyboard.leftShiftKey);
        Map(KeyCode.RightShift, keyboard => keyboard.rightShiftKey);
        Map(KeyCode.LeftAlt, keyboard => keyboard.leftAltKey);
        Map(KeyCode.RightAlt, keyboard => keyboard.rightAltKey);
        Map(KeyCode.Space, keyboard => keyboard.spaceKey);
        Map(KeyCode.Alpha0, keyboard => keyboard.digit0Key);
        Map(KeyCode.Alpha1, keyboard => keyboard.digit1Key);
        Map(KeyCode.Alpha2, keyboard => keyboard.digit2Key);
        Map(KeyCode.Alpha3, keyboard => keyboard.digit3Key);
        Map(KeyCode.Alpha4, keyboard => keyboard.digit4Key);
        Map(KeyCode.Alpha5, keyboard => keyboard.digit5Key);
        Map(KeyCode.Alpha6, keyboard => keyboard.digit6Key);
        Map(KeyCode.Alpha7, keyboard => keyboard.digit7Key);
        Map(KeyCode.Alpha8, keyboard => keyboard.digit8Key);
        Map(KeyCode.Alpha9, keyboard => keyboard.digit9Key);
        Map(KeyCode.Backspace, keyboard => keyboard.backspaceKey);
        Map(KeyCode.Delete, keyboard => keyboard.deleteKey);
        Map(KeyCode.Insert, keyboard => keyboard.insertKey);
        Map(KeyCode.Tab, keyboard => keyboard.tabKey);
        Map(KeyCode.Escape, keyboard => keyboard.escapeKey);
        Map(KeyCode.CapsLock, keyboard => keyboard.capsLockKey);
        Map(KeyCode.Numlock, keyboard => keyboard.numLockKey);
        Map(KeyCode.ScrollLock, keyboard => keyboard.scrollLockKey);
        Map(KeyCode.Print, keyboard => keyboard.printScreenKey);
        Map(KeyCode.Pause, keyboard => keyboard.pauseKey);
        Map(KeyCode.Home, keyboard => keyboard.homeKey);
        Map(KeyCode.End, keyboard => keyboard.endKey);
        Map(KeyCode.PageUp, keyboard => keyboard.pageUpKey);
        Map(KeyCode.PageDown, keyboard => keyboard.pageDownKey);
        Map(KeyCode.BackQuote, keyboard => keyboard.backquoteKey);
        Map(KeyCode.Minus, keyboard => keyboard.minusKey);
        Map(KeyCode.Equals, keyboard => keyboard.equalsKey);
        Map(KeyCode.LeftBracket, keyboard => keyboard.leftBracketKey);
        Map(KeyCode.RightBracket, keyboard => keyboard.rightBracketKey);
        Map(KeyCode.Semicolon, keyboard => keyboard.semicolonKey);
        Map(KeyCode.Quote, keyboard => keyboard.quoteKey);
        Map(KeyCode.Comma, keyboard => keyboard.commaKey);
        Map(KeyCode.Period, keyboard => keyboard.periodKey);
        Map(KeyCode.Slash, keyboard => keyboard.slashKey);
        Map(KeyCode.Backslash, keyboard => keyboard.backslashKey);

        // Multiple enum aliases (platform-specific) may map to the same underlying value.
        // Attempt to add all known aliases but tolerate duplicates by skipping them.
        Map(KeyCode.LeftWindows, keyboard => keyboard.leftWindowsKey);
        Map(KeyCode.LeftCommand, keyboard => keyboard.leftWindowsKey);
        Map(KeyCode.LeftApple, keyboard => keyboard.leftWindowsKey);
        Map(KeyCode.RightWindows, keyboard => keyboard.rightWindowsKey);
        Map(KeyCode.RightCommand, keyboard => keyboard.rightWindowsKey);
        Map(KeyCode.RightApple, keyboard => keyboard.rightWindowsKey);

        Map(KeyCode.Menu, keyboard => keyboard.contextMenuKey);
        Map(KeyCode.Keypad0, keyboard => keyboard.numpad0Key);
        Map(KeyCode.Keypad1, keyboard => keyboard.numpad1Key);
        Map(KeyCode.Keypad2, keyboard => keyboard.numpad2Key);
        Map(KeyCode.Keypad3, keyboard => keyboard.numpad3Key);
        Map(KeyCode.Keypad4, keyboard => keyboard.numpad4Key);
        Map(KeyCode.Keypad5, keyboard => keyboard.numpad5Key);
        Map(KeyCode.Keypad6, keyboard => keyboard.numpad6Key);
        Map(KeyCode.Keypad7, keyboard => keyboard.numpad7Key);
        Map(KeyCode.Keypad8, keyboard => keyboard.numpad8Key);
        Map(KeyCode.Keypad9, keyboard => keyboard.numpad9Key);
        Map(KeyCode.KeypadDivide, keyboard => keyboard.numpadDivideKey);
        Map(KeyCode.KeypadMultiply, keyboard => keyboard.numpadMultiplyKey);
        Map(KeyCode.KeypadMinus, keyboard => keyboard.numpadMinusKey);
        Map(KeyCode.KeypadPlus, keyboard => keyboard.numpadPlusKey);
        Map(KeyCode.KeypadPeriod, keyboard => keyboard.numpadPeriodKey);
        Map(KeyCode.KeypadEquals, keyboard => keyboard.numpadEqualsKey);
    }

    private bool TryGetNewInputSystemKeyState(KeyCode keyCode, out bool wasPressed)
    {
        wasPressed = false;

        var keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        if (!TryGetKeyControl(keyboard, keyCode, out var keyControl) || keyControl == null)
            return false;

        wasPressed = keyControl.wasPressedThisFrame;
        return true;
    }

    private bool TryGetKeyControl(Keyboard keyboard, KeyCode keyCode, out KeyControl keyControl)
    {
        keyControl = null;

        if (keyboard == null)
            return false;

        if (KeyboardControlResolvers.TryGetValue(keyCode, out var resolver) && resolver != null)
        {
            keyControl = resolver.Invoke(keyboard);
            if (keyControl != null)
                return true;
        }

        if (Enum.TryParse<Key>(keyCode.ToString(), true, out var parsedKey))
        {
            keyControl = keyboard[parsedKey];
            if (keyControl != null)
                return true;
        }

        return false;
    }
#endif

    private bool IsKeyActivationTriggered(KeyCode keyCode)
    {
        if (keyCode == KeyCode.None)
            return false;

#if ENABLE_INPUT_SYSTEM
        if (TryGetNewInputSystemKeyState(keyCode, out var pressed))
            return pressed;

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(keyCode);
#else
        return false;
#endif
#else
        return Input.GetKeyDown(keyCode);
#endif
    }
}
