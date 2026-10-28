// 로비의 튜토리얼 버튼으로 켜는 임시 튜토리얼 플래그(최소 구현).
// 켜져 있는 동안에는 이어하기 세이브를 저장하거나 지우지 않고, MainScene_Map에 안내 오버레이를 띄운다.
public static class TutorialMode
{
    // 튜토리얼 첫 전투 몬스터 체력. 한 턴에 잡힐 정도로 낮춘다.
    public const int MonsterHp = 5;

    public static bool Active { get; private set; }

    public static void Start()
    {
        Active = true;
    }

    // 여러 번 불러도 안전하다(로비 진입 때마다 한 번 더 부른다).
    public static void End()
    {
        Active = false;
    }

    // 바이옴 일반 몬스터 중 최대 체력이 가장 낮은 몬스터. 없으면 null.
    public static MonsterDataSO WeakestMonster(BiomeDataSO biome)
    {
        if (biome == null || biome.biomeMonsters == null) return null;
        MonsterDataSO weakest = null;
        foreach (MonsterDataSO monster in biome.biomeMonsters)
        {
            if (monster != null && (weakest == null || monster.maxHp < weakest.maxHp))
                weakest = monster;
        }
        return weakest;
    }
}
