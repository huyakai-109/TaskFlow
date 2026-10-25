namespace TaskFlow.Common.Constants
{
    public static class GlobalConstants
    {
        public struct UserToken
        {
            public const string LoginProvider = "Self-hosted";
        }

        public struct RegexPattern
        {
            public const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        }

        public struct Files
        {
            public const string CsvExtension = "csv";

            public const string CsvContentType = "text/csv";

            public const string ExcelExtension = "xlsx";

            public const string ExcelContentType = "application/excel";
        }

        public struct Culture
        {
            public const string English = "en-US";
            public const string Vietnam = "vi-VN";
        }

        public struct SortDirection
        {
            public const string Ascending = "asc";
            public const string Descending = "desc";
        }

        public struct PageConfig
        {
            public const int Start = 1;
            public const int Length = 10;
            public const int MaxLength = 100;
        }

        public struct Images
        {
            public const string JpegExtension = ".jpeg";
            public const string JpgExtension = ".jpg";
            public const string JpgContentType = "image/jpeg";
            public const string PngExtension = ".png";
            public const string PngContentType = "image/png";
            public const string GifExtension = ".gif";
            public const string GifContentType = "image/gif";
            public const string BitMapExtension = ".bmp";
            public const string BitMapContentType = "image/bmp";
            public const string IconExtension = ".ico";
            public const string IconContentType = "image/x-icon";
            public const string SvgExtension = ".svg";
            public const string SvgContentType = "image/svg+xml";
            public const int MaxSize = 5; // MB
        }

        public static Dictionary<string, string> ImageExtToType = new()
        {
            { Images.JpegExtension, Images.JpgContentType },
            { Images.JpgExtension, Images.JpgContentType },
            { Images.PngExtension, Images.PngContentType },
            { Images.GifExtension, Images.GifContentType },
            { Images.BitMapExtension, Images.BitMapContentType },
            { Images.IconExtension, Images.IconContentType },
            { Images.SvgExtension, Images.SvgContentType }
        };

        public struct StorageErrorMessage
        {
            public const string BucketIsNull = "Bucket is null.";
            public const string BucketNotFound = "Bucket not found.";
            public const string UploadFailed = "Upload file failed.";
        }

        public struct Symbol
        {
            public const string ForwardSlash = "/";
            public const string BackSlash = @"\";
            public const string UnderScore = "_";
            public const string Comma = ",";
            public const string Space = " ";
            public const string Dash = "-";
            public const string Question = "?";
            public const string Colon = ":";
            public const string LeftBracket = "[";
            public const string RightBracket = "]";
            public const string Asterisk = "*";
            public const string Dot = ".";
            public const string AtSign = "@";
            public const string SemiColon = ";";
            public const string Hash = "#";
        }
    }
}
