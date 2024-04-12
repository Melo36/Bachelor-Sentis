// RotateMirrorShader.shader

Shader "Custom/RotateMirrorShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float2 texcoord : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);

                // Rotate texture coordinates by -90 degrees and mirror horizontally
                o.texcoord = float2(v.texcoord.y, 1.0 - v.texcoord.x);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Sample texture and return color
                return tex2D(_MainTex, i.texcoord);
            }
            ENDCG
        }
    }
}
