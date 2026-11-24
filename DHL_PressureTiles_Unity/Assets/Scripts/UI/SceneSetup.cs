using UnityEngine;
using UnityEngine.UI;
using DHL_PressureTiles.Config;
using DHL_PressureTiles.ArtNet;
using DHL_PressureTiles.Game;

namespace DHL_PressureTiles.UI
{
    /// <summary>
    /// Auto-builds the entire scene hierarchy at runtime.
    /// Add this to an empty GameObject named "SceneSetup" and assign a TileConfig asset.
    /// It creates Canvas, EventSystem, all managers, screens, and UI elements.
    /// </summary>
    public class SceneSetup : MonoBehaviour
    {
        public TileConfig config;

        private Canvas mainCanvas;
        private GameObject canvasObj;

        // DHL brand colors
        private static readonly Color DHL_YELLOW = new Color(1f, 0.8f, 0f);           // #FFCC00
        private static readonly Color DHL_RED = new Color(0.831f, 0.02f, 0.067f);      // #D40511
        private static readonly Color DHL_PINK = new Color(0.91f, 0.086f, 0.302f);     // #E8164D
        private static readonly Color BG_DARK = new Color(0.08f, 0.08f, 0.1f);         // Dark background
        private static readonly Color PANEL_BG = new Color(0.1f, 0.1f, 0.12f, 0.95f);

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogError("[SceneSetup] TileConfig not assigned!");
                return;
            }

            CreateEventSystem();
            CreateCanvas();
            CreateManagers();
            CreateScreens();

