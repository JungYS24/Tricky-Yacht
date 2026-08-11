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
    private Coroutine idleAnimCoroutine;

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
        bool isOddEven = myData.specialEffect == SpecialDieEffect.Odd || myData.specialEffect == SpecialDieEffect.Even;

        useNumberSprite = isFixed || isOddEven;

        currentValue = initialValue;
        UpdateSprite(initialValue);

        ApplyDiceColor();

        // 필드 대기 상태 애니메이션 추가
        int[] uniqueFaces = myData.faceValues.Distinct().ToArray();
        bool shouldAnimate = uniqueFaces.Length > 1 && uniqueFaces.Length < 6;

    }

    private void UpdateSprite(int value)
    {
        if (spriteRenderer == null) return;

        // 1. 값이 0 이하일 때(가짜 주사위)의 처리
        if (value <= 0)
        {
            if (myData != null && myData.customFaceSprites != null && myData.customFaceSprites.Length > 0)
            {
                spriteRenderer.sprite = myData.customFaceSprites[0];
            }
            return;
        }

        // 커스텀 이미지가 있는 경우 (88 주사위 완벽 대응)
        if (myData != null && myData.customFaceSprites != null && myData.customFaceSprites.Length > 0)
        {
            // 커스텀 이미지가 1장뿐이라면 무조건 0번을, 여러 장이라면 눈금에 맞게 안전하게 가져옵니다.
            int safeIndex = (myData.customFaceSprites.Length == 1) ? 0 : Mathf.Clamp(value - 1, 0, myData.customFaceSprites.Length - 1);
            spriteRenderer.sprite = myData.customFaceSprites[safeIndex];
            return; // 여기서 바로 함수를 끝내서 에러를 차단합니다!
        }

        //기존 1~6 일반 숫자/눈금 처리 (안전장치 추가)
        int safeDefaultIndex = Mathf.Clamp(value - 1, 0, 5); // 0~5 범위를 벗어나지 못하도록 강제 고정

        if (useNumberSprite && fixedNumberSprites != null && fixedNumberSprites.Length >= 6)
        {
            spriteRenderer.sprite = fixedNumberSprites[safeDefaultIndex];
        }
        else if (diceFaceSprites != null && diceFaceSprites.Length > 0)
        {
            // SetData에서 커스텀 이미지로 덮어씌워졌을 경우를 대비해 배열 길이에 맞게 안전하게 가져옴
            int finalIndex = Mathf.Clamp(value - 1, 0, diceFaceSprites.Length - 1);
            spriteRenderer.sprite = diceFaceSprites[finalIndex];
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

    private System.Collections.IEnumerator IdleAnimationRoutine(int[] faces)
    {
        int index = 0;
        while (true)
        {
            UpdateSprite(faces[index]);
            index = (index + 1) % faces.Length;
            yield return new WaitForSeconds(0.4f);
        }
    }

    public void PlayRollEffect(int finalValue)
    {
        if (isKept) return;

        if (idleAnimCoroutine != null)
        {
            StopCoroutine(idleAnimCoroutine);
            idleAnimCoroutine = null;
        }

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
        // 상점이 열려있거나 피규어 상세 패널이 열려있으면 클릭 취소
        if (ShopManager.IsShopOpen || FigureDetailPanel.IsPanelOpen || LootSelectionPanel.IsPanelOpen || DeckUI.IsPanelOpen || TicketDetailPanel.IsPanelOpen || CoatingSelectionPanel.IsPanelOpen) return;

        //상점 선택 패널(상점/다음 스테이지) 또는 바이옴 선택 패널이 열려있을 때 클릭 차단
        if (DiceManager.Instance != null)
        {
            if (DiceManager.Instance.ui != null && DiceManager.Instance.ui.shopChoicePanel.activeInHierarchy) return;
            if (DiceManager.Instance.biomeSelectionPanel != null && DiceManager.Instance.biomeSelectionPanel.panelRoot.activeInHierarchy) return;
        }

        //튜토리얼 중 클릭 제한 로직
        if (TutorialManager.Instance != null && TutorialManager.Instance.isTutorialActive)
        {
            if (!TutorialManager.Instance.IsDiceClickable(this))
            {
                return; // 허락되지 않은 주사위면 여기서 클릭 취소
            }
        }

        isKept = !isKept;
        OnDiceStateChanged?.Invoke();

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