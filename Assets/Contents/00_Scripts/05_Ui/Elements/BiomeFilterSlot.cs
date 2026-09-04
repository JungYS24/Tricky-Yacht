using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BiomeFilterSlot : MonoBehaviour
{
    public TextMeshProUGUI filterNameText;
    public Button filterButton;

    private int myFilterIndex;
    private CollectionBookManager manager;

    public void Setup(string filterName, int filterIndex, CollectionBookManager mgr)
    {
        myFilterIndex = filterIndex;
        manager = mgr;

        if (filterNameText != null)
            filterNameText.text = filterName;

        filterButton.onClick.RemoveAllListeners();
        filterButton.onClick.AddListener(OnClickSlot);
    }

    private void OnClickSlot()
    {
        // 버튼이 눌리면 매니저에게 내 인덱스를 전달해서 필터 적용
        manager.ChangeBiomeFilter(myFilterIndex);
    }
}