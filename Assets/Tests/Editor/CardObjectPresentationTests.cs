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
        public void StaminaBadgeTracksActualStateAndResetButNotPreview()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CharacterObjGroup.prefab");
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                var view = instance.GetComponent<CharacterCombatantView>();
                if (view == null) view = instance.AddComponent<CharacterCombatantView>();
                view.Bind(new FurrySocialCard.CharacterData.CharacterDefinition
                    { id = "test", climaxLimit = 100, stamina = 2 }, null, null, true);
                TMP_Text badge = instance.transform.Find("Stamina").GetComponentInChildren<TMP_Text>(true);
                Assert.AreEqual("2", badge.text);
                var preview = view.BattleState.Copy();
                preview.Apply(100, false, true);
                view.SetStatePreview(preview);
                Assert.AreEqual("2", badge.text);
                view.ApplyClimax(100, false, true);
                Assert.AreEqual("1", badge.text);
                view.ResetBattleState();
                Assert.AreEqual("2", badge.text);
                foreach (Graphic graphic in instance.transform.Find("Stamina").GetComponentsInChildren<Graphic>(true))
                    Assert.IsFalse(graphic.raycastTarget);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void TurnPhaseTokenIsSeparateFromPhaseAndSurvivesGameOver()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TurnPhase.prefab");
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                var type = typeof(TurnPhaseDisplayController).GetNestedType("PhaseView",
                    System.Reflection.BindingFlags.NonPublic);
                object view = System.Activator.CreateInstance(type, new object[] { instance });
                TMP_Text token = instance.transform.Find("TokenCount").GetComponentInChildren<TMP_Text>(true);
                TMP_Text phase = instance.transform.Find("Text (TMP)").GetComponent<TMP_Text>();
                type.GetMethod("SetTokens").Invoke(view, new object[] { 3 });
                type.GetMethod("Set").Invoke(view, new object[] { true, "PLAY", Color.blue, false });
                Assert.AreEqual("3", token.text);
                Assert.AreEqual("PLAY", phase.text);
                type.GetMethod("Set").Invoke(view, new object[] { false, "", Color.gray, true });
                Assert.IsTrue(token.gameObject.activeInHierarchy);
                Assert.IsFalse(phase.gameObject.activeInHierarchy);
                Assert.IsFalse(instance.transform.Find("Image").gameObject.activeInHierarchy);
                type.GetMethod("SetTokens").Invoke(view, new object[] { 0 });
                type.GetMethod("Set").Invoke(view, new object[] { false, "", Color.gray, false });
                Assert.IsFalse(instance.activeSelf);
                type.GetMethod("Set").Invoke(view, new object[] { true, "PLAY", Color.blue, false });
                Assert.AreEqual("0", token.text);
                Assert.IsTrue(phase.gameObject.activeInHierarchy);
                foreach (Graphic graphic in instance.transform.Find("TokenCount").GetComponentsInChildren<Graphic>(true))
                    Assert.IsFalse(graphic.raycastTarget);
            }
            finally { Object.DestroyImmediate(instance); }
        }

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
