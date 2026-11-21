// 캐릭터 선택 화면 공통 문구. 키는 UI_StringTable, 기본값은 한국어(키가 없을 때 표시).
// 캐릭터별 문구는 CharacterSelectCatalog 에셋에 있다.
public static class CharacterSelectLabels
{
    private const string Ui = LocalizationManager.UiTable;

    public static readonly LocText OverviewTitle = new LocText(Ui, "UI_CHARSELECT_OVERVIEW_TITLE", "여정을 함께할 귀인을 선택하시오");
    public static readonly LocText OverviewSubtitle = new LocText(Ui, "UI_CHARSELECT_OVERVIEW_SUBTITLE", "귀인은 고유한 피규어와 시작 주사위 세트를 가지고 있습니다");
    public static readonly LocText Hp = new LocText(Ui, "UI_CHARSELECT_HP", "체력");
    public static readonly LocText Gold = new LocText(Ui, "UI_CHARSELECT_GOLD", "골드");
    public static readonly LocText DiceCount = new LocText(Ui, "UI_CHARSELECT_DICE_COUNT", "×{0}");
    public static readonly LocText BaseHp = new LocText(Ui, "UI_CHARSELECT_BASE_HP", "기초 체력");
    public static readonly LocText BaseGold = new LocText(Ui, "UI_CHARSELECT_BASE_GOLD", "기초 골드");
    public static readonly LocText Play = new LocText(Ui, "UI_CHARSELECT_PLAY", "플레이");
    public static readonly LocText StartDeck = new LocText(Ui, "UI_CHARSELECT_START_DECK", "시작 덱 · {0}개");
    public static readonly LocText UniqueDice = new LocText(Ui, "UI_CHARSELECT_UNIQUE_DICE", "고유 주사위");
    public static readonly LocText StartFigure = new LocText(Ui, "UI_CHARSELECT_START_FIGURE", "시작 피규어 · 2개 중 1개 선택");
    public static readonly LocText Provisional = new LocText(Ui, "UI_CHARSELECT_PROVISIONAL", "가칭 · 수치 제안");
    public static readonly LocText UnlockHeader = new LocText(Ui, "UI_CHARSELECT_UNLOCK_HEADER", "해금 조건");
    public static readonly LocText Back = new LocText(Ui, "UI_CHARSELECT_BACK", "목록");
    // 기존 키(UI.csv에 이미 있음)
    public static readonly LocText Cancel = new LocText(Ui, "UI_CHARSELECT_CANCEL", "취소");
    public static readonly LocText Start = new LocText(Ui, "UI_CHARSELECT_START", "시작");

    public static readonly LocText[] NewKeys =
    {
        OverviewTitle, OverviewSubtitle, Hp, Gold, DiceCount, BaseHp, BaseGold, Play,
        StartDeck, UniqueDice, StartFigure, Provisional, UnlockHeader, Back
    };

    public static string Format(LocText text, params object[] args)
    {
        string pattern = text.Get();
        try { return string.Format(pattern, args); }
        catch (System.FormatException) { return string.Format(text.fallback, args); }
    }
}
