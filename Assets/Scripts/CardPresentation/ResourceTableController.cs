using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace FurrySocialCard.CardPresentation
{
    public sealed class ResourceTableController : MonoBehaviour
    {
        private static readonly string[] TierOrder = { "獸徵", "法術", "器具", "動作" };
        private static readonly string[] AttributeOrder = { "束縛", "撫摸", "震動", "濕潤", "衝擊", "侵入" };

        [SerializeField] private PlayerTurnDealController gameFlow;
        [SerializeField] private RectTransform table;
        [SerializeField] private RectTransform tableTargetPosition;
        [SerializeField] private RectTransform resourceGrid;
        [SerializeField] private Button tabButton;
        [SerializeField, Min(0f)] private float moveDurationSeconds = 0.25f;

        private readonly Dictionary<ResourceKey, int> counts = new Dictionary<ResourceKey, int>();
        private Vector2 closedPosition;
        private bool isOpen;

        private void Awake()
        {
            FindReferences();
            if (table != null)
            {
                closedPosition = table.anchoredPosition;
            }
            Refresh();
        }

        private void OnEnable()
        {
            FindReferences();
            tabButton?.onClick.AddListener(Toggle);
            if (gameFlow != null)
            {
                gameFlow.ResourceCardsChanged += Refresh;
            }
            Refresh();
        }

        private void OnDisable()
        {
            tabButton?.onClick.RemoveListener(Toggle);
            if (gameFlow != null)
            {
                gameFlow.ResourceCardsChanged -= Refresh;
            }
            table?.DOKill();
        }

        public void Toggle()
        {
            if (table == null || tableTargetPosition == null)
            {
                return;
            }

            isOpen = !isOpen;
            Vector2 destination = isOpen ? tableTargetPosition.anchoredPosition : closedPosition;
            table.DOKill();
            DOTween.To(
                    () => table.anchoredPosition,
                    position => table.anchoredPosition = position,
                    destination,
                    moveDurationSeconds)
                .SetEase(Ease.InOutQuad)
                .SetLink(table.gameObject);
        }

        public void Refresh()
        {
            FindReferences();
            counts.Clear();
            if (gameFlow != null)
            {
                foreach (CardObject card in gameFlow.ResourceCards)
                {
                    if (card?.Definition == null)
                    {
                        continue;
                    }

                    var key = new ResourceKey(card.Definition.Tier, card.Definition.Attribute);
                    counts.TryGetValue(key, out int count);
                    counts[key] = count + 1;
                }
            }

            if (resourceGrid == null)
            {
                return;
            }

            int cellIndex = 0;
            for (int tierIndex = 0; tierIndex < TierOrder.Length; tierIndex++)
            {
                for (int attributeIndex = 0; attributeIndex < AttributeOrder.Length; attributeIndex++)
                {
                    if (cellIndex >= resourceGrid.childCount)
                    {
                        Debug.LogWarning("ResourceGrid needs 24 ResourceCell children.", this);
                        return;
                    }

                    Transform child = resourceGrid.GetChild(cellIndex++);
                    ResourceCellView cell = child.GetComponent<ResourceCellView>();
                    if (cell == null)
                    {
                        cell = child.gameObject.AddComponent<ResourceCellView>();
                    }

                    var key = new ResourceKey(TierOrder[tierIndex], AttributeOrder[attributeIndex]);
                    counts.TryGetValue(key, out int count);
                    cell.Bind(key.Tier, key.Attribute, count);
                }
            }
        }

        private void FindReferences()
        {
            table ??= transform as RectTransform;
            resourceGrid ??= table?.Find("ResourceGrid") as RectTransform;
            gameFlow ??= FindObjectOfType<PlayerTurnDealController>();

            if (tableTargetPosition == null && table != null)
            {
                tableTargetPosition = table.parent?.Find("TableTargetPosition") as RectTransform;
            }
            if (tabButton == null && table != null)
            {
                Transform tab = table.Find("Text (TMP)");
                if (tab != null)
                {
                    tabButton = tab.GetComponent<Button>();
                    if (tabButton == null)
                    {
                        tabButton = tab.gameObject.AddComponent<Button>();
                        tabButton.targetGraphic = tab.GetComponent<Graphic>();
                    }
                }
            }
        }

        private readonly struct ResourceKey : IEquatable<ResourceKey>
        {
            public readonly string Tier;
            public readonly string Attribute;

            public ResourceKey(string tier, string attribute)
            {
                Tier = tier;
                Attribute = attribute;
            }

            public bool Equals(ResourceKey other) => Tier == other.Tier && Attribute == other.Attribute;
            public override bool Equals(object obj) => obj is ResourceKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Tier != null ? Tier.GetHashCode() : 0) * 397)
                        ^ (Attribute != null ? Attribute.GetHashCode() : 0);
                }
            }
        }
    }
}
