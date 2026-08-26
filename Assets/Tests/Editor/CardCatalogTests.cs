using FurrySocialCard.CardData;
using NUnit.Framework;
using UnityEngine;

namespace FurrySocialCard.Tests.Editor
{
    public sealed class CardCatalogTests
    {
        [Test]
        public void BundledCatalog_ContainsAllInitialCardsAndDeck()
        {
            TextAsset source = Resources.Load<TextAsset>("CardData/fsc_cards");

            Assert.That(source, Is.Not.Null);
            Assert.That(CardCatalog.TryParse(source.text, out CardCatalog catalog, out string error), Is.True, error);
            Assert.That(catalog.Cards, Has.Count.EqualTo(48));
            Assert.That(catalog.TryGetCard("FSC-048", out CardDefinition lastCard), Is.True);
            Assert.That(lastCard.color, Is.EqualTo("黃"));
            Assert.That(lastCard.shape, Is.EqualTo("方形"));
            Assert.That(lastCard.points, Is.EqualTo(4));
            Assert.That(catalog.TryGetDeck("FSC-INITIAL", out DeckDefinition deck), Is.True);
            Assert.That(deck.cardIds, Has.Count.EqualTo(48));
        }

        [Test]
        public void Parser_RejectsDuplicateIds()
        {
            const string json = "{\"schemaVersion\":1,\"dataVersion\":\"test\",\"cards\":[" +
                                "{\"id\":\"same\",\"serialNumber\":1,\"color\":\"紅\",\"shape\":\"圓形\",\"points\":1}," +
                                "{\"id\":\"same\",\"serialNumber\":2,\"color\":\"藍\",\"shape\":\"方形\",\"points\":2}]," +
                                "\"decks\":[{\"id\":\"test\",\"displayName\":\"test\",\"cardIds\":[\"same\"]}]}";

            Assert.That(CardCatalog.TryParse(json, out _, out string error), Is.False);
            StringAssert.Contains("Duplicate card id", error);
        }

        [Test]
        public void Parser_RejectsDeckWithUnknownCard()
        {
            const string json = "{\"schemaVersion\":1,\"dataVersion\":\"test\",\"cards\":[" +
                                "{\"id\":\"known\",\"serialNumber\":1,\"color\":\"紅\",\"shape\":\"圓形\",\"points\":1}]," +
                                "\"decks\":[{\"id\":\"test\",\"displayName\":\"test\",\"cardIds\":[\"missing\"]}]}";

            Assert.That(CardCatalog.TryParse(json, out _, out string error), Is.False);
            StringAssert.Contains("unknown card", error);
        }
    }
}