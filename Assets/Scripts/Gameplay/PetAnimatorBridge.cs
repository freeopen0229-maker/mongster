using UnityEngine;
namespace Mongle.Gameplay
{
    /// <summary>
    /// Animator를 다루는 단일 창구. Locomotion의 MoveSpeed BlendTree와 액션 CrossFade를 분리한다.
    /// CrossFade를 매 프레임 호출하지 않아 클립이 계속 처음으로 되감기는 문제를 피한다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class PetAnimatorBridge:MonoBehaviour
    {
        public PetRuntimeManager data;
        [Min(.01f)]public float parameterDamp=.12f,crossfadeSeconds=.14f;
        public bool ActionBusy {get;private set;}
        public event System.Action Interrupted;
        Animator animator;float deadline;
        static readonly int MoveSpeed=Animator.StringToHash("MoveSpeed");
        void Awake(){animator=GetComponent<Animator>();animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;}
        void OnEnable(){if(data)data.Cared+=React;}
        void OnDisable(){if(data)data.Cared-=React;CancelAction();}
        public void SetMovement(float speed){if(animator)animator.SetFloat(MoveSpeed,Mathf.Clamp01(speed),parameterDamp,Time.deltaTime);}
        public bool BeginAttack(){return Begin("Attack",1.5f);}
        bool Begin(string state,float maxSeconds)
        {
            if(ActionBusy||!animator||!animator.runtimeAnimatorController)return false;
            int hash=Animator.StringToHash(state);
            if(!animator.HasState(0,hash)){Debug.LogWarning("Missing Animator state: "+state);return false;}
            ActionBusy=true;deadline=Time.time+maxSeconds;
            animator.CrossFadeInFixedTime(hash,crossfadeSeconds,0,0);return true;
        }
        void React(CareAction action){Begin(action==CareAction.Feed?"Feed":action==CareAction.Pet?"Pet":"Play",1.6f);}
        /// <summary>AnimationEvent의 종료 표식. 공격/돌봄이 끝나면 BlendTree로 부드럽게 복귀한다.</summary>
        public void CompleteAction(){if(!ActionBusy)return;ActionBusy=false;animator.CrossFadeInFixedTime("Locomotion",crossfadeSeconds,0);}
        public void CancelAction(){if(!ActionBusy)return;ActionBusy=false;Interrupted?.Invoke();if(animator&&animator.runtimeAnimatorController)animator.CrossFadeInFixedTime("Locomotion",crossfadeSeconds,0);}
        void Update(){if(ActionBusy&&Time.time>deadline)CancelAction();} // 종료 이벤트 누락 시 이동을 영구 잠그지 않는 방어 장치
    }
}
