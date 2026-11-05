using UnityEngine;

// Менеджер звука. Положи в КОРЕНЬ иерархии (не внутрь другого объекта) в обеих сценах.
// Он переживает смену сцен, поэтому музыка не прерывается при переходе из меню в игру.
// Все звуки можно оставить пустыми: игра просто будет без них (ошибок не будет).
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Источники звука (два AudioSource на этом объекте)")]
    public AudioSource musicSource;     // выход (Output) -> группа Music в AudioMixer
    public AudioSource sfxSource;       // выход (Output) -> группа SFX в AudioMixer

    [Header("Музыка")]
    public AudioClip musicClip;

    [Header("Эффекты")]
    public AudioClip shootClip;
    public AudioClip explosionClip;
    public AudioClip hitClip;           // попадание лазера
    public AudioClip playerHitClip;     // игрок получил урон
    public AudioClip ultClip;
    public AudioClip clickClip;         // клик по кнопке
    public AudioClip buyClip;           // покупка в магазине
    public AudioClip gameOverClip;
    public AudioClip victoryClip;

    void Awake()
    {
        // Если такой менеджер уже есть (остался с прошлой сцены), этот лишний
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Лишний дубликат пропускаем
        if (Instance != this) return;

        if (musicSource != null && musicClip != null)
        {
            musicSource.clip = musicClip;
            musicSource.loop = true;
            musicSource.Play();
        }
    }

    // ---------- Статические методы: вызываются как AudioManager.Shoot() ----------
    // Если менеджера в сцене нет, ничего не происходит.

    public static void Shoot()      { if (Instance != null) Instance.Play(Instance.shootClip, 0.6f); }
    public static void Explosion()  { if (Instance != null) Instance.Play(Instance.explosionClip, 0.8f); }
    public static void Hit()        { if (Instance != null) Instance.Play(Instance.hitClip, 0.5f); }
    public static void PlayerHit()  { if (Instance != null) Instance.Play(Instance.playerHitClip, 1f); }
    public static void Ult()        { if (Instance != null) Instance.Play(Instance.ultClip, 1f); }
    public static void Click()      { if (Instance != null) Instance.Play(Instance.clickClip, 0.8f); }
    public static void Buy()        { if (Instance != null) Instance.Play(Instance.buyClip, 1f); }
    public static void GameOver()   { if (Instance != null) Instance.Play(Instance.gameOverClip, 1f); }
    public static void Victory()    { if (Instance != null) Instance.Play(Instance.victoryClip, 1f); }

    void Play(AudioClip clip, float volume)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, volume);
    }
}
