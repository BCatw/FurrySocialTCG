using System;
using System.Collections.Generic;
using FurrySocialCard.CardData;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FurrySocialCard.CardPresentation
{
    public sealed class CardObject : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text pointText;

        private static readonly Dictionary<Shape, Sprite> ShapeSprites = new Dictionary<Shape, Sprite>();

        public CardDefinition Definition { get; private set; }
        public static event Action<CardObject, PointerEventData> Clicked;

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
            SetSelected(false);
            SetDimmed(false);
            gameObject.name = $"Card_{definition.id}";
            gameObject.SetActive(true);

            if (backgroundImage != null)
            {
                backgroundImage.color = ResolveTierColor(definition.Tier);
            }

            if (iconImage != null)
            {
                iconImage.sprite = ResolveAttributeSprite(definition.Attribute);
                iconImage.color = Color.white;
                iconImage.preserveAspect = true;
            }

            if (pointText != null)
            {
                pointText.enableAutoSizing = true;
                pointText.fontSizeMin = 18f;
                pointText.fontSizeMax = 48f;
                pointText.text = definition.DisplayText;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke(this, eventData);
        }

        public void SetSelected(bool selected)
        {
            transform.localScale = selected ? Vector3.one * 1.12f : Vector3.one;
        }

        public void SetDimmed(bool dimmed)
        {
            CanvasGroup group = GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = gameObject.AddComponent<CanvasGroup>();
            }

            group.alpha = dimmed ? 0.3f : 1f;
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

        private static Color ResolveTierColor(string tier)
        {
            switch (tier)
            {
                case "獸徵":
                case "紅": return new Color32(155, 92, 92, 255);
                case "法術":
                case "黃": return new Color32(155, 145, 88, 255);
                case "器具":
                case "綠": return new Color32(88, 155, 108, 255);
                case "動作":
                case "藍": return new Color32(88, 119, 155, 255);
                default: return new Color32(125, 125, 125, 255);
            }
        }

        private static Sprite ResolveAttributeSprite(string attribute)
        {
            Shape shape;
            switch (attribute)
            {
                case "束縛":
                case "圓形": shape = Shape.Circle; break;
                case "撫摸": shape = Shape.Paw; break;
                case "震動": shape = Shape.Lightning; break;
                case "濕潤": shape = Shape.Droplet; break;
                case "衝擊": shape = Shape.Cross; break;
                case "侵入": shape = Shape.Arrow; break;
                case "三角形": shape = Shape.Triangle; break;
                case "方形": shape = Shape.Square; break;
                default: shape = Shape.Circle; break;
            }

            if (!ShapeSprites.TryGetValue(shape, out Sprite sprite))
            {
                sprite = CreateShapeSprite(shape);
                ShapeSprites.Add(shape, sprite);
            }
            return sprite;
        }

        private static Sprite CreateShapeSprite(Shape shape)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = $"Generated_{shape}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Vector2[] polygon = GetPolygon(shape);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    bool inside = IsInsideShape(shape, nx, ny, polygon);
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

        private static bool IsInsideShape(Shape shape, float x, float y, Vector2[] polygon)
        {
            switch (shape)
            {
                case Shape.Circle:
                    return x * x + y * y <= 0.62f;
                case Shape.Paw:
                    return Ellipse(x, y + 0.22f, 0.48f, 0.40f)
                        || Ellipse(x + 0.48f, y - 0.30f, 0.18f, 0.24f)
                        || Ellipse(x + 0.17f, y - 0.48f, 0.18f, 0.25f)
                        || Ellipse(x - 0.17f, y - 0.48f, 0.18f, 0.25f)
                        || Ellipse(x - 0.48f, y - 0.30f, 0.18f, 0.24f);
                case Shape.Droplet:
                    return Ellipse(x, y + 0.25f, 0.55f, 0.55f)
                        || (y >= -0.15f && y <= 0.82f && Mathf.Abs(x) <= (0.82f - y) * 0.57f);
                case Shape.Cross:
                    return Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) <= 0.72f
                        && (Mathf.Abs(x - y) <= 0.17f || Mathf.Abs(x + y) <= 0.17f);
                case Shape.Square:
                    return Mathf.Abs(x) <= 0.72f && Mathf.Abs(y) <= 0.72f;
                case Shape.Triangle:
                    return y >= -0.72f && y <= 0.76f && Mathf.Abs(x) <= (0.76f - y) * 0.56f;
                default:
                    return IsInsidePolygon(x, y, polygon);
            }
        }

        private static bool Ellipse(float x, float y, float radiusX, float radiusY)
        {
            float nx = x / radiusX;
            float ny = y / radiusY;
            return nx * nx + ny * ny <= 1f;
        }

        private static Vector2[] GetPolygon(Shape shape)
        {
            switch (shape)
            {
                case Shape.Lightning:
                    return new[]
                    {
                        new Vector2(0.18f, 0.82f), new Vector2(-0.52f, 0.02f),
                        new Vector2(-0.12f, 0.02f), new Vector2(-0.34f, -0.82f),
                        new Vector2(0.58f, -0.02f), new Vector2(0.16f, -0.02f)
                    };
                case Shape.Arrow:
                    return new[]
                    {
                        new Vector2(0.82f, 0f), new Vector2(0.22f, 0.62f),
                        new Vector2(0.22f, 0.24f), new Vector2(-0.76f, 0.24f),
                        new Vector2(-0.76f, -0.24f), new Vector2(0.22f, -0.24f),
                        new Vector2(0.22f, -0.62f)
                    };
                default:
                    return null;
            }
        }

        private static bool IsInsidePolygon(float x, float y, Vector2[] polygon)
        {
            if (polygon == null || polygon.Length < 3)
            {
                return false;
            }

            bool inside = false;
            int previous = polygon.Length - 1;
            for (int current = 0; current < polygon.Length; current++)
            {
                Vector2 a = polygon[current];
                Vector2 b = polygon[previous];
                if ((a.y > y) != (b.y > y)
                    && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
                previous = current;
            }
            return inside;
        }

        private enum Shape
        {
            Circle,
            Paw,
            Lightning,
            Droplet,
            Cross,
            Arrow,
            Triangle,
            Square
        }
    }
}
