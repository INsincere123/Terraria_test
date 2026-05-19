sampler baseTexture : register(s0);
sampler noiseTexture : register(s1);

float time;
float blackRadius;
float opacity;
float3 accretionDiskColor;

float InverseLerp(float from, float to, float x)
{
    return saturate((x - from) / (to - from));
}

float2 RotatedBy(float2 v, float theta)
{
    float s = sin(theta);
    float c = cos(theta);
    return float2(v.x * c - v.y * s, v.x * s + v.y * c);
}

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float2 centered = coords - 0.5;
    float distanceFromCenter = length(centered);
    float angle = atan2(centered.y, centered.x) / 6.2831853 + 0.5;

    float2 diskCoords = float2(angle * 3.0 + time * 0.45, distanceFromCenter * 4.5 - time * 0.15);
    float diskNoise = tex2D(noiseTexture, diskCoords).r;
    float diskMask = InverseLerp(blackRadius * 2.2, blackRadius * 1.16, distanceFromCenter);
    diskMask *= 1.0 - InverseLerp(blackRadius * 1.02, blackRadius * 1.14, distanceFromCenter);
    diskMask *= saturate(diskNoise * 1.25);

    float horizonRing = exp(-pow((distanceFromCenter - blackRadius * 1.04) / max(blackRadius * 0.045, 0.001), 2.0));
    float edgeGlow = pow(InverseLerp(blackRadius * 1.2, blackRadius * 1.02, distanceFromCenter), 2.8) * (1.0 - InverseLerp(blackRadius * 0.94, blackRadius * 1.0, distanceFromCenter));
    float outerGlow = 0.018 / max(abs(distanceFromCenter - blackRadius * 1.52), 0.028);
    outerGlow *= smoothstep(blackRadius * 2.8, blackRadius * 1.0, distanceFromCenter);

    float coreMask = InverseLerp(blackRadius * 1.0, blackRadius * 0.94, distanceFromCenter);
    float hardCoreMask = InverseLerp(blackRadius * 0.96, blackRadius * 0.9, distanceFromCenter);
    float3 diskColor = accretionDiskColor * (diskMask * 0.95 + outerGlow * 0.45);
    float3 hotColor = lerp(accretionDiskColor, float3(1.0, 0.92, 0.72), 0.75) * (edgeGlow + horizonRing * 1.45);
    float3 color = diskColor + hotColor;
    color = lerp(color, float3(0.0, 0.0, 0.0), coreMask);

    float alpha = saturate(diskMask + edgeGlow + horizonRing + outerGlow + coreMask + hardCoreMask) * opacity;
    alpha = lerp(alpha, opacity, hardCoreMask);
    alpha *= smoothstep(0.5, 0.42, distanceFromCenter);
    return float4(color, alpha) * sampleColor;
}

technique Technique1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
