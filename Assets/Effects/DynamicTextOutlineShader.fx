sampler baseTexture : register(s0);
float2 sampleOffset;

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    // 八方向膨胀只在生成字形遮罩时执行，后续绘制直接复用结果。
    float alpha = tex2D(baseTexture, coords).a;
    alpha = max(alpha, tex2D(baseTexture, coords + float2(sampleOffset.x, 0)).a);
    alpha = max(alpha, tex2D(baseTexture, coords - float2(sampleOffset.x, 0)).a);
    alpha = max(alpha, tex2D(baseTexture, coords + float2(0, sampleOffset.y)).a);
    alpha = max(alpha, tex2D(baseTexture, coords - float2(0, sampleOffset.y)).a);
    float2 diagonal = sampleOffset * 0.70710678;
    alpha = max(alpha, tex2D(baseTexture, coords + diagonal).a);
    alpha = max(alpha, tex2D(baseTexture, coords - diagonal).a);
    alpha = max(alpha, tex2D(baseTexture, coords + float2(diagonal.x, -diagonal.y)).a);
    alpha = max(alpha, tex2D(baseTexture, coords + float2(-diagonal.x, diagonal.y)).a);
    return float4(alpha, alpha, alpha, alpha) * sampleColor;
}

technique Technique1
{
    pass OutlineMask
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
