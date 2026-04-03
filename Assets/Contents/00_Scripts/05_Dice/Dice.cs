using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections;

public class Dice : MonoBehaviour, IPointerDownHandler
{
    public int currentValue;
    public bool isKept = false;
    public int currentKeepIndex = -1;
    public Vector3 rollPos;

    public Sprite[] diceFaceSprites;
    private SpriteRenderer spriteRenderer;
    public ParticleSystem rollParticle;

    public float rollDuration = 0.45f;
    public float shakePower = 0.12f;
    public float rotatePower = 25f;
    public float popScale = 1.15f;

    private Vector3 originalScale;
    private Coroutine rollCoroutine;
    public static event Action OnDiceStateChanged;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalScale = transform.localScale;
    }

    public void SetValue(int value)
    {
        if (isKept) return;
        currentValue = value;
        UpdateSprite(value);
    }

    private void UpdateSprite(int value)
    {
        if (diceFaceSprites != null && value > 0 && value <= diceFaceSprites.Length && spriteRenderer != null)
            spriteRenderer.sprite = diceFaceSprites[value - 1];
    }

    public void PlayRollEffect(int finalValue)
    {
        if (rollCoroutine != null) StopCoroutine(rollCoroutine);
        rollCoroutine = StartCoroutine(RollRoutine(finalValue));
    }

    private IEnumerator RollRoutine(int finalValue)
    {
        Vector3 startPos = transform.position;
        float elapsed = 0f;

        while (elapsed < rollDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / rollDuration;

            UpdateSprite(UnityEngine.Random.Range(1, 7));

            Vector3 randomOffset = new Vector3(
                UnityEngine.Random.Range(-shakePower, shakePower),
                UnityEngine.Random.Range(-shakePower, shakePower), 0f);

            transform.position = startPos + randomOffset;
            transform.rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-rotatePower, rotatePower));
            transform.localScale = originalScale * (1f + Mathf.Sin(t * Mathf.PI) * (popScale - 1f));

            yield return null;
        }

        transform.SetPositionAndRotation(startPos, Quaternion.identity);
        transform.localScale = originalScale;
        currentValue = finalValue;
        UpdateSprite(finalValue);

        transform.localScale = originalScale * 1.25f;
        if (rollParticle != null)
        {
            rollParticle.transform.position = transform.position;
            rollParticle.Play();
        }

        yield return new WaitForSeconds(0.06f);
        transform.localScale = originalScale;
        rollCoroutine = null;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (ShopManager.IsShopOpen) return;
        isKept = !isKept;
        OnDiceStateChanged?.Invoke();
        spriteRenderer.color = isKept ? new Color(0.7f, 0.7f, 0.7f) : Color.white;
    }

    public void MoveToTarget(Vector3 targetPos)
    {
        StopAllCoroutines();
        StartCoroutine(MoveRoutine(targetPos));
    }

    private IEnumerator MoveRoutine(Vector3 target)
    {
        float duration = 0.2f, elapsed = 0f;
        Vector3 startPos = transform.position;
        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(startPos, target, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = target;
    }
}