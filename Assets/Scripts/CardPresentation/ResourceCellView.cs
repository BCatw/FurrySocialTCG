using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FurrySocialCard.CardPresentation
{
    public sealed class ResourceCellView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private GameObject conditionalStatement;
        [SerializeField] private GameObject patternCreatorObject;
        [SerializeField] private TMP_InputField patternCountInput;
        [SerializeField] private TMP_Text conditionalStatementText;
        [SerializeField] private UIGlowEffect backgroundGlow;
        [SerializeField, Range(0f, 1f)] private float emptyAlpha = 0.3f;

        public string Tier { get; private set; }
        public string Attribute { get; private set; }
        public PatternScope PatternScope { get; private set; }
        public PatternComparison PatternComparison { get; private set; } = PatternComparison.GreaterThan;
        public int PatternCount { get; private set; }

        private bool patternMode;
        private bool inputEventsRegistered;
        private Color normalCountColor = Color.white;
        private bool countColorCaptured;

        private void Reset() => FindReferences();

        public void Bind(string tier, string attribute, int count)
        {
            FindReferences();
            Tier = tier;
            Attribute = attribute;

            if (backgroundImage != null)
            {
                backgroundImage.color = CardObject.ResolveTierColor(tier);
            }
            if (iconImage != null)
            {
                iconImage.sprite = CardObject.ResolveAttributeSprite(attribute);
                iconImage.color = Color.white;
                iconImage.preserveAspect = true;
            }
            if (countText != null)
            {
                CaptureCountColor();
                countText.text = count.ToString();
                countText.color = normalCountColor;
            }
            SetGlow(false, Color.white);
            if (conditionalStatement != null && !patternMode)
            {
                conditionalStatement.SetActive(false);
            }

            CanvasGroup group = GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = gameObject.AddComponent<CanvasGroup>();
            }
            group.alpha = patternMode
                ? PatternCount > 0 ? 1f : emptyAlpha
                : count > 0 ? 1f : emptyAlpha;
        }

        public void ConfigurePattern(PatternScope scope, string medium, string interactionType, bool showBackground, bool showIcon)
        {
            FindReferences();
            PatternScope = scope;
            Tier = medium;
            Attribute = interactionType;

            if (backgroundImage != null)
            {
                backgroundImage.gameObject.SetActive(showBackground);
                if (showBackground)
                {
                    backgroundImage.color = CardObject.ResolveTierColor(medium);
                }
            }
            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(showIcon);
                if (showIcon)
                {
                    iconImage.sprite = CardObject.ResolveAttributeSprite(interactionType);
                    iconImage.color = Color.white;
                    iconImage.preserveAspect = true;
                }
            }
            if (countText != null)
            {
                countText.gameObject.SetActive(scope == PatternScope.MediumAndInteraction);
            }

            RegisterInputEvents();
            RefreshPatternPresentation();
        }

        public void SetPatternMode(bool active)
        {
            FindReferences();
            patternMode = active;
            patternCreatorObject?.SetActive(active);
            if (active)
            {
                RegisterInputEvents();
                RefreshPatternPresentation();
            }
        }

        public void ShowActionPreview(int beforeAvailable, int afterAvailable, bool relevant,
            Color glowColor, Color changedColor, Color unchangedColor, float glowIntensity = 1f)
        {
            FindReferences();
            if (countText != null)
            {
                countText.text = beforeAvailable == afterAvailable
                    ? beforeAvailable.ToString()
                    : $"{beforeAvailable}>{afterAvailable}";
                countText.color = beforeAvailable == afterAvailable ? unchangedColor : changedColor;
            }
            SetGlow(relevant, glowColor, glowIntensity);
        }

        public void ShowSkillRequirement(string text, Color textColor, Color glowColor)
        {
            FindReferences();
            if (countText != null)
            {
                countText.text = text ?? string.Empty;
                countText.color = textColor;
            }
            SetGlow(true, glowColor);
        }

        public void BindSummary(PatternScope scope, string categoryName, int count)
        {
            FindReferences();
            PatternScope = scope;
            Tier = scope == PatternScope.ResourceMedium ? categoryName : null;
            Attribute = scope == PatternScope.InteractionType ? categoryName : null;
            if (backgroundImage != null)
            {
                bool show = scope == PatternScope.ResourceMedium;
                backgroundImage.gameObject.SetActive(show);
                if (show) backgroundImage.color = CardObject.ResolveTierColor(categoryName);
            }
            if (iconImage != null)
            {
                bool show = scope == PatternScope.InteractionType;
                iconImage.gameObject.SetActive(show);
                if (show)
                {
                    iconImage.sprite = CardObject.ResolveAttributeSprite(categoryName);
                    iconImage.color = Color.white;
                }
            }
            if (countText != null)
            {
                countText.gameObject.SetActive(true);
                CaptureCountColor();
                countText.text = count.ToString();
                countText.color = normalCountColor;
            }
            SetGlow(false, Color.white);
            CanvasGroup group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = count > 0 ? 1f : emptyAlpha;
        }

        public PatternRequirement GetPatternRequirement()
        {
            return new PatternRequirement(PatternScope, Tier, Attribute, PatternComparison, PatternCount);
        }

        public void ApplyPatternRequirement(PatternRequirement requirement)
        {
            PatternComparison = requirement.Comparison;
            PatternCount = requirement.Count;
            if (patternCountInput != null)
            {
                patternCountInput.SetTextWithoutNotify(PatternCount.ToString());
            }
            RefreshPatternPresentation();
        }

        public void ClearPatternRequirement()
        {
            PatternComparison = PatternComparison.GreaterThan;
            PatternCount = 0;
            if (patternCountInput != null)
            {
                patternCountInput.SetTextWithoutNotify("0");
            }
            RefreshPatternPresentation();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!patternMode || eventData == null)
            {
                return;
            }

            Transform pressed = eventData.pointerPress != null ? eventData.pointerPress.transform : null;
            if (pressed != null && patternCreatorObject != null && pressed.IsChildOf(patternCreatorObject.transform))
            {
                return;
            }

            PatternComparison = PatternComparison == PatternComparison.GreaterThan
                ? PatternComparison.Equal
                : PatternComparison == PatternComparison.Equal
                    ? PatternComparison.GreaterThanOrEqual
                    : PatternComparison.GreaterThan;
            RefreshPatternPresentation();
        }

        private void RegisterInputEvents()
        {
            if (inputEventsRegistered || patternCountInput == null)
            {
                return;
            }

            patternCountInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            patternCountInput.onValueChanged.AddListener(HandlePatternCountChanged);
            patternCountInput.onEndEdit.AddListener(NormalizePatternCountInput);
            inputEventsRegistered = true;
            HandlePatternCountChanged(patternCountInput.text);
        }

        private void HandlePatternCountChanged(string value)
        {
            PatternCount = int.TryParse(value, out int count) && count > 0 ? count : 0;
            RefreshPatternPresentation();
        }

        private void NormalizePatternCountInput(string value)
        {
            if (patternCountInput != null && patternCountInput.text != PatternCount.ToString())
            {
                patternCountInput.SetTextWithoutNotify(PatternCount.ToString());
            }
        }

        private void RefreshPatternPresentation()
        {
            if (conditionalStatementText != null)
            {
                conditionalStatementText.text = PatternRequirement.ComparisonSymbol(PatternComparison);
            }
            if (conditionalStatement != null)
            {
                conditionalStatement.SetActive(patternMode);
            }

            CanvasGroup group = GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = gameObject.AddComponent<CanvasGroup>();
            }
            if (patternMode)
            {
                group.alpha = PatternCount > 0 ? 1f : emptyAlpha;
            }
        }

        private void FindReferences()
        {
            if (backgroundImage == null)
            {
                backgroundImage = transform.Find("BG")?.GetComponent<Image>();
            }
            if (backgroundGlow == null && backgroundImage != null)
                backgroundGlow = backgroundImage.GetComponent<UIGlowEffect>();
            if (iconImage == null)
            {
                iconImage = transform.Find("Icon")?.GetComponent<Image>();
            }
            if (countText == null)
            {
                countText = transform.Find("Count")?.GetComponent<TMP_Text>();
            }
            if (patternCreatorObject == null)
            {
                patternCreatorObject = transform.Find("PatternCreatorObj")?.gameObject;
            }
            if (conditionalStatement == null && patternCreatorObject != null)
            {
                conditionalStatement = patternCreatorObject.transform.Find("Conditional Statement")?.gameObject;
            }
            if (conditionalStatementText == null && conditionalStatement != null)
            {
                conditionalStatementText = conditionalStatement.GetComponent<TMP_Text>();
            }
            if (patternCountInput == null && patternCreatorObject != null)
            {
                patternCountInput = patternCreatorObject.transform.Find("InputField (TMP)")?.GetComponent<TMP_InputField>();
            }
        }

        private void SetGlow(bool visible, Color color, float intensity = 1f)
        {
            if (backgroundGlow == null) return;
            backgroundGlow.GlowColor = color;
            backgroundGlow.Intensity = intensity;
            backgroundGlow.enabled = visible;
        }

        private void CaptureCountColor()
        {
            if (countColorCaptured || countText == null) return;
            normalCountColor = countText.color;
            countColorCaptured = true;
        }
    }
}
