using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections;

public class Dice : MonoBehaviour, IPointerDownHandler
{
    [Header("주사위 데이터")]
    public int currentValue;
    public bool isKept = false;
    public int currentKeepIndex = -1;
    public Vector3 rollPos;

    [Header("시각 효과 설정")]
    public Sprite[] diceFaceSprites; // 1~6번 눈 이미지
    private SpriteRenderer spriteRenderer;

    public ParticleSystem rollParticle;

    [Header("굴림 이펙트")]
    public float rollDuration = 0.45f;
    public float shakePower = 0.12f;
    public float rotatePower = 25f;
    public float popScale = 1.15f;

    private Vector3 originalScale;
    private Coroutine rollCoroutine;

    // 상태 변화를 알리는 이벤트
    public static event Action OnDiceStateChanged;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalScale = transform.localScale;

        if (spriteRenderer != null)
            spriteRenderer.color = Color.white;
    }

    public void SetValue(int value)
    {
        if (isKept) return;

        currentValue = value;

        if (diceFaceSprites != null && diceFaceSprites.Length >= 6 && spriteRenderer != null)
            spriteRenderer.sprite = diceFaceSprites[value - 1];
    }
    public void PlayRollEffect(int finalValue)
    {
        if (rollCoroutine != null)
            StopCoroutine(rollCoroutine);

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

            // 면을 빠르게 바꿔서 굴러가는 느낌
            int fakeValue = UnityEngine.Random.Range(1, 7);
            if (diceFaceSprites != null && diceFaceSprites.Length >= 6 && spriteRenderer != null)
                spriteRenderer.sprite = diceFaceSprites[fakeValue - 1];

            // 흔들림
            Vector3 randomOffset = new Vector3(
                UnityEngine.Random.Range(-shakePower, shakePower),
                UnityEngine.Random.Range(-shakePower, shakePower),
                0f
            );

            transform.position = startPos + randomOffset;

            // 회전
            float z = UnityEngine.Random.Range(-rotatePower, rotatePower);
            transform.rotation = Quaternion.Euler(0f, 0f, z);

            // 살짝 커졌다 돌아오기
            float scaleT = 1f + Mathf.Sin(t * Mathf.PI) * (popScale - 1f);
            transform.localScale = originalScale * scaleT;

            yield return null;
        }

        // 최종 상태 고정
        transform.position = startPos;
        transform.rotation = Quaternion.identity;
        transform.localScale = originalScale;

        currentValue = finalValue;
        if (diceFaceSprites != null && diceFaceSprites.Length >= 6 && spriteRenderer != null)
            spriteRenderer.sprite = diceFaceSprites[finalValue - 1];

        // 숫자 확정 순간 팍 터뜨리기
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
        isKept = !isKept;

        // 매니저에게 상태 변화 알림
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
        float duration = 0.2f;
        float elapsed = 0f;
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