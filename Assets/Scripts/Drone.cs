using UnityEngine;
using DG.Tweening;

// Дрон-помощник: кружит вокруг корабля и раз в секунду бьёт ближайший астероид
// (если астероидов нет, а на экране босс, то бьёт босса).
public class Drone : MonoBehaviour
{
    public int damage = 1;              // урон за один удар
    public float fireInterval = 1f;     // раз в сколько секунд бьёт
    public float orbitSpeed = 90f;      // скорость кружения, градусов в секунду
    public LineRenderer beam;           // луч до цели (можно оставить пустым)

    Transform owner;    // вокруг кого летаем (корабль)
    float angle;
    float radius;
    float timer;
    float screenTop;

    // Вызывается из PlayerUpgrades
    public void Init(Transform owner, float startAngle, float radius)
    {
        this.owner = owner;
        this.angle = startAngle;
        this.radius = radius;
    }

    void Start()
    {
        screenTop = Camera.main.ViewportToWorldPoint(new Vector3(0, 1, 0)).y;
        if (beam != null) beam.enabled = false;
    }

    void Update()
    {
        if (owner == null) return;

        // Летим по кругу вокруг корабля
        angle += orbitSpeed * Time.deltaTime;
        float rad = angle * Mathf.Deg2Rad;
        transform.position = owner.position + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * radius;

        // Бьём по таймеру
        timer += Time.deltaTime;
        if (timer >= fireInterval)
        {
            timer = 0f;
            Fire();
        }
    }

    void Fire()
    {
        // Ищем ближайший астероид, который уже на экране
        Transform nearest = null;
        float bestDistance = float.MaxValue;

        foreach (Asteroid asteroid in Asteroid.Active)
        {
            if (asteroid == null) continue;
            if (asteroid.transform.position.y > screenTop) continue;

            float distance = (asteroid.transform.position - transform.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = asteroid.transform;
            }
        }

        // Астероидов нет - бьём босса
        if (nearest == null && Boss.Current != null)
        {
            nearest = Boss.Current.transform;
        }

        if (nearest == null) return;

        ShowBeam(nearest.position);

        // Дрон "дёрнулся" при выстреле
        transform.DOKill(true);
        transform.DOPunchScale(Vector3.one * 0.3f, 0.15f).SetLink(gameObject);

        // Урон наносим после цикла: астероид может удалиться из списка Active
        IDamageable target = nearest.GetComponent<IDamageable>();
        if (target != null)
        {
            target.TakeDamage(damage);
        }
    }

    void ShowBeam(Vector3 targetPosition)
    {
        if (beam == null) return;

        beam.positionCount = 2;
        beam.SetPosition(0, transform.position);
        beam.SetPosition(1, targetPosition);
        beam.enabled = true;
        Invoke(nameof(HideBeam), 0.08f);
    }

    void HideBeam()
    {
        if (beam != null) beam.enabled = false;
    }
}
