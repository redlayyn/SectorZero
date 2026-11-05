using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

// Повесь на любую UI-кнопку (меню, пауза, магазин).
// Наведение мыши: кнопка плавно увеличивается. Нажатие: сжимается и с отскоком возвращается. Плюс звук клика.
public class ButtonFx : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    public float hoverScale = 1.1f;     // во сколько раз увеличивается при наведении
    public float pressScale = 0.92f;    // во сколько раз сжимается при нажатии

    Vector3 baseScale;
    bool hovered;
    Button button;

    void Awake()
    {
        baseScale = transform.localScale;
        button = GetComponent<Button>();
    }

    // Если окно с кнопкой выключили, пока на неё наведена мышь, возвращаем нормальный размер
    void OnDisable()
    {
        DOTween.Kill(this);
        transform.localScale = baseScale;
        hovered = false;
    }

    // Кнопка не реагирует, если она недоступна (например, улучшение куплено до максимума)
    bool IsActive()
    {
        return button == null || button.interactable;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsActive()) return;
        hovered = true;
        ScaleTo(baseScale * hoverScale, 0.15f, Ease.OutQuad);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        ScaleTo(baseScale, 0.15f, Ease.OutQuad);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsActive()) return;
        ScaleTo(baseScale * pressScale, 0.07f, Ease.OutQuad);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!IsActive()) return;

        // Отпустили: отскок обратно (OutBack даёт лёгкий перелёт за нужный размер)
        Vector3 target = hovered ? baseScale * hoverScale : baseScale;
        ScaleTo(target, 0.25f, Ease.OutBack);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsActive()) return;
        AudioManager.Click();
    }

    void ScaleTo(Vector3 target, float time, Ease ease)
    {
        // Убиваем только свои твины (по id), чтобы не трогать чужие
        DOTween.Kill(this);
        transform.DOScale(target, time).SetEase(ease).SetUpdate(true).SetId(this);
    }
}
