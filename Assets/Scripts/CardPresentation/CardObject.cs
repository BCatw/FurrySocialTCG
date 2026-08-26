using FurrySocialCard.CardData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FurrySocialCard.CardPresentation
{
    public sealed class CardObject : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text pointText;

        private static Sprite triangleSprite;
        private static Sprite circleSprite;
        private static Sprite squareSprite;

        public CardDefinition Definition { get; private set; }

        private void Reset() => FindReferences();

        public void Bind(CardDefinition definition)
        {
            Definition = definition;
            if (definition == null)
            {
                gameObject.SetActive(false);
                return;
            }

            FindReferences();
            gameObject.name = $"Card_{definition.id}";
            gameObject.SetActive(true);

            if (backgroundImage != null)
            {
                backgroundImage.color = ResolveColor(definition.color);
            }

            if (iconImage != null)
            {
                iconImage.sprite = ResolveShapeSprite(definition.shape);
                iconImage.color = Color.white;
                iconImage.preserveAspect = true;
            }

            if (pointText != null)
            {
                pointText.text = definition.points.ToString("00");
            }
        }

        private void FindReferences()
        {
            if (backgroundImage == null)
            {
                Transform background = transform.Find("Scaler/Image");
                backgroundImage = background != null ? background.GetComponent<Image>() : null;
            }

            if (iconImage == null)
            {
                Transform icon = transform.Find("Icon");
                iconImage = icon != null ? icon.GetComponent<Image>() : null;
            }

            if (pointText == null)
            {
                pointText = GetComponentInChildren<TMP_Text>(true);
            }
        }

        private static Color ResolveColor(string color)
        {
            switch (color)
            {
                case "紅": return new Color32(214, 76, 76, 255);
                case "藍": return new Color32(72, 125, 214, 255);
                case "綠": return new Color32(71, 158, 101, 255);
                case "黃": return new Color32(230, 185, 62, 255);
                default: return new Color32(150, 150, 150, 255);
            }
        }

        private static Sprite ResolveShapeSprite(string shape)
        {
            switch (shape)
            {
                case "三角形": return triangleSprite ?? (triangleSprite = CreateShapeSprite(Shape.Triangle));
                case "方形": return squareSprite ?? (squareSprite = CreateShapeSprite(Shape.Square));
                default: return circleSprite ?? (circleSprite = CreateShapeSprite(Shape.Circle));
            }
        }

        private static Sprite CreateShapeSprite(Shape shape)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = $"Generated_{shape}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    bool inside;
                    switch (shape)
                    {
                        case Shape.Circle:
                            inside = nx * nx + ny * ny <= 0.78f;
                            break;
                        case Shape.Square:
                            inside = Mathf.Abs(nx) <= 0.78f && Mathf.Abs(ny) <= 0.78f;
                            break;
                        default:
                            inside = ny >= -0.78f && ny <= 0.82f && Mathf.Abs(nx) <= (0.82f - ny) * 0.58f;
                            break;
                    }

                    pixels[y * size + x] = inside
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private enum Shape
        {
            Triangle,
            Circle,
            Square
        }
    }
}
