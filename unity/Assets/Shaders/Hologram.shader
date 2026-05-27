Shader "Dejarik/Hologram"
{
    // Holographic creature look (matches the web game's vibe): the diffuse texture tinted toward the
    // player's holo color, a Fresnel rim glow, scanlines, and additive-ish transparency. _Glow drives the
    // selection pulse; _Alpha drives the death dissolve.
    Properties
    {
        _MainTex ("Diffuse", 2D) = "white" {}
        _HoloColor ("Holo Color", Color) = (0.22, 0.88, 1, 1)
        _RimPower ("Rim Power", Float) = 2.2
        _Glow ("Glow", Float) = 1.0
        _Alpha ("Alpha", Float) = 0.92
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
            };

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _HoloColor; float _RimPower; float _Glow; float _Alpha;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = _WorldSpaceCameraPos - o.worldPos;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float fres = pow(1.0 - saturate(dot(normalize(i.worldNormal), normalize(i.viewDir))), _RimPower);
                float scan = 0.82 + 0.18 * sin(i.worldPos.y * 140.0 + _Time.y * 5.0);
                // Keep the creature's own texture detail, shifted toward the holo color (a mix, like the web).
                fixed3 tint = lerp(fixed3(1.0, 1.0, 1.0), _HoloColor.rgb, 0.55);
                fixed3 baseCol = tex.rgb * tint * 1.5;
                fixed3 col = (baseCol * scan + _HoloColor.rgb * fres * 1.6) * _Glow;
                float alpha = saturate((_Alpha * scan + fres) );
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}
