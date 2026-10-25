namespace KanaanFlow.Web.Services;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;

/// <summary>
/// Writes a plain .xlsx workbook: text and number cells, a bold header row, optional right-to-left sheets.
/// </summary>
public sealed class XlsxWriter
{
    private readonly List<Sheet> sheets = new List<Sheet>();

    public void AddSheet(string name, IReadOnlyList<IReadOnlyList<object?>> rows, bool rightToLeft)
    {
        sheets.Add(new Sheet(name, rows, rightToLeft));
    }

    public byte[] ToBytes()
    {
        using MemoryStream output = new MemoryStream();
        using (ZipArchive zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "[Content_Types].xml", ContentTypes());
            Write(zip, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>"
                + "</Relationships>");
            Write(zip, "xl/workbook.xml", Workbook());
            Write(zip, "xl/_rels/workbook.xml.rels", WorkbookRels());
            Write(zip, "xl/styles.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
                + "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>"
                + "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>"
                + "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>"
                + "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>"
                + "<cellXfs count=\"3\">"
                + "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>"
                + "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>"
                + "<xf numFmtId=\"4\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>"
                + "</cellXfs><cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>");
            for (int i = 0; i < sheets.Count; i++)
            {
                Write(zip, "xl/worksheets/sheet" + (i + 1).ToString(CultureInfo.InvariantCulture) + ".xml", SheetXml(sheets[i]));
            }
        }

        return output.ToArray();
    }

    private static void Write(ZipArchive zip, string path, string content)
    {
        ZipArchiveEntry entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        byte[] bytes = new UTF8Encoding(false).GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }

    private string ContentTypes()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
        sb.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
        sb.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
        sb.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
        sb.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
        for (int i = 0; i < sheets.Count; i++)
        {
            sb.Append("<Override PartName=\"/xl/worksheets/sheet").Append(i + 1)
              .Append(".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        }

        sb.Append("</Types>");
        return sb.ToString();
    }

    private string Workbook()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
        for (int i = 0; i < sheets.Count; i++)
        {
            sb.Append("<sheet name=\"").Append(Escape(SafeSheetName(sheets[i].Name))).Append("\" sheetId=\"").Append(i + 1)
              .Append("\" r:id=\"rId").Append(i + 1).Append("\"/>");
        }

        sb.Append("</sheets></workbook>");
        return sb.ToString();
    }

    private string WorkbookRels()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        for (int i = 0; i < sheets.Count; i++)
        {
            sb.Append("<Relationship Id=\"rId").Append(i + 1)
              .Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet")
              .Append(i + 1).Append(".xml\"/>");
        }

        sb.Append("<Relationship Id=\"rId").Append(sheets.Count + 1)
          .Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
        sb.Append("</Relationships>");
        return sb.ToString();
    }

    private static string SheetXml(Sheet sheet)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        sb.Append("<sheetViews><sheetView workbookViewId=\"0\"").Append(sheet.RightToLeft ? " rightToLeft=\"1\"" : string.Empty).Append("/></sheetViews>");
        sb.Append("<sheetData>");
        for (int r = 0; r < sheet.Rows.Count; r++)
        {
            int rowNumber = r + 1;
            sb.Append("<row r=\"").Append(rowNumber).Append("\">");
            IReadOnlyList<object?> row = sheet.Rows[r];
            for (int c = 0; c < row.Count; c++)
            {
                string reference = ColumnName(c) + rowNumber.ToString(CultureInfo.InvariantCulture);
                object? value = row[c];
                string style = r == 0 ? " s=\"1\"" : string.Empty;
                if (value is decimal number)
                {
                    sb.Append("<c r=\"").Append(reference).Append("\"").Append(r == 0 ? style : " s=\"2\"").Append("><v>")
                      .Append(number.ToString(CultureInfo.InvariantCulture)).Append("</v></c>");
                }
                else if (value != null)
                {
                    sb.Append("<c r=\"").Append(reference).Append("\" t=\"inlineStr\"").Append(style).Append("><is><t xml:space=\"preserve\">")
                      .Append(Escape(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)).Append("</t></is></c>");
                }
            }

            sb.Append("</row>");
        }

        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
    }

    private static string ColumnName(int index)
    {
        string name = string.Empty;
        int n = index + 1;
        while (n > 0)
        {
            int rem = (n - 1) % 26;
            name = (char)('A' + rem) + name;
            n = (n - 1) / 26;
        }

        return name;
    }

    private static string SafeSheetName(string name)
    {
        StringBuilder sb = new StringBuilder();
        foreach (char ch in name)
        {
            if ("[]:*?/\\".IndexOf(ch) < 0)
            {
                sb.Append(ch);
            }
        }

        string result = sb.ToString();
        return result.Length > 31 ? result.Substring(0, 31) : (result.Length == 0 ? "Sheet" : result);
    }

    private static string Escape(string text)
    {
        StringBuilder sb = new StringBuilder(text.Length);
        foreach (char ch in text)
        {
            // XML 1.0 forbids most control characters.
            if (ch < 0x20 && ch != '\t' && ch != '\n' && ch != '\r')
            {
                continue;
            }

            sb.Append(ch);
        }

        return SecurityElement.Escape(sb.ToString()) ?? string.Empty;
    }

    private sealed class Sheet
    {
        public Sheet(string name, IReadOnlyList<IReadOnlyList<object?>> rows, bool rightToLeft)
        {
            Name = name;
            Rows = rows;
            RightToLeft = rightToLeft;
        }

        public string Name { get; }

        public IReadOnlyList<IReadOnlyList<object?>> Rows { get; }

        public bool RightToLeft { get; }
    }
}
