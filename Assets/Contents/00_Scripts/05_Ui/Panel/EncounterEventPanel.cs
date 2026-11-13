using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// 조우자 종류를 구분하기 위한 Enum (데블 다이스 제외)
public enum EncounterType
{
    Clown, AbyssDealer, BlindFortuneTeller, Poacher,
    SacrificedGirl, Alchemist, WishWanderer, ForgottenExplorer,
    MadHatter, RustyCaptain
}

// 인스펙터에서 조우자 데이터를 쉽게 세팅하기 위한 클래스
[System.Serializable]
public class EncounterData
{
    public EncounterType type;
    public string encounterName;
    public List<BiomeType> appearBiomes; // 등장 가능한 바이옴 리스트

    [Header("조우자 외형 (이미지 & 애니메이션)")]
    public Sprite encounterSprite; // 조우자의 기본 일러스트
    public RuntimeAnimatorController animatorController; // 조우자 전용 애니메이터 컨트롤러

    [TextArea(2, 4)] public string[] dialogues; // 3~4줄의 대사 배열
    public string choiceAText; // 하이리스크 하이리턴 텍스트
    public string choiceBText; // 안전/거절 텍스트
}

public class EncounterEventPanel : MonoBehaviour
{
    [Header("참조 설정")]
    public DiceManager diceManager;

    [Header("특수 보상 아이템 (ScriptableObject)")]
    public DiceItemSO eightyEightDiceSO;     // 88주사위 데이터
    public FigureItemSO sacrificedGirlFigureSO;    // 제물소녀 전용 피규어
    public FigureItemSO wishWandererFigureSO;      // 방랑자 전용 피규어


    [Header("조우자 데이터베이스")]
    public List<EncounterData> encounterDatabase = new List<EncounterData>();
    private EncounterData currentEncounter;

    [Header("전체 화면 및 대화 UI")]
    public GameObject fullScreenBackground;
    public Animator encounterAnimator;
    public GameObject dialogueRoot;
    public TextMeshProUGUI dialogueText;
    public Button nextDialogueButton;
    [Tooltip("대사 창 오른쪽 아래 '다음' 표시. 마지막 대사에서는 숨깁니다.")]
    public Graphic nextIndicator;
    public SpriteRenderer encounterSpriteRenderer;

    [Header("선택지 UI")]
    public GameObject choiceRoot;
    public Button choiceAButton;
    public TextMeshProUGUI choiceAText;
    public Button choiceBButton;
    public TextMeshProUGUI choiceBText;

    [Header("대화창 스킨")]
    public Sprite dialogPanelSprite;
    [Tooltip("값이 클수록 9슬라이스 테두리가 얇아집니다. 2 = 테두리 24px 스프라이트가 화면에서 12px.")]
    public float dialogBorderScale = 2f;
    [Tooltip("대사·선택지 글자 크기 배율. 씬에 저장된 원래 크기에 곱합니다.")]
    public float encounterTextScale = 0.85f;

    static readonly Vector4 DialogueTextMargin = new Vector4(32f, 24f, 32f, 24f);
    const float NextIndicatorBob = 6f;
    const float NextIndicatorPeriod = 0.7f;
    const float NextIndicatorMinAlpha = 0.35f;

    private int dialogueIndex = 0;

    // 보스 등장 대사 모드(조우자 대신 보스가 말한다). 선택지 없이 대사만 보여 주고 끝나면 콜백을 부른다.
    string introKeyPrefix;
    string[] introFallback;
    System.Action introFinished;
    Color choiceATextColor = Color.white;
    Color choiceBTextColor = Color.white;
    bool encounterTextScaleApplied;
    Vector2 nextIndicatorBasePos;
    bool nextIndicatorBaseSaved;

    private void Awake()
    {
        if (nextDialogueButton != null) nextDialogueButton.onClick.AddListener(OnNextDialogue);
        if (choiceAButton != null) choiceAButton.onClick.AddListener(OnChoiceASelected);
        if (choiceBButton != null) choiceBButton.onClick.AddListener(OnChoiceBSelected);

        if (choiceAText != null) choiceATextColor = choiceAText.color;
        if (choiceBText != null) choiceBTextColor = choiceBText.color;

        ApplyDialogSkin();
        ApplyEncounterTextScale();

        if (fullScreenBackground != null) fullScreenBackground.SetActive(false);
        if (dialogueRoot != null) dialogueRoot.SetActive(false);
        if (choiceRoot != null) choiceRoot.SetActive(false);
    }

