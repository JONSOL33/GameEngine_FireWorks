Shader "Firefly Festival/Night Sky"
{
    Properties { _Tint("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
            CBUFFER_END
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453); }
            Varyings vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; return o; }
            half4 frag(Varyings i):SV_Target
            {
                float2 uv = (i.uv - .5) * float2(600.0 / 320.0, 360.0 / 190.0) + .5;
                float3 sky = lerp(float3(.018,.066,.073),float3(.003,.009,.024),smoothstep(.19,.86,uv.y));
                float mist = exp(-pow((uv.y-.30)*12,2));
                sky += float3(.015,.024,.017)*mist;
                float2 grid = uv * float2(310,170);
                float2 cell=floor(grid), local=frac(grid)-.5;
                float rnd=hash(cell);
                float star=exp(-dot(local,local)*lerp(500,90,rnd)) * step(.975,rnd);
                star *= (.65+.35*sin(_Time.y*.7+rnd*80)) * smoothstep(.25,.6,uv.y);
                sky += star*float3(.7,.83,.73)*1.3;
                float2 moon=(uv-float2(.255,.655))*float2(1.684,1);
                float disc=1-smoothstep(.017,.018,length(moon));
                float cut=1-smoothstep(.0165,.0175,length(moon-float2(.009,.004)));
                float crescent=saturate(disc-cut);
                float halo=exp(-length(moon)*55)*.055;
                sky += float3(.65,.76,.61)*halo+float3(.86,.88,.67)*crescent*1.2;
                return half4(sky*_Tint.rgb,1);
            }
            ENDHLSL
        }
    }
}
