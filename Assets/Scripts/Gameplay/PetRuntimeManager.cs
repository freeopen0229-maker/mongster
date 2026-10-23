using System;
using UnityEngine;
using Online=Mongle.Slice.Client.PetRuntimeManager;
namespace Mongle.Gameplay
{
    /// <summary>Compatibility view for the old motor/animation components. No local save or reward authority.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class PetRuntimeManager:MonoBehaviour
    {
        public Online server;public PetAnimatorBridge animationBridge;
        public event Action Changed;public event Action<CareAction> Cared;
        public static long UtcNow=>DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public PetState State
        {
            get
            {
                var p=server?server.ProjectForDisplay():null;if(p==null)return null;
                return new PetState{level=p.Level,xp=p.Experience,coins=(int)Math.Min(int.MaxValue,server.AccountCoins),food=server.AccountFood,
                    hunger=p.Hunger,affection=p.Affection,fatigue=p.Fatigue,lastUtc=p.LastUtc};
            }
        }
        public PetCombatStats Stats
        {
            get
            {
                var p=server?server.Snapshot:null;if(p==null)return new PetCombatStats{moveSpeed=3.5f};
                var s=Mongle.Slice.PetRules.Stats(p);return new PetCombatStats{attack=(float)s.Attack,maxHealth=(float)s.Health,moveSpeed=(float)s.Speed,condition=(float)s.Condition};
            }
        }
        void OnEnable(){if(server){server.Changed+=Notify;server.CareConfirmed+=CareConfirmed;}}
        void OnDisable(){if(server){server.Changed-=Notify;server.CareConfirmed-=CareConfirmed;}}
        void Notify(){Changed?.Invoke();}
        void CareConfirmed(Mongle.Slice.CareAction action){Cared?.Invoke((CareAction)(int)action);}
        public void AdvanceNow(){if(server&&!server.Busy)_=server.RefreshAsync();}
        public bool TryCare(CareAction action)
        {if(!server||!server.Ready||server.Busy||server.HasPending)return false;_=server.CareAsync((Mongle.Slice.CareAction)(int)action);return true;}
        public bool BuyFood()=>false; // Requires an authenticated shop command; never mint locally.
        public bool TrySpendAttack()=>server&&server.Ready&&!server.HasPending&&!server.Busy;
        public void RewardVictory(){} // Visual combat cannot grant currency; exploration completion is server-owned.
        public IdleReward ClaimIdle(){if(server&&server.Ready&&!server.Busy&&!server.HasPending)_=server.ClaimIdleAsync();return default(IdleReward);}
        public void Save(){} // No mongle-pet-core-v1.json reads/writes or migration of untrusted local money.
    }
}
