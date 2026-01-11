using System.Collections;
using UnityEngine;

public class Gem : MonoBehaviour
{
    public enum SpecialType
    {
        None,
        Bomb,
        RocketH,
        RocketV,
        DiscoBall,
        PaperPlane
    }

    public int type;
    public int x, y;
    public SpriteRenderer spriteRenderer;

    [HideInInspector] public bool isMatched = false;
    [HideInInspector] public bool isBeingDestroyed = false;

    [Header("Special")]
    public SpecialType specialType = SpecialType.None;
    public bool IsSpecial => specialType != SpecialType.None;

    void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void SetType(int newType, Sprite sprite)
    {
        type = newType;
        if (spriteRenderer != null) spriteRenderer.sprite = sprite;
    }

    public void SetSpecial(SpecialType newSpecial, Sprite specialSprite)
    {
        specialType = newSpecial;

        if (newSpecial != SpecialType.None && specialSprite != null && spriteRenderer != null)
            spriteRenderer.sprite = specialSprite;
        Debug.Log($"SET SPECIAL: ({x},{y}) type={type} special={newSpecial}");

    }

    public IEnumerator AnimateSpawn(float duration = 0.15f, float overshoot = 1.1f)
    {
        if (isBeingDestroyed) yield break;
        BoardManager bm = FindObjectOfType<BoardManager>();
        float s = (bm != null) ? bm.gemScale : 1f;

        Vector3 start = Vector3.zero;
        Vector3 over = Vector3.one * s * overshoot;
        Vector3 end = Vector3.one * s;

        float t = 0f;
        float half = duration * 0.5f;

        while (t < half)
        {
            t += Time.deltaTime;
            float k = t / half;
            float ease = 1f - Mathf.Cos(k * Mathf.PI * 0.5f);
            transform.localScale = Vector3.Lerp(start, over, ease);
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float k = t / half;
            float ease = k * k;
            transform.localScale = Vector3.Lerp(over, end, ease);
            yield return null;
        }

        transform.localScale = end;
    }

    public IEnumerator AnimateDestroy(float duration = 0.15f)
    {
        isBeingDestroyed = true;
        Vector3 start = transform.localScale;
        Vector3 end = Vector3.zero;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = t / duration;
            float ease = k * k;
            transform.localScale = Vector3.Lerp(start, end, ease);
            yield return null;
        }
        transform.localScale = end;
    }

    public IEnumerator Bounce(float duration = 0.10f, float amplitude = 0.10f)
    {
        if (isBeingDestroyed) yield break;
        BoardManager bm = FindObjectOfType<BoardManager>();
        Vector3 baseScale = Vector3.one * ((bm != null) ? bm.gemScale : 1f);

        float t = 0f;
        float half = duration * 0.5f;

        while (t < half)
        {
            if (isBeingDestroyed) yield break;

            t += Time.deltaTime;
            float k = t / half;
            float ease = 1f - Mathf.Cos(k * Mathf.PI * 0.5f);

            float sx = baseScale.x + amplitude * ease;
            float sy = baseScale.y - amplitude * ease;

            transform.localScale = new Vector3(sx, sy, 1f);
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            if (isBeingDestroyed) yield break;

            t += Time.deltaTime;
            float k = t / half;
            float ease = k * k;
            transform.localScale = Vector3.Lerp(transform.localScale, baseScale, ease);
            yield return null;
        }

        transform.localScale = baseScale;
    }
    float lastClickTime;
    const float doubleClickThreshold = 0.25f;

    void OnMouseDown()
    {
        if (Time.time - lastClickTime < doubleClickThreshold)
        {
            lastClickTime = 0;
            BoardManager bm = FindObjectOfType<BoardManager>();
            if (bm != null)
                bm.TryActivateSpecial(this);

            return;
        }
        lastClickTime = Time.time;
    }

}
