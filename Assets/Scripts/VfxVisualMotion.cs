using UnityEngine;
namespace Mongle
{
    public sealed class VfxVisualMotion : MonoBehaviour
    {
        public float duration=.5f,growth=1,spin=0,fade=1;
        public Vector3 drift;
        public Color color=Color.white;
        float age;Vector3 initial;Renderer visual;MaterialPropertyBlock block;
        void Start(){initial=transform.localScale;visual=GetComponent<Renderer>();block=new MaterialPropertyBlock();}
        void Update()
        {
            float dt=Time.unscaledDeltaTime;age+=dt;float u=Mathf.Clamp01(age/duration);
            transform.localScale=initial*Mathf.Lerp(1,growth,NeulguiMath.Ease(u));transform.Rotate(0,0,spin*dt,Space.Self);transform.position+=drift*dt;
            if(visual){visual.GetPropertyBlock(block);block.SetColor("_Tint",new Color(color.r,color.g,color.b,color.a*Mathf.Pow(1-u,fade)));visual.SetPropertyBlock(block);}
            if(age>=duration)Destroy(gameObject);
        }
    }
}
