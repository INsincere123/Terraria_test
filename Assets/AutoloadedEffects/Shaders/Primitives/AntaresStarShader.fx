sampler haloNoise : register(s2);
sampler radialRamp : register(s4);

matrix uWorldViewProjection;
float globalTime;
float starTier;
float starIntensity;
float shotEnvelope;
float shotContraction;
float noisePresence;
float rampPresence;

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
    for (int i = 0; i < 2; i++)
    {
        sum += ValueNoise(p) * amp;
        p = p * 2.07 + 13.17;
        amp *= 0.5;
    }

    return sum;
}

// 非对称日冕弧：缓慢生长/消退，不使用持续旋转的规则十字星芒。
float CoronaArc(float2 p, float angle, float phase, float radius, float strength)
{
    float2 direction = float2(cos(angle), sin(angle));
    float along = dot(p, direction);
    float across = dot(p, float2(-direction.y, direction.x));
    float growth = 0.5 - 0.5 * cos(phase);
    float extent = lerp(0.30, radius, growth);
    float curve = across - sin(saturate(along / max(extent, 0.01)) * 3.14159) * 0.13;
    float filament = exp(-curve * curve * 620.0);
    float envelope = smoothstep(0.15, 0.28, along) * (1.0 - smoothstep(extent * 0.65, extent, along));
    float diffuse = exp(-curve * curve * 80.0) * 0.14;
    return (filament + diffuse) * envelope * growth * strength;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float2 uv = input.TextureCoordinates;
    float2 p = uv * 2.0 - 1.0;
    float r = length(p);
    float tier = saturate(starTier / 3.0);
    float secondLayer = saturate(starTier - 1.0);
    float thirdLayer = saturate(starTier - 2.0);
    float contraction = 1.0 - shotContraction * 0.06;
    float bodyRadius = 0.23 * contraction;
    float activity = 1.0 + thirdLayer * 0.35;
    float noise = FractalNoise(p * 9.0 + float2(globalTime * 0.10, -globalTime * 0.06) * activity);
    float textureNoise = tex2D(haloNoise, p * 0.8 + float2(globalTime * 0.025 * activity, -globalTime * 0.018 * activity)).r;
    noise = lerp(noise, textureNoise, noisePresence * 0.55);
    float surfaceMask = 1.0 - smoothstep(bodyRadius * 0.78, bodyRadius, r);
    float limb = sqrt(saturate(1.0 - r * r / (bodyRadius * bodyRadius)));
    float granulation = smoothstep(0.23, 0.76, noise);
    float surface = surfaceMask * (0.40 + limb * 0.30 + granulation * (0.24 + tier * 0.18));
    float halo = exp(-r * r * 24.0) * (0.07 + tier * 0.012);

    float corona = CoronaArc(p, 0.45, globalTime * 0.68, 0.78, tier * 0.48 + shotEnvelope * 0.36);
    corona += CoronaArc(p, 3.50, globalTime * 0.47 + 2.0, 0.65, tier * 0.32 + shotEnvelope * 0.22);
    corona += CoronaArc(p, 5.35, globalTime * 0.56 + 4.1, 0.88, secondLayer * 0.28);
    corona += CoronaArc(p, 2.12, globalTime * 0.38 + 1.0, 0.74, thirdLayer * 0.22);
    // 射击释放是局部喷流的短包络，不叠加成整圈白光。
    corona += CoronaArc(p, 0.95, 1.8 + shotEnvelope * 1.2, 0.84, shotEnvelope * 0.32);

    float ramp = lerp(pow(saturate(1.0 - r), 2.0), tex2D(radialRamp, float2(saturate(r), 0.5)).r, rampPresence);
    float falloff = (1.0 - smoothstep(0.90, 0.99, r)) * lerp(0.35, 1.0, ramp);
    float3 surfaceColor = lerp(float3(0.52, 0.025, 0.006), float3(0.95, 0.26, 0.055), granulation * 0.65 + limb * 0.20);
    float surfaceAlpha = surfaceMask * 0.92;
    float coronaAlpha = saturate(corona * 0.65);
    float haloAlpha = halo * 0.60;
    float3 color = surfaceColor * surface * 0.92;
    color += float3(0.9, 0.25, 0.045) * coronaAlpha * (1.0 - surfaceAlpha);
    color += float3(0.8, 0.10, 0.02) * haloAlpha * (1.0 - surfaceAlpha) * (1.0 - coronaAlpha);
    float alpha = 1.0 - (1.0 - surfaceAlpha) * (1.0 - coronaAlpha) * (1.0 - haloAlpha);
    // 各层只预乘一次；不把已衰减的日冕再乘一遍总alpha。
    return float4(saturate(color) * input.Color.rgb * falloff * starIntensity,
        alpha * input.Color.a * falloff * starIntensity);
}

technique Technique1
{
    pass AutoloadPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
