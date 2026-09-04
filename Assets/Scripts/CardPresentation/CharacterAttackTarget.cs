using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FurrySocialCard.CardPresentation
{
    public sealed class CharacterAttackTarget : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private bool isAlly;
        [SerializeField] private RectTransform allyLinkPoint;
        [SerializeField] private RectTransform enemyLinkPoint;
        [SerializeField] private UIGlowEffect attackGlow;

        private RectTransform movementRect;
        private Vector2 restPosition;
        private bool hasRestPosition;

        public bool IsAlly => isAlly;
        public RectTransform ActiveLinkPoint => isAlly ? allyLinkPoint : enemyLinkPoint;
        public event Action<CharacterAttackTarget> Clicked;

        public void Configure(bool ally)
        {
            isAlly = ally;
            FindLinkPoints();
            if (allyLinkPoint != null) allyLinkPoint.gameObject.SetActive(isAlly);
            if (enemyLinkPoint != null) enemyLinkPoint.gameObject.SetActive(!isAlly);
            SetAttackGlow(false, Color.white);
        }

        public void SetAttackGlow(bool visible, Color color)
        {
            FindLinkPoints();
            if (attackGlow == null) return;
            attackGlow.GlowColor = color;
            attackGlow.enabled = visible;
        }

        public void SetAttackOffset(bool attacking, float distance, float duration)
        {
            CaptureRestPosition();
            if (movementRect == null) return;
            movementRect.DOKill();
            float direction = isAlly ? 1f : -1f;
            Vector2 target = attacking
                ? restPosition + Vector2.up * Mathf.Max(0f, distance) * direction
                : restPosition;
            DOTween.To(
                    () => movementRect.anchoredPosition,
                    position => movementRect.anchoredPosition = position,
                    target,
                    Mathf.Max(0f, duration))
                .SetEase(Ease.InOutQuad)
                .SetLink(gameObject);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke(this);
        }

        private void Awake()
        {
            FindLinkPoints();
            CaptureRestPosition();
        }

        private void OnDestroy()
        {
            movementRect?.DOKill();
        }

        private void CaptureRestPosition()
        {
            if (hasRestPosition) return;
            movementRect = transform as RectTransform;
            if (movementRect == null) return;
            restPosition = movementRect.anchoredPosition;
            hasRestPosition = true;
        }

        private void FindLinkPoints()
        {
            if (allyLinkPoint == null) allyLinkPoint = FindChild("LinkPoint_Ally");
            if (enemyLinkPoint == null) enemyLinkPoint = FindChild("LinkPoint_Enemy");
            if (attackGlow == null)
            {
                RectTransform background = FindChild("BG");
                attackGlow = background != null ? background.GetComponent<UIGlowEffect>() : null;
            }
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