    // 보스 노드에 들어섰을 때 보스의 대사를 조우자 대화창으로 보여 준다. 대사는 ENC 표의 {keyPrefix}_DLG_01.. 를 쓴다.
    public void StartBossIntro(MonsterDataSO speaker, string keyPrefix, string[] fallbackLines, System.Action onFinished)
    {
        gameObject.SetActive(true);
        dialogueIndex = 0;
        currentEncounter = null;
        introKeyPrefix = keyPrefix;
        introFallback = fallbackLines;
        introFinished = onFinished;

        if (encounterSpriteRenderer != null && speaker != null && speaker.monsterSprite != null)
            encounterSpriteRenderer.sprite = speaker.monsterSprite;
        if (encounterAnimator != null)
        {
            bool animated = speaker != null && speaker.animatorController != null;
            if (animated) encounterAnimator.runtimeAnimatorController = speaker.animatorController;
            encounterAnimator.enabled = animated;
            encounterAnimator.speed = 1f;
        }

        if (fullScreenBackground != null) fullScreenBackground.SetActive(true);
        if (dialogueRoot != null) dialogueRoot.SetActive(true);
        if (choiceRoot != null) choiceRoot.SetActive(false);

        UpdateDialogue();
    }

    void FinishBossIntro()
    {
        System.Action finished = introFinished;
        introKeyPrefix = null;
        introFallback = null;
        introFinished = null;

        if (fullScreenBackground != null) fullScreenBackground.SetActive(false);
        if (dialogueRoot != null) dialogueRoot.SetActive(false);
        if (choiceRoot != null) choiceRoot.SetActive(false);
        gameObject.SetActive(false);
        finished?.Invoke();
    }

    public void StartEvent(BiomeType currentBiome)
    {
        gameObject.SetActive(true);
        dialogueIndex = 0;
        introKeyPrefix = null;
        introFinished = null;

        // 현재 바이옴에 등장 가능한 조우자들만 필터링
        List<EncounterData> possibleEncounters = encounterDatabase
            .Where(e => e.appearBiomes.Contains(currentBiome))
            .ToList();

        if (possibleEncounters.Count > 0)
        {
            // 조건에 맞는 조우자 중 랜덤 1명 선택
            currentEncounter = possibleEncounters[Random.Range(0, possibleEncounters.Count)];
        }
        else
        {
            Debug.LogWarning("현재 바이옴에 등장 가능한 조우자가 없습니다!");
            EndEvent();
            return;
        }

        // 선택된 조우자의 외형으로 UI 교체
        if (encounterSpriteRenderer != null && currentEncounter.encounterSprite != null)
        {
            encounterSpriteRenderer.sprite = currentEncounter.encounterSprite;
        }

        if (encounterAnimator != null)
        {
            if (currentEncounter.animatorController != null)
            {
                // 전용 애니메이션이 있다면 컨트롤러를 갈아끼우고 재생
                encounterAnimator.runtimeAnimatorController = currentEncounter.animatorController;
                encounterAnimator.enabled = true;
                encounterAnimator.speed = 1f;
            }
            else
            {
                // 전용 애니메이션이 없다면 (정지 일러스트라면) 애니메이터 끄기
                encounterAnimator.enabled = false;
            }
        }

        if (fullScreenBackground != null) fullScreenBackground.SetActive(true);
        if (dialogueRoot != null) dialogueRoot.SetActive(true);
        if (choiceRoot != null) choiceRoot.SetActive(false);

        UpdateDialogue();
    }

    private void UpdateDialogue()
    {
        bool intro = introKeyPrefix != null;
        if (currentEncounter == null && !intro)
        {
            ShowChoices();
            return;
        }

        string line = GetDialogueLine(dialogueIndex);
        if (!string.IsNullOrEmpty(line))
        {
            dialogueText.text = line;
            SetNextIndicator(!string.IsNullOrEmpty(GetDialogueLine(dialogueIndex + 1)));
            return;
        }

        SetNextIndicator(false);
        if (intro)
        {
            FinishBossIntro();
            return;
        }
        if (dialogueRoot != null) dialogueRoot.SetActive(false);
        ShowChoices();
    }

    string GetDialogueLine(int index)
    {
        if (introKeyPrefix != null)
        {
            LocalizationManager.TryGetLocalized(LocalizationManager.EncounterTable, introKeyPrefix + "_DLG_" + (index + 1).ToString("D2"), out string introLine);
            if (!string.IsNullOrEmpty(introLine)) return introLine;
            return introFallback != null && index < introFallback.Length ? introFallback[index] : null;
        }

        string locLine = GetEncounterDialogue(currentEncounter.type, index);
        if (!string.IsNullOrEmpty(locLine))
            return locLine;

        if (currentEncounter.dialogues != null && index < currentEncounter.dialogues.Length)
            return currentEncounter.dialogues[index];
        return null;
    }

