using System;
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
        [SerializeField] private Slider climaxPreviewBar;
        [SerializeField] private TMP_Text climaxValueText;
        [SerializeField] private TMP_Text[] skillTexts = new TMP_Text[3];
        [SerializeField] private TMP_Text[] skillSelfClimaxTexts = new TMP_Text[3];
        [SerializeField] private TMP_Text[] skillTargetClimaxTexts = new TMP_Text[3];
        [SerializeField] private Color usableSkillColor = new Color32(255, 215, 0, 255);

        private readonly Color[] normalSkillColors = new Color[3];
        public event Action<CharacterCombatantView, int, bool> SkillHoverChanged;
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
            ClearAttackPreview();
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

        public void SetClimaxPreview(int projectedClimax, bool visible)
        {
            if (Definition == null) return;
            int maximum = Mathf.Max(1, Definition.climaxLimit);
            int projected = Mathf.Clamp(projectedClimax, 0, maximum);
            int delta = projected - CurrentClimax;
            if (climaxPreviewBar != null)
            {
                climaxPreviewBar.gameObject.SetActive(visible);
                climaxPreviewBar.minValue = 0f;
                climaxPreviewBar.maxValue = maximum;
                climaxPreviewBar.value = projected;
            }
            if (climaxValueText != null)
                climaxValueText.text = visible
                    ? $"{CurrentClimax} ({delta:+#;-#;0}) / {maximum}"
                    : $"{CurrentClimax} / {maximum}";
        }

        public void SetSkillClimaxPreview(int index, int selfDelta, int targetDelta, bool visible)
        {
            SetSignedValue(skillSelfClimaxTexts, index, selfDelta, visible && selfDelta != 0);
            SetSignedValue(skillTargetClimaxTexts, index, targetDelta, visible && targetDelta != 0);
        }

        public void ClearAttackPreview()
        {
            if (climaxPreviewBar != null) climaxPreviewBar.gameObject.SetActive(false);
            for (int index = 0; index < 3; index++)
            {
                SetSignedValue(skillSelfClimaxTexts, index, 0, false);
                SetSignedValue(skillTargetClimaxTexts, index, 0, false);
            }
            if (Definition != null) RefreshClimaxBar();
        }

        private void FindReferences()
        {
            if (portraitImage == null) portraitImage = transform.Find("Mask/Image")?.GetComponent<Image>();
            if (climaxBar == null) climaxBar = transform.Find("ClimaxBar")?.GetComponent<Slider>();
            if (climaxPreviewBar == null) climaxPreviewBar = transform.Find("ClimaxBar_Preview")?.GetComponent<Slider>();
            if (climaxValueText == null) climaxValueText = transform.Find("ClimaxBar/Value")?.GetComponentInChildren<TMP_Text>(true);
            for (int index = 0; index < skillTexts.Length; index++)
            {
                Transform skillRoot = transform.Find($"SkillGroup/Skill_{index + 1}");
                if (skillTexts[index] == null) skillTexts[index] = skillRoot?.Find("Name")?.GetComponentInChildren<TMP_Text>(true);
                if (skillSelfClimaxTexts[index] == null) skillSelfClimaxTexts[index] = skillRoot?.Find("climax_self")?.GetComponentInChildren<TMP_Text>(true);
                if (skillTargetClimaxTexts[index] == null) skillTargetClimaxTexts[index] = skillRoot?.Find("climax_target")?.GetComponentInChildren<TMP_Text>(true);
                if (skillRoot != null)
                {
                    SkillHoverTarget hover = skillRoot.GetComponent<SkillHoverTarget>();
                    if (hover == null) hover = skillRoot.gameObject.AddComponent<SkillHoverTarget>();
                    int capturedIndex = index;
                    hover.Configure(active => SkillHoverChanged?.Invoke(this, capturedIndex, active));
                }
            }
        }

        private void RefreshClimaxBar()
        {
            if (climaxBar == null || Definition == null) return;
            climaxBar.minValue = 0f;
            climaxBar.maxValue = Mathf.Max(1, Definition.climaxLimit);
            climaxBar.value = CurrentClimax;
            if (climaxValueText != null)
                climaxValueText.text = $"{CurrentClimax} / {Mathf.Max(1, Definition.climaxLimit)}";
        }

        private static void SetSignedValue(TMP_Text[] labels, int index, int value, bool visible)
        {
            if (labels == null || index < 0 || index >= labels.Length || labels[index] == null) return;
            labels[index].gameObject.SetActive(visible);
            if (visible) labels[index].text = value.ToString("+#;-#;0");
        }
    }
}
