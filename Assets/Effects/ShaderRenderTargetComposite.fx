sampler overlayTexture : register(s0);
sampler noiseTexture : register(s1);

float time;
float2 screenSize;
float overlayStrength;
float noiseStrength;
float chromaticStrength;

float Hash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float ValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);

    float a = Hash(i);
    float b = Hash(i + float2(1.0, 0.0));
    float c = Hash(i + float2(0.0, 1.0));
    float d = Hash(i + float2(1.0, 1.0));

    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float2 aspect = float2(max(screenSize.x / max(screenSize.y, 1.0), 0.001), 1.0);
    float2 centered = (coords - 0.5) * aspect;
    float radial = length(centered);
    float wave = ValueNoise(coords * 13.0 + float2(time * 0.72, -time * 0.31));
    float textureNoise = tex2D(noiseTexture, coords * 2.35 + float2(time * 0.045, -time * 0.032)).r;
    float n = lerp(wave, textureNoise, 0.65);
    float2 normal = normalize(centered + float2(0.0001, 0.0001));
    float2 swirl = float2(-normal.y, normal.x);
    float2 offset = (normal * (n - 0.5) + swirl * sin(time * 2.1 + radial * 18.0) * 0.28) * noiseStrength;

    float4 baseOverlay = tex2D(overlayTexture, coords + offset * 0.45) * sampleColor;
    float4 redShift = tex2D(overlayTexture, coords + offset * (0.72 + chromaticStrength * 0.16));
    float4 blueShift = tex2D(overlayTexture, coords - offset * (0.72 + chromaticStrength * 0.16));

    float4 color = baseOverlay;
    color.r = max(color.r, redShift.r * (0.94 + chromaticStrength * 0.12));
    color.b = max(color.b, blueShift.b * (0.88 + chromaticStrength * 0.18));
    color.g = max(color.g, baseOverlay.g);

    float vignette = smoothstep(0.92, 0.16, radial);
    float shimmer = 0.82 + n * 0.28;
    color.rgb *= shimmer * overlayStrength;
    color.a = saturate(color.a * (0.78 + vignette * 0.32) * overlayStrength);

    return color;
}

technique Technique1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
