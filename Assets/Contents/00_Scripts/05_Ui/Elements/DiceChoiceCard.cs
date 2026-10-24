using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// 주사위 선택창의 카드 한 장: 양피지 카드 위에 덱의 주사위(코팅·스탬프·위성이 적용된 모습 그대로)와 이름.
// 마우스를 올리면 살짝 떠오르고 금색 테두리가 생긴다.
public class DiceChoiceCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public RectTransform body;        // 등장·뒤집기·떠오르기를 움직이는 부분(카드 자리는 줄 배치가 잡는다)
    public CanvasGroup bodyGroup;
    public Button button;
    public RectTransform diceHolder;  // 덱 주사위 칸(DeckSlot)이 들어가는 자리
    public TextMeshProUGUI nameText;

    [Header("마우스 올림")]
    public float hoverLift = 12f;
    public float hoverDuration = 0.12f;
    public Color hoverOutlineColor = new Color(1f, 0.84f, 0.38f, 1f);
    public Vector2 hoverOutlineDistance = new Vector2(4f, -4f);

    public DiceData1 Dice { get; private set; }
    public DeckSlot Slot { get; private set; }

    private Outline outline;
    private Canvas holderCanvas;

    // 덱 주사위 칸 프리팹을 카드 안에 하나 만들어 두고, 주사위가 바뀔 때마다 다시 그린다.
    public void SetDice(DiceData1 dice, GameObject diceSlotPrefab, string displayName)
    {
        Dice = dice;
        if (Slot == null && diceSlotPrefab != null)
        {
            EnsureHolderCanvas();
            GameObject slotGo = Instantiate(diceSlotPrefab, diceHolder, false);
            var rect = (RectTransform)slotGo.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            // 클릭은 카드 전체가 받는다.
            var slotButton = slotGo.GetComponent<Button>();
            if (slotButton != null) slotButton.enabled = false;
            foreach (var graphic in slotGo.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            Slot = slotGo.GetComponent<DeckSlot>();
        }
        if (Slot != null) Slot.SetDice(dice, false);
        if (nameText != null) nameText.text = displayName;
    }

    // 코팅 반짝임(파티클)은 주사위 뒤에 깔린다. 주사위 자리를 카드보다 한 단계 위 캔버스에 두어 양피지에 가려지지 않게 한다.
    private void EnsureHolderCanvas()
    {
        if (holderCanvas != null) return;
        Canvas parent = GetComponentInParent<Canvas>();
        if (parent == null) return;
        holderCanvas = diceHolder.gameObject.AddComponent<Canvas>();
        holderCanvas.overrideSorting = true;
        holderCanvas.sortingLayerID = parent.sortingLayerID;
        holderCanvas.sortingOrder = parent.sortingOrder + 1;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button == null || !button.IsInteractable()) return;
        if (outline == null)
        {
            outline = body.GetComponent<Outline>();
            if (outline == null) outline = body.gameObject.AddComponent<Outline>();
        }
        outline.effectColor = hoverOutlineColor;
        outline.effectDistance = hoverOutlineDistance;
        outline.enabled = true;
        body.DOKill(true);
        body.DOAnchorPosY(hoverLift, hoverDuration).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(body.gameObject);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ResetHover();
    }

    public void ResetHover()
    {
        if (outline != null) outline.enabled = false;
        if (body == null) return;
        body.DOKill(true);
        body.DOAnchorPosY(0f, hoverDuration).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(body.gameObject);
    }
}
