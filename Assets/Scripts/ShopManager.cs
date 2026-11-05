using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

// Одно улучшение в магазине. Поля настраиваются прямо в Inspector.
[System.Serializable]
public class UpgradeItem
{
    public string title = "Upgrade";    // название (на английском, см. README про шрифт)
    public int baseCost = 20;           // цена первого уровня
    public float costGrowth = 1.5f;     // во сколько раз дорожает каждый следующий уровень
    public int maxLevel = 10;

    public Button buyButton;            // кнопка "купить"
    public TMP_Text infoText;           // текст на кнопке: название, уровень, цена

    [HideInInspector] public int level = 0;
}

// Магазин: окно с 4 улучшениями. Повесь на ПОСТОЯННО АКТИВНЫЙ объект (например, Canvas),
// а не на само окно магазина: окно мы выключаем, и скрипт перестал бы работать.
public class ShopManager : MonoBehaviour
{
    // Открыт ли магазин сейчас (его читает PauseMenu)
    public static bool IsOpen { get; private set; }

    [Header("Окно магазина")]
    public RectTransform panel;         // само окно (RectTransform)
    public CanvasGroup panelGroup;      // Canvas Group на этом окне
    public Button openButton;           // кнопка открытия магазина на экране
    public Button closeButton;          // кнопка закрытия внутри окна
    public float hiddenOffset = 1200f;  // на сколько пикселей окно прячется вниз

    [Header("Улучшения")]
    public UpgradeItem damageUpgrade   = new UpgradeItem { title = "Laser Damage",  baseCost = 20,  maxLevel = 10 };
    public UpgradeItem autoGunUpgrade  = new UpgradeItem { title = "Auto Gun",      baseCost = 50,  maxLevel = 8 };
    public UpgradeItem droneUpgrade    = new UpgradeItem { title = "Drones",        baseCost = 100, maxLevel = 5 };
    public UpgradeItem ultUpgrade      = new UpgradeItem { title = "Ultimate CD",   baseCost = 60,  maxLevel = 8 };

    [Header("Ссылки")]
    public PlayerController player;
    public PlayerUpgrades playerUpgrades;

    UpgradeItem[] allUpgrades;
    Vector2 shownPosition;
    float baseUltCooldown;

    void Start()
    {
        IsOpen = false;

        allUpgrades = new UpgradeItem[] { damageUpgrade, autoGunUpgrade, droneUpgrade, ultUpgrade };
        baseUltCooldown = player.ultCooldown;
        shownPosition = panel.anchoredPosition;     // позиция окна, как ты поставил её в редакторе

        // Вешаем покупку на каждую кнопку
        foreach (UpgradeItem item in allUpgrades)
        {
            UpgradeItem captured = item;
            item.buyButton.onClick.AddListener(() => Buy(captured));
        }

        openButton.onClick.AddListener(Open);
        closeButton.onClick.AddListener(Close);

        // Загружаем сохранённую прокачку и сразу применяем её
        foreach (UpgradeItem item in allUpgrades)
        {
            item.level = Mathf.Clamp(PlayerPrefs.GetInt(SaveKey(item), 0), 0, item.maxLevel);
        }
        ApplyUpgrades();

        panel.gameObject.SetActive(false);
        RefreshUI();
    }

    void Update()
    {
        // Клавиша B открывает/закрывает магазин
        if (Input.GetKeyDown(KeyCode.B))
        {
            if (IsOpen) Close();
            else Open();
        }
    }

    // ---------- Окно ----------

    public void Open()
    {
        if (IsOpen || PauseMenu.IsPaused || GameManager.Instance.isUiBlocked) return;

        IsOpen = true;
        Time.timeScale = 0f;    // пока магазин открыт, игра стоит
        RefreshUI();

        panel.gameObject.SetActive(true);
        panel.DOKill();
        panelGroup.DOKill();

        // Выплываем снизу + проявляемся. SetUpdate(true): работает даже при timeScale = 0
        panel.anchoredPosition = shownPosition + Vector2.down * hiddenOffset;
        panelGroup.alpha = 0f;
        panel.DOAnchorPos(shownPosition, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);
        panelGroup.DOFade(1f, 0.3f).SetUpdate(true);
    }

    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        Time.timeScale = 1f;

        panel.DOKill();
        panelGroup.DOKill();

        // Уезжаем вниз и гаснем, потом выключаем окно
        panel.DOAnchorPos(shownPosition + Vector2.down * hiddenOffset, 0.3f).SetEase(Ease.InBack).SetUpdate(true);
        panelGroup.DOFade(0f, 0.3f).SetUpdate(true).OnComplete(() => panel.gameObject.SetActive(false));
    }

    // ---------- Покупка ----------

    void Buy(UpgradeItem item)
    {
        if (item.level >= item.maxLevel) return;

        int cost = GetCost(item);

        if (!GameManager.Instance.TrySpendCredits(cost))
        {
            // Не хватает кредитов: кнопка трясётся
            item.buyButton.transform.DOKill(true);
            item.buyButton.transform.DOShakePosition(0.25f, 8f, 25).SetUpdate(true);
            return;
        }

        item.level++;
        PlayerPrefs.SetInt(SaveKey(item), item.level);
        PlayerPrefs.Save();
        AudioManager.Buy();
        ApplyUpgrades();
        RefreshUI();
    }

    // Ключ сохранения зависит от названия улучшения (поле Title)
    string SaveKey(UpgradeItem item)
    {
        return "Upgrade_" + item.title;
    }

    // Сброс сохранений: повесь на кнопку "Reset save" (OnClick).
    // Стирает прокачку и рекорды, потом перезапускает сцену.
    public void ResetProgress()
    {
        foreach (UpgradeItem item in allUpgrades)
        {
            PlayerPrefs.DeleteKey(SaveKey(item));
        }
        GameManager.Instance.ClearRecords();
        PlayerPrefs.Save();

        GameManager.Instance.RestartGame();
    }

    int GetCost(UpgradeItem item)
    {
        return Mathf.RoundToInt(item.baseCost * Mathf.Pow(item.costGrowth, item.level));
    }

    // Переносим уровни улучшений в сами игровые параметры
    void ApplyUpgrades()
    {
        // Урон: 1 + уровень
        GameManager.Instance.laserDamage = 1 + damageUpgrade.level;

        // Авто-пушка и дроны: уровень = мощность
        playerUpgrades.SetAutoGunLevel(autoGunUpgrade.level);
        playerUpgrades.SetDroneCount(droneUpgrade.level);

        // Ульта: каждый уровень уменьшает перезарядку на 10%, но не меньше 3 секунд
        player.ultCooldown = Mathf.Max(3f, baseUltCooldown * Mathf.Pow(0.9f, ultUpgrade.level));
    }

    void RefreshUI()
    {
        foreach (UpgradeItem item in allUpgrades)
        {
            bool maxed = item.level >= item.maxLevel;
            string cost = maxed ? "MAX" : GetCost(item).ToString();

            item.infoText.text = item.title + "  Lv " + item.level + "/" + item.maxLevel + "\nCost: " + cost;
            item.buyButton.interactable = !maxed;
        }
    }
}
