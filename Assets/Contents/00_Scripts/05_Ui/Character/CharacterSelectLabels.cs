// 캐릭터 선택 화면 공통 문구. 키는 UI_StringTable, 기본값은 한국어(키가 없을 때 표시).
// 캐릭터별 문구는 CharacterSelectCatalog 에셋에 있다.
public static class CharacterSelectLabels
{
    private const string Ui = LocalizationManager.UiTable;

    public static readonly LocText OverviewSection = new LocText(Ui, "UI_CHARSELECT_OVERVIEW_SECTION", "한눈에 보기");
    public static readonly LocText OverviewTitle = new LocText(Ui, "UI_CHARSELECT_OVERVIEW_TITLE", "캐릭터마다 시작 덱이 다르다");
    public static readonly LocText OverviewSubtitle = new LocText(Ui, "UI_CHARSELECT_OVERVIEW_SUBTITLE", "모든 덱은 여정 주사위 1개 + 캐릭터 주사위 11개 = 12개 · 시작 피규어는 2개 중 1개를 골라 시작");
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
    public static readonly LocText Footer = new LocText(Ui, "UI_CHARSELECT_FOOTER", "TRICKY YACHT · 플레이어블 캐릭터");
    // 기존 키(UI.csv에 이미 있음)
    public static readonly LocText Cancel = new LocText(Ui, "UI_CHARSELECT_CANCEL", "취소");
    public static readonly LocText Start = new LocText(Ui, "UI_CHARSELECT_START", "시작");

    public static readonly LocText[] NewKeys =
    {
        OverviewSection, OverviewTitle, OverviewSubtitle, Hp, Gold, DiceCount, BaseHp, BaseGold, Play,
        StartDeck, UniqueDice, StartFigure, Provisional, UnlockHeader, Back, Footer
    };

    public static string Format(LocText text, params object[] args)
    {
        string pattern = text.Get();
        try { return string.Format(pattern, args); }
        catch (System.FormatException) { return string.Format(text.fallback, args); }
    }
}
