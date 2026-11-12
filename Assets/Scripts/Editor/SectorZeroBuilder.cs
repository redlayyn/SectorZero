using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Автосборщик сцен. Файл ДОЛЖЕН лежать в папке с именем Editor (например Assets/Scripts/Editor).
// После компиляции в верхнем меню Unity появится пункт "Sector Zero".
//
// Сборщик строит объекты в ТЕКУЩЕЙ открытой сцене: создаёт заглушки спрайтов и префабы,
// корабль, фон, HUD, панели, менеджеры и связывает все поля скриптов.
// Готовые префабы и спрайты он не перезаписывает, поэтому твои замены картинок не пропадут.
public static class SectorZeroBuilder
{
    const string SpritesFolder = "Assets/Sprites/Placeholders";
    const string PrefabsFolder = "Assets/Prefabs";

    // Якоря: точка привязки элемента к экрану (она же его pivot)
    static readonly Vector2 TL = new Vector2(0f, 1f);
    static readonly Vector2 TC = new Vector2(0.5f, 1f);
    static readonly Vector2 TR = new Vector2(1f, 1f);
    static readonly Vector2 BL = new Vector2(0f, 0f);
    static readonly Vector2 BC = new Vector2(0.5f, 0f);
    static readonly Vector2 BR = new Vector2(1f, 0f);
    static readonly Vector2 CC = new Vector2(0.5f, 0.5f);

    static readonly Color DarkText = new Color(0.05f, 0.05f, 0.15f, 1f);
    static readonly Color PanelColor = new Color(0.05f, 0.07f, 0.15f, 0.95f);
    static readonly Color ButtonColor = new Color(0.8f, 0.9f, 1f, 1f);
    static readonly Color32 ClearPixel = new Color32(0, 0, 0, 0);

    // =====================================================================
    //  Пункты меню
    // =====================================================================

