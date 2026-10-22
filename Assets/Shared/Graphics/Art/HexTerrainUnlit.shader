Shader "TD/Hex Terrain Unlit"
{
    Properties { [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        struct Attributes { float3 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
        struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
        Varyings vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.color = input.color;
            output.uv = input.uv;
            return output;
        }
        half4 frag(Varyings input) : SV_Target
        {
            // Exact regular hex clipping avoids raster alpha seams while keeping a genuine Tilemap.
            float2 p = abs(input.uv - 0.5) * float2(1.0, 444.0 / 512.0);
            clip(0.4330127019 - p.y + 0.0001);
            clip(0.5 - p.x - p.y * 0.5773502692 + 0.0001);
            return input.color;
        }
        ENDHLSL
        Pass
        {
            Name "Hex 2D"
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
        Pass
        {
            Name "Hex Forward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
}