            Debug.Log("[SceneSetup] Scene setup complete. Press F12 to open Debug Panel.");
        }

        private void CreateEventSystem()
        {
            var es = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            if (es != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        private void CreateCanvas()
        {
            canvasObj = new GameObject("MainCanvas");
            mainCanvas = canvasObj.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            mainCanvas.sortingOrder = 0;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // Full-screen background
            var bgObj = CreatePanel(canvasObj.transform, "Background", BG_DARK);
            var bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
        }

        private void CreateManagers()
        {
            // ArtNet Controller
            var artNetObj = new GameObject("ArtNetController");
            var artNet = artNetObj.AddComponent<ArtNetController>();
            artNet.config = config;

            // Game Manager
            var gmObj = new GameObject("GameManager");
            var gm = gmObj.AddComponent<GameManager>();
            gm.config = config;

            // Debug Panel
            var debugObj = new GameObject("DebugPanel");
            var debug = debugObj.AddComponent<DebugPanel>();
            debug.config = config;
        }

        private void CreateScreens()
        {
            var uiCtrl = canvasObj.AddComponent<GameUIController>();
            uiCtrl.config = config;

            // ── START SCREEN ──
            var startScreen = CreateScreen("StartScreen");
            uiCtrl.startScreen = startScreen;

            // Title
            CreateText(startScreen.transform, "Title", "DANCE WITH\nDHL EXPRESS", 72,
                DHL_YELLOW, TextAnchor.MiddleCenter, new Vector2(0, 100), new Vector2(800, 200));

            // Subtitle
            CreateText(startScreen.transform, "Subtitle", "PRESSURE TILES CHALLENGE", 28,
                Color.white, TextAnchor.MiddleCenter, new Vector2(0, -20), new Vector2(600, 50));

            // F1 tagline
            CreateText(startScreen.transform, "F1Tag", "DHL × FORMULA 1", 20,
                DHL_RED, TextAnchor.MiddleCenter, new Vector2(0, -60), new Vector2(400, 40));

            // Start button
            var startBtn = CreateButton(startScreen.transform, "StartButton", "TAP TO START",
                DHL_RED, Color.white, new Vector2(0, -180), new Vector2(320, 70), 28);
            uiCtrl.startButton = startBtn;

            // ── REGISTRATION SCREEN ──
            var regScreen = CreateScreen("RegistrationScreen");
            regScreen.SetActive(false);
            uiCtrl.registrationScreen = regScreen;

            CreateText(regScreen.transform, "RegTitle", "REGISTER", 48,
                DHL_YELLOW, TextAnchor.MiddleCenter, new Vector2(0, 200), new Vector2(600, 80));

            // Name input
            CreateText(regScreen.transform, "NameLabel", "YOUR NAME", 18,
                Color.white, TextAnchor.MiddleLeft, new Vector2(-140, 100), new Vector2(300, 30));
            var nameInput = CreateInputField(regScreen.transform, "NameInput",
                "Enter your name...", new Vector2(0, 60), new Vector2(400, 50));
            uiCtrl.nameInput = nameInput;

            var nameErr = CreateText(regScreen.transform, "NameError", "Name is required", 14,
                DHL_RED, TextAnchor.MiddleLeft, new Vector2(-140, 28), new Vector2(300, 25));
            nameErr.gameObject.SetActive(false);
            uiCtrl.nameErrorText = nameErr;

            // Email input
            CreateText(regScreen.transform, "EmailLabel", "YOUR EMAIL", 18,
                Color.white, TextAnchor.MiddleLeft, new Vector2(-140, -10), new Vector2(300, 30));
            var emailInput = CreateInputField(regScreen.transform, "EmailInput",
                "Enter your email...", new Vector2(0, -50), new Vector2(400, 50));
            uiCtrl.emailInput = emailInput;

            var emailErr = CreateText(regScreen.transform, "EmailError", "Valid email required", 14,
                DHL_RED, TextAnchor.MiddleLeft, new Vector2(-140, -82), new Vector2(300, 25));
            emailErr.gameObject.SetActive(false);
            uiCtrl.emailErrorText = emailErr;

            // Submit button
            var submitBtn = CreateButton(regScreen.transform, "SubmitButton", "LET'S GO!",
                DHL_RED, Color.white, new Vector2(0, -150), new Vector2(280, 60), 24);
            uiCtrl.submitButton = submitBtn;

            // Back button
            var regBack = CreateNavButton(regScreen.transform, "BackBtn", "←", new Vector2(-880, 460));
            uiCtrl.regBackButton = regBack;

            // ── GAMEPLAY SCREEN ──
            var gameScreen = CreateScreen("GameplayScreen");
            gameScreen.SetActive(false);
            uiCtrl.gameplayScreen = gameScreen;

            // Top HUD
            var timerTxt = CreateText(gameScreen.transform, "Timer", "1:00", 48,
                Color.white, TextAnchor.MiddleCenter, new Vector2(0, 470), new Vector2(200, 60));
            uiCtrl.timerText = timerTxt;

            var scoreTxt = CreateText(gameScreen.transform, "Score", "0", 36,
                DHL_YELLOW, TextAnchor.MiddleRight, new Vector2(400, 470), new Vector2(200, 50));
            uiCtrl.scoreText = scoreTxt;

            CreateText(gameScreen.transform, "ScoreLabel", "SCORE", 16,
                Color.gray, TextAnchor.MiddleRight, new Vector2(400, 440), new Vector2(200, 30));

            var playerTxt = CreateText(gameScreen.transform, "PlayerName", "", 20,
                Color.white, TextAnchor.MiddleLeft, new Vector2(-400, 470), new Vector2(300, 40));
            uiCtrl.playerNameText = playerTxt;

            var streakTxt = CreateText(gameScreen.transform, "Streak", "", 24,
                DHL_PINK, TextAnchor.MiddleCenter, new Vector2(0, 430), new Vector2(400, 40));
            streakTxt.gameObject.SetActive(false);
            uiCtrl.streakText = streakTxt;

            // Tile grid
            var gridObj = new GameObject("TileGrid");
            gridObj.transform.SetParent(gameScreen.transform, false);
            var gridRect = gridObj.AddComponent<RectTransform>();
            gridRect.anchoredPosition = new Vector2(0, -30);
            gridRect.sizeDelta = new Vector2(900, 800);

            var grid = gridObj.AddComponent<GridLayoutGroup>();
            float cellSize = Mathf.Min(900f / config.columns, 800f / config.rows) - 6;
            grid.cellSize = new Vector2(cellSize, cellSize);
            grid.spacing = new Vector2(4, 4);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = config.columns;
            uiCtrl.tileGrid = grid;

            // Tile prefab (created once, instantiated by GameUIController)
            var tilePrefab = new GameObject("TilePrefab");
            tilePrefab.SetActive(false);
            var tileImg = tilePrefab.AddComponent<Image>();
            tileImg.color = new Color(0.15f, 0.15f, 0.15f, 0.7f);

            // Dark border via Outline
            var outline = tilePrefab.AddComponent<Outline>();
            outline.effectColor = new Color(0.23f, 0.23f, 0.23f, 0.9f);
            outline.effectDistance = new Vector2(3, -3);

            tilePrefab.AddComponent<Button>();
            uiCtrl.tilePrefab = tilePrefab;

            // Nav buttons
            var gameBack = CreateNavButton(gameScreen.transform, "GameBackBtn", "←", new Vector2(-880, 460));
            uiCtrl.gameBackButton = gameBack;
            var gameHome = CreateNavButton(gameScreen.transform, "GameHomeBtn", "⌂", new Vector2(-820, 460));
            uiCtrl.gameHomeButton = gameHome;

            // Countdown overlay
            var countdownObj = CreatePanel(gameScreen.transform, "CountdownOverlay",
                new Color(0, 0, 0, 0.7f));
            var cdRect = countdownObj.GetComponent<RectTransform>();
            cdRect.anchorMin = Vector2.zero;
            cdRect.anchorMax = Vector2.one;
            cdRect.sizeDelta = Vector2.zero;
            countdownObj.SetActive(false);
            uiCtrl.countdownOverlay = countdownObj;

            var cdText = CreateText(countdownObj.transform, "CountdownText", "3", 120,
                DHL_YELLOW, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(300, 200));
            uiCtrl.countdownText = cdText;

            // ── GAME OVER SCREEN ──
            var overScreen = CreateScreen("GameOverScreen");
            overScreen.SetActive(false);
            uiCtrl.gameOverScreen = overScreen;

            CreateText(overScreen.transform, "GOTitle", "GREAT JOB!", 56,
                DHL_YELLOW, TextAnchor.MiddleCenter, new Vector2(0, 250), new Vector2(600, 80));

            var finalName = CreateText(overScreen.transform, "FinalName", "", 24,
                Color.white, TextAnchor.MiddleCenter, new Vector2(0, 180), new Vector2(400, 40));
            uiCtrl.finalPlayerNameText = finalName;

            // Score display
            CreateText(overScreen.transform, "FinalScoreLabel", "YOUR SCORE", 20,
                Color.gray, TextAnchor.MiddleCenter, new Vector2(0, 120), new Vector2(300, 30));

            var finalScore = CreateText(overScreen.transform, "FinalScore", "0", 72,
                DHL_YELLOW, TextAnchor.MiddleCenter, new Vector2(0, 60), new Vector2(400, 100));
            uiCtrl.finalScoreText = finalScore;

            // Stats
            CreateText(overScreen.transform, "StepsLabel", "CORRECT STEPS", 18,
                Color.gray, TextAnchor.MiddleCenter, new Vector2(-120, -30), new Vector2(200, 30));
            var stepsTxt = CreateText(overScreen.transform, "StepsValue", "0", 36,
                Color.white, TextAnchor.MiddleCenter, new Vector2(-120, -70), new Vector2(200, 50));
            uiCtrl.correctStepsText = stepsTxt;

            CreateText(overScreen.transform, "DurLabel", "DURATION", 18,
                Color.gray, TextAnchor.MiddleCenter, new Vector2(120, -30), new Vector2(200, 30));
            var durTxt = CreateText(overScreen.transform, "DurValue", "0s", 36,
                Color.white, TextAnchor.MiddleCenter, new Vector2(120, -70), new Vector2(200, 50));
            uiCtrl.durationText = durTxt;

            // Play again button
            var playAgain = CreateButton(overScreen.transform, "PlayAgainBtn", "PLAY AGAIN",
                DHL_RED, Color.white, new Vector2(0, -180), new Vector2(280, 60), 24);
            uiCtrl.playAgainButton = playAgain;

            // Nav buttons
            var scoreBack = CreateNavButton(overScreen.transform, "ScoreBackBtn", "←", new Vector2(-880, 460));
            uiCtrl.scoreBackButton = scoreBack;
            var scoreHome = CreateNavButton(overScreen.transform, "ScoreHomeBtn", "⌂", new Vector2(-820, 460));
            uiCtrl.scoreHomeButton = scoreHome;
        }

        // ══════════════════════════════════════
        // UI FACTORY HELPERS
        // ══════════════════════════════════════

        private GameObject CreateScreen(string name)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(canvasObj.transform, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            return obj;
        }

        private GameObject CreatePanel(Transform parent, string name, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var img = obj.AddComponent<Image>();
            img.color = color;
            return obj;
        }

        private Text CreateText(Transform parent, string name, string text, int fontSize,
            Color color, TextAnchor alignment, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            var rect = obj.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var txt = obj.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = alignment;
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;

            return txt;
        }

        private Button CreateButton(Transform parent, string name, string label,
            Color bgColor, Color textColor, Vector2 position, Vector2 size, int fontSize)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            var rect = obj.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var img = obj.AddComponent<Image>();
            img.color = bgColor;

            var btn = obj.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(bgColor.r * 1.2f, bgColor.g * 1.2f, bgColor.b * 1.2f);
            colors.pressedColor = new Color(bgColor.r * 0.8f, bgColor.g * 0.8f, bgColor.b * 0.8f);
            btn.colors = colors;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(obj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            var txt = textObj.AddComponent<Text>();
            txt.text = label;
            txt.fontSize = fontSize;
            txt.color = textColor;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            txt.fontStyle = FontStyle.Bold;

            return btn;
        }

        private InputField CreateInputField(Transform parent, string name,
            string placeholder, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            var rect = obj.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var img = obj.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.22f, 1f);

            // Text child
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(obj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.offsetMin = new Vector2(10, 2);
            textRect.offsetMax = new Vector2(-10, -2);

            var txt = textObj.AddComponent<Text>();
            txt.fontSize = 20;
            txt.color = Color.white;
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", 20);
            txt.supportRichText = false;

            // Placeholder child
            var phObj = new GameObject("Placeholder");
            phObj.transform.SetParent(obj.transform, false);
            var phRect = phObj.AddComponent<RectTransform>();
            phRect.anchorMin = new Vector2(0, 0);
            phRect.anchorMax = new Vector2(1, 1);
            phRect.offsetMin = new Vector2(10, 2);
            phRect.offsetMax = new Vector2(-10, -2);

            var phTxt = phObj.AddComponent<Text>();
            phTxt.text = placeholder;
            phTxt.fontSize = 20;
            phTxt.color = new Color(0.5f, 0.5f, 0.5f);
            phTxt.font = Font.CreateDynamicFontFromOSFont("Arial", 20);
            phTxt.fontStyle = FontStyle.Italic;

            var input = obj.AddComponent<InputField>();
            input.textComponent = txt;
            input.placeholder = phTxt;

            return input;
        }

        private Button CreateNavButton(Transform parent, string name, string icon, Vector2 position)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            var rect = obj.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(48, 48);

            var img = obj.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.22f, 0.8f);

            var btn = obj.AddComponent<Button>();

            var textObj = new GameObject("Icon");
            textObj.transform.SetParent(obj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            var txt = textObj.AddComponent<Text>();
            txt.text = icon;
            txt.fontSize = 24;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", 24);

            return btn;
        }
    }
}
