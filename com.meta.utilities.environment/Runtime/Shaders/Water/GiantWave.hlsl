// Copyright (c) Meta Platforms, Inc. and affiliates.

#ifndef GIANT_WAVE_INCLUDED
#define GIANT_WAVE_INCLUDED

#ifdef __INTELLISENSE__
float4 _Time;
#endif

const static float Pi = radians(180.0);

float _GiantWaveDelta, _GiantWaveDistance, _GiantWaveDuration;
float3 _GiantWaveOffset;

float Remap(float1 v, float1 pMin, float1 pMax = 1.0, float1 nMin = 0.0, float1 nMax = 1.0)
{
    return nMin + (v - pMin) * rcp(pMax - pMin) * (nMax - nMin);
}

void GiantWave_float(float3 position, float height, float width, float length, float angle, float2 center, float falloff, float phase, float steepness, float curveStrength, out float3 displacement, out float3 normal, out float3 tangent)
{
    #ifdef _GIANT_WAVE_ENABLED
        bool giantWaveEnabled = true;
    #else
        bool giantWaveEnabled = false;
    #endif
    
    displacement = 0;
    normal = float3(0, 1, 0);
    tangent = float3(1, 0, 0);
    
    if(!giantWaveEnabled)
        return;
    
    float fadeInTime = 1.5;
    
    float freq = 2 * Pi / length;
     float gravity = 9.81;
    float speed = sqrt(gravity * freq);
    
    center += _GiantWaveOffset.xz;
    phase = (phase + _GiantWaveDelta) * speed;
    
    float angleRads = angle * 2 * Pi;
    float2 dir = float2(cos(angleRads), sin(angleRads));
    float x = dot(position.xz - center, dir) + phase * length * rcp(2.0 * Pi);
    float fade = saturate((0.75 * length - abs(x)) / (0.5 * length));
    
    float x1 = dot(position.xz - center, dir.yx);
    float widthFade = saturate(width / falloff - abs(x1 / falloff));
    
    float ampFade = saturate(Remap(_GiantWaveDelta, _GiantWaveDuration, _GiantWaveDuration * 0.25));
    
    ampFade *= saturate(Remap(_GiantWaveDelta, 0.0, _GiantWaveDuration * 0.1));
    
    float amp = height * fade * widthFade * ampFade;
    float q = fade * ampFade * steepness / max(1e-3, freq * amp);
   
    
   // phase = _Time.y * speed;
    
   // phase = (0.5 + phase * 0.5) * Pi ;
    
    float factor = freq * dot(position.xz - center, dir) + phase + 0.5 * Pi;
    float sinFactor = sin(factor);
    float cosFactor = cos(factor);
    
    displacement.x = q * amp * dir.x * cosFactor;
    displacement.y = amp * sinFactor;
    displacement.z = q * amp * dir.y * cosFactor;
    
    // Move top of wave forward
    float curveFactor =  amp * pow(saturate(smoothstep(0.0, 1.0, sinFactor)), 2) *1.5 * saturate(Remap(_GiantWaveDelta, _GiantWaveDuration * 0.75, _GiantWaveDuration * 0.1));
    displacement.xz += dir * curveFactor * curveStrength;
    
    normal.x = -dir.x * freq * amp * 1 * cosFactor;
    normal.y = 1.0 - q * freq * amp * sinFactor;
    normal.z = -dir.y * freq * amp * 1 * cosFactor;
    
    tangent.x = 1.0 - q * dir.y * dir.y * freq * amp * sinFactor;
    tangent.y = dir.y * freq * amp * cosFactor;
    tangent.z = -q * dir.x * dir.y * freq * amp * sinFactor;
    
    normal = normalize(normal);
    tangent = normalize(tangent);
}

#endif