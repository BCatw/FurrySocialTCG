using FurrySocialCard.CardData;
using FurrySocialCard.CardPresentation;
using NUnit.Framework;

namespace FurrySocialCard.Tests.Editor
{
    public sealed class ResourceExchangeRuleTests
    {
        [Test]
        public void SameTierAndAttribute_RequiresBothProperties()
        {
            CardDefinition source = Card("red", "circle");

            Assert.That(ResourceExchangeController.CanEat(source, Card("red", "circle"), ResourceExchangeController.MatchRule.SameTierAndAttribute), Is.True);
            Assert.That(ResourceExchangeController.CanEat(source, Card("red", "square"), ResourceExchangeController.MatchRule.SameTierAndAttribute), Is.False);
            Assert.That(ResourceExchangeController.CanEat(source, Card("blue", "circle"), ResourceExchangeController.MatchRule.SameTierAndAttribute), Is.False);
        }

        [TestCase(ResourceExchangeController.MatchRule.SameTier, "red", "square", true)]
        [TestCase(ResourceExchangeController.MatchRule.SameTier, "blue", "circle", false)]
        [TestCase(ResourceExchangeController.MatchRule.SameAttribute, "blue", "circle", true)]
        [TestCase(ResourceExchangeController.MatchRule.SameAttribute, "red", "square", false)]
        [TestCase(ResourceExchangeController.MatchRule.SameTierOrAttribute, "red", "square", true)]
        [TestCase(ResourceExchangeController.MatchRule.SameTierOrAttribute, "blue", "circle", true)]
        [TestCase(ResourceExchangeController.MatchRule.SameTierOrAttribute, "blue", "square", false)]
        public void AlternateRules_UseConfiguredProperty(ResourceExchangeController.MatchRule rule, string targetTier, string targetAttribute, bool expected)
        {
            Assert.That(ResourceExchangeController.CanEat(Card("red", "circle"), Card(targetTier, targetAttribute), rule), Is.EqualTo(expected));
        }

        [Test]
        public void AnyRule_ReturnsFalseForMissingCard()
        {
            Assert.That(ResourceExchangeController.CanEat(null, Card("red", "circle"), ResourceExchangeController.MatchRule.SameTier), Is.False);
        }

        private static CardDefinition Card(string tier, string attribute)
        {
            return new CardDefinition { tier = tier, attribute = attribute, text = "test" };
        }
    }
}

