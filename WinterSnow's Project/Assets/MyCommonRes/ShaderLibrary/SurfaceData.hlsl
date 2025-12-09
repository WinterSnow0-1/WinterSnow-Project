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

CustomSurfaceData GenSurfaceData(v2f input, float4 col, float smoothness, float metallic)
{
    CustomSurfaceData surface;
    surface.normal = input.normal;
    surface.color = col.rgb;
    surface.alpha = col.a;
    surface.vDir = normalize(_WorldSpaceCameraPos - input.posWS);
    surface.smoothness = smoothness;
    surface.metallic = metallic;
    surface.position = input.posWS;
    surface.depth = -TransformWorldToView(input.posWS).z;
    surface.dither = InterleavedGradientNoise(input.positionCS.xy, 0);
    return surface;
}

#endif
