Shader "Custom/SinglePassGaussianBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurAmount ("Blur Amount", Range(0, 5)) = 1.0
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
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float _BlurAmount;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                const int numSamples = 9;
                float2 texOffset = _BlurAmount * 0.01 / _ScreenParams.xy;
                half4 sum = tex2D(_MainTex, i.texcoord) * 0.2270270270; // Center weight (Gaussian distribution)

                for (int j = 1; j < numSamples; j++)
                {
                    half weight = 0.1945945946; // Side weights (Gaussian distribution)
                    sum += tex2D(_MainTex, i.texcoord + texOffset * float2(j, 0)) * weight;
                    sum += tex2D(_MainTex, i.texcoord - texOffset * float2(j, 0)) * weight;
                    sum += tex2D(_MainTex, i.texcoord + texOffset * float2(0, j)) * weight;
                    sum += tex2D(_MainTex, i.texcoord - texOffset * float2(0, j)) * weight;
                }

                return sum;
            }
            ENDCG
        }
    }
}
