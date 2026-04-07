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

    public DiceData1 myData; // 추가됨: 이 주사위가 가진 고유 데이터 (코팅, 색상 등)

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

    // SetValue 대신 SetData로 진화! (데이터와 색상을 함께 받음)
    public void SetData(DiceData1 data, int initialValue)
    {
        if (isKept) return;
        myData = data;
        currentValue = initialValue;
        UpdateSprite(initialValue);

        // 코팅된 색상 칠해주기
        if (spriteRenderer != null)
            spriteRenderer.color = myData.diceColor;
    }

    private void UpdateSprite(int value)
    {
        if (diceFaceSprites != null && value > 0 && value <= diceFaceSprites.Length && spriteRenderer != null)
            spriteRenderer.sprite = diceFaceSprites[value - 1];
    }

    public void PlayRollEffect(int finalValue)
    {
        if (isKept) return;
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

        // 연출이 끝나도 코팅 색상 유지
        spriteRenderer.color = isKept ? myData.diceColor * 0.6f : myData.diceColor;
        rollCoroutine = null;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (ShopManager.IsShopOpen) return;
        isKept = !isKept;
        OnDiceStateChanged?.Invoke();

        // 무조건 하얀색으로 돌아가는 게 아니라, 내 고유의 색상(코팅색)을 기준으로 어두워짐
        spriteRenderer.color = isKept ? myData.diceColor * 0.6f : myData.diceColor;
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