using UnityEngine;
namespace Mongle
{
    public enum RpgSkill { Fireball, BladeWave, Lightning, ArcaneCircle }
    [CreateAssetMenu(menuName="Mongle/RPG Skill")]
    public sealed class RpgSkillDefinition : ScriptableObject
    {
        public RpgSkill kind;
        public Color color=Color.cyan;
        public float windup=.3f,travel=.55f,radius=1,damage=25,knockback=2,cooldown=.7f,strength=1;
    }
}
