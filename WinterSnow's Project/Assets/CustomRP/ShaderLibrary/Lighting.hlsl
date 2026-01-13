#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED


float3 circulateLighting(CustomSurfaceData suraface, BRDFData brdf, CustomLight light)
{
    return saturate(dot(suraface.normal, light.direction)) * light.color;
}

float3 GetLighting(CustomSurfaceData surface, BRDFData brdf,GI gi)
{
    CustomShadowData shadowData = GetShadowData(surface);
    shadowData.shadowMask = gi.shadowMask;
    float3 color = gi.diffuse * brdf.diffuse;
    for (int i = 0; i < GetDirectionalLightCount(); i++)
    {
        CustomLight light = GetDirectionalLight(i, surface, shadowData);
        color += circulateLighting(surface, brdf, light) * CustomDirectBDRF(surface, brdf, light) * light.attenuation;
    }

    #if defined(_LIGHTS_PER_OBJECT)
        for (int j = 0;j < min(unity_LightData.y, 8); j++) {
            int lightIndex = unity_LightIndices[j / 4][j % 4];
            CustomLight light = GetOtherLight(lightIndex, surface, shadowData);
            color += circulateLighting(surface, brdf, light) * CustomDirectBDRF(surface, brdf, light) * light.attenuation;
        }
    #else
        for (int j = 0; j < GetOtherLightCount(); j++) {
            CustomLight light = GetOtherLight(j, surface, shadowData);
            color += circulateLighting(surface, brdf, light) * CustomDirectBDRF(surface, brdf, light) * light.attenuation;
            
        }
	#endif
    
    return color;
}

#endif
