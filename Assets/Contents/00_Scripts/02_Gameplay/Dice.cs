using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections;
using System.Linq;
using DG.Tweening;

public class Dice : MonoBehaviour, IPointerDownHandler
{
    [Header("상태 데이터")]
    public int currentValue;
    // 현재 눈이 나온 면 번호(0~5). 면별 효과를 찾을 때 쓰고, 모르면 -1이다.
    public int currentFaceIndex = -1;
    public bool isKept = false;
    public int currentKeepIndex = -1;
    public Vector3 rollPos;
    public bool IsMoving { get; private set; }
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


    private Tween scoreFeedbackTween;
    private UiHoverIdleJuice hoverJuice;


    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalScale = transform.localScale;
        hoverJuice = GetComponent<UiHoverIdleJuice>();
        if (hoverJuice == null)
            hoverJuice = gameObject.AddComponent<UiHoverIdleJuice>();
        RefreshHoverJuice();
    }

    // 면 효과 계산용 면 번호. 기록된 번호가 현재 눈과 어긋나면 눈 값이 같은 첫 면으로 대신한다.
    // SetData/PlayRollEffect의 faceIndex는 확실할 때만 넘기고, 모르면(복원·튜토리얼 강제값 등) -1을 넘긴다.
    public int ResolveFaceIndex()
    {
        if (myData == null || myData.faceValues == null) return -1;
        if (currentFaceIndex >= 0 && currentFaceIndex < myData.faceValues.Length && myData.faceValues[currentFaceIndex] == currentValue)
            return currentFaceIndex;
        return myData.FindFaceIndex(currentValue);
    }

    public void SetData(DiceData1 data, int initialValue, int faceIndex = -1)
    {
        if (isKept) return;
        myData = data;

        //// 만약 데이터에 커스텀 눈금이 있다면 그것을 사용하고, 없다면 기본 스프라이트를 사용
        //if (myData.customFaceSprites != null && myData.customFaceSprites.Length > 0)
        //{
        //    // Dice 스크립트의 diceFaceSprites 배열을 커스텀 이미지로 교체
        //    this.diceFaceSprites = myData.customFaceSprites;
        //}
        if (myData.customDiceShell != null)
        {
            SetFaceSprite(myData.customDiceShell);
        }

        // 6면이 같은 고정 주사위만 JustDice 숫자 스프라이트를 쓴다. 홀수·짝수는 전용 테이블을 쓴다.
        bool isFixed = myData.faceValues.All(f => f == myData.faceValues[0]);
        useNumberSprite = isFixed;

        currentValue = initialValue;
        currentFaceIndex = faceIndex >= 0 ? faceIndex : myData.FindFaceIndex(initialValue);
        ShowFace(currentFaceIndex);

        // 디버프 주사위는 뒤에 은은한 보랏빛을 깔아 굴릴 때도 알아볼 수 있게 한다.
        DebuffDiceGlow.Sync(this, spriteRenderer);

        spriteRenderer.DOKill(); // 지난 결산의 가짜 주사위 흐림 연출이 남지 않도록 정리
        ApplyDiceColor();

        // 필드 대기 상태 애니메이션 추가
        int[] uniqueFaces = myData.faceValues.Distinct().ToArray();
        bool shouldAnimate = uniqueFaces.Length > 1 && uniqueFaces.Length < 6;

    }

    // 커스텀 6면은 눈 값이 같아도 면 번호로 스프라이트를 고른다(여정 해골/하트/골드).
    void ShowFace(int faceIndex)
    {
        if (spriteRenderer == null) return;
        if (faceIndex >= 0 && SpecialDiceSprites.TryGetSpecialFaceSprite(myData, faceIndex, out Sprite special) && special != null)
        {
            SetFaceSprite(special);
            return;
        }
        int value = currentValue;
        if (myData != null && myData.faceValues != null && faceIndex >= 0 && faceIndex < myData.faceValues.Length)
            value = myData.faceValues[faceIndex];
        UpdateSprite(value);
    }

    private void UpdateSprite(int value)
    {
        if (spriteRenderer == null) return;

        // 1. 값이 0 이하일 때(가짜 주사위)의 처리
        if (value <= 0)
        {
            if (myData != null && myData.customFaceSprites != null && myData.customFaceSprites.Length > 0)
            {
                SetFaceSprite(myData.customFaceSprites[0]);
            }
            return;
        }

        // 커스텀 면 또는 홀수·짝수 전용 테이블. null이면 이번 프레임은 건너뛴다.
        if (SpecialDiceSprites.TryGetSpecialFaceSprite(myData, value - 1, out Sprite special) && special != null)
        {
            SetFaceSprite(special);
            return;
        }

        //기존 1~6 일반 숫자/눈금 처리 (안전장치 추가)
        int safeDefaultIndex = Mathf.Clamp(value - 1, 0, 5); // 0~5 범위를 벗어나지 못하도록 강제 고정

        if (useNumberSprite && fixedNumberSprites != null && fixedNumberSprites.Length >= 6)
        {
            SetFaceSprite(fixedNumberSprites[safeDefaultIndex]);
        }
        else if (diceFaceSprites != null && diceFaceSprites.Length > 0)
        {
            // SetData에서 커스텀 이미지로 덮어씌워졌을 경우를 대비해 배열 길이에 맞게 안전하게 가져옴
            int finalIndex = Mathf.Clamp(value - 1, 0, diceFaceSprites.Length - 1);
            SetFaceSprite(diceFaceSprites[finalIndex]);
        }
    }

    // 주사위 그림은 모두 여기서 정한다. 스탬프가 찍혀 있으면 문양을 새긴 그림으로 바꿔 끼운다(캐시라 굴릴 때도 가볍다).
    // 코팅·킵 색은 renderer.color로 곱하므로 새긴 문양도 코팅 위에 새겨진 것처럼 같이 물든다.
    private void SetFaceSprite(Sprite sprite)
    {
        if (spriteRenderer == null) return;
        StampType stamp = myData != null ? myData.stampType : StampType.None;
        spriteRenderer.sprite = StampEngraveCache.Get(sprite, stamp);
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

    public void PlayRollEffect(int finalValue, int faceIndex = -1)
    {
        if (isKept) return;

        if (idleAnimCoroutine != null)
        {
            StopCoroutine(idleAnimCoroutine);
            idleAnimCoroutine = null;
        }

        if (rollCoroutine != null) StopCoroutine(rollCoroutine);
        rollCoroutine = StartCoroutine(RollRoutine(finalValue, faceIndex));
        RefreshHoverJuice();
    }

    private IEnumerator RollRoutine(int finalValue, int faceIndex)
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
            ShowFace(randomFaceIndex);

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
        currentFaceIndex = faceIndex >= 0 ? faceIndex : (myData != null ? myData.FindFaceIndex(finalValue) : -1);
        ShowFace(currentFaceIndex);

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
        RefreshHoverJuice();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (DiceManager.Instance == null || DiceManager.Instance.IsDiceInputLocked)
        {
            return;
        }

        // 상점이 열려있거나 피규어 상세 패널이 열려있으면 클릭 취소
        if (ShopManager.IsShopOpen || FigureDetailPanel.IsPanelOpen || DebuffDetailPanel.IsPanelOpen || LootSelectionPanel.IsPanelOpen || DeckUI.IsPanelOpen || CoatingSelectionPanel.IsPanelOpen || UIManager.IsSettingsOpen)
            return;

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
        RefreshHoverJuice();
    }

    public void MoveToTarget(Vector3 targetPos)
    {
        StopAllCoroutines();
        rollCoroutine = null;
        StartCoroutine(MoveRoutine(targetPos));
    }

    private IEnumerator MoveRoutine(Vector3 target)
    {
        IsMoving = true;
        RefreshHoverJuice();
        float duration = 0.2f, elapsed = 0f;
        Vector3 startPos = transform.position;
        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(startPos, target, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = target;
        IsMoving = false;
        RefreshHoverJuice();
    }

    // 이어하기는 클릭 이벤트나 이동/굴림 연출 없이 킵 색상과 입력 상태만 복원합니다.
    public void RefreshRestoredState()
    {
        ApplyDiceColor();
        RefreshHoverJuice();
    }

    public bool IsRollAnimating => rollCoroutine != null;

    public void RefreshHoverJuice()
    {
        if (hoverJuice == null)
            hoverJuice = GetComponent<UiHoverIdleJuice>();
        if (hoverJuice == null)
            return;

        hoverJuice.enabled = !isKept && !IsMoving && rollCoroutine == null;
    }

    private void OnDisable() => IsMoving = false;

    private void Update()
    {
        if (myData != null && myData.type == DiceType.Prism)
        {
            ApplyDiceColor();
        }
    }

    public void PlayScoreFeedback(float duration)
    {
        if (!gameObject.activeInHierarchy) return;
        CameraShake.Instance?.ShakeScore();

        // 이전 반응을 끝내고 원래 상태에서 다시 시작
        scoreFeedbackTween?.Kill();

        Vector3 startScale = transform.localScale;
        Quaternion startRotation = transform.localRotation;

        const float scaleBoost = 0.18f; // 최대 1.18배 확대
        const float rotationStrength = 14f; // 좌우 회전 강도
        const float shakeCycles = 3f; // 전체 시간 동안 흔들리는 횟수

        scoreFeedbackTween = DOVirtual.Float(0f, 1f, Mathf.Max(0.05f, duration), t =>
        {
            // 앞부분에서 빠르게 확대하고, 남은 시간 동안 부드럽게 복귀
            float scaleWeight;

            if (t < 0.2f)
            {
                float progress = t / 0.2f;
                scaleWeight = 1f - (1f - progress) * (1f - progress);
            }
            else
            {
                float remaining = (1f - t) / 0.8f;
                scaleWeight = remaining * remaining;
            }

            // 처음에는 강하게 흔들리고 점차 잦아듦
            float damping = (1f - t) * (1f - t);
            float angle = Mathf.Sin(t * Mathf.PI * 2f * shakeCycles) * rotationStrength * damping;

            transform.localScale = startScale * (1f + scaleBoost * scaleWeight);
            transform.localRotation = startRotation * Quaternion.Euler(0f, 0f, angle);
        })
        .SetEase(Ease.Linear)
        .SetUpdate(true) // 점수 카운터와 같은 시간 기준으로 흔들림을 동기화
        .SetLink(gameObject, LinkBehaviour.KillOnDisable)
        .OnKill(() =>
        {
            // 정상 종료하거나 중간에 취소되어도 원래 상태로 복구
            if (this != null)
            {
                transform.localScale = startScale;
                transform.localRotation = startRotation;
            }

            scoreFeedbackTween = null;
        });
    }


    public void ShowFloatingText(int bonusValue)
    {
        // 최초 눈금과 강화 누적 숫자의 생성·정리는 같은 담당자가 처리합니다.
        DiceBonusLabel.Get(this).ShowBaseValue(bonusValue);
    }

    // 가짜 주사위가 결산에서 흐릿하게 꺼지는 연출. 다음 SetData에서 원래 색으로 돌아옵니다.
    public void FadeAsFake()
    {
        if (spriteRenderer == null) return;
        spriteRenderer.DOKill();
        spriteRenderer.DOColor(new Color(0.5f, 0.5f, 0.55f, 0.45f), 0.35f).SetUpdate(true).SetLink(gameObject);
    }

}
