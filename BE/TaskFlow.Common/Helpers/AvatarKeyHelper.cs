namespace TaskFlow.Common.Helpers
{
    // f08-accounts-profile.md "Avatars on the existing StorageService": the
    // object key never contains the uploaded filename (no path traversal,
    // no leaked original name) - it is server-generated from the sniffed
    // type, and its stem doubles as the cache-busting "v" query value.
    public static class AvatarKeyHelper
    {
        public static string BuildKey(long userId, Guid id, string extension) => $"avatars/{userId}/{id:N}.{extension}";

        public static string? ExtractVersion(string? objectKey) =>
            string.IsNullOrEmpty(objectKey) ? null : System.IO.Path.GetFileNameWithoutExtension(objectKey);

        public static string? BuildUrl(long userId, string? objectKey)
        {
            var version = ExtractVersion(objectKey);
            return version == null ? null : $"/api/v1/users/{userId}/avatar?v={version}";
        }
    }
}
