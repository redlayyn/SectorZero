using System.Collections.Generic;
using UnityEngine;

// Повесь на корабль игрока рядом с PlayerController.
// Хранит авто-пушку и дронов. Уровни ему выставляет ShopManager.
public class PlayerUpgrades : MonoBehaviour
{
    [Header("Авто-пушка")]
    public GameObject laserPrefab;      // тот же префаб лазера, что и у PlayerController
    public Transform firePoint;         // нос корабля (можно оставить пустым)

    [Header("Дроны")]
    public GameObject dronePrefab;      // префаб дрона со скриптом Drone
    public float droneRadius = 1.3f;    // на каком расстоянии от корабля кружат

    int autoGunLevel = 0;
    float autoGunTimer = 0f;
    readonly List<Drone> drones = new List<Drone>();

    // 0 = пушка выключена
    public void SetAutoGunLevel(int level)
    {
        autoGunLevel = level;
    }

    // Выстрелов в секунду: 1 на первом уровне, +0.5 за каждый следующий
    float ShotsPerSecond()
    {
        return 1f + (autoGunLevel - 1) * 0.5f;
    }

    void Update()
    {
        if (autoGunLevel <= 0) return;
        if (GameManager.Instance.isUiBlocked) return;

        autoGunTimer += Time.deltaTime;
        float interval = 1f / ShotsPerSecond();

        if (autoGunTimer >= interval)
        {
            autoGunTimer -= interval;

            Transform spawnPoint = (firePoint != null) ? firePoint : transform;
            Instantiate(laserPrefab, spawnPoint.position, Quaternion.identity);
            AudioManager.Shoot();
        }
    }

    // Столько дронов должно быть вокруг корабля
    public void SetDroneCount(int count)
    {
        // Добавляем недостающих (уменьшать число дронов магазин не умеет)
        while (drones.Count < count)
        {
            GameObject obj = Instantiate(dronePrefab, transform.position, Quaternion.identity);
            drones.Add(obj.GetComponent<Drone>());
        }

        // Раскладываем дронов равномерно по кругу
        for (int i = 0; i < drones.Count; i++)
        {
            drones[i].Init(transform, 360f / drones.Count * i, droneRadius);
        }
    }
}
