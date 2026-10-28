using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CharacterSelectInteraction : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private CharacterData characterData;

    [SerializeField] private TextMeshProUGUI nameText;

    [SerializeField] private UnityEvent<CharacterData> OnClickDisplay;

    [Header("선택 연출")]
    [SerializeField] private float selectedScale = 1.15f;
    [SerializeField] private float scaleDuration = 0.15f;

    [Header("이름표")]
    [SerializeField] private Image nameTag;
    [SerializeField] private GameObject lockIcon;
    [SerializeField] private Sprite nameTagSprite;
    [SerializeField] private Sprite nameTagSelectedSprite;
    [SerializeField] private Color lockedNameColor = new Color32(0x96, 0xAA, 0xAA, 0xFF);

    // 씬에서는 비활성 상태로 두고, 첫 선택 때 켠다.
    [Header("선택 표시 (모든 캐릭터가 같은 오브젝트를 공유)")]
    [SerializeField] private RectTransform selectGlow;
    [SerializeField] private RectTransform selectArrow;

    private static CharacterSelectInteraction currentSelected;
    private Vector3 defaultScale;
    private Coroutine scaleRoutine;

    public void OnPointerClick(PointerEventData eventData)
    {
        OnClickDisplay.Invoke(characterData);
        Select();
    }

    public void Awake()
    {
        defaultScale = transform.localScale;

        bool unlocked = characterData.IsUnlocked;
        if (lockIcon != null)
            lockIcon.SetActive(!unlocked);

        if (unlocked)
        {
            nameText.text = LocalizationManager.GetCharacterName(characterData);
        }
        else
        {
            nameText.text = "???";
            nameText.color = lockedNameColor;
            enabled = false;
        }

        SetNameTag(false);
    }

    private void Select()
    {
        if (currentSelected != null && currentSelected != this)
            currentSelected.SetSelected(false);

        currentSelected = this;
        SetSelected(true);
        MoveMarker(selectGlow);
        MoveMarker(selectArrow);
    }

    private void SetSelected(bool selected)
    {
        Vector3 target = selected ? defaultScale * selectedScale : defaultScale;

        if (scaleRoutine != null)
            StopCoroutine(scaleRoutine);

        scaleRoutine = StartCoroutine(ScaleTo(target));
        SetNameTag(selected);
    }

    private void SetNameTag(bool selected)
    {
        if (nameTag == null)
            return;

        Sprite sprite = selected ? nameTagSelectedSprite : nameTagSprite;
        if (sprite != null)
            nameTag.sprite = sprite;
    }

    // 발광·화살표는 y를 고정하고 x만 선택 캐릭터 위치로 옮긴다.
    private void MoveMarker(RectTransform marker)
    {
        if (marker == null)
            return;

        Vector3 position = marker.position;
        position.x = transform.position.x;
        marker.position = position;
        marker.gameObject.SetActive(true);
    }

    private IEnumerator ScaleTo(Vector3 target)
    {
        Vector3 start = transform.localScale;
        float elapsed = 0f;

        while (elapsed < scaleDuration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(start, target, elapsed / scaleDuration);
            yield return null;
        }

        transform.localScale = target;
        scaleRoutine = null;
    }
}
