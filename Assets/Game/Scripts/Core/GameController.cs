using UnityEngine;
using Game.Core;
using Game.Systems.TimeSystem;

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

    private GameState gameState;
    private TimeSystem timeSystem;

    public GameState GameState => gameState;

    void Start()
    {
        gameState = new GameState();
        gameState.Initialize();
        timeSystem = gameState.GetSystem<TimeSystem>();

        // Apply inspector defaults
        timeSystem.SetGameSpeed(speedMultiplier);
        timeSystem.SetSecondsPerDay(secondsPerDay);
        if (paused) timeSystem.Pause();
    }

    void Update()
    {
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

    void OnDestroy() => gameState.Shutdown();

}

