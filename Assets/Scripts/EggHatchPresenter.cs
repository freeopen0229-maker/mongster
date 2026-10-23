using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FusionTamagotchi.Client
{
    /// <summary>구매 성공은 밀봉 알 지급, Hatch 성공만 깨짐/사운드/펫 결과를 연출한다.</summary>
    public sealed class EggHatchPresenter:MonoBehaviour
    {
        public ClientGameManager manager;
        public Animator eggAnimator;
        public AudioSource audioSource;
        public AudioClip hatchSound;
        public GameObject resultPanel;
        public Text resultText,purchaseText;
        public HatchResultStyling gradeStyling;
        readonly Queue<PetDTO> queue=new Queue<PetDTO>();
        readonly HashSet<string> shown=new HashSet<string>();
        Coroutine playback;
        AudioClip generatedSound;
        void Awake()
        {
            if(eggAnimator)eggAnimator.updateMode=AnimatorUpdateMode.UnscaledTime;
            // 테스트용 짧은 chime. 상용 RPG 사운드 자산을 대체하는 품질이라고 주장하지 않는다.
            if(!hatchSound&&audioSource)
            {
                const int rate=24000;var samples=new float[(int)(rate*.24f)];
                for(int i=0;i<samples.Length;i++)
                {
                    float t=(float)i/rate,envelope=Mathf.Sin(Mathf.PI*i/(samples.Length-1f))*Mathf.Exp(-t*12);
                    samples[i]=.15f*envelope*(Mathf.Sin(2*Mathf.PI*523.25f*t)+.5f*Mathf.Sin(2*Mathf.PI*659.25f*t)+.3f*Mathf.Sin(2*Mathf.PI*783.99f*t));
                }
                generatedSound=AudioClip.Create("Local hatch chime",samples.Length,1,rate,false);generatedSound.SetData(samples,0);hatchSound=generatedSound;
            }
        }
        void OnEnable(){if(manager){manager.EggsPurchased+=Purchased;manager.PetHatched+=Hatched;}}
        void OnDisable()
        {
            if(manager){manager.EggsPurchased-=Purchased;manager.PetHatched-=Hatched;}
            if(playback!=null)StopCoroutine(playback);playback=null;queue.Clear();
        }
        void OnDestroy(){if(generatedSound)Destroy(generatedSound);}
        void Purchased(string[] ids){if(eggAnimator&&eggAnimator.HasState(0,Animator.StringToHash("Closed")))eggAnimator.Play("Closed",0,0);if(resultPanel)resultPanel.SetActive(false);if(purchaseText)purchaseText.text=$"영롱한 알 {ids.Length}개 지급 완료 · 부화 대기";}
        void Hatched(PetDTO p)
        {
            if(p==null||!shown.Add(p.petId))return;queue.Enqueue(p.Clone());
            if(playback==null)playback=StartCoroutine(PlayQueue());
        }
        IEnumerator PlayQueue()
        {
            while(queue.Count>0)
            {
                var p=queue.Dequeue();if(resultPanel)resultPanel.SetActive(false);
                if(eggAnimator&&eggAnimator.HasState(0,Animator.StringToHash("Crack")))eggAnimator.Play("Crack",0,0);
                yield return new WaitForSecondsRealtime(.28f);
                if(audioSource&&hatchSound)audioSource.PlayOneShot(hatchSound);
                yield return new WaitForSecondsRealtime(.45f);
                if(eggAnimator&&eggAnimator.HasState(0,Animator.StringToHash("Reveal")))eggAnimator.Play("Reveal",0,0);
                yield return new WaitForSecondsRealtime(.2f);
                if(resultText)
                {
                    resultText.supportRichText=false;
                    resultText.text=$"부화 완료\n{p.petName}\n{GradeName(p.rarity)}";
                }
                if(gradeStyling)gradeStyling.Apply(p);
                if(resultPanel)resultPanel.SetActive(true);
                yield return new WaitForSecondsRealtime(1.5f);
            }
            playback=null;
        }
        static string GradeName(string rarity)
        {
            switch(rarity){case "Common":return "일반";case "Rare":return "희귀";case "Epic":return "영웅";case "Legendary":return "전설";case "Divine":return "신수";default:return rarity;}
        }
    }
}
