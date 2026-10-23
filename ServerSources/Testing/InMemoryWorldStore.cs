using System;
using System.Collections.Generic;
using System.Linq;
using Mongle.Slice;
using FusionTamagotchi;

namespace Mongle.Slice.Server.Testing
{
    /// <summary>
    /// 테스트 전용 단일 프로세스 저장소. 전체 복사 후 성공 시 교체하여 예외 시 롤백한다.
    /// 다중 서버/DB 영속성/인증을 제공하지 않는다. 운영 서버에 배포하지 않는다.
    /// </summary>
    public sealed class InMemoryWorldStore:IWorldStore
    {
        // 로컬 HTTPS 참고 서버의 영속 snapshot. 운영용 정규화 DB adapter는 아니다.
        [Serializable] public sealed class CountEntry{public string Owner;public int Count;}
        [Serializable] public sealed class GrantEntry{public string Id,Owner;}
        [Serializable] public sealed class WorldSave
        {
            public List<PetState> Pets;public List<AccountState> Accounts;public List<TradeOffer> Offers;
            public List<TradeReceipt> Trades;public List<ActionReceipt> Actions;
            public List<EconomyAsset> Assets;public List<EconomyListing> EconomyOffers;public List<LimitedQuota> Quotas;
            public List<AwakeningNotice> Notices;public List<EconomyReceipt> Receipts;
            public List<string> Costumes;public List<CountEntry> Scrolls;public List<GrantEntry> Grants;public long Burned;
        }
        public sealed class World:IWorldTransaction
        {
            public readonly Dictionary<string,PetState> Pets=new Dictionary<string,PetState>();
            public readonly Dictionary<string,AccountState> Accounts=new Dictionary<string,AccountState>();
            public readonly Dictionary<string,TradeOffer> Offers=new Dictionary<string,TradeOffer>();
            readonly Dictionary<string,TradeReceipt> trades=new Dictionary<string,TradeReceipt>();
            readonly Dictionary<string,ActionReceipt> actions=new Dictionary<string,ActionReceipt>();
            public readonly Dictionary<string,EconomyAsset> EconomyAssets=new Dictionary<string,EconomyAsset>();
            public readonly Dictionary<string,EconomyListing> EconomyOffers=new Dictionary<string,EconomyListing>();
            public readonly Dictionary<int,LimitedQuota> Quotas=new Dictionary<int,LimitedQuota>();
            public readonly List<AwakeningNotice> AwakeningOutbox=new List<AwakeningNotice>();
            readonly Dictionary<string,EconomyReceipt> economyReceipts=new Dictionary<string,EconomyReceipt>();
            readonly HashSet<string> costumes=new HashSet<string>();
            readonly Dictionary<string,int> scrolls=new Dictionary<string,int>();
            readonly Dictionary<string,string> externalGrants=new Dictionary<string,string>();
            public EconomyAsset Asset(string id)=>EconomyAssets.TryGetValue(id,out var a)?a:throw new InvalidOperationException("Asset missing.");
            public void AddAsset(EconomyAsset a)=>EconomyAssets.Add(a.Id,a.Clone());
            public void RemoveAsset(string id){if(!EconomyAssets.Remove(id))throw new InvalidOperationException("Asset missing.");}
            public EconomyListing EconomyOffer(string id)=>EconomyOffers.TryGetValue(id,out var a)?a:throw new InvalidOperationException("Economy offer missing.");
            public void AddEconomyOffer(EconomyListing a)=>EconomyOffers.Add(a.Id,a.Clone());
            public LimitedQuota Quota(int type)=>Quotas.TryGetValue(type,out var q)?q:throw new InvalidOperationException("Hidden quota missing; configure B~E quantities.");
            public EconomyReceipt EconomyReceipt(string owner,string key)=>economyReceipts.TryGetValue(owner+"/"+key,out var r)?r:null;
            public void AddEconomyReceipt(EconomyReceipt r)=>economyReceipts.Add(r.AccountId+"/"+r.RequestId,r.Clone());
            public bool HasCostume(string owner,string costume)=>costumes.Contains(owner+"/"+costume);
            public void AddCostume(string owner,string costume){if(!costumes.Add(owner+"/"+costume))throw new InvalidOperationException("Costume already owned.");}
            public int Scrolls(string owner)=>scrolls.TryGetValue(owner,out var count)?count:0;
            public void SetScrolls(string owner,int count){if(count<0)throw new InvalidOperationException("Negative scroll count.");scrolls[owner]=count;}
            public void AddAwakeningNotice(AwakeningNotice n)=>AwakeningOutbox.Add(n.Clone());
            public string ExternalGrantOwner(string id)=>externalGrants.TryGetValue(id,out var owner)?owner:null;
            public void RecordExternalGrant(string id,string owner)=>externalGrants.Add(id,owner);
            public long Burned;
            public PetState Pet(string id)=>Pets.TryGetValue(id,out var p)?p:throw new InvalidOperationException("Pet missing.");
            public AccountState Account(string id)=>Accounts.TryGetValue(id,out var a)?a:throw new InvalidOperationException("Account missing.");
            public TradeOffer Offer(string id)=>Offers.TryGetValue(id,out var o)?o:throw new InvalidOperationException("Offer missing.");
            public void AddOffer(TradeOffer offer)=>Offers.Add(offer.OfferId,offer.Clone());
            public TradeReceipt FindTradeReceipt(string buyer,string key)=>trades.TryGetValue(buyer+"/"+key,out var r)?r:null;
            public void AddTradeReceipt(TradeReceipt r)=>trades.Add(r.BuyerId+"/"+r.RequestId,r.Clone());
            public ActionReceipt FindActionReceipt(string owner,string key)=>actions.TryGetValue(owner+"/"+key,out var r)?r:null;
            public void AddActionReceipt(ActionReceipt r)=>actions.Add(r.AccountId+"/"+r.RequestId,r.Clone());
            public void Burn(long amount){if(amount<0)throw new InvalidOperationException("Negative burn.");Burned=checked(Burned+amount);}
            public WorldSave Export()
            {
                return new WorldSave{Pets=Pets.Values.ToList(),Accounts=Accounts.Values.ToList(),Offers=Offers.Values.ToList(),
                    Trades=trades.Values.ToList(),Actions=actions.Values.ToList(),Assets=EconomyAssets.Values.ToList(),
                    EconomyOffers=EconomyOffers.Values.ToList(),Quotas=Quotas.Values.ToList(),Notices=AwakeningOutbox.ToList(),
                    Receipts=economyReceipts.Values.ToList(),Costumes=costumes.ToList(),
                    Scrolls=scrolls.Select(x=>new CountEntry{Owner=x.Key,Count=x.Value}).ToList(),
                    Grants=externalGrants.Select(x=>new GrantEntry{Id=x.Key,Owner=x.Value}).ToList(),Burned=Burned};
            }
            public static World Import(WorldSave s)
            {
                var w=new World{Burned=s.Burned};
                foreach(var x in s.Pets)w.Pets.Add(x.PetId,x);foreach(var x in s.Accounts)w.Accounts.Add(x.AccountId,x);
                foreach(var x in s.Offers)w.Offers.Add(x.OfferId,x);foreach(var x in s.Trades)w.AddTradeReceipt(x);
                foreach(var x in s.Actions)w.AddActionReceipt(x);foreach(var x in s.Assets)w.AddAsset(x);
                foreach(var x in s.EconomyOffers)w.AddEconomyOffer(x);foreach(var x in s.Quotas)w.Quotas.Add(x.HiddenType,x);
                foreach(var x in s.Notices)w.AddAwakeningNotice(x);foreach(var x in s.Receipts)w.AddEconomyReceipt(x);
                foreach(var x in s.Costumes)w.costumes.Add(x);foreach(var x in s.Scrolls)w.SetScrolls(x.Owner,x.Count);
                foreach(var x in s.Grants)w.RecordExternalGrant(x.Id,x.Owner);return w;
            }
            public World Clone()
            {
                var w=new World{Burned=Burned};
                foreach(var p in Pets)w.Pets.Add(p.Key,p.Value.Clone());
                foreach(var a in Accounts)w.Accounts.Add(a.Key,a.Value.Clone());
                foreach(var o in Offers)w.Offers.Add(o.Key,o.Value.Clone());
                foreach(var r in trades)w.trades.Add(r.Key,r.Value.Clone());
                foreach(var r in actions)w.actions.Add(r.Key,r.Value.Clone());
                foreach(var a in EconomyAssets)w.EconomyAssets.Add(a.Key,a.Value.Clone());
                foreach(var o in EconomyOffers)w.EconomyOffers.Add(o.Key,o.Value.Clone());
                foreach(var q in Quotas)w.Quotas.Add(q.Key,q.Value.Clone());
                foreach(var n in AwakeningOutbox)w.AwakeningOutbox.Add(n.Clone());
                foreach(var r in economyReceipts)w.economyReceipts.Add(r.Key,r.Value.Clone());
                foreach(var c in costumes)w.costumes.Add(c);
                foreach(var x in scrolls)w.scrolls.Add(x.Key,x.Value);
                foreach(var x in externalGrants)w.externalGrants.Add(x.Key,x.Value);
                return w;
            }
        }
        readonly object gate=new object();World world=new World();
        public void Seed(Action<World> seed){lock(gate){var copy=world.Clone();seed(copy);world=copy.Clone();}}
        public World Inspect(){lock(gate)return world.Clone();}
        public void Restore(WorldSave save){lock(gate)world=World.Import(save).Clone();}
        public T ExecuteSerializable<T>(Func<IWorldTransaction,T> work)
        {
            lock(gate){var next=world.Clone();T result=work(next);world=next;return result;}
        }
    }
}
