using UnityEngine;
using UnityEngine.UI;

// 캐릭터 IDLE 한 칸. 256px 시트를 정수 배율(목록 1배, 상세 2배)로 Point 필터 그대로 보여 주고,
// 미해금이면 그림만 회색(채도 낮춤 + 약간 어둡게)으로 바꾼다. 글자는 건드리지 않는다.
[RequireComponent(typeof(Image), typeof(Animator))]
public class CharacterSelectFigure : MonoBehaviour
{
    private const int SourcePixels = 256;

    [SerializeField, Min(1)] private int pixelScale = 1;

    private Image image;
    private Animator animator;

    private void Awake() => Cache();

    private void Cache()
    {
        if (image == null) image = GetComponent<Image>();
        if (animator == null) animator = GetComponent<Animator>();
    }

    public void Show(CharacterSelectEntry entry, bool locked, CharacterSelectCatalog catalog)
    {
        Cache();

        int size = SourcePixels * pixelScale;
        ((RectTransform)transform).sizeDelta = new Vector2(size, size);

        image.sprite = entry.idleFirstFrame;
        image.preserveAspect = false;
        image.enabled = entry.idleFirstFrame != null || entry.idleController != null;

        if (animator.runtimeAnimatorController != entry.idleController)
        {
            animator.runtimeAnimatorController = entry.idleController;
            if (Application.isPlaying && animator.isActiveAndEnabled && entry.idleController != null)
                animator.Play(0, 0, 0f);
        }

        bool useMaterial = locked && catalog != null && catalog.lockedMaterial != null;
        image.material = useMaterial ? catalog.lockedMaterial : null;
        image.color = locked && !useMaterial && catalog != null ? catalog.lockedTint : Color.white;
    }
}
