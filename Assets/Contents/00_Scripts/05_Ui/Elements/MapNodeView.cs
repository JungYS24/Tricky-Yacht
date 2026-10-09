using System.Collections;
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
    Coroutine pulse;

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
            icon.sprite = sprite;
            icon.color = interactable || current ? Color.white : (visited ? new Color(1f, 1f, 1f, 0.55f) : new Color(1f, 1f, 1f, 0.35f));
            if (sprite == null) icon.color = color;
        }
        if (ring != null)
        {
            ring.enabled = current || interactable;
            ring.color = current ? new Color(1f, 0.92f, 0.4f, 1f) : new Color(1f, 1f, 1f, 0.9f);
        }

        if (pulse != null) StopCoroutine(pulse);
        pulse = null;
        transform.localScale = Vector3.one;
        if (interactable)
            pulse = StartCoroutine(Pulse());
    }

    void OnClick()
    {
        owner?.EnterMapNode(nodeId);
    }

    IEnumerator Pulse()
    {
        while (true)
        {
            float t = (Mathf.Sin(Time.unscaledTime * 3.2f) + 1f) * 0.5f;
            float scale = Mathf.Lerp(1f, 1.12f, t);
            transform.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }
    }
}
