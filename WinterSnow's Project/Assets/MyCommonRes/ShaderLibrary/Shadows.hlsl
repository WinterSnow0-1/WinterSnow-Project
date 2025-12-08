#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4

#ifndef CUSTOM_SURFACEDATA_INCLUDED
#include "SurfaceData.hlsl"
#endif

/// 和传统的TEXTURE2D、SAMPLER定义不同，阴影的定义有特殊方式。
/// https://developer.unity.cn/ask/question/67276b13edbc2a001f16717e
TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
    float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT];
CBUFFER_END

struct DirectionalShadowData {
    float strength;
    int tileIndex;
};

float SampleDirectionalShadowAtlas (float3 positionSTS) {
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

float GetDirectionalShadowAttenuation (DirectionalShadowData data, CustomSurfaceData surfaceWS) {
    
    if (data.strength <= 0.0)
        return 1.0;
    
    float3 positionSTS = mul(_DirectionalShadowMatrices[data.tileIndex],float4(surfaceWS.position, 1.0)).xyz;
    float shadow = SampleDirectionalShadowAtlas(positionSTS);

    return lerp(1.0, shadow, data.strength);
}

#endif