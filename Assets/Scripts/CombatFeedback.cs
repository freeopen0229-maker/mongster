using UnityEngine;
namespace Mongle
{
    public sealed class CombatFeedback : MonoBehaviour
    {
        public bool hitStopEnabled=true;
        public float hitStopSeconds=.05f;
        readonly HitStopState freeze = new HitStopState();
        public static CombatFeedback Instance {get;private set;}
        public VfxPresentation cameraFx;
        AudioSource[] voices;int nextVoice;
        AudioClip thump,snap,tail;
        void Awake()
        {
            Instance=this;voices=new AudioSource[24];
            for(int i=0;i<voices.Length;i++){voices[i]=gameObject.AddComponent<AudioSource>();voices[i].playOnAwake=false;voices[i].spatialBlend=0;voices[i].volume=.6f;}
            thump=Resources.Load<AudioClip>("VFX/impact_thump");snap=Resources.Load<AudioClip>("VFX/impact_snap");tail=Resources.Load<AudioClip>("VFX/impact_tail");
        }
        public double PrepareImpact(float strength)
        {
            double at=AudioSettings.dspTime+.04;
            Schedule(thump,at,.65f*strength,.85f);Schedule(snap,at+.01,.42f*strength,1.05f);Schedule(tail,at+.02,.24f*strength,1);
            return at;
        }
        /// <summary>명중 이벤트용. 이미 메모리에 올린 소리를 다음 DSP 버퍼에 예약한다.
        /// 화면의 한 프레임과 실제 스피커 지연은 기기별 검증 대상이며 0ms를 보장하지 않는다.</summary>
        public void PlayImpactNow(float strength)
        {
            AudioSettings.GetDSPBufferSize(out int samples,out int buffers);
            double lead=(double)samples/Mathf.Max(1,AudioSettings.outputSampleRate);
            double at=AudioSettings.dspTime+lead;
            Schedule(thump,at,.65f*strength,.85f);Schedule(snap,at+.01,.42f*strength,1.05f);Schedule(tail,at+.02,.24f*strength,1);
        }
        void Schedule(AudioClip clip,double at,float volume,float pitch)
        {
            if(!clip)return;var voice=voices[nextVoice++%voices.Length];voice.Stop();voice.clip=clip;voice.volume=Mathf.Clamp01(volume);voice.pitch=pitch;voice.PlayScheduled(at);
        }
        public void Impact(float strength)
        {
            cameraFx?.Impulse(.26f*strength);
            if(!hitStopEnabled)return;
            Time.timeScale=freeze.Trigger(Time.unscaledTimeAsDouble,hitStopSeconds,Time.timeScale);
        }
        void Update(){if(freeze.Frozen)Time.timeScale=freeze.Evaluate(Time.unscaledTimeAsDouble);}
        void Restore(){if(freeze.Frozen)Time.timeScale=freeze.Release();}
        void OnDisable(){Restore();}
        void OnDestroy(){Restore();if(Instance==this)Instance=null;}
    }
}
