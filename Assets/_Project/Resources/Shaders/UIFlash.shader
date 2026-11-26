// 전투 필드 유닛 그림용 UI 셰이더: 기본 UI 셰이더(UI/Default)와 같고, _Flash(색 · 세기)만큼 그림을 그 색으로 물들임(피격 번쩍임).
// RectMask2D(부드러운 가장자리 포함) · 스텐실 마스크를 지원합니다. Resources 에 두어 빌드에 포함되고 Shader.Find 로 찾습니다.
Shader "SubcultureRPG/UIFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _FlashAmount ("Flash Amount", Range(0,1)) = 0
        _FlashBody ("Flash Body Brighten", Range(0,1)) = 0.38
        _RimWidth ("Rim Width (texels)", Float) = 5
        _TintColor ("Tint Color", Color) = (0,0,0,1)
        _TintAmount ("Tint Amount", Range(0,1)) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

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
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4  mask : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            fixed4 _FlashColor;
            float _FlashAmount;
            float _FlashBody;
            float _RimWidth;
            fixed4 _TintColor;
            float _TintAmount;
            float4 _MainTex_TexelSize;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float4 vPosition = UnityObjectToClipPos(v.vertex);
                OUT.worldPosition = v.vertex;
                OUT.vertex = vPosition;

                float2 pixelSize = vPosition.w;
                pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
                OUT.mask = float4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 tex = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
                half4 color = IN.color * tex;
                // 물들이기(쓰러질 때 어둡게 등): 색만 _TintColor 쪽으로
                color.rgb = lerp(color.rgb, _TintColor.rgb, saturate(_TintAmount));

                // 피격 번쩍임: 그림 전체를 단색으로 칠하지 않고, 몸은 밝게(무늬는 그대로) + 테두리를 그 색으로 빛나게
                float f = saturate(_FlashAmount);
                if (f > 0.001)
                {
                    float2 d = _MainTex_TexelSize.xy * _RimWidth;
                    float a0 = tex.a;
                    float s1 = tex2D(_MainTex, IN.texcoord + float2( d.x, 0)).a;
                    float s2 = tex2D(_MainTex, IN.texcoord + float2(-d.x, 0)).a;
                    float s3 = tex2D(_MainTex, IN.texcoord + float2(0,  d.y)).a;
                    float s4 = tex2D(_MainTex, IN.texcoord + float2(0, -d.y)).a;
                    float s5 = tex2D(_MainTex, IN.texcoord + d * 0.7071).a;
                    float s6 = tex2D(_MainTex, IN.texcoord - d * 0.7071).a;
                    float s7 = tex2D(_MainTex, IN.texcoord + float2(d.x, -d.y) * 0.7071).a;
                    float s8 = tex2D(_MainTex, IN.texcoord + float2(-d.x, d.y) * 0.7071).a;
                    float mx = max(max(max(s1, s2), max(s3, s4)), max(max(s5, s6), max(s7, s8)));
                    float mn = min(min(min(s1, s2), min(s3, s4)), min(min(s5, s6), min(s7, s8)));
                    // 바깥 가장자리만(안쪽 선까지 그리면 날개 · 촉수 사이 빈틈이 많은 그림은 몸 전체가 선 그림처럼 보임)
                    float rim = saturate(mx - a0) * 0.75;

                    color.rgb += _FlashColor.rgb * (f * _FlashBody) * color.a;    // 몸: 밝게(무늬 유지)
                    float ra = rim * f;
                    color.rgb = lerp(color.rgb, _FlashColor.rgb * 1.15, ra);      // 테두리: 그 색으로 빛남
                    color.a = max(color.a, ra * IN.color.a);
                }

                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                color.a *= m.x * m.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                color.rgb *= color.a;
                return color;
            }
        ENDCG
        }
    }
}
