using System;
using UnityEngine;
using Mongle.VisualRenewal;

namespace Mongle.Slice.Testing
{
    /// <summary>
    /// 로컬 표시 테스트. 플레이 모드에서만 사용한다. 성장/각성/실제 스크롤 지급을 하지 않는다.
    /// 일반 성체 전환과 고등급 VFX 미리보기는 별도 동작이다.
    /// </summary>
    [AddComponentMenu("Mongle/Testing/Pet Test Controller")]
    public sealed class PetTestController:MonoBehaviour
    {
        public PetVisualManager visuals;
        public VisualQuality quality=VisualQuality.Standard;
        public string costumeId="";
        public PetGrade adultPreviewGrade=PetGrade.Unique;
        public bool showRuntimeButtons=true;
        public string Status { get; private set; }="Enter Play Mode to test.";
        public long EggRemainingSeconds=>egg==null?0:egg.Remaining(Now);
        public int Scrolls=>egg==null?0:egg.Scrolls;
        LocalHatchSimulation egg;
        long startUtc;double startRealtime;
        long Now=>startUtc+(long)Math.Max(0,Time.realtimeSinceStartupAsDouble-startRealtime);
        public static bool TestBuild
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }
        void Start(){if(TestBuild)ResetEgg();}

        bool Allowed()
        {
            if(Application.isPlaying&&TestBuild&&isActiveAndEnabled)return true;
            Debug.LogWarning("PetTestController requires Play Mode and an editor/development build.",this);return false;
        }
        [ContextMenu("Tests/Reset 8h Egg")]
        public void ResetEgg()
        {
            if(!Allowed())return;
            startUtc=DateTimeOffset.UtcNow.ToUnixTimeSeconds();startRealtime=Time.realtimeSinceStartupAsDouble;
            egg=new LocalHatchSimulation(startUtc);Report("[LOCAL] Egg reset: 28800 seconds / 1 test scroll.");
        }
        [ContextMenu("Tests/Equip Baby Neulgui")]
        public void EquipBabyNeulgui()=>Equip(GrowthStage.Baby,PetGrade.Common,false);

        [ContextMenu("Tests/Equip Adult Neulgui - Aura Preview")]
        public void EquipAdultNeulgui()=>Equip(GrowthStage.Adult,adultPreviewGrade,true);

        [ContextMenu("Tests/Equip Adult Neulgui - Common")]
        public void EquipCommonAdultNeulgui()=>Equip(GrowthStage.Adult,PetGrade.Common,false);

        void Equip(GrowthStage stage,PetGrade grade,bool requireAura)
        {
            if(!Allowed())return;
            try
            {
                if(!visuals)throw new InvalidOperationException("Assign PetVisualManager.");
                if(requireAura&&(int)grade<(int)PetGrade.Unique)
                    throw new InvalidOperationException("Set preview grade to Unique/Spirit/Divine for aura test.");
                visuals.Equip(0,stage,grade,quality,costumeId??"");
                var root=visuals.CurrentView;var definition=visuals.CurrentDefinition;
                if(!root||!root.activeInHierarchy||!definition||definition.speciesId!=0||definition.stage!=stage||definition.grade!=grade)
                    throw new InvalidOperationException("Equipped definition/view does not match the request.");
                var binding=root.GetComponent<PetVisualBindings>();
                if(!binding||!binding.bodyRenderer||!binding.bodyRenderer.sprite)
                    throw new InvalidOperationException("Whole-body sprite binding missing.");
                bool costumeFound=string.IsNullOrEmpty(costumeId);
                foreach(var c in definition.costumes??Array.Empty<PetCostumeAttachment>())
                    if(c!=null&&c.costumeId==costumeId)
                        foreach(var s in binding.sockets)
                            if(s.id==c.socketId&&s.socket&&c.prefab&&s.socket.Find(c.prefab.name+"(Clone)"))costumeFound=true;
                if(!costumeFound)throw new InvalidOperationException("Requested costume instance missing under its socket.");
                int configuredFx=0;
                foreach(var fx in definition.effects??Array.Empty<PetSocketEffect>())
                    if(fx!=null&&(int)quality>=(int)fx.minimumQuality)
                        foreach(var s in binding.sockets)
                            if(s.id==fx.socketId&&s.socket&&fx.prefab&&s.socket.Find(fx.prefab.name+"(Clone)"))configuredFx++;
                if(requireAura&&configuredFx==0)throw new InvalidOperationException("No aura prefab attached. Check grade, quality and Aura socket.");
                Report($"[PASS LOCAL VIEW] species=0 stage={stage} grade={grade} costume={costumeId} attachedFX={configuredFx}. No saved progression changed.");
                Debug.Log("Pet renderers enabled="+binding.bodyRenderer.enabled+", animation clips="+
                    (binding.animator&&binding.animator.runtimeAnimatorController?binding.animator.runtimeAnimatorController.animationClips.Length:0),this);
            }
            catch(Exception e){Status="[FAIL] "+e.Message;Debug.LogError(Status,this);}
        }

        [ContextMenu("Tests/Trigger Hatch Scroll")]
        public void TriggerHatchScroll()
        {
            if(!Allowed())return;if(egg==null)ResetEgg();
            long now=Now,before=egg.Remaining(now);
            if(!egg.TryUseInstantScroll(now)){Report("[LOCAL] Scroll not consumed: egg is ready or no scroll remains.");return;}
            if(egg.Remaining(now)!=0||egg.Scrolls!=0)throw new InvalidOperationException("Instant hatch assertion failed.");
            Report($"[PASS LOCAL SCROLL] {before}s -> 0s; test scrolls=0. No paid item or pet stats changed.");
        }
        void Report(string text){Status=text;Debug.Log(text,this);}
        void OnGUI()
        {
            if(!Application.isPlaying||!TestBuild||!showRuntimeButtons)return;
            GUILayout.BeginArea(new Rect(16,16,370,330),GUI.skin.box);
            GUILayout.Label("LOCAL TEST / NO SERVER MUTATION");
            if(GUILayout.Button("Equip Baby Neulgui"))EquipBabyNeulgui();
            if(GUILayout.Button("Equip Adult Neulgui (Aura Preview)"))EquipAdultNeulgui();
            if(GUILayout.Button("Equip Adult Neulgui (Common)"))EquipCommonAdultNeulgui();
            if(GUILayout.Button("Reset 8h Egg"))ResetEgg();
            if(GUILayout.Button("Trigger Hatch Scroll"))TriggerHatchScroll();
            GUILayout.Label($"Egg remaining: {EggRemainingSeconds}s / scrolls: {Scrolls}");
            GUILayout.Label(Status);GUILayout.EndArea();
        }
    }
}
