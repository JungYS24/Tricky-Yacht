using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections;
using System.Linq;

public class Dice : MonoBehaviour, IPointerDownHandler
{
    [Header("상태 데이터")]
    public int currentValue;
    public bool isKept = false;
    public int currentKeepIndex = -1;
    public Vector3 rollPos;
    public DiceData1 myData; // 주사위 고유 데이터 (코팅, 색상, 면 구성 등)

    [Header("렌더링 및 연출")]
    public Sprite[] diceFaceSprites;     // 일반 주사위 눈금 이미지 (1~6)
    public Sprite[] fixedNumberSprites;  // 고정 주사위용 아라비아 숫자 이미지 (1~6)
    private SpriteRenderer spriteRenderer;
    public ParticleSystem rollParticle;

    [Header("애니메이션 설정")]
    public float rollDuration = 0.45f;
    public float shakePower = 0.12f;
    public float rotatePower = 25f;
    public float popScale = 1.15f;

    private Vector3 originalScale;
    private Coroutine rollCoroutine;
    private bool isFixedDice = false; // 6면이 모두 같은 숫자인지 여부
    private bool useNumberSprite = false;

    [Header("코팅 색상 보정")]
    [SerializeField] private float coatingBrightness = 1.35f;
    [SerializeField] private float keptDarkness = 0.6f;

    public static event Action OnDiceStateChanged;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalScale = transform.localScale;
    }

    public void SetData(DiceData1 data, int initialValue)
    {
        if (isKept) return;
        myData = data;

        // 만약 데이터에 커스텀 눈금이 있다면 그것을 사용하고, 없다면 기본 스프라이트를 사용
        if (myData.customFaceSprites != null && myData.customFaceSprites.Length > 0)
        {
            // Dice 스크립트의 diceFaceSprites 배열을 커스텀 이미지로 교체
            this.diceFaceSprites = myData.customFaceSprites;
        }
        if (myData.customDiceShell != null)
        {
            spriteRenderer.sprite = myData.customDiceShell;
        }

        // 고정 주사위이거나, 이름에 '홀수' 또는 '짝수'가 들어가면 숫자 이미지를 사용하도록 설정
        bool isFixed = myData.faceValues.All(f => f == myData.faceValues[0]);
        bool isOddEven = !string.IsNullOrEmpty(myData.diceName) &&
                         (myData.diceName.Contains("홀수") || myData.diceName.Contains("짝수"));

        useNumberSprite = isFixed || isOddEven;

        currentValue = initialValue;
        UpdateSprite(initialValue);

        ApplyDiceColor();

    }

    private void UpdateSprite(int value)
    {
        if (spriteRenderer == null || value <= 0) return;

        // 판별된 결과에 따라 숫자 스프라이트 또는 눈금 스프라이트를 선택
        if (useNumberSprite && fixedNumberSprites != null && value <= fixedNumberSprites.Length)
        {
            spriteRenderer.sprite = fixedNumberSprites[value - 1];
        }
        else if (diceFaceSprites != null && value <= diceFaceSprites.Length)
        {
            spriteRenderer.sprite = diceFaceSprites[value - 1];
        }
    }

    private void ApplyDiceColor()
    {
        if (spriteRenderer == null || myData == null) return;

        Color finalColor;

        if (myData.type == DiceType.Prism)
        {
            float hue = Mathf.Repeat(Time.time * 0.6f, 1f);
            finalColor = Color.HSVToRGB(hue, 0.55f, coatingBrightness);
        }
        else
        {
            finalColor = myData.diceColor * coatingBrightness;
        }

        finalColor.a = myData.diceColor.a;

        if (isKept)
        {
            finalColor *= keptDarkness;
            finalColor.a = myData.diceColor.a;
        }

        spriteRenderer.color = finalColor;
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

            // 1~6 전체가 아닌, 이 주사위가 가진 면(faceValues) 중에서만 랜덤하게 보여줌
            // 홀수 주사위라면 굴러가는 동안에도 1, 3, 5만 보임
            int randomFaceIndex = UnityEngine.Random.Range(0, 6);
            UpdateSprite(myData.faceValues[randomFaceIndex]);

            // 흔들림 및 회전 연출
            Vector3 randomOffset = new Vector3(
                UnityEngine.Random.Range(-shakePower, shakePower),
                UnityEngine.Random.Range(-shakePower, shakePower), 0f);

            transform.position = startPos + randomOffset;
            transform.rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-rotatePower, rotatePower));
            transform.localScale = originalScale * (1f + Mathf.Sin(t * Mathf.PI) * (popScale - 1f));

            yield return null;
        }

        // 연출 종료 후 상태 복구
        transform.SetPositionAndRotation(startPos, Quaternion.identity);
        transform.localScale = originalScale;
        currentValue = finalValue;
        UpdateSprite(finalValue);

        // 팝업 이펙트 및 파티클
        transform.localScale = originalScale * 1.25f;
        if (rollParticle != null)
        {
            rollParticle.transform.position = transform.position;
            rollParticle.Play();
        }

        yield return new WaitForSeconds(0.06f);
        transform.localScale = originalScale;

        // 보관 상태에 따른 색상 최종 조정
        ApplyDiceColor();
        rollCoroutine = null;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (ShopManager.IsShopOpen) return;

        isKept = !isKept;
        OnDiceStateChanged?.Invoke();

        // 보관 시에는 고유 코팅 색상을 기준으로 어둡게 처리
        ApplyDiceColor();
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
    private void Update()
    {
        if (myData != null && myData.type == DiceType.Prism)
        {
            ApplyDiceColor();
        }
    }
}