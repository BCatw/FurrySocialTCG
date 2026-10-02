using FurrySocialCard.CharacterData;
using NUnit.Framework;

namespace FurrySocialCard.Tests.Editor
{
    public sealed class CharacterBattleStateTests
    {
        [Test]
        public void ClimaxResetsAndDiscardsOverflow()
        {
            var state = new CharacterBattleState(100, 2);
            Assert.IsTrue(state.Apply(250, false, true));
            Assert.AreEqual(0, state.Climax);
            Assert.AreEqual(1, state.Stamina);
        }

        [Test]
        public void OwnTurnClimaxSkipsNextOwnerTurn()
        {
            var state = new CharacterBattleState(100, 2);
            state.Apply(100, false, true);
            state.CompleteOwnerTurn(); // The turn in which the climax occurred.
            Assert.IsFalse(state.CanAct);
            state.CompleteOwnerTurn(); // The next owner's turn is skipped.
            Assert.IsTrue(state.CanAct);
        }

        [Test]
        public void OpponentTurnClimaxSkipsNextOwnerTurn()
        {
            var state = new CharacterBattleState(100, 2);
            state.Apply(100, false, false);
            Assert.IsFalse(state.CanAct);
            state.CompleteOwnerTurn();
            Assert.IsTrue(state.CanAct);
        }

        [Test]
        public void RestBlocksChangesAndRepeatedTokens()
        {
            var state = new CharacterBattleState(100, 3);
            state.Apply(100, false, true);
            Assert.IsFalse(state.Apply(999, false, true));
            Assert.IsFalse(state.Apply(-100, false, true));
            Assert.IsFalse(state.Apply(0, true, true));
            Assert.AreEqual(2, state.Stamina);
            Assert.AreEqual(0, state.Climax);
        }

        [Test]
        public void SaintNeverRecoversOrProducesMoreTokens()
        {
            var state = new CharacterBattleState(100, 1);
            state.Apply(100, false, false);
            state.CompleteOwnerTurn();
            state.CompleteOwnerTurn();
            Assert.IsTrue(state.IsSaint);
            Assert.IsFalse(state.CanAct);
            Assert.IsFalse(state.Apply(0, true, false));
            Assert.AreEqual(0, state.Stamina);
        }

        [Test]
        public void PreviewCopyCannotMutateExecution()
        {
            var state = new CharacterBattleState(100, 2);
            state.Apply(20, false, false);
            var copy = state.Copy();
            copy.Apply(80, false, false);
            Assert.AreEqual(20, state.Climax);
            Assert.AreEqual(2, state.Stamina);
            Assert.AreEqual(1, copy.Stamina);
        }

        [Test]
        public void NegativeDeltaClampsToZero()
        {
            var state = new CharacterBattleState(100, 2);
            state.Apply(30, false, false);
            Assert.IsFalse(state.Apply(-200, false, false));
            Assert.AreEqual(0, state.Climax);
            Assert.AreEqual(2, state.Stamina);
        }

        [Test]
        public void LargePositiveDeltaDoesNotOverflow()
        {
            var state = new CharacterBattleState(100, 2);
            state.Apply(50, false, false);
            Assert.IsTrue(state.Apply(int.MaxValue, false, false));
            Assert.AreEqual(1, state.Stamina);
        }
    }
}
