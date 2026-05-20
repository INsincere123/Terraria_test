sampler uImage0 : register(s0);
sampler occlusionTexture : register(s1);
sampler accretionNoiseTexture : register(s2);
sampler diskTexture : register(s3);
sampler diskFlowTexture : register(s4);

float time;
float distortionStrength;
float maxLensingAngle;
float lensingFalloff;
float accretionOuterRadiusFactor;
float accretionInnerStartFactor;
float accretionInnerFullFactor;
float accretionNoiseStrength;
float hotRingRadiusFactor;
float hotRingWidthFactor;
float sourceRadii[5];
float sourceStrengths[5];
float sourceOcclusionRadii[5];
float sourceOcclusionOpacities[5];
float sourceAccretionOpacities[5];
float3 sourceAccretionColors[5];
float sourceDiskModes[5];
float2 sourcePositions[5];
float2 aspectRatioCorrectionFactor;
float2 zoom;
int sourceCount;

float2 RotatedBy(float2 v, float theta)
{
    float s = sin(theta);
    float c = cos(theta);
    return float2(v.x * c - v.y * s, v.x * s + v.y * c);
}

float InverseLerp(float from, float to, float x)
{
    return saturate((x - from) / (to - from));
}

float CalculateLensingAngle(float sourceRadius, float sourceStrength, float2 coords, float2 sourcePosition)
{
    float distanceToSource = distance((coords - sourcePosition) * aspectRatioCorrectionFactor, float2(0.0, 0.0));
    return distortionStrength * sourceStrength * maxLensingAngle * exp(-distanceToSource / sourceRadius * lensingFalloff);
}

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float2 distortedCoords = coords;

    for (int i = 0; i < 5; i++)
    {
        if (i >= sourceCount)
            break;

        float angle = CalculateLensingAngle(sourceRadii[i], sourceStrengths[i], distortedCoords, sourcePositions[i]);
        float2 localDistortedCoords = (distortedCoords - sourcePositions[i]) * aspectRatioCorrectionFactor;
        localDistortedCoords = RotatedBy(localDistortedCoords, angle);
        distortedCoords = sourcePositions[i] + localDistortedCoords / aspectRatioCorrectionFactor;
    }

    float4 color = tex2D(uImage0, distortedCoords);

    for (int j = 0; j < 5; j++)
    {
        if (j >= sourceCount)
            break;

        float sourceRadius = sourceRadii[j];
        float occlusionRadius = sourceOcclusionRadii[j];
        float occlusionOpacity = sourceOcclusionOpacities[j];
        float2 correctedDelta = (coords - sourcePositions[j]) * aspectRatioCorrectionFactor;

        float accretionOpacity = sourceAccretionOpacities[j];
        if (accretionOpacity > 0.0)
        {
            float radialDistance = length(correctedDelta);
            float angle = atan2(correctedDelta.y, correctedDelta.x) / 6.2831853 + 0.5;
            float2 noiseCoords = float2(angle * 3.0 + time * 0.45, radialDistance / max(sourceRadius, 0.0001) * 2.4 - time * 0.15);
            float noise = tex2D(accretionNoiseTexture, noiseCoords).r;
            float outerMask = InverseLerp(sourceRadius * accretionOuterRadiusFactor, sourceRadius * 0.28, radialDistance);
            float innerMask = InverseLerp(occlusionRadius * accretionInnerStartFactor, occlusionRadius * accretionInnerFullFactor, radialDistance);
            float hotRing = exp(-pow((radialDistance - occlusionRadius * hotRingRadiusFactor) / max(occlusionRadius * hotRingWidthFactor, 0.0001), 2.0));
            float3 hotColor = lerp(sourceAccretionColors[j], float3(1.0, 0.92, 0.7), 0.72);

            if (sourceDiskModes[j] > 0.5)
            {
                float2 flowCoords = float2(angle * 2.0 - time * 0.16, radialDistance / max(sourceRadius, 0.0001) * 1.7 + time * 0.07);
                float2 flow = tex2D(diskFlowTexture, flowCoords).rg * 2.0 - 1.0;
                float2 diskCoords = float2(angle * 2.8 + time * 0.32, radialDistance / max(sourceRadius, 0.0001) * 0.88 - time * 0.12);
                float4 diskSample = tex2D(diskTexture, diskCoords + flow * 0.028);
                float luma = dot(diskSample.rgb, float3(0.299, 0.587, 0.114));
                float materialMask = saturate((luma * 1.35 + diskSample.a * 0.55) * outerMask * innerMask);
                float3 materialColor = lerp(sourceAccretionColors[j] * max(luma, 0.18), diskSample.rgb, 0.42);
                color.rgb += (materialColor * materialMask * (0.9 + noise * 0.28) + hotColor * hotRing * 0.62) * accretionOpacity;
            }
            else
            {
                float diskMask = saturate(outerMask * innerMask * noise * accretionNoiseStrength);
                color.rgb += (sourceAccretionColors[j] * diskMask * 0.85 + hotColor * hotRing * 0.72) * accretionOpacity;
            }
        }

        if (occlusionRadius <= 0.0 || occlusionOpacity <= 0.0)
            continue;

        float2 occlusionCoords = correctedDelta / occlusionRadius * 0.5 + 0.5;
        if (occlusionCoords.x < 0.0 || occlusionCoords.x > 1.0 || occlusionCoords.y < 0.0 || occlusionCoords.y > 1.0)
            continue;

        float4 occlusion = tex2D(occlusionTexture, occlusionCoords);
        color.rgb = lerp(color.rgb, occlusion.rgb, occlusion.a * occlusionOpacity);
    }

    return color;
}

technique Technique1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
