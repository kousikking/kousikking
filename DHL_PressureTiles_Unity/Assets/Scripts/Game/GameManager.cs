using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using DHL_PressureTiles.Config;
using DHL_PressureTiles.ArtNet;

namespace DHL_PressureTiles.Game
{
    /// <summary>
    /// Core game manager: handles game state, scoring, tile spawning,
    /// and communication with ArtNet controller.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Configuration")]
        public TileConfig config;

        [Header("State")]
        [SerializeField] private GameState currentState = GameState.Start;
        [SerializeField] private float timeRemaining = 60f;
        [SerializeField] private int score = 0;
        [SerializeField] private int streak = 0;
        [SerializeField] private int correctSteps = 0;
        [SerializeField] private int totalAttempts = 0;

        [Header("Player")]
        [SerializeField] private string playerName = "";
        [SerializeField] private string playerEmail = "";

        // Active tiles
        private Dictionary<int, LitTileInfo> litTiles = new Dictionary<int, LitTileInfo>();
        private float nextSpawnTime = 0f;

        // Events for UI
        public event Action<GameState> OnStateChanged;
        public event Action<float> OnTimerUpdated;
        public event Action<int> OnScoreUpdated;
        public event Action<int> OnStreakUpdated;
        public event Action<int, TileColorType> OnTileLit;
        public event Action<int> OnTileCleared;
        public event Action<int, int, bool> OnTileHit; // tileIndex, points, isCorrect
        public event Action<GameResults> OnGameOver;

