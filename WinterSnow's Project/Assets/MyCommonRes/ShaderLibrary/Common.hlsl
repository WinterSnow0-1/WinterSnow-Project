#ifndef CUSTOM_COMMON_INCLUDED
#define CUSTOM_COMMON_INCLUDED

float DistanceSquared(float3 pA, float3 pB)
{
    return dot(pA - pB, pA - pB);
}



#endif
