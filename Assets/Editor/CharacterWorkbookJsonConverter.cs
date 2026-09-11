using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using FurrySocialCard.CardPresentation;
using FurrySocialCard.CharacterData;
using UnityEngine;

namespace FurrySocialCard.EditorTools
{
    internal sealed class CharacterWorkbookJsonConverter : IXlsxJsonWorkbookConverter
    {
        private const string OutputPath = "Assets/Resources/CharacterData/fsc_characters.json";
        private static readonly string[] CharacterHeaders =
            { "characterId", "displayName", "tags", "climaxLimit", "activeSkill1Id", "activeSkill2Id", "activeSkill3Id", "passiveSkillId" };
        private static readonly string[] SkillHeaders =
            { "skillId", "displayName", "skillType", "pattern", "resourceBehavior", "effectId", "rawEffectText", "designStatus", "notes" };
        private static readonly string[] EffectHeaders =
            { "effectId", "effectType", "target", "value", "durationTurns" };

        public string Name => "角色資料";

        public bool CanConvert(XlsxWorkbookData workbook) =>
            workbook.TryGetSheet("Characters", out _) &&
            workbook.TryGetSheet("Skills", out _) &&
            workbook.TryGetSheet("Effects", out _);

        public bool TryConvert(string sourceAssetPath, XlsxWorkbookData workbook, out string outputAssetPath,
            out string json, out List<string> warnings, out List<string> errors)
        {
            outputAssetPath = OutputPath;
            warnings = new List<string>();
            errors = new List<string>();
            if (!HasSchema(workbook, "Characters", CharacterHeaders, errors) |
                !HasSchema(workbook, "Skills", SkillHeaders, errors) |
                !HasSchema(workbook, "Effects", EffectHeaders, errors))
            {
                json = string.Empty;
                return false;
            }

            var document = new CharacterDataDocument
            {
                schemaVersion = 1,
                dataVersion = "xlsx-" + File.GetLastWriteTimeUtc(Path.GetFullPath(sourceAssetPath))
                    .ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
                characters = new List<CharacterDefinition>(),
                skills = new List<SkillDefinition>(),
                effects = new List<EffectDefinition>()
            };

            workbook.TryGetSheet("Characters", out var characterSheet);
            workbook.TryGetSheet("Skills", out var skillSheet);
            workbook.TryGetSheet("Effects", out var effectSheet);
            characterSheet.TryCreateTable(out var characters, out _);
            skillSheet.TryCreateTable(out var skills, out _);
            effectSheet.TryCreateTable(out var effects, out _);
            ReadCharacters(characters, document, warnings, errors);
            ReadSkills(skills, document, warnings, errors);
            ReadEffects(effects, document, warnings, errors);
            ValidateReferences(document, errors);

            json = errors.Count == 0 ? JsonUtility.ToJson(document, true) + "\n" : string.Empty;
            return errors.Count == 0;
        }

