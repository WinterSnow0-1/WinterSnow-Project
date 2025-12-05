#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

float3 circulateLighting(CustomSurfaceData suraface,BRDFData brdf,CustomLight light)
{
    return saturate(dot(suraface.normal,light.direction)) * light.color;
}

float3 GetLighting(CustomSurfaceData surface,BRDFData brdf)
{
    float3 color = 0;
    for (int i = 0;i<GetDirectionalLightCount();i++)
    {
        CustomLight light = GetDirectionalLight(i);
        color += circulateLighting(surface,brdf,light) * CustomDirectBDRF(surface,brdf,light);
    }
    return color ;
}

#endif