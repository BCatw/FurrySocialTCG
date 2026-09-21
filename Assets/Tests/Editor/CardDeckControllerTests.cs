using System.Collections;
using System.Collections.Generic;
using FurrySocialCard.CardData;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FurrySocialCard.Tests.Editor
{
    public sealed class CardDeckControllerTests
    {
        [UnityTest]
        public IEnumerator InitialDeck_DrawsAll48CardsWithoutDuplicates()
        {
            var gameObject = new GameObject("Deck Test");
            gameObject.AddComponent<CardCatalogLoader>();
            CardDeckController controller = gameObject.AddComponent<CardDeckController>();

            yield return controller.Initialize();

            Assert.That(controller.IsReady, Is.True);
            Assert.That(controller.RemainingCount, Is.EqualTo(48));

            var drawnIds = new HashSet<string>();
            while (controller.TryDraw(out CardDefinition card))
            {
                Assert.That(drawnIds.Add(card.id), Is.True, $"Duplicate draw: {card.id}");
            }

            Assert.That(drawnIds, Has.Count.EqualTo(48));
            Assert.That(controller.RemainingCount, Is.Zero);
            Object.DestroyImmediate(gameObject);
        }
    }
}
