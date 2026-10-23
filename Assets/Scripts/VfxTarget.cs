using System.Collections.Generic;
using UnityEngine;
namespace Mongle
{
    public sealed class VfxTarget : MonoBehaviour
    {
        public float health=100,maxHealth=100;
        public Renderer[] visuals;
        public Color baseColor=new Color(.3f,.45f,.5f,1);
        public int HitCount {get;private set;}
        public float LastFlash {get;private set;}
        readonly HashSet<int> received=new HashSet<int>();
        MaterialPropertyBlock block;Vector3 spawn,velocity;float flashUntil,resetAt;
        void Awake(){spawn=transform.position;block=new MaterialPropertyBlock();visuals=GetComponentsInChildren<Renderer>();}
        public bool Hit(int attack,float damage,Vector3 direction,float knockback)
        {
            if(health<=0||!received.Add(attack))return false;
            health=Mathf.Max(0,health-damage);HitCount++;LastFlash=Time.unscaledTime;flashUntil=Time.unscaledTime+.16f;
            ApplyTint(new Color(3,.24f,.12f),1);
            velocity+=direction.normalized*knockback;if(health<=0)resetAt=Time.unscaledTime+2;
            return true;
        }
        void Update()
        {
            float dt=Mathf.Min(Time.deltaTime,.04f);transform.position+=velocity*dt;velocity*=Mathf.Exp(-8*dt);
            transform.position=new Vector3(Mathf.Clamp(transform.position.x,-5,5),transform.position.y,Mathf.Clamp(transform.position.z,-2,3));
            float t=Mathf.Clamp01((flashUntil-Time.unscaledTime)/.16f);Color tint=Color.Lerp(baseColor,new Color(3,.24f,.12f),t);
            ApplyTint(tint,t);
            if(resetAt>0&&Time.unscaledTime>=resetAt)ResetTarget();
        }
        void ApplyTint(Color tint,float flash)
        {
            foreach(var r in visuals){r.GetPropertyBlock(block);block.SetColor("_Color",tint);block.SetColor("_EmissionColor",Color.red*flash*2);r.SetPropertyBlock(block);}
        }
        public void ResetTarget(){health=maxHealth;velocity=Vector3.zero;transform.position=spawn;received.Clear();resetAt=0;}
    }
}
