using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using DG.Tweening;

// Окно настроек: громкость музыки и эффектов. Используется и в главном меню, и в паузе.
// Повесь скрипт на ПОСТОЯННО АКТИВНЫЙ объект (не на само окно), окно перетащи в поле Panel.
// Громкость сохраняется в PlayerPrefs и применяется при старте любой сцены.
public class SettingsPanel : MonoBehaviour
{
    // Открыты ли настройки (читает PauseMenu для клавиши Esc)
    public static bool IsOpen { get; private set; }

    [Header("Звук")]
    public AudioMixer mixer;            // твой AudioMixer
    public Slider musicSlider;
    public Slider sfxSlider;

    [Header("Окно")]
    public RectTransform panel;         // окно настроек
    public CanvasGroup panelGroup;      // Canvas Group на этом окне
    public Button closeButton;

    // Имена параметров, которые ты "выставишь наружу" (Expose) в AudioMixer
    const string MusicParam = "MusicVolume";
    const string SfxParam = "SFXVolume";

    void Start()
    {
        IsOpen = false;

        // Диапазон ползунков: 0.0001..1 (ноль нельзя: логарифм нуля не существует)
        musicSlider.minValue = 0.0001f;
        musicSlider.maxValue = 1f;
        sfxSlider.minValue = 0.0001f;
        sfxSlider.maxValue = 1f;

        // Загружаем сохранённые значения (по умолчанию 0.75)
        musicSlider.value = PlayerPrefs.GetFloat(MusicParam, 0.75f);
        sfxSlider.value = PlayerPrefs.GetFloat(SfxParam, 0.75f);

        // Применяем сразу при старте сцены
        ApplyMusic(musicSlider.value);
        ApplySfx(sfxSlider.value);

        musicSlider.onValueChanged.AddListener(ApplyMusic);
        sfxSlider.onValueChanged.AddListener(ApplySfx);
        closeButton.onClick.AddListener(Close);

        panel.gameObject.SetActive(false);
    }

    void ApplyMusic(float value)
    {
        // Ползунок 0..1 переводим в децибелы (-80..0)
        mixer.SetFloat(MusicParam, Mathf.Log10(value) * 20f);
        PlayerPrefs.SetFloat(MusicParam, value);
    }

    void ApplySfx(float value)
    {
        mixer.SetFloat(SfxParam, Mathf.Log10(value) * 20f);
        PlayerPrefs.SetFloat(SfxParam, value);
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;

        panel.gameObject.SetActive(true);
        panel.DOKill();
        panelGroup.DOKill();

        // Окно вырастает из нуля. SetUpdate(true): работает и на паузе (timeScale = 0)
        panel.localScale = Vector3.zero;
        panelGroup.alpha = 0f;
        panel.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);
        panelGroup.DOFade(1f, 0.25f).SetUpdate(true);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;

        PlayerPrefs.Save();

        panel.DOKill();
        panelGroup.DOKill();

        panel.DOScale(0f, 0.2f).SetEase(Ease.InBack).SetUpdate(true);
        panelGroup.DOFade(0f, 0.2f).SetUpdate(true).OnComplete(() => panel.gameObject.SetActive(false));
    }
}
