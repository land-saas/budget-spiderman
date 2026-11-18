Shader "ForestRoadtrip25D/Pixel Cutout"
{
    Properties
    {
        _MainTex("Texture",2D)="white"{}
        _Tint("Tint",Color)=(0.90,0.96,1,1)
        _Cutoff("Alpha cutoff",Range(0,1))=0.5
        _FlipX("Flip horizontal",Float)=0
        _SwayAmplitude("Canopy sway",Float)=0
        _SwaySpeed("Wind speed",Float)=1.5
        _SwayPhase("Wind phase",Float)=0
        _WindSampleTime("Preview time (-1 = live)",Float)=-1
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest"}
        Cull Off ZWrite On
        Pass
        {
            Tags {"LightMode"="SRPDefaultUnlit"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Cutoff;
                float _FlipX;
                float _SwayAmplitude;
                float _SwaySpeed;
                float _SwayPhase;
                float _WindSampleTime;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            Varyings Vert(Attributes v)
            {
                Varyings o;
                float canopy=smoothstep(.28,1.0,v.uv.y);
                float clock=_WindSampleTime<0?_Time.y:_WindSampleTime;
                float wind=sin(clock*_SwaySpeed+_SwayPhase)+.25*sin(clock*_SwaySpeed*2.2+_SwayPhase*1.6+v.uv.y*3);
                v.positionOS.x+=wind*_SwayAmplitude*canopy*canopy;
                o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                i.uv.x=lerp(i.uv.x,1-i.uv.x,_FlipX);
                half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*_Tint;
                clip(c.a-_Cutoff);return half4(c.rgb,1);
            }
            ENDHLSL
        }
    }
}
