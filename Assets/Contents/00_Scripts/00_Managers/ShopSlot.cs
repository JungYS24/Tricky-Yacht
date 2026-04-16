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
        private Coroutine animCoroutine;

    public void SetupSlot(BaseItemDataSO data, ShopManager shopMgr)
        {
            currentData = data;
            manager = shopMgr;
            isPurchased = false;

        if (animCoroutine != null)
        {
            StopCoroutine(animCoroutine);
            animCoroutine = null;
        }

        // 아이템이 주사위 데이터(DiceItemSO)인지 확인하고 애니메이션 실행
        if (data is DiceItemSO diceData)
        {
            int[] uniqueFaces = diceData.customFaces.Distinct().ToArray();
            bool shouldAnimate = uniqueFaces.Length > 1 && uniqueFaces.Length < 6;

            if (shouldAnimate && diceData.customFaceSprites != null && diceData.customFaceSprites.Length >= 6)
            {
                animCoroutine = StartCoroutine(AnimateShopIcon(uniqueFaces, diceData.customFaceSprites));
            }
            else if (itemIcon != null)
            {
                itemIcon.sprite = data.icon; // 일반 아이템은 기존 아이콘 사용
            }
        }

        if (itemIcon != null)
            {
                itemIcon.sprite = data.icon;
                itemIcon.color = Color.white;
            }
            if (priceText != null) priceText.text = data.price + " G";

            buyButton.interactable = true; 
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(TryPurchase); 
        }

        // 버튼 클릭 시 작동하는 구매 로직
        private void TryPurchase()
        {
            if (isPurchased) return;


            if (manager.PurchaseItem(currentData))
            {
                isPurchased = true; 

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
    //상점 아이콘 애니메이션 코루틴
     private System.Collections.IEnumerator AnimateShopIcon(int[] faces, Sprite[] sprites)
    {
        int index = 0;
        while (true)
        {
            itemIcon.sprite = sprites[faces[index] - 1];
            index = (index + 1) % faces.Length;
            yield return new WaitForSeconds(0.7f);
        }
        }
}