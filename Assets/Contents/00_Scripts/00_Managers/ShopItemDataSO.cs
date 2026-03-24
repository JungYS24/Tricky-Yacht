using UnityEngine;

// 아이템의 대분류와 세부 속성을 정의하는 Enum
public enum ItemCategory { Shaker, Coating, Figure, Snack }
public enum ItemGrade { None, Normal, Rare, Epic, Legendary }
public enum ShakerClass { None, Balance, Attack, Technic, Custom }

[CreateAssetMenu(fileName = "NewShopItem", menuName = "Shop/ItemData")]
public class ShopItemDataSO : ScriptableObject
{
    [Header("--- 공통 기본 정보 ---")]
    public string itemID;             // 예: "ITEM_SNACK_04"
    public ItemCategory category;     // 아이템 종류 선택
    public string itemName;           // "샌드위치"
    public int price;                 // 450
    public Sprite icon;               // 상점에 띄울 아이콘

    [TextArea(3, 5)]
    public string description;        // "칩스 추가 +50" (툴팁용 설명)

    [Header("--- 1. 쉐이커(Shaker) 전용 스펙 ---")]
    public ItemGrade grade;           // 등급
    public ShakerClass shakerClass;   // 클래스

    [Header("--- 2. 코팅(Coating) 전용 스펙 ---")]
    public float scoreMultiplier = 1f; // 최종 배수 가산 (프리즘: 1.2 등)
    public int bonusCoin = 0;          // 획득 코인 증가 (골드: +1 등)
    public float targetScoreReduce = 0f; // 목표 점수 감쇠율 (다크: 0.02 등)

    [Header("--- 3. 피규어(Figure) 전용 스펙 ---")]
    public bool isPermanent = true;   // 영구 배치 여부
    public int requiredSlots = 1;     // 차지하는 배치 슬롯 수

    [Header("--- 4. 스낵(Snack) 전용 스펙 ---")]
    public int instantBonusChips = 0; // 즉시 획득하는 칩 (샌드위치: 50)
    public int tempRerollAdd = 0;     // 임시 리롤 증가량 (포이즌 코크)
    public bool ignoreDebuff = false; // 디버프 무시 여부 (그린 애플)

    // 💡 팁: '특정 족보 점수 증가', '유니콘의 프리즘 연계' 같은 복잡한 특수 로직은 
    // GameManager나 ScoreManager에서 이 SO의 itemID나 category를 읽어와서 코드로 별도 처리하는 것이 깔끔합니다.
}