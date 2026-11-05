using UnityEngine;
using DG.Tweening;

// Общие визуальные эффекты. Класс статический: вызывается откуда угодно, вешать никуда не нужно.
//   Fx.Shake()  - тряска камеры
//   Fx.Burst()  - всплеск частиц (искры, взрыв), создаётся прямо из кода
public static class Fx
{
    static Material particleMaterial;

    // Тряска камеры: duration - сколько секунд, strength - насколько сильно
    public static void Shake(float duration = 0.2f, float strength = 0.3f)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        // Завершаем прошлую тряску, чтобы камера вернулась на место и не "уплывала"
        cam.transform.DOKill(true);
        cam.transform.DOShakePosition(duration, strength, 20);
    }

    // Всплеск частиц в точке position
    public static void Burst(Vector3 position, Color color, int count = 20,
                             float speed = 4f, float size = 0.15f, float lifetime = 0.6f)
    {
        Material material = GetMaterial();
        if (material == null) return;   // шейдер не найден: просто пропускаем эффект

        GameObject go = new GameObject("BurstFx");
        go.transform.position = position;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);     // настраивать можно только остановленную систему

        ParticleSystem.MainModule main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = lifetime;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        // Один резкий выброс всех частиц сразу
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)count) });

        // Разлёт во все стороны по кругу
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.1f;

        // К концу жизни частицы гаснут и уменьшаются
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        ParticleSystemRenderer particleRenderer = go.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = material;
        particleRenderer.sortingOrder = 50;     // рисуем поверх спрайтов

        ps.Play();
        Object.Destroy(go, lifetime + 1f);      // убираем объект, когда частицы закончились
    }

    // Простой материал для частиц (создаётся один раз)
    static Material GetMaterial()
    {
        if (particleMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;

            particleMaterial = new Material(shader);
            particleMaterial.hideFlags = HideFlags.HideAndDontSave;
        }
        return particleMaterial;
    }
}
