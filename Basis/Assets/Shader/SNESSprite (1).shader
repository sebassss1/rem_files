Shader "Custom/URP/SNESSprite"
{
    Properties
    {
        _BaseMap ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)

        // --- SNES Style Controls ---
        [Header(Cel Shading)]
        _ShadowThreshold ("Shadow Threshold", Range(0,1)) = 0.4
        _ShadowColor ("Shadow Color", Color) = (0.15, 0.1, 0.25, 1)
        _MidtoneThreshold ("Midtone Threshold", Range(0,1)) = 0.75
        _MidtoneColor ("Midtone Tint", Color) = (1,1,1,1)

        [Header(Outline)]
        [Toggle] _UseOutline ("Enable Outline", Float) = 1
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineThickness ("Outline Thickness (px)", Range(0.5, 4)) = 1.5

        [Header(Pixelation)]
        [Toggle] _UsePixelation ("Enable Pixelation", Float) = 1
        _PixelSize ("Pixel Size", Range(1, 16)) = 3

        [Header(Color Palette)]
        [Toggle] _UsePaletteLimit ("Limit Color Depth", Float) = 1
        _PaletteSteps ("Palette Steps per Channel", Range(2, 32)) = 8

        [Header(Specular)]
        [Toggle] _UseSpecular ("Enable Specular", Float) = 0
        _SpecularColor ("Specular Color", Color) = (1,1,1,1)
        _SpecularThreshold ("Specular Threshold", Range(0,1)) = 0.95

        // URP required
        [HideInInspector] _Cull ("__cull", Float) = 2.0
        [HideInInspector] _AlphaClip ("__clip", Float) = 0.0
        [HideInInspector] _BlendOp ("__blendop", Float) = 0.0
        [HideInInspector] _SrcBlend ("__src", Float) = 1.0
        [HideInInspector] _DstBlend ("__dst", Float) = 0.0
        [HideInInspector] _ZWrite ("__zw", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        // ────────────────────────────────────────────────
        // Pass 1 — Outline (expanded back-face hull)
        // ────────────────────────────────────────────────
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineThickness;
                float  _UseOutline;
                // Declare all other properties so the buffer matches across passes
                float4 _BaseColor;
                float4 _ShadowColor;
                float4 _MidtoneColor;
                float4 _SpecularColor;
                float  _ShadowThreshold;
                float  _MidtoneThreshold;
                float  _SpecularThreshold;
                float  _UseSpecular;
                float  _UsePixelation;
                float  _PixelSize;
                float  _UsePaletteLimit;
                float  _PaletteSteps;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings OutlineVert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                if (_UseOutline < 0.5)
                {
                    OUT.positionCS = float4(0,0,0,0);
                    return OUT;
                }

                // Expand along normal in clip space for a screen-consistent outline
                float4 posCS = TransformObjectToHClip(IN.positionOS.xyz);
                float3 normalCS = mul((float3x3)UNITY_MATRIX_VP,
                                     mul((float3x3)UNITY_MATRIX_M, IN.normalOS));
                float2 offset = normalize(normalCS.xy);

                // _OutlineThickness is in pixels; convert to NDC
                float2 screenSize = float2(_ScreenParams.x, _ScreenParams.y);
                posCS.xy += (offset / screenSize) * _OutlineThickness * posCS.w * 2.0;

                OUT.positionCS = posCS;
                return OUT;
            }

            half4 OutlineFrag(Varyings IN) : SV_Target
            {
                return half4(_OutlineColor.rgb, 1.0);
            }
            ENDHLSL
        }

        // ────────────────────────────────────────────────
        // Pass 2 — Main SNES lighting pass (ForwardLit)
        // ────────────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _ShadowColor;
                float4 _MidtoneColor;
                float4 _SpecularColor;
                float4 _OutlineColor;
                float  _ShadowThreshold;
                float  _MidtoneThreshold;
                float  _SpecularThreshold;
                float  _OutlineThickness;
                float  _UseOutline;
                float  _UseSpecular;
                float  _UsePixelation;
                float  _PixelSize;
                float  _UsePaletteLimit;
                float  _PaletteSteps;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── Helpers ──────────────────────────────────

            // Snap UV to a pixel grid to fake low-resolution sprites
            float2 Pixelate(float2 uv, float2 texSize, float pixelSize)
            {
                float2 gridSize = texSize / pixelSize;
                return floor(uv * gridSize) / gridSize;
            }

            // Reduce color depth to N steps per channel (SNES had 5-bit per channel = 32 levels)
            float3 QuantizeColor(float3 col, float steps)
            {
                return floor(col * steps + 0.5) / steps;
            }

            // Hard 3-band cel shading
            float3 CelShade(float3 albedo, float NdotL,
                            float3 shadowCol, float shadowThresh,
                            float3 midTint,   float midThresh)
            {
                if (NdotL < shadowThresh)
                    return albedo * shadowCol;
                else if (NdotL < midThresh)
                    return albedo * midTint * 0.75;
                else
                    return albedo * midTint;
            }

            // ── Vertex ───────────────────────────────────
            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   normInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS  = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.normalWS    = normInputs.normalWS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.shadowCoord = GetShadowCoord(posInputs);

                return OUT;
            }

            // ── Fragment ─────────────────────────────────
            half4 Frag(Varyings IN) : SV_Target
            {
                // --- Pixelation ---
                float2 uv = IN.uv;
                if (_UsePixelation > 0.5)
                {
                    float2 texSize;
                    _BaseMap.GetDimensions(texSize.x, texSize.y);
                    uv = Pixelate(uv, texSize, _PixelSize);
                }

                float4 texSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                float3 albedo = texSample.rgb * _BaseColor.rgb;

                // --- Lighting ---
                Light mainLight = GetMainLight(IN.shadowCoord);
                float3 normalWS = normalize(IN.normalWS);
                float3 lightDir = normalize(mainLight.direction);
                float NdotL = dot(normalWS, lightDir);

                // Remap NdotL 0..1 (include shadow attenuation for extra crunchiness)
                float lightValue = saturate(NdotL) * mainLight.shadowAttenuation;

                // Cel shade
                float3 color = CelShade(
                    albedo, lightValue,
                    _ShadowColor.rgb, _ShadowThreshold,
                    _MidtoneColor.rgb, _MidtoneThreshold
                );

                // Optional specular dot
                if (_UseSpecular > 0.5)
                {
                    float3 viewDir  = normalize(GetWorldSpaceViewDir(IN.positionWS));
                    float3 halfDir  = normalize(lightDir + viewDir);
                    float  NdotH    = saturate(dot(normalWS, halfDir));
                    float  specMask = step(_SpecularThreshold, NdotH);
                    color += _SpecularColor.rgb * specMask;
                }

                // --- Color quantization ---
                if (_UsePaletteLimit > 0.5)
                    color = QuantizeColor(color, _PaletteSteps);

                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // ────────────────────────────────────────────────
        // Shadow caster pass (so the object still casts shadows)
        // ────────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // ────────────────────────────────────────────────
        // Depth-only pass (required by URP for depth prepass / SSAO)
        // ────────────────────────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
