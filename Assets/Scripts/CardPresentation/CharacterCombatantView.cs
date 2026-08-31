using FurrySocialCard.CharacterData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FurrySocialCard.CardPresentation
{
    public sealed class CharacterCombatantView : MonoBehaviour
    {
        [SerializeField] private Image portraitImage;
        [SerializeField] private Slider climaxBar;
        [SerializeField] private TMP_Text[] skillTexts = new TMP_Text[3];
        [SerializeField] private Color usableSkillColor = new Color32(255, 215, 0, 255);

        private readonly Color[] normalSkillColors = new Color[3];
        public CharacterDefinition Definition { get; private set; }
        public int CurrentClimax { get; private set; }
        public bool IsAlly { get; private set; }

        public void Bind(CharacterDefinition definition, SkillDefinition[] skills, Sprite portrait, bool isAlly)
        {
            FindReferences();
            Definition = definition;
            IsAlly = isAlly;
            CurrentClimax = 0;
            if (portraitImage != null)
            {
                portraitImage.sprite = portrait;
                portraitImage.enabled = portrait != null;
                portraitImage.preserveAspect = true;
            }
            for (int index = 0; index < skillTexts.Length; index++)
            {
                if (skillTexts[index] == null) continue;
                skillTexts[index].text = skills != null && index < skills.Length && skills[index] != null
                    ? skills[index].displayName
                    : string.Empty;
                normalSkillColors[index] = skillTexts[index].color;
            }
            RefreshClimaxBar();
        }

        public void SetSkillAvailable(int index, bool available)
        {
            if (index < 0 || index >= skillTexts.Length || skillTexts[index] == null) return;
            skillTexts[index].color = available ? usableSkillColor : normalSkillColors[index];
        }

        public void AddClimax(int delta)
        {
            if (Definition == null) return;
            CurrentClimax = Mathf.Clamp(CurrentClimax + delta, 0, Mathf.Max(1, Definition.climaxLimit));
            RefreshClimaxBar();
        }

        public void ForceClimax()
        {
            if (Definition == null) return;
            CurrentClimax = Mathf.Max(1, Definition.climaxLimit);
            RefreshClimaxBar();
        }

        private void FindReferences()
        {
            if (portraitImage == null) portraitImage = transform.Find("Mask/Image")?.GetComponent<Image>();
            if (climaxBar == null) climaxBar = transform.Find("ClimaxBar")?.GetComponent<Slider>();
            for (int index = 0; index < skillTexts.Length; index++)
            {
                if (skillTexts[index] == null)
                {
                    skillTexts[index] = transform.Find($"SkillGroup/Skill_{index + 1}/Text (TMP)")?.GetComponent<TMP_Text>();
                }
            }
        }

        private void RefreshClimaxBar()
        {
            if (climaxBar == null || Definition == null) return;
            climaxBar.minValue = 0f;
            climaxBar.maxValue = Mathf.Max(1, Definition.climaxLimit);
            climaxBar.value = CurrentClimax;
        }
    }
}
