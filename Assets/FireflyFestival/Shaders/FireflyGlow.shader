Shader "Firefly Festival/Particle Glow"
{
    Properties { _Intensity("Light intensity", Float) = 3 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
            CBUFFER_END
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float radius = length(i.uv * 2 - 1);
                float halo = exp(-radius * radius * 4) * .5;
                float core = exp(-radius * radius * 36);
                float mask = saturate(1 - radius) * (halo + core);
                return half4(i.color.rgb * _Intensity, i.color.a * mask);
            }
            ENDHLSL
        }
    }
}
