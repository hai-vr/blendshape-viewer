Shader "Hai/BlendshapeViewerRectOnly"
{
    Properties
    {
        _MainTex ("Morphed Texture", 2D) = "white" {}
        _NeutralTex ("Neutral Texture", 2D) = "white" {}
        _Hotspots ("Hotspots", Range(0, 1)) = 0
        _Rect ("Rect", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            sampler2D _NeutralTex;
            float4 _NeutralTex_ST;

            float _Hotspots;
            float4 _Rect;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float width = _MainTex_TexelSize.z;
                float height = _MainTex_TexelSize.w;

                float4 difference = _Rect;
                difference = difference + float4(-1, -1, 1, 1) * 2; // Margin

                fixed4 col = tex2D(_MainTex, i.uv);
                if (_Rect.x == 0 && _Rect.y == 0 && _Rect.z == 0 && _Rect.w == 0) {
                    col.xyz = col.xyz * 0.05;
                } else if (i.uv.x < difference.x / width || i.uv.x > difference.z / width
                    || i.uv.y < difference.y / height || i.uv.y > difference.w / height) {
                        col.xyz = col.xyz * 0.2;
                }
                if (_Hotspots > 0.01) {
                    fixed3 neutral = tex2D(_NeutralTex, i.uv).xyz;
                    fixed3 morphed = tex2D(_MainTex, i.uv).xyz;
                    float isGreater = ((neutral.x + neutral.y + neutral.z) - (morphed.x + morphed.y + morphed.z)) / 3.0;
                    isGreater = isGreater * 5;
                    if (isGreater > 1) isGreater = 1;
                    else if (isGreater < -1) isGreater = -1;
                    isGreater = isGreater * 0.5 + 0.5;
                    
                    float4 highlight = lerp(float4(1, 0.65, 0, 1), float4(1, 0, 0, 1), isGreater);
                    col = lerp(col, length(neutral - morphed) * highlight, _Hotspots);
                }
                return col;
            }
            ENDCG
        }
    }
}
