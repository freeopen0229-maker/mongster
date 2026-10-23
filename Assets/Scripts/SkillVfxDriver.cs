using System;
using System.Collections.Generic;
using UnityEngine;
namespace Mongle
{
    public sealed class SkillVfxDriver : MonoBehaviour
    {
        [Serializable]class Cast
        {
            public int id;public RpgSkillDefinition skill;public Vector3 origin,end,last;public float age;public GameObject body;public bool launched,queued;
        }
        class Pending {public Cast cast;public double at;public float fallbackAt;}
        public Transform caster;
        public RpgSkillDefinition[] skills;
        public CombatFeedback feedback;
        public VfxPresentation presentation;
        public Vector3 aim=new Vector3(1.4f,.8f,0);
        public int Hits {get;private set;}
        public double LastImpactScheduledDsp {get;private set;}
        public double LastImpactActualDsp {get;private set;}
        public int Casts {get;private set;}
        public string LastEvent {get;private set;}="Choose a skill";
        readonly List<Cast> active=new List<Cast>();readonly List<Pending> pending=new List<Pending>();
        readonly Collider[] hitBuffer=new Collider[64];readonly HashSet<VfxTarget> hitTargets=new HashSet<VfxTarget>();
        readonly float[] nextCast=new float[4];
        public void CastSkill(int index)
        {
            if(index<0||index>=skills.Length||Time.time<nextCast[index]||Time.timeScale<=0||active.Count>=8)return;
            var s=skills[index];nextCast[index]=Time.time+s.cooldown;var c=new Cast{id=CombatAttackId.Next(),skill=s,origin=caster.position+Vector3.up*.9f,end=aim,last=caster.position+Vector3.up*.9f};active.Add(c);Casts++;
            VfxLibrary.Quad(caster.position+Vector3.up*.035f,"arcane_sigil_ai",s.color,1.5f,s.windup+.2f,1.1f,true);
            VfxLibrary.Burst(c.origin,"magic_01",22,s.color,.12f,.5f,s.windup+.1f);
            if(s.kind==RpgSkill.ArcaneCircle)VfxLibrary.Quad(new Vector3(aim.x,.04f,aim.z),"arcane_sigil_ai",s.color,2.8f,s.windup+.5f,1.25f,true).GetComponent<VfxVisualMotion>().spin=35;
            LastEvent=s.kind+" / windup";
        }
        void Update()
        {
            Physics.SyncTransforms();
            for(int i=0;i<4;i++)if(Input.GetKeyDown(KeyCode.Alpha1+i))CastSkill(i);
            if(Input.GetMouseButtonDown(0)&&Input.mousePosition.y>100&&Input.mousePosition.y<Screen.height-150&&Camera.main)
            {
                var ray=Camera.main.ScreenPointToRay(Input.mousePosition);var plane=new Plane(Vector3.up,Vector3.zero);
                if(plane.Raycast(ray,out float enter)){var p=ray.GetPoint(enter);aim=new Vector3(Mathf.Clamp(p.x,-2.8f,4),.8f,Mathf.Clamp(p.z,-1.7f,2));}
            }
            for(int i=active.Count-1;i>=0;i--)
            {
                var c=active[i];c.age+=Time.deltaTime;
                if(c.age<c.skill.windup)continue;
                if(!c.launched){Launch(c);c.launched=true;}
                float u=Mathf.Clamp01((c.age-c.skill.windup)/Mathf.Max(.01f,c.skill.travel));
                if(c.body)
                {
                    c.body.transform.position=Vector3.Lerp(c.origin,c.end,u)+Vector3.up*Mathf.Sin(u*Mathf.PI)*.3f;
                    if(c.skill.kind==RpgSkill.BladeWave)c.body.transform.Rotate(0,0,Time.deltaTime*420,Space.Self);
                    Vector3 step=c.body.transform.position-c.last;
                    if(step.sqrMagnitude>.00001f&&Physics.SphereCast(c.last,.12f,step.normalized,out var hit,step.magnitude,1<<8,QueryTriggerInteraction.Collide))
                    {c.end=hit.point;c.body.transform.position=hit.point;u=1;}
                    c.last=c.body.transform.position;
                }
                if(u>=1&&!c.queued){QueueImpact(c);active.RemoveAt(i);}
            }
            for(int i=pending.Count-1;i>=0;i--)if(AudioSettings.dspTime>=pending[i].at||Time.unscaledTime>=pending[i].fallbackAt){Resolve(pending[i].cast);pending.RemoveAt(i);}
        }
        void Launch(Cast c)
        {
            LastEvent=c.skill.kind+" / release";
            if(c.skill.kind==RpgSkill.Fireball||c.skill.kind==RpgSkill.BladeWave)
            {
                c.body=VfxLibrary.Quad(c.origin,c.skill.kind==RpgSkill.Fireball?"fire_01":"slash_01",c.skill.color,c.skill.kind==RpgSkill.Fireball?.6f:1.4f,3,1);
                c.body.name=c.skill.kind+"_Projectile";VfxLibrary.Trail(c.body,c.skill.color,c.skill.kind==RpgSkill.Fireball?.35f:.5f,.23f);
                var light=c.body.AddComponent<Light>();light.color=c.skill.color;light.range=3;light.intensity=2;
            }
            else if(c.skill.kind==RpgSkill.Lightning)
            {
                Bolt(c.end+Vector3.up*5,c.end,c.skill.color,.07f);
                VfxLibrary.Quad(c.end,"flare_01",c.skill.color,1.2f,.18f,1.5f);
            }
            else
            {
                for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f;var start=c.end+new Vector3(Mathf.Cos(a),-.7f,Mathf.Sin(a));VfxLibrary.Burst(start,"magic_01",30,c.skill.color,.12f,1,.55f);}
                Bolt(c.end+Vector3.up*4,c.end,c.skill.color,.18f);
            }
        }
        void Bolt(Vector3 from,Vector3 to,Color color,float width)
        {
            var points=new Vector3[17];for(int i=0;i<points.Length;i++){float u=i/(float)(points.Length-1);points[i]=Vector3.Lerp(from,to,u);if(i>0&&i<points.Length-1)points[i]+=UnityEngine.Random.insideUnitSphere*.25f;}
            VfxLibrary.Line(points,color,width,.18f);VfxLibrary.Line(points,Color.white,width*.28f,.12f);
            for(int k=0;k<3;k++){var branch=new Vector3[5];Vector3 start=points[3+k*4];for(int j=0;j<branch.Length;j++)branch[j]=start+new Vector3((k%2==0?1:-1)*j*.2f,-j*.18f,j*.06f)+UnityEngine.Random.insideUnitSphere*.06f;VfxLibrary.Line(branch,color,width*.35f,.13f);}
        }
        void QueueImpact(Cast c)
        {
            c.queued=true;double at=feedback.PrepareImpact(c.skill.strength);LastImpactScheduledDsp=at;pending.Add(new Pending{cast=c,at=at,fallbackAt=Time.unscaledTime+.14f});
            if(c.body){c.body.GetComponent<VfxVisualMotion>().enabled=false;}
        }
        void Resolve(Cast c)
        {
            LastImpactActualDsp=AudioSettings.dspTime;LastEvent=c.skill.kind+" / impact";VfxLibrary.Impact(c.end,c.skill.color,c.skill.strength);
            int count=Physics.OverlapSphereNonAlloc(c.end,c.skill.radius,hitBuffer,1<<8,QueryTriggerInteraction.Collide);hitTargets.Clear();bool hit=false;
            for(int i=0;i<count;i++)
            {
                var target=hitBuffer[i].GetComponentInParent<VfxTarget>();if(!target||!hitTargets.Add(target))continue;
                var direction=target.transform.position-c.origin;direction.y=0;
                if(target.Hit(c.id,c.skill.damage,direction,c.skill.knockback)){Hits++;hit=true;}
            }
            if(hit)feedback.Impact(c.skill.strength);else presentation.Impulse(.04f*c.skill.strength);
            if(c.body){c.body.GetComponent<MeshRenderer>().enabled=false;var light=c.body.GetComponent<Light>();if(light)light.enabled=false;var trail=c.body.GetComponent<TrailRenderer>();if(trail){trail.transform.SetParent(null);trail.emitting=false;}Destroy(c.body,.35f);}
        }
        void OnGUI()
        {
            float scale=Mathf.Clamp(Screen.width/1100f,.65f,1.5f);GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));float w=Screen.width/scale,h=Screen.height/scale;
            GUI.Box(new Rect(18,18,w-36,112),"MONGLE / UNITY RPG VFX LAB");GUI.Label(new Rect(36,47,w-70,28),"1 Fireball   2 Blade wave   3 Lightning   4 Arcane circle / Click ground to aim");
            GUI.Label(new Rect(36,74,w-70,28),LastEvent+" | Hits "+Hits+" | Particle layers "+VfxLibrary.ActiveParticles);
            float width=Mathf.Min(170,(w-40)/4);for(int i=0;i<skills.Length;i++)if(GUI.Button(new Rect(20+i*width,h-92,width-6,42),skills[i].kind.ToString()))CastSkill(i);
            feedback.hitStopEnabled=GUI.Toggle(new Rect(22,h-42,160,28),feedback.hitStopEnabled,"Hit stop 0.05s");presentation.bloomEnabled=GUI.Toggle(new Rect(190,h-42,130,28),presentation.bloomEnabled,"Bloom");
            bool shake=GUI.Toggle(new Rect(330,h-42,130,28),presentation.shakeScale>0,"Camera shake");presentation.shakeScale=shake?1:0;
            GUI.Label(new Rect(470,h-42,w-490,28),"Audio layers: impact / +10ms snap / +20ms tail");
        }
        void OnDestroy(){VfxLibrary.ClearCache();}
    }
}
