using UnityEngine;

// Бесконечно прокручивающийся фон. Нужны два одинаковых спрайта неба (tileA и tileB).
// tileA поставь по центру камеры, tileB можно положить куда угодно: скрипт сам поставит его ровно над tileA.
public class ScrollingBackground : MonoBehaviour
{
    public Transform tileA;
    public Transform tileB;         // копия tileA
    public float speed = 1f;        // скорость прокрутки вниз

    float tileHeight;
    float startY;

    void Start()
    {
        SpriteRenderer sr = tileA.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            Debug.LogError("ScrollingBackground: у tileA нет SpriteRenderer");
            enabled = false;
            return;
        }

        tileHeight = sr.bounds.size.y;
        startY = tileA.position.y;

        // Ставим tileB точно над tileA
        tileB.position = new Vector3(tileA.position.x, startY + tileHeight, tileA.position.z);
    }

    void Update()
    {
        Scroll(tileA);
        Scroll(tileB);
    }

    void Scroll(Transform tile)
    {
        tile.Translate(Vector3.down * speed * Time.deltaTime, Space.World);

        // Спрайт целиком ушёл вниз: переносим его наверх, над соседом
        if (tile.position.y <= startY - tileHeight)
        {
            tile.position += Vector3.up * tileHeight * 2f;
        }
    }
}
