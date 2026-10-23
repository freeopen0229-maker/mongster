using System;
using Mongle.Slice;

namespace Mongle.Slice.Server
{
    /// <summary>서버 전용. buyerId는 검증된 세션/토큰 claim에서 얻으며 request body에서 받지 않는다.</summary>
    public sealed class PetTradeService
    {
        readonly IWorldStore store;readonly IServerClock clock;readonly TradePolicy policy;
        public PetTradeService(IWorldStore store,IServerClock clock,TradePolicy policy)
        { this.store=store??throw new ArgumentNullException(nameof(store));this.clock=clock??throw new ArgumentNullException(nameof(clock));this.policy=policy??throw new ArgumentNullException(nameof(policy));policy.Validate(); }

        public TradeOffer CreateOffer(string authenticatedSeller,string petId,long price,long expectedRevision)
        {
            if(!PetIds.Valid(authenticatedSeller)||!PetIds.Valid(petId))throw new InvalidOperationException("Invalid seller/pet.");
            policy.Fee(price);
            return store.ExecuteSerializable(tx=>
            {
                var p=tx.Pet(petId);p.Validate();long now=clock.UtcSeconds;
                if(p.OwnerAccountId!=authenticatedSeller||p.Revision!=expectedRevision||p.IsListed||p.IsBusy||p.IsLocked||
                   now<p.LastUtc||!p.Trade.CanTrade(p.OwnerAccountId))throw new InvalidOperationException("Pet cannot be listed.");
                p.IsListed=true;p.Revision=checked(p.Revision+1);
                var offer=new TradeOffer{OfferId=Guid.NewGuid().ToString("N"),PetId=p.PetId,SellerId=authenticatedSeller,
                    Price=price,PetRevision=p.Revision,ExpiresUtc=checked(now+86400)};
                tx.AddOffer(offer);return offer.Clone();
            });
        }
        public void CancelOffer(string authenticatedSeller,string offerId)
        {
            if(!PetIds.Valid(authenticatedSeller)||!PetIds.Valid(offerId))throw new InvalidOperationException("Invalid cancel request.");
            store.ExecuteSerializable(tx=>
            {
                var offer=tx.Offer(offerId);var p=tx.Pet(offer.PetId);
                if(offer.SellerId!=authenticatedSeller||p.OwnerAccountId!=authenticatedSeller)
                    throw new InvalidOperationException("Not listing owner.");
                if(offer.Active){offer.Active=false;p.IsListed=false;p.Revision=checked(p.Revision+1);}return true;
            });
        }

        public TradeReceipt Buy(string authenticatedBuyerId,BuyPetRequest request)
        {
            if(!PetIds.Valid(authenticatedBuyerId)||request==null||!PetIds.Valid(request.OfferId)||!PetIds.Valid(request.RequestId))
                throw new InvalidOperationException("Invalid buy request.");
            return store.ExecuteSerializable(tx=>
            {
                var previous=tx.FindTradeReceipt(authenticatedBuyerId,request.RequestId);
                if(previous!=null)
                {
                    if(previous.OfferId!=request.OfferId)throw new InvalidOperationException("Idempotency key reused for another offer.");
                    return previous.Clone();
                }
                long now=clock.UtcSeconds;if(now<0)throw new InvalidOperationException("Invalid server clock.");
                var offer=tx.Offer(request.OfferId);
                if(!offer.Active||offer.ExpiresUtc<=now||offer.SellerId==authenticatedBuyerId)
                    throw new InvalidOperationException("Offer unavailable.");
                var pet=tx.Pet(offer.PetId);pet.Validate();
                if(pet.OwnerAccountId!=offer.SellerId||pet.Revision!=offer.PetRevision||!pet.IsListed||pet.IsBusy||pet.IsLocked||
                   !pet.Trade.CanTrade(pet.OwnerAccountId)||now<pet.LastUtc||
                   now-pet.Trade.LastTransferredUtc<policy.OwnershipCooldownSeconds)
                    throw new InvalidOperationException("Pet ownership/version/trade restriction failed.");
                var buyer=tx.Account(authenticatedBuyerId);var seller=tx.Account(offer.SellerId);
                ValidateWallet(buyer);ValidateWallet(seller);
                Limit(buyer,now);Limit(seller,now);
                long fee=policy.Fee(offer.Price);
                if(buyer.Coins<offer.Price)throw new InvalidOperationException("Insufficient coins.");

                // 이전 주인의 미수령 생활 보상은 판매자 계정에 정산한다. 구매자에게 이전하지 않는다.
                var idle=PetRules.ClaimIdle(pet,now);
                long sellerNext=checked(seller.Coins+offer.Price-fee+idle.Coins);
                if(sellerNext>TradePolicy.MaxWallet)throw new InvalidOperationException("Seller wallet capacity exceeded.");
                buyer.Coins-=offer.Price;seller.Coins=sellerNext;
                buyer.TradesToday++;seller.TradesToday++;
                pet.Trade.ConsumeTransfer(pet.OwnerAccountId,authenticatedBuyerId,now);
                pet.OwnerAccountId=authenticatedBuyerId;pet.IsListed=false;pet.CostumeId="";
                // 코스튬은 계정 인벤토리 소유. 판매자의 유료 의상을 펫과 함께 넘기지 않는다.
                pet.Revision=checked(pet.Revision+1);pet.Validate();offer.Active=false;tx.Burn(fee);
                var receipt=new TradeReceipt{BuyerId=authenticatedBuyerId,SellerId=seller.AccountId,
                    OfferId=offer.OfferId,PetId=pet.PetId,RequestId=request.RequestId,
                    Price=offer.Price,FeeBurned=fee,PetRevision=pet.Revision,ServerUtc=now};
                tx.AddTradeReceipt(receipt);return receipt.Clone();
            });
        }
        static void ValidateWallet(AccountState account)
        {
            if(account==null||!PetIds.Valid(account.AccountId)||account.Coins<0||account.Coins>TradePolicy.MaxWallet||account.TradesToday<0)
                throw new InvalidOperationException("Invalid account wallet.");
        }
        void Limit(AccountState account,long now)
        {
            long day=now/86400;
            if(account.TradeUtcDay>day)throw new InvalidOperationException("Server clock moved backwards.");
            if(account.TradeUtcDay!=day){account.TradeUtcDay=day;account.TradesToday=0;}
            if(account.TradesToday>=policy.MaxTradesPerAccountPerUtcDay)throw new InvalidOperationException("Daily trade limit.");
        }
    }
}
