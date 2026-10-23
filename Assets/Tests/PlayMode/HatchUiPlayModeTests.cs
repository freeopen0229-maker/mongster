using System.Collections;
using System.IO;
using FusionTamagotchi.Client;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FusionTamagotchi.Tests
{
    // 실제 Unity Play Mode 전용. 현재 호스트에서는 실행되지 않았다.
    // API/domain 테스트와 별개로 UI 컴포넌트/이벤트 분배/프레임 캡처를 검사한다.
    public sealed class HatchUiPlayModeTests
    {
        GameObject root;
        [TearDown] public void Cleanup(){if(root)Object.DestroyImmediate(root);}
        [UnityTest] public IEnumerator GradeColorsAuraAndRenderedFrames()
        {
            Assert.AreNotEqual(UnityEngine.Rendering.GraphicsDeviceType.Null,SystemInfo.graphicsDeviceType,"A graphics-capable native runner is required.");
            root=new GameObject("TestCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var panel=new GameObject("Result",typeof(RectTransform),typeof(Image));panel.transform.SetParent(root.transform,false);
            panel.GetComponent<RectTransform>().sizeDelta=new Vector2(400,220);
            var aura=new GameObject("Aura",typeof(RectTransform),typeof(Image));aura.transform.SetParent(panel.transform,false);
            aura.GetComponent<RectTransform>().sizeDelta=new Vector2(480,260);
            var label=new GameObject("Grade",typeof(RectTransform),typeof(Text));label.transform.SetParent(panel.transform,false);
            var text=label.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=32;
            text.alignment=TextAnchor.MiddleCenter;label.GetComponent<RectTransform>().sizeDelta=new Vector2(380,160);
            var styling=panel.AddComponent<HatchResultStyling>();styling.background=panel.GetComponent<Image>();styling.aura=aura.GetComponent<Image>();styling.resultText=text;
            foreach(var grade in new[]{"Common","Rare","Epic","Legendary","Divine"})
            {
                var expected=HatchPresentationPolicy.Style(grade);text.text=expected.Korean;
                styling.Apply(new PetDTO{rarity=grade});yield return null;
                Assert.That(text.color.r,Is.EqualTo(expected.R/255f).Within(.005f));
                Assert.That(text.color.g,Is.EqualTo(expected.G/255f).Within(.005f));
                Assert.That(text.color.b,Is.EqualTo(expected.B/255f).Within(.005f));
                Assert.That(styling.aura.color.a,Is.InRange(0,expected.AuraAlpha));
                if(expected.AuraAlpha>0)Assert.Greater(styling.aura.color.a,0);
                // GPU/display가 있는 native runner에서 실제 프레임을 기록한다.
                // batchmode에서 WaitForEndOfFrame을 기다리지 않는다. 완성된 이전 프레임을 캡처한다.
                yield return null;yield return null;
                var capture=ScreenCapture.CaptureScreenshotAsTexture();Assert.IsNotNull(capture);
                try
                {
                    Directory.CreateDirectory("Validation/PlayMode");
                    File.WriteAllBytes("Validation/PlayMode/grade-"+grade+".png",capture.EncodeToPNG());
                }
                finally{Object.Destroy(capture);}
            }
        }
        [UnityTest] public IEnumerator SerialTooltipMouseAndTouchEvents()
        {
            root=new GameObject("TooltipRoot");var eventObject=new GameObject("Events",typeof(EventSystem));eventObject.transform.SetParent(root.transform);
            var panel=new GameObject("TooltipPanel");panel.transform.SetParent(root.transform);
            var label=new GameObject("TooltipLabel",typeof(RectTransform),typeof(Text));label.transform.SetParent(panel.transform);
            var target=new GameObject("SerialTarget");target.transform.SetParent(root.transform);
            var tooltip=target.AddComponent<PetSerialTooltip>();tooltip.panel=panel;tooltip.tooltipText=label.GetComponent<Text>();
            tooltip.Bind(new PetDTO{isAdult=true,isHiddenPet=true,serialNumber=3,hiddenLimit=10});
            var mouse=new PointerEventData(eventObject.GetComponent<EventSystem>()){pointerId=-1};
            ExecuteEvents.Execute(target,mouse,ExecuteEvents.pointerEnterHandler);Assert.IsTrue(panel.activeSelf);
            Assert.AreEqual("전 서버 한정 각성\nNo. 003 / 010",tooltip.tooltipText.text);
            ExecuteEvents.Execute(target,mouse,ExecuteEvents.pointerExitHandler);Assert.IsFalse(panel.activeSelf);
            var touch=new PointerEventData(eventObject.GetComponent<EventSystem>()){pointerId=0};
            ExecuteEvents.Execute(target,touch,ExecuteEvents.pointerEnterHandler);Assert.IsFalse(panel.activeSelf);
            ExecuteEvents.Execute(target,touch,ExecuteEvents.pointerClickHandler);Assert.IsTrue(panel.activeSelf);
            ExecuteEvents.Execute(target,touch,ExecuteEvents.pointerExitHandler);Assert.IsTrue(panel.activeSelf);
            ExecuteEvents.Execute(target,touch,ExecuteEvents.pointerClickHandler);Assert.IsFalse(panel.activeSelf);
            tooltip.Bind(new PetDTO{isAdult=false,isHiddenPet=false});
            ExecuteEvents.Execute(target,touch,ExecuteEvents.pointerClickHandler);Assert.IsFalse(panel.activeSelf);
            yield return null;
        }
    }
}
