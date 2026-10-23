Shader "Mongle/Energy"
{
 Properties { _MainTex("Texture",2D)="white"{} [HDR]_Tint("Tint",Color)=(1,1,1,1) _Flow("UV flow",Vector)=(0,0,0,0) _Distortion("Distortion",Range(0,0.06))=0 _Softness("Soft particles",Float)=1 }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
  Blend SrcAlpha One ZWrite Off Cull Off
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_particles
   #include "UnityCG.cginc"
   sampler2D _MainTex;float4 _MainTex_ST,_Tint,_Flow;float _Distortion,_Softness;
   UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
   struct appdata {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
   struct v2f {float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float4 screen:TEXCOORD1;};
   v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color*_Tint;o.uv=TRANSFORM_TEX(v.uv,_MainTex);o.screen=ComputeScreenPos(o.vertex);COMPUTE_EYEDEPTH(o.screen.z);return o;}
   half4 frag(v2f i):SV_Target
   {
    float2 uv=i.uv+_Time.y*_Flow.xy;uv+=sin(float2(uv.y*19+_Time.y*5,uv.x*17-_Time.y*3))*_Distortion;
    half4 c=tex2D(_MainTex,uv)*i.color;
    #ifdef SOFTPARTICLES_ON
    float scene=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)));c.a*=saturate((scene-i.screen.z)*_Softness);
    #endif
    return c;
   }
   ENDCG
  }
 }
}
