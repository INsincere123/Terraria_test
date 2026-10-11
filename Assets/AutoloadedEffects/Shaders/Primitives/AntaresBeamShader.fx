sampler headTexture : register(s0);
sampler beamNoise : register(s2);
sampler bodyTexture : register(s3);
sampler flowTexture : register(s4);
sampler coreTexture : register(s5);

matrix uWorldViewProjection;
float globalTime;
float beamIntensity;
float burstPalette;
float noisePresence;
float3 materialPresence;
float headOpacity;

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
        p = p * 2.03 + 11.7;
        amp *= 0.5;
    }

    return sum;
}

float MaterialMask(float4 sample)
{
    return max(sample.r, max(sample.g, sample.b)) * sample.a;
}

// 弹头与拖尾使用同一热度梯度，避免暖色贴图与程序亮线各自着色。
float3 PlasmaColor(float heat)
{
    float3 outer = lerp(float3(0.58, 0.035, 0.008), float3(0.035, 0.10, 0.46), burstPalette);
    float3 middle = lerp(float3(1.0, 0.43, 0.085), float3(0.25, 0.65, 1.0), burstPalette);
    float3 hot = lerp(float3(1.0, 0.90, 0.72), float3(0.88, 0.97, 1.0), burstPalette);
    float3 color = lerp(outer, middle, smoothstep(0.0, 0.62, heat));
    return lerp(color, hot, smoothstep(0.62, 1.0, heat));
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float2 uv = input.TextureCoordinates;
    float along = saturate(uv.x);
    float across = abs(uv.y - 0.5) * 2.0;
    float tail = smoothstep(0.0, 0.10, along);
    float edge = 1.0 - smoothstep(0.72, 1.0, across);
    float2 flowUv = float2(frac(along * 1.35 - globalTime * 0.22), uv.y);
    float noise = lerp(FractalNoise(float2(along * 4.0 - globalTime * 0.6, uv.y * 5.0)),
        tex2D(beamNoise, flowUv * 2.0).r, noisePresence);

    // 纹理只提供形状和细节，原有蓝色不进入普通红橙光束。
    // 取材质有能量的内部区段，不把图片透明边缘拉到弹头接口。
    float2 bodyUv = float2(lerp(0.18, 0.68, along), uv.y);
    float body = lerp(exp(-across * across * 4.5),
        MaterialMask(tex2D(bodyTexture, bodyUv)), materialPresence.x) * edge;
    float flow = lerp(exp(-across * across * 13.0) * (0.4 + noise * 0.6),
        MaterialMask(tex2D(flowTexture, flowUv)), materialPresence.y);
    flow *= smoothstep(0.10, 0.20, along) * edge;

    // 亮芯限于靠近弹头的65%路径、外焰18%宽度；重采样中央材质区域保留细节。
    float coreProgress = saturate((along - 0.35) / 0.65);
    float2 coreUv = float2(lerp(0.20, 0.70, coreProgress), (uv.y - 0.5) / 0.18 * 0.25 + 0.5);
    float core = lerp(1.0, MaterialMask(tex2D(coreTexture, saturate(coreUv))), materialPresence.z);
    core *= (1.0 - smoothstep(0.08, 0.18, across)) * smoothstep(0.35, 0.43, along);

    // 纹理细节调制亮芯，末端保持连续；接近弹头才逐渐提升外焰热度。
    core *= 0.86 + noise * 0.14;
    float heat = max(flow * 0.62, core);
    heat = max(heat, body * smoothstep(0.70, 1.0, along) * 0.38);
    float3 color = PlasmaColor(saturate(heat));
    float alpha = max(body * 0.54, max(flow * 0.48, core * 0.88));
    alpha *= tail * beamIntensity;
    return float4(color * input.Color.rgb, saturate(alpha) * input.Color.a);
}

float4 HeadPixelShader(float4 color : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float4 head = tex2D(headTexture, uv);
    float intensity = max(head.r, max(head.g, head.b));
    float whiteness = min(head.r, min(head.g, head.b)) / max(intensity, 0.001);
    float activity = lerp(1.0, 0.92 + tex2D(beamNoise,
        uv * 1.5 + float2(-globalTime * 0.04, globalTime * 0.02)).r * 0.08, noisePresence);
    float heat = saturate(sqrt(intensity) * 0.62 + smoothstep(0.48, 0.95, whiteness) * 0.38);
    float3 plasma = PlasmaColor(heat);
    // 限制柔光的重复叠亮，保留纹理内的丝状明暗和小亮核。
    float emission = pow(max(intensity, 0.00001), 0.85) * activity;
    float edge = smoothstep(0.025, 0.20, intensity);
    return float4(plasma * emission, head.a * edge * headOpacity * 0.85) * color;
}

technique Technique1
{
    pass AutoloadPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }

    pass HeadPass
    {
        PixelShader = compile ps_3_0 HeadPixelShader();
    }
}
