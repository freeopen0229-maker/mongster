using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FusionTamagotchi.Client
{
    [Serializable] public sealed class UserWalletDTO
    {
        public long jeon, revision;
        public int gem, mileageThread;
        public UserWalletDTO Clone()=>(UserWalletDTO)MemberwiseClone();
        public void Validate(){if(jeon<0||jeon>1000000000000L||gem<0||mileageThread<0||revision<0)throw new InvalidOperationException("Invalid wallet response.");}
    }
    [Serializable] public sealed class PetDTO
    {
        public string petId,petName,rarity;
        public long revision;
        public bool isAdult,isHiddenPet; // true는 '각성 완료'만 의미. 잠재 후보를 공개하지 않는다.
        public int hiddenType=-1,serialNumber,hiddenLimit,fullness,cleanliness;
        public long nextRerollCost;
        public List<string> skills=new List<string>();
        public PetDTO Clone(){var p=(PetDTO)MemberwiseClone();p.skills=new List<string>(skills);return p;}
        public void Validate()
        {
            PetRarity r;
            if(!Mongle.Slice.PetIds.Valid(petId)||revision<0||!Enum.TryParse(rarity,out r)||!Enum.IsDefined(typeof(PetRarity),r)||
                rarity!=r.ToString()||skills==null||fullness<0||fullness>100||cleanliness<0||cleanliness>100||nextRerollCost<1||
                (isHiddenPet?(!isAdult||!HatchPresentationPolicy.ValidHiddenIdentity(hiddenType,rarity,serialNumber,hiddenLimit)):
                              (hiddenType!=-1||serialNumber!=0||hiddenLimit!=0)))
                throw new InvalidOperationException("Invalid pet response.");
            int count=r==PetRarity.Divine?4:r==PetRarity.Epic||r==PetRarity.Legendary?3:2;
            var ids=new HashSet<string>();
            if(skills.Count!=count)throw new InvalidOperationException("Invalid skill count.");
            foreach(var id in skills)if(!Mongle.Slice.PetIds.Valid(id)||!ids.Add(id))throw new InvalidOperationException("Invalid skill IDs.");
        }
    }
    [Serializable] public sealed class ClientSnapshotDTO
    {
        public string accountId;
        public UserWalletDTO wallet;
        public PetDTO pet;
        public EggDTO egg;
    }
    [Serializable] public sealed class EggDTO
    {
        public string assetId;public long revision,hatchAtUtc;public bool incubating;
        public EggDTO Clone()=>(EggDTO)MemberwiseClone();
        public void Validate(){if(!Mongle.Slice.PetIds.Valid(assetId)||revision<0||hatchAtUtc<0)throw new InvalidOperationException("Invalid egg response.");}
    }
    public enum ClientCommandKind { BuyLuminousEgg, RerollSkill, StartIncubation, UseHatchScroll, Hatch }
    [Serializable] public sealed class ClientCommandDTO
    {
        public string requestId,petId;
        public ClientCommandKind kind;
        public bool tenPack;
        public long expectedRevision;
        public int slotIndex;
        public ClientCommandDTO Clone()=>(ClientCommandDTO)MemberwiseClone();
    }
    [Serializable] public sealed class ClientCommandReplyDTO
    {
        public string requestId;
        public ClientSnapshotDTO snapshot;
        public string[] receivedAssetIds=Array.Empty<string>();
    }
    [Serializable] public sealed class HiddenAwakeningNoticeDTO
    {
        public string eventId,trainerName,petName,rarity;
        public int hiddenType,serialNo,maxLimit;
    }
    public interface IClientEconomyGateway
    {
        Task<ClientSnapshotDTO> ReadAsync(string selectedPetId,CancellationToken token);
        Task<ClientCommandReplyDTO> ExecuteAsync(ClientCommandDTO command,CancellationToken token);
    }
    // 서버가 미처리/rollback을 확정한 400/409 등의 응답. timeout/5xx는 이 예외로 바꾸지 않는다.
    public sealed class EconomyRequestRejectedException:Exception
    { public EconomyRequestRejectedException(string message):base(message){} }
}
