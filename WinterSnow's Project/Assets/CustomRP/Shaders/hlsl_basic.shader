Shader "URP/CustomUnlit"
{
    Properties //着色器的输入 
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("颜色", color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend",float) =1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend",float) =1
        [Enum(Off,0,On,1)] _ZWrite("Z Write",float) = 1
        _Cutoff ("裁剪范围",Range(0,1)) = 0.5
		[Toggle(_CLIPPING)] _Clipping ("开启裁剪", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeLine"="UniversalRenderPipeline" //用于指明使用URP来渲染
        }


        HLSLINCLUDE		
		#include "../ShaderLibrary/Common.hlsl"
		#include "UnlitInput.hlsl"
        #pragma multi_compile_instancing
        #pragma shader_feature CLIP_ON
        
        ENDHLSL

        Pass
        {

            Blend [_SrcBlend][_DstBlend]
            ZWrite [_ZWrite]
            HLSLPROGRAM
            #pragma shader_feature _CLIPPING
            #pragma multi_compile_instancing
			#include "UnlitPass.hlsl"
			#pragma vertex UnlitPassVertex
			#pragma fragment UnlitPassFragment
            ENDHLSL
        }

        Pass
        {
            Tags
            {
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