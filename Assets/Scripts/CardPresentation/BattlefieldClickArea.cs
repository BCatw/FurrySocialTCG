using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FurrySocialCard.CardPresentation
{
    public sealed class BattlefieldClickArea : MonoBehaviour, IPointerClickHandler
    {
        public static event Action<PointerEventData> Clicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke(eventData);
        }
    }
}
