Shader "Custom/BlenderGrid"
{
    Properties
    {
        _SubGridColor   ("Sub-grid Color",  Color)  = (0.35,  0.35,  0.35,  1)
        _MainGridColor  ("Main Grid Color", Color)  = (0.50,  0.50,  0.50,  1)
        _GridScale      ("Main Grid Scale", Float)  = 1.0
        _SubDivisions   ("Sub Divisions",   Float)  = 10.0
        _LineWidth      ("Anti-alias Width",Float)  = 1.0
        _FadeStart      ("Fade Start",      Float)  = 8.0
        _FadeEnd        ("Fade End",        Float)  = 22.0
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _SubGridColor;
                float4 _MainGridColor;
                float  _GridScale;
                float  _SubDivisions;
                float  _LineWidth;
                float  _FadeStart;
                float  _FadeEnd;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.worldPos   = TransformObjectToWorld(input.positionOS).xyz;
                return o;
            }

            // Returns 1 on grid lines, 0 in the gaps (anti-aliased).
            float GridLine(float2 xz, float scale, float lineWidth)
            {
                float2 coord = xz / scale;
                float2 width = max(fwidth(coord) * lineWidth, 0.0001);
                float2 d     = abs(frac(coord - 0.5) - 0.5) / width;
                return 1.0 - saturate(min(d.x, d.y));
            }

            // Returns 1 on the axis line at coord == 0.
            float AxisLine(float coord, float pixelWidth)
            {
                return 1.0 - saturate(abs(coord) / max(fwidth(coord) * pixelWidth, 0.0001));
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 xz = input.worldPos.xz;

                float subGrid  = GridLine(xz, _GridScale / _SubDivisions, _LineWidth);
                float mainGrid = GridLine(xz, _GridScale,                  _LineWidth);

                // X-axis (z == 0) → red  |  Z-axis (x == 0) → green
                float xAxis = AxisLine(input.worldPos.z, 2.5);
                float zAxis = AxisLine(input.worldPos.x, 2.5);

                // Start fully transparent
                half4 col = half4(0, 0, 0, 0);

                // Layer sub-grid lines
                col = lerp(col, half4(_SubGridColor.rgb,  1), subGrid  * _SubGridColor.a);
                // Layer main grid lines on top
                col = lerp(col, half4(_MainGridColor.rgb, 1), mainGrid * _MainGridColor.a);
                // Layer axis lines on top of everything
                col = lerp(col, half4(0.53, 0.11, 0.11, 1), xAxis);
                col = lerp(col, half4(0.22, 0.47, 0.22, 1), zAxis);

                float distanceToCamera = distance(GetCameraPositionWS(), input.worldPos);
                float fade = 1.0 - smoothstep(_FadeStart, max(_FadeStart + 0.001, _FadeEnd), distanceToCamera);
                col.a *= fade;

                return col;
            }
            ENDHLSL
        }
    }
}
