sampler image : register(s1);
sampler haloNoise : register(s2);
sampler spikeMask : register(s3);
sampler radialRamp : register(s4);

matrix uWorldViewProjection;
float globalTime;
float starTier;
float starIntensity;
float3 texturePresence;

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float3 TextureCoordinates : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TextureCoordinates : TEXCOORD0;
};

VertexShaderOutput VertexShaderFunction(in VertexShaderInput input)
{
    VertexShaderOutput output = (VertexShaderOutput) 0;
    output.Position = mul(input.Position, uWorldViewProjection);
    output.Color = input.Color;
    output.TextureCoordinates = input.TextureCoordinates.xy;
    output.TextureCoordinates.y = (output.TextureCoordinates.y - 0.5) / input.TextureCoordinates.z + 0.5;
    return output;
}

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

float FractalNoise(float2 p)
{
    float sum = 0.0;
    float amp = 0.5;

    [unroll]
    for (int i = 0; i < 4; i++)
    {
        sum += ValueNoise(p) * amp;
        p = p * 2.07 + 13.17;
        amp *= 0.5;
    }

    return sum;
}

float Spike(float2 p, float angle, float width, float length, float softness)
{
    float s = sin(angle);
    float c = cos(angle);
    float2 q = float2(p.x * c - p.y * s, p.x * s + p.y * c);
    float axis = abs(q.y) / max(width, 0.0005);
    float reach = saturate(1.0 - abs(q.x) / max(length, 0.0005));
    return exp(-axis * axis * softness) * pow(reach, 2.4);
}

float ProceduralSpikeMask(float2 p, float rotation, float tier)
{
    float spikes = 0.0;

    spikes += Spike(p, rotation, 0.018, 1.10, 3.0);
    spikes += Spike(p, rotation + 1.570796, 0.018, 1.10, 3.0);
    spikes += Spike(p, rotation + 0.785398, 0.011, 0.70, 3.6);
    spikes += Spike(p, rotation - 0.785398, 0.011, 0.70, 3.6);

    spikes += Spike(p, -rotation * 0.55 + 0.20, 0.0055, 1.08, 4.2) * (0.55 + tier * 0.35);
    spikes += Spike(p, -rotation * 0.55 + 1.570796 + 0.20, 0.0055, 1.08, 4.2) * (0.55 + tier * 0.35);

    float ringAngle = atan2(p.y, p.x);
    float rayHash = Hash(float2(floor((ringAngle + 3.14159) * 10.0), 17.0));
    float fineRay = pow(abs(cos(ringAngle * 8.0 + rotation * 1.7)), 18.0) * rayHash;
    float radiusFade = pow(saturate(1.04 - length(p)), 2.1);
    spikes += fineRay * radiusFade * (0.12 + tier * 0.09);

    return saturate(spikes);
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float2 uv = input.TextureCoordinates;
    float2 p = uv * 2.0 - 1.0;
    float r = length(p);
    float tier = saturate(starTier / 3.0);
    float rotation = globalTime * (0.28 + tier * 0.07);

    float proceduralNoise = FractalNoise(uv * 7.0 + float2(globalTime * 0.035, -globalTime * 0.022));
    float textureNoise = tex2D(haloNoise, uv * 1.65 + float2(globalTime * 0.018, -globalTime * 0.012)).r;
    float noise = lerp(proceduralNoise, textureNoise, texturePresence.x);
    float disturbedRadius = r * (0.93 + (noise - 0.5) * (0.11 + tier * 0.05));

    float fallbackRamp = saturate(1.0 - disturbedRadius);
    fallbackRamp = fallbackRamp * fallbackRamp * (3.0 - 2.0 * fallbackRamp);
    float textureRamp = tex2D(radialRamp, float2(saturate(r), 0.5)).r;
    float ramp = lerp(fallbackRamp, textureRamp, texturePresence.z);

    float farHalo = exp(-disturbedRadius * disturbedRadius * 2.05) * (0.34 + tier * 0.16);
    float outerHalo = exp(-disturbedRadius * disturbedRadius * 4.4) * 0.56;
    float innerHalo = exp(-disturbedRadius * disturbedRadius * 14.0) * 0.92;
    float hotCore = exp(-r * r * 72.0) * 1.45;
    float emberRing = exp(-abs(disturbedRadius - 0.36) * 16.0) * (0.09 + tier * 0.11);
    float coronaGrain = pow(saturate(noise), 2.2) * exp(-disturbedRadius * disturbedRadius * 5.8) * (0.16 + tier * 0.12);

    float proceduralSpikes = ProceduralSpikeMask(p, rotation, tier);
    float textureSpikes = tex2D(spikeMask, uv).r;
    float spikes = lerp(proceduralSpikes, saturate(textureSpikes * 1.35 + proceduralSpikes * 0.32), texturePresence.y);
    spikes *= smoothstep(0.02, 0.22, r) * smoothstep(1.10, 0.54, r);

    float flicker = 0.92 + sin(globalTime * 3.7) * 0.045 + (noise - 0.5) * 0.06;
    float body = farHalo + outerHalo + innerHalo + emberRing + coronaGrain + hotCore;
    float alpha = saturate((body * (0.72 + ramp * 0.55) + spikes * (0.68 + tier * 0.28)) * flicker);
    alpha *= smoothstep(1.10, 0.70, r) * starIntensity;

    float3 deepRed = float3(0.42, 0.020, 0.006);
    float3 darkEmber = float3(0.78, 0.060, 0.012);
    float3 ember = float3(1.00, 0.23, 0.040);
    float3 gold = float3(1.00, 0.62, 0.14);
    float3 whiteHot = float3(1.00, 0.94, 0.62);

    float3 color = 0.0;
    color += deepRed * farHalo;
    color += darkEmber * outerHalo;
    color += ember * (innerHalo + emberRing + coronaGrain);
    color += gold * (spikes * 0.82 + hotCore * 0.45);
    color += whiteHot * hotCore;
    color = lerp(color, color + float3(0.24, 0.075, 0.0), tier);

    float chroma = saturate(spikes + hotCore) * 0.18;
    color.r += chroma * 0.18;
    color.b += chroma * 0.035;

    return float4(color * input.Color.rgb, alpha * input.Color.a);
}

technique Technique1
{
    pass AutoloadPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
