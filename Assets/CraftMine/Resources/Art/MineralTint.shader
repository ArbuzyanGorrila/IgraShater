Shader "CraftMine/MineralTint"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite",2D) = "white" {}
        _Brightness ("Brightness",Range(1,2)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Output { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            sampler2D _MainTex;
            fixed _Brightness;
            Output vert(Input v) { Output o; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color; o.uv=v.uv; return o; }
            fixed4 frag(Output v):SV_Target
            {
                fixed4 source=tex2D(_MainTex,v.uv);
                fixed value=saturate(dot(source.rgb,fixed3(.299,.587,.114))*_Brightness);
                return fixed4(value*v.color.rgb,source.a*v.color.a);
            }
            ENDCG
        }
    }
}