    [MenuItem("Sector Zero/1. Build Game Scene")]
    static void BuildGameScene()
    {
        if (Object.FindFirstObjectByType<GameManager>() != null)
        {
            EditorUtility.DisplayDialog("Sector Zero",
                "В этой сцене уже есть GameManager. Открой пустую сцену Game или удали старые объекты.", "OK");
            return;
        }

        EnsureFolders();
        SetupCamera();
        EnsureEventSystem();

        // ---------- Спрайты-заглушки ----------
        Sprite shipSprite = GetOrCreateSprite("Ship", 96, 96, 96f,
            (x, y, w, h) => Triangle(x, y, w, h, new Color32(80, 200, 255, 255)));
        Sprite laserSprite = GetOrCreateSprite("Laser", 12, 48, 96f,
            (x, y, w, h) => new Color32(255, 230, 80, 255));
        Sprite asteroidSprite = GetOrCreateSprite("Asteroid", 96, 96, 96f,
            (x, y, w, h) => Ellipse(x, y, w, h, new Color32(150, 130, 110, 255)));
        Sprite bossSprite = GetOrCreateSprite("Boss", 288, 192, 96f,
            (x, y, w, h) => Ellipse(x, y, w, h, new Color32(200, 60, 120, 255)));
        Sprite droneSprite = GetOrCreateSprite("Drone", 40, 40, 96f,
            (x, y, w, h) => Ellipse(x, y, w, h, new Color32(90, 230, 120, 255)));
        Sprite starfield = GetOrCreateStarfield();

        // ---------- Файл-слоты: сюда ты потом перетащишь свои спрайты ----------
        if (AssetDatabase.LoadAssetAtPath<SectorZeroSprites>(SlotsPath) == null)
        {
            SectorZeroSprites slots = ScriptableObject.CreateInstance<SectorZeroSprites>();
            slots.ship = shipSprite;
            slots.laser = laserSprite;
            slots.asteroid = asteroidSprite;
            slots.boss = bossSprite;
            slots.drone = droneSprite;
            slots.background = starfield;
            AssetDatabase.CreateAsset(slots, SlotsPath);
        }

        // ---------- Префабы ----------
        GameObject laserPrefab = GetOrCreatePrefab("Laser", () => BuildLaserObject(laserSprite));
        GameObject asteroidPrefab = GetOrCreatePrefab("Asteroid", () => BuildAsteroidObject(asteroidSprite));
        GameObject bossPrefab = GetOrCreatePrefab("Boss", () => BuildBossObject(bossSprite));
        GameObject dronePrefab = GetOrCreatePrefab("Drone", () => BuildDroneObject(droneSprite));

        // ---------- Объекты сцены ----------
        CreateBackground(starfield, 1f);
        CreateAudioManager();

        // Корабль
        GameObject player = new GameObject("Player");
        player.tag = "Player";
        player.transform.position = new Vector3(0f, -4f, 0f);
        AddSprite(player, shipSprite, 5);
        MakeKinematic(player);
        BoxCollider2D playerCol = player.AddComponent<BoxCollider2D>();
        playerCol.isTrigger = true;
        playerCol.size = shipSprite.bounds.size * 0.8f;

        GameObject firePoint = new GameObject("FirePoint");
        firePoint.transform.SetParent(player.transform, false);
        firePoint.transform.localPosition = new Vector3(0f, shipSprite.bounds.extents.y, 0f);

        PlayerController playerController = player.AddComponent<PlayerController>();
        playerController.laserPrefab = laserPrefab;
        playerController.firePoint = firePoint.transform;

        PlayerUpgrades upgrades = player.AddComponent<PlayerUpgrades>();
        upgrades.laserPrefab = laserPrefab;
        upgrades.firePoint = firePoint.transform;
        upgrades.dronePrefab = dronePrefab;

        // ---------- UI ----------
        Canvas canvas = CreateCanvas("Canvas");
        Transform c = canvas.transform;

        // HUD: кредиты, рекорд, волна, жизни
        TextMeshProUGUI creditsText = CreateText(c, "CreditsText", "Credits: 0", 40f, TextAlignmentOptions.Left, Color.white);
        Place(creditsText.rectTransform, TL, new Vector2(30f, -20f), new Vector2(500f, 60f));

        TextMeshProUGUI bestText = CreateText(c, "BestText", "Best: 0", 30f, TextAlignmentOptions.Left, new Color(0.8f, 0.8f, 0.9f));
        Place(bestText.rectTransform, TL, new Vector2(30f, -75f), new Vector2(500f, 50f));

        TextMeshProUGUI waveText = CreateText(c, "WaveText", "Wave: 1", 44f, TextAlignmentOptions.Center, Color.white);
        Place(waveText.rectTransform, TC, new Vector2(0f, -20f), new Vector2(400f, 60f));

        TextMeshProUGUI livesText = CreateText(c, "LivesText", "Lives: 3", 36f, TextAlignmentOptions.Right, Color.white);
        Place(livesText.rectTransform, TR, new Vector2(-140f, -30f), new Vector2(300f, 50f));

        // Кнопка паузы (три линии) в правом верхнем углу
        Button pauseButton = CreateButton(c, "PauseButton", "", 30f, TR, new Vector2(-30f, -30f), new Vector2(90f, 90f));
        AddBars((RectTransform)pauseButton.transform);

        // Кнопка магазина в правом нижнем углу
        Button shopButton = CreateButton(c, "ShopButton", "Shop (B)", 34f, BR, new Vector2(-30f, 30f), new Vector2(240f, 80f));

        // Индикатор ульты в левом нижнем углу
        RectTransform ult = NewRect("UltUI", c);
        Place(ult, BL, new Vector2(30f, 30f), new Vector2(110f, 110f));
        Image ultBg = ult.gameObject.AddComponent<Image>();
        ultBg.sprite = KnobSprite();
        ultBg.color = new Color(0.1f, 0.1f, 0.2f, 0.9f);
        ultBg.raycastTarget = false;

        RectTransform fillRect = NewRect("Fill", ult);
        Stretch(fillRect);
        Image fill = fillRect.gameObject.AddComponent<Image>();
        fill.sprite = KnobSprite();
        fill.color = new Color(0.3f, 0.8f, 1f, 1f);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.raycastTarget = false;

        TextMeshProUGUI ultLabel = CreateText(ult, "Label", "X", 48f, TextAlignmentOptions.Center, DarkText);
        Stretch(ultLabel.rectTransform);

        UltUI ultUi = ult.gameObject.AddComponent<UltUI>();
        ultUi.player = playerController;
        ultUi.fillImage = fill;
        ultUi.label = ultLabel;

        // Панель Game Over
        RectTransform gameOverPanel = CreatePanel(c, "GameOverPanel", new Vector2(820f, 620f));
        TextMeshProUGUI gameOverText = CreateText(gameOverPanel, "GameOverText", "GAME OVER", 56f, TextAlignmentOptions.Center, Color.white);
        Place(gameOverText.rectTransform, TC, new Vector2(0f, -40f), new Vector2(760f, 380f));
        Button restartButton = CreateButton(gameOverPanel, "RestartButton", "Restart", 38f, BC, new Vector2(-200f, 50f), new Vector2(340f, 90f));
        Button gameOverMenuButton = CreateButton(gameOverPanel, "MenuButton", "Menu", 38f, BC, new Vector2(200f, 50f), new Vector2(340f, 90f));

        // Панель победы
        RectTransform victoryPanel = CreatePanel(c, "VictoryPanel", new Vector2(820f, 620f));
        TextMeshProUGUI victoryText = CreateText(victoryPanel, "VictoryText", "VICTORY!", 56f, TextAlignmentOptions.Center, Color.white);
        Place(victoryText.rectTransform, TC, new Vector2(0f, -40f), new Vector2(760f, 380f));
        Button continueButton = CreateButton(victoryPanel, "ContinueButton", "Continue", 38f, BC, new Vector2(-200f, 50f), new Vector2(340f, 90f));
        Button victoryMenuButton = CreateButton(victoryPanel, "MenuButton", "Menu", 38f, BC, new Vector2(200f, 50f), new Vector2(340f, 90f));

        // Панель паузы
        RectTransform pausePanel = CreatePanel(c, "PausePanel", new Vector2(520f, 560f));
        TextMeshProUGUI pauseTitle = CreateText(pausePanel, "Title", "PAUSED", 52f, TextAlignmentOptions.Center, Color.white);
        Place(pauseTitle.rectTransform, TC, new Vector2(0f, -30f), new Vector2(460f, 80f));
        Button resumeButton = CreateButton(pausePanel, "ResumeButton", "Resume", 38f, TC, new Vector2(0f, -150f), new Vector2(380f, 90f));
        Button pauseSettingsButton = CreateButton(pausePanel, "SettingsButton", "Settings", 38f, TC, new Vector2(0f, -270f), new Vector2(380f, 90f));
        Button pauseExitButton = CreateButton(pausePanel, "ExitButton", "Exit", 38f, TC, new Vector2(0f, -390f), new Vector2(380f, 90f));

        // Панель магазина
        RectTransform shopPanel = CreatePanel(c, "ShopPanel", new Vector2(900f, 780f));
        TextMeshProUGUI shopTitle = CreateText(shopPanel, "Title", "SHOP", 52f, TextAlignmentOptions.Center, Color.white);
        Place(shopTitle.rectTransform, TC, new Vector2(0f, -30f), new Vector2(600f, 80f));
        Button damageButton = CreateButton(shopPanel, "DamageButton", "", 30f, TC, new Vector2(0f, -140f), new Vector2(780f, 100f));
        Button autoGunButton = CreateButton(shopPanel, "AutoGunButton", "", 30f, TC, new Vector2(0f, -255f), new Vector2(780f, 100f));
        Button droneButton = CreateButton(shopPanel, "DroneButton", "", 30f, TC, new Vector2(0f, -370f), new Vector2(780f, 100f));
        Button ultButton = CreateButton(shopPanel, "UltButton", "", 30f, TC, new Vector2(0f, -485f), new Vector2(780f, 100f));
        Button shopCloseButton = CreateButton(shopPanel, "CloseButton", "Close", 34f, BC, new Vector2(-190f, 40f), new Vector2(320f, 80f));
        Button resetButton = CreateButton(shopPanel, "ResetButton", "Reset save", 34f, BC, new Vector2(190f, 40f), new Vector2(320f, 80f));

        // ---------- Менеджеры ----------
        GameObject gmObject = new GameObject("GameManager");
        GameManager gm = gmObject.AddComponent<GameManager>();
        gm.asteroidPrefab = asteroidPrefab;
        gm.bossPrefab = bossPrefab;
        gm.creditsText = creditsText;
        gm.waveText = waveText;
        gm.livesText = livesText;
        gm.bestText = bestText;
        gm.gameOverPanel = gameOverPanel.GetComponent<CanvasGroup>();
        gm.gameOverText = gameOverText;
        gm.victoryPanel = victoryPanel.GetComponent<CanvasGroup>();
        gm.victoryText = victoryText;

        // Кнопки, которые вызывают методы GameManager
        UnityEventTools.AddPersistentListener(restartButton.onClick, gm.RestartGame);
        UnityEventTools.AddPersistentListener(gameOverMenuButton.onClick, gm.ExitToMenu);
        UnityEventTools.AddPersistentListener(continueButton.onClick, gm.ContinueAfterVictory);
        UnityEventTools.AddPersistentListener(victoryMenuButton.onClick, gm.ExitToMenu);

        // Контроллеры UI живут на отдельном всегда активном объекте
        GameObject controllers = new GameObject("Controllers");
        SettingsPanel settings = CreateSettingsPanel(c, controllers);

        PauseMenu pause = controllers.AddComponent<PauseMenu>();
        pause.panel = pausePanel;
        pause.panelGroup = pausePanel.GetComponent<CanvasGroup>();
        pause.pauseButton = pauseButton;
        pause.resumeButton = resumeButton;
        pause.settingsButton = pauseSettingsButton;
        pause.exitButton = pauseExitButton;
        pause.settings = settings;

        ShopManager shop = controllers.AddComponent<ShopManager>();
        shop.panel = shopPanel;
        shop.panelGroup = shopPanel.GetComponent<CanvasGroup>();
        shop.openButton = shopButton;
        shop.closeButton = shopCloseButton;
        shop.player = playerController;
        shop.playerUpgrades = upgrades;
        shop.damageUpgrade.buyButton = damageButton;
        shop.damageUpgrade.infoText = damageButton.GetComponentInChildren<TextMeshProUGUI>();
        shop.autoGunUpgrade.buyButton = autoGunButton;
        shop.autoGunUpgrade.infoText = autoGunButton.GetComponentInChildren<TextMeshProUGUI>();
        shop.droneUpgrade.buyButton = droneButton;
        shop.droneUpgrade.infoText = droneButton.GetComponentInChildren<TextMeshProUGUI>();
        shop.ultUpgrade.buyButton = ultButton;
        shop.ultUpgrade.infoText = ultButton.GetComponentInChildren<TextMeshProUGUI>();
        UnityEventTools.AddPersistentListener(resetButton.onClick, shop.ResetProgress);

        // Все окна в начале скрыты (скрипты и так их прячут, но так чище в редакторе)
        gameOverPanel.gameObject.SetActive(false);
        victoryPanel.gameObject.SetActive(false);
        pausePanel.gameObject.SetActive(false);
        shopPanel.gameObject.SetActive(false);

        FinishScene("Игровая сцена собрана. Нажми Ctrl+S, чтобы сохранить сцену.");
    }

