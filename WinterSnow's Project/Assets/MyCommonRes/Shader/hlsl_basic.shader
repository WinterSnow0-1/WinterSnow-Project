Shader "URP/falushan"
{
    Properties //着色器的输入 
    {
        _BaseMap ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags {
            "RenderType"="Opaque"
            "RenderPipeLine"="UniversalRenderPipeline" //用于指明使用URP来渲染
        }

        HLSLINCLUDE 
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl" 
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl" 
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        #pragma multi_compile_instancing

        // SRP Batching
        CBUFFER_START(UnityPerMaterial) //声明变量
            float4 _BaseMap_ST;
        CBUFFER_END

        TEXTURE2D(_BaseMap); //贴图采样  
        SAMPLER(sampler_BaseMap);

        struct a2v //顶点着色器
        {
            float4 vertex: POSITION;
            float4 normal: NORMAL;
            float3 tangent: TANGENT;
            half4 vertexColor: COLOR;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct v2f //片元着色器
        {
            float4 positionCS: SV_POSITION;
            float2 uv: TEXCOORD0;
            float3 normal : TEXCOORD1;
            float3 tangent : TEXCOORD2;
            float3 bitangent : TEXCOORD3;
            float4 screenUV : TEXCOORD4;
            float4 posWS : TEXCOORD5;
            half4 vertexColor: COLOR;
        }; 

        ENDHLSL

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag


            v2f vert (a2v v)
            {
                v2f o;
                //instance branch
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v,o)
                o.positionCS = TransformObjectToHClip(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.vertexColor = v.vertexColor;
                o.normal = TransformObjectToWorldNormal(v.normal);
                o.tangent = TransformObjectToWorldDir(v.tangent);
                o.bitangent = cross(o.normal,o.tangent);
                o.posWS = mul(unity_ObjectToWorld, v.vertex);
                o.screenUV =  ComputeScreenPos(o.positionCS);
                return o;
            }

            half4 frag (v2f i) : SV_Target  /* 注意在HLSL中，fixed4类型变成了half4类型*/
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float2 screenUV = i.screenUV.xy / i.screenUV.w;
                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                return half4(col);
            }
            ENDHLSL
        }
    }
}