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

    // 环形区域：内边 80%，外边 108%（厚度约 28% 半径）
    float innerEdge = 0.80;
    float outerEdge = 1.08;
    if (normDist < innerEdge || normDist > outerEdge)
        return original; // 不在护盾环上，直接返回原始画面

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
    float alpha = (plasma * 0.35 + ripple * 0.20 + fresnel * 0.65) * shieldStrength;

    // 内外两侧平滑淡出，消除硬边
    alpha *= smoothstep(outerEdge, outerEdge - 0.06, normDist); // 外侧淡出
    alpha *= smoothstep(innerEdge, innerEdge + 0.06, normDist); // 内侧淡出
    alpha  = saturate(alpha * 0.85); // 整体透明度上限，避免过曝

    // ── 颜色合成 ─────────────────────────────────────────────────────────
    // 内侧 shieldColor，边缘 edgeColor（由 fresnel 插值）
    float3 finalColor = lerp(shieldColor, edgeColor, saturate(fresnel));

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
