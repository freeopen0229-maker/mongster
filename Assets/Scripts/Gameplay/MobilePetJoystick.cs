using UnityEngine;
using UnityEngine.UIElements;
namespace Mongle.Gameplay
{
    /// <summary>
    /// UI Toolkit용 가상 조이스틱. 한 손가락만 소유하고, 영역 밖 드래그도 포인터 캡처로 받는다.
    /// 공격 버튼은 다른 pointerId를 사용할 수 있어 이동+공격 멀티터치를 방해하지 않는다.
    /// </summary>
    public sealed class MobilePetJoystick:MonoBehaviour
    {
        [Range(0,.5f)]public float deadZone=.12f;
        public Vector2 Value {get;private set;}
        VisualElement pad,knob;int owner=-1;
        public void Bind(VisualElement area,VisualElement handle)
        {
            Unbind();pad=area;knob=handle;
            pad.RegisterCallback<PointerDownEvent>(Down);pad.RegisterCallback<PointerMoveEvent>(Move);
            pad.RegisterCallback<PointerUpEvent>(Up);pad.RegisterCallback<PointerCancelEvent>(Cancel);pad.RegisterCallback<PointerCaptureOutEvent>(CaptureLost);
        }
        void Down(PointerDownEvent e){if(owner>=0)return;owner=e.pointerId;pad.CapturePointer(owner);Read(e.position);e.StopPropagation();}
        void Move(PointerMoveEvent e){if(e.pointerId!=owner)return;Read(e.position);e.StopPropagation();}
        void Up(PointerUpEvent e){if(e.pointerId==owner)ResetInput();}
        void Cancel(PointerCancelEvent e){if(e.pointerId==owner)ResetInput();}
        void CaptureLost(PointerCaptureOutEvent e){if(e.pointerId==owner)ResetInput();}
        void Read(Vector2 position)
        {
            Vector2 p=pad.WorldToLocal(position),center=pad.contentRect.center;
            float radius=Mathf.Max(1,Mathf.Min(pad.contentRect.width,pad.contentRect.height)*.38f);
            Vector2 clamped=Vector2.ClampMagnitude((p-center)/radius,1);
            float magnitude=clamped.magnitude;
            Value=magnitude<=deadZone?Vector2.zero:clamped.normalized*((magnitude-deadZone)/(1-deadZone));
            if(knob!=null)knob.style.translate=new Translate(clamped.x*radius,clamped.y*radius,0);
            // UI Toolkit의 y축은 아래 방향이므로 이동 코드에서 Value.y를 반전한다.
        }
        public void ResetInput()
        {
            int id=owner;owner=-1;Value=Vector2.zero;
            if(knob!=null)knob.style.translate=new Translate(0,0,0);
            if(pad!=null&&id>=0&&pad.HasPointerCapture(id))pad.ReleasePointer(id);
        }
        void Unbind()
        {
            ResetInput();if(pad==null)return;
            pad.UnregisterCallback<PointerDownEvent>(Down);pad.UnregisterCallback<PointerMoveEvent>(Move);pad.UnregisterCallback<PointerUpEvent>(Up);
            pad.UnregisterCallback<PointerCancelEvent>(Cancel);pad.UnregisterCallback<PointerCaptureOutEvent>(CaptureLost);pad=null;knob=null;
        }
        void OnDisable(){ResetInput();}
        void OnApplicationFocus(bool focused){if(!focused)ResetInput();}
        void OnApplicationPause(bool paused){if(paused)ResetInput();}
        void OnDestroy(){Unbind();}
    }
}
