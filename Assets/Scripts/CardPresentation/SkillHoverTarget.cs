using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FurrySocialCard.CardPresentation
{
    public sealed class SkillHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Action<bool> changed;

        public void Configure(Action<bool> callback) => changed = callback;
        public void OnPointerEnter(PointerEventData eventData) => changed?.Invoke(true);
        public void OnPointerExit(PointerEventData eventData) => changed?.Invoke(false);
        private void OnDisable() => changed?.Invoke(false);
    }
}