        // Singleton
        public static GameManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Listen for hardware tile presses
            if (ArtNetController.Instance != null)
            {
                ArtNetController.Instance.OnTilePressed += HandleHardwareTilePress;
            }
        }

        private void OnDestroy()
        {
            if (ArtNetController.Instance != null)
            {
                ArtNetController.Instance.OnTilePressed -= HandleHardwareTilePress;
            }
        }

        private void Update()
        {
            if (currentState != GameState.Playing) return;

            // Update timer
            timeRemaining -= Time.deltaTime;
            OnTimerUpdated?.Invoke(timeRemaining);

            if (timeRemaining <= 0)
            {
                timeRemaining = 0;
                EndGame();
                return;
            }

            // Spawn tiles
            if (Time.time >= nextSpawnTime)
            {
                SpawnTiles();
                ScheduleNextSpawn();
            }

            // Check for expired tiles
            CheckExpiredTiles();
        }

        // ══════════════════════════════════════
        // STATE MANAGEMENT
        // ══════════════════════════════════════

        public void SetState(GameState newState)
        {
            currentState = newState;
            OnStateChanged?.Invoke(newState);
        }

        public GameState CurrentState => currentState;

        /// <summary>
        /// Go to registration screen.
        /// </summary>
        public void GoToRegistration()
        {
            SetState(GameState.Registration);
        }

        /// <summary>
        /// Go back to start screen.
        /// </summary>
        public void GoToStart()
        {
            if (currentState == GameState.Playing)
                StopGame();
            SetState(GameState.Start);
        }

        /// <summary>
        /// Submit registration and start countdown.
        /// </summary>
        public void SubmitRegistration(string name, string email)
        {
            playerName = name;
            playerEmail = email;
            StartCoroutine(CountdownSequence());
        }

        /// <summary>
        /// 3-2-1-GO countdown then start game.
        /// </summary>
        private IEnumerator CountdownSequence()
        {
            SetState(GameState.Countdown);
            yield return new WaitForSeconds(0.8f); // "3"
            yield return new WaitForSeconds(0.8f); // "2"
            yield return new WaitForSeconds(0.8f); // "1"
            yield return new WaitForSeconds(0.8f); // "GO!"
            StartGame();
        }

        // ══════════════════════════════════════
        // GAME LOGIC
        // ══════════════════════════════════════

        private void StartGame()
        {
            score = 0;
            streak = 0;
            correctSteps = 0;
            totalAttempts = 0;
            timeRemaining = config.gameDuration;
            litTiles.Clear();

            // Clear all hardware tiles
            ArtNetController.Instance?.ClearAllTiles();

            SetState(GameState.Playing);
            OnScoreUpdated?.Invoke(score);
            OnStreakUpdated?.Invoke(streak);
            OnTimerUpdated?.Invoke(timeRemaining);

            // Initial tile spawn
            SpawnTiles();
            ScheduleNextSpawn();
        }

        private void StopGame()
        {
            // Clear tiles
            foreach (var kvp in litTiles)
            {
                OnTileCleared?.Invoke(kvp.Key);
            }
            litTiles.Clear();
            ArtNetController.Instance?.ClearAllTiles();
        }

        private void EndGame()
        {
            StopGame();

            var results = new GameResults
            {
                playerName = playerName,
                playerEmail = playerEmail,
                score = score,
                correctSteps = correctSteps,
                totalAttempts = totalAttempts,
                duration = config.gameDuration
            };

            SetState(GameState.GameOver);
            OnGameOver?.Invoke(results);
        }

        /// <summary>
        /// Handle a tile being pressed (from UI touch or hardware).
        /// </summary>
        public void OnTilePressed(int tileIndex)
        {
            if (currentState != GameState.Playing) return;

            totalAttempts++;

            if (litTiles.TryGetValue(tileIndex, out LitTileInfo info))
            {
                // Correct hit!
                float reactionMs = (Time.time - info.litTime) * 1000f;
                int basePoints = config.GetPointsForColor(info.colorType);
                float speedMult = config.GetSpeedMultiplier(reactionMs);

                streak++;
                correctSteps++;
                float streakMult = config.GetStreakMultiplier(streak);

                int points = Mathf.RoundToInt(basePoints * speedMult * streakMult);
                score += points;

                // Clear this tile
                litTiles.Remove(tileIndex);
                ArtNetController.Instance?.ClearTile(tileIndex);
                OnTileCleared?.Invoke(tileIndex);

                OnScoreUpdated?.Invoke(score);
                OnStreakUpdated?.Invoke(streak);
                OnTileHit?.Invoke(tileIndex, points, true);
            }
            else
            {
                // Wrong tile
                score = Mathf.Max(0, score + config.wrongTilePenalty);
                streak = 0;

                OnScoreUpdated?.Invoke(score);
                OnStreakUpdated?.Invoke(streak);
                OnTileHit?.Invoke(tileIndex, config.wrongTilePenalty, false);
            }
        }

        // ══════════════════════════════════════
        // TILE SPAWNING
        // ══════════════════════════════════════

        private void SpawnTiles()
        {
            float elapsed = config.gameDuration - timeRemaining;
            float progress = elapsed / config.gameDuration;
            int targetLit = Mathf.RoundToInt(Mathf.Lerp(config.maxLitTilesStart, config.maxLitTilesEnd, progress));
            int desired = Mathf.Max(config.minLitTiles, targetLit);

            int attempts = 0;
            while (litTiles.Count < desired && attempts < 20)
            {
                SpawnSingleTile();
                attempts++;
            }
        }

        private void SpawnSingleTile()
        {
            // Find an unlit tile
            List<int> available = new List<int>();
            for (int i = 0; i < config.TotalTiles; i++)
            {
                if (!litTiles.ContainsKey(i))
                    available.Add(i);
            }

            if (available.Count == 0) return;

            int tileIndex = available[UnityEngine.Random.Range(0, available.Count)];

            // Pick color
            TileColorType[] colors = { TileColorType.Yellow, TileColorType.Red, TileColorType.White };
            TileColorType color = colors[UnityEngine.Random.Range(0, colors.Length)];

            // Light it up
            litTiles[tileIndex] = new LitTileInfo
            {
                colorType = color,
                litTime = Time.time
            };

            // Send to hardware
            ArtNetController.Instance?.SetTileColor(tileIndex, color);

            // Notify UI
            OnTileLit?.Invoke(tileIndex, color);
        }

        private void CheckExpiredTiles()
        {
            List<int> expired = new List<int>();
            float now = Time.time;

            foreach (var kvp in litTiles)
            {
                if (now - kvp.Value.litTime >= config.tileLifetime)
                {
                    expired.Add(kvp.Key);
                }
            }

            foreach (int idx in expired)
            {
                litTiles.Remove(idx);
                totalAttempts++; // Missed tile counts as attempt
                streak = 0;

                ArtNetController.Instance?.ClearTile(idx);
                OnTileCleared?.Invoke(idx);
                OnStreakUpdated?.Invoke(streak);
            }
        }

        private void ScheduleNextSpawn()
        {
            float elapsed = config.gameDuration - timeRemaining;
            float progress = elapsed / config.gameDuration;
            float interval = Mathf.Lerp(0.8f, 0.4f, progress);
            nextSpawnTime = Time.time + interval;
        }

        // ══════════════════════════════════════
        // HARDWARE INPUT
        // ══════════════════════════════════════

        private void HandleHardwareTilePress(TilePressEvent evt)
        {
            if (evt.pressed)
            {
                OnTilePressed(evt.tileIndex);
            }
        }

        // ══════════════════════════════════════
        // PUBLIC GETTERS
        // ══════════════════════════════════════

        public string PlayerName => playerName;
        public int Score => score;
        public int Streak => streak;
        public int CorrectSteps => correctSteps;
        public float TimeRemaining => timeRemaining;
    }

    public enum GameState
    {
        Start,
        Registration,
        Countdown,
        Playing,
        GameOver
    }

    [Serializable]
    public struct LitTileInfo
    {
        public TileColorType colorType;
        public float litTime;
    }

    [Serializable]
    public struct GameResults
    {
        public string playerName;
        public string playerEmail;
        public int score;
        public int correctSteps;
        public int totalAttempts;
        public float duration;
    }
}
