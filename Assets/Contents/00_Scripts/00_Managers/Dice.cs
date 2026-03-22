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

    // 상태 변화를 알리는 이벤트
    public static event Action OnDiceStateChanged;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) spriteRenderer.color = Color.white;
    }

    public void SetValue(int value)
    {
        if (isKept) return;
        currentValue = value;
        if (diceFaceSprites != null && diceFaceSprites.Length >= 6)
            spriteRenderer.sprite = diceFaceSprites[value - 1];
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