    // 다음 대사가 있을 때만 ▼ 를 보이고 위아래로 천천히 깜빡인다.
    void SetNextIndicator(bool visible)
    {
        if (nextIndicator == null) return;
        RectTransform rect = nextIndicator.rectTransform;
        if (!nextIndicatorBaseSaved)
        {
            nextIndicatorBasePos = rect.anchoredPosition;
            nextIndicatorBaseSaved = true;
        }

        rect.DOKill();
        nextIndicator.DOKill();
        rect.anchoredPosition = nextIndicatorBasePos;
        Color color = nextIndicator.color;
        color.a = 1f;
        nextIndicator.color = color;
        nextIndicator.gameObject.SetActive(visible);
        if (!visible) return;

        rect.DOAnchorPosY(nextIndicatorBasePos.y - NextIndicatorBob, NextIndicatorPeriod)
            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
        nextIndicator.DOFade(NextIndicatorMinAlpha, NextIndicatorPeriod)
            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
    }

    private void OnNextDialogue()
    {
        dialogueIndex++;
        UpdateDialogue();
    }

    private void ShowChoices()
    {
        if (encounterAnimator != null && encounterAnimator.enabled)
            encounterAnimator.speed = 0f; // 대화가 끝나고 선택지가 나오면 애니메이션 정지

        string locA = GetEncounterChoice(currentEncounter.type, true);
        string locB = GetEncounterChoice(currentEncounter.type, false);
        if (choiceAText != null) choiceAText.text = !string.IsNullOrEmpty(locA) ? locA : currentEncounter.choiceAText;
        if (choiceBText != null) choiceBText.text = !string.IsNullOrEmpty(locB) ? locB : currentEncounter.choiceBText;

        choiceAButton.interactable = CheckChoiceACondition(currentEncounter.type);
        choiceBButton.interactable = true;
        RefreshChoiceATextAlpha();

        if (choiceAButton != null) choiceAButton.gameObject.SetActive(true);
        if (choiceBButton != null) choiceBButton.gameObject.SetActive(true);

        if (choiceRoot != null) choiceRoot.SetActive(true);
    }

    void EnsureSkinSprites()
    {
#if UNITY_EDITOR
        if (dialogPanelSprite == null)
            dialogPanelSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Contents/06_UI/Sprites/Encounter/Encounter_Dialog_Navy.png");
#endif
    }

    void ApplyDialogSkin()
    {
        EnsureSkinSprites();

        float borderMul = Mathf.Max(0.01f, dialogBorderScale);
        ApplyPanelImage(dialogueRoot, dialogPanelSprite, borderMul);
        ApplyPanelImage(choiceRoot, dialogPanelSprite, borderMul);

        if (dialogueText != null)
            dialogueText.margin = DialogueTextMargin;

        ApplyChoiceButtonColors(choiceAButton, borderMul);
        ApplyChoiceButtonColors(choiceBButton, borderMul);
    }

    void ApplyEncounterTextScale()
    {
        if (encounterTextScaleApplied) return;
        encounterTextScaleApplied = true;

        ScaleEncounterText(dialogueText);
        ScaleEncounterText(choiceAText);
        ScaleEncounterText(choiceBText);
    }

    void ScaleEncounterText(TextMeshProUGUI label)
    {
        if (label == null) return;

        float originalSize = label.fontSize;
        label.fontSize = originalSize * encounterTextScale;

        if (!label.enableAutoSizing) return;
        label.fontSizeMax *= encounterTextScale;
        label.fontSizeMin *= encounterTextScale;
    }

    static void ApplyPanelImage(GameObject root, Sprite sprite, float borderMul)
    {
        if (root == null || sprite == null) return;
        Image image = root.GetComponent<Image>();
        if (image == null) return;
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.fillCenter = true;
        image.pixelsPerUnitMultiplier = borderMul;
    }

