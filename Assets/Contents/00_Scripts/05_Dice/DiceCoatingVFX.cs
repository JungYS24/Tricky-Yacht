using UnityEngine;

public class DiceCoatingVFX : MonoBehaviour
{
    [Header("코팅 VFX 프리팹")]
    public GameObject iceVFXPrefab;
    public GameObject darkVFXPrefab;
    public GameObject goldVFXPrefab;
    public GameObject prismVFXPrefab;

    [Header("VFX 위치 보정")]
    public Vector3 vfxLocalOffset = new Vector3(0f, 0.15f, 0f);

    private Dice dice;
    private DiceType lastType;
    private GameObject currentVFX;

    void Awake()
    {
        dice = GetComponent<Dice>();
    }

    void Start()
    {
        RefreshCoatingVFX(true);
    }

    void Update()
    {
        RefreshCoatingVFX(false);
    }

    void RefreshCoatingVFX(bool forceRefresh)
    {
        if (dice == null || dice.myData == null)
            return;

        DiceType currentType = dice.myData.type;

        if (!forceRefresh && currentType == lastType)
            return;

        lastType = currentType;
        ApplyVFX(currentType);
    }

    void ApplyVFX(DiceType type)
    {
        if (currentVFX != null)
        {
            Destroy(currentVFX);
            currentVFX = null;
        }

        GameObject targetPrefab = null;

        switch (type)
        {
            case DiceType.Prism:
                targetPrefab = prismVFXPrefab;
                break;

            case DiceType.Gold:
                targetPrefab = goldVFXPrefab;
                break;

            case DiceType.Black:
                targetPrefab = darkVFXPrefab;
                break;

            case DiceType.Ice:
                targetPrefab = iceVFXPrefab;
                break;
        }

        if (targetPrefab == null)
            return;

        currentVFX = Instantiate(targetPrefab, transform);
        currentVFX.transform.localPosition = vfxLocalOffset;
        currentVFX.transform.localRotation = Quaternion.identity;
        currentVFX.transform.localScale = Vector3.one;
    }

    public void ForceRefresh()
    {
        RefreshCoatingVFX(true);
    }
}