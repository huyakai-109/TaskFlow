using System.Globalization;
using System.Text;

namespace TaskFlow.Common.Helpers
{
    // f04-activity-feed.md "Request": an opaque base64url encoding of the
    // last row's (CreatedAt, Id) - order CreatedAt DESC, Id DESC (R2, AC2).
    // Never meant to be read by the client; undecodable input is a
    // validation error (Activity.GetList.InvalidCursor), never a thrown
    // exception reaching the controller's generic catch.
    public static class ActivityCursor
    {
        private const string Separator = "|";

        public static string Encode(DateTimeOffset createdAt, long id)
        {
            var raw = $"{createdAt.UtcDateTime:O}{Separator}{id}";
            var bytes = Encoding.UTF8.GetBytes(raw);
            return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        public static bool TryDecode(string? cursor, out DateTimeOffset createdAt, out long id)
        {
            createdAt = default;
            id = default;

            if (string.IsNullOrEmpty(cursor))
            {
                return false;
            }

            try
            {
                var base64 = cursor.Replace('-', '+').Replace('_', '/');
                base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
                var raw = Encoding.UTF8.GetString(Convert.FromBase64String(base64));

                var parts = raw.Split(Separator);
                if (parts.Length != 2)
                {
                    return false;
                }

                if (!DateTime.TryParseExact(parts[0], "O", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedDate))
                {
                    return false;
                }

                if (!long.TryParse(parts[1], out var parsedId))
                {
                    return false;
                }

                createdAt = new DateTimeOffset(parsedDate, TimeSpan.Zero);
                id = parsedId;
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
