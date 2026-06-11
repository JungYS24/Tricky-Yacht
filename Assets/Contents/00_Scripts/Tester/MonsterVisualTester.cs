using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
public class MonsterVisualTester : MonoBehaviour
{
    [Header("Test Target Data")]
    public MonsterDataSO monsterDataSO;

    private SpriteRenderer spriteRenderer;
    private Animator animator;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    void Start()
    {
        ApplyMonsterVisual();
    }

    // 인스펙터에서 SO 데이터 에셋을 실시간으로 갈아끼울 때 대응
    void OnValidate()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();

        if (monsterDataSO != null)
        {
            ApplyMonsterVisual();
        }
    }

    private void ApplyMonsterVisual()
    {
        if (monsterDataSO == null) return;

        if (monsterDataSO.monsterSprite != null)
        {
            spriteRenderer.sprite = monsterDataSO.monsterSprite;
        }

        // 2. 런타임 애니메이터 컨트롤러 동기화 및 강제 업데이트 재생
        if (monsterDataSO.animatorController != null)
        {
            animator.runtimeAnimatorController = monsterDataSO.animatorController;
            animator.Update(0f);
        }

        // 데이터가 정상 파싱되었는지 콘솔에 이쁘게 출력
        Debug.Log($"<color=lime>[Tester]</color> 몬스터 ID: {monsterDataSO.monsterID} | 이름: {monsterDataSO.monsterName} 비주얼 동기화 성공!");
    }
}