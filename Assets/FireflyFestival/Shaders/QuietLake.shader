Shader "Firefly Festival/Quiet Lake"
{
    Properties { _BaseColor("Deep water", Color) = (.005,.024,.029,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END
            float4 _FireworkPositions[16];
            float4 _FireworkColors[16];
            Varyings vert(Attributes v) { Varyings o; o.world=TransformObjectToWorld(v.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.world); return o; }
            half4 frag(Varyings i):SV_Target
            {
                float2 p=i.world.xz;
                float wave=sin(p.y*3+sin(p.x*.9+_Time.y)*1.4-_Time.y*1.8);
                float ripple=sin(p.y*13+p.x*1.8+_Time.y*2)*.5+.5;
                float3 color=_BaseColor.rgb + float3(.002,.007,.007)*(wave*.5+.5);
                color += float3(.006,.015,.012)*pow(saturate((p.y+60)/170),2);
                float moon=exp(-pow((p.x+32+sin(p.y*1.4+_Time.y)*.8)/3,2)) * exp(-abs(p.y+12)*.05);
                color += moon * float3(.09,.12,.065) * pow(ripple,3)*.45;
                [unroll] for(int index=0; index<16; index++)
                {
                    float4 light=_FireworkPositions[index];
                    float t=(_WorldSpaceCameraPos.y-i.world.y)/max(.1,_WorldSpaceCameraPos.y+light.y-2*i.world.y);
                    float2 reflection=lerp(_WorldSpaceCameraPos.xz,light.xz,t);
                    float dx=p.x-reflection.x+sin(p.y*2+_Time.y)*.36;
                    float dz=p.y-reflection.y;
                    float glow=exp(-dx*dx/(1.6+light.y*.022))*exp(-abs(dz)*.095);
                    color += _FireworkColors[index].rgb * light.w * glow * (.07+pow(ripple,6)*.52) * (.4+.6*saturate(wave));
                }
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
