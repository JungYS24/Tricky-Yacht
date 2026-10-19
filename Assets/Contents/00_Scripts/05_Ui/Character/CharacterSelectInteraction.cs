using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class CharacterSelectInteraction : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private CharacterData characterData;

    [SerializeField] private TextMeshProUGUI nameText;

    [SerializeField] private UnityEvent<CharacterData> OnClickDisplay;

    [Header("선택 연출")]
    [SerializeField] private float selectedScale = 1.15f;
    [SerializeField] private float scaleDuration = 0.15f;

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

        if (characterData.IsUnlocked)
        {
            nameText.text = characterData.CharacterName;
        }
        else
        {
            nameText.text = "???";
            enabled = false;
        }
    }

    private void Select()
    {
        if (currentSelected != null && currentSelected != this)
            currentSelected.SetSelected(false);

        currentSelected = this;
        SetSelected(true);
    }

    private void SetSelected(bool selected)
    {
        Vector3 target = selected ? defaultScale * selectedScale : defaultScale;

        if (scaleRoutine != null)
            StopCoroutine(scaleRoutine);

        scaleRoutine = StartCoroutine(ScaleTo(target));
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
