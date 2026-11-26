using UnityEditor;

// 숲(아기 고블린)·유적·정글 몬스터 13종의 256 IDLE 시트(4열×2행 = 8프레임)로 IDLE 클립 키프레임을 다시 채운다.
// 클립 파일·GUID·바인딩 경로·대상 속성은 그대로 두고 m_Sprite 키프레임만 바꾼다(실제 작업은 NpcIdleClipBuilder.Rebuild).
// 프레임 간격을 바꾸려면 아래 상수나 Targets 의 간격을 고친 뒤 메뉴(Tools/Sprites/유적·정글 몬스터 IDLE 클립 다시 만들기)를 실행한다.
public static class RuinsJungleIdleClipBuilder
{
    public const float DefaultFrameInterval = 0.15f; // 기본
    public const float SlowFrameInterval = 0.2f;     // 가고일 석상, 황금 해골, 거신병, 바나나 킹콩, 덩굴 골렘
    public const float FastFrameInterval = 0.12f;    // 나비, 바나나 몽키

    const string Forest = "Assets/Contents/02_Sprites/01_Characters/01_Forest/";
    const string Ruins = "Assets/Contents/02_Sprites/01_Characters/06_Ruins/";
    const string Jungle = "Assets/Contents/02_Sprites/01_Characters/04_Jungle/";

    public static readonly NpcIdleClipBuilder.Target[] Targets =
    {
        // 숲
        new NpcIdleClipBuilder.Target(Forest + "baby_goblin/baby_goblin.png", Forest + "baby_goblin/AC_baby_goblin.anim", DefaultFrameInterval),                         // 아기 고블린
        // 유적 (몬스터 SO 가 쓰는 _Clean 시트·클립)
        new NpcIdleClipBuilder.Target(Ruins + "Blind_Priest/Blind_Priest_Clean.png", Ruins + "Blind_Priest/AC_Blind_Priest_Clean.anim", DefaultFrameInterval),          // 눈먼 사제
        new NpcIdleClipBuilder.Target(Ruins + "Dart_Totem/Dart_Totem_Clean.png", Ruins + "Dart_Totem/AC_Dart_Totem_Clean.anim", DefaultFrameInterval),                  // 독침 석상
        new NpcIdleClipBuilder.Target(Ruins + "Gargoyle_Totem/Gargoyle_Totem_Clean.png", Ruins + "Gargoyle_Totem/AC_Gargoyle_Totem_Clean.anim", SlowFrameInterval),     // 가고일 석상
        new NpcIdleClipBuilder.Target(Ruins + "Gold_Skeleton/Gold_Skeleton_Clean.png", Ruins + "Gold_Skeleton/AC_Gold_Skeleton_Clean.anim", SlowFrameInterval),         // 황금 해골
        new NpcIdleClipBuilder.Target(Ruins + "Mimic/Mimic_Clean.png", Ruins + "Mimic/AC_Mimic_Clean.anim", DefaultFrameInterval),                                      // 미믹
        new NpcIdleClipBuilder.Target(Ruins + "Tormentor/Tormentor_Clean.png", Ruins + "Tormentor/AC_Tormentor_Clean.anim", SlowFrameInterval),                         // 거신병
        // 정글
        new NpcIdleClipBuilder.Target(Jungle + "banana kingkong/banana kingkong.png", Jungle + "banana kingkong/banana kingkong.anim", SlowFrameInterval),               // 바나나 킹콩
        new NpcIdleClipBuilder.Target(Jungle + "Banana Monkey/Banana Monkey.png", Jungle + "Banana Monkey/Banana Monkey.anim", FastFrameInterval),                      // 바나나 몽키
        new NpcIdleClipBuilder.Target(Jungle + "Butterfly/Butterfly.png", Jungle + "Butterfly/Butterfly.anim", FastFrameInterval),                                      // 나비
        new NpcIdleClipBuilder.Target(Jungle + "jungle mushroom/jungle mushroom.png", Jungle + "jungle mushroom/jungle mushroom.anim", DefaultFrameInterval),            // 정글 버섯
        new NpcIdleClipBuilder.Target(Jungle + "leopard/leopard.png", Jungle + "leopard/AC_leopard.anim", DefaultFrameInterval),                                        // 흑표범
        new NpcIdleClipBuilder.Target(Jungle + "Vine Golem/Vine Golem.png", Jungle + "Vine Golem/AC_VineGolem.anim", SlowFrameInterval),                                // 덩굴 골렘
    };

    [MenuItem("Tools/Sprites/유적·정글 몬스터 IDLE 클립 다시 만들기")]
    public static void RebuildAll()
    {
        foreach (NpcIdleClipBuilder.Target t in Targets) NpcIdleClipBuilder.Rebuild(t);
        AssetDatabase.SaveAssets();
    }
}
