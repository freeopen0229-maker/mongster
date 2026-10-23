// GENERATED from ServerSources/EconomyService.cs. Do not edit.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using FusionTamagotchi;
using Mongle.Slice;

namespace Mongle.Slice.Server
{
    /// <summary>
    /// 서버 전용 도메인 서비스. owner는 인증 계층의 claim 값이어야 한다.
    /// 클라이언트는 금액, 결과 스킬, 등급, 시리얼, 확률을 명령으로 지정할 수 없다.
    /// 모든 변경은 월드 transaction 안에서 실행하고 영수증과 함께 commit한다.
    /// </summary>
    public sealed class EconomyService
    {
        readonly IWorldStore store; readonly IServerClock clock; readonly IServerRandom rng;
        readonly EconomyPolicy policy;
        public EconomyService(IWorldStore store,IServerClock clock,IServerRandom rng,EconomyPolicy policy)
        {
            this.store=store??throw new ArgumentNullException(nameof(store));
            this.clock=clock??throw new ArgumentNullException(nameof(clock));
            this.rng=rng??throw new ArgumentNullException(nameof(rng));
            this.policy=(policy??throw new ArgumentNullException(nameof(policy))).Copy();this.policy.Validate();
        }
        static void Need(bool ok,string error){if(!ok)throw new InvalidOperationException(error);}
        static void Id(string id){Need(PetIds.Valid(id),"Invalid ID.");}
        long Now {get{long n=clock.UtcSeconds;Need(n>=0,"Invalid UTC.");return n;}}
        EconomyReceipt Command(string owner,string key,string fingerprint,Func<IWorldTransaction,EconomyReceipt> body)
        {
            Id(owner);Id(key);
            return store.ExecuteSerializable(tx=>
            {
                var old=tx.EconomyReceipt(owner,key);
                if(old!=null){Need(old.Fingerprint==fingerprint,"Request ID reused with different command.");return old.Clone();}
                var before=tx.Account(owner);before.Currency.Validate();
                var oldCurrency=before.Currency;long oldRevision=before.WalletRevision;
                var r=body(tx);
                var after=tx.Account(owner);
                if(after.WalletRevision==oldRevision&&(after.Currency.Jeon!=oldCurrency.Jeon||after.Currency.Gem!=oldCurrency.Gem||
                    after.Currency.MileageThread!=oldCurrency.MileageThread))after.WalletRevision=checked(oldRevision+1);tx.Account(owner).Currency.Validate();
                r.AccountId=owner;r.RequestId=key;r.Fingerprint=fingerprint;
                tx.AddEconomyReceipt(r);return r.Clone();
            });
        }
        static EconomyAsset Owned(IWorldTransaction tx,string owner,string id,long revision)
        {
            Id(id);var a=tx.Asset(id);
            Need(a.OwnerId==owner&&a.Revision==revision&&!a.IsListed&&!a.Locked,"Ownership/revision/lock failed.");return a;
        }
        static PetData Pet(EconomyAsset a)
        {
            Need(a.Kind==AssetKind.Pet&&a.Pet!=null,"Pet required.");var p=a.Pet;
            Need(p.schemaVersion==2&&p.petInstanceId==a.Id&&p.level>=1&&Enum.IsDefined(typeof(PetRarity),p.rarity)&&
                p.fullness>=0&&p.fullness<=100&&p.cleanliness>=0&&p.cleanliness<=100&&p.affection>=0&&p.affection<=100&&
                p.currentSkills.Count==p.GetMaxSkillSlots()&&p.slotRerollCounts.Count==p.GetMaxSkillSlots()&&
                p.currentSkills.Distinct().Count()==p.currentSkills.Count&&p.slotRerollCounts.All(x=>x>=0)&&
                p.rerollAttempts>=0&&p.slotRerollCounts.Sum()==p.rerollAttempts,"Invalid V2 pet.");
            p.trade.Validate(a.OwnerId);return p;
        }
        static void SpendJeon(AccountState a,long cost)
        {Need(cost>0&&a.Coins>=cost,"Insufficient Jeon.");a.Coins=checked(a.Coins-cost);}
        static void SpendGem(AccountState a,int cost)
        {Need(cost>0&&a.Currency.Gem>=cost,"Insufficient Gem.");a.Currency.Gem=checked(a.Currency.Gem-cost);}
        static void Bump(EconomyAsset a){a.Revision=checked(a.Revision+1);}
        public EconomyReceipt Care(string owner,string key,string asset,long revision,bool wash)
        {
            return Command(owner,key,"care/"+asset+"/"+revision+"/"+wash,tx=>
            {
                var a=Owned(tx,owner,asset,revision);var p=Pet(a);
                Need(wash?p.cleanliness<100:p.fullness<100,"Care meter already full.");
                long cost=wash?policy.WashJeon:policy.FoodJeon;SpendJeon(tx.Account(owner),cost);tx.Burn(cost);
                if(wash)p.cleanliness=Math.Min(100,p.cleanliness+30);else p.fullness=Math.Min(100,p.fullness+30);
                p.affection=Math.Min(100,p.affection+5);Bump(a);
                return new EconomyReceipt{ResultId=a.Id,JeonSpent=cost};
            });
        }
        public EconomyReceipt Reroll(string owner,string key,string asset,long revision,int slot)
        {
            return Command(owner,key,"reroll/"+asset+"/"+revision+"/"+slot,tx=>
            {
                var a=Owned(tx,owner,asset,revision);var p=Pet(a);
                Need(slot>=0&&slot<p.GetMaxSkillSlots(),"Invalid slot.");
                SpeciesRule species;Need(policy.Species.TryGetValue(p.speciesId,out species),"Missing skill catalog.");
                var choices=species.Skills.Where(x=>PetIds.Valid(x)&&!p.currentSkills.Contains(x)).Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray();
                Need(choices.Length>0,"No replacement skills available.");
                int nextCount=checked(p.slotRerollCounts[slot]+1),nextTotal=checked(p.rerollAttempts+1);
                long cost=policy.RerollCost(p.rerollAttempts);
                SpendJeon(tx.Account(owner),cost);string result=choices[rng.Next(choices.Length)];
                p.currentSkills[slot]=result;p.slotRerollCounts[slot]=nextCount;p.rerollAttempts=nextTotal;Bump(a);tx.Burn(cost);
                return new EconomyReceipt{ResultId=a.Id,JeonSpent=cost,Rewards=new[]{result}};
            });
        }
        void FillSkills(PetData p)
        {
            SpeciesRule s;Need(policy.Species.TryGetValue(p.speciesId,out s),"Missing species.");
            var choices=s.Skills.Where(PetIds.Valid).Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToList();
            Need(choices.Count>=p.GetMaxSkillSlots(),"Insufficient unique skills in species catalog.");
            while(p.currentSkills.Count<p.GetMaxSkillSlots())
            {
                var available=choices.Where(x=>!p.currentSkills.Contains(x)).ToList();
                p.currentSkills.Add(available[rng.Next(available.Count)]);p.slotRerollCounts.Add(0);
            }
        }
        public EconomyReceipt GrowAdult(string owner,string key,string asset,long revision)
        {
            return Command(owner,key,"adult/"+asset+"/"+revision,tx=>
            {
                var a=Owned(tx,owner,asset,revision);var p=Pet(a);
                Need(!p.isAdult&&!p.awakeningResolved,"Adult roll already resolved.");
                Need(policy.AdultMinLevel>0&&p.level>=policy.AdultMinLevel,"Adult growth requirement not configured or not met.");
                if(p.isHiddenPet)
                {
                    Need(p.hiddenType>=0&&p.hiddenType<=4&&(p.rarity==PetRarity.Common||p.rarity==PetRarity.Rare),"Invalid hidden eligibility.");
                    // 한정 잔여량/추첨/시리얼/성체화/outbox를 같은 transaction에서 확정.
                    var q=tx.Quota(p.hiddenType);
                    Need(q.HiddenType==p.hiddenType&&q.Limit>0&&q.Issued>=0&&q.Issued<=q.Limit&&
                        (p.hiddenType==0?q.Limit==10:q.Limit==100),"Hidden quota not configured correctly.");
                    int denominator=p.hiddenType==0?1000000000:2000000;
                    if(q.Issued<q.Limit&&rng.Next(denominator)==0)
                    {
                        p.rarity=p.hiddenType==0?PetRarity.Divine:PetRarity.Legendary;
                        FillSkills(p);p.serialNumber=++q.Issued;
                        tx.AddAwakeningNotice(new AwakeningNotice{EventId=Guid.NewGuid().ToString("N"),AssetId=a.Id,
                            OwnerId=owner,HiddenType=p.hiddenType,Serial=p.serialNumber,Limit=q.Limit});
                    }
                    // 실패/매진은 원래 Common/Rare 고유 성체를 유지. 잠재 히든이라는 이유로 등급을 강등하지 않는다.
                }
                p.isAdult=true;p.awakeningResolved=true;Bump(a);
                return new EconomyReceipt{ResultId=a.Id,Rewards=new[]{p.rarity.ToString(),p.serialNumber.ToString()}};
            });
        }
        public EconomyReceipt StartIncubation(string owner,string key,string asset,long revision)
        {
            return Command(owner,key,"incubate/"+asset+"/"+revision,tx=>
            {
                var a=Owned(tx,owner,asset,revision);
                Need(a.Kind==AssetKind.Egg&&a.Egg!=null&&!a.Egg.incubating,"Fresh egg required.");
                Need(a.Egg.incubationSeconds>=28800&&a.Egg.incubationSeconds<=43200,"Incubation must be 8~12 hours.");
                a.Egg.incubating=true;a.Egg.hatchAtUtc=checked(Now+a.Egg.incubationSeconds);Bump(a);
                return new EconomyReceipt{ResultId=a.Id};
            });
        }
        public EconomyReceipt UseHatchScroll(string owner,string key,string asset,long revision)
        {
            return Command(owner,key,"scroll/"+asset+"/"+revision,tx=>
            {
                var a=Owned(tx,owner,asset,revision);long now=Now;
                Need(a.Kind==AssetKind.Egg&&a.Egg!=null&&a.Egg.incubating&&a.Egg.hatchAtUtc>now,"Egg is not waiting.");
                int count=tx.Scrolls(owner);Need(count>0,"No scroll.");
                tx.SetScrolls(owner,count-1);a.Egg.hatchAtUtc=now;Bump(a);
                return new EconomyReceipt{ResultId=a.Id};
            });
        }
        public EconomyReceipt Hatch(string owner,string key,string asset,long revision)
        {
            return Command(owner,key,"hatch/"+asset+"/"+revision,tx=>
            {
                var a=Owned(tx,owner,asset,revision);
                Need(a.Kind==AssetKind.Egg&&a.Egg!=null&&a.Egg.incubating&&a.Egg.hatchAtUtc<=Now,"Egg not ready.");
                PetRarity rarity;bool normal=a.Egg.kind==EggKind.Normal;
                if(normal)
                {
                    int total=checked(policy.NormalCommonWeight+policy.NormalRareWeight);Need(total>0,"Normal egg odds not configured.");
                    rarity=rng.Next(total)<policy.NormalCommonWeight?PetRarity.Common:PetRarity.Rare;
                }
                else {Need(a.Egg.kind==EggKind.Luminous,"Unknown egg kind.");rarity=LuminousEggOdds.Resolve(rng.Next(LuminousEggOdds.Total));}
                var species=policy.Species.Values.Where(x=>x.Rarity==rarity&&(normal||x.HiddenType<0)).OrderBy(x=>x.Id).ToArray();
                Need(species.Length>0,"No species configured for egg rarity.");var s=species[rng.Next(species.Length)];
                var p=new PetData{petInstanceId=a.Id,petName=s.Name,speciesId=s.Id,rarity=rarity,
                    isHiddenPet=normal&&s.HiddenType>=0,hiddenType=normal?s.HiddenType:-1,trade=PetTradeData.Create(policy.MaxPetTransfers)};
                FillSkills(p);a.Kind=AssetKind.Pet;a.Pet=p;a.Egg=null;Bump(a);
                return new EconomyReceipt{ResultId=a.Id,Rewards=new[]{p.rarity.ToString()}};
            });
        }
        EconomyAsset NewEgg(string owner,EggKind kind)
        {return new EconomyAsset{Id=Guid.NewGuid().ToString("N"),OwnerId=owner,Kind=AssetKind.Egg,Egg=new EggData{kind=kind}};}
        public EconomyReceipt BuyShop(string owner,string key,ShopProduct product)
        {
            return Command(owner,key,"shop/"+product,tx=>
            {
                int cost,count;bool eggs=false,draw=false;
                switch(product)
                {
                    case ShopProduct.HatchScroll:cost=11;count=1;break;
                    case ShopProduct.HatchScrollBundle:cost=100;count=11;break;
                    case ShopProduct.CostumeDraw:cost=15;count=1;draw=true;break;
                    case ShopProduct.CostumeDrawTen:cost=135;count=10;draw=true;break;
                    case ShopProduct.LuminousEgg:cost=30;count=1;eggs=true;break;
                    case ShopProduct.LuminousEggBundle:cost=300;count=11;eggs=true;break;
                    default:throw new InvalidOperationException("Unknown product.");
                }
                var account=tx.Account(owner);SpendGem(account,cost);var rewards=new List<string>();
                if(eggs)for(int i=0;i<count;i++){var e=NewEgg(owner,EggKind.Luminous);tx.AddAsset(e);rewards.Add(e.Id);}
                else if(draw)
                {
                    Need(policy.SeasonCostumes.Count>0,"Costume table not configured.");
                    Need(policy.SeasonCostumes.Select(x=>x.Id).Distinct().Count()==policy.SeasonCostumes.Count,"Duplicate costume catalog ID.");
                    int total=0;foreach(var c in policy.SeasonCostumes)
                    {Id(c.Id);Need(c.Weight>0&&c.DuplicateThread>0,"Invalid costume weight/thread.");total=checked(total+c.Weight);}
                    for(int i=0;i<count;i++)
                    {
                        int roll=rng.Next(total);WeightedCostume chosen=null;
                        foreach(var c in policy.SeasonCostumes){if(roll<c.Weight){chosen=c;break;}roll-=c.Weight;}
                        if(tx.HasCostume(owner,chosen.Id))account.Currency.MileageThread=checked(account.Currency.MileageThread+chosen.DuplicateThread);
                        else tx.AddCostume(owner,chosen.Id);
                        rewards.Add(chosen.Id);
                    }
                }
                else tx.SetScrolls(owner,checked(tx.Scrolls(owner)+count));
                return new EconomyReceipt{GemSpent=cost,Rewards=rewards.ToArray()};
            });
        }
        public EconomyReceipt BuyArchiveCostume(string owner,string key,string costume)
        {
            Id(costume);
            return Command(owner,key,"archive/"+costume,tx=>
            {
                ArchiveCostume c;Need(policy.Archive.TryGetValue(costume,out c)&&c.Id==costume&&c.Season>=1&&
                    c.Season<policy.CurrentSeason&&c.ThreadCost>0,"Only configured past-season costumes are sold here.");
                var a=tx.Account(owner);Need(!tx.HasCostume(owner,costume)&&a.Currency.MileageThread>=c.ThreadCost,"Owned or insufficient thread.");
                a.Currency.MileageThread-=c.ThreadCost;tx.AddCostume(owner,costume);
                return new EconomyReceipt{ThreadSpent=c.ThreadCost,Rewards=new[]{costume}};
            });
        }
        public EconomyReceipt CreditVerifiedPurchase(string owner,string key,string token,IGemPurchaseVerifier verifier)
        {
            Id(owner);var p=verifier.Verify(owner,token);
            Need(p!=null&&p.AccountId==owner&&PetIds.Valid(p.PurchaseId)&&p.Gems>0,"Invalid verified purchase.");
            return Command(owner,key,"payment/"+p.PurchaseId,tx=>
            {
                string grant="payment/"+p.PurchaseId;Need(tx.ExternalGrantOwner(grant)==null,"Purchase already credited.");
                var a=tx.Account(owner);a.Currency.Gem=checked(a.Currency.Gem+p.Gems);tx.RecordExternalGrant(grant,owner);
                return new EconomyReceipt{ResultId=p.PurchaseId,Rewards=new[]{"Gem:"+p.Gems}};
            });
        }
        public EconomyReceipt SettleVerifiedRaid(string owner,string key,string token,IRaidRewardVerifier verifier)
        {
            Id(owner);var r=verifier.Verify(owner,token);
            Need(r!=null&&r.AccountId==owner&&PetIds.Valid(r.RewardId)&&r.Jeon>=0&&r.Jeon<=1000000&&r.NormalEggs>=0&&r.NormalEggs<=10,"Invalid verified raid.");
            return Command(owner,key,"raid/"+r.RewardId,tx=>
            {
                Need(policy.RaidGemPartsPerMillion>0,"Raid gem odds not configured.");string grant="raid/"+r.RewardId;
                Need(tx.ExternalGrantOwner(grant)==null,"Raid already settled.");
                var a=tx.Account(owner);a.Coins=checked(a.Coins+r.Jeon);var rewards=new List<string>();
                if(rng.Next(1000000)<policy.RaidGemPartsPerMillion){a.Currency.Gem=checked(a.Currency.Gem+1);rewards.Add("Gem:1");}
                for(int i=0;i<r.NormalEggs;i++){var e=NewEgg(owner,EggKind.Normal);tx.AddAsset(e);rewards.Add(e.Id);}
                tx.RecordExternalGrant(grant,owner);return new EconomyReceipt{ResultId=r.RewardId,Rewards=rewards.ToArray()};
            });
        }
        public EconomyReceipt List(string owner,string key,string asset,long revision,long price)
        {
            Need(price>=1&&price<=TradePolicy.MaxPrice,"Invalid price.");
            return Command(owner,key,"list/"+asset+"/"+revision+"/"+price,tx=>
            {
                var a=Owned(tx,owner,asset,revision);
                if(a.Kind==AssetKind.Pet){var p=Pet(a);Need(p.trade.CanTrade(owner),"Soulbound pet.");}
                else if(a.Kind==AssetKind.Egg)Need(a.Egg!=null&&!a.Egg.incubating,"Incubating egg cannot be listed.");
                else Need(a.Kind==AssetKind.Material&&a.Quantity>0&&PetIds.Valid(a.MaterialId),"Invalid tradable material.");
                a.IsListed=true;Bump(a);var o=new EconomyListing{Id=Guid.NewGuid().ToString("N"),AssetId=a.Id,SellerId=owner,
                    Price=price,AssetRevision=a.Revision,ExpiresUtc=checked(Now+86400)};tx.AddEconomyOffer(o);
                return new EconomyReceipt{ResultId=o.Id};
            });
        }
        public EconomyReceipt Cancel(string owner,string key,string offer)
        {
            Id(offer);return Command(owner,key,"cancel/"+offer,tx=>
            {
                var o=tx.EconomyOffer(offer);var a=tx.Asset(o.AssetId);
                Need(o.SellerId==owner&&a.OwnerId==owner,"Not listing owner.");
                if(o.Active){Need(a.IsListed&&a.Revision==o.AssetRevision,"Stale listing.");o.Active=false;a.IsListed=false;Bump(a);}
                return new EconomyReceipt{ResultId=o.Id};
            });
        }
        static void DailyLimit(AccountState a,long now)
        {
            long day=now/86400;Need(a.TradeUtcDay<=day,"UTC went backwards.");
            if(a.TradeUtcDay!=day){a.TradeUtcDay=day;a.TradesToday=0;}
            Need(a.TradesToday<10,"Daily market limit.");a.TradesToday++;
        }
        public EconomyReceipt BuyListing(string buyer,string key,string offer)
        {
            Id(offer);return Command(buyer,key,"market-buy/"+offer,tx=>
            {
                var o=tx.EconomyOffer(offer);var a=tx.Asset(o.AssetId);long now=Now;
                Need(o.Active&&o.SellerId!=buyer&&o.ExpiresUtc>now&&a.OwnerId==o.SellerId&&a.IsListed&&!a.Locked&&a.Revision==o.AssetRevision,"Offer unavailable.");
                var b=tx.Account(buyer);var s=tx.Account(o.SellerId);s.Currency.Validate();DailyLimit(b,now);DailyLimit(s,now);
                if(a.Kind==AssetKind.Pet)
                {
                    var p=Pet(a);Need(p.trade.CanTrade(a.OwnerId)&&now>=p.trade.LastTransferredUtc&&now-p.trade.LastTransferredUtc>=3600,"Trade cooldown/binding failed.");
                    p.trade.ConsumeTransfer(a.OwnerId,buyer,now);
                }
                Need(o.Price>=1&&o.Price<=TradePolicy.MaxPrice,"Invalid stored price.");
                long fee=checked((o.Price*policy.MarketFeeBasisPoints+9999)/10000);
                SpendJeon(b,o.Price);s.Coins=checked(s.Coins+o.Price-fee);s.Currency.Validate();
                a.OwnerId=buyer;a.IsListed=false;Bump(a);o.Active=false;tx.Burn(fee);
                return new EconomyReceipt{ResultId=a.Id,JeonSpent=o.Price,FeeBurned=fee};
            });
        }
    }
}

#endif
