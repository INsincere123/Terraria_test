// ═══════════════════════════════════════════════════════════════════════
//   TimeStopFilter.fx
//   时停滤镜：圆内保留原色，圆外渐变为复古泛黄色调（老照片风格）。
// ═══════════════════════════════════════════════════════════════════════

sampler uImage0 : register(s0);

float2 focusPosition;
float2 screenPosition;
float2 screenSize;

float radius01;
float fadeWidth01;
float grayStrength;

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(uImage0, coords);

    // 玩家归一化坐标
    float2 playerUV = (focusPosition - screenPosition) / screenSize;

    // 宽高比修正，确保正圆
    float aspect = screenSize.x / screenSize.y;
    float2 deltaUV = (coords - playerUV) * float2(aspect, 1.0);
    float dist = length(deltaUV);

    // 过渡带：圆内=0，圆外=1
    float t = smoothstep(radius01, radius01 + fadeWidth01, dist);
    float finalLerp = t * grayStrength;

    // 取亮度并往中灰压缩，保证明暗场景都有对比度
    float luma = dot(color.rgb, float3(0.299, 0.587, 0.114));
    float compressedLuma = lerp(luma, 0.5, finalLerp * 0.5);

    // 脱色
    float3 gray = float3(compressedLuma, compressedLuma, compressedLuma);
    float3 result = lerp(color.rgb, gray, finalLerp);

    // 叠加泛黄色调：R 提亮、G 轻微提亮、B 压低
    // 模拟老照片/复古照片的暖黄感
    result += float3(0.12, 0.06, -0.08) * finalLerp;

    color.rgb = result;
    return color;
}

technique Technique1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}
