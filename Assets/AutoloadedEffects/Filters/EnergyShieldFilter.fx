// ══════════════════════════════════════════════════════════════════════════
//   EnergyShieldFilter.fx  —  圆形能量护盾滤镜
//
//   在玩家周围绘制一圈流动的能量气泡。
//   使用 Luminance ManagedScreenFilter 框架，与 TimeStopFilter 同一管线。
//
//   参数说明：
//     focusPosition  — Luminance 自动设置（护盾中心世界坐标）
//     screenPosition — Luminance 自动设置（屏幕左上角世界坐标）
//     screenSize     — Luminance 自动设置（屏幕像素尺寸）
//     time           — 每帧传入 Main.GlobalTimeWrappedHourly，驱动动画
//     shieldStrength — 0~1，当前护盾比例，控制透明度与效果强度
//     hitFlash       — 0~1，护盾受击后的短暂闪光强度
//     shieldRadius   — 护盾圆的半径（像素）
//     shieldColor    — 护盾主体颜色（float3）
//     edgeColor      — 护盾边缘颜色（float3，菲涅尔高光）
// ══════════════════════════════════════════════════════════════════════════

sampler uImage0 : register(s0); // 游戏画面（Luminance 自动绑定）

float2 focusPosition;
float2 screenPosition;
float2 screenSize;

float  time;
float  shieldStrength;
float  hitFlash;
float  shieldRadius;
float3 shieldColor;
float3 edgeColor;

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float4 original = tex2D(uImage0, coords);

    // 玩家在屏幕上的归一化坐标（0~1）
    float2 playerUV = (focusPosition - screenPosition) / screenSize;

    // 宽高比修正，确保正圆（而非椭圆）
    float  aspect = screenSize.x / screenSize.y;
    float2 delta  = (coords - playerUV) * float2(aspect, 1.0);
    float  dist   = length(delta);

    // 护盾半径转归一化（相对屏幕高度）
    float shieldR    = shieldRadius / screenSize.y;
    float normDist   = dist / shieldR; // 0 = 中心，1 = 护盾边缘
    float strength   = saturate(shieldStrength);
    float flash      = saturate(hitFlash);

    // 环形区域：内边 80%，外边 108%（厚度约 28% 半径）
    float innerEdge = lerp(0.80, 0.74, flash);
    float outerEdge = lerp(1.08, 1.12, flash);
    if (normDist > outerEdge)
        return original; // 不在护盾罩范围内，直接返回原始画面

    // 护盾内部：薄薄的同色滤镜，让玩家明确处于能量罩内。
    if (normDist < innerEdge)
    {
        float interiorT = saturate(normDist / innerEdge);
        float interiorPulse = sin(time * 1.7 + normDist * 9.0) * 0.5 + 0.5;
        float interiorAlpha = (0.092 + interiorT * 0.081 + interiorPulse * 0.021 + flash * 0.098) * strength;
        float3 interiorColor = lerp(shieldColor, edgeColor, interiorT * 0.35 + flash * 0.35);
        float interiorMask = smoothstep(0.05, 0.28, interiorT) * smoothstep(innerEdge, innerEdge - 0.08, normDist);
        float ringContact = smoothstep(0.62, 1.0, interiorT);
        float inwardPulse = pow(saturate(sin((1.0 - interiorT) * 24.0 - time * 6.0) * 0.5 + 0.5), 8.0) * ringContact;
        float verticalLane = pow(saturate(sin(delta.x * 90.0 + sin(delta.y * 18.0) * 0.7) * 0.5 + 0.5), 13.0);
        float fallingPulse = pow(saturate(sin(delta.y * 42.0 + time * 7.4 + delta.x * 8.0) * 0.5 + 0.5), 8.0);
        float edgeSpark = pow(saturate(sin(delta.x * 120.0 + time * 9.0) * 0.5 + 0.5), 18.0) * ringContact;
        float filaments = saturate(inwardPulse * 0.90 + verticalLane * fallingPulse * 0.72 + edgeSpark * 0.52) * interiorMask;
        float3 flowColor = lerp(edgeColor, float3(1.0, 1.0, 1.0), flash * 0.24 + filaments * 0.18);
        float3 interiorResult = original.rgb
            + interiorColor * saturate(interiorAlpha)
            + flowColor * saturate((filaments * 0.253 + flash * 0.040) * strength);
        return float4(interiorResult, original.a);
    }

    // 在环内的归一化位置：0 = 内边，1 = 外边
    float ringT = (normDist - innerEdge) / (outerEdge - innerEdge);

    // ── 能量流动层 ───────────────────────────────────────────────────────
    // 用 delta（方向向量）生成随位置变化的扰动，模拟流动等离子体
    float2 flowUV = delta * (14.0 / shieldR);
    float plasma1 = sin(flowUV.x * 1.2 + time * 2.8) * sin(flowUV.y * 1.5 + time * 2.1);
    float plasma2 = sin((flowUV.x + flowUV.y) * 0.9  + time * 3.4);
    float plasma   = saturate(plasma1 * 0.4 + plasma2 * 0.3 + 0.4); // 0~1

    // 径向涟漪（从中心向外传播的波）
    float ripple   = sin(normDist * 22.0 - time * 5.0) * 0.5 + 0.5;

    // ── 菲涅尔（边缘发光）────────────────────────────────────────────────
    // ringT 在 0（内边）和 1（外边）处最大，0.5 处最小 → 边缘亮，中间暗
    float fresnel = pow(abs(ringT - 0.5) * 2.0, 2.5);

    // ── 透明度合成 ───────────────────────────────────────────────────────
    float alpha = (plasma * 0.35 + ripple * 0.20 + fresnel * 0.65 + flash * 0.55) * strength;

    // 内外两侧平滑淡出，消除硬边
    alpha *= smoothstep(outerEdge, outerEdge - 0.06, normDist); // 外侧淡出
    alpha *= smoothstep(innerEdge, innerEdge + 0.06, normDist); // 内侧淡出
    alpha  = saturate(alpha * lerp(1.265, 1.553, flash)); // 受击时短暂提亮，平时保持更清晰的护盾亮度

    // ── 颜色合成 ─────────────────────────────────────────────────────────
    // 内侧 shieldColor，边缘 edgeColor（由 fresnel 插值）
    float3 finalColor = lerp(shieldColor, edgeColor, saturate(fresnel));
    finalColor = lerp(finalColor, float3(1.0, 1.0, 1.0), flash * 0.28);

    // Additive 叠加：护盾颜色直接叠加在原始画面上，无需替换原色
    float3 result = original.rgb + finalColor * alpha;

    return float4(result, original.a);
}

technique Technique1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
