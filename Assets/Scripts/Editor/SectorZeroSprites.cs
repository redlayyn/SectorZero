using UnityEngine;

// Один файл-"полка" для всех картинок игры.
// Перетащи свои спрайты в слоты в Inspector, потом выбери в меню Sector Zero > 4. Apply Sprites.
// Файл ДОЛЖЕН лежать в папке с именем Editor (рядом с SectorZeroBuilder).
[CreateAssetMenu(fileName = "SectorZeroSprites", menuName = "Sector Zero/Sprites")]
public class SectorZeroSprites : ScriptableObject
{
    [Header("Игровые объекты (оставь пустым, если не хочешь менять)")]
    public Sprite ship;         // твой корабль
    public Sprite laser;        // выстрел
    public Sprite asteroid;     // астероид
    public Sprite boss;         // босс-монстр
    public Sprite drone;        // дрон-помощник

    [Header("Размер на экране: высота в игровых единицах (камера видит 10 единиц по высоте)")]
    public float shipHeight = 1f;
    public float laserHeight = 0.5f;
    public float asteroidHeight = 1f;
    public float bossHeight = 2f;
    public float droneHeight = 0.4f;

    [Header("Фон")]
    public Sprite background;   // звёздное небо

    [Header("Интерфейс")]
    public Sprite button;       // картинка кнопки
    public Sprite panel;        // картинка окон (пауза, магазин, настройки)
    public Color buttonTextColor = Color.white;     // цвет надписей на кнопках
}
