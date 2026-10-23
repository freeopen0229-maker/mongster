using System;
using System.IO;
using UnityEngine;
namespace Mongle.Gameplay
{
    /// <summary>생활 시간·개체 데이터·저장을 소유한다. 소비자는 Changed 이벤트로 HUD/능력치를 갱신한다.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class PetRuntimeManager:MonoBehaviour
    {
        public PetAnimatorBridge animationBridge;
        public PetState State {get;private set;}
        public PetCombatStats Stats=>PetRules.Stats(State);
        public event Action Changed;
        public event Action<CareAction> Cared;
        string path;float tick,saveClock;bool loaded;
        public static long UtcNow=>DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        void Awake()
        {
            path=Path.Combine(Application.persistentDataPath,"mongle-pet-core-v1.json");
            State=Read(path)??Read(path+".bak")??PetRules.New(UtcNow);
            loaded=true;AdvanceNow();
        }
        PetState Read(string file)
        {
            if(!File.Exists(file))return null;
            try{var s=JsonUtility.FromJson<PetState>(File.ReadAllText(file));return PetRules.IsValid(s)?s:null;}
            catch(Exception e){Debug.LogWarning("Pet save could not be read: "+e.GetType().Name);return null;}
        }
        void Update()
        {
            // 욕구 계산은 1초 간격. 렌더 프레임마다 파일 I/O나 날짜 파싱을 하지 않는다.
            tick+=Time.unscaledDeltaTime;saveClock+=Time.unscaledDeltaTime;
            if(tick>=1){tick=0;AdvanceNow();}
            if(saveClock>=30){saveClock=0;Save();}
        }
        public void AdvanceNow(){if(!loaded)return;PetRules.Advance(State,UtcNow);Changed?.Invoke();}
        public bool TryCare(CareAction action)
        {
            if(animationBridge&&animationBridge.ActionBusy)return false;
            if(!PetRules.Care(State,action,UtcNow))return false;
            Changed?.Invoke();Cared?.Invoke(action);Save();return true;
        }
        public bool BuyFood(){AdvanceNow();if(!PetRules.BuyFood(State))return false;Changed?.Invoke();Save();return true;}
        public bool TrySpendAttack(){AdvanceNow();if(!PetRules.SpendAttack(State))return false;Changed?.Invoke();return true;}
        public void RewardVictory(){PetRules.Victory(State,UtcNow);Changed?.Invoke();Save();}
        public IdleReward ClaimIdle(){var reward=PetRules.Claim(State,UtcNow);Changed?.Invoke();Save();return reward;}
        public void Save()
        {
            if(!loaded)return;
            string temp=path+".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(temp,JsonUtility.ToJson(State,true));
                if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);
            }
            catch(Exception e){Debug.LogWarning("Pet save failed; state remains in memory: "+e.GetType().Name);}
        }
        void OnApplicationPause(bool paused){if(paused){AdvanceNow();Save();}else AdvanceNow();}
        void OnApplicationFocus(bool focused){if(!focused)Save();else AdvanceNow();}
        void OnDestroy(){Save();}
    }
}
