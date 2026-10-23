using System;
using FusionTamagotchi;
using Mongle.Slice;
namespace Mongle.Slice.Server
{
    // EconomyAsset is the ownership/revision authority. Lifecycle holds care/exploration fields once.
    public static class PetLifecycle
    {
        public static PetState Resolve(IWorldTransaction tx,string owner,string id,long now)
        {
            EconomyAsset asset=null;try{asset=tx.Asset(id);}catch(InvalidOperationException){}
            if(asset==null)return tx.Pet(id); // Existing V1 server/test-store records only.
            if(asset.OwnerId!=owner||asset.Kind!=AssetKind.Pet||asset.Pet==null)throw new InvalidOperationException("Pet not owned.");
            var source=asset.Pet;
            if(asset.Lifecycle==null)
                asset.Lifecycle=new PetState{PetId=id,OwnerAccountId=owner,LastUtc=now,SpeciesId=source.speciesId,
                    Level=source.level,Hunger=source.fullness,Affection=source.affection,Trade=source.trade.Clone()};
            var p=asset.Lifecycle;p.OwnerAccountId=owner;p.Revision=asset.Revision;p.IsListed=asset.IsListed;
            p.IsLocked=asset.Locked&&string.IsNullOrEmpty(p.ExplorationId);p.Trade=source.trade.Clone();
            p.Stage=source.isAdult?GrowthStage.Adult:GrowthStage.Baby;
            switch(source.rarity)
            {
                case PetRarity.Common:p.Grade=PetGrade.Common;break;
                case PetRarity.Rare:p.Grade=PetGrade.Rare;break;
                case PetRarity.Epic:p.Grade=PetGrade.Unique;break;
                case PetRarity.Legendary:p.Grade=PetGrade.Spirit;break;
                case PetRarity.Divine:p.Grade=PetGrade.Divine;break;
                default:throw new InvalidOperationException("Unknown pet rarity.");
            }
            p.Validate();return p;
        }
        public static void Commit(IWorldTransaction tx,PetState p)
        {
            EconomyAsset a=null;try{a=tx.Asset(p.PetId);}catch(InvalidOperationException){}
            if(a==null)return;
            a.Lifecycle=p;a.Revision=p.Revision;a.Pet.level=p.Level;a.Pet.fullness=(int)p.Hunger;a.Pet.affection=(int)p.Affection;
            a.Locked=p.IsBusy||p.IsLocked;
        }
    }
}
