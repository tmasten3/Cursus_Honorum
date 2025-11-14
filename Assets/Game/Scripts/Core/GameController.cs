using System;
using UnityEngine;
using Game.Core;
using Game.Systems.Time;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
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

        if (quickSaveKey != KeyCode.None
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            && WasKeyPressedThisFrame(quickSaveKey)
#else
            && Input.GetKeyDown(quickSaveKey)
#endif
            )
        {
            SaveGameToDisk();
        }

        if (quickLoadKey != KeyCode.None
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            && WasKeyPressedThisFrame(quickLoadKey)
#else
            && Input.GetKeyDown(quickLoadKey)
#endif
            )
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

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
    private bool WasKeyPressedThisFrame(KeyCode keyCode)
    {
        if (keyCode == KeyCode.None)
            return false;

        var keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        return TryGetKeyControl(keyboard, keyCode, out var keyControl) && keyControl.wasPressedThisFrame;
    }

    private bool TryGetKeyControl(Keyboard keyboard, KeyCode keyCode, out KeyControl keyControl)
    {
        keyControl = null;
        if (keyboard == null || keyCode == KeyCode.None)
            return false;

        if (Enum.TryParse<Key>(keyCode.ToString(), out var parsedKey))
        {
            keyControl = keyboard[parsedKey];
            if (keyControl != null)
                return true;
        }

        switch (keyCode)
        {
            case KeyCode.Return:
                keyControl = keyboard.enterKey;
                break;
            case KeyCode.KeypadEnter:
                keyControl = keyboard.numpadEnterKey;
                break;
            case KeyCode.LeftArrow:
                keyControl = keyboard.leftArrowKey;
                break;
            case KeyCode.RightArrow:
                keyControl = keyboard.rightArrowKey;
                break;
            case KeyCode.UpArrow:
                keyControl = keyboard.upArrowKey;
                break;
            case KeyCode.DownArrow:
                keyControl = keyboard.downArrowKey;
                break;
            case KeyCode.LeftControl:
                keyControl = keyboard.leftCtrlKey;
                break;
            case KeyCode.RightControl:
                keyControl = keyboard.rightCtrlKey;
                break;
            case KeyCode.LeftShift:
                keyControl = keyboard.leftShiftKey;
                break;
            case KeyCode.RightShift:
                keyControl = keyboard.rightShiftKey;
                break;
            case KeyCode.LeftAlt:
                keyControl = keyboard.leftAltKey;
                break;
            case KeyCode.RightAlt:
                keyControl = keyboard.rightAltKey;
                break;
            case KeyCode.Space:
                keyControl = keyboard.spaceKey;
                break;
            case KeyCode.Alpha0:
                keyControl = keyboard.digit0Key;
                break;
            case KeyCode.Alpha1:
                keyControl = keyboard.digit1Key;
                break;
            case KeyCode.Alpha2:
                keyControl = keyboard.digit2Key;
                break;
            case KeyCode.Alpha3:
                keyControl = keyboard.digit3Key;
                break;
            case KeyCode.Alpha4:
                keyControl = keyboard.digit4Key;
                break;
            case KeyCode.Alpha5:
                keyControl = keyboard.digit5Key;
                break;
            case KeyCode.Alpha6:
                keyControl = keyboard.digit6Key;
                break;
            case KeyCode.Alpha7:
                keyControl = keyboard.digit7Key;
                break;
            case KeyCode.Alpha8:
                keyControl = keyboard.digit8Key;
                break;
            case KeyCode.Alpha9:
                keyControl = keyboard.digit9Key;
                break;
            case KeyCode.Backspace:
                keyControl = keyboard.backspaceKey;
                break;
            case KeyCode.Delete:
                keyControl = keyboard.deleteKey;
                break;
            case KeyCode.Insert:
                keyControl = keyboard.insertKey;
                break;
            case KeyCode.Tab:
                keyControl = keyboard.tabKey;
                break;
            case KeyCode.Escape:
                keyControl = keyboard.escapeKey;
                break;
            case KeyCode.CapsLock:
                keyControl = keyboard.capsLockKey;
                break;
            case KeyCode.Numlock:
                keyControl = keyboard.numLockKey;
                break;
            case KeyCode.ScrollLock:
                keyControl = keyboard.scrollLockKey;
                break;
            case KeyCode.Print:
                keyControl = keyboard.printScreenKey;
                break;
            case KeyCode.Pause:
                keyControl = keyboard.pauseKey;
                break;
            case KeyCode.Home:
                keyControl = keyboard.homeKey;
                break;
            case KeyCode.End:
                keyControl = keyboard.endKey;
                break;
            case KeyCode.PageUp:
                keyControl = keyboard.pageUpKey;
                break;
            case KeyCode.PageDown:
                keyControl = keyboard.pageDownKey;
                break;
            case KeyCode.BackQuote:
                keyControl = keyboard.backquoteKey;
                break;
            case KeyCode.Minus:
                keyControl = keyboard.minusKey;
                break;
            case KeyCode.Equals:
                keyControl = keyboard.equalsKey;
                break;
            case KeyCode.LeftBracket:
                keyControl = keyboard.leftBracketKey;
                break;
            case KeyCode.RightBracket:
                keyControl = keyboard.rightBracketKey;
                break;
            case KeyCode.Semicolon:
                keyControl = keyboard.semicolonKey;
                break;
            case KeyCode.Quote:
                keyControl = keyboard.quoteKey;
                break;
            case KeyCode.Comma:
                keyControl = keyboard.commaKey;
                break;
            case KeyCode.Period:
                keyControl = keyboard.periodKey;
                break;
            case KeyCode.Slash:
                keyControl = keyboard.slashKey;
                break;
            case KeyCode.Backslash:
                keyControl = keyboard.backslashKey;
                break;
            case KeyCode.LeftWindows:
            case KeyCode.LeftCommand:
            case KeyCode.LeftApple:
                keyControl = keyboard.leftWindowsKey;
                break;
            case KeyCode.RightWindows:
            case KeyCode.RightCommand:
            case KeyCode.RightApple:
                keyControl = keyboard.rightWindowsKey;
                break;
            case KeyCode.Menu:
                keyControl = keyboard.contextMenuKey;
                break;
            case KeyCode.Keypad0:
                keyControl = keyboard.numpad0Key;
                break;
            case KeyCode.Keypad1:
                keyControl = keyboard.numpad1Key;
                break;
            case KeyCode.Keypad2:
                keyControl = keyboard.numpad2Key;
                break;
            case KeyCode.Keypad3:
                keyControl = keyboard.numpad3Key;
                break;
            case KeyCode.Keypad4:
                keyControl = keyboard.numpad4Key;
                break;
            case KeyCode.Keypad5:
                keyControl = keyboard.numpad5Key;
                break;
            case KeyCode.Keypad6:
                keyControl = keyboard.numpad6Key;
                break;
            case KeyCode.Keypad7:
                keyControl = keyboard.numpad7Key;
                break;
            case KeyCode.Keypad8:
                keyControl = keyboard.numpad8Key;
                break;
            case KeyCode.Keypad9:
                keyControl = keyboard.numpad9Key;
                break;
            case KeyCode.KeypadDivide:
                keyControl = keyboard.numpadDivideKey;
                break;
            case KeyCode.KeypadMultiply:
                keyControl = keyboard.numpadMultiplyKey;
                break;
            case KeyCode.KeypadMinus:
                keyControl = keyboard.numpadMinusKey;
                break;
            case KeyCode.KeypadPlus:
                keyControl = keyboard.numpadPlusKey;
                break;
            case KeyCode.KeypadPeriod:
                keyControl = keyboard.numpadPeriodKey;
                break;
            case KeyCode.KeypadEquals:
                keyControl = keyboard.numpadEqualsKey;
                break;
            default:
                return false;
        }

        return keyControl != null;
    }
#endif
}

