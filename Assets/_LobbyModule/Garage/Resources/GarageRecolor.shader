// Recolours the baked AC-26 livery (and the rim / tyre-sidewall atlases) on the GPU for the Garage.
// The source art uses a fixed palette (graphite body, a violet -> magenta accent ramp, white lettering), so each
// texel is classified in gamma space and re-shaded: body -> _Primary, accent ramp -> _AccentA.._AccentB (keeping
// the texel's own shading and saturation relative to the ramp), lettering -> _Lettering. Used with Graphics.Blit.
Shader "Hidden/Accertone/GarageRecolor"
{
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Primary;      // gamma-space target for the body colour
            float4 _AccentA;      // gamma-space target for the violet end of the accent ramp
            float4 _AccentB;      // gamma-space target for the magenta end of the accent ramp
            float4 _Lettering;    // gamma-space target for white lettering
            float _KeepBody;      // 1 = leave the body / base untouched (tyre sidewalls, emission maps)
            float _KeepLettering; // 1 = leave white / grey texels untouched
            float _BodyValue;     // value (max channel) of the source body colour
            float _HueA;          // source hue (0..1) of the violet end
            float _HueB;          // source hue (0..1) of the magenta end
            float _SatRef;        // saturation of the source accent ramp

            float3 RgbToHsv(float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-5;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            float3 HsvToRgb(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float4 src = tex2D(_MainTex, i.uv);
                float3 g = LinearToGammaSpace(max(src.rgb, 0.0));
                float3 hsv = RgbToHsv(g);
                float h = hsv.x, s = hsv.y, v = hsv.z;

                // Accent: saturated texels of the violet..magenta family (hue wraps through red at 0/1).
                float hueDist = min(abs(h - 0.79), 1.0 - abs(h - 0.79));
                float inFamily = 1.0 - smoothstep(0.16, 0.24, hueDist);
                float wa = smoothstep(0.30, 0.50, s) * inFamily * smoothstep(0.03, 0.10, v);
                // Lettering: bright, unsaturated texels.
                float ww = (1.0 - smoothstep(0.10, 0.26, s)) * smoothstep(0.45, 0.80, v) * (1.0 - wa);
                float wb = saturate(1.0 - wa - ww);

                // Accent ramp position and the texel's shading / saturation relative to the source ramp.
                float t = saturate((h - _HueA) / max(1e-4, _HueB - _HueA));
                float3 target = lerp(_AccentA.rgb, _AccentB.rgb, t);
                float3 thsv = RgbToHsv(target);
                float vRef = lerp(1.0, 0.91, t);
                float3 accent = HsvToRgb(float3(thsv.x, saturate(thsv.y * s / max(1e-3, _SatRef)), saturate(thsv.z * v / vRef)));

                float3 body = _KeepBody > 0.5 ? g : _Primary.rgb * saturate(v / max(1e-3, _BodyValue));
                float3 letters = _KeepLettering > 0.5 ? g : _Lettering.rgb * saturate(v / 0.97);

                float3 outG = body * wb + accent * wa + letters * ww;
                return float4(GammaToLinearSpace(outG), src.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
