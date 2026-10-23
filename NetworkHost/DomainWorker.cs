using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using FusionTamagotchi;
using FusionTamagotchi.Client;
using Mongle.Slice;
using Mongle.Slice.Server;
using Mongle.Slice.Server.Testing;

// HTTP 호스트가 로컬 stdin으로 호출한다. 이 프로세스 자체는 네트워크에 노출하지 않는다.
public static class DomainWorker
{
    public sealed class Input
    {
        public string action,accountId,selectedId;
        public ClientCommandDTO command;
        public InMemoryWorldStore.WorldSave world;
    }
    public sealed class Output
    {
        public int status;
        public object body;
        public InMemoryWorldStore.WorldSave world;
    }
    // DataContractJsonSerializer의 object polymorphism 대신 구체적인 output type을 사용한다.
    public sealed class WireOutput
    {
        public int status;
        public string bodyJson,error;
        public InMemoryWorldStore.WorldSave world;
    }
    static T Parse<T>(string value)
    {using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(value)))return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);}
    static string Json<T>(T value)
    {using(var stream=new MemoryStream()){new DataContractJsonSerializer(typeof(T)).WriteObject(stream,value);return Encoding.UTF8.GetString(stream.ToArray());}}
    static EconomyPolicy ReferencePolicy()
    {
        // 테스트/로컬 연동 전용 카탈로그. 실제 126종 및 운영 확률 확정값이 아니다.
        var p=new EconomyPolicy{CurrentSeason=2,AdultMinLevel=10,NormalCommonWeight=90,NormalRareWeight=10,RaidGemPartsPerMillion=1000};
        for(int i=0;i<5;i++)p.Species.Add(i,new SpeciesRule{Id=i,Name="Local species "+i,Rarity=(PetRarity)i,Skills=new[]{"s1","s2","s3","s4","s5","s6"}});
        return p;
    }
    static InMemoryWorldStore Fresh()
    {
        var s=new InMemoryWorldStore();s.Seed(w=>
        {
            w.Accounts.Add("local-a",new AccountState{AccountId="local-a",Currency=new UserCurrency(100000,1000)});
            w.Accounts.Add("local-b",new AccountState{AccountId="local-b",Currency=new UserCurrency(100000,1000)});
            foreach(var q in HiddenQuotaPolicy.CreateFresh())w.Quotas.Add(q.HiddenType,q);
            w.SetScrolls("local-a",2);
            w.AddAsset(new EconomyAsset{Id="local-neulgui",OwnerId="local-a",Kind=AssetKind.Pet,Pet=new PetData{petInstanceId="local-neulgui",
                petName="Neulgui",speciesId=0,currentSkills=new List<string>{"s1","s2"},slotRerollCounts=new List<int>{0,0}}});
        });return s;
    }
    static WireOutput Handle(Input input)
    {
        var store=new InMemoryWorldStore();
        if(input.action=="init")return new WireOutput{status=200,bodyJson="{}",world=Fresh().Inspect().Export()};
        if(input.world==null)throw new InvalidOperationException("World is missing.");store.Restore(input.world);
        var policy=ReferencePolicy();
        using(var random=new CryptoServerRandom())
        {
            var service=new EconomyService(store,new SystemServerClock(),random,policy);
            var facade=new ClientEconomyFacade(input.accountId,store,service,policy);
            string body;
            if(input.action=="read")body=Json(facade.ReadAsync(input.selectedId,CancellationToken.None).GetAwaiter().GetResult());
            else if(input.action=="command")body=Json(facade.ExecuteAsync(input.command,CancellationToken.None).GetAwaiter().GetResult());
            else throw new InvalidOperationException("Unsupported operation.");
            return new WireOutput{status=200,bodyJson=body,world=store.Inspect().Export()};
        }
    }
    public static void Main()
    {
        string line;
        while((line=Console.ReadLine())!=null)
        {
            WireOutput result;
            try{result=Handle(Parse<Input>(line));}
            catch(EconomyRequestRejectedException e){result=new WireOutput{status=409,error=e.Message};}
            catch(Exception e){Console.Error.WriteLine(e.GetType().Name+": "+e.Message);result=new WireOutput{status=500,error="Domain operation failed; transaction was not persisted."};}
            Console.WriteLine(Json(result));Console.Out.Flush();
        }
    }
}
