Shader "Hidden/Mongle/Bloom"
{
 Properties{_MainTex("Source",2D)="black"{} _BloomTex("Bloom",2D)="black"{} _Threshold("Threshold",Float)=1 _Intensity("Intensity",Float)=.65}
 SubShader
 {
  Cull Off ZWrite Off ZTest Always
  CGINCLUDE
  #include "UnityCG.cginc"
  sampler2D _MainTex,_BloomTex;float4 _MainTex_TexelSize;float _Threshold,_Intensity;float2 _Direction;
  half4 threshold(v2f_img i):SV_Target{half3 c=tex2D(_MainTex,i.uv).rgb;float brightness=max(c.r,max(c.g,c.b));float soft=saturate((brightness-_Threshold*.5)/max(.01,_Threshold));return half4(c*soft*max(0,brightness-_Threshold*.5)/max(.001,brightness),1);}
  half4 blur(v2f_img i):SV_Target{float2 d=_MainTex_TexelSize.xy*_Direction;half3 c=tex2D(_MainTex,i.uv).rgb*.227027;c+=tex2D(_MainTex,i.uv+d*1.384615).rgb*.316216;c+=tex2D(_MainTex,i.uv-d*1.384615).rgb*.316216;c+=tex2D(_MainTex,i.uv+d*3.230769).rgb*.070270;c+=tex2D(_MainTex,i.uv-d*3.230769).rgb*.070270;return half4(c,1);}
  half4 combine(v2f_img i):SV_Target{half4 c=tex2D(_MainTex,i.uv);c.rgb+=tex2D(_BloomTex,i.uv).rgb*_Intensity;return c;}
  ENDCG
  Pass{CGPROGRAM
  #pragma vertex vert_img
  #pragma fragment threshold
  ENDCG}
  Pass{CGPROGRAM
  #pragma vertex vert_img
  #pragma fragment blur
  ENDCG}
  Pass{CGPROGRAM
  #pragma vertex vert_img
  #pragma fragment combine
  ENDCG}
 }
}
