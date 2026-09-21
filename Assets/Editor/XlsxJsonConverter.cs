using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FurrySocialCard.EditorTools
{
    internal interface IXlsxJsonWorkbookConverter
    {
        string Name { get; }
        bool CanConvert(XlsxWorkbookData workbook);
        bool TryConvert(string sourceAssetPath, XlsxWorkbookData workbook, out string outputAssetPath,
            out string json, out List<string> warnings, out List<string> errors);
    }

    internal static class XlsxJsonConverter
    {
        private const string MenuPath = "Assets/Furry Social Card/Convert XLSX to JSON";
        private static readonly IXlsxJsonWorkbookConverter[] Converters = { new CharacterWorkbookJsonConverter() };

        [MenuItem(MenuPath, false, 2000)]
        private static void ConvertSelected()
        {
            var outputs = new List<string>();
            var failures = new List<string>();
            foreach (var path in SelectedXlsxPaths())
            {
                try { outputs.Add(Convert(path)); }
                catch (Exception exception)
                {
                    failures.Add(path + ": " + exception.Message);
                    Debug.LogError($"XLSX 轉換失敗：{path}\n{exception}");
                }
            }

            AssetDatabase.Refresh();
            if (failures.Count > 0)
                EditorUtility.DisplayDialog("XLSX 轉換未完成",
                    $"成功：{outputs.Count} 個\n失敗：{failures.Count} 個\n\n{string.Join("\n", failures)}\n\n失敗的檔案不會覆蓋既有 JSON。", "確定");
            else
                EditorUtility.DisplayDialog("XLSX 轉換完成",
                    $"已轉換 {outputs.Count} 個檔案：\n\n{string.Join("\n", outputs)}", "確定");
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateMenu() => SelectedXlsxPaths().Count > 0;

        internal static string ConvertWorkbookForTests(string assetPath)
        {
            var output = Convert(assetPath);
            AssetDatabase.Refresh();
            return output;
        }

        private static string Convert(string assetPath)
        {
            var workbook = XlsxWorkbookReader.Read(Path.GetFullPath(assetPath));
            IXlsxJsonWorkbookConverter converter = null;
            foreach (var candidate in Converters)
                if (candidate.CanConvert(workbook)) { converter = candidate; break; }
            converter = converter ?? new GenericWorkbookJsonConverter();

            if (!converter.TryConvert(assetPath, workbook, out var outputPath, out var json,
                    out var warnings, out var errors) || errors.Count > 0)
                throw new InvalidDataException(string.Join("\n", errors));

            var absoluteOutput = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutput) ??
                                      throw new InvalidDataException("無法取得輸出資料夾。"));
            File.WriteAllText(absoluteOutput, json, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
            foreach (var warning in warnings)
                Debug.LogWarning($"XLSX 轉換提醒（{assetPath}）：{warning}");
            Debug.Log($"XLSX 轉換完成（{converter.Name}）：{assetPath} → {outputPath}");
            return outputPath;
        }

        private static List<string> SelectedXlsxPaths()
        {
            var result = new List<string>();
            foreach (var guid in Selection.assetGUIDs)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase))
                    result.Add(path);
            }
            return result;
        }
    }

    internal sealed class GenericWorkbookJsonConverter : IXlsxJsonWorkbookConverter
    {
        public string Name => "通用表格";
        public bool CanConvert(XlsxWorkbookData workbook) => true;

        public bool TryConvert(string sourceAssetPath, XlsxWorkbookData workbook, out string outputAssetPath,
            out string json, out List<string> warnings, out List<string> errors)
        {
            outputAssetPath = Path.ChangeExtension(sourceAssetPath, ".json").Replace('\\', '/');
            warnings = new List<string>();
            errors = new List<string>();
            var tables = new List<KeyValuePair<string, XlsxTableData>>();
            foreach (var sheet in workbook.Sheets)
            {
                if (sheet.TryCreateTable(out var table, out _))
                    tables.Add(new KeyValuePair<string, XlsxTableData>(sheet.Name, table));
                else
                    warnings.Add($"分頁「{sheet.Name}」不是標題列表格，已略過。");
            }

            if (tables.Count == 0)
            {
                json = string.Empty;
                errors.Add("找不到可轉換的表格。每個分頁第一列須為不重複的欄位名稱。");
                return false;
            }

            var b = new StringBuilder("{\n  \"schemaVersion\": 1,\n  \"sourceFile\": ");
            JsonString(b, Path.GetFileName(sourceAssetPath));
            b.Append(",\n  \"sheets\": {");
            for (var t = 0; t < tables.Count; t++)
            {
                var entry = tables[t];
                b.Append(t == 0 ? "\n    " : ",\n    ");
                JsonString(b, entry.Key);
                b.Append(": [");
                for (var r = 0; r < entry.Value.Rows.Count; r++)
                {
                    b.Append(r == 0 ? "\n      {" : ",\n      {");
                    for (var c = 0; c < entry.Value.Headers.Count; c++)
                    {
                        b.Append(c == 0 ? "\n        " : ",\n        ");
                        JsonString(b, entry.Value.Headers[c]);
                        b.Append(": ");
                        JsonCell(b, entry.Value.Rows[r].GetCell(c));
                    }
                    b.Append("\n      }");
                }
                if (entry.Value.Rows.Count > 0) b.Append('\n');
                b.Append("    ]");
            }
            b.Append("\n  }\n}\n");
            json = b.ToString();
            return true;
        }

        private static void JsonCell(StringBuilder b, XlsxCellData cell)
        {
            if (cell.Kind == XlsxCellKind.Blank) { b.Append("null"); return; }
            if (cell.Kind == XlsxCellKind.Boolean)
            {
                b.Append(cell.Text == "1" || cell.Text.Equals("true", StringComparison.OrdinalIgnoreCase)
                    ? "true" : "false");
                return;
            }
            if (cell.Kind == XlsxCellKind.Number &&
                decimal.TryParse(cell.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                b.Append(cell.Text);
            else
                JsonString(b, cell.Text);
        }

        private static void JsonString(StringBuilder b, string value)
        {
            b.Append('"');
            foreach (var ch in value ?? string.Empty)
            {
                switch (ch)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\b': b.Append("\\b"); break;
                    case '\f': b.Append("\\f"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\t': b.Append("\\t"); break;
                    default:
                        if (ch < 32) b.Append("\\u").Append(((int)ch).ToString("x4"));
                        else b.Append(ch);
                        break;
                }
            }
            b.Append('"');
        }
    }
}
