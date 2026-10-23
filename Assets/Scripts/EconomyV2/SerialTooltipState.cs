namespace FusionTamagotchi.Client
{
    // 실제 컴포넌트가 사용하는 입력 상태 로직. Unity EventSystem/raycast는 별도 검증 대상.
    public sealed class SerialTooltipState
    {
        public bool Available {get;private set;}
        public bool Visible {get;private set;}
        public void Bind(bool available){Available=available;Visible=false;}
        public void Enter(bool mouse){if(mouse&&Available)Visible=true;}
        public void Exit(bool mouse){if(mouse)Visible=false;}
        public void Click(bool mouse){if(Available)Visible=mouse||!Visible;}
        public void Hide(){Visible=false;}
    }
}
