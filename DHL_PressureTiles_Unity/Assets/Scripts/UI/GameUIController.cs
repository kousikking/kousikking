using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using DHL_PressureTiles.Game;
using DHL_PressureTiles.Config;

namespace DHL_PressureTiles.UI
{
    /// <summary>
    /// Controls all UI screens: Start, Registration, Gameplay, Final Score.
    /// Wire up in the Unity Inspector or use the SceneSetup script.
    /// </summary>
    public class GameUIController : MonoBehaviour
    {
        [Header("Screens")]
        public GameObject startScreen;
        public GameObject registrationScreen;
        public GameObject gameplayScreen;
        public GameObject gameOverScreen;
        public GameObject countdownOverlay;

        [Header("Start Screen")]
        public Button startButton;

        [Header("Registration")]
        public InputField nameInput;
        public InputField emailInput;
        public Button submitButton;
        public Button regBackButton;
        public Text nameErrorText;
        public Text emailErrorText;

        [Header("Gameplay")]
        public Text timerText;
        public Text scoreText;
        public Text streakText;
        public Text playerNameText;
        public Button gameBackButton;
        public Button gameHomeButton;
        public Text countdownText;

        [Header("Gameplay - Tile Grid")]
        public GridLayoutGroup tileGrid;
        public GameObject tilePrefab;

        [Header("Game Over")]
        public Text finalScoreText;
        public Text finalPlayerNameText;
        public Text correctStepsText;
        public Text durationText;
        public Button playAgainButton;
        public Button scoreBackButton;
        public Button scoreHomeButton;

        [Header("Config")]
        public TileConfig config;

        private GameObject[] tileObjects;
        private Image[] tileImages;

        private void Start()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // Subscribe to events
            gm.OnStateChanged += HandleStateChange;
            gm.OnTimerUpdated += HandleTimerUpdate;
            gm.OnScoreUpdated += HandleScoreUpdate;
            gm.OnStreakUpdated += HandleStreakUpdate;
            gm.OnTileLit += HandleTileLit;
            gm.OnTileCleared += HandleTileCleared;
            gm.OnTileHit += HandleTileHit;
            gm.OnGameOver += HandleGameOver;

            // Wire buttons
            startButton?.onClick.AddListener(() => gm.GoToRegistration());
            submitButton?.onClick.AddListener(OnSubmit);
            regBackButton?.onClick.AddListener(() => gm.GoToStart());
            gameBackButton?.onClick.AddListener(() =>
            {
                gm.GoToStart();
                gm.GoToRegistration();
            });
            gameHomeButton?.onClick.AddListener(() => gm.GoToStart());
            playAgainButton?.onClick.AddListener(() => gm.GoToStart());
            scoreBackButton?.onClick.AddListener(() => gm.GoToRegistration());
            scoreHomeButton?.onClick.AddListener(() => gm.GoToStart());

            // Build tile grid
            BuildTileGrid();

            // Show start screen
            HandleStateChange(GameState.Start);
        }

