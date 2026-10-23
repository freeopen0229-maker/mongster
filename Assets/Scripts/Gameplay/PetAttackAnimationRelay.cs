using UnityEngine;
namespace Mongle.Gameplay
{
    /// <summary>
    /// 반드시 Animator와 같은 GameObject에 둔다. 클립의 AnimationEvent가 이 메서드를 호출한다.
    /// 타격 프레임을 서로 다른 Coroutine/Invoke 타이머로 복제하지 않는다.
    /// </summary>
    public sealed class PetAttackAnimationRelay:MonoBehaviour
    {
        public PetAttackController attack;
        public PetAnimatorBridge bridge;
        public void AttackStart(){attack?.MarkerAttackStart();}
        public void DamageWindowOpen(){attack?.MarkerWindowOpen();}
        public void DamageWindowClose(){attack?.MarkerWindowClose();}
        public void AttackEnd(){attack?.MarkerAttackEnd();bridge?.CompleteAction();}
        public void ReactionEnd(){bridge?.CompleteAction();}
    }
}