    static void ApplyChoiceButtonColors(Button button, float borderMul)
    {
        if (button == null) return;

        // 선택지 배경(Risk/Safe)도 대화창과 같은 테두리 두께로 보이게 한다.
        Image background = button.targetGraphic as Image;
        if (background != null && background.type == Image.Type.Sliced)
            background.pixelsPerUnitMultiplier = borderMul;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.selectedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 1f);
        button.colors = colors;
    }

    void RefreshChoiceATextAlpha()
    {
        if (choiceAText == null || choiceAButton == null) return;
        Color color = choiceATextColor;
        color.a = choiceAButton.interactable ? choiceATextColor.a : choiceATextColor.a * 0.5f;
        choiceAText.color = color;
    }

    private static string GetEncounterKeyPrefix(EncounterType type)
    {
        switch (type)
        {
            case EncounterType.Clown: return "ENC_CLOWN";
            case EncounterType.AbyssDealer: return "ENC_ABYSS_DEALER";
            case EncounterType.BlindFortuneTeller: return "ENC_BLIND_FORTUNE_TELLER";
            case EncounterType.Poacher: return "ENC_POACHER";
            case EncounterType.SacrificedGirl: return "ENC_SACRIFICED_GIRL";
            case EncounterType.Alchemist: return "ENC_ALCHEMIST";
            case EncounterType.WishWanderer: return "ENC_WISH_WANDERER";
            case EncounterType.ForgottenExplorer: return "ENC_FORGOTTEN_EXPLORER";
            case EncounterType.MadHatter: return "ENC_MAD_HATTER";
            case EncounterType.RustyCaptain: return "ENC_RUSTY_CAPTAIN";
            default: return null;
        }
    }

    private static string GetEncounterDialogue(EncounterType type, int index)
    {
        string prefix = GetEncounterKeyPrefix(type);
        if (string.IsNullOrEmpty(prefix))
            return null;

        string key = prefix + "_DLG_" + (index + 1).ToString("D2");
        LocalizationManager.TryGetLocalized(LocalizationManager.EncounterTable, key, out string value);
        return value;
    }

    private static string GetEncounterChoice(EncounterType type, bool choiceA)
    {
        string prefix = GetEncounterKeyPrefix(type);
        if (string.IsNullOrEmpty(prefix))
            return null;

        string key = prefix + (choiceA ? "_CHOICE_A" : "_CHOICE_B");
        LocalizationManager.TryGetLocalized(LocalizationManager.EncounterTable, key, out string value);
        return value;
    }

    private bool CheckChoiceACondition(EncounterType type)
    {
        switch (type)
        {
            case EncounterType.Clown: return diceManager.masterDeck.Count >= 4;
            // 여정 주사위는 희생할 수 없으므로 지울 수 있는 주사위가 2개 이상 있어야 한다.
            case EncounterType.SacrificedGirl: return diceManager.deckManager.RemovableCount() >= 2;
            default: return true;
        }
    }

    // 선택지 A: 효과는 EncounterChoiceEffects가 적용하고, 연출이 필요한 조우자(어릿광대·제물 소녀)는 여기서 연출을 띄운다.
    private void OnChoiceASelected()
    {
        if (choiceAButton != null) choiceAButton.gameObject.SetActive(false);
        if (choiceBButton != null) choiceBButton.gameObject.SetActive(false);

        bool waitCoroutine = false; // 코루틴 대기를 위한 플래그

        switch (currentEncounter.type)
        {
            case EncounterType.Clown:
                EncounterChoiceEffects.ClownA(diceManager);
                // 선택창을 4번 연속 띄우기 위해 코루틴 실행
                waitCoroutine = true;
                StartCoroutine(ClownCoatingRoutine());
                break;

            case EncounterType.AbyssDealer:
                EncounterChoiceEffects.AbyssDealerA(diceManager);
                break;

            case EncounterType.BlindFortuneTeller:
                EncounterChoiceEffects.BlindFortuneTellerA(diceManager, eightyEightDiceSO);
                break;

            case EncounterType.RustyCaptain:
                EncounterChoiceEffects.RustyCaptainA(diceManager);
                break;

            case EncounterType.Poacher:
                EncounterChoiceEffects.PoacherA(diceManager);
                break;

            case EncounterType.SacrificedGirl:
            {
                EncounterChoiceEffects.SacrificedGirlA(diceManager, sacrificedGirlFigureSO, out List<Sprite> offeredSprites, out List<Color> offeredTints);
                // 바친 주사위가 소녀에게 빨려 들어가는 연출을 보여 준 뒤 지도로 돌아간다.
                if (offeredSprites.Count > 0)
                {
                    waitCoroutine = true;
                    StartCoroutine(SacrificeRoutine(offeredSprites, offeredTints));
                }
                break;
            }

            case EncounterType.Alchemist:
                EncounterChoiceEffects.AlchemistA(diceManager);
                break;

            case EncounterType.WishWanderer:
                EncounterChoiceEffects.WishWandererA(diceManager, wishWandererFigureSO);
                break;

            case EncounterType.ForgottenExplorer:
                EncounterChoiceEffects.ForgottenExplorerA(diceManager);
                break;

            case EncounterType.MadHatter:
                EncounterChoiceEffects.MadHatterA(diceManager);
                break;
        }

        // 코루틴으로 대기해야 하는 조우자(어릿광대)가 아닐 때만 즉시 UI 갱신 및 이벤트 종료
        if (!waitCoroutine)
        {
            UpdateUIAfterChoice();
            EndEvent();
        }
    }

    // 선택지 B: 효과는 EncounterChoiceEffects가 적용한다. 연출이 필요한 B는 없다.
    private void OnChoiceBSelected()
    {
        switch (currentEncounter.type)
        {
            case EncounterType.AbyssDealer:
                EncounterChoiceEffects.AbyssDealerB(diceManager);
                break;
            case EncounterType.BlindFortuneTeller:
                EncounterChoiceEffects.BlindFortuneTellerB(diceManager);
                break;
            case EncounterType.SacrificedGirl:
                EncounterChoiceEffects.SacrificedGirlB(diceManager);
                break;
            case EncounterType.RustyCaptain:
                //전투 없이 그냥 이동
                break;
            case EncounterType.Alchemist:
                EncounterChoiceEffects.AlchemistB(diceManager);
                break;
            case EncounterType.ForgottenExplorer:
                EncounterChoiceEffects.ForgottenExplorerB(diceManager);
                break;
            case EncounterType.MadHatter:
                EncounterChoiceEffects.MadHatterB(diceManager);
                break;
        }

        UpdateUIAfterChoice();
        EndEvent();
    }

    // 제물이 된 소녀 A: 선택지 창을 내리고, 바친 주사위가 녹아 소녀에게 빨려 들어가는 연출을 본 뒤 지도로 돌아간다.
    private IEnumerator SacrificeRoutine(List<Sprite> sprites, List<Color> tints)
    {
        if (choiceRoot != null) choiceRoot.SetActive(false);
        // 선택지에서 멈췄던 소녀 애니메이션을 다시 움직여 연출 동안 살아 있게 한다.
        if (encounterAnimator != null && encounterAnimator.enabled) encounterAnimator.speed = 1f;

        Graphic girl = encounterAnimator != null ? encounterAnimator.GetComponent<Graphic>() : null;
        if (girl == null && fullScreenBackground != null) girl = fullScreenBackground.GetComponent<Graphic>();
        yield return DebuffFeedback.PlaySacrifice(sprites, tints, girl);

        UpdateUIAfterChoice();
        EndEvent();
    }

    // 어릿광대용 코팅 4번 띄우기 코루틴
    private IEnumerator ClownCoatingRoutine()
    {
        for (int i = 0; i < 4; i++)
        {
            // 1. 코팅창 열기
            diceManager.shopManager.ShowCoatingSelection(DiceType.Dark, 1.0f, new Color32(43, 42, 26, 255));

            // 패널이 활성화될 때까지 1프레임 대기
            yield return null;

            // 패널이 화면에 떠 있는 동안 무한 대기
            while (diceManager.shopManager.coatingSelectionPanel.panelRoot.activeSelf)
            {
                yield return null;
            }

            // 주사위를 선택해서 창이 닫힌 후 0.5초 대기
            yield return new WaitForSeconds(0.2f);
        }

        UpdateUIAfterChoice();
        EndEvent();
    }

    private void UpdateUIAfterChoice()
    {
        if (diceManager.currentPlayerHP > diceManager.playerMaxHP)
            diceManager.currentPlayerHP = diceManager.playerMaxHP;

        if (diceManager.ui != null)
            diceManager.ui.UpdateGoldUI(diceManager.shopManager.currentGold);
        if (GoldCounter.Instance != null)
            GoldCounter.Instance.SetGold(diceManager.shopManager.currentGold);

        diceManager.ForceUpdateUI();
    }

    private void EndEvent()
    {
        if (fullScreenBackground != null) fullScreenBackground.SetActive(false);
        if (choiceRoot != null) choiceRoot.SetActive(false);
        gameObject.SetActive(false);

        // 조우가 끝나면 지도로 돌아간다(조우자는 지도의 조우 칸에서만 나온다).
        if (diceManager != null)
            diceManager.ReturnToMap();
    }
}
