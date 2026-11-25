// 움직이는 일러스트(셰이더 방식): 한 장 그림을 움직임 지도대로 흔듭니다(UI Image 용).
//   _SwayMask  R = 흔들림(머리카락 · 옷자락, 끝일수록 셈) · G = 호흡(가슴) · B = 반짝임(날개 · 빛 · 이펙트)
//   _BlinkTex  눈 감은 그림, 알파 = 눈 영역 → _Blink(0 ~ 1)만큼 섞음
//   _T         시간(초) — SwayIllustration 컴포넌트가 매 프레임 넣음
// 식은 tools/art/generate-sway.js 의 미리보기와 같음(바꾸면 둘 다 바꿀 것). 텍스처 v 는 위가 1 이라 미리보기와 부호만 반대.
Shader "SubcultureRPG/UI Sway"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SwayMask ("Sway Mask (R sway, G breath, B glow)", 2D) = "black" {}
        _BlinkTex ("Blink (alpha = eyes)", 2D) = "black" {}
        _T ("Time", Float) = 0
        _Blink ("Blink", Range(0,1)) = 0
        _SwayAmp ("Sway Amount", Float) = 0.012
        _SwaySpeed ("Sway Speed", Float) = 1.6
        _SwayFreq ("Sway Wave", Float) = 9
        _BreathAmp ("Breath Amount", Float) = 0.006
        _BreathSpeed ("Breath Speed", Float) = 1.1
        _GlowAmp ("Glow Amount", Float) = 0.22
        _GlowSpeed ("Glow Speed", Float) = 2.4

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 worldPosition : TEXCOORD1; };

            sampler2D _MainTex, _SwayMask, _BlinkTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float4 _ClipRect;
            float _T, _Blink, _SwayAmp, _SwaySpeed, _SwayFreq, _BreathAmp, _BreathSpeed, _GlowAmp, _GlowSpeed;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float vd = 1.0 - uv.y;                       // 미리보기와 같은 '위에서부터' 좌표
                float3 m = tex2D(_SwayMask, uv).rgb;         // 지도는 제자리 기준
                float du = m.r * _SwayAmp * sin(_T * _SwaySpeed * 3.14159 + vd * _SwayFreq + uv.x * 3.0);
                float dv = m.r * _SwayAmp * 0.35 * cos(_T * _SwaySpeed * 3.14159 + vd * _SwayFreq * 0.7)
                         - m.g * _BreathAmp * (0.5 + 0.5 * sin(_T * _BreathSpeed * 3.14159));
                fixed4 c = tex2D(_MainTex, float2(uv.x - du, uv.y + dv));
                fixed4 b = tex2D(_BlinkTex, uv);
                c.rgb = lerp(c.rgb, b.rgb, b.a * _Blink);
                c.rgb *= 1.0 + m.b * _GlowAmp * (0.5 + 0.5 * sin(_T * _GlowSpeed + vd * 6.0 - uv.x * 4.0));
                c *= i.color;
                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
