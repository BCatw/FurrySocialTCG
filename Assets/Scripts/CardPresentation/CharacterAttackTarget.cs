using System;
using DG.Tweening;
using TMPro;
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
        [SerializeField] private TMP_Text allyAttackOrderText;
        [SerializeField] private TMP_Text enemyAttackOrderText;

        private RectTransform movementRect;
        private Vector2 restPosition;
        private bool hasRestPosition;

        public bool IsAlly => isAlly;
        public RectTransform ActiveLinkPoint => isAlly ? allyLinkPoint : enemyLinkPoint;
        private TMP_Text ActiveAttackOrderText => isAlly ? allyAttackOrderText : enemyAttackOrderText;
        public event Action<CharacterAttackTarget> Clicked;

        public void Configure(bool ally)
        {
            isAlly = ally;
            FindLinkPoints();
            if (allyLinkPoint != null) allyLinkPoint.gameObject.SetActive(isAlly);
            if (enemyLinkPoint != null) enemyLinkPoint.gameObject.SetActive(!isAlly);
            SetAttackOrderText(null);
            SetAttackGlow(false, Color.white);
        }

        public void SetAttackGlow(bool visible, Color color)
        {
            FindLinkPoints();
            if (attackGlow == null) return;
            attackGlow.GlowColor = color;
            attackGlow.enabled = visible;
        }

        public void SetAttackOrderText(string value)
        {
            FindLinkPoints();
            TMP_Text label = ActiveAttackOrderText;
            if (label == null) return;
            bool visible = !string.IsNullOrWhiteSpace(value);
            label.text = visible ? value : string.Empty;
            label.gameObject.SetActive(visible);
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
            TweenAnchoredPosition(movementRect, target, duration, Ease.InOutQuad).SetLink(gameObject);
        }

        public Tween CreateAttackTween(
            CharacterAttackTarget target,
            float distance,
            float totalDuration,
            float shakeStrength,
            int shakeVibrato,
            Action onImpact)
        {
            CaptureRestPosition();
            if (movementRect == null) return null;

            movementRect.DOKill();
            target?.CaptureRestPosition();
            target?.movementRect?.DOKill();

            float duration = Mathf.Max(0.01f, totalDuration);
            float forwardDuration = duration * 0.42f;
            float returnDuration = duration - forwardDuration;
            float direction = isAlly ? 1f : -1f;
            Vector2 impactPosition = restPosition + Vector2.up * Mathf.Max(0f, distance) * direction;

            Sequence sequence = DOTween.Sequence().SetLink(gameObject);
            sequence.Append(TweenAnchoredPosition(movementRect, impactPosition, forwardDuration, Ease.InQuad));
            sequence.AppendCallback(() => onImpact?.Invoke());
            sequence.Append(TweenAnchoredPosition(movementRect, restPosition, returnDuration, Ease.OutQuad));
            if (target?.movementRect != null)
            {
                sequence.Join(target.CreateShakeTween(returnDuration, shakeStrength, shakeVibrato));
            }
            return sequence;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke(this);
        }

        private void Awake()
        {
            FindLinkPoints();
            CaptureRestPosition();
            SetAttackOrderText(null);
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
            if (allyAttackOrderText == null && allyLinkPoint != null)
            {
                allyAttackOrderText = allyLinkPoint.GetComponentInChildren<TMP_Text>(true);
            }
            if (enemyAttackOrderText == null && enemyLinkPoint != null)
            {
                enemyAttackOrderText = enemyLinkPoint.GetComponentInChildren<TMP_Text>(true);
            }
            if (attackGlow == null)
            {
                RectTransform background = FindChild("BG");
                attackGlow = background != null ? background.GetComponent<UIGlowEffect>() : null;
            }
        }

        private Tween CreateShakeTween(float duration, float strength, int vibrato)
        {
            if (movementRect == null) return null;
            int steps = Mathf.Max(2, vibrato);
            float stepDuration = Mathf.Max(0.01f, duration) / (steps + 1);
            Sequence sequence = DOTween.Sequence().SetLink(gameObject);
            for (int index = 0; index < steps; index++)
            {
                float remaining = 1f - index / (float)steps;
                float direction = index % 2 == 0 ? 1f : -1f;
                Vector2 offset = Vector2.right * Mathf.Max(0f, strength) * remaining * direction;
                sequence.Append(TweenAnchoredPosition(movementRect, restPosition + offset, stepDuration, Ease.Linear));
            }
            sequence.Append(TweenAnchoredPosition(movementRect, restPosition, stepDuration, Ease.OutQuad));
            return sequence;
        }

        private static Tween TweenAnchoredPosition(RectTransform target, Vector2 destination, float duration, Ease ease)
        {
            return DOTween.To(
                    () => target.anchoredPosition,
                    value => target.anchoredPosition = value,
                    destination,
                    Mathf.Max(0f, duration))
                .SetEase(ease);
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