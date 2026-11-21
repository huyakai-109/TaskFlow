using System.Text;

namespace TaskFlow.BusinessLogic.Reports
{
    // f09-reports.md AC19: UTF-8 BOM, comma-separated, CRLF line endings,
    // and formula-injection escaping (a cell starting with = + - @ or a tab
    // is prefixed with ' before being opened in a spreadsheet application).
    public static class CsvBuilder
    {
        private static readonly char[] FormulaTriggers = ['=', '+', '-', '@', '\t'];

        public static byte[] Build(IReadOnlyList<string> header, IReadOnlyList<string[]> rows)
        {
            var builder = new StringBuilder();
            builder.Append(string.Join(',', header.Select(Escape)));
            builder.Append("\r\n");

            foreach (var row in rows)
            {
                builder.Append(string.Join(',', row.Select(Escape)));
                builder.Append("\r\n");
            }

            var preamble = Encoding.UTF8.GetPreamble();
            var body = Encoding.UTF8.GetBytes(builder.ToString());
            var result = new byte[preamble.Length + body.Length];
            preamble.CopyTo(result, 0);
            body.CopyTo(result, preamble.Length);
            return result;
        }

        private static string Escape(string value)
        {
            var cell = value;
            if (cell.Length > 0 && FormulaTriggers.Contains(cell[0]))
            {
                cell = "'" + cell;
            }

            if (cell.Contains(',') || cell.Contains('"') || cell.Contains('\n') || cell.Contains('\r'))
            {
                cell = "\"" + cell.Replace("\"", "\"\"") + "\"";
            }

            return cell;
        }
    }
}
