using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class CardFlipManager : MonoBehaviour
{
    [Header("연동할 요소")]
    [SerializeField] private Button triggerButton;                  // 시작 버튼
    [SerializeField] private LetterShakeController letterController; // 편지 흔들기 컨트롤러

    [Header("Project 창의 티켓/이미지 파일들")]
    [SerializeField] private List<Object> allTicketDataList = new List<Object>();

    [Header("카드 생성 위치 및 프리팹 설정")]
    [SerializeField] private RectTransform cardSpawnParent;
    [SerializeField] private GameObject cardPrefab;

    [Header("연출 및 배치 상세 설정")]
    [SerializeField] private float appearanceInterval = 0.18f;
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private float cardSpacing = 220f;

    private List<CardUI> generatedCardUIList = new List<CardUI>();
    private Sequence cardSequence;
    private bool isPlayingSequence = false; // 중복 실행 방지 플래그

    private void Awake()
    {
        DOTween.Init();

        if (cardSpawnParent == null)
        {
            cardSpawnParent = GetComponent<RectTransform>();
        }

        if (triggerButton != null)
        {
            triggerButton.onClick.RemoveAllListeners();
            triggerButton.onClick.AddListener(OnClickStartButton);
        }
    }

    // ⭐ 버튼 클릭 시 호출 (편지 그룹 자동 활성화 + 연출 시작)
    public void OnClickStartButton()
    {
        if (isPlayingSequence) return; // 이미 실행 중이면 무시
        isPlayingSequence = true;

        if (triggerButton != null)
        {
            triggerButton.gameObject.SetActive(false);
        }

        if (letterController != null)
        {
            // 편지 그룹이 꺼져있어도 자동으로 켜주고 연출 시작!
            letterController.gameObject.SetActive(true);
            letterController.StartLetterShake();
        }
        else
        {
            // 편지가 없을 때만 예외적으로 바로 카드 생성
            PlayCardFlipSequence();
        }
    }

    // ⭐ 편지가 다 찢어진 후 호출되는 카드 등장 함수
    [ContextMenu("Play Random Card Flip")]
    public void PlayCardFlipSequence()
    {
        Debug.Log("<color=cyan>[CardFlip] 카드 등장 연출 실행!</color>");

        if (cardSpawnParent != null)
        {
            cardSpawnParent.gameObject.SetActive(true);
            cardSpawnParent.SetAsLastSibling();
        }

        // 기존에 남아있던 카드 완벽 삭제
        ClearGeneratedCards();

        if (allTicketDataList.Count == 0)
        {
            isPlayingSequence = false;
            return;
        }

        int countToPick = Mathf.Min(3, allTicketDataList.Count);
        List<Object> selectedData = GetRandomElements(allTicketDataList, countToPick);

        cardSequence = DOTween.Sequence();
        float startX = -((countToPick - 1) * cardSpacing) / 2f;

        for (int i = 0; i < selectedData.Count; i++)
        {
            int index = i;
            Object data = selectedData[index];

            CardUI cardUI = CreateCardUIObject(data);
            if (cardUI == null) continue;

            generatedCardUIList.Add(cardUI);
            RectTransform cardRect = cardUI.GetComponent<RectTransform>();

            Vector2 targetPos = new Vector2(startX + (index * cardSpacing), 0f);
            cardRect.anchoredPosition = targetPos;

            cardRect.localScale = Vector3.zero;
            cardRect.localRotation = Quaternion.Euler(0f, -90f, 0f);
            cardUI.gameObject.SetActive(false);

            float startTime = index * appearanceInterval;

            cardSequence.InsertCallback(startTime, () =>
            {
                if (cardUI != null) cardUI.gameObject.SetActive(true);
            });

            cardSequence.Insert(startTime,
                cardRect.DOScale(Vector3.one, duration).SetEase(Ease.OutBack)
            );

            cardSequence.Insert(startTime,
                cardRect.DOLocalRotate(Vector3.zero, duration, RotateMode.FastBeyond360).SetEase(Ease.OutCubic)
            );

            cardSequence.InsertCallback(startTime + (duration * 0.4f), () =>
            {
                if (cardUI != null) cardUI.PlayAuraEffect();
            });
        }

        cardSequence.OnComplete(() =>
        {
            isPlayingSequence = false; // 연출 완료 후 잠금 해제
        });
    }

    private CardUI CreateCardUIObject(Object data)
    {
        GameObject newCardObj = null;

        if (cardPrefab != null)
        {
            newCardObj = Instantiate(cardPrefab, cardSpawnParent);
        }
        else if (data is GameObject go)
        {
            newCardObj = Instantiate(go, cardSpawnParent);
        }
        else
        {
            newCardObj = new GameObject($"CardUI_{data.name}", typeof(RectTransform), typeof(Image), typeof(CardUI));
            newCardObj.transform.SetParent(cardSpawnParent, false);

            RectTransform rect = newCardObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(160f, 220f);
        }

        CardUI cardUI = newCardObj.GetComponent<CardUI>();

        if (cardUI != null && data != null)
        {
            TrySetupCardUI(cardUI, data);
        }

        return cardUI;
    }

    private void TrySetupCardUI(CardUI cardUI, Object data)
    {
        System.Type cardType = cardUI.GetType();
        string[] candidateMethods = { "SetupCard", "Setup", "SetData", "Init", "Initialize", "SetTicket" };

        foreach (string methodName in candidateMethods)
        {
            MethodInfo method = cardType.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (method != null)
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(data.GetType()))
                {
                    method.Invoke(cardUI, new object[] { data });
                    return;
                }
            }
        }

        Sprite sprite = ExtractSpriteFromObject(data);
        Image img = cardUI.GetComponent<Image>();
        if (img != null && sprite != null)
        {
            img.sprite = sprite;
            img.color = Color.white;
            img.preserveAspect = true;
        }
    }

    private Sprite ExtractSpriteFromObject(Object data)
    {
        if (data is Sprite s) return s;

        if (data is Texture2D tex)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        if (data is ScriptableObject so)
        {
            System.Type type = so.GetType();

            while (type != null && type != typeof(ScriptableObject) && type != typeof(Object))
            {
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    object val = field.GetValue(so);
                    if (val == null) continue;

                    if (val is Sprite spriteVal) return spriteVal;

                    if (val is Texture2D subTex)
                    {
                        return Sprite.Create(subTex, new Rect(0, 0, subTex.width, subTex.height), new Vector2(0.5f, 0.5f));
                    }

                    if (!field.FieldType.IsPrimitive && !field.FieldType.IsEnum && field.FieldType != typeof(string))
                    {
                        var subFields = field.FieldType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        foreach (var sf in subFields)
                        {
                            object subVal = sf.GetValue(val);
                            if (subVal is Sprite subSprite) return subSprite;
                            if (subVal is Texture2D subTexture)
                            {
                                return Sprite.Create(subTexture, new Rect(0, 0, subTexture.width, subTexture.height), new Vector2(0.5f, 0.5f));
                            }
                        }
                    }
                }
                type = type.BaseType;
            }
        }

        return null;
    }

    private void ClearGeneratedCards()
    {
        if (cardSequence != null && cardSequence.IsActive()) cardSequence.Kill();

        if (cardSpawnParent != null)
        {
            foreach (Transform child in cardSpawnParent)
            {
                Destroy(child.gameObject);
            }
        }

        foreach (var card in generatedCardUIList)
        {
            if (card != null)
            {
                card.transform.DOKill();
                Destroy(card.gameObject);
            }
        }
        generatedCardUIList.Clear();
    }

    private List<T> GetRandomElements<T>(List<T> sourceList, int countToPick)
    {
        List<T> tempPool = new List<T>(sourceList);
        List<T> result = new List<T>();

        for (int i = 0; i < countToPick; i++)
        {
            if (tempPool.Count == 0) break;
            int randIndex = Random.Range(0, tempPool.Count);
            result.Add(tempPool[randIndex]);
            tempPool.RemoveAt(randIndex);
        }

        return result;
    }
}