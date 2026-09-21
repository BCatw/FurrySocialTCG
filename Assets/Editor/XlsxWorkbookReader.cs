using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace FurrySocialCard.EditorTools
{
    internal sealed class XlsxWorkbookData
    {
        private readonly Dictionary<string, XlsxSheetData> sheetsByName =
            new Dictionary<string, XlsxSheetData>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<XlsxSheetData> Sheets { get; }

        public XlsxWorkbookData(List<XlsxSheetData> sheets)
        {
            Sheets = sheets;
            foreach (XlsxSheetData sheet in sheets) sheetsByName[sheet.Name] = sheet;
        }

        public bool TryGetSheet(string name, out XlsxSheetData sheet)
        {
            return sheetsByName.TryGetValue(name, out sheet);
        }
    }

    internal sealed class XlsxSheetData
    {
        public string Name { get; }
        public IReadOnlyList<XlsxRowData> Rows { get; }

        public XlsxSheetData(string name, List<XlsxRowData> rows)
        {
            Name = name;
            Rows = rows;
        }

        public bool TryCreateTable(out XlsxTableData table, out string error)
        {
            table = null;
            error = null;
            if (Rows.Count == 0)
            {
                error = $"Sheet '{Name}' has no rows.";
                return false;
            }

            XlsxRowData headerRow = Rows[0];
            var headers = new List<string>();
            var headerIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int lastHeaderColumn = headerRow.LastPopulatedColumn;
            for (int column = 0; column <= lastHeaderColumn; column++)
            {
                string header = headerRow.GetString(column)?.Trim();
                if (string.IsNullOrWhiteSpace(header))
                {
                    error = $"Sheet '{Name}' has an empty header at column {column + 1}.";
                    return false;
                }
                if (headerIndexes.ContainsKey(header))
                {
                    error = $"Sheet '{Name}' has duplicate header '{header}'.";
                    return false;
                }
                headerIndexes.Add(header, column);
                headers.Add(header);
            }

            var dataRows = new List<XlsxRowData>();
            for (int index = 1; index < Rows.Count; index++)
            {
                if (!Rows[index].IsEmpty) dataRows.Add(Rows[index]);
            }
            table = new XlsxTableData(Name, headers, headerIndexes, dataRows);
            return true;
        }
    }

    internal sealed class XlsxTableData
    {
        private readonly Dictionary<string, int> headerIndexes;

        public string SheetName { get; }
        public IReadOnlyList<string> Headers { get; }
        public IReadOnlyList<XlsxRowData> Rows { get; }

        public XlsxTableData(
            string sheetName,
            List<string> headers,
            Dictionary<string, int> headerIndexes,
            List<XlsxRowData> rows)
        {
            SheetName = sheetName;
            Headers = headers;
            this.headerIndexes = headerIndexes;
            Rows = rows;
        }

        public bool HasHeaders(params string[] required)
        {
            return required.All(headerIndexes.ContainsKey);
        }

        public string GetString(XlsxRowData row, string header)
        {
            return headerIndexes.TryGetValue(header, out int index) ? row.GetString(index) : null;
        }

        public XlsxCellData GetCell(XlsxRowData row, string header)
        {
            return headerIndexes.TryGetValue(header, out int index) ? row.GetCell(index) : XlsxCellData.Empty;
        }
    }

    internal sealed class XlsxRowData
    {
        private readonly Dictionary<int, XlsxCellData> cells;

        public int ExcelRowNumber { get; }
        public bool IsEmpty => cells.Count == 0 || cells.Values.All(cell => cell.IsBlank);
        public int LastPopulatedColumn => cells.Count == 0 ? -1 : cells.Keys.Max();

        public XlsxRowData(int excelRowNumber, Dictionary<int, XlsxCellData> cells)
        {
            ExcelRowNumber = excelRowNumber;
            this.cells = cells;
        }

        public XlsxCellData GetCell(int column)
        {
            return cells.TryGetValue(column, out XlsxCellData cell) ? cell : XlsxCellData.Empty;
        }

        public string GetString(int column)
        {
            return GetCell(column).Text;
        }
    }

    internal readonly struct XlsxCellData
    {
        public static readonly XlsxCellData Empty = new XlsxCellData(null, XlsxCellKind.Blank);

        public string Text { get; }
        public XlsxCellKind Kind { get; }
        public bool IsBlank => string.IsNullOrEmpty(Text);

        public XlsxCellData(string text, XlsxCellKind kind)
        {
            Text = text;
            Kind = kind;
        }
    }

    internal enum XlsxCellKind
    {
        Blank,
        String,
        Number,
        Boolean
    }

    internal static class XlsxWorkbookReader
    {
        private static readonly XNamespace SpreadsheetNamespace =
            "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace OfficeRelationshipNamespace =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PackageRelationshipNamespace =
            "http://schemas.openxmlformats.org/package/2006/relationships";

        public static XlsxWorkbookData Read(string absolutePath)
        {
            using (FileStream stream = File.Open(absolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                List<string> sharedStrings = ReadSharedStrings(archive);
                XDocument workbook = LoadXml(archive, "xl/workbook.xml");
                XDocument relationships = LoadXml(archive, "xl/_rels/workbook.xml.rels");
                Dictionary<string, string> targets = relationships
                    .Root?
                    .Elements(PackageRelationshipNamespace + "Relationship")
                    .ToDictionary(
                        element => (string)element.Attribute("Id"),
                        element => NormalizeWorksheetPath((string)element.Attribute("Target")))
                    ?? new Dictionary<string, string>();

                var sheets = new List<XlsxSheetData>();
                IEnumerable<XElement> sheetElements = workbook
                    .Descendants(SpreadsheetNamespace + "sheet");
                foreach (XElement sheetElement in sheetElements)
                {
                    string name = (string)sheetElement.Attribute("name");
                    string relationshipId = (string)sheetElement.Attribute(OfficeRelationshipNamespace + "id");
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(relationshipId)
                        || !targets.TryGetValue(relationshipId, out string worksheetPath)) continue;
                    sheets.Add(ReadSheet(archive, worksheetPath, name, sharedStrings));
                }
                return new XlsxWorkbookData(sheets);
            }
        }

        private static XlsxSheetData ReadSheet(
            ZipArchive archive,
            string worksheetPath,
            string name,
            IReadOnlyList<string> sharedStrings)
        {
            XDocument document = LoadXml(archive, worksheetPath);
            var rows = new List<XlsxRowData>();
            foreach (XElement rowElement in document.Descendants(SpreadsheetNamespace + "row"))
            {
                int excelRow = ParseInt((string)rowElement.Attribute("r"), rows.Count + 1);
                var cells = new Dictionary<int, XlsxCellData>();
                int fallbackColumn = 0;
                foreach (XElement cellElement in rowElement.Elements(SpreadsheetNamespace + "c"))
                {
                    string reference = (string)cellElement.Attribute("r");
                    int column = string.IsNullOrEmpty(reference) ? fallbackColumn : ColumnIndex(reference);
                    fallbackColumn = column + 1;
                    XlsxCellData cell = ReadCell(cellElement, sharedStrings);
                    if (!cell.IsBlank) cells[column] = cell;
                }
                rows.Add(new XlsxRowData(excelRow, cells));
            }
            return new XlsxSheetData(name, rows);
        }

        private static XlsxCellData ReadCell(XElement cell, IReadOnlyList<string> sharedStrings)
        {
            string type = (string)cell.Attribute("t");
            string value = (string)cell.Element(SpreadsheetNamespace + "v");
            if (type == "inlineStr")
            {
                value = string.Concat(cell
                    .Descendants(SpreadsheetNamespace + "t")
                    .Select(element => element.Value));
                return new XlsxCellData(value, XlsxCellKind.String);
            }
            if (type == "s" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
            {
                return index >= 0 && index < sharedStrings.Count
                    ? new XlsxCellData(sharedStrings[index], XlsxCellKind.String)
                    : XlsxCellData.Empty;
            }
            if (type == "b")
            {
                return new XlsxCellData(value == "1" ? "true" : "false", XlsxCellKind.Boolean);
            }
            if (type == "str" || type == "e") return new XlsxCellData(value, XlsxCellKind.String);
            return string.IsNullOrEmpty(value)
                ? XlsxCellData.Empty
                : new XlsxCellData(value, XlsxCellKind.Number);
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null) return new List<string>();
            using (Stream stream = entry.Open())
            {
                XDocument document = XDocument.Load(stream);
                return document
                    .Descendants(SpreadsheetNamespace + "si")
                    .Select(item => string.Concat(item
                        .Descendants(SpreadsheetNamespace + "t")
                        .Select(text => text.Value)))
                    .ToList();
            }
        }

        private static XDocument LoadXml(ZipArchive archive, string path)
        {
            ZipArchiveEntry entry = archive.GetEntry(path.Replace('\\', '/'));
            if (entry == null) throw new InvalidDataException($"XLSX entry is missing: {path}");
            using (Stream stream = entry.Open()) return XDocument.Load(stream);
        }

        private static string NormalizeWorksheetPath(string target)
        {
            if (string.IsNullOrWhiteSpace(target)) return target;
            string normalized = target.Replace('\\', '/').TrimStart('/');
            return normalized.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
                ? normalized
                : "xl/" + normalized.TrimStart('.', '/');
        }

        private static int ColumnIndex(string reference)
        {
            int index = 0;
            int position = 0;
            while (position < reference.Length && char.IsLetter(reference[position]))
            {
                index = index * 26 + (char.ToUpperInvariant(reference[position]) - 'A' + 1);
                position++;
            }
            return Math.Max(0, index - 1);
        }

        private static int ParseInt(string value, int fallback)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
                ? result
                : fallback;
        }
    }
}
