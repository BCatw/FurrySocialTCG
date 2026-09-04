using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace FurrySocialCard.CardPresentation
{
    [AddComponentMenu("UI/Effects/UI Glow Effect")]
    [RequireComponent(typeof(Graphic))]
    public sealed class UIGlowEffect : BaseMeshEffect
    {
        [SerializeField] private Color glowColor = new Color(1f, 0.8f, 0.2f, 0.8f);
        [SerializeField, Min(0f)] private float radius = 8f;
        [SerializeField, Range(4, 24)] private int samplesPerRing = 8;
        [SerializeField, Range(1, 6)] private int softnessLayers = 3;
        [SerializeField, Min(0f)] private float intensity = 1f;

        [Header("Pulse (Play Mode)")]
        [SerializeField] private bool animatePulse;
        [SerializeField, Range(0f, 2f)] private float pulseMinimum = 0.65f;
        [SerializeField, Range(0f, 2f)] private float pulseMaximum = 1.15f;
        [SerializeField, Min(0.05f)] private float pulseHalfCycleSeconds = 0.65f;
        [SerializeField] private bool ignoreTimeScale = true;

        private readonly List<UIVertex> sourceVertices = new List<UIVertex>();
        private readonly List<UIVertex> outputVertices = new List<UIVertex>();
        private float pulseMultiplier = 1f;
        private Tween pulseTween;

        public Color GlowColor
        {
            get => glowColor;
            set { glowColor = value; SetVerticesDirty(); }
        }

        public float Radius
        {
            get => radius;
            set { radius = Mathf.Max(0f, value); SetVerticesDirty(); }
        }

        public float Intensity
        {
            get => intensity;
            set { intensity = Mathf.Max(0f, value); SetVerticesDirty(); }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            StartPulse();
            SetVerticesDirty();
        }

        protected override void OnDisable()
        {
            StopPulse();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            StopPulse();
            base.OnDestroy();
        }

        public override void ModifyMesh(VertexHelper vertexHelper)
        {
            if (!IsActive() || radius <= 0f || intensity <= 0f || vertexHelper.currentVertCount == 0)
            {
                return;
            }

            sourceVertices.Clear();
            outputVertices.Clear();
            vertexHelper.GetUIVertexStream(sourceVertices);

            int ringCount = Mathf.Max(1, softnessLayers);
            int directionCount = Mathf.Max(4, samplesPerRing);
            outputVertices.Capacity = Mathf.Max(
                outputVertices.Capacity,
                sourceVertices.Count * (ringCount * directionCount + 1));

            for (int ring = ringCount; ring >= 1; ring--)
            {
                float normalizedRadius = ring / (float)ringCount;
                float distance = radius * normalizedRadius;
                float falloff = Mathf.Lerp(1f, 0.28f, normalizedRadius);
                float alpha = glowColor.a * intensity * pulseMultiplier * falloff * 0.32f;

                for (int sample = 0; sample < directionCount; sample++)
                {
                    float angle = sample * Mathf.PI * 2f / directionCount;
                    Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * distance;
                    AppendGlowCopy(sourceVertices, outputVertices, offset, alpha);
                }
            }

            outputVertices.AddRange(sourceVertices);
            vertexHelper.Clear();
            vertexHelper.AddUIVertexTriangleStream(outputVertices);
        }

        public void Refresh()
        {
            SetVerticesDirty();
        }

        private void AppendGlowCopy(
            IReadOnlyList<UIVertex> source,
            ICollection<UIVertex> destination,
            Vector3 offset,
            float alpha)
        {
            Color color = glowColor;
            color.a = Mathf.Clamp01(alpha);

            for (int index = 0; index < source.Count; index++)
            {
                UIVertex vertex = source[index];
                vertex.position += offset;

                float sourceAlpha = vertex.color.a / 255f;
                Color tinted = color;
                tinted.a *= sourceAlpha;
                vertex.color = tinted;
                destination.Add(vertex);
            }
        }

        private void StartPulse()
        {
            StopPulse();
            pulseMultiplier = animatePulse ? pulseMaximum : 1f;
            if (!Application.isPlaying || !animatePulse) return;

            pulseTween = DOTween.To(
                    () => pulseMultiplier,
                    value =>
                    {
                        pulseMultiplier = value;
                        SetVerticesDirty();
                    },
                    pulseMinimum,
                    pulseHalfCycleSeconds)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(ignoreTimeScale)
                .SetLink(gameObject);
        }

        private void StopPulse()
        {
            pulseTween?.Kill();
            pulseTween = null;
            pulseMultiplier = 1f;
        }

        private void SetVerticesDirty()
        {
            if (graphic != null) graphic.SetVerticesDirty();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            radius = Mathf.Max(0f, radius);
            intensity = Mathf.Max(0f, intensity);
            pulseHalfCycleSeconds = Mathf.Max(0.05f, pulseHalfCycleSeconds);
            if (!Application.isPlaying) pulseMultiplier = 1f;
            SetVerticesDirty();
        }
#endif
    }
}
