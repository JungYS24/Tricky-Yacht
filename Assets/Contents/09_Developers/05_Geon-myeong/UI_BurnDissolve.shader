Shader "UI/BurnDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        _NoiseTex ("Burn Noise Texture", 2D) = "white" {}
        _BurnAmount ("Burn Amount", Range(0.0, 1.0)) = 0.0
        _EdgeWidth ("Edge Width", Range(0.0, 0.2)) = 0.03
        [HDR] _BurnColor ("Burn Edge Color", Color) = (2.5, 0.5, 0.0, 1)
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
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _NoiseTex;
            fixed4 _Color;
            float _BurnAmount;
            float _EdgeWidth;
            fixed4 _BurnColor;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;
                fixed noise = tex2D(_NoiseTex, IN.texcoord).r;

                // 1. 원본 이미지가 이미 투명한 영억(알파값 없음)은 즉시 제거
                if (c.a <= 0.01)
                {
                    discard;
                }

                // 2. Dissolve 처리 (BurnAmount보다 낮은 노이즈 영역 제거)
                if (noise < _BurnAmount)
                {
                    discard;
                }

                // 3. 불타는 테두리 연출
                // _BurnAmount가 0 초과일 때만 작동하도록 조건 추가 (시작 전 노란 테두리 노출 방지)
                if (_BurnAmount > 0.001 && noise < _BurnAmount + _EdgeWidth)
                {
                    c.rgb = _BurnColor.rgb;
                }

                return c;
            }
            ENDCG
        }
    }
}