    [MenuItem("Sector Zero/2. Build Main Menu")]
    static void BuildMainMenu()
    {
        if (Object.FindFirstObjectByType<MainMenu>() != null)
        {
            EditorUtility.DisplayDialog("Sector Zero",
                "В этой сцене уже есть MainMenu. Открой пустую сцену MainMenu или удали старые объекты.", "OK");
            return;
        }

        EnsureFolders();
        SetupCamera();
        EnsureEventSystem();

        Sprite starfield = GetOrCreateStarfield();
        CreateBackground(starfield, 0.5f);
        CreateAudioManager();

        Canvas canvas = CreateCanvas("Canvas");
        Transform c = canvas.transform;

        TextMeshProUGUI title = CreateText(c, "Title", "SECTOR ZERO", 120f, TextAlignmentOptions.Center, Color.white);
        Place(title.rectTransform, TC, new Vector2(0f, -150f), new Vector2(1500f, 200f));

        Button playButton = CreateButton(c, "PlayButton", "Play", 44f, CC, new Vector2(0f, 40f), new Vector2(480f, 100f));
        Button settingsButton = CreateButton(c, "SettingsButton", "Settings", 44f, CC, new Vector2(0f, -90f), new Vector2(480f, 100f));
        Button exitButton = CreateButton(c, "ExitButton", "Exit", 44f, CC, new Vector2(0f, -220f), new Vector2(480f, 100f));

        GameObject controllers = new GameObject("Controllers");
        SettingsPanel settings = CreateSettingsPanel(c, controllers);

        MainMenu menu = controllers.AddComponent<MainMenu>();
        menu.playButton = playButton;
        menu.settingsButton = settingsButton;
        menu.exitButton = exitButton;
        menu.settings = settings;
        menu.titleText = title.rectTransform;

        FinishScene("Главное меню собрано. Нажми Ctrl+S, чтобы сохранить сцену.");
    }

