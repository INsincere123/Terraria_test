sampler baseTexture : register(s0);

float2 sourcePositions[3];
float sourceStrengths[3];
float2 textureSize;
float lensRadius;
float distortionStrength;
int sourceCount;

float2 RotatedBy(float2 v, float theta)
{
    float s = sin(theta);
    float c = cos(theta);
    return float2(v.x * c - v.y * s, v.x * s + v.y * c);
}

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float2 distortedCoords = coords;

    for (int i = 0; i < 3; i++)
    {
        if (i >= sourceCount)
            break;

        // 使用像素空间旋转，保持透镜圆形，并消除复用纹理尺寸对半径的影响。
        float2 delta = (distortedCoords - sourcePositions[i]) * textureSize;
        float distanceToSource = length(delta);
        float lensMask = exp(-distanceToSource / max(lensRadius, 0.0001));
        float angle = sourceStrengths[i] * distortionStrength * lensMask;
        distortedCoords = RotatedBy(delta, angle) / textureSize + sourcePositions[i];
    }

    float4 color = tex2D(baseTexture, distortedCoords) * sampleColor;
    return color;
}

technique Technique1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
