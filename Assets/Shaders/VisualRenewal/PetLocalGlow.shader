Shader "Mongle/VisualRenewal/PetLocalGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Color", 2D) = "white" {}
        _MaskTex ("RGBA: Glow/Rim/Sheen/Protect", 2D) = "black" {}
        _Color ("Material Tint", Color) = (1,1,1,1)
        [HDR] _GlowColor ("Glow HDR Color", Color) = (0.2,1,0.8,1)
        _GlowStrength ("Glow Strength", Range(0,8)) = 0
        _PulseHz ("Pulse Cycles Per Second", Range(0,3)) = 0.35
        _PulseMin ("Minimum Pulse", Range(0,1)) = 0.75
        _PulsePhase ("Pulse Phase (radians)", Float) = 0
        [HDR] _RimColor ("Painted Rim Color", Color) = (0.3,0.8,1,1)
        _RimStrength ("Painted Rim Strength", Range(0,3)) = 0
        [HDR] _SheenColor ("Sheen Color", Color) = (1,0.8,0.3,1)
        _SheenStrength ("Sheen Strength", Range(0,3)) = 0
        _SheenSpeed ("Sheen Speed", Range(0,2)) = 0.2
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent"
               "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="False" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        // RGB는 straight alpha, 알파는 over 합성. 투명 픽셀의 발광은 배경에 더하지 않는다.
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

        Pass
        {
            Name "PetUnlit"
            // URP Forward 및 2D Renderer의 unlit 패스. 2D Light 조명은 계산하지 않는다.
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            // Unity 6000.3 동봉 URP의 Sprite 색/flip 규약을 사용한다. CPU SpriteSkin용.
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _GlowColor, _RimColor, _SheenColor;
                float _GlowStrength, _PulseHz, _PulseMin, _PulsePhase;
                float _RimStrength, _SheenStrength, _SheenSpeed;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 position = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                output.positionCS = TransformObjectToHClip(position);
                output.uv = input.uv;
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                half4 mask = saturate(SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv));
                half allowed = 1.0h - mask.a;
                half pulse = lerp(saturate(_PulseMin), 1.0h,
                    0.5h + 0.5h * sin(_Time.y * 6.28318530718 * _PulseHz + _PulsePhase));
                // R만 Emission에 사용. G는작화된 림, B는 국소 광택이다.
                half3 glow = mask.r * allowed * _GlowColor.rgb * _GlowStrength * pulse;
                half3 rim = mask.g * allowed * _RimColor.rgb * _RimStrength;
                half sweep = pow(saturate(1.0h - abs(frac(input.uv.x + input.uv.y * 0.2
                    - _Time.y * _SheenSpeed) - 0.5h) * 12.0h), 2.0h);
                half3 sheen = mask.b * allowed * _SheenColor.rgb * _SheenStrength * sweep;
                // 알파는 원화/틴트 알파 그대로. 발광 때문에 외곽 영역을 불투명하게 만들지 않는다.
                return half4(baseColor.rgb + glow + rim + sheen, baseColor.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
