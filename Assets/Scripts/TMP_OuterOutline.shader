Shader "TextMeshPro/OuterOutline"
{
    Properties
    {
        [HideInInspector] _FaceTex("Font Atlas", 2D) = "white" {}
        _FaceColor("Face Color", Color) = (1,1,1,1)
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth("Outline Width", Range(0,1)) = 0.1
        _Gradient("Gradient (0 = Off, 1 = Vertical, 2 = Horizontal)", Range(0,2)) = 0
        _GradientColor("Gradient Color", Color) = (1,0,0,1)
    }

        SubShader
        {
            Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
            Lighting Off ZWrite Off Cull Off Fog { Mode Off }
            Blend SrcAlpha OneMinusSrcAlpha
            Pass
            {
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #include "UnityCG.cginc"

                sampler2D _FaceTex;
                fixed4 _FaceColor;
                fixed4 _OutlineColor;
                float _OutlineWidth;
                float _Gradient;
                fixed4 _GradientColor;

                struct appdata_t
                {
                    float4 vertex : POSITION;
                    float2 texcoord : TEXCOORD0;
                };

                struct v2f
                {
                    float4 vertex : SV_POSITION;
                    float2 uv : TEXCOORD0;
                    float2 screenUV : TEXCOORD1;
                };

                v2f vert(appdata_t IN)
                {
                    v2f OUT;
                    OUT.vertex = UnityObjectToClipPos(IN.vertex);
                    OUT.uv = IN.texcoord;
                    OUT.screenUV = IN.vertex.xy;
                    return OUT;
                }

                fixed4 frag(v2f IN) : SV_Target
                {
                    float sdf = tex2D(_FaceTex, IN.uv).a;

                // Outer only outline: Keep face sharp, expand outline beyond
                float outlineAlpha = smoothstep(0.5, 0.5 - _OutlineWidth, sdf);
                float faceAlpha = smoothstep(0.5 - 0.01, 0.5, sdf);

                fixed4 outlineColor = _OutlineColor;

                // Optional gradient
                if (_Gradient == 1) {
                    outlineColor.rgb = lerp(_OutlineColor.rgb, _GradientColor.rgb, saturate(IN.screenUV.y / _ScreenParams.y));
                }
                else if (_Gradient == 2) {
                    outlineColor.rgb = lerp(_OutlineColor.rgb, _GradientColor.rgb, saturate(IN.screenUV.x / _ScreenParams.x));
                }

                fixed4 col = outlineColor * outlineAlpha + _FaceColor * faceAlpha;
                col.a = max(outlineAlpha, faceAlpha) * _FaceColor.a;
                return col;
            }
            ENDCG
        }
        }
}
