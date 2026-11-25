using UnityEngine;

// 악마주사위 최종 처치 연출. 프리팹 루트에 SpriteRenderer + Animator와 함께 붙인다.
// 클립(Devil_DIce_destroy) 마지막 이벤트에서 OnDestroyAnimEnd가 불린다.
[RequireComponent(typeof(Animator))]
public class DevilDiceDestroyFx : MonoBehaviour
{
    [Tooltip("끝나면 파괴 대신 비활성화(풀링 사용 시)")]
    [SerializeField] bool deactivateInsteadOfDestroy = false;

    [SerializeField] SpriteRenderer spriteRenderer;

    [Tooltip("보스 스프라이트 칸 폭 대비 이펙트 칸 폭. 1이면 같은 폭")]
    [SerializeField] float sizeMultiplier = 1f;

    public System.Action Finished;
    public bool IsFinished { get; private set; }

    void OnEnable()
    {
        IsFinished = false;
        var anim = GetComponent<Animator>();
        anim.Rebind();
        anim.Update(0f);
    }

    // 보스 스프라이트 칸의 중심에 같은 폭으로 놓고, 보스보다 한 칸 앞에 그린다.
    public void PlaceOver(SpriteRenderer target)
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (target == null || target.sprite == null) return;

        Sprite s = target.sprite;
        float ppu = s.pixelsPerUnit;
        Vector2 localCenter = (s.rect.size * 0.5f - s.pivot) / ppu;
        if (target.flipX) localCenter.x = -localCenter.x;
        transform.position = target.transform.TransformPoint(localCenter);

        float targetWidth = s.rect.width / ppu * Mathf.Abs(target.transform.lossyScale.x);
        Sprite own = spriteRenderer != null ? spriteRenderer.sprite : null;
        float ownWidth = own != null ? own.rect.width / own.pixelsPerUnit : 1f;
        float scale = targetWidth / Mathf.Max(0.0001f, ownWidth) * sizeMultiplier;
        transform.localScale = new Vector3(scale, scale, 1f);

        if (spriteRenderer != null)
        {
            spriteRenderer.sortingLayerID = target.sortingLayerID;
            spriteRenderer.sortingOrder = target.sortingOrder + 1;
            spriteRenderer.flipX = target.flipX;
            gameObject.layer = target.gameObject.layer;
        }
    }

    // AnimationClip 마지막 이벤트에서 호출
    public void OnDestroyAnimEnd()
    {
        if (IsFinished) return;
        IsFinished = true;
        Finished?.Invoke();
        if (deactivateInsteadOfDestroy) gameObject.SetActive(false);
        else Destroy(gameObject);
    }
}
