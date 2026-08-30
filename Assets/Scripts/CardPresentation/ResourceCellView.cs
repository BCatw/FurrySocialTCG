using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FurrySocialCard.CardPresentation
{
    public sealed class ResourceCellView : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private GameObject conditionalStatement;
        [SerializeField, Range(0f, 1f)] private float emptyAlpha = 0.3f;

        public string Tier { get; private set; }
        public string Attribute { get; private set; }

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
            if (conditionalStatement != null)
            {
                conditionalStatement.SetActive(false);
            }

            CanvasGroup group = GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = gameObject.AddComponent<CanvasGroup>();
            }
            group.alpha = count > 0 ? 1f : emptyAlpha;
        }

        private void FindReferences()
        {
            backgroundImage ??= transform.Find("BG")?.GetComponent<Image>();
            iconImage ??= transform.Find("Icon")?.GetComponent<Image>();
            countText ??= transform.Find("Count")?.GetComponent<TMP_Text>();
            conditionalStatement ??= transform.Find("Conditional Statement")?.gameObject;
        }
    }
}
