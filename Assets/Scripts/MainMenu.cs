using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;

// Главное меню: три кнопки. Повесь на любой активный объект в сцене MainMenu.
public class MainMenu : MonoBehaviour
{
    [Header("Кнопки")]
    public Button playButton;
    public Button settingsButton;
    public Button exitButton;

    [Header("Прочее")]
    public SettingsPanel settings;          // окно настроек этой сцены
    public RectTransform titleText;         // название игры (можно оставить пустым): будет плавно "парить"
    public string gameScene = "Game";       // имя игровой сцены

    void Start()
    {
        Time.timeScale = 1f;

        playButton.onClick.AddListener(Play);
        settingsButton.onClick.AddListener(OpenSettings);
        exitButton.onClick.AddListener(Exit);

        // Кнопки по очереди "выпрыгивают" из нулевого размера
        Button[] buttons = { playButton, settingsButton, exitButton };
        for (int i = 0; i < buttons.Length; i++)
        {
            Transform t = buttons[i].transform;
            t.localScale = Vector3.zero;
            t.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetDelay(0.15f * i).SetLink(buttons[i].gameObject);
        }

        // Название покачивается вверх-вниз по кругу
        if (titleText != null)
        {
            titleText.DOAnchorPosY(titleText.anchoredPosition.y + 15f, 1.5f)
                     .SetEase(Ease.InOutSine)
                     .SetLoops(-1, LoopType.Yoyo)
                     .SetLink(titleText.gameObject);
        }
    }

    void Play()
    {
        DOTween.KillAll();
        SceneManager.LoadScene(gameScene);
    }

    void OpenSettings()
    {
        settings.Open();
    }

    void Exit()
    {
#if UNITY_EDITOR
        // В редакторе Application.Quit() ничего не делает, поэтому выключаем Play Mode
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
