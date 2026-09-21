using System;
using UnityEngine;

namespace FurrySocialCard.CardPresentation
{
    internal static class CharacterSlotUtility
    {
        public static Transform ResolveCharacter(Transform slotOrCharacter)
        {
            if (slotOrCharacter == null) return null;
            if (slotOrCharacter.GetComponent<CharacterAttackTarget>() != null) return slotOrCharacter;

            if (slotOrCharacter.name.StartsWith("Slot", StringComparison.OrdinalIgnoreCase)
                && slotOrCharacter.childCount > 0)
            {
                return slotOrCharacter.GetChild(0);
            }

            CharacterAttackTarget nested = slotOrCharacter.GetComponentInChildren<CharacterAttackTarget>(true);
            return nested != null ? nested.transform : slotOrCharacter;
        }
    }
}
