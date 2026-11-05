using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

// Индикатор ульты: круг заполняется, пока идёт перезарядка, внутри секунды.
// Когда ульта готова, внутри буква X и индикатор "подпрыгивает".
public class UltUI : MonoBehaviour
{
    public PlayerController player;
    public Image fillImage;     // Image Type = Filled, Fill Method = Radial 360
    public TMP_Text label;      // текст в центре

    bool wasReady = true;

    void Update()
    {
        float left = player.UltCooldownLeft;
        bool ready = left <= 0f;

        // Clamp01: если магазин уменьшил перезарядку, значение не уйдёт за 0..1
        fillImage.fillAmount = ready ? 1f : Mathf.Clamp01(1f - left / player.ultCooldown);
        label.text = ready ? "X" : Mathf.CeilToInt(left).ToString();

        // Ульта только что стала готова: подпрыгиваем
        if (ready && !wasReady)
        {
            transform.DOKill(true);
            transform.DOPunchScale(Vector3.one * 0.3f, 0.3f).SetLink(gameObject);
        }
        wasReady = ready;
    }
}
