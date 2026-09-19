using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Linq;

public class ShopSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image itemIcon;
    public TextMeshProUGUI priceText;
    public Button buyButton;

    // [자물쇠 아이콘
    [Header("잠금 UI")]
    public GameObject lockUI;
    public bool isLocked = false;

    public BaseItemDataSO currentData;
    private ShopManager manager;

    public bool isPurchased = false;
    public bool isLuckyCatFree = false; //복고양이 발동 여부

    // --- 애니메이션 제어용 변수 (코루틴 대체) ---
    private bool isAnimating = false;
    private int[] animFaces;
    private Sprite[] animSprites;
    private int currentAnimIndex;
    private float animTimer;
    private float animInterval = 0.7f;

    public void SetupSlot(BaseItemDataSO data, ShopManager shopMgr)
    {
        currentData = data;
        manager = shopMgr;
        isPurchased = false;
        isAnimating = false; // 새로운 아이템이 들어올 때 애니메이션 초기화

        //정상적으로 아이템이 들어올 때는 자물쇠를 끄고 잠금 해제
        isLocked = false;
        if (lockUI != null) lockUI.SetActive(false);


        // 아이콘 색상 초기화
        if (itemIcon != null) itemIcon.color = Color.white;

        // 주사위 아이템(DiceItemSO) 처리
        if (data is DiceItemSO diceData)
        {
            int[] uniqueFaces = diceData.customFaces.Distinct().ToArray();
            bool shouldAnimate = uniqueFaces.Length > 1 && uniqueFaces.Length < 6;

            if (shouldAnimate && diceData.customFaceSprites != null && diceData.customFaceSprites.Length >= 6)
            {
                // 애니메이션 세팅
                isAnimating = true;
                animFaces = uniqueFaces;
                animSprites = diceData.customFaceSprites;
                currentAnimIndex = 0;
                animTimer = 0f;

                // 첫 프레임부터 하얀 네모나 잘못된 이미지가 뜨지 않도록 즉시 적용
                if (itemIcon != null) itemIcon.sprite = animSprites[animFaces[0] - 1];
            }
            else
            {
                // 일반 주사위이거나 고정 눈금 주사위인 경우
                if (itemIcon != null) itemIcon.sprite = data.icon;
            }
        }
        else
        {
            // 주사위가 아닌 일반 아이템 (피규어, 스낵, 코팅 등)
            if (itemIcon != null) itemIcon.sprite = data.icon;
        }

        //가격 및 버튼 설정
        isLuckyCatFree = false; // 슬롯 세팅 시 무료 스위치 초기화
        int displayPrice = GetFinalPrice();
        if (priceText != null) priceText.text = displayPrice + " G";

        buyButton.interactable = true;
        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(TryPurchase);
    }

    private void Update()
    {
        //상점 슬롯이 켜져있고, 애니메이션 대상이며, '구매 전'일 때만 작동!
        if (isAnimating && !isPurchased && itemIcon != null && animSprites != null)
        {
            animTimer += Time.deltaTime;
            if (animTimer >= animInterval)
            {
                animTimer = 0f;
                currentAnimIndex = (currentAnimIndex + 1) % animFaces.Length;
                itemIcon.sprite = animSprites[animFaces[currentAnimIndex] - 1];
            }
        }
    }

    // 버튼 클릭 시 작동하는 구매 로직
    private void TryPurchase()
    {
        if (isPurchased) return;

        //할인된 최종 가격을 계산해서 ShopManager에게 결제를 요청
        int actualPrice = GetFinalPrice();
        if (manager.PurchaseItem(currentData, actualPrice))
        {
            // 사운드 구매 성공 소리 재생 (코인 지불하는 소리 등)
            isPurchased = true;
            isAnimating = false; //아이템을 구매하면 즉시 애니메이션 연산을 정지    

            if (itemIcon != null) itemIcon.color = new Color(0.3f, 0.3f, 0.3f, 1f);

            buyButton.interactable = false;
            if (priceText != null) priceText.text = LocalizationManager.GetUi("UI_SOLD_OUT", "Sold Out");

            manager.HideTooltip();
        }
    }

    //슬롯을 자물쇠 모드로 만드는 전용 함수
    public void SetLockedSlot()
    {
        isLocked = true;
        isPurchased = false;
        isAnimating = false;

        if (lockUI != null) lockUI.SetActive(true); // 자물쇠 아이콘 켜기
        if (itemIcon != null) itemIcon.color = Color.clear; // 아이템 아이콘 투명하게 숨김
        if (priceText != null) priceText.text = "???";

        buyButton.interactable = false; // 버튼 클릭 방지
        buyButton.onClick.RemoveAllListeners();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isLocked) return; // 잠겨있는 슬롯은 툴팁 안 띄움
        if (currentData != null && !isPurchased && manager != null)
        {
            manager.ShowTooltip(LocalizationManager.GetItemDescription(currentData), GetComponent<RectTransform>());
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (manager != null)
        {
            manager.HideTooltip();
        }
    }

    // 최종 가격을 계산하는 전용 함수 (할인 및 무료화 적용)
    public int GetFinalPrice()
    {
        // 이벤트 무료화(선장) 또는 복고양이 무료화 당첨 시 무조건 0원
        if (manager.diceManager.isNextShopFree || isLuckyCatFree) return 0;

        int price = currentData.price;
        // 코팅 및 위성 20% 할인 적용 (클래스 타입으로 안전하게 구분)
        if (currentData is CoatingItemSO)
        {
            // InventoryManager를 FigureEffectManager로 변경!
            int discount = FigureEffectManager.Instance != null ? FigureEffectManager.Instance.GetShopDiscountRate(FigureEffectType.DiscountCoating) : 0;
            price = Mathf.FloorToInt(price * (1f - discount / 100f));
        }
        // 위성 아이템 전용 SO 스크립트
        else if (currentData is SatelliteItemSO)
        {
            // 여기도 FigureEffectManager로 변경!
            int discount = FigureEffectManager.Instance != null ? FigureEffectManager.Instance.GetShopDiscountRate(FigureEffectType.DiscountSatellite) : 0;
            price = Mathf.FloorToInt(price * (1f - discount / 100f));
        }

        return Mathf.Max(0, price);
    }

    // 복고양이 당첨 시 호출되는 함수
    public void ApplyLuckyCatFree()
    {
        isLuckyCatFree = true;
        if (priceText != null) priceText.text = "<color=#FFFF00>0 G (무료!)</color>";
        Debug.Log($"[복고양이] {currentData.itemName} 무료 적용!");
    }
}