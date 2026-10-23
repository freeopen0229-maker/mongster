using System;
using System.Globalization;

namespace FusionTamagotchi.Client
{
    // UI 스타일의 데이터 규칙. Unity rendering이나 게임 능력치와 독립적이다.
    public struct HatchGradeStyle
    {
        public string Korean;
        public byte R,G,B;
        public float AuraAlpha,RotationDegreesPerSecond;
    }
    public static class HatchPresentationPolicy
    {
        public static HatchGradeStyle Style(string rarity)
        {
            switch(rarity)
            {
                case "Common":return Make("일반",202,208,222,0,0);
                case "Rare":return Make("희귀",101,186,255,.14f,4);
                case "Epic":return Make("영웅",186,140,255,.22f,7);
                case "Legendary":return Make("전설",255,213,122,.32f,10);
                case "Divine":return Make("신수",189,252,240,.45f,14);
                default:throw new ArgumentException("Unknown rarity.");
            }
        }
        static HatchGradeStyle Make(string name,byte r,byte g,byte b,float alpha,float rotation)
        {return new HatchGradeStyle{Korean=name,R=r,G=g,B=b,AuraAlpha=alpha,RotationDegreesPerSecond=rotation};}
        public static string Serial(int serial,int limit)
        {
            if(serial<1||limit<1||serial>limit)throw new ArgumentOutOfRangeException(nameof(serial));
            return "No. "+serial.ToString("D3",CultureInfo.InvariantCulture)+" / "+limit.ToString("D3",CultureInfo.InvariantCulture);
        }
        public static bool ValidNotice(HiddenAwakeningNoticeDTO n)
        {
            return n!=null&&Mongle.Slice.PetIds.Valid(n.eventId)&&ValidHiddenIdentity(n.hiddenType,n.rarity,n.serialNo,n.maxLimit);
        }
        // DTO/방송/레거시 UI 진입점이 같은 희소성 규칙을 사용한다.
        public static bool ValidHiddenIdentity(int type,string rarity,int serial,int limit)
        {return serial>=1&&serial<=limit&&(type==0?rarity=="Divine"&&limit==10:
            type>=1&&type<=4&&rarity=="Legendary"&&limit==100);}
    }
}