        private static void ReadCharacters(XlsxTableData table, CharacterDataDocument document,
            ICollection<string> warnings, ICollection<string> errors)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in table.Rows)
            {
                var id = Text(table, row, "characterId");
                if (string.IsNullOrEmpty(id)) continue;
                if (!ids.Add(id)) { errors.Add($"Characters 第 {row.ExcelRowNumber} 列：角色 ID「{id}」重複。"); continue; }
                if (!PositiveInt(table.GetString(row, "climaxLimit"), out var limit))
                {
                    errors.Add($"Characters 第 {row.ExcelRowNumber} 列：climaxLimit 必須是正整數。");
                    continue;
                }
                var name = Text(table, row, "displayName");
                if (string.IsNullOrEmpty(name)) warnings.Add($"角色「{id}」沒有 displayName。");
                document.characters.Add(new CharacterDefinition
                {
                    id = id, displayName = name, tags = Text(table, row, "tags"), climaxLimit = limit,
                    activeSkill1Id = Text(table, row, "activeSkill1Id"),
                    activeSkill2Id = Text(table, row, "activeSkill2Id"),
                    activeSkill3Id = Text(table, row, "activeSkill3Id"),
                    passiveSkillId = Text(table, row, "passiveSkillId")
                });
            }
        }

        private static void ReadSkills(XlsxTableData table, CharacterDataDocument document,
            ICollection<string> warnings, ICollection<string> errors)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in table.Rows)
            {
                var id = Text(table, row, "skillId");
                if (string.IsNullOrEmpty(id)) continue;
                if (!ids.Add(id)) { errors.Add($"Skills 第 {row.ExcelRowNumber} 列：技能 ID「{id}」重複。"); continue; }
                var pattern = Text(table, row, "pattern");
                if (string.IsNullOrEmpty(pattern))
                    warnings.Add($"技能「{id}」沒有 pattern，會視為尚未完成的資料。");
                else if (!PatternExpressionParser.TryParse(pattern, out _, out var parseError))
                    errors.Add($"技能「{id}」的 pattern 不合法：{parseError}");

                var behavior = Text(table, row, "resourceBehavior");
                if (!string.IsNullOrEmpty(behavior) && !behavior.Equals("Check", StringComparison.OrdinalIgnoreCase) &&
                    !behavior.Equals("Tap", StringComparison.OrdinalIgnoreCase) &&
                    !behavior.Equals("Consume", StringComparison.OrdinalIgnoreCase))
                    errors.Add($"技能「{id}」的 resourceBehavior「{behavior}」不支援，請使用 Check、Tap 或 Consume。");

                document.skills.Add(new SkillDefinition
                {
                    id = id, displayName = Text(table, row, "displayName"),
                    skillType = Text(table, row, "skillType"), pattern = pattern,
                    resourceBehavior = behavior, effectIds = Split(table.GetString(row, "effectId")),
                    designStatus = Text(table, row, "designStatus")
                });
            }
        }

        private static void ReadEffects(XlsxTableData table, CharacterDataDocument document,
            ICollection<string> warnings, ICollection<string> errors)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in table.Rows)
            {
                var id = Text(table, row, "effectId");
                if (string.IsNullOrEmpty(id)) continue;
                if (!ids.Add(id)) { errors.Add($"Effects 第 {row.ExcelRowNumber} 列：效果 ID「{id}」重複。"); continue; }
                var durationText = Text(table, row, "durationTurns");
                var duration = 0;
                if (!string.IsNullOrEmpty(durationText) && !NonNegativeInt(durationText, out duration))
                    errors.Add($"效果「{id}」的 durationTurns 必須是 0 以上的整數或留白。");
                var type = Text(table, row, "effectType");
                var target = Text(table, row, "target");
                var value = Text(table, row, "value");
                if (string.IsNullOrEmpty(type)) warnings.Add($"效果「{id}」沒有 effectType，會視為尚未完成的資料。");
                else if (type.Equals("climax_delta", StringComparison.OrdinalIgnoreCase) &&
                         (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(value)))
                    errors.Add($"效果「{id}」是 climax_delta，target 與 value 不可留白。");
                document.effects.Add(new EffectDefinition
                    { id = id, effectType = type, target = target, value = value, durationTurns = duration });
            }
        }

        private static void ValidateReferences(CharacterDataDocument document, ICollection<string> errors)
        {
            var skillIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var skill in document.skills) skillIds.Add(skill.id);
            var effectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var effect in document.effects) effectIds.Add(effect.id);
            foreach (var character in document.characters)
            {
                Reference(character.id, "activeSkill1Id", character.activeSkill1Id, skillIds, errors);
                Reference(character.id, "activeSkill2Id", character.activeSkill2Id, skillIds, errors);
                Reference(character.id, "activeSkill3Id", character.activeSkill3Id, skillIds, errors);
                Reference(character.id, "passiveSkillId", character.passiveSkillId, skillIds, errors);
            }
            foreach (var skill in document.skills)
                foreach (var effectId in skill.effectIds)
                    if (!effectIds.Contains(effectId))
                        errors.Add($"技能「{skill.id}」引用了不存在的效果「{effectId}」。");
        }

        private static void Reference(string owner, string field, string value, ISet<string> valid,
            ICollection<string> errors)
        {
            if (!string.IsNullOrEmpty(value) && !valid.Contains(value))
                errors.Add($"角色「{owner}」的 {field} 引用了不存在的技能「{value}」。");
        }

        private static bool HasSchema(XlsxWorkbookData workbook, string name, string[] headers,
            ICollection<string> errors)
        {
            if (!workbook.TryGetSheet(name, out var sheet))
            {
                errors.Add($"缺少必要分頁「{name}」。");
                return false;
            }
            if (!sheet.TryCreateTable(out var table, out var tableError))
            {
                errors.Add($"分頁「{name}」無法讀取：{tableError}");
                return false;
            }
            var missing = new List<string>();
            foreach (var header in headers)
                if (!table.HasHeaders(header)) missing.Add(header);
            if (missing.Count == 0) return true;
            errors.Add($"分頁「{name}」缺少欄位：{string.Join(", ", missing)}。");
            return false;
        }

        private static List<string> Split(string value)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(value)) return result;
            foreach (var part in value.Split(new[] { ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries))
                if (!string.IsNullOrWhiteSpace(part)) result.Add(part.Trim());
            return result;
        }

        private static string Text(XlsxTableData table, XlsxRowData row, string header) =>
            (table.GetString(row, header) ?? string.Empty).Trim();

        private static bool PositiveInt(string value, out int result) => Integer(value, out result) && result > 0;
        private static bool NonNegativeInt(string value, out int result) => Integer(value, out result) && result >= 0;
        private static bool Integer(string value, out int result)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result)) return true;
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
                number == Math.Truncate(number) && number >= int.MinValue && number <= int.MaxValue)
            { result = (int)number; return true; }
            result = 0;
            return false;
        }
    }
}
