using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mongle.VisualRenewal
{
    [Serializable] public sealed class PetSocketBinding { public string id; public Transform socket; }
    /// <summary>본체는 하나의 SpriteRenderer와 골격. 소켓은 의상/오라 부착점이다.</summary>
    public sealed class PetVisualBindings:MonoBehaviour
    {
        public SpriteRenderer bodyRenderer;
        public Animator animator;
        public PetSocketBinding[] sockets=Array.Empty<PetSocketBinding>();
        public void ValidateAndCollect(Dictionary<string,Transform> map)
        {
            map.Clear();
            if(!bodyRenderer||!animator||!bodyRenderer.transform.IsChildOf(transform)||!animator.transform.IsChildOf(transform)||
               !bodyRenderer.sprite||animator.updateMode!=AnimatorUpdateMode.Normal)
                throw new InvalidOperationException("Whole-body SpriteRenderer/Animator binding is invalid.");
            if(GetComponentsInChildren<SpriteRenderer>(true).Length!=1)
                throw new InvalidOperationException("Base rig must have one body SpriteRenderer. Costumes attach later.");
            if(sockets==null)throw new InvalidOperationException("Socket list missing.");
            foreach(var s in sockets)
            {
                if(s==null||string.IsNullOrWhiteSpace(s.id)||!s.socket||!s.socket.IsChildOf(transform)||map.ContainsKey(s.id))
                    throw new InvalidOperationException("Invalid/duplicate costume or aura socket.");
                map.Add(s.id,s.socket);
            }
        }
    }
}
