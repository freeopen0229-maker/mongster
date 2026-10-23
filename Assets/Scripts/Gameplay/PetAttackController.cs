using System;
using System.Collections.Generic;
using UnityEngine;
namespace Mongle.Gameplay
{
    /// <summary>동일 애니메이션 이벤트에서 공격 판정·Trail·파티클을 시작한다. 같은 적은 한 공격당 한 번만 맞는다.</summary>
    public sealed class PetAttackController:MonoBehaviour
    {
        public PetRuntimeManager data;
        public PetAnimatorBridge animationBridge;
        public Transform visualRoot,hitOrigin,weaponTip;
        public TrailRenderer trail;
        public CombatFeedback feedback;
        public LayerMask targetLayers=1<<8;
        [Min(.05f)]public float radius=.85f;
        [Min(0)]public float cooldown=.65f,damageMultiplier=1.8f;
        public event Action WindowOpened,WindowClosed,HitConfirmed;
        readonly Collider[] buffer=new Collider[32];
        readonly RaycastHit[] sweepBuffer=new RaycastHit[32];
        readonly HashSet<VfxTarget> damaged=new HashSet<VfxTarget>();
        int attackId;float nextAttack,damage;bool reserved,active,pendingOpen,pendingClose,pendingEnd;
        Vector3 previous;
        void OnEnable(){if(animationBridge)animationBridge.Interrupted+=Cancel;}
        void OnDisable(){if(animationBridge)animationBridge.Interrupted-=Cancel;Cancel();}
        public bool RequestAttack()
        {
            if(!data||!animationBridge||animationBridge.ActionBusy||reserved||Time.time<nextAttack||Time.timeScale<=0)return false;
            if(!animationBridge.BeginAttack())return false;
            if(!data.TrySpendAttack()){animationBridge.CancelAction();return false;}
            attackId=CombatAttackId.Next();damage=data.Stats.attack*damageMultiplier;damaged.Clear();reserved=true;nextAttack=Time.time+cooldown;
            return true;
        }
        public void MarkerAttackStart()
        {
            if(!reserved)return;
            VfxLibrary.Quad(transform.position+Vector3.up*.04f,"arcane_sigil_ai",Color.cyan,1.2f,.35f,1.1f,true);
        }
        public void MarkerWindowOpen(){if(reserved&&!active)pendingOpen=true;}
        public void MarkerWindowClose(){pendingClose=true;}
        public void MarkerAttackEnd(){pendingEnd=true;pendingClose=true;}
        void CloseNow(){if(!active)return;active=false;if(trail)trail.emitting=false;WindowClosed?.Invoke();}
        void Cancel(){CloseNow();reserved=false;pendingOpen=pendingClose=pendingEnd=false;}
        void LateUpdate()
        {
            // AnimationEvent는 표식만 전달한다. 본의 최종 Transform 적용 후 같은 LateUpdate에서
            // Trail 시작·입자·판정을 함께 실행해 이전 프레임 본 위치를 사용하는 일을 피한다.
            if(pendingOpen&&reserved&&hitOrigin)
            {
                pendingOpen=false;active=true;previous=hitOrigin.position;
                if(trail){trail.Clear();trail.emitting=true;}
                VfxLibrary.Burst(hitOrigin.position,"magic_01",12,Color.cyan,.12f,.7f,.25f);
                WindowOpened?.Invoke();
            }
            if(active&&Time.deltaTime>0)Sample();
            if(pendingClose){pendingClose=false;CloseNow();}
            if(pendingEnd){pendingEnd=false;reserved=false;}
        }
        void Sample()
        {
            if(!hitOrigin)return;Physics.SyncTransforms();
            int count=Physics.OverlapSphereNonAlloc(hitOrigin.position,radius,buffer,targetLayers,QueryTriggerInteraction.Collide);
            for(int i=0;i<count;i++)Damage(buffer[i]);
            Vector3 delta=hitOrigin.position-previous;
            if(delta.sqrMagnitude>.00001f)
            {
                int n=Physics.SphereCastNonAlloc(previous,radius,delta.normalized,sweepBuffer,delta.magnitude,targetLayers,QueryTriggerInteraction.Collide);
                for(int i=0;i<n;i++)Damage(sweepBuffer[i].collider);
            }
            previous=hitOrigin.position;
        }
        void Damage(Collider collider)
        {
            var target=collider.GetComponentInParent<VfxTarget>();if(!target||damaged.Contains(target))return;
            Vector3 direction=target.transform.position-transform.position;direction.y=0;
            if(direction.sqrMagnitude>.001f&&Vector3.Dot(visualRoot.forward,direction.normalized)<.15f)return;
            float before=target.health;
            if(!target.Hit(attackId,damage,direction,2))return;
            damaged.Add(target);
            // 실제 명중이 확인된 한 지점에서 피격 VFX/소리/히트스톱을 호출한다.
            Vector3 point=collider.ClosestPoint(hitOrigin.position);
            VfxLibrary.Impact(point,Color.cyan,.75f);feedback?.PlayImpactNow(1);feedback?.Impact(1);HitConfirmed?.Invoke();
            if(before>0&&target.health<=0)data.RewardVictory();
        }
        void OnDrawGizmosSelected(){if(hitOrigin){Gizmos.color=active?Color.red:Color.yellow;Gizmos.DrawWireSphere(hitOrigin.position,radius);}}
    }
}
