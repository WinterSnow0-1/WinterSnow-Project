Shader "URP/falushan"
{
    Properties //着色器的输入 
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseCol ("颜色", color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend",float) =1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend",float) =1
        [Enum(Off,0,On,1)] _ZWrite("Z Write",float) = 1
        _clipRange ("裁剪范围",Range(0,1)) = 0.5
        [Toggle(CLIP_ON)] _clipOn ("开启裁剪",float) = 0
    }
    SubShader
    {
        Tags {
            "RenderType"="Opaque"
            "RenderPipeLine"="UniversalRenderPipeline" //用于指明使用URP来渲染
        }

        
        HLSLINCLUDE 
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl" 
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl" 
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        #pragma multi_compile_instancing
        #pragma shader_feature CLIP_ON

        // SRP Batching
        /*CBUFFER_START(UnityPerMaterial) //声明变量
            float4 _BaseMap_ST;
            float4 _BaseCol;
        CBUFFER_END*/

        // GPU Instancing
        /// UNITY_DEFINE_INSTANCED_PROP里按照每个示例进行一次存储。
        /// 因此获取时，不能单纯去调用 普通的uniform，而是必须通过 UNITY_ACCESS_INSTANCED_PROP（UnityPerMaterial，参数名）的方式进行获取
        /// 因此 当UNITY_DEFINE_INSTANCED_PROP(float4,_BaseMap_ST)时， o.uv = TRANSFORM_TEX(v.uv, _BaseMap); 会调用普通的_BaseMap_ST，导致报错。
        UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
        UNITY_DEFINE_INSTANCED_PROP(float4,_BaseCol)
        UNITY_DEFINE_INSTANCED_PROP(float4,_BaseMap_ST)
        UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)
        float _clipRange;

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
            UNITY_VERTEX_INPUT_INSTANCE_ID
            
        }; 

        ENDHLSL

        Pass
        {
            
            Blend [_SrcBlend][_DstBlend]
            ZWrite [_ZWrite]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            

            v2f vert (a2v v)
            {
                v2f o;
                //instance branch
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v,o);
                o.positionCS = TransformObjectToHClip(v.vertex);
                float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial,_BaseMap_ST);
                o.uv = baseST.xy * v.uv + baseST.zw;
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
                //half4 col = _BaseCol;
                float4 base = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
                half4 col = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial,_BaseCol) * base;
                #if CLIP_ON
                    clip(base.r - _clipRange);
                #endif
                
                return half4(col);
            }
            ENDHLSL
        }

        Pass {
			Tags {
				"LightMode" = "ShadowCaster"
			}

			ColorMask 0

			HLSLPROGRAM
			#pragma target 3.5
			#pragma shader_feature _CLIPPING
			#pragma multi_compile_instancing
			#pragma vertex ShadowCasterPassVertex
			#pragma fragment ShadowCasterPassFragment
			#include "ShadowCasterPass.hlsl"
			ENDHLSL
		}
    }
    CustomEditor "CustomShaderGUI"
}