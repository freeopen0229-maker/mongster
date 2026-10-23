using UnityEngine;
namespace Mongle.Gameplay
{
    /// <summary>입력은 Update, 물리 속도는 FixedUpdate, 시각 회전은 Update에서 처리한다.</summary>
    [RequireComponent(typeof(Rigidbody),typeof(CapsuleCollider))]
    public sealed class PetMotor:MonoBehaviour
    {
        public MobilePetJoystick joystick;
        public PetRuntimeManager data;
        public PetAnimatorBridge animationBridge;
        public Transform visualRoot;
        public Camera viewCamera;
        [Min(.1f)]public float acceleration=12,deceleration=18,turnSharpness=14;
        Vector3 desiredDirection,smoothedVelocity;float desiredMagnitude;
        Rigidbody body;
        public float NormalizedSpeed {get;private set;}
        void Awake()
        {
            body=GetComponent<Rigidbody>();body.interpolation=RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            body.constraints=RigidbodyConstraints.FreezeRotation;
            if(!viewCamera)viewCamera=Camera.main;
        }
        void Update()
        {
            Vector2 input=joystick?joystick.Value:Vector2.zero;
#if UNITY_EDITOR || UNITY_STANDALONE
            // 에디터 키보드 검증용. 모바일 빌드에서는 이 분기가 제거된다.
            if(input.sqrMagnitude<.001f)input=new Vector2(Input.GetAxisRaw("Horizontal"),-Input.GetAxisRaw("Vertical"));
#endif
            input=Vector2.ClampMagnitude(input,1);
            if(animationBridge&&animationBridge.ActionBusy)input=Vector2.zero;
            desiredMagnitude=input.magnitude;
            Vector3 forward=viewCamera?Vector3.ProjectOnPlane(viewCamera.transform.forward,Vector3.up).normalized:Vector3.forward;
            if(forward.sqrMagnitude<.01f)forward=Vector3.forward;
            Vector3 right=Vector3.Cross(Vector3.up,forward).normalized;
            desiredDirection=right*input.x-forward*input.y;
            if(visualRoot&&desiredDirection.sqrMagnitude>.001f&&Time.deltaTime>0)
            {
                // 지수 보간 계수는 30/60/120fps에서 대체로 같은 회전 응답을 만든다.
                float blend=1-Mathf.Exp(-turnSharpness*Time.deltaTime);
                visualRoot.rotation=Quaternion.Slerp(visualRoot.rotation,Quaternion.LookRotation(desiredDirection,Vector3.up),blend);
            }
            float max=data?data.Stats.moveSpeed:3.5f;
            NormalizedSpeed=Mathf.Clamp01(new Vector2(body.linearVelocity.x,body.linearVelocity.z).magnitude/Mathf.Max(.01f,max));
            animationBridge?.SetMovement(NormalizedSpeed);
        }
        void FixedUpdate()
        {
            float max=data?data.Stats.moveSpeed:3.5f;
            Vector3 desired=animationBridge&&animationBridge.ActionBusy?Vector3.zero:desiredDirection.normalized*(max*desiredMagnitude);
            float rate=desired.sqrMagnitude>smoothedVelocity.sqrMagnitude?acceleration:deceleration;
            float blend=1-Mathf.Exp(-rate*Time.fixedDeltaTime);
            // 명시적인 Lerp 가속/감속. Y 속도는 중력/바닥 충돌에 맡긴다.
            smoothedVelocity.x=Mathf.Lerp(smoothedVelocity.x,desired.x,blend);
            smoothedVelocity.z=Mathf.Lerp(smoothedVelocity.z,desired.z,blend);
            body.linearVelocity=new Vector3(smoothedVelocity.x,body.linearVelocity.y,smoothedVelocity.z);
        }
        void OnDisable(){desiredMagnitude=0;desiredDirection=Vector3.zero;smoothedVelocity=Vector3.zero;if(body)body.linearVelocity=new Vector3(0,body.linearVelocity.y,0);}
    }
}
