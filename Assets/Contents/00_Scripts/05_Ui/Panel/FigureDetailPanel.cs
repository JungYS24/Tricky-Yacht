using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;

public class FigureDetailPanel : MonoBehaviour
{
    //다른 스크립트(주사위, 매니저 등)에서 패널 오픈 여부를 확인할 수 있는 정적 변수
    public static bool IsPanelOpen { get; private set; } = false;

    public GameObject panelRoot;
    public Image figureImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descText;

    [Header("도감용 UI")]
    public TextMeshProUGUI locationText;

    [Header("도감 오른쪽 페이지 (비어 있으면 인벤토리 팝업은 그대로)")]
    public Image figureShadow;
    public GameObject dropMonsterRoot;
    public TextMeshProUGUI dropMonsterLabel;
    public TextMeshProUGUI dropMonsterName;
    public TextMeshProUGUI dropMonsterBiome;
    public Image dropMonsterImage;
    public Image dropMonsterShadow;
    public Button dropMonsterPrev;
    public Button dropMonsterNext;
    public GameObject relatedSynergyRoot;
    public TextMeshProUGUI relatedSynergyLabel;
    public RectTransform relatedSynergyContent;
    public GameObject synergyCardTemplate;
    public Sprite countBadgeSprite;
    [NonSerialized] public Action<SynergyData> synergyClicked;

    // 도감 오른쪽 아이콘 박스. 100px 그림은 3배, 그보다 큰 그림(바나나)은 이 박스 안에 맞춘다.
    public const float BookFigureIconBox = 300f;
    // 128px 몬스터를 2배(256)로 키우면 양피지 아래로 나간다. 페이지에 남는 정수 배는 128이다.
    public const float BookMonsterIconBox = 128f;

    [Header("인벤토리 팝업 그림 무대 (비어 있으면 기존 배치)")]
    public RectTransform figureStage;
    public RectTransform figureStageShadow;

    // 인벤토리 팝업 무대의 그림 박스. 100px 그림은 3배, 64px 그림은 5배, 그보다 큰 그림(바나나)은 이 박스 안에 맞춘다.
    public const float PopupFigureIconBox = 360f;

    public Button leftButton;
    public Button rightButton;
    public Button closeButton;

    [Header("글자 색 덮어쓰기 (양피지 배경용, 끄면 기존 색)")]
    public bool useCustomTextColors = false;
    public Color customTextColor = new Color(0.29f, 0.18f, 0.1f, 1f);
    public Color customSubTextColor = new Color(0.45f, 0.32f, 0.2f, 1f);
    public Color customLockedTextColor = new Color(0.5f, 0.44f, 0.37f, 1f);

    // 보유 중인 피규어 리스트와 현재 보고 있는 피규어 인덱스
    private List<FigureItemSO> currentOwnedFigures;
    private int currentIndex;
    private readonly List<MonsterDrop> currentDrops = new List<MonsterDrop>();
    private int dropIndex;
    private static Dictionary<FigureItemSO, List<MonsterDrop>> dropsByFigure;

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
        if (leftButton != null) leftButton.onClick.AddListener(ShowPrevious);
        if (rightButton != null) rightButton.onClick.AddListener(ShowNext);
        if (dropMonsterPrev != null) dropMonsterPrev.onClick.AddListener(() => StepDrop(-1));
        if (dropMonsterNext != null) dropMonsterNext.onClick.AddListener(() => StepDrop(1));

