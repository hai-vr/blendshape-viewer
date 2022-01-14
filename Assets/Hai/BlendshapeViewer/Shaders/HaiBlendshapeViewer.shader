Shader "Hai/BlendshapeViewer"
{
    Properties
    {
        _MainTex ("Morphed Texture", 2D) = "white" {}
        _NeutralTex ("Neutral Texture", 2D) = "white" {}
        _Hotspots ("Hotspots", Range(0, 1)) = 0
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
                float4 diff : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            sampler2D _NeutralTex;
            float4 _NeutralTex_ST;

            float _Hotspots;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                float width = _MainTex_TexelSize.z;
                float height = _MainTex_TexelSize.w;
                float4 difference = float4(width, height, 0, 0);
                // TODO: There's gotta be a more clever way to do this that uses parallelism
                for (int y = 0; y < height; y++) {
                    for (int x = 0; x < width; x++) {
                        float4 sampleLocation = float4(x / width, y / height, 0, 0);
                        fixed3 neutral = tex2Dlod(_NeutralTex, sampleLocation).xyz;
                        fixed3 morphed = tex2Dlod(_MainTex, sampleLocation).xyz;
                        fixed3 v = neutral - morphed;
                        if (dot(v, v) > 0.01) {
                            difference = float4(
                                min(x, difference.x),
                                min(y, difference.y),
                                max(x, difference.z),
                                max(y, difference.w)
                            );
                        }
                    }
                }
                o.diff = difference;

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float width = _MainTex_TexelSize.z;
                float height = _MainTex_TexelSize.w;

                float4 difference = i.diff;
                difference = difference + float4(-1, -1, 1, 1) * 2; // Margin
                
                fixed4 col = tex2D(_MainTex, i.uv);
                if (i.uv.x < difference.x / width || i.uv.x > difference.z / width
                    || i.uv.y < difference.y / height || i.uv.y > difference.w / height) {
                        col.xyz = col.xyz * 0.2;
                }
                if (_Hotspots > 0.01) {
                    fixed3 neutral = tex2D(_NeutralTex, i.uv).xyz;
                    fixed3 morphed = tex2D(_MainTex, i.uv).xyz;
                    col = lerp(col, length(neutral - morphed) * float4(1, 0, 0, 1), _Hotspots);
                }
                return col;
            }
            ENDCG
        }
    }
}
