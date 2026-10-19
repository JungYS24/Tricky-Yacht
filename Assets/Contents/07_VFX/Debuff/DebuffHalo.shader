// 테이블 위 디버프 주사위 뒤에 까는 보랏빛 후광용 스프라이트 셰이더.
// 일반 반투명은 초록 바닥과 섞여 회색이 되고, 더하기(가산)는 하늘색이 되므로
// 바닥을 보라로 살짝 덮은 뒤(_Occlude) 그 위에 보랏빛을 조금 더해(_Boost) 은은하게 빛나 보이게 한다.
// 색은 SpriteRenderer 색(rgb), 세기는 그 알파로 조절한다(DebuffDiceGlow가 숨 쉬듯 바꿈).
Shader "TrickYacht/Sprite/DebuffHalo"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Occlude ("Tint (바닥을 보라로 덮는 정도)", Range(0, 1)) = 0.45
        _Boost ("Glow (위에 더하는 밝기)", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            half _Occlude;
            half _Boost;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half a = tex2D(_MainTex, IN.texcoord).a * IN.color.a;
                // 미리 곱한 알파: 결과 = 바닥 × (1 - a·덮기) + 색 × a·(덮기 + 더하기)
                return fixed4(IN.color.rgb * a * (_Occlude + _Boost), a * _Occlude);
            }
            ENDCG
        }
    }
}
