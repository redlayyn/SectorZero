using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;

// Главный менеджер: кредиты, счёт, рекорды, волны, спавн астероидов и босса, UI, победа и Game Over.
// Синглтон: в любом скрипте доступен через GameManager.Instance
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Экономика")]
    public int credits = 0;
    public int laserDamage = 1;     // урон одного лазера (магазин увеличивает)

    [Header("Волны")]
    public int currentWave = 1;
    public int killsToNextWave = 10;        // сколько астероидов убить до следующей волны
    public float healthGrowth = 1.2f;       // +20% к HP за каждую новую волну
    public float speedGrowth = 1.1f;        // +10% к скорости за каждую новую волну

    [Header("Спавн астероидов")]
    public GameObject asteroidPrefab;
    public float spawnInterval = 1.2f;      // пауза между астероидами, сек
    public float spawnPadding = 0.8f;       // отступ от боковых краёв экрана

    [Header("Босс (можно оставить пустым - тогда боссов не будет)")]
    public GameObject bossPrefab;
    public int bossEveryNWaves = 5;         // босс появляется на каждой 5-й волне
    public int bossesToWin = 2;             // экран победы после убийства стольких боссов

    [Header("UI (перетащи в Inspector)")]
    public TMP_Text creditsText;
    public TMP_Text waveText;
    public TMP_Text livesText;
    public CanvasGroup gameOverPanel;       // панель Game Over с компонентом Canvas Group
    public TMP_Text gameOverText;           // итоговый текст на панели Game Over (можно оставить пустым)
    public TMP_Text bestText;               // "Best: N" на экране (можно оставить пустым)

    [Header("Победа (можно оставить пустым - тогда победы не будет)")]
    public CanvasGroup victoryPanel;        // панель победы с Canvas Group
    public TMP_Text victoryText;            // итоговый текст на панели победы

    [Header("Прочее")]
    public string mainMenuScene = "MainMenu";   // имя сцены главного меню

    public bool isGameOver { get; private set; }
    public bool isVictoryOpen { get; private set; }

    // Открыто ли окно, которое останавливает игру (управление, пауза и магазин это учитывают)
    public bool isUiBlocked { get { return isGameOver || isVictoryOpen; } }

    // Счёт = сколько кредитов заработано за этот забег (траты в магазине его не уменьшают)
    public int score { get; private set; }

    const string BestScoreKey = "BestScore";
    const string BestWaveKey = "BestWave";

    int killsInWave = 0;
    int bossesKilled = 0;
    bool victoryShown = false;
    float spawnMinX, spawnMaxX, spawnY;

    void Awake()
    {
        // Синглтон: должен быть только один GameManager
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // После Game Over/паузы время стоит, поэтому при старте сцены возвращаем нормальное
        Time.timeScale = 1f;
    }

    void Start()
    {
        // Область спавна: ширина экрана по камере, высота чуть выше верхнего края
        Camera cam = Camera.main;
        Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0, 0, 0));
        Vector3 topRight = cam.ViewportToWorldPoint(new Vector3(1, 1, 0));
        spawnMinX = bottomLeft.x + spawnPadding;
        spawnMaxX = topRight.x - spawnPadding;
        spawnY = topRight.y + 1f;

        gameOverPanel.gameObject.SetActive(false);
        if (victoryPanel != null) victoryPanel.gameObject.SetActive(false);

        UpdateCreditsText();
        UpdateWaveText();
        UpdateBestText();

        StartCoroutine(SpawnLoop());
    }

    // ---------- Спавн ----------

    IEnumerator SpawnLoop()
    {
        while (!isGameOver)
        {
            yield return new WaitForSeconds(spawnInterval);

            // Пока жив босс, астероиды сверху не падают - босс плюётся сам
            if (Boss.Current == null)
            {
                float x = Random.Range(spawnMinX, spawnMaxX);
                SpawnAsteroidAt(new Vector3(x, spawnY, 0f));
            }
        }
    }

    // Создаёт астероид в нужной точке (его же использует босс, когда плюётся)
    public void SpawnAsteroidAt(Vector3 position)
    {
        GameObject obj = Instantiate(asteroidPrefab, position, Quaternion.identity);
        obj.GetComponent<Asteroid>().Setup(GetHealthMultiplier(), GetSpeedMultiplier(), PickTier());
    }

    // Случайный размер астероида: 50% маленький, 35% средний, 15% большой
    int PickTier()
    {
        float r = Random.value;
        int tier = (r < 0.5f) ? 0 : (r < 0.85f ? 1 : 2);

        // Большие астероиды появляются только с 3-й волны
        if (currentWave < 3 && tier == 2) tier = 1;

        return tier;
    }

    void SpawnBoss()
    {
        GameObject obj = Instantiate(bossPrefab, new Vector3(0f, spawnY + 2f, 0f), Quaternion.identity);
        obj.GetComponent<Boss>().Setup(GetHealthMultiplier(), GetSpeedMultiplier());
    }

    // Pow: множитель накапливается каждую волну (+20% от прошлой волны)
    public float GetHealthMultiplier() { return Mathf.Pow(healthGrowth, currentWave - 1); }
    public float GetSpeedMultiplier()  { return Mathf.Pow(speedGrowth, currentWave - 1); }

    // ---------- Экономика и волны ----------

    // Вызывается из Asteroid.cs, когда астероид уничтожен игроком
    public void OnAsteroidKilled(int reward)
    {
        AddCredits(reward);

        // Пока жив босс, убийства астероидов не двигают волну вперёд
        if (Boss.Current != null) return;

        killsInWave++;
        if (killsInWave >= killsToNextWave)
        {
            killsInWave = 0;
            currentWave++;
            UpdateWaveText();

            // Пульсация текста волны
            waveText.transform.DOKill(true);
            waveText.transform.DOPunchScale(Vector3.one * 0.3f, 0.3f).SetLink(waveText.gameObject);

            // Каждая N-я волна начинается с босса
            if (bossPrefab != null && currentWave % bossEveryNWaves == 0)
            {
                SpawnBoss();
            }
        }
    }

    // Вызывается из Boss.cs, когда босс убит
    public void OnBossKilled(int reward)
    {
        AddCredits(reward);
        bossesKilled++;

        if (!victoryShown && victoryPanel != null && bossesKilled >= bossesToWin)
        {
            ShowVictory();
        }
    }

    public void AddCredits(int amount)
    {
        credits += amount;
        score += amount;
        UpdateCreditsText();

        // Пульсация счётчика кредитов: увеличился и вернулся обратно
        creditsText.transform.DOKill(true);
        creditsText.transform.DOPunchScale(Vector3.one * 0.25f, 0.25f, 5, 0.5f).SetLink(creditsText.gameObject);
    }

    // Для магазина: возвращает true, если хватило денег
    public bool TrySpendCredits(int cost)
    {
        if (credits < cost) return false;

        credits -= cost;
        UpdateCreditsText();
        return true;
    }

    // ---------- Рекорды ----------

    // Сохраняет лучший счёт и лучшую волну. Возвращает true, если побит рекорд счёта.
    public bool SaveRecords()
    {
        bool newRecord = score > PlayerPrefs.GetInt(BestScoreKey, 0);

        if (newRecord)
        {
            PlayerPrefs.SetInt(BestScoreKey, score);
        }
        if (currentWave > PlayerPrefs.GetInt(BestWaveKey, 0))
        {
            PlayerPrefs.SetInt(BestWaveKey, currentWave);
        }

        PlayerPrefs.Save();
        UpdateBestText();
        return newRecord;
    }

    // Стереть рекорды (вызывается из ShopManager.ResetProgress)
    public void ClearRecords()
    {
        PlayerPrefs.DeleteKey(BestScoreKey);
        PlayerPrefs.DeleteKey(BestWaveKey);
    }

    // ---------- UI ----------

    void UpdateCreditsText() { creditsText.text = "Credits: " + credits; }
    void UpdateWaveText()    { waveText.text = "Wave: " + currentWave; }

    void UpdateBestText()
    {
        if (bestText != null)
        {
            bestText.text = "Best: " + PlayerPrefs.GetInt(BestScoreKey, 0);
        }
    }

    public void SetLives(int lives)
    {
        livesText.text = "Lives: " + Mathf.Max(0, lives);
    }

    // Строка с итогами забега для панелей победы и Game Over
    string BuildResultText(string title, bool newRecord)
    {
        string text = title
                    + "\nScore: " + score
                    + "\nWave: " + currentWave
                    + "\nBest: " + PlayerPrefs.GetInt(BestScoreKey, 0);

        if (newRecord) text += "\nNEW RECORD!";
        return text;
    }

    // ---------- Победа ----------

    void ShowVictory()
    {
        victoryShown = true;
        isVictoryOpen = true;

        bool newRecord = SaveRecords();
        if (victoryText != null)
        {
            victoryText.text = BuildResultText("VICTORY!", newRecord);
        }

        AudioManager.Victory();

        // Окно появляется из нулевого масштаба. SetUpdate(true): работает при timeScale = 0
        victoryPanel.gameObject.SetActive(true);
        victoryPanel.alpha = 0f;
        victoryPanel.transform.localScale = Vector3.zero;

        victoryPanel.DOKill();
        victoryPanel.transform.DOKill();
        victoryPanel.DOFade(1f, 0.5f).SetUpdate(true);
        victoryPanel.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack).SetUpdate(true);

        Time.timeScale = 0f;
    }

    // Повесь на кнопку "Continue" на панели победы (OnClick): бесконечный режим
    public void ContinueAfterVictory()
    {
        if (!isVictoryOpen) return;

        isVictoryOpen = false;
        Time.timeScale = 1f;

        victoryPanel.DOKill();
        victoryPanel.transform.DOKill();
        victoryPanel.transform.DOScale(0f, 0.2f).SetEase(Ease.InBack).SetUpdate(true);
        victoryPanel.DOFade(0f, 0.2f).SetUpdate(true).OnComplete(() => victoryPanel.gameObject.SetActive(false));
    }

    // Повесь на кнопку "Menu" на панелях победы и Game Over (OnClick)
    public void ExitToMenu()
    {
        SaveRecords();
        DOTween.KillAll();
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuScene);
    }

    // ---------- Game Over ----------

    public void GameOver()
    {
        if (isGameOver) return;
        isGameOver = true;

        bool newRecord = SaveRecords();
        if (gameOverText != null)
        {
            gameOverText.text = BuildResultText("GAME OVER", newRecord);
        }

        AudioManager.GameOver();

        // Окно плавно появляется: из нулевого масштаба и из прозрачности.
        // SetUpdate(true) обязателен: ниже мы ставим timeScale = 0,
        // а без этого флага твины на паузе не проигрываются.
        gameOverPanel.gameObject.SetActive(true);
        gameOverPanel.alpha = 0f;
        gameOverPanel.transform.localScale = Vector3.zero;

        gameOverPanel.DOFade(1f, 0.5f).SetUpdate(true);
        gameOverPanel.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack).SetUpdate(true);

        Time.timeScale = 0f;    // останавливаем игру
    }

    // Повесь на кнопку Restart (OnClick)
    public void RestartGame()
    {
        DOTween.KillAll();
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
