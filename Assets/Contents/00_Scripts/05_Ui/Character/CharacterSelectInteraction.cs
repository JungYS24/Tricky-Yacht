using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

// 캐릭터 선택 씬의 흐름만 맡는다: 목록 ↔ 상세 전환, 이전/다음 순환, 키 입력, 시작 요청.
// 화면 채우기는 CharacterSelectListView / CharacterDetailView, 게임 시작은 기존 CharacterApply가 한다.
public class CharacterSelectInteraction : MonoBehaviour
{
    [SerializeField] private CharacterSelectCatalog catalog;
    [SerializeField] private CharacterSelectListView listView;
    [SerializeField] private CharacterDetailView detailView;
    [SerializeField] private CharacterApply apply;

    [Header("전환")]
    [SerializeField] private CanvasGroup listGroup;
    [SerializeField] private CanvasGroup detailGroup;
    [SerializeField, Range(0.05f, 0.5f)] private float fadeDuration = 0.18f;
    [SerializeField] private float slideDistance = 32f;

    [Tooltip("씬 진입 시 선택된 캐릭터(카탈로그 순서). 0 = 폰 바르한")]
    [SerializeField] private int defaultIndex = 0;

    // 모든 캐릭터의 시작 골드. 게임 시작 시 DiceManager도 같은 값을 쓴다.
    public static int StartingGold => BalanceConfig.Current.startGold;

    public int SelectedIndex { get; private set; }
    public bool IsDetailOpen { get; private set; }

    private CanvasGroup contentGroup;
    private Vector2 contentPosition;

    private void Start()
    {
        listView.Bind(catalog);
        listView.CardClicked += OpenDetail;

        detailView.BackButton.onClick.AddListener(ShowList);
        detailView.PreviousButton.onClick.AddListener(() => Step(-1));
        detailView.NextButton.onClick.AddListener(() => Step(1));

        if (detailView.Content != null)
        {
            contentPosition = detailView.Content.anchoredPosition;
            if (!detailView.Content.TryGetComponent(out contentGroup))
                contentGroup = detailView.Content.gameObject.AddComponent<CanvasGroup>();
        }

        Select(Mathf.Clamp(defaultIndex, 0, catalog.Count - 1));
        SetVisible(listGroup, true, instant: true);
        SetVisible(detailGroup, false, instant: true);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        // 시작 확인 창이 떠 있으면 ESC로 창만 닫는다.
        if (apply.IsConfirmOpen)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
                apply.OnFinalApply(false);
            return;
        }

        bool left = keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame;
        bool right = keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame;
        bool confirm = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;

        if (IsDetailOpen)
        {
            if (left) Step(-1);
            else if (right) Step(1);
            else if (keyboard.escapeKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame) ShowList();
            else if (confirm && CurrentEntry.character != null && CurrentEntry.character.IsUnlocked) apply.OnApply();
        }
        else
        {
            if (left) Select(Wrap(SelectedIndex - 1));
            else if (right) Select(Wrap(SelectedIndex + 1));
            else if (confirm) OpenDetail(SelectedIndex);
        }
    }

    private CharacterSelectEntry CurrentEntry => catalog.entries[SelectedIndex];

    public void OpenDetail(int index)
    {
        Select(index);
        detailView.Bind(CurrentEntry, SelectedIndex, catalog);
        IsDetailOpen = true;
        SetVisible(listGroup, false, instant: false);
        SetVisible(detailGroup, true, instant: false);
    }

    public void ShowList()
    {
        if (!IsDetailOpen)
            return;

        IsDetailOpen = false;
        listView.SetSelected(SelectedIndex);
        SetVisible(detailGroup, false, instant: false);
        SetVisible(listGroup, true, instant: false);
    }

    // 상세 화면에서 이전(-1)/다음(+1) 캐릭터로 순환 이동한다.
    public void Step(int direction)
    {
        Select(Wrap(SelectedIndex + direction));
        detailView.Bind(CurrentEntry, SelectedIndex, catalog);
        PlaySlide(direction);
    }

    // 목록 강조와 CharacterApply의 시작 대상(잠긴 캐릭터는 시작 불가)을 함께 맞춘다.
    private void Select(int index)
    {
        SelectedIndex = index;
        listView.SetSelected(index);
        apply.SetCharacter(CurrentEntry.character);
    }

    private int Wrap(int index)
    {
        int count = catalog.Count;
        return ((index % count) + count) % count;
    }

    private void PlaySlide(int direction)
    {
        if (detailView.Content == null || contentGroup == null)
            return;

        RectTransform content = detailView.Content;
        content.DOKill();
        contentGroup.DOKill();

        content.anchoredPosition = contentPosition + new Vector2(slideDistance * direction, 0f);
        contentGroup.alpha = 0f;
        content.DOAnchorPos(contentPosition, fadeDuration).SetEase(Ease.OutCubic).SetLink(content.gameObject);
        contentGroup.DOFade(1f, fadeDuration).SetLink(content.gameObject);
    }

    private void SetVisible(CanvasGroup group, bool visible, bool instant)
    {
        if (group == null)
            return;

        group.DOKill();
        group.interactable = visible;
        group.blocksRaycasts = visible;

        if (visible)
            group.gameObject.SetActive(true);

        if (instant)
        {
            group.alpha = visible ? 1f : 0f;
            group.gameObject.SetActive(visible);
            return;
        }

        group.DOFade(visible ? 1f : 0f, fadeDuration)
            .SetLink(group.gameObject)
            .OnComplete(() => { if (!visible) group.gameObject.SetActive(false); });
    }
}
