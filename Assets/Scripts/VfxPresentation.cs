using UnityEngine;
namespace Mongle
{
    [DefaultExecutionOrder(500)]
    [RequireComponent(typeof(Camera))]
    public sealed class VfxPresentation : MonoBehaviour
    {
        [Range(0,2)]public float bloomIntensity=.65f;
        [Range(0,1)]public float shakeScale=1;
        public bool bloomEnabled=true;
        Material bloom;
        Vector3 origin,offset,velocity;
        float trauma;
        void Awake(){origin=transform.localPosition;var shader=Resources.Load<Shader>("VFX/Bloom");if(shader)bloom=new Material(shader);GetComponent<Camera>().allowHDR=true;GetComponent<Camera>().depthTextureMode|=DepthTextureMode.Depth;}
        public void Impulse(float strength){trauma=Mathf.Clamp01(trauma+strength);}
        void LateUpdate()
        {
            float dt=Mathf.Min(Time.unscaledDeltaTime,.04f);trauma=Mathf.MoveTowards(trauma,0,dt*2.7f);
            float t=Time.unscaledTime;Vector3 target=new Vector3(Mathf.PerlinNoise(t*21,1)-.5f,Mathf.PerlinNoise(1,t*23)-.5f,0)*trauma*trauma*.32f*shakeScale;
            offset=Vector3.SmoothDamp(offset,target,ref velocity,.035f,5,dt);transform.localPosition=origin+offset;
        }
        void OnRenderImage(RenderTexture source,RenderTexture destination)
        {
            if(!bloomEnabled||!bloom||!bloom.shader.isSupported){Graphics.Blit(source,destination);return;}
            var a=RenderTexture.GetTemporary(Mathf.Max(1,source.width/2),Mathf.Max(1,source.height/2),0,RenderTextureFormat.DefaultHDR);
            var b=RenderTexture.GetTemporary(a.width,a.height,0,RenderTextureFormat.DefaultHDR);
            try
            {
                bloom.SetFloat("_Threshold",1);bloom.SetFloat("_Intensity",bloomIntensity);Graphics.Blit(source,a,bloom,0);
                for(int i=0;i<3;i++){bloom.SetVector("_Direction",new Vector4(1+i,0,0,0));Graphics.Blit(a,b,bloom,1);bloom.SetVector("_Direction",new Vector4(0,1+i,0,0));Graphics.Blit(b,a,bloom,1);}
                bloom.SetTexture("_BloomTex",a);Graphics.Blit(source,destination,bloom,2);
            }
            finally{RenderTexture.ReleaseTemporary(a);RenderTexture.ReleaseTemporary(b);}
        }
        void OnDestroy(){if(bloom)Destroy(bloom);transform.localPosition=origin;}
    }
}