        private void OnDestroy()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            gm.OnStateChanged -= HandleStateChange;
            gm.OnTimerUpdated -= HandleTimerUpdate;
            gm.OnScoreUpdated -= HandleScoreUpdate;
            gm.OnStreakUpdated -= HandleStreakUpdate;
            gm.OnTileLit -= HandleTileLit;
            gm.OnTileCleared -= HandleTileCleared;
            gm.OnTileHit -= HandleTileHit;
            gm.OnGameOver -= HandleGameOver;
        }

        // ══════════════════════════════════════
        // SCREEN MANAGEMENT
        // ══════════════════════════════════════

        private void HandleStateChange(GameState state)
        {
            startScreen?.SetActive(state == GameState.Start);
            registrationScreen?.SetActive(state == GameState.Registration);
            gameplayScreen?.SetActive(state == GameState.Playing || state == GameState.Countdown);
            gameOverScreen?.SetActive(state == GameState.GameOver);

            if (state == GameState.Countdown)
            {
                countdownOverlay?.SetActive(true);
                StartCoroutine(ShowCountdown());
            }
            else
            {
                countdownOverlay?.SetActive(false);
            }
        }

        private IEnumerator ShowCountdown()
        {
            string[] nums = { "3", "2", "1", "GO!" };
            foreach (string n in nums)
            {
                if (countdownText != null)
                    countdownText.text = n;
                yield return new WaitForSeconds(0.8f);
            }
        }

        // ══════════════════════════════════════
        // REGISTRATION
        // ══════════════════════════════════════

        private void OnSubmit()
        {
            string name = nameInput?.text?.Trim() ?? "";
            string email = emailInput?.text?.Trim() ?? "";
            bool valid = true;

            if (nameErrorText != null) nameErrorText.gameObject.SetActive(false);
            if (emailErrorText != null) emailErrorText.gameObject.SetActive(false);

            if (string.IsNullOrEmpty(name))
            {
                if (nameErrorText != null) nameErrorText.gameObject.SetActive(true);
                valid = false;
            }

            if (string.IsNullOrEmpty(email) || !email.Contains("@") || !email.Contains("."))
            {
                if (emailErrorText != null) emailErrorText.gameObject.SetActive(true);
                valid = false;
            }

            if (!valid) return;

            GameManager.Instance?.SubmitRegistration(name, email);
        }

        // ══════════════════════════════════════
        // GAMEPLAY UI
        // ══════════════════════════════════════

        private void HandleTimerUpdate(float time)
        {
            if (timerText == null) return;
            int mins = Mathf.FloorToInt(time / 60);
            int secs = Mathf.FloorToInt(time % 60);
            timerText.text = $"{mins}:{secs:D2}";

            // Urgency color
            timerText.color = time <= 10 ? Color.red : Color.white;
        }

        private void HandleScoreUpdate(int newScore)
        {
            if (scoreText != null)
                scoreText.text = newScore.ToString();
        }

        private void HandleStreakUpdate(int newStreak)
        {
            if (streakText == null) return;
            if (newStreak >= 3)
            {
                float mult = config.GetStreakMultiplier(newStreak);
                streakText.text = $"{newStreak} STREAK! x{mult:F1}";
                streakText.gameObject.SetActive(true);
            }
            else
            {
                streakText.gameObject.SetActive(false);
            }
        }

        // ══════════════════════════════════════
        // TILE GRID
        // ══════════════════════════════════════

        private void BuildTileGrid()
        {
            if (tileGrid == null || tilePrefab == null) return;

            int total = config.TotalTiles;
            tileObjects = new GameObject[total];
            tileImages = new Image[total];

            // Configure grid layout
            tileGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            tileGrid.constraintCount = config.columns;

            for (int i = 0; i < total; i++)
            {
                GameObject tile = Instantiate(tilePrefab, tileGrid.transform);
                tile.name = $"Tile_{i}";
                tileObjects[i] = tile;
                tileImages[i] = tile.GetComponent<Image>();

                // Set off color
                if (tileImages[i] != null)
                    tileImages[i].color = new Color(0.15f, 0.15f, 0.15f, 0.7f);

                // Add click handler
                int index = i;
                Button btn = tile.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() => GameManager.Instance?.OnTilePressed(index));
                }
            }
        }

        private void HandleTileLit(int tileIndex, TileColorType colorType)
        {
            if (tileImages == null || tileIndex >= tileImages.Length) return;

            Color color;
            switch (colorType)
            {
                case TileColorType.Yellow: color = config.yellowTile; break;
                case TileColorType.Red: color = config.redTile; break;
                case TileColorType.White: color = config.whiteTile; break;
                default: color = config.offTile; break;
            }

            if (tileImages[tileIndex] != null)
                tileImages[tileIndex].color = color;
        }

        private void HandleTileCleared(int tileIndex)
        {
            if (tileImages == null || tileIndex >= tileImages.Length) return;
            if (tileImages[tileIndex] != null)
                tileImages[tileIndex].color = new Color(0.15f, 0.15f, 0.15f, 0.7f);
        }

        private void HandleTileHit(int tileIndex, int points, bool isCorrect)
        {
            // Flash effect on tile
            if (tileImages != null && tileIndex < tileImages.Length && tileImages[tileIndex] != null)
            {
                StartCoroutine(FlashTile(tileIndex, isCorrect));
            }
        }

        private IEnumerator FlashTile(int tileIndex, bool correct)
        {
            if (tileImages[tileIndex] == null) yield break;

            Color flashColor = correct ? Color.green : Color.red;
            tileImages[tileIndex].color = flashColor;
            yield return new WaitForSeconds(0.15f);
            tileImages[tileIndex].color = new Color(0.15f, 0.15f, 0.15f, 0.7f);
        }

        // ══════════════════════════════════════
        // GAME OVER
        // ══════════════════════════════════════

        private void HandleGameOver(GameResults results)
        {
            if (finalScoreText != null) finalScoreText.text = results.score.ToString();
            if (finalPlayerNameText != null) finalPlayerNameText.text = results.playerName;
            if (correctStepsText != null) correctStepsText.text = results.correctSteps.ToString();
            if (durationText != null) durationText.text = $"{results.duration:F0}s";

            // Auto-return after 15 seconds
            StartCoroutine(AutoReturn());
        }

        private IEnumerator AutoReturn()
        {
            yield return new WaitForSeconds(15f);
            if (GameManager.Instance?.CurrentState == GameState.GameOver)
            {
                GameManager.Instance.GoToStart();
            }
        }
    }
}
