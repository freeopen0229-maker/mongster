using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Mongle.Slice;

namespace Mongle.VisualRenewal
{
    /// <summary>
    /// 통합 본체 리그를 교체하고 공유 머티리얼·코스튬·오라만 장착한다.
    /// 거래/과금/의상 소유권은 서버가 검증하며 이 컴포넌트는 확정된 결과를 표시한다.
    /// </summary>
    public sealed class PetVisualManager:MonoBehaviour
    {
        public PetVisualCatalog catalog;
        public string catalogResourcePath="PetVisuals/NeulguiCatalog";
        public Transform viewMount,motionRoot,turnRoot;
        public string sortingLayerName="Default";
        public int sortingOrder;
        [Range(0,2)]public int maxInactivePerKey=1;
        public GameObject CurrentView=>current?.root;
        public PetVisualDefinition CurrentDefinition=>current?.definition;
        struct Key:IEquatable<Key>
        {
            public PetVisualDefinition definition;public VisualQuality quality;public string costume;
            public bool Equals(Key b)=>definition==b.definition&&quality==b.quality&&costume==b.costume;
            public override bool Equals(object b)=>b is Key other&&Equals(other);
            public override int GetHashCode()=>definition.GetInstanceID()^((int)quality*397)^costume.GetHashCode();
        }
        sealed class Lease
        {
            public Key key;public GameObject root;public PetVisualDefinition definition;
            public SortingGroup sorting;public ParticleSystem[] particles;public PetSecondaryMotion[] secondary;
        }
        readonly Dictionary<Key,Stack<Lease>> pool=new Dictionary<Key,Stack<Lease>>();
        readonly Dictionary<string,Transform> sockets=new Dictionary<string,Transform>(StringComparer.Ordinal);
        Transform storage;Lease current;bool equipping;
        void EnsureStorage()
        {
            if(storage)return;var go=new GameObject("Whole Pet Pool (inactive)");
            go.transform.SetParent(transform,false);go.SetActive(false);storage=go.transform;
        }
        public bool TryEquip(int speciesId,GrowthStage stage,PetGrade grade,VisualQuality quality,out string error,string costumeId="")
        {try{Equip(speciesId,stage,grade,quality,costumeId);error=null;return true;}catch(Exception e){error=e.Message;return false;}}

