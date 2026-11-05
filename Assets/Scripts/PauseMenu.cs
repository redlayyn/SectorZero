using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;

// Меню паузы в игровой сцене. Открывается кнопкой с тремя линиями (правый верхний угол) или клавишей Esc.
// Повесь на ПОСТОЯННО АКТИВНЫЙ объект (например, Canvas), а не на само окно паузы.
public class PauseMenu : MonoBehaviour
{
    // На паузе ли игра (читает ShopManager)
    public static bool IsPaused { get; private set; }

    [Header("Окно паузы")]
    public RectTransform panel;         // окно с тремя кнопками
    public CanvasGroup panelGroup;      // Canvas Group на этом окне

    [Header("Кнопки")]
    public Button pauseButton;          // три линии в углу экрана
    public Button resumeButton;         // Возобновить
    public Button settingsButton;       // Настройки
    public Button exitButton;           // Выход в главное меню

    [Header("Прочее")]
    public SettingsPanel settings;      // окно настроек этой сцены
    public string mainMenuScene = "MainMenu";   // имя сцены главного меню

    void Start()
    {
        IsPaused = false;

        pauseButton.onClick.AddListener(Pause);
        resumeButton.onClick.AddListener(Resume);
        settingsButton.onClick.AddListener(OpenSettings);
        exitButton.onClick.AddListener(ExitToMenu);

        panel.gameObject.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Esc закрывает самое верхнее окно: сначала настройки, потом паузу
            if (SettingsPanel.IsOpen) settings.Close();
            else if (IsPaused) Resume();
            else Pause();
        }
    }

    public void Pause()
    {
        if (IsPaused || ShopManager.IsOpen || GameManager.Instance.isUiBlocked) return;

        IsPaused = true;
        Time.timeScale = 0f;

        panel.gameObject.SetActive(true);
        panel.DOKill();
        panelGroup.DOKill();

        // Окно вырастает из нуля. SetUpdate(true): работает при timeScale = 0
        panel.localScale = Vector3.zero;
        panelGroup.alpha = 0f;
        panel.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);
        panelGroup.DOFade(1f, 0.25f).SetUpdate(true);
    }

    public void Resume()
    {
        if (!IsPaused) return;

        IsPaused = false;
        Time.timeScale = 1f;

        panel.DOKill();
        panelGroup.DOKill();

        panel.DOScale(0f, 0.2f).SetEase(Ease.InBack).SetUpdate(true);
        panelGroup.DOFade(0f, 0.2f).SetUpdate(true).OnComplete(() => panel.gameObject.SetActive(false));
    }

    void OpenSettings()
    {
        settings.Open();
    }

    void ExitToMenu()
    {
        GameManager.Instance.SaveRecords();
        IsPaused = false;
        Time.timeScale = 1f;
        DOTween.KillAll();
        SceneManager.LoadScene(mainMenuScene);
    }
}
