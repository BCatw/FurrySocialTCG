using FurrySocialCard.CardData;
using FurrySocialCard.CardPresentation;
using NUnit.Framework;

namespace FurrySocialCard.Tests.Editor
{
    public sealed class ResourceExchangeRuleTests
    {
        [Test]
        public void SameColorAndShape_RequiresBothProperties()
        {
            CardDefinition source = Card("red", "circle");

            Assert.That(ResourceExchangeController.CanEat(source, Card("red", "circle"), ResourceExchangeController.MatchRule.SameColorAndShape), Is.True);
            Assert.That(ResourceExchangeController.CanEat(source, Card("red", "square"), ResourceExchangeController.MatchRule.SameColorAndShape), Is.False);
            Assert.That(ResourceExchangeController.CanEat(source, Card("blue", "circle"), ResourceExchangeController.MatchRule.SameColorAndShape), Is.False);
        }

        [TestCase(ResourceExchangeController.MatchRule.SameColor, "red", "square", true)]
        [TestCase(ResourceExchangeController.MatchRule.SameColor, "blue", "circle", false)]
        [TestCase(ResourceExchangeController.MatchRule.SameShape, "blue", "circle", true)]
        [TestCase(ResourceExchangeController.MatchRule.SameShape, "red", "square", false)]
        [TestCase(ResourceExchangeController.MatchRule.SameColorOrShape, "red", "square", true)]
        [TestCase(ResourceExchangeController.MatchRule.SameColorOrShape, "blue", "circle", true)]
        [TestCase(ResourceExchangeController.MatchRule.SameColorOrShape, "blue", "square", false)]
        public void AlternateRules_UseConfiguredProperty(ResourceExchangeController.MatchRule rule, string targetColor, string targetShape, bool expected)
        {
            Assert.That(ResourceExchangeController.CanEat(Card("red", "circle"), Card(targetColor, targetShape), rule), Is.EqualTo(expected));
        }

        [Test]
        public void AnyRule_ReturnsFalseForMissingCard()
        {
            Assert.That(ResourceExchangeController.CanEat(null, Card("red", "circle"), ResourceExchangeController.MatchRule.SameColor), Is.False);
        }

        private static CardDefinition Card(string color, string shape)
        {
            return new CardDefinition { color = color, shape = shape };
        }
    }
}
