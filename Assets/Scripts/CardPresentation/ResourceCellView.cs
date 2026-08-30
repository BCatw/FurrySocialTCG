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
        [SerializeField, Range(0f, 1f)] private float emptyAlpha = 0.3f;

        public string Tier { get; private set; }
        public string Attribute { get; private set; }
        public PatternScope PatternScope { get; private set; }
        public PatternComparison PatternComparison { get; private set; } = PatternComparison.GreaterThan;
        public int PatternCount { get; private set; }

        private bool patternMode;
        private bool inputEventsRegistered;

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
                countText.text = count.ToString();
            }
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
    }
}
