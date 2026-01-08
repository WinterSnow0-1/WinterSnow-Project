#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

///我们可以手动进入观察函数 SampleShadow_ComputeSamples_Tent_ 函数，DIRECTIONAL_FILTER_SAMPLES决定采样次数，DIRECTIONAL_FILTER_SETUP决定采样函数
///同时注意提升PCF采样时，会增加皮特潘现象，为什么？
///
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#if defined(_DIRECTIONAL_PCF3)
    #define DIRECTIONAL_FILTER_SAMPLES 4
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
    #define DIRECTIONAL_FILTER_SAMPLES 9
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
    #define DIRECTIONAL_FILTER_SAMPLES 16
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_CASCADE_COUNT 4


/// 和传统的TEXTURE2D、SAMPLER定义不同，阴影的定义有特殊方式。
/// https://developer.unity.cn/ask/question/67276b13edbc2a001f16717e
TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

/// 多个光源 x  多个cascade距离摄像机
/// 同时有一个点需要注意：尽管我们修改了数组的大小，但是在GPU的环境下，固定数组一旦形成，就不会同时更改。我们必须要重启unity。
/// 实测也确实如此
CBUFFER_START(_CustomShadows)
    int _CascadeCount;
    float4 _ShadowAtlasSize;
    float4 _ShadowDistanceFade;
    float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
    float4 _CascadeData[MAX_CASCADE_COUNT];
    float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
CBUFFER_END

struct ShadowMask {
	bool always;
    bool distance;
    float4 shadows;
};

struct DirectionalShadowData
{
    float strength;
    int tileIndex;
    float normalBias;
    int shadowMaskChannel;
};

struct OtherShadowData {
    float strength;
    int shadowMaskChannel;
};

struct CustomShadowData
{
    int cascadeIndex;
    float strength;
    float cascadeBlend;
    ShadowMask shadowMask;
};



float FadedShadowStrength(float distance, float scale, float fade)
{
    return saturate((1.0 - distance * scale) * fade);
}



CustomShadowData GetShadowData(CustomSurfaceData surfaceWS)
{

    CustomShadowData data;
    data.shadowMask.distance = false;
    data.shadowMask.shadows = 1;
    data.cascadeBlend = 1;
    data.strength = FadedShadowStrength(surfaceWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y);
    int i;
    for (i = 0; i < _CascadeCount; i++)
    {
        float4 sphere = _CascadeCullingSpheres[i];
        float distanceSqr = DistanceSquared(surfaceWS.position, sphere.xyz);
        if (distanceSqr < sphere.w)
        {
            float fade = FadedShadowStrength(distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z);
            if (i == _CascadeCount - 1)
                data.strength *= fade;
            else
                data.cascadeBlend = fade;
            break;
        }
    }
    if (i == _CascadeCount)
        data.strength = 0.0;

    #if defined(_CASCADE_BLEND_DITHER)
    else if (data.cascadeBlend < surfaceWS.dither) 
        i += 1;
    #endif

    #if !defined(_CASCADE_BLEND_SOFT)
    data.cascadeBlend = 1.0;
    #endif
    data.cascadeIndex = i;
    return data;
}

/// SAMPLE_TEXTURE2D_SHADOW 会自动返回 depth > pos.z ? 1 : 0（根据平台不同，判断不同）
float SampleDirectionalShadowAtlas(float3 positionSTS)
{
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

float FilterDirectionalShadow(float3 positionSTS)
{
    #if defined(DIRECTIONAL_FILTER_SETUP)
    float weights[DIRECTIONAL_FILTER_SAMPLES];
    float2 positions[DIRECTIONAL_FILTER_SAMPLES];
    float4 size = _ShadowAtlasSize.yyxx;
    DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);
    float shadow = 0;
    for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++)
        shadow += weights[i] * SampleDirectionalShadowAtlas(float3(positions[i].xy, positionSTS.z));
    return shadow;
    #else
    return SampleDirectionalShadowAtlas(positionSTS);
    #endif
}

float GetBakedShadow (ShadowMask mask,int channel) {
    float shadow = 1.0;
    if (mask.distance || mask.always) {
        if (channel >= 0)
        {
            shadow = mask.shadows[channel];
        }
    }
    return shadow;
}


float GetCascadedShadow ( DirectionalShadowData directional, CustomShadowData global, CustomSurfaceData surfaceWS)
{
    float3 normalBias = surfaceWS.normal * (directional.normalBias * _CascadeData[global.cascadeIndex].y);
    float3 positionSTS = mul(_DirectionalShadowMatrices[directional.tileIndex], float4(surfaceWS.position + normalBias, 1.0)).xyz;
    float shadow = FilterDirectionalShadow(positionSTS);

    if (global.cascadeBlend < 1.0)
    {
        normalBias = surfaceWS.normal * (directional.normalBias * _CascadeData[global.cascadeIndex + 1].y);
        positionSTS = mul(_DirectionalShadowMatrices[directional.tileIndex + 1], float4(surfaceWS.position + normalBias, 1.0)).xyz;
        shadow = lerp(FilterDirectionalShadow(positionSTS), shadow, global.cascadeBlend);
    }
	return shadow;
}

float MixBakedAndRealtimeShadows (
    CustomShadowData global, float shadow, int shadowMaskChannel, float strength
) {
    float baked = GetBakedShadow(global.shadowMask,shadowMaskChannel);
    if (global.shadowMask.always) {
        shadow = lerp(1.0, shadow, global.strength);
        shadow = min(baked, shadow);
        return lerp(1.0, shadow, strength);
    }
    if (global.shadowMask.distance) {
        shadow = lerp(baked,shadow,strength);
        return lerp(1,shadow,strength);
    }
    return lerp(1.0, shadow, strength * global.strength);
}

float GetBakedShadow (ShadowMask mask,int channel, float strength) {
    if (mask.distance || mask.always) {
        return lerp(1.0, GetBakedShadow(mask,channel), strength);
    }
    return 1.0;
}

float GetDirectionalShadowAttenuation(DirectionalShadowData directional, CustomShadowData global, CustomSurfaceData surfaceWS)
{
    float shadow;
    
    if (directional.strength * global.strength <= 0.0)
        shadow = GetBakedShadow(global.shadowMask,directional.shadowMaskChannel, abs(directional.strength));
    else
    {
        shadow = GetCascadedShadow(directional, global, surfaceWS);
        shadow = MixBakedAndRealtimeShadows(global, shadow,directional.shadowMaskChannel, directional.strength);
        shadow = lerp(1.0, shadow, directional.strength);
    }
return shadow;
}

float GetOtherShadowAttenuation ( OtherShadowData other, CustomShadowData global, CustomSurfaceData surfaceWS) {
    #if !defined(_RECEIVE_SHADOWS)
    return 1.0;
    #endif
    float shadow;
    if (other.strength > 0.0) 
        shadow = GetBakedShadow(global.shadowMask, other.shadowMaskChannel, other.strength);
    else 
        shadow = 1.0;
    return shadow;
}



#endif
