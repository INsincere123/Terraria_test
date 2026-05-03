// ═══════════════════════════════════════════════════════════════════════
//   TimeStopFilter.fx
//   时停滤镜：圆内保留原色，圆外往中灰压缩并轻微偏蓝。
//   不乘 sampleColor 避免 Luminance 管线带入的暗化系数。
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

    // 取亮度
    float luma = dot(color.rgb, float3(0.299, 0.587, 0.114));

    // 往中灰 0.5 压缩：亮的压暗，暗的提亮，任何背景都有对比度
    float compressedLuma = lerp(luma, 0.5, finalLerp * 0.5);

    // 压制饱和度：原色 → 压缩后的灰
    float3 result = lerp(color.rgb, float3(compressedLuma, compressedLuma, compressedLuma), finalLerp);

    // 叠加轻微蓝偏色，增加"时间凝固"感
    result += float3(-0.02, -0.01, 0.06) * finalLerp;

    color.rgb = result;

    // 不乘 sampleColor，避免 Luminance 管线暗化
    return color;
}

technique Technique1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}
