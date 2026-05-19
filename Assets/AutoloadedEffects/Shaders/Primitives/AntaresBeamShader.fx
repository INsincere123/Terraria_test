sampler beamNoise : register(s2);

matrix uWorldViewProjection;
float globalTime;
float beamIntensity;
float noisePresence;

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
        p = p * 2.03 + 11.7;
        amp *= 0.5;
    }

    return sum;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float2 uv = input.TextureCoordinates;
    float along = saturate(uv.x);
    float across = abs(uv.y - 0.5) * 2.0;
    float head = smoothstep(0.55, 1.0, along);
    float tail = smoothstep(0.0, 0.18, along);
    float bodyFade = tail * smoothstep(1.0, 0.68, across);

    float2 flowUv = float2(along * 3.6 - globalTime * 1.35, uv.y * 7.0 + globalTime * 0.22);
    float proceduralNoise = FractalNoise(flowUv);
    float textureNoise = tex2D(beamNoise, flowUv * 0.18).r;
    float noise = lerp(proceduralNoise, textureNoise, saturate(noisePresence));

    float edgeTear = smoothstep(0.42, 1.0, across + (noise - 0.5) * (0.42 + head * 0.18));
    float hotCore = exp(-across * across * (18.0 + head * 13.0));
    float plasmaBody = exp(-across * across * 4.2) * (0.50 + noise * 0.38);
    float emberRibs = pow(saturate(sin((along * 19.0 - globalTime * 7.0) + noise * 5.0) * 0.5 + 0.5), 5.0);
    emberRibs *= smoothstep(0.85, 0.12, across) * 0.22;

    float alpha = (plasmaBody + hotCore * 0.86 + emberRibs) * bodyFade;
    alpha *= (1.0 - edgeTear * 0.55) * beamIntensity;

    float3 darkRed = float3(0.55, 0.025, 0.005);
    float3 ember = float3(1.00, 0.18, 0.035);
    float3 gold = float3(1.00, 0.58, 0.12);
    float3 whiteHot = float3(1.00, 0.95, 0.68);

    float3 color = 0.0;
    color += darkRed * plasmaBody * (1.0 - head * 0.32);
    color += ember * (plasmaBody * 0.72 + emberRibs);
    color += gold * (hotCore * 0.68 + head * 0.18);
    color += whiteHot * hotCore * (0.58 + head * 0.5);
    color *= input.Color.rgb;

    return float4(color, saturate(alpha) * input.Color.a);
}

technique Technique1
{
    pass AutoloadPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
