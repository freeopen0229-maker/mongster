using UnityEngine;
using UnityEngine.EventSystems;

namespace FusionTamagotchi.Client
{
    public sealed class PreviewJoystick:MonoBehaviour,IPointerDownHandler,IDragHandler,IPointerUpHandler
    {
        public RectTransform knob;
        public Vector2 Value{get;private set;}
        int pointer=int.MinValue;
        public void OnPointerDown(PointerEventData e){if(pointer!=int.MinValue)return;pointer=e.pointerId;OnDrag(e);}
        public void OnDrag(PointerEventData e)
        {
            if(e.pointerId!=pointer)return;
            Vector2 point;RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,e.position,e.pressEventCamera,out point);
            Value=Vector2.ClampMagnitude(point/85,1);if(knob)knob.anchoredPosition=Value*65;
        }
        public void OnPointerUp(PointerEventData e){if(e.pointerId==pointer)ResetStick();}
        void OnDisable(){ResetStick();}
        void OnApplicationFocus(bool value){if(!value)ResetStick();}
        void ResetStick(){pointer=int.MinValue;Value=Vector2.zero;if(knob)knob.anchoredPosition=Vector2.zero;}
    }
}
