using UnityEngine;
using TMPro;
using DG.Tweening;

// Босс-монстр: влетает сверху, качается влево-вправо и выплёвывает астероиды изо рта.
// Его тоже нужно уничтожить лазерами (и дронами, и ультой).
public class Boss : MonoBehaviour, IDamageable
{
    // Текущий живой босс (null, если боссa нет)
    public static Boss Current { get; private set; }

    [Header("Параметры (до умножения на волну)")]
    public int baseHealth = 40;
    public int reward = 100;            // награда за убийство
    public float moveSpeed = 1.2f;      // скорость качания влево-вправо
    public float moveRange = 3f;        // на сколько единиц уходит влево/вправо
    public float spitInterval = 2f;     // раз в сколько секунд плюётся астероидом
    public float hoverDepth = 2.5f;     // на сколько ниже верхнего края экрана висит

    [Header("Ссылки (перетащи в Inspector)")]
    public SpriteRenderer spriteRenderer;   // спрайт босса
    public Transform mouthPoint;            // пустой объект у рта: отсюда вылетают астероиды
    public TMP_Text hpText;                 // цифра HP (можно оставить пустым)
    public GameObject explosionPrefab;      // эффект взрыва (можно оставить пустым)

    public int maxHealth { get; private set; }

    int health;
    float spitTimer;
    float moveTimer;
    float hoverY;
    bool entered;       // долетел ли до своей позиции
    bool isDead;

    void Awake()
    {
        Current = this;
        maxHealth = baseHealth;
        health = baseHealth;
    }

    // Вызывается из GameManager сразу после создания
    public void Setup(float healthMultiplier, float speedMultiplier)
    {
        maxHealth = Mathf.Max(1, Mathf.RoundToInt(baseHealth * healthMultiplier));
        health = maxHealth;
        spitInterval = spitInterval / speedMultiplier;   // с каждой волной плюётся чаще
        UpdateHpText();
    }

    void Start()
    {
        float top = Camera.main.ViewportToWorldPoint(new Vector3(0, 1, 0)).y;
        hoverY = top - hoverDepth;

        // Плавный влёт сверху на свою позицию
        transform.DOMoveY(hoverY, 1.5f)
                 .SetEase(Ease.OutBack)
                 .OnComplete(() => entered = true)
                 .SetLink(gameObject);

        UpdateHpText();
    }

    void Update()
    {
        if (!entered || isDead) return;

        // Качаемся влево-вправо (moveTimer начинается с 0, поэтому рывка нет)
        moveTimer += Time.deltaTime;
        float x = Mathf.Sin(moveTimer * moveSpeed) * moveRange;
        transform.position = new Vector3(x, hoverY, 0f);

        // Плюёмся астероидами по таймеру
        spitTimer += Time.deltaTime;
        if (spitTimer >= spitInterval)
        {
            spitTimer = 0f;
            Spit();
        }
    }

    void Spit()
    {
        Vector3 pos = (mouthPoint != null) ? mouthPoint.position : transform.position;
        GameManager.Instance.SpawnAsteroidAt(pos);

        // Лёгкий "толчок", будто босс выплюнул астероид
        transform.DOKill(true);
        transform.DOPunchScale(Vector3.one * 0.15f, 0.2f).SetLink(gameObject);
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        health -= damage;
        UpdateHpText();

        // Подёргивание и красная вспышка (как у астероида)
        transform.DOKill(true);
        transform.DOShakeScale(0.15f, 0.15f, 20).SetLink(gameObject);

        spriteRenderer.DOKill(true);
        spriteRenderer.DOColor(Color.red, 0.07f).SetLoops(2, LoopType.Yoyo).SetLink(gameObject);

        if (health <= 0)
        {
            Die();
        }
        else
        {
            AudioManager.Hit();
        }
    }

    void Die()
    {
        isDead = true;
        Current = null;

        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        }
        AudioManager.Explosion();
        Fx.Burst(transform.position, new Color(1f, 0.5f, 0.1f), 60, 8f, 0.3f, 1f);
        Fx.Shake(0.5f, 0.5f);

        GameManager.Instance.OnBossKilled(reward);
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    void UpdateHpText()
    {
        if (hpText != null)
        {
            hpText.text = Mathf.Max(0, health).ToString();
        }
    }
}
