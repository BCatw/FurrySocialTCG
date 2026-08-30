using System.Collections.Generic;
using FurrySocialCard.CardPresentation;
using NUnit.Framework;

namespace FurrySocialCard.Tests.Editor
{
    public sealed class PatternExpressionBuilderTests
    {
        [Test]
        public void Build_UsesCanonicalUnicodeTokensAndConfiguredComparisons()
        {
            var requirements = new List<PatternRequirement>
            {
                new PatternRequirement(PatternScope.ResourceMedium, "獸徵", null, PatternComparison.GreaterThanOrEqual, 3),
                new PatternRequirement(PatternScope.InteractionType, null, "侵入", PatternComparison.Equal, 2),
                new PatternRequirement(PatternScope.MediumAndInteraction, "動作", "侵入", PatternComparison.GreaterThan, 1)
            };

            Assert.That(
                PatternExpressionBuilder.Build(requirements),
                Is.EqualTo("〈獸徵〉 >= 3 + [侵入] = 2 + 〈動作〉[侵入] > 1"));
        }

        [Test]
        public void Build_OmitsZeroCountRequirements()
        {
            var requirements = new[]
            {
                new PatternRequirement(PatternScope.ResourceMedium, "獸徵", null, PatternComparison.Equal, 0),
                new PatternRequirement(PatternScope.InteractionType, null, "束縛", PatternComparison.GreaterThan, 2)
            };

            Assert.That(PatternExpressionBuilder.Build(requirements), Is.EqualTo("[束縛] > 2"));
        }

        [Test]
        public void Parse_ValidExpression_RoundTripsToCanonicalFormat()
        {
            const string input = "〈獸徵〉>=3 + [侵入] = 2 + 〈動作〉[侵入]>1";

            bool parsed = PatternExpressionParser.TryParse(input, out List<PatternRequirement> requirements, out string error);

            Assert.That(parsed, Is.True, error);
            Assert.That(
                PatternExpressionBuilder.Build(requirements),
                Is.EqualTo("〈獸徵〉 >= 3 + [侵入] = 2 + 〈動作〉[侵入] > 1"));
        }

        [TestCase("<獸徵> >= 3")]
        [TestCase("〈未知〉 >= 3")]
        [TestCase("[侵入] != 2")]
        [TestCase("〈獸徵〉 >= 3 + 〈獸徵〉 = 2")]
        [TestCase("")]
        public void Parse_InvalidExpression_ReturnsError(string input)
        {
            bool parsed = PatternExpressionParser.TryParse(input, out List<PatternRequirement> requirements, out string error);

            Assert.That(parsed, Is.False);
            Assert.That(requirements, Is.Empty);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void Parse_SpecificCard_IsAvailableForFutureSkillMatching()
        {
            bool parsed = PatternExpressionParser.TryParse("{card_insert} >= 2", out List<PatternRequirement> requirements, out string error);

            Assert.That(parsed, Is.True, error);
            Assert.That(requirements[0].Scope, Is.EqualTo(PatternScope.SpecificCard));
            Assert.That(requirements[0].CardId, Is.EqualTo("card_insert"));
            Assert.That(requirements[0].IsSatisfiedBy(2), Is.True);
        }
    }
}
