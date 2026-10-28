using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 튜토리얼 모드에서만 MainScene_Map에 런타임으로 생성하는 안내 오버레이(임시·최소 구현).
// 딤이나 입력 차단 없이 말풍선과 강조 테두리만 보여 준다. 행동 단계는 해당 행동이 일어나면 넘어가고,
// 순서를 틀려도 막히지 않도록 뒤 단계의 행동이 먼저 일어나면 그 단계로 건너뛴다.
public class TutorialOverlay : MonoBehaviour
{
    enum Step { Intro, MapRoll, MapResult, GoToBattle, Roll, Keep, Reroll, Score, Finish, Loot, Outro }

    public RectTransform root;
    public RectTransform bubble;
    public TextMeshProUGUI bubbleText;
    public Image highlight;
    public Button nextButton;
    public TextMeshProUGUI nextLabel;
    public Button skipButton;
    public TextMeshProUGUI skipLabel;

    const float BubbleMargin = 40f;
    const float HighlightPad = 12f;
    const float TurnEndCheckDelay = 0.6f;

    DiceManager dm;
    Step step;
    JourneyRollResult lastJourney;
    RectTransform target;
    bool waitingTurnEnd;
    float finishClickedAt;
    bool bound;

    public static void Spawn(DiceManager diceManager)
    {
        var prefab = Resources.Load<TutorialOverlay>("Tutorial/TutorialOverlay");
        if (prefab == null)
        {
            Debug.LogWarning("[Tutorial] Resources/Tutorial/TutorialOverlay 프리팹을 찾지 못했습니다.");
            return;
        }
        Instantiate(prefab).Bind(diceManager);
    }

    void Bind(DiceManager diceManager)
    {
        dm = diceManager;
        nextButton.onClick.AddListener(OnNext);
        skipButton.onClick.AddListener(EndTutorial);
        nextLabel.text = Ui("UI_TUTORIAL_NEXT", "다음");
        skipLabel.text = Ui("UI_TUTORIAL_SKIP", "건너뛰기");
        if (dm.ui != null)
        {
            if (dm.ui.rollButton != null) dm.ui.rollButton.onClick.AddListener(OnRollClicked);
            if (dm.ui.finishButton != null) dm.ui.finishButton.onClick.AddListener(OnFinishClicked);
        }
        MapPanel.JourneyRolled += OnJourneyRolled;
        DiceManager.MapCombatStarted += OnCombatStarted;
        LootSelectionPanel.LootChosen += OnLootChosen;
        Dice.OnDiceStateChanged += OnDiceChanged;
        bound = true;
        Show(Step.Intro);
    }

    void OnDestroy()
    {
        if (!bound) return;
        MapPanel.JourneyRolled -= OnJourneyRolled;
        DiceManager.MapCombatStarted -= OnCombatStarted;
        LootSelectionPanel.LootChosen -= OnLootChosen;
        Dice.OnDiceStateChanged -= OnDiceChanged;
        if (dm != null && dm.ui != null)
        {
            if (dm.ui.rollButton != null) dm.ui.rollButton.onClick.RemoveListener(OnRollClicked);
            if (dm.ui.finishButton != null) dm.ui.finishButton.onClick.RemoveListener(OnFinishClicked);
        }
    }

    void Update()
    {
        if (dm == null) return;

        // 전리품 창이 뜨면(승리) 어느 단계에서든 전리품 안내로 넘어간다.
        if (step < Step.Loot && LootSelectionPanel.IsPanelOpen)
        {
            Show(Step.Loot);
            return;
        }

        // 끝내기 후 몬스터가 살아남아 다음 턴이 오면 굴리기부터 다시 안내한다.
        if (waitingTurnEnd && Time.unscaledTime - finishClickedAt > TurnEndCheckDelay
            && !dm.isCalculating && !dm.isStageClearing && dm.enemy != null && !dm.enemy.IsDead)
        {
            Show(Step.Roll);
        }
    }

    void LateUpdate()
    {
        PlaceHighlight();
        PlaceBubble();
    }

    void Show(Step next)
    {
        step = next;
        waitingTurnEnd = false;
        bool hasNext = false;
        target = null;
        string text;

        switch (next)
        {
            case Step.Intro:
                text = Ui("UI_TUTORIAL_STEP_01", "트리키 요트에 온 걸 환영합니다. 기본 조작만 빠르게 알려 드릴게요.");
                hasNext = true;
                break;
            case Step.MapRoll:
                text = Ui("UI_TUTORIAL_STEP_02", "지도에서는 여정 주사위를 굴려 앞으로 나아갑니다. 굴리기 버튼을 눌러 보세요.");
                target = dm.mapPanel != null && dm.mapPanel.journeyRollButton != null ? (RectTransform)dm.mapPanel.journeyRollButton.transform : null;
                break;
            case Step.MapResult:
                text = JourneyResultText(lastJourney);
                target = dm.mapPanel != null && dm.mapPanel.journeyResultImage != null ? dm.mapPanel.journeyResultImage.rectTransform : null;
                hasNext = true;
                break;
            case Step.GoToBattle:
                text = Ui("UI_TUTORIAL_STEP_03", "지도 이동은 자동이 아니에요. 빛나는 노드 중 전투 노드를 눌러 이동하면 전투가 시작됩니다.");
                break;
            case Step.Roll:
                text = Ui("UI_TUTORIAL_STEP_04", "굴리기를 눌러 주사위 5개를 굴리세요.");
                target = dm.ui != null && dm.ui.rollButton != null ? (RectTransform)dm.ui.rollButton.transform : null;
                break;
            case Step.Keep:
                text = Ui("UI_TUTORIAL_STEP_05", "남기고 싶은 주사위를 클릭해 위 칸에 올리세요.");
                target = dm.keepSlotParent as RectTransform;
                break;
            case Step.Reroll:
                text = Ui("UI_TUTORIAL_STEP_06", "킵하지 않은 주사위만 다시 굴립니다. 횟수는 제한이 있어요.");
                target = dm.ui != null && dm.ui.roundPlaysText != null ? dm.ui.roundPlaysText.rectTransform : null;
                hasNext = true;
                break;
            case Step.Score:
                text = Ui("UI_TUTORIAL_STEP_07", "족보로 칩과 배수가 정해지고, 칩 × 배수가 피해량이 됩니다.");
                target = dm.ui != null && dm.ui.chipsSumText != null ? dm.ui.chipsSumText.transform.parent as RectTransform : null;
                hasNext = true;
                break;
            case Step.Finish:
                text = Ui("UI_TUTORIAL_STEP_08", "끝내기를 누르면 킵한 주사위로 공격합니다.");
                target = dm.ui != null && dm.ui.finishButton != null ? (RectTransform)dm.ui.finishButton.transform : null;
                break;
            case Step.Loot:
                text = Ui("UI_TUTORIAL_STEP_09", "승리! 보상 하나를 고르세요.");
                break;
            default:
                text = Ui("UI_TUTORIAL_STEP_10", "기본 조작은 여기까지입니다. 이제 실전에서 만나요!");
                hasNext = true;
                break;
        }

        bubbleText.text = text;
        nextButton.gameObject.SetActive(hasNext);
    }

