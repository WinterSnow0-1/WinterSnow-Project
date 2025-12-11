#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED


float3 circulateLighting(CustomSurfaceData suraface, BRDFData brdf, CustomLight light)
{
    return saturate(dot(suraface.normal, light.direction)) * light.color;
}

float3 GetLighting(CustomSurfaceData surface, BRDFData brdf,GI gi)
{
    CustomShadowData shadowData = GetShadowData(surface);
    float3 color = gi.diffuse * brdf.diffuse;
    for (int i = 0; i < GetDirectionalLightCount(); i++)
    {
        CustomLight light = GetDirectionalLight(i, surface, shadowData);
        color += circulateLighting(surface, brdf, light) * CustomDirectBDRF(surface, brdf, light) * light.attenuation;
    }
    return color;
}

#endif
