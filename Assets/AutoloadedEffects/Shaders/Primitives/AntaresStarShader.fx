sampler image : register(s1);

matrix uWorldViewProjection;
float globalTime;
float starTier;
float starIntensity;

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

float Spike(float2 p, float angle, float width, float length)
{
    float s = sin(angle);
    float c = cos(angle);
    float2 q = float2(p.x * c - p.y * s, p.x * s + p.y * c);
    float axis = abs(q.y) / max(width, 0.001);
    float reach = saturate(1.0 - abs(q.x) / max(length, 0.001));
    return exp(-axis * axis * 2.8) * pow(reach, 2.2);
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float2 uv = input.TextureCoordinates;
    float2 p = uv * 2.0 - 1.0;
    float r = length(p);
    float angle = globalTime * 0.38;
    float tier = saturate(starTier / 3.0);
    float flicker = 0.9 + 0.1 * sin(globalTime * 3.1) + (Hash(floor(uv * 18.0) + floor(globalTime * 9.0)) - 0.5) * 0.035;

    float outerHalo = exp(-r * r * 3.2) * 0.58;
    float midHalo = exp(-r * r * 12.0) * 0.85;
    float hotCore = exp(-r * r * 58.0) * 1.25;
    float emberRing = exp(-abs(r - 0.34) * 14.0) * 0.12 * (0.75 + tier);

    float mainSpikes = 0.0;
    mainSpikes += Spike(p, angle, 0.020, 0.95);
    mainSpikes += Spike(p, angle + 1.570796, 0.020, 0.95);
    mainSpikes += Spike(p, angle + 0.785398, 0.014, 0.58);
    mainSpikes += Spike(p, angle - 0.785398, 0.014, 0.58);

    float thinNeedles = 0.0;
    thinNeedles += Spike(p, -angle * 0.55 + 0.16, 0.007, 1.05);
    thinNeedles += Spike(p, -angle * 0.55 + 1.570796 + 0.16, 0.007, 1.05);

    float body = saturate(outerHalo + midHalo + hotCore + emberRing);
    float spikes = saturate(mainSpikes * (0.48 + tier * 0.23) + thinNeedles * (0.25 + tier * 0.16));
    float alpha = saturate((body + spikes) * flicker) * smoothstep(1.06, 0.72, r) * starIntensity;

    float3 deepRed = float3(0.50, 0.035, 0.005);
    float3 ember = float3(1.00, 0.22, 0.035);
    float3 gold = float3(1.00, 0.66, 0.16);
    float3 whiteHot = float3(1.00, 0.94, 0.58);

    float3 color = deepRed * outerHalo + ember * (midHalo + emberRing) + gold * max(spikes, hotCore * 0.55) + whiteHot * hotCore;
    color = lerp(color, color + float3(0.22, 0.06, 0.0), tier);

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