        panelRoot.SetActive(false); // 시작 시 패널 숨기기
        IsPanelOpen = false;        // 상태 초기화
    }

    public void OpenPanel(List<FigureItemSO> ownedFigures, FigureItemSO selectedFigure)
    {
        if (ownedFigures == null || ownedFigures.Count == 0) return;

        currentOwnedFigures = ownedFigures;
        currentIndex = currentOwnedFigures.IndexOf(selectedFigure);

        IsPanelOpen = true; // 패널이 열렸음을 알림 (주사위/버튼 조작 차단 시작)
        UpdateUI();
        panelRoot.SetActive(true);
    }

    private string GetBiomeDisplayName(BiomeType biome)
    {
        return LocalizationManager.GetBiomeDisplayName(biome);
    }


    private void UpdateUI()
    {
        if (currentIndex < 0 || currentIndex >= currentOwnedFigures.Count) return;

        FigureItemSO currentFigure = currentOwnedFigures[currentIndex];
        int figureState = CollectionDataManager.Instance != null
            ? CollectionDataManager.Instance.GetFigureState(currentFigure)
            : 0;
        bool isUnlocked = figureState == 2;
        bool isEncountered = figureState >= 1;

        string biomeNames = string.Join(", ", currentFigure.sourceBiomes.Select(b => GetBiomeDisplayName(b)));
        string biomeText = LocalizationManager.GetUi("UI_BIOME_SOURCES", "획득 바이옴 : {0}", biomeNames);

        if (isUnlocked)
        {
            if (figureImage != null)
            {
                figureImage.sprite = currentFigure.icon;
                figureImage.color = Color.white;
            }
            if (nameText != null)
            {
                nameText.text = LocalizationManager.GetItemDisplayName(currentFigure);
                nameText.color = Color.white;
            }
            if (descText != null)
            {
                descText.text = LocalizationManager.GetItemDescription(currentFigure);
                descText.color = Color.white;
            }
            if (locationText != null)
            {
                locationText.text = biomeText;
                locationText.color = Color.green;
            }
        }
        else if (isEncountered)
        {
            if (figureImage != null)
            {
                figureImage.sprite = currentFigure.icon;
                figureImage.color = Color.gray;
            }
            if (nameText != null)
            {
                nameText.text = LocalizationManager.GetItemDisplayName(currentFigure);
                nameText.color = Color.green;
            }
            if (descText != null)
            {
                descText.text = LocalizationManager.GetItemDescription(currentFigure);
                descText.color = Color.white;
            }
            if (locationText != null)
            {
                locationText.text = biomeText;
                locationText.color = Color.green;
            }
        }
        else
        {
            if (figureImage != null)
            {
                figureImage.sprite = currentFigure.icon;
                figureImage.color = new Color(0f, 0f, 0f, 0.7f);
            }
            if (nameText != null)
            {
                nameText.text = LocalizationManager.GetUi("UI_UNKNOWN", "???");
                nameText.color = new Color(0.4f, 0.4f, 0.4f, 1f);
            }
            if (descText != null)
            {
                descText.text = LocalizationManager.GetUi("UI_UNKNOWN", "???");
                descText.color = new Color(0.4f, 0.4f, 0.4f, 1f);
            }
            if (locationText != null)
            {
                locationText.text = biomeText;
                locationText.color = new Color(0.4f, 0.4f, 0.4f, 1f);
            }
        }

        if (useCustomTextColors)
            ApplyCustomTextColors(isEncountered);

        RefreshBookSections(currentFigure, isEncountered);
        FitPopupStage(figureImage, figureStage, figureStageShadow, PopupFigureIconBox);

        // 첫 번째거나 마지막 피규어면 화살표 비활성화 (버튼이 있을 때만 실행)
        if (leftButton != null)
            leftButton.interactable = (currentIndex > 0);

        if (rightButton != null)
            rightButton.interactable = (currentIndex < currentOwnedFigures.Count - 1);
    }

    private void ApplyCustomTextColors(bool isEncountered)
    {
        Color mainColor = isEncountered ? customTextColor : customLockedTextColor;
        Color subColor = isEncountered ? customSubTextColor : customLockedTextColor;

        if (nameText != null) nameText.color = mainColor;
        if (descText != null) descText.color = mainColor;
        if (locationText != null) locationText.color = subColor;
    }

    // 이전 피규어 보기
    private void ShowPrevious()
    {
        if (currentIndex > 0)
        {
            currentIndex--;
            UpdateUI();
        }
    }

    // 다음 피규어 보기
    private void ShowNext()
    {
        if (currentIndex < currentOwnedFigures.Count - 1)
        {
            currentIndex++;
            UpdateUI();
        }
    }

    public void ClosePanel()
    {
        IsPanelOpen = false; // 패널이 닫혔음을 알림 (조작 차단 해제)
        panelRoot.SetActive(false);
    }

    // 도감 인스턴스에만 드롭 몬스터와 관련 시너지를 채운다.
    private void RefreshBookSections(FigureItemSO figure, bool isEncountered)
    {
        if (dropMonsterRoot == null && relatedSynergyRoot == null) return;

        FitSpriteBox(figureImage, BookFigureIconBox);
        PlaceShadow(figureShadow, figureImage);

        bool showExtra = isEncountered;
        if (dropMonsterRoot != null) dropMonsterRoot.SetActive(showExtra);
        if (relatedSynergyRoot != null) relatedSynergyRoot.SetActive(showExtra);
        if (!showExtra)
        {
            LayoutBookDescription();
            return;
        }

        if (dropMonsterLabel != null)
            dropMonsterLabel.text = LocalizationManager.GetUi("UI_DROP_MONSTER", "드롭 몬스터");
        if (relatedSynergyLabel != null)
            relatedSynergyLabel.text = LocalizationManager.GetUi("UI_RELATED_SYNERGY", "관련 시너지");

        currentDrops.Clear();
        currentDrops.AddRange(GetDrops(figure));
        dropIndex = 0;
        bool hasDrop = currentDrops.Count > 0;
        if (dropMonsterRoot != null) dropMonsterRoot.SetActive(hasDrop);
        if (relatedSynergyRoot != null)
        {
            var rect = (RectTransform)relatedSynergyRoot.transform;
            var position = rect.anchoredPosition;
            position.x = hasDrop ? 160f : 0f;
            rect.anchoredPosition = position;
        }
        ShowDrop();
        ShowRelatedSynergies(figure);
        LayoutBookDescription();
    }

    private void StepDrop(int direction)
    {
        if (currentDrops.Count <= 1) return;
        dropIndex = (dropIndex + direction + currentDrops.Count) % currentDrops.Count;
        ShowDrop();
    }

    private void ShowDrop()
    {
        if (dropMonsterRoot == null || !dropMonsterRoot.activeSelf || currentDrops.Count == 0) return;

        MonsterDrop drop = currentDrops[dropIndex];
        if (dropMonsterName != null) dropMonsterName.text = LocalizationManager.GetMonsterDisplayName(drop.monster);
        if (dropMonsterBiome != null)
        {
            string grade = LocalizationManager.GetUi(
                drop.boss ? "UI_MONSTER_GRADE_BOSS" : "UI_MONSTER_GRADE_NORMAL",
                drop.boss ? "보스" : "일반");
            string biomes = drop.biomeNames.Count > 0 ? string.Join(", ", drop.biomeNames) : "-";
            dropMonsterBiome.text = LocalizationManager.GetUi("UI_MONSTER_APPEARANCE", "출현 바이옴 : {0} · {1}", biomes, grade);
        }
        if (dropMonsterImage != null)
        {
            dropMonsterImage.sprite = drop.monster.monsterSprite;
            dropMonsterImage.enabled = drop.monster.monsterSprite != null;
            FitSpriteBox(dropMonsterImage, BookMonsterIconBox);
            PlaceShadow(dropMonsterShadow, dropMonsterImage);
        }
        bool many = currentDrops.Count > 1;
        if (dropMonsterPrev != null) dropMonsterPrev.gameObject.SetActive(many);
        if (dropMonsterNext != null) dropMonsterNext.gameObject.SetActive(many);
    }

    private void ShowRelatedSynergies(FigureItemSO figure)
    {
        if (relatedSynergyContent == null || synergyCardTemplate == null) return;

        for (int i = relatedSynergyContent.childCount - 1; i >= 0; i--)
        {
            GameObject child = relatedSynergyContent.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }

        if (SynergyManager.Instance == null) return;
        var owned = CollectOwnedFigures();
        foreach (SynergyData synergy in SynergyManager.Instance.GetAllSynergies())
        {
            if (synergy == null || synergy.RequiredFigures == null || !synergy.RequiredFigures.Contains(figure)) continue;

            GameObject card = Instantiate(synergyCardTemplate, relatedSynergyContent);
            card.SetActive(true);
            Image icon = card.transform.Find("Icon").GetComponent<Image>();
            icon.sprite = synergy.icon;
            icon.enabled = synergy.icon != null;
            icon.preserveAspect = true;
            int required = synergy.RequiredFigures.Count;
            int have = synergy.RequiredFigures.Count(owned.Contains);
            card.transform.Find("Badge/Text").GetComponent<TextMeshProUGUI>().text = have + "/" + required;
            Transform nameLabel = card.transform.Find("Name");
            if (nameLabel != null) nameLabel.gameObject.SetActive(false);

            SynergyData captured = synergy;
            Button button = card.GetComponent<Button>();
            button.onClick.AddListener(() => synergyClicked?.Invoke(captured));
        }

        LayoutSynergyCards();
    }

    // 칸을 제목 아래 가운데로 놓고, 너비를 넘치면 다음 줄로 넘긴다.
    private void LayoutSynergyCards()
    {
        if (relatedSynergyContent == null) return;
        float cell = synergyCardTemplate != null
            ? ((RectTransform)synergyCardTemplate.transform).sizeDelta.x
            : 96f;
        if (cell < 1f) cell = 96f;
        const float spacing = 12f;
        float width = relatedSynergyContent.rect.width;
        if (width < cell) width = relatedSynergyContent.sizeDelta.x;
        if (width < cell) width = cell;
        int columns = Mathf.Max(1, Mathf.FloorToInt((width + spacing) / (cell + spacing)));

        var cards = new List<RectTransform>();
        for (int i = 0; i < relatedSynergyContent.childCount; i++)
        {
            RectTransform child = relatedSynergyContent.GetChild(i) as RectTransform;
            if (child != null && child.gameObject.activeSelf) cards.Add(child);
        }

        int rows = Mathf.CeilToInt(cards.Count / (float)columns);
        for (int index = 0; index < cards.Count; index++)
        {
            int row = index / columns;
            int column = index % columns;
            int rowCount = row == rows - 1 ? cards.Count - row * columns : columns;
            float rowWidth = rowCount * cell + (rowCount - 1) * spacing;
            float x = -rowWidth * 0.5f + cell * 0.5f + column * (cell + spacing);
            float y = -cell * 0.5f - row * (cell + spacing);
            RectTransform card = cards[index];
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 1f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(cell, cell);
            card.anchoredPosition = new Vector2(x, y);
        }
    }

    private static List<MonsterDrop> GetDrops(FigureItemSO figure)
    {
        EnsureDropIndex();
        if (dropsByFigure != null && dropsByFigure.TryGetValue(figure, out List<MonsterDrop> found))
            return found;
        return new List<MonsterDrop>();
    }

    // 피규어를 떨어뜨리는 몬스터와 출현 바이옴은 바이옴 데이터에 연결된 몬스터 SO에서 역으로 모은다.
    private static void EnsureDropIndex()
    {
        if (dropsByFigure != null) return;
        dropsByFigure = new Dictionary<FigureItemSO, List<MonsterDrop>>();

        DiceManager diceManager = DiceManager.Instance;
        if (diceManager == null || diceManager.biomeList == null) return;

        foreach (BiomeDataSO biome in diceManager.biomeList)
        {
            if (biome == null) continue;
            string biomeName = LocalizationManager.GetBiomeDisplayName(biome.biomeType);
            if (biome.biomeMonsters != null)
            {
                foreach (MonsterDataSO monster in biome.biomeMonsters)
                    AddDrop(monster, biomeName, false);
            }
            AddDrop(biome.bossMonster, biomeName, true);
        }
    }

    private static void AddDrop(MonsterDataSO monster, string biomeName, bool boss)
    {
        if (monster == null || monster.dropFigureData == null) return;
        if (!dropsByFigure.TryGetValue(monster.dropFigureData, out List<MonsterDrop> list))
        {
            list = new List<MonsterDrop>();
            dropsByFigure[monster.dropFigureData] = list;
        }

        MonsterDrop drop = list.FirstOrDefault(item => item.monster == monster);
        if (drop == null)
        {
            drop = new MonsterDrop { monster = monster };
            list.Add(drop);
        }
        drop.boss |= boss;
        if (!string.IsNullOrEmpty(biomeName) && !drop.biomeNames.Contains(biomeName))
            drop.biomeNames.Add(biomeName);
    }

    private static HashSet<FigureItemSO> CollectOwnedFigures()
    {
        var result = new HashSet<FigureItemSO>();
        GameSaveManager saveManager = GameSaveManager.Instance;
        var data = saveManager != null ? saveManager.LoadSaveData() : null;
        if (data != null && data.ownedFigureIDs != null)
        {
            foreach (string id in data.ownedFigureIDs)
            {
                FigureItemSO figure = saveManager.FindFigureByID(id);
                if (figure != null) result.Add(figure);
            }
        }
        if (InventoryManager.Instance != null && InventoryManager.Instance.ownedFigures != null)
        {
            foreach (FigureItemSO figure in InventoryManager.Instance.ownedFigures)
            {
                if (figure != null) result.Add(figure);
            }
        }
        return result;
    }

    // 설명이 길면 칸을 늘리고, 획득 바이옴·구분선·하단 묶음을 같은 만큼 내린다.
    // 그래도 페이지를 넘치면 글자를 24에서 20까지 2pt씩 줄인다.
    private void LayoutBookDescription()
    {
        if (descText == null || dropMonsterRoot == null) return;

        const float descTop = -52f;
        const float descWidth = 580f;
        const float baseHeight = 56f;
        const float locationBase = -128f;
        const float dividerBase = -152f;
        const float columnBase = -279f;
        const float monsterBottomOffset = -9.3f - 128f * 1.5f * 0.5f;
        const float pageBottom = -414f;

        descText.textWrappingMode = TextWrappingModes.Normal;
        descText.overflowMode = TextOverflowModes.Overflow;
        descText.enableAutoSizing = false;

        float height = baseHeight;
        foreach (float size in new float[] { 24f, 22f, 20f })
        {
            descText.fontSize = size;
            descText.ForceMeshUpdate();
            float preferred = Mathf.Max(baseHeight, descText.preferredHeight);
            float bottom = columnBase - (preferred - baseHeight) + monsterBottomOffset;
            height = preferred;
            if (bottom >= pageBottom) break;
        }

        RectTransform descRect = descText.rectTransform;
        if (descRect.pivot != new Vector2(0.5f, 1f)) descRect.pivot = new Vector2(0.5f, 1f);
        descRect.anchoredPosition = new Vector2(0f, descTop);
        descRect.sizeDelta = new Vector2(descWidth, height);
        LayoutElement element = descText.GetComponent<LayoutElement>();
        if (element != null) element.preferredHeight = height;

        float delta = height - baseHeight;
        if (locationText != null)
            locationText.rectTransform.anchoredPosition = new Vector2(locationText.rectTransform.anchoredPosition.x, locationBase - delta);
        Transform divider = transform.Find("Divider");
        if (divider is RectTransform dividerRect)
            dividerRect.anchoredPosition = new Vector2(dividerRect.anchoredPosition.x, dividerBase - delta);
        var dropRect = (RectTransform)dropMonsterRoot.transform;
        dropRect.anchoredPosition = new Vector2(dropRect.anchoredPosition.x, columnBase - delta);
        if (relatedSynergyRoot != null)
        {
            var synergyRect = (RectTransform)relatedSynergyRoot.transform;
            synergyRect.anchoredPosition = new Vector2(synergyRect.anchoredPosition.x, columnBase - delta);
        }
    }

    // 박스에 들어가는 가장 큰 정수 배로 키운다. 원본이 박스보다 크면 박스에 맞추고 비율은 유지한다.
    private static void FitSpriteBox(Image image, float box)
    {
        if (image == null || image.sprite == null) return;
        image.preserveAspect = true;
        float longest = Mathf.Max(image.sprite.rect.width, image.sprite.rect.height);
        int scale = Mathf.FloorToInt(box / Mathf.Max(1f, longest));
        Vector2 size = scale >= 1 ? image.sprite.rect.size * scale : new Vector2(box, box);
        if (image.rectTransform.sizeDelta != size) image.rectTransform.sizeDelta = size;
    }

    // 인벤토리 팝업 무대에 그림을 박스 안 최대 정수 배로 키우고, 발이 그림자에 닿게 아래를 맞춘다.
    public static void FitPopupStage(Image image, RectTransform stage, RectTransform stageShadow, float box)
    {
        if (image == null || stage == null) return;
        FitSpriteBox(image, box);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0f);
        float baseline = stageShadow != null ? stageShadow.anchoredPosition.y : -stage.rect.height * 0.5f;
        rect.anchoredPosition = new Vector2(0f, baseline);
    }

    private static void PlaceShadow(Image shadow, Image body)
    {
        if (shadow == null || body == null) return;
        float scaleX = Mathf.Abs(body.rectTransform.localScale.x);
        float scaleY = Mathf.Abs(body.rectTransform.localScale.y);
        float width = Mathf.Max(48f, body.rectTransform.sizeDelta.x * scaleX * 0.72f);
        float height = Mathf.Max(16f, width * 0.22f);
        shadow.rectTransform.sizeDelta = new Vector2(width, height);
        float bodyHeight = body.rectTransform.sizeDelta.y * scaleY;
        shadow.rectTransform.anchoredPosition = new Vector2(
            body.rectTransform.anchoredPosition.x,
            body.rectTransform.anchoredPosition.y - bodyHeight * 0.42f);
    }

    private class MonsterDrop
    {
        public MonsterDataSO monster;
        public readonly List<string> biomeNames = new List<string>();
        public bool boss;
    }
}