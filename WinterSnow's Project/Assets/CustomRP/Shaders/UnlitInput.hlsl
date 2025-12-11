#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED


#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/SurfaceData.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

#pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile_instancing


// GPU Instancing
/// UNITY_DEFINE_INSTANCED_PROP里按照每个示例进行一次存储。
/// 因此获取时，不能单纯去调用 普通的uniform，而是必须通过 UNITY_ACCESS_INSTANCED_PROP（UnityPerMaterial，参数名）的方式进行获取
/// 因此 当UNITY_DEFINE_INSTANCED_PROP(float4,_BaseMap_ST)时， o.uv = TRANSFORM_TEX(v.uv, _BaseMap); 会调用普通的_BaseMap_ST，导致报错。
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial) //声明变量
UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionMap_ST)
UNITY_DEFINE_INSTANCED_PROP(float4, _BaseCol)
UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);


float2 TransformBaseUV (float2 baseUV) {
    float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
    return baseUV * baseST.xy + baseST.zw;
}

float4 GetBase (float2 baseUV) {
    float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV);
    float4 color = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseCol);
    return map * color;
}

float GetCutoff (float2 baseUV) {
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff);
}

float GetMetallic (float2 baseUV) {
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic);
}

float GetSmoothness (float2 baseUV) {
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness);
}

float3 GetEmission (float2 baseUV) {
    return GetBase(baseUV).rgb;
}

#endif