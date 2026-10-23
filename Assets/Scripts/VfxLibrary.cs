using System.Collections.Generic;
using UnityEngine;
namespace Mongle
{
    public static class VfxLibrary
    {
        static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        static readonly Queue<VfxParticleLease> pool=new Queue<VfxParticleLease>();
        public static int ActiveParticles {get;private set;}
        public static Material Material(string texture)
        {
            if(materials.TryGetValue(texture,out var m)&&m)return m;
            var shader=Resources.Load<Shader>("VFX/Energy");if(!shader)throw new System.InvalidOperationException("Missing VFX/Energy shader");
            m=new Material(shader){name="VFX_"+texture};m.mainTexture=texture=="white"?Texture2D.whiteTexture:Resources.Load<Texture2D>("VFX/"+texture);
            if(!m.mainTexture)throw new System.InvalidOperationException("Missing VFX texture: "+texture);
            materials[texture]=m;return m;
        }
        public static Color Hdr(Color c,float intensity){return new Color(c.r*intensity,c.g*intensity,c.b*intensity,c.a);}
        public static void Burst(Vector3 position,string texture,int count,Color color,float size,float speed,float lifetime)
        {
            if(ActiveParticles>=80)return;
            VfxParticleLease lease=null;while(pool.Count>0&&!lease)lease=pool.Dequeue();
            if(!lease){var go=new GameObject("PooledParticles");lease=go.AddComponent<VfxParticleLease>();lease.system=go.AddComponent<ParticleSystem>();}
            lease.gameObject.SetActive(true);lease.transform.position=position;lease.transform.rotation=Quaternion.identity;
            var ps=lease.system;ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=false;main.duration=1;main.startLifetime=new ParticleSystem.MinMaxCurve(lifetime*.55f,lifetime);main.startSpeed=new ParticleSystem.MinMaxCurve(speed*.45f,speed);main.startSize=new ParticleSystem.MinMaxCurve(size*.5f,size);main.startColor=Hdr(color,3);main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);main.maxParticles=256;main.simulationSpace=ParticleSystemSimulationSpace.World;main.useUnscaledTime=true;main.gravityModifier=texture.Contains("spark")?.32f:0;
            var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.07f;
            var noise=ps.noise;noise.enabled=texture.Contains("smoke")||texture.Contains("fire");noise.strength=.3f;noise.frequency=1.7f;noise.scrollSpeed=2;
            var sizeOver=ps.sizeOverLifetime;sizeOver.enabled=true;sizeOver.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,1,1,texture.Contains("smoke")?2:0));
            var c=ps.colorOverLifetime;c.enabled=true;var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(Hdr(color,4),0),new GradientColorKey(color,.5f),new GradientColorKey(color,1)},new[]{new GradientAlphaKey(.9f,0),new GradientAlphaKey(.7f,.35f),new GradientAlphaKey(0,1)});c.color=g;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=Material(texture);renderer.renderMode=ParticleSystemRenderMode.Billboard;
            lease.releaseAt=Time.unscaledTime+lifetime+.1f;ActiveParticles++;ps.Play();ps.Emit(Mathf.Min(220,count));
        }
        public static void Return(VfxParticleLease lease){if(!lease.gameObject.activeSelf)return;lease.system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);lease.gameObject.SetActive(false);ActiveParticles=Mathf.Max(0,ActiveParticles-1);pool.Enqueue(lease);}
        public static GameObject Quad(Vector3 position,string texture,Color color,float size,float lifetime,float growth,bool floor=false)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Quad);go.name="VFX_"+texture;Object.Destroy(go.GetComponent<Collider>());go.transform.position=position;
            go.transform.rotation=floor?Quaternion.Euler(90,0,0):Camera.main?Camera.main.transform.rotation:Quaternion.identity;
            go.transform.localScale=Vector3.one*size;go.GetComponent<Renderer>().sharedMaterial=Material(texture);
            var motion=go.AddComponent<VfxVisualMotion>();motion.color=Hdr(color,3);motion.duration=lifetime;motion.growth=growth;return go;
        }
        public static TrailRenderer Trail(GameObject go,Color color,float width,float life)
        {
            var trail=go.AddComponent<TrailRenderer>();trail.sharedMaterial=Material("trace_01");trail.time=life;trail.minVertexDistance=.015f;trail.startWidth=width;trail.endWidth=0;trail.numCornerVertices=4;trail.numCapVertices=3;trail.emitting=true;
            var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(Hdr(color,5),0),new GradientColorKey(color,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});trail.colorGradient=g;return trail;
        }
        public static LineRenderer Line(Vector3[] points,Color color,float width,float life)
        {
            var go=new GameObject("LightningBranch");var line=go.AddComponent<LineRenderer>();line.sharedMaterial=Material("white");line.useWorldSpace=true;line.positionCount=points.Length;line.SetPositions(points);line.startWidth=width;line.endWidth=width*.3f;line.startColor=Hdr(color,5);line.endColor=Hdr(color,2);line.numCapVertices=3;
            var motion=go.AddComponent<VfxVisualMotion>();motion.color=Hdr(color,3);motion.duration=life;return line;
        }
        public static void Impact(Vector3 point,Color color,float scale)
        {
            Burst(point,"spark_01",70,color,.09f*scale,3.4f*scale,.55f);
            Burst(point,"fire_01",20,color,.38f*scale,.8f*scale,.42f);
            Burst(point,"smoke_01",12,new Color(.35f,.38f,.4f,.5f),.45f*scale,.4f,1.1f);
            Quad(point,"light_01",Color.white,.65f*scale,.13f,2.8f);
            Quad(point,"circle_01",color,.4f,.4f,7*scale);
            Quad(new Vector3(point.x,.025f,point.z),"circle_01",color,.4f,.55f,8*scale,true);
        }
        public static void ClearCache()
        {
            foreach(var m in materials.Values)if(m)Object.Destroy(m);materials.Clear();
            while(pool.Count>0){var p=pool.Dequeue();if(p)Object.Destroy(p.gameObject);}ActiveParticles=0;
        }
    }
}
