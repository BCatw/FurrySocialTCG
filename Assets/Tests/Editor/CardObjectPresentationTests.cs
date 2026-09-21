using System.Collections.Generic;
using FurrySocialCard.CardData;
using FurrySocialCard.CardPresentation;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FurrySocialCard.Tests.Editor
{
    public sealed class CardObjectPresentationTests
    {
        [Test]
        public void NewAttributes_GenerateDistinctSpritesAndDisplayText()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CardGroup.prefab");
            Assert.That(prefab, Is.Not.Null);

            var spriteNames = new HashSet<string>();
            string[] attributes = { "束縛", "撫摸", "震動", "濕潤", "衝擊", "侵入" };
            foreach (string attribute in attributes)
            {
                GameObject instance = Object.Instantiate(prefab);
                CardObject card = instance.GetComponent<CardObject>();
                card.Bind(new CardDefinition
                {
                    id = $"test-{attribute}",
                    serialNumber = 1,
                    attribute = attribute,
                    tier = "獸徵",
                    text = "測試文字"
                });

                Image icon = instance.transform.Find("Icon").GetComponent<Image>();
                TMP_Text label = instance.GetComponentInChildren<TMP_Text>(true);
                Assert.That(icon.sprite, Is.Not.Null, attribute);
                Assert.That(spriteNames.Add(icon.sprite.name), Is.True, $"Duplicate generated shape for {attribute}");
                Assert.That(label.text, Is.EqualTo("測試文字"));
                Object.DestroyImmediate(instance);
            }
        }

        [TestCase("獸徵")]
        [TestCase("法術")]
        [TestCase("器具")]
        [TestCase("動作")]
        public void TierColors_UseMutedRgbRange(string tier)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CardGroup.prefab");
            GameObject instance = Object.Instantiate(prefab);
            CardObject card = instance.GetComponent<CardObject>();
            card.Bind(new CardDefinition { id = "test", serialNumber = 1, attribute = "束縛", tier = tier, text = "文字" });

            Image background = instance.transform.Find("Scaler/Image").GetComponent<Image>();
            Color32 color = background.color;
            Assert.That(Mathf.Max(color.r, color.g, color.b), Is.LessThanOrEqualTo(155), tier);
            Object.DestroyImmediate(instance);
        }
    }
}
