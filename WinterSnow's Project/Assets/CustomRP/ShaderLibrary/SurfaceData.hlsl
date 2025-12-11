#ifndef CUSTOM_SURFACEDATA_INCLUDED
#define CUSTOM_SURFACEDATA_INCLUDED
    
struct CustomSurfaceData
{
    float3 position;
    float3 normal;
    float3 color;
    float3 vDir;
    float depth;
    float alpha;
    float smoothness;
    float metallic;
    float dither;
};

#endif
