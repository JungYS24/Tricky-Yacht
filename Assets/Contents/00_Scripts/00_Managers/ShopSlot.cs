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

    private BaseItemDataSO currentData;
    private ShopManager manager;

    public bool isPurchased = false;

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

        // 가격 및 버튼 설정
        if (priceText != null) priceText.text = data.price + " G";

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

        if (manager.PurchaseItem(currentData))
        {

            // 사운드 구매 성공 소리 재생 (코인 지불하는 소리 등)

            isPurchased = true;
            isAnimating = false; //아이템을 구매하면 즉시 애니메이션 연산을 정지

            if (itemIcon != null) itemIcon.color = new Color(0.3f, 0.3f, 0.3f, 1f);

            buyButton.interactable = false;
            if (priceText != null) priceText.text = "Sold Out";

            manager.HideTooltip();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentData != null && !isPurchased)
        {
            manager.ShowTooltip(currentData.description, GetComponent<RectTransform>());
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        manager.HideTooltip();
    }
}