        public void Equip(int speciesId,GrowthStage stage,PetGrade grade,VisualQuality quality=VisualQuality.Standard,string costumeId="")
        {
            if(!isActiveAndEnabled||equipping)throw new InvalidOperationException("VisualManager inactive/reentrant.");
            if(!Enum.IsDefined(typeof(VisualQuality),quality)||costumeId==null||costumeId.Length>80)
                throw new ArgumentException("Invalid quality/costume.");
            if(!catalog&&!string.IsNullOrWhiteSpace(catalogResourcePath))catalog=Resources.Load<PetVisualCatalog>(catalogResourcePath);
            if(!catalog)throw new InvalidOperationException("PetVisualCatalog missing.");
            var d=catalog.Resolve(speciesId,stage,grade);
            var key=new Key{definition=d,quality=quality,costume=costumeId};
            if(current!=null&&current.key.Equals(key))return;
            EnsureStorage();equipping=true;Lease next=null;
            try
            {
                next=Rent(key);next.root.transform.SetParent(viewMount?viewMount:transform,false);
                next.root.transform.localPosition=Vector3.zero;next.root.transform.localRotation=Quaternion.identity;
                next.sorting.sortingLayerName=sortingLayerName;next.sorting.sortingOrder=sortingOrder;
                foreach(var s in next.secondary)s.BindSources(motionRoot?motionRoot:transform,turnRoot?turnRoot:transform);
                next.root.SetActive(true);foreach(var p in next.particles){p.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);p.Play(false);}
                var old=current;current=next;next=null;Return(old);
            }
            finally{if(next!=null)Return(next);equipping=false;}
        }
        Lease Rent(Key key)
        {
            if(pool.TryGetValue(key,out var stack))while(stack.Count>0){var cached=stack.Pop();if(cached.root)return cached;}
            var d=key.definition;var root=Instantiate(d.rigPrefab,storage,false);root.SetActive(false);
            try
            {
                var binding=root.GetComponent<PetVisualBindings>();
                if(!binding)throw new InvalidOperationException("Whole-body rig binding missing.");
                binding.ValidateAndCollect(sockets);
                var material=d.bodyMaterials?.Resolve(key.quality);
                if(!material)throw new InvalidOperationException("Shared body material missing.");
                binding.bodyRenderer.sharedMaterial=material;
                if(key.costume!="")
                {
                    PetCostumeAttachment found=null;
                    foreach(var costume in d.costumes??Array.Empty<PetCostumeAttachment>())
                        if(costume!=null&&costume.costumeId==key.costume)
                        {if(found!=null)throw new InvalidOperationException("Duplicate costume definition.");found=costume;}
                    if(found==null||!found.prefab||!sockets.TryGetValue(found.socketId??"",out var anchor))
                        throw new InvalidOperationException("Costume is not supported by this pet/view.");
                    Instantiate(found.prefab,anchor,false);
                }
                foreach(var fx in d.effects??Array.Empty<PetSocketEffect>())
                {
                    if(fx==null||!fx.prefab||!Enum.IsDefined(typeof(VisualQuality),fx.minimumQuality)||
                       !sockets.TryGetValue(fx.socketId??"",out var anchor)||
                       (fx.simulationSpace!=ParticleSystemSimulationSpace.Local&&fx.simulationSpace!=ParticleSystemSimulationSpace.World))
                        throw new InvalidOperationException("Invalid aura/socket.");
                    if((int)key.quality<(int)fx.minimumQuality)continue;
                    var instance=Instantiate(fx.prefab,anchor,false);
                    foreach(var p in instance.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        var main=p.main;main.simulationSpace=fx.simulationSpace;main.useUnscaledTime=false;
                        var renderer=p.GetComponent<ParticleSystemRenderer>();
                        if(renderer){renderer.sortingLayerName=sortingLayerName;renderer.sortingOrder=fx.sortingOrder;}
                    }
                }
                var secondary=root.GetComponentsInChildren<PetSecondaryMotion>(true);var owners=new HashSet<Transform>();
                foreach(var s in secondary)
                {
                    s.ValidateSetup();
                    if(s.visualOffset && !owners.Add(s.visualOffset))throw new InvalidOperationException("Duplicate body offset owner.");
                    foreach(var j in s.joints)if(!owners.Add(j.bone))throw new InvalidOperationException("Duplicate spring bone owner.");
                    foreach(var b in s.breathBones)if(!owners.Add(b.bone))throw new InvalidOperationException("Duplicate breathing bone owner.");
                }
                var particles=root.GetComponentsInChildren<ParticleSystem>(true);
                foreach(var p in particles){var main=p.main;main.playOnAwake=false;main.stopAction=ParticleSystemStopAction.None;}
                var sorting=root.GetComponent<SortingGroup>();if(!sorting)sorting=root.AddComponent<SortingGroup>();
                return new Lease{key=key,definition=d,root=root,sorting=sorting,particles=particles,secondary=secondary};
            }
            catch{Destroy(root);throw;}
        }
        void Return(Lease lease)
        {
            if(lease==null||!lease.root)return;
            foreach(var p in lease.particles)if(p)p.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            lease.root.SetActive(false);lease.root.transform.SetParent(storage,false);
            if(!pool.TryGetValue(lease.key,out var stack))pool.Add(lease.key,stack=new Stack<Lease>());
            if(stack.Count<maxInactivePerKey)stack.Push(lease);else Destroy(lease.root);
        }
        public void SetSortingOrder(int order){sortingOrder=order;if(current!=null)current.sorting.sortingOrder=order;}
        public void Clear(){var old=current;current=null;Return(old);}
        void OnDisable(){Clear();}
        void OnDestroy(){if(current?.root)Destroy(current.root);foreach(var stack in pool.Values)foreach(var lease in stack)if(lease.root)Destroy(lease.root);pool.Clear();if(storage)Destroy(storage.gameObject);}
    }
}
