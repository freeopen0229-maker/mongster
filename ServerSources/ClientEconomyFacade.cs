using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FusionTamagotchi;
using FusionTamagotchi.Client;
using Mongle.Slice;

namespace Mongle.Slice.Server
{
    /// <summary>
    /// HTTP 호스트가 인증된 계정으로 생성하는 연결 계층. 실제 HTTP 호스트는 별도 구현 필요.
    /// 로컬 테스트에서는 직접 호출 가능. client command에 owner/가격/결과 필드는 없다.
    /// </summary>
    public sealed class ClientEconomyFacade:IClientEconomyGateway
    {
        readonly string owner;readonly IWorldStore store;readonly EconomyService economy;readonly EconomyPolicy policy;
        public ClientEconomyFacade(string authenticatedOwner,IWorldStore store,EconomyService economy,EconomyPolicy policy)
        {if(!PetIds.Valid(authenticatedOwner))throw new ArgumentException("Invalid account.");owner=authenticatedOwner;this.store=store;this.economy=economy;this.policy=policy.Copy();}
        ClientSnapshotDTO Snapshot(string selected)
        {
            return store.ExecuteSerializable(tx=>
            {
                var a=tx.Account(owner);a.Currency.Validate();
                var s=new ClientSnapshotDTO{accountId=owner,wallet=new UserWalletDTO{jeon=a.Currency.Jeon,gem=a.Currency.Gem,
                    mileageThread=a.Currency.MileageThread,revision=a.WalletRevision}};
                if(!string.IsNullOrEmpty(selected))
                {
                    EconomyAsset asset=null;
                    try{asset=tx.Asset(selected);}catch(InvalidOperationException){/* 선택 펫이 삭제/이전됐으면 선택 해제 */}
                    if(asset!=null&&asset.OwnerId==owner&&asset.Kind==AssetKind.Pet)
                    {
                        var p=asset.Pet;bool awakened=p.isAdult&&p.serialNumber>0;
                        s.pet=new PetDTO{petId=p.petInstanceId,petName=p.petName,revision=asset.Revision,rarity=p.rarity.ToString(),
                            isAdult=p.isAdult,isHiddenPet=awakened,hiddenType=awakened?p.hiddenType:-1,serialNumber=awakened?p.serialNumber:0,
                            hiddenLimit=awakened?tx.Quota(p.hiddenType).Limit:0,fullness=p.fullness,cleanliness=p.cleanliness,
                            skills=new List<string>(p.currentSkills),nextRerollCost=policy.RerollCost(p.rerollAttempts)};
                    }
                }
                if(!string.IsNullOrEmpty(selected)&&s.pet==null)
                {
                    EconomyAsset selectedAsset=null;try{selectedAsset=tx.Asset(selected);}catch(InvalidOperationException){}
                    if(selectedAsset!=null&&selectedAsset.OwnerId==owner&&selectedAsset.Kind==AssetKind.Egg)
                        s.egg=new EggDTO{assetId=selectedAsset.Id,revision=selectedAsset.Revision,incubating=selectedAsset.Egg.incubating,hatchAtUtc=selectedAsset.Egg.hatchAtUtc};
                }
                return s;
            });
        }
        public Task<ClientSnapshotDTO> ReadAsync(string selectedPetId,CancellationToken token)
        {
            try{token.ThrowIfCancellationRequested();return Task.FromResult(Snapshot(selectedPetId));}
            catch(Exception e){return Task.FromException<ClientSnapshotDTO>(e);}
        }
        public Task<ClientCommandReplyDTO> ExecuteAsync(ClientCommandDTO c,CancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();if(c==null)throw new EconomyRequestRejectedException("Empty request.");
                EconomyReceipt r;
                try
                {
                    switch(c.kind)
                    {
                        case ClientCommandKind.BuyLuminousEgg:r=economy.BuyShop(owner,c.requestId,c.tenPack?ShopProduct.LuminousEggBundle:ShopProduct.LuminousEgg);break;
                        case ClientCommandKind.RerollSkill:r=economy.Reroll(owner,c.requestId,c.petId,c.expectedRevision,c.slotIndex);break;
                        case ClientCommandKind.StartIncubation:r=economy.StartIncubation(owner,c.requestId,c.petId,c.expectedRevision);break;
                        case ClientCommandKind.UseHatchScroll:r=economy.UseHatchScroll(owner,c.requestId,c.petId,c.expectedRevision);break;
                        case ClientCommandKind.Hatch:r=economy.Hatch(owner,c.requestId,c.petId,c.expectedRevision);break;
                        default:throw new InvalidOperationException("Unsupported command.");
                    }
                }
                catch(InvalidOperationException e){throw new EconomyRequestRejectedException(e.Message);}
                // command commit 이후 snapshot 실패는 '결제 거절'로 바꾸지 않는다. 같은 ID로 재확인해야 한다.
                return Task.FromResult(new ClientCommandReplyDTO{requestId=c.requestId,snapshot=Snapshot(c.petId),
                    receivedAssetIds=c.kind==ClientCommandKind.BuyLuminousEgg?(string[])r.Rewards.Clone():Array.Empty<string>()});
            }
            catch(Exception e){return Task.FromException<ClientCommandReplyDTO>(e);}
        }
    }
    public static class HiddenQuotaPolicy
    {
        public static LimitedQuota[] CreateFresh()
        {
            var q=new LimitedQuota[5];
            for(int i=0;i<5;i++)q[i]=new LimitedQuota{HiddenType=i,Limit=i==0?10:100,Issued=0};
            return q; // 신규 월드 전용. 기존 발급량을 0으로 덮어쓰는 데 사용하지 않는다.
        }
    }
}
