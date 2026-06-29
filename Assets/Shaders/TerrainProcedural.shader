Shader "Custom/TerrainProcedural"
{
    Properties
    {
        [Header(Colors)]
        _ColorFlat  ("Color Planicie",   Color) = (0.22, 0.48, 0.12, 1)
        _ColorMid   ("Color Ladera",     Color) = (0.38, 0.30, 0.18, 1)
        _ColorPeak  ("Color Cima",       Color) = (0.72, 0.70, 0.68, 1)

        [Header(Height Thresholds)]
        _HeightMid  ("Altura Ladera",    Float) = 30.0
        _HeightPeak ("Altura Cima",      Float) = 100.0

        [Header(Color Noise)]
        _NoiseScale    ("Escala Ruido",   Float)       = 0.04
        _NoiseStrength ("Fuerza Ruido",   Range(0,1))  = 0.25
        _NoiseTex      ("Noise Texture",  2D)          = "white" {}

        [Header(Surface)]
        _Glossiness ("Smoothness", Range(0,1)) = 0.05
        _Metallic   ("Metallic",   Range(0,1)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _NoiseTex;

        fixed4  _ColorFlat, _ColorMid, _ColorPeak;
        float   _HeightMid, _HeightPeak;
        float   _NoiseScale, _NoiseStrength;
        half    _Glossiness, _Metallic;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
        };

        // Hash noise inline (no necesita textura)
        float hash(float2 p)
        {
            p = frac(p * float2(443.8975, 397.2973));
            p += dot(p, p + 19.19);
            return frac(p.x * p.y);
        }

        float smoothNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(hash(i),              hash(i + float2(1,0)), u.x),
                        lerp(hash(i + float2(0,1)), hash(i + float2(1,1)), u.x), u.y);
        }

        float fbm(float2 p)
        {
            float v = 0.0, a = 0.5;
            for (int i = 0; i < 4; i++)
            {
                v += a * smoothNoise(p);
                p  = p * 2.1 + float2(1.7, 9.2);
                a *= 0.5;
            }
            return v;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float h = IN.worldPos.y;

            // Mezcla de color por altura
            float t0 = saturate(h / max(_HeightMid, 0.001));
            float t1 = saturate((h - _HeightMid) / max(_HeightPeak - _HeightMid, 0.001));

            fixed4 col = lerp(_ColorFlat, _ColorMid, t0);
            col        = lerp(col,        _ColorPeak, t1);

            // Manchas de ruido en el color
            float2 noiseUV = IN.worldPos.xz * _NoiseScale;
            float  noise   = fbm(noiseUV) * 2.0 - 1.0;
            col.rgb       += noise * _NoiseStrength * 0.3;

            o.Albedo     = saturate(col.rgb);
            o.Metallic   = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha      = 1.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
