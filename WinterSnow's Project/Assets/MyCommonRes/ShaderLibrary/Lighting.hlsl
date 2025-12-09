#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

#ifndef CUSTOM_SHADOWS_INCLUDED
#include "Shadows.hlsl"
#endif

float3 circulateLighting(CustomSurfaceData suraface, BRDFData brdf, CustomLight light)
{
    return saturate(dot(suraface.normal, light.direction)) * light.color;
}

float3 GetLighting(CustomSurfaceData surface, BRDFData brdf)
{
    CustomShadowData shadowData = GetShadowData(surface);
    float3 color = 0;
    for (int i = 0; i < GetDirectionalLightCount(); i++)
    {
        CustomLight light = GetDirectionalLight(i, surface, shadowData);
        color += circulateLighting(surface, brdf, light) * CustomDirectBDRF(surface, brdf, light) * light.attenuation;
    }
    return color;
}

#endif
