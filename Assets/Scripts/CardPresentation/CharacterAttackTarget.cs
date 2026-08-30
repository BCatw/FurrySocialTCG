using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FurrySocialCard.CardPresentation
{
    public sealed class CharacterAttackTarget : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private bool isAlly;
        [SerializeField] private RectTransform allyLinkPoint;
        [SerializeField] private RectTransform enemyLinkPoint;

        public bool IsAlly => isAlly;
        public RectTransform ActiveLinkPoint => isAlly ? allyLinkPoint : enemyLinkPoint;
        public event Action<CharacterAttackTarget> Clicked;

        public void Configure(bool ally)
        {
            isAlly = ally;
            FindLinkPoints();
            if (allyLinkPoint != null) allyLinkPoint.gameObject.SetActive(isAlly);
            if (enemyLinkPoint != null) enemyLinkPoint.gameObject.SetActive(!isAlly);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke(this);
        }

        private void Awake()
        {
            FindLinkPoints();
        }

        private void FindLinkPoints()
        {
            if (allyLinkPoint == null) allyLinkPoint = FindChild("LinkPoint_Ally");
            if (enemyLinkPoint == null) enemyLinkPoint = FindChild("LinkPoint_Enemy");
        }

        private RectTransform FindChild(string childName)
        {
            foreach (RectTransform child in GetComponentsInChildren<RectTransform>(true))
            {
                if (child.name == childName) return child;
            }
            return null;
        }
    }
}