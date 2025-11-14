using System;
using UnityEngine;
using Game.Core;
using Game.Systems.Time;
using System.Collections.Generic;
using System.Reflection;

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
    private static readonly Type KeyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
    private static readonly Type KeyEnumType = Type.GetType("UnityEngine.InputSystem.Key, Unity.InputSystem");
    private static readonly Type KeyControlType = Type.GetType("UnityEngine.InputSystem.Controls.KeyControl, Unity.InputSystem");
    private static readonly PropertyInfo KeyboardCurrentProperty = KeyboardType?.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
    private static readonly PropertyInfo KeyboardItemProperty = KeyboardType?.GetProperty("Item", BindingFlags.Public | BindingFlags.Instance, null, KeyControlType, new[] { KeyEnumType }, null);
    private static readonly PropertyInfo KeyWasPressedThisFrameProperty = KeyControlType?.GetProperty("wasPressedThisFrame", BindingFlags.Public | BindingFlags.Instance);
    private static readonly Dictionary<KeyCode, string> KeyboardPropertyMap = new Dictionary<KeyCode, string>
    {
        { KeyCode.Return, "enterKey" },
        { KeyCode.KeypadEnter, "numpadEnterKey" },
        { KeyCode.LeftArrow, "leftArrowKey" },
        { KeyCode.RightArrow, "rightArrowKey" },
        { KeyCode.UpArrow, "upArrowKey" },
        { KeyCode.DownArrow, "downArrowKey" },
        { KeyCode.LeftControl, "leftCtrlKey" },
        { KeyCode.RightControl, "rightCtrlKey" },
        { KeyCode.LeftShift, "leftShiftKey" },
        { KeyCode.RightShift, "rightShiftKey" },
        { KeyCode.LeftAlt, "leftAltKey" },
        { KeyCode.RightAlt, "rightAltKey" },
        { KeyCode.Space, "spaceKey" },
        { KeyCode.Alpha0, "digit0Key" },
        { KeyCode.Alpha1, "digit1Key" },
        { KeyCode.Alpha2, "digit2Key" },
        { KeyCode.Alpha3, "digit3Key" },
        { KeyCode.Alpha4, "digit4Key" },
        { KeyCode.Alpha5, "digit5Key" },
        { KeyCode.Alpha6, "digit6Key" },
        { KeyCode.Alpha7, "digit7Key" },
        { KeyCode.Alpha8, "digit8Key" },
        { KeyCode.Alpha9, "digit9Key" },
        { KeyCode.Backspace, "backspaceKey" },
        { KeyCode.Delete, "deleteKey" },
        { KeyCode.Insert, "insertKey" },
        { KeyCode.Tab, "tabKey" },
        { KeyCode.Escape, "escapeKey" },
        { KeyCode.CapsLock, "capsLockKey" },
        { KeyCode.Numlock, "numLockKey" },
        { KeyCode.ScrollLock, "scrollLockKey" },
        { KeyCode.Print, "printScreenKey" },
        { KeyCode.Pause, "pauseKey" },
        { KeyCode.Home, "homeKey" },
        { KeyCode.End, "endKey" },
        { KeyCode.PageUp, "pageUpKey" },
        { KeyCode.PageDown, "pageDownKey" },
        { KeyCode.BackQuote, "backquoteKey" },
        { KeyCode.Minus, "minusKey" },
        { KeyCode.Equals, "equalsKey" },
        { KeyCode.LeftBracket, "leftBracketKey" },
        { KeyCode.RightBracket, "rightBracketKey" },
        { KeyCode.Semicolon, "semicolonKey" },
        { KeyCode.Quote, "quoteKey" },
        { KeyCode.Comma, "commaKey" },
        { KeyCode.Period, "periodKey" },
        { KeyCode.Slash, "slashKey" },
        { KeyCode.Backslash, "backslashKey" },
        { KeyCode.LeftWindows, "leftWindowsKey" },
        { KeyCode.LeftCommand, "leftWindowsKey" },
        { KeyCode.LeftApple, "leftWindowsKey" },
        { KeyCode.RightWindows, "rightWindowsKey" },
        { KeyCode.RightCommand, "rightWindowsKey" },
        { KeyCode.RightApple, "rightWindowsKey" },
        { KeyCode.Menu, "contextMenuKey" },
        { KeyCode.Keypad0, "numpad0Key" },
        { KeyCode.Keypad1, "numpad1Key" },
        { KeyCode.Keypad2, "numpad2Key" },
        { KeyCode.Keypad3, "numpad3Key" },
        { KeyCode.Keypad4, "numpad4Key" },
        { KeyCode.Keypad5, "numpad5Key" },
        { KeyCode.Keypad6, "numpad6Key" },
        { KeyCode.Keypad7, "numpad7Key" },
        { KeyCode.Keypad8, "numpad8Key" },
        { KeyCode.Keypad9, "numpad9Key" },
        { KeyCode.KeypadDivide, "numpadDivideKey" },
        { KeyCode.KeypadMultiply, "numpadMultiplyKey" },
        { KeyCode.KeypadMinus, "numpadMinusKey" },
        { KeyCode.KeypadPlus, "numpadPlusKey" },
        { KeyCode.KeypadPeriod, "numpadPeriodKey" },
        { KeyCode.KeypadEquals, "numpadEqualsKey" }
    };

    private bool WasKeyPressedThisFrame(KeyCode keyCode)
    {
        if (keyCode == KeyCode.None)
            return false;

        if (KeyboardType == null || KeyControlType == null || KeyEnumType == null)
            return Input.GetKeyDown(keyCode);

        var keyboard = KeyboardCurrentProperty?.GetValue(null);
        if (keyboard == null)
            return false;

        if (!TryGetKeyControl(keyboard, keyCode, out var keyControl))
            return false;

        var wasPressed = KeyWasPressedThisFrameProperty?.GetValue(keyControl);
        return wasPressed is bool pressed && pressed;
    }

    private bool TryGetKeyControl(object keyboard, KeyCode keyCode, out object keyControl)
    {
        keyControl = null;
        if (keyboard == null)
            return false;

        if (KeyboardItemProperty != null)
        {
            try
            {
                var parsedKey = Enum.Parse(KeyEnumType, keyCode.ToString());
                keyControl = KeyboardItemProperty.GetValue(keyboard, new[] { parsedKey });
                if (keyControl != null)
                    return true;
            }
            catch
            {
                // Ignored - will attempt mapped properties instead.
            }
        }

        if (!KeyboardPropertyMap.TryGetValue(keyCode, out var propertyName))
            return false;

        var property = KeyboardType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        keyControl = property?.GetValue(keyboard);
        return keyControl != null;
    }
#endif
}

