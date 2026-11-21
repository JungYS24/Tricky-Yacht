using UnityEditor;

// 초원 몬스터 5종의 256 IDLE 시트(4열×2행 = 8프레임)로 IDLE 클립 키프레임을 다시 채운다.
// 클립 파일·GUID·바인딩 경로·대상 속성은 그대로 두고 m_Sprite 키프레임만 바꾼다(실제 작업은 NpcIdleClipBuilder.Rebuild).
// 프레임 간격을 바꾸려면 아래 상수나 Targets 의 간격을 고친 뒤 메뉴(Tools/Sprites/초원 몬스터 IDLE 클립 다시 만들기)를 실행한다.
public static class MeadowIdleClipBuilder
{
    public const float DefaultFrameInterval = 0.15f; // 기본
    public const float SlowFrameInterval = 0.2f;     // 녹지 거북, 솔가

    const string Dir = "Assets/Contents/02_Sprites/01_Characters/02_Meadow/";

    public static readonly NpcIdleClipBuilder.Target[] Targets =
    {
        new NpcIdleClipBuilder.Target(Dir + "Blade-Glass Snake/Blade-Glass Snake.png", Dir + "Blade-Glass Snake/Blade-Glass Snake.anim", DefaultFrameInterval), // 칼날뱀
        new NpcIdleClipBuilder.Target(Dir + "Greenery Turtle/Greenery Turtle.png", Dir + "Greenery Turtle/AC_Greenery Turtle.anim", SlowFrameInterval),         // 녹지 거북
        new NpcIdleClipBuilder.Target(Dir + "muscle rabbit/muscle rabbit.png", Dir + "muscle rabbit/muscle rabbit.anim", DefaultFrameInterval),                // 근육토끼
        new NpcIdleClipBuilder.Target(Dir + "Solgar/Solgar.png", Dir + "Solgar/Solgar.anim", SlowFrameInterval),                                                // 사자왕 솔가
        new NpcIdleClipBuilder.Target(Dir + "Thunder Hoof/Thunder Hoof.png", Dir + "Thunder Hoof/Thunder Hoof.anim", DefaultFrameInterval),                    // 천둥마
    };

    [MenuItem("Tools/Sprites/초원 몬스터 IDLE 클립 다시 만들기")]
    public static void RebuildAll()
    {
        foreach (NpcIdleClipBuilder.Target t in Targets) NpcIdleClipBuilder.Rebuild(t);
        AssetDatabase.SaveAssets();
    }
}