    [MenuItem("Sector Zero/3. Add Scenes To Build Settings")]
    static void AddScenesToBuild()
    {
        string menuPath = null;
        string gamePath = null;

        foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/")) continue;

            string sceneName = Path.GetFileNameWithoutExtension(path);
            if (sceneName == "MainMenu") menuPath = path;
            else if (sceneName == "Game") gamePath = path;
        }

        List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>();
        if (menuPath != null) list.Add(new EditorBuildSettingsScene(menuPath, true));
        if (gamePath != null) list.Add(new EditorBuildSettingsScene(gamePath, true));
        EditorBuildSettings.scenes = list.ToArray();

        string message = "Сцены в сборке: " + list.Count + " из 2 (MainMenu, Game).";
        if (menuPath == null) message += "\nНе найдена сцена MainMenu.";
        if (gamePath == null) message += "\nНе найдена сцена Game.";
        EditorUtility.DisplayDialog("Sector Zero", message, "OK");
    }

    // =====================================================================
    //  4. Apply Sprites: раскладывает спрайты из файла-слотов по игре
    // =====================================================================

    const string SlotsPath = "Assets/Sprites/SectorZeroSprites.asset";
    const float CoverWidth = 19.2f;     // фон должен перекрывать экран камеры
    const float CoverHeight = 10.8f;

    [MenuItem("Sector Zero/4. Apply Sprites")]
    static void ApplySprites()
    {
        SectorZeroSprites slots = AssetDatabase.LoadAssetAtPath<SectorZeroSprites>(SlotsPath);
        if (slots == null)
        {
            EditorUtility.DisplayDialog("Sector Zero",
                "Не найден файл Assets/Sprites/SectorZeroSprites. Сначала выполни пункт 1 (Build Game Scene).", "OK");
            return;
        }

        List<string> done = new List<string>();

        // ---------- Префабы (меняются во всех сценах сразу) ----------
        if (ApplyToPrefab("Laser", slots.laser, slots.laserHeight, false)) done.Add("выстрел");
        if (ApplyToPrefab("Asteroid", slots.asteroid, slots.asteroidHeight, false)) done.Add("астероид");
        if (ApplyToPrefab("Boss", slots.boss, slots.bossHeight, true)) done.Add("босс");
        if (ApplyToPrefab("Drone", slots.drone, slots.droneHeight, false)) done.Add("дрон");

        // ---------- Корабль (он лежит прямо в сцене) ----------
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null && slots.ship != null)
        {
            FitSprite(player, slots.ship, slots.shipHeight, 0.8f);

            Transform fire = player.transform.Find("FirePoint");
            if (fire != null)
            {
                fire.localPosition = new Vector3(0f, slots.ship.bounds.center.y + slots.ship.bounds.extents.y, 0f);
            }
            done.Add("корабль");
        }

        // ---------- Фон ----------
        if (slots.background != null)
        {
            ScrollingBackground[] backgrounds =
                Object.FindObjectsByType<ScrollingBackground>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (ScrollingBackground background in backgrounds)
            {
                foreach (Transform tile in new Transform[] { background.tileA, background.tileB })
                {
                    if (tile == null) continue;

                    SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();
                    if (sr == null) continue;

                    sr.sprite = slots.background;

                    // Растягиваем так, чтобы картинка полностью закрывала экран
                    Vector3 size = slots.background.bounds.size;
                    float scale = Mathf.Max(CoverWidth / size.x, CoverHeight / size.y);
                    tile.localScale = new Vector3(scale, scale, 1f);
                }
                done.Add("фон");
            }
        }

        // ---------- Кнопки (только наши, с ButtonFx) ----------
        if (slots.button != null)
        {
            Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int count = 0;

            foreach (Button button in buttons)
            {
                if (button.GetComponent<ButtonFx>() == null) continue;

                Image image = button.targetGraphic as Image;
                if (image == null) continue;

                SetUiSprite(image, slots.button);

                // Надписи и полоски на кнопке перекрашиваем в выбранный цвет
                foreach (TextMeshProUGUI text in button.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    text.color = slots.buttonTextColor;
                }
                foreach (Image child in button.GetComponentsInChildren<Image>(true))
                {
                    if (child != image) child.color = slots.buttonTextColor;
                }
                count++;
            }

            if (count > 0) done.Add("кнопки (" + count + ")");
        }

        // ---------- Окна (пауза, магазин, настройки, Game Over, победа) ----------
        if (slots.panel != null)
        {
            CanvasGroup[] groups = Object.FindObjectsByType<CanvasGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int count = 0;

            foreach (CanvasGroup group in groups)
            {
                if (!group.name.EndsWith("Panel")) continue;

                Image image = group.GetComponent<Image>();
                if (image == null) continue;

                SetUiSprite(image, slots.panel);
                count++;
            }

            if (count > 0) done.Add("окна (" + count + ")");
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        string message = done.Count > 0
            ? "Заменено: " + string.Join(", ", done) + ".\nНажми Ctrl+S, чтобы сохранить сцену."
            : "Ничего не заменено. Перетащи свои спрайты в слоты файла SectorZeroSprites и повтори.";
        EditorUtility.DisplayDialog("Sector Zero", message, "OK");
    }

    // Подставляет спрайт в префаб (открывает префаб, меняет, сохраняет)
    static bool ApplyToPrefab(string prefabName, Sprite sprite, float height, bool fixMouth)
    {
        if (sprite == null) return false;

        string path = PrefabsFolder + "/" + prefabName + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return false;

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        FitSprite(root, sprite, height, 1f);

        if (fixMouth)
        {
            // Рот босса: чуть выше нижнего края нового спрайта
            Transform mouth = root.transform.Find("MouthPoint");
            if (mouth != null)
            {
                mouth.localPosition = new Vector3(0f, sprite.bounds.center.y - sprite.bounds.extents.y * 0.6f, 0f);
            }
        }

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        return true;
    }

    // Ставит спрайт, подгоняет размер на экране и коллайдер под новую картинку
    static void FitSprite(GameObject go, Sprite sprite, float targetHeight, float colliderFactor)
    {
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        sr.sprite = sprite;

        // Масштаб: чтобы любая картинка (хоть 4000 пикселей) получила нужную высоту на экране
        float scale = targetHeight / sprite.bounds.size.y;
        go.transform.localScale = new Vector3(scale, scale, 1f);

        BoxCollider2D box = go.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.size = sprite.bounds.size * colliderFactor;
            box.offset = sprite.bounds.center;
        }

        CircleCollider2D circle = go.GetComponent<CircleCollider2D>();
        if (circle != null)
        {
            circle.radius = (sprite.bounds.extents.x + sprite.bounds.extents.y) / 2f * colliderFactor;
            circle.offset = sprite.bounds.center;
        }

        // Цифра HP над объектом не должна уменьшаться вместе с ним
        Transform hp = go.transform.Find("HP");
        if (hp != null)
        {
            hp.localScale = Vector3.one / scale;
        }
    }

    static void SetUiSprite(Image image, Sprite sprite)
    {
        image.sprite = sprite;
        image.type = (sprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
        image.color = Color.white;
    }

    static void FinishScene(string message)
    {
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Sector Zero", message, "OK");
    }

    // =====================================================================
    //  Камера, EventSystem, папки
    // =====================================================================

    static void EnsureFolders()
    {
        Directory.CreateDirectory(Path.GetFullPath(SpritesFolder));
        Directory.CreateDirectory(Path.GetFullPath(PrefabsFolder));
        AssetDatabase.Refresh();
    }

    static void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            cam = go.GetComponent<Camera>();
        }

        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.02f, 0.08f);
    }

    static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    // =====================================================================
    //  Спрайты-заглушки
    // =====================================================================

    static Color32 Triangle(int x, int y, int w, int h, Color32 color)
    {
        // Вершина вверху, основание внизу
        float half = (1f - (float)y / h) * (w / 2f);
        return Mathf.Abs(x + 0.5f - w / 2f) <= half ? color : ClearPixel;
    }

    static Color32 Ellipse(int x, int y, int w, int h, Color32 color)
    {
        float dx = (x + 0.5f - w / 2f) / (w / 2f);
        float dy = (y + 0.5f - h / 2f) / (h / 2f);
        return (dx * dx + dy * dy <= 1f) ? color : ClearPixel;
    }

    static Sprite GetOrCreateSprite(string name, int w, int h, float ppu,
                                    System.Func<int, int, int, int, Color32> pixel)
    {
        string assetPath = SpritesFolder + "/" + name + ".png";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (existing != null) return existing;

        Color32[] pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                pixels[y * w + x] = pixel(x, y, w, h);
            }
        }
        return SaveSprite(assetPath, w, h, ppu, pixels);
    }

    static Sprite GetOrCreateStarfield()
    {
        string assetPath = SpritesFolder + "/Starfield.png";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (existing != null) return existing;

        int w = 1920;
        int h = 1080;
        Color32[] pixels = new Color32[w * h];
        Color32 background = new Color32(5, 5, 25, 255);
        for (int i = 0; i < pixels.Length; i++) pixels[i] = background;

        // Случайные звёзды (seed фиксирован, чтобы результат был одинаковым)
        System.Random rng = new System.Random(7);
        for (int s = 0; s < 450; s++)
        {
            int sx = rng.Next(1, w - 2);
            int sy = rng.Next(1, h - 2);
            byte brightness = (byte)rng.Next(120, 256);
            int size = (rng.Next(0, 4) == 0) ? 2 : 1;

            for (int dy = 0; dy < size; dy++)
            {
                for (int dx = 0; dx < size; dx++)
                {
                    pixels[(sy + dy) * w + sx + dx] = new Color32(brightness, brightness, brightness, 255);
                }
            }
        }

        // 1920x1080 при 100 пикселях на единицу = 19.2 x 10.8, этого хватает на экран 16:9
        return SaveSprite(assetPath, w, h, 100f, pixels);
    }

    static Sprite SaveSprite(string assetPath, int w, int h, float ppu, Color32[] pixels)
    {
        Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply();
        File.WriteAllBytes(Path.GetFullPath(assetPath), texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = ppu;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    static Material GetBeamMaterial()
    {
        string path = SpritesFolder + "/Beam.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;

        material = new Material(shader);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // =====================================================================
    //  Префабы
    // =====================================================================

    static GameObject GetOrCreatePrefab(string name, System.Func<GameObject> build)
    {
        string path = PrefabsFolder + "/" + name + ".prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        GameObject temp = build();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
        Object.DestroyImmediate(temp);
        return prefab;
    }

    static GameObject BuildLaserObject(Sprite sprite)
    {
        GameObject go = new GameObject("Laser");
        AddSprite(go, sprite, 5);
        MakeKinematic(go);

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = sprite.bounds.size;

        go.AddComponent<Laser>();
        return go;
    }

    static GameObject BuildAsteroidObject(Sprite sprite)
    {
        GameObject go = new GameObject("Asteroid");
        SpriteRenderer sr = AddSprite(go, sprite, 3);
        MakeKinematic(go);

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = sprite.bounds.extents.x;

        Asteroid asteroid = go.AddComponent<Asteroid>();
        asteroid.spriteRenderer = sr;
        asteroid.hpText = CreateWorldText(go.transform, "HP", "3", 5f);
        return go;
    }

    static GameObject BuildBossObject(Sprite sprite)
    {
        GameObject go = new GameObject("Boss");
        SpriteRenderer sr = AddSprite(go, sprite, 4);
        MakeKinematic(go);

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = sprite.bounds.size;

        GameObject mouth = new GameObject("MouthPoint");
        mouth.transform.SetParent(go.transform, false);
        mouth.transform.localPosition = new Vector3(0f, -sprite.bounds.extents.y * 0.6f, 0f);

        Boss boss = go.AddComponent<Boss>();
        boss.spriteRenderer = sr;
        boss.mouthPoint = mouth.transform;
        boss.hpText = CreateWorldText(go.transform, "HP", "40", 8f);
        return go;
    }

    static GameObject BuildDroneObject(Sprite sprite)
    {
        GameObject go = new GameObject("Drone");
        AddSprite(go, sprite, 5);

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.startWidth = 0.06f;
        line.endWidth = 0.06f;
        line.useWorldSpace = true;
        line.startColor = new Color(0.4f, 1f, 0.5f, 1f);
        line.endColor = new Color(0.4f, 1f, 0.5f, 1f);
        line.sortingOrder = 6;
        Material beam = GetBeamMaterial();
        if (beam != null) line.sharedMaterial = beam;
        line.enabled = false;

        Drone drone = go.AddComponent<Drone>();
        drone.beam = line;
        return go;
    }

    static SpriteRenderer AddSprite(GameObject go, Sprite sprite, int sortingOrder)
    {
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortingOrder;
        return sr;
    }

    static void MakeKinematic(GameObject go)
    {
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
    }

    static TextMeshPro CreateWorldText(Transform parent, string name, string text, float fontSize)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        TextMeshPro t = go.AddComponent<TextMeshPro>();
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.sortingOrder = 10;
        t.rectTransform.sizeDelta = new Vector2(3f, 1f);
        return t;
    }

    // =====================================================================
    //  Фон и звук
    // =====================================================================

    static void CreateBackground(Sprite starfield, float speed)
    {
        GameObject bg = new GameObject("Background");
        ScrollingBackground scrolling = bg.AddComponent<ScrollingBackground>();
        scrolling.speed = speed;
        scrolling.tileA = CreateTile(bg.transform, "TileA", starfield);
        scrolling.tileB = CreateTile(bg.transform, "TileB", starfield);
    }

    static Transform CreateTile(Transform parent, string name, Sprite sprite)
    {
        GameObject tile = new GameObject(name);
        tile.transform.SetParent(parent, false);
        AddSprite(tile, sprite, -10);
        return tile.transform;
    }

    static void CreateAudioManager()
    {
        if (Object.FindFirstObjectByType<AudioManager>() != null) return;

        GameObject go = new GameObject("AudioManager");
        AudioSource music = go.AddComponent<AudioSource>();
        AudioSource sfx = go.AddComponent<AudioSource>();
        music.playOnAwake = false;
        music.loop = true;
        sfx.playOnAwake = false;
        sfx.loop = false;

        AudioManager manager = go.AddComponent<AudioManager>();
        manager.musicSource = music;
        manager.sfxSource = sfx;
    }

    // =====================================================================
    //  UI-помощники
    // =====================================================================

    static Sprite UiSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    static Sprite KnobSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
    }

    static Canvas CreateCanvas(string name)
    {
        GameObject go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    // Ставит элемент: якорь и pivot совпадают, pos считается от якоря
    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI CreateText(Transform parent, string name, string text, float fontSize,
                                      TextAlignmentOptions alignment, Color color)
    {
        RectTransform rt = NewRect(name, parent);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = alignment;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }

    static RectTransform CreatePanel(Transform parent, string name, Vector2 size)
    {
        RectTransform rt = NewRect(name, parent);
        Place(rt, CC, Vector2.zero, size);

        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = UiSprite();
        image.type = Image.Type.Sliced;
        image.color = PanelColor;

        rt.gameObject.AddComponent<CanvasGroup>();
        return rt;
    }

    static Button CreateButton(Transform parent, string name, string label, float fontSize,
                               Vector2 anchor, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(ButtonFx));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), anchor, pos, size);

        // Цвет светлый: когда поставишь свой спрайт, Color на Image лучше сделать белым
        Image image = go.GetComponent<Image>();
        image.sprite = UiSprite();
        image.type = Image.Type.Sliced;
        image.color = ButtonColor;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI text = CreateText(go.transform, "Text (TMP)", label, fontSize, TextAlignmentOptions.Center, DarkText);
        Stretch(text.rectTransform);
        return button;
    }

    // Три полоски на кнопке паузы
    static void AddBars(RectTransform parent)
    {
        for (int i = -1; i <= 1; i++)
        {
            RectTransform bar = NewRect("Bar", parent);
            Place(bar, CC, new Vector2(0f, i * 16f), new Vector2(46f, 7f));

            Image image = bar.gameObject.AddComponent<Image>();
            image.color = DarkText;
            image.raycastTarget = false;
        }
    }

    static Slider CreateSlider(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        DefaultControls.Resources resources = new DefaultControls.Resources();
        resources.standard = UiSprite();
        resources.background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        resources.knob = KnobSprite();

        GameObject go = DefaultControls.CreateSlider(resources);
        go.name = name;
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), anchor, pos, size);

        Slider slider = go.GetComponent<Slider>();
        slider.value = 1f;
        return slider;
    }

    // Окно настроек (одинаковое в главном меню и в игре)
    static SettingsPanel CreateSettingsPanel(Transform canvasTransform, GameObject controllers)
    {
        RectTransform panel = CreatePanel(canvasTransform, "SettingsPanel", new Vector2(780f, 540f));

        TextMeshProUGUI title = CreateText(panel, "Title", "SETTINGS", 52f, TextAlignmentOptions.Center, Color.white);
        Place(title.rectTransform, TC, new Vector2(0f, -30f), new Vector2(700f, 80f));

        TextMeshProUGUI musicLabel = CreateText(panel, "MusicLabel", "Music", 38f, TextAlignmentOptions.Left, Color.white);
        Place(musicLabel.rectTransform, CC, new Vector2(-290f, 70f), new Vector2(220f, 50f));
        Slider musicSlider = CreateSlider(panel, "MusicSlider", CC, new Vector2(80f, 70f), new Vector2(440f, 30f));

        TextMeshProUGUI sfxLabel = CreateText(panel, "SfxLabel", "Effects", 38f, TextAlignmentOptions.Left, Color.white);
        Place(sfxLabel.rectTransform, CC, new Vector2(-290f, -30f), new Vector2(220f, 50f));
        Slider sfxSlider = CreateSlider(panel, "SfxSlider", CC, new Vector2(80f, -30f), new Vector2(440f, 30f));

        Button closeButton = CreateButton(panel, "CloseButton", "Close", 38f, BC, new Vector2(0f, 40f), new Vector2(300f, 80f));

        SettingsPanel settings = controllers.AddComponent<SettingsPanel>();
        settings.panel = panel;
        settings.panelGroup = panel.GetComponent<CanvasGroup>();
        settings.musicSlider = musicSlider;
        settings.sfxSlider = sfxSlider;
        settings.closeButton = closeButton;

        panel.gameObject.SetActive(false);
        return settings;
    }
}
