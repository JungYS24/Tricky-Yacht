using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 맵에 놓이는 노드 버튼입니다. 아트가 없으면 색 원으로 대체합니다.
public class MapNodeView : MonoBehaviour
{
    public Button button;
    public Image icon;
    public Image ring;
    int nodeId;
    DiceManager owner;

    public void Bind(DiceManager diceManager, int id, Sprite sprite, Color color, bool interactable, bool current, bool visited)
    {
        owner = diceManager;
        nodeId = id;
        if (button == null) button = GetComponent<Button>();
        if (icon == null) icon = GetComponent<Image>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = interactable;
            button.onClick.AddListener(OnClick);
        }
        if (icon != null)
        {
            icon.DOKill();
            icon.type = Image.Type.Simple;
            icon.sprite = sprite;
            icon.preserveAspect = sprite != null;
            icon.color = sprite != null ? Color.white : color;
        }
        if (ring != null) ring.enabled = false;

        RectTransform rect = transform as RectTransform;
        float size = interactable ? 108f : 56f;
        if (rect != null) rect.sizeDelta = new Vector2(size, size);
        transform.localScale = Vector3.one;
        if (interactable && icon != null)
        {
            icon.DOFade(0.35f, 0.42f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true)
                .SetLink(gameObject);
        }
    }

    void OnClick()
    {
        owner?.EnterMapNode(nodeId);
    }

}