    string JourneyResultText(JourneyRollResult result)
    {
        switch (result.outcome)
        {
            case JourneyOutcome.Damage:
                return Ui("UI_TUTORIAL_STEP_02_DAMAGE", "해골 면: 체력이 줄어들고, 한 번 더 굴려야 합니다.");
            case JourneyOutcome.Heal:
                return Ui("UI_TUTORIAL_STEP_02_HEAL", "하트 면: 체력을 회복하고, 한 번 더 굴려야 합니다.");
            case JourneyOutcome.Gold:
                return Ui("UI_TUTORIAL_STEP_02_GOLD", "코인 면: 골드를 얻고, 한 번 더 굴려야 합니다.");
            default:
                return LocalizationManager.GetUi("UI_TUTORIAL_STEP_02_MOVE", "{0}칸까지 이동할 수 있어요. 빛나는 노드를 눌러 이동합니다.", result.amount);
        }
    }

    void OnNext()
    {
        switch (step)
        {
            case Step.Intro: Show(Step.MapRoll); break;
            case Step.MapResult: Show(lastJourney.outcome == JourneyOutcome.Move ? Step.GoToBattle : Step.MapRoll); break;
            case Step.Reroll: Show(Step.Score); break;
            case Step.Score: Show(Step.Finish); break;
            case Step.Outro: EndTutorial(); break;
        }
    }

    void OnJourneyRolled(JourneyRollResult result)
    {
        if (step >= Step.Roll) return;
        lastJourney = result;
        Show(Step.MapResult);
    }

    void OnCombatStarted()
    {
        if (step < Step.Roll) Show(Step.Roll);
    }

    void OnRollClicked()
    {
        if (step == Step.Roll) Show(Step.Keep);
    }

    void OnDiceChanged()
    {
        if (step == Step.Keep && KeptCount() >= 1) Show(Step.Reroll);
    }

    void OnFinishClicked()
    {
        if (step < Step.Roll || step > Step.Finish) return;
        Show(Step.Finish);
        waitingTurnEnd = true;
        finishClickedAt = Time.unscaledTime;
    }

    void OnLootChosen()
    {
        if (step == Step.Loot) Show(Step.Outro);
    }

    int KeptCount()
    {
        int count = 0;
        var dice = dm.activeDiceList;
        if (dice == null) return 0;
        foreach (Dice die in dice)
        {
            if (die != null && die.isKept) count++;
        }
        return count;
    }

    void EndTutorial()
    {
        TutorialMode.End();
        SceneManager.LoadScene("Lobby");
    }

    // 강조 테두리를 대상 크기에 맞춰 덮고 알파를 천천히 깜빡인다.
    void PlaceHighlight()
    {
        Rect area = default;
        bool visible = target != null && target.gameObject.activeInHierarchy && TryLocalRect(target, out area);
        if (highlight.gameObject.activeSelf != visible) highlight.gameObject.SetActive(visible);
        if (!visible) return;

        RectTransform frame = highlight.rectTransform;
        frame.anchoredPosition = area.center;
        frame.sizeDelta = area.size + Vector2.one * HighlightPad * 2f;
        Color color = highlight.color;
        color.a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3f);
        highlight.color = color;
    }

    // 말풍선은 화면 아래 가운데. 강조 대상과 겹치면 위로 옮긴다.
    void PlaceBubble()
    {
        Rect screen = root.rect;
        Vector2 size = bubble.rect.size;
        var bottom = new Rect(-size.x * 0.5f, screen.yMin + BubbleMargin, size.x, size.y);
        bool overlaps = highlight.gameObject.activeSelf && TryLocalRect(target, out Rect area) && area.Overlaps(bottom);
        float y = overlaps ? screen.yMax - BubbleMargin - size.y * 0.5f : bottom.center.y;
        bubble.anchoredPosition = new Vector2(0f, y);
    }

    // 다른 캔버스의 RectTransform을 이 오버레이 루트 기준 사각형으로 바꾼다.
    bool TryLocalRect(RectTransform other, out Rect rect)
    {
        rect = default;
        if (other == null) return false;
        Canvas canvas = other.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
        var corners = new Vector3[4];
        other.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < 4; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPoint, null, out Vector2 local)) return false;
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
        rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return true;
    }

    static string Ui(string key, string fallback) => LocalizationManager.GetUi(key, fallback);
}
