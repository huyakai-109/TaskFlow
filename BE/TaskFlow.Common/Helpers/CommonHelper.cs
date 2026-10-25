using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TaskFlow.Common.Constants;
using static TaskFlow.Common.Constants.GlobalConstants;
using ClaimTypes = System.Security.Claims.ClaimTypes;

namespace TaskFlow.Common.Helpers
{
    public static class CommonHelper
    {
        /// <summary>
        /// Extract data between a symbol
        /// Get first group
        /// </summary>
        /// <param name="text">Source</param>
        /// <param name="pattern">Pattern to search</param>
        /// <param name="idx">Index of matched data</param>
        /// <returns>Text result</returns>
        public static string RegexGroupValue(this string text, string pattern, int? idx = null)
        {
            var regex = new Regex(pattern);
            var match = regex.Match(text);
            return idx != null ? match.Groups[idx.Value].Value : match.Value;
        }

        /// <summary>
        /// Extract data between a symbol
        /// Get last group
        /// </summary>
        /// <param name="text">Source</param>
        /// <param name="pattern">Pattern to search</param>
        /// <param name="idx">Index of matched data</param>
        /// <returns>Text result</returns>
        public static string RegexGroupValueLast(this string text, string pattern, int? idx = null)
        {
            var regex = new Regex(pattern);
            var matches = regex.Matches(text);
            return idx != null ? matches.Last().Groups[idx.Value].Value : matches.Last().Value;
        }

        /// <summary>
        /// Compute an object
        /// </summary>
        /// <param name="toCompute">Object to compute</param>
        /// <returns>Hash</returns>
        public static string ComputeHash(this object toCompute)
        {
            using var md5 = MD5.Create();
            var hashBytes = md5.ComputeHash(toCompute.ToJson().ToByteArray());

            var sb = new StringBuilder();
            foreach (var hashByte in hashBytes)
            {
                sb.Append(hashByte.ToString("x2"));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Compute a string
        /// </summary>
        /// <param name="text">String to compute</param>
        /// <returns>Hash</returns>
        public static string ComputeHash(this string text)
        {
            using var md5 = MD5.Create();
            var hashBytes = md5.ComputeHash(text.ToByteArray());

            var sb = new StringBuilder();
            foreach (var hashByte in hashBytes)
            {
                sb.Append(hashByte.ToString("x2"));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Compare 2 hash
        /// </summary>
        /// <param name="source">Source</param>
        /// <param name="toCompare">Hash to compare</param>
        /// <returns>Compare result</returns>
        public static bool CompareHash(string source, string toCompare)
        {
            return string.IsNullOrEmpty(source) ? string.IsNullOrEmpty(toCompare) : source.Equals(toCompare);
        }

        /// <summary>
        /// Convert 0 to empty, usually use for data exportation
        /// </summary>
        /// <param name="source">Source</param>
        /// <param name="format">Format</param>
        /// <returns>Convert result</returns>
        public static string ConvertZeroToEmpty(this double? source, string format = "0.00")
        {
            if (source == null) return string.Empty;
            return source == 0 ? string.Empty : source.Value.ToString(format);
        }

        /// <summary>
        /// Parse double
        /// </summary>
        /// <param name="source">Source</param>
        /// <param name="func">Function</param>
        /// <param name="defaultValue">Default value</param>
        /// <returns>Parse result, default value if parse error</returns>
        public static double? TryParseDoubleNullable(this string source, Func<string, string>? func, double? defaultValue = 0.0)
        {
            if (func != null)
            {
                source = func(source);
            }

            var canParse = double.TryParse(source, out var result);
            return canParse ? result : defaultValue;
        }

        /// <summary>
        /// Convert to integer
        /// </summary>
        /// <param name="value">Value</param>
        /// <returns>Int</returns>
        public static int ToInt(this Enum value)
        {
            return Convert.ToInt32(value);
        }

        /// <summary>
        /// Convert double to string
        /// </summary>
        /// <param name="source">Source</param>
        /// <param name="format">Format</param>
        /// <returns>Double as string</returns>
        public static string DoubleToString(this double? source, string format = "0.00")
        {
            if (source == null) return string.Empty;
            return source != 0 ? source.Value.ToString(format) : "0";
        }

        /// <summary>
        /// Init array
        /// </summary>
        /// <param name="count">Array size</param>
        /// <param name="defaultValue">Init value</param>
        /// <returns>Array</returns>
        public static T[] InitArray<T>(int count, T defaultValue)
        {
            return Enumerable.Repeat(defaultValue, count).ToArray();
        }

        /// <summary>
        /// Convert to byte array
        /// </summary>
        /// <param name="source">Source</param>
        /// <returns>Byte array</returns>
        public static byte[] ToByteArray(this string source)
        {
            return Encoding.UTF8.GetBytes(source);
        }

        /// <summary>
        /// Convert stream to byte array
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static byte[] ConvertToByteArray(this Stream stream)
        {
            if (stream == null || stream.Length == 0)
            {
                return [];
            }

            var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Convert to utf8
        /// </summary>
        /// <param name="data">Data</param>
        /// <returns>String</returns>
        public static string ToUtf8String(this byte[] data)
        {
            return Encoding.UTF8.GetString(data);
        }

        /// <summary>
        /// Remove spaces
        /// </summary>
        /// <param name="source">Source</param>
        /// <returns>String with no space</returns>
        public static string RemoveSpaces(this string source)
        {
            return source.Replace(" ", "");
        }

        /// <summary>
        /// Check file existence
        /// </summary>
        /// <param name="filePath">FilePath</param>
        /// <param name="cancelToken">Cancellation token</param>
        /// <returns>True if the file exists. Otherwise, False</returns>
        public static async Task<bool> CheckFileExistence(string filePath, CancellationToken? cancelToken = null)
        {
            var attempt = 5;
            while (attempt > 0)
            {
                if (File.Exists(filePath)) return true;

                if (cancelToken.HasValue)
                {
                    await Task.Delay(3000, cancelToken.Value);
                }
                else
                {
                    await Task.Delay(3000);
                }

                attempt--;
            }

            return false;
        }

        /// <summary>
        /// Convert object to JSON string
        /// </summary>
        /// <typeparam name="T">Type of object</typeparam>
        /// <param name="obj">object to convert</param>
        /// <returns>JSON string</returns>
        public static string ToJson<T>(this T? obj)
            where T : class
        {
            return JsonConvert.SerializeObject(obj);
        }

        /// <summary>
        /// Convert json string to object
        /// </summary>
        /// <typeparam name="T">Type of object</typeparam>
        /// <param name="json">JSON string</param>
        /// <returns>Object converted</returns>
        public static T? ToObject<T>(this string json)
            where T : class
        {
            return !string.IsNullOrEmpty(json)
                ? JsonConvert.DeserializeObject<T>(json)
                : null;
        }

        public static T? Copy<T>(this T? obj)
            where T : class
        {
            return obj?.ToJson().ToObject<T>();
        }

        public static bool IsEqual(this object source, object toCompare)
        {
            return source.ToJson().Equals(toCompare.ToJson());
        }

        public static string LookupResource(IReflect resourceManagerProvider, string resourceKey)
        {
            foreach (var staticProperty in resourceManagerProvider.GetProperties(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (staticProperty.PropertyType != typeof(System.Resources.ResourceManager))
                {
                    continue;
                }

                var resourceManager = (System.Resources.ResourceManager)staticProperty.GetValue(null, null)!;
                return resourceManager.GetString(resourceKey)!;
            }

            return resourceKey; // Fallback with the key name
        }

        /// <summary>
        /// Extract userId from claims.
        /// </summary>
        /// <param name="claims">Logged in claims</param>
        /// <returns>UserId</returns>
        public static long GetUserId(this IEnumerable<Claim> claims)
        {
            var claimValue = claims.First(x => x.Type == "nameid" || x.Type == ClaimTypes.NameIdentifier).Value;
            return long.Parse(claimValue);
        }

        /// <summary>
        /// Extract userId nullable from claims.
        /// </summary>
        /// <param name="claims">Logged in claims</param>
        /// <returns>UserId</returns>
        public static long? GetUserIdNullable(this IEnumerable<Claim> claims)
        {
            var claimValue = claims.FirstOrDefault(x => x.Type == "nameid" || x.Type == ClaimTypes.NameIdentifier)?.Value;
            return claimValue != null ? long.Parse(claimValue) : null;
        }

        public static string[] GetUserRoleClaims(this IEnumerable<Claim> claims)
        {
            return claims.Where(x => x.Type == RolePolicies.ClaimType).Select(i => i.Value).ToArray();
        }

        public static bool IsEnglish(this CultureInfo culture)
        {
            return culture.Name == Culture.English;
        }

        public static bool IsVietnamese(this CultureInfo culture)
        {
            return culture.Name == Culture.Vietnam;
        }

        public static string HashPassword(this string password)
        {
            PasswordHasher<string> passwordHasher = new(
                new OptionsWrapper<PasswordHasherOptions>(
                    new PasswordHasherOptions()
                    {
                        CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
                    }));

            string hashPassword = passwordHasher.HashPassword(string.Empty, password);
            return hashPassword;
        }

        public static string GeneratePassword(string regexPattern = UserConstants.Password.RegexPattern, int length = UserConstants.Password.MinLength)
        {
            return "Test!1234";
            //var regex = new Regex(regexPattern, RegexOptions.None);

            //string password;
            //do
            //{
            //    password = GenerateRandomPassword(length);
            //} while (!regex.IsMatch(password));

            //return password;
        }

        private static string GenerateRandomPassword(int length)
        {
            var result = new char[length];
            using var rng = RandomNumberGenerator.Create();
            var randomBytes = new byte[length];
            rng.GetBytes(randomBytes);

            for (int i = 0; i < length; i++)
            {
                int randomIndex = Convert.ToInt32(randomBytes[i] % UserConstants.Password.ValidChars.Length);

                result[i] = UserConstants.Password.ValidChars[randomIndex];
            }

            return new string(result);
        }

        public static string GetFile(this string fileName, string url)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }

            return string.Concat(url, fileName);
        }

        public static object TrimData(this object obj)
        {
            var stringProperties = obj
                .GetType()
                .GetProperties()
                .Where(p => p.PropertyType == typeof(string));
            foreach (var stringProperty in stringProperties)
            {
                var value = stringProperty.GetValue(obj, null);
                if (value != null)
                {
                    string currentValue = (string)value;
                    stringProperty.SetValue(obj, currentValue.Trim(), null);
                }
            }

            return obj;
        }

        public static bool IsValidEmail(this string email)
        {
            var regex = new Regex(RegexPattern.EmailPattern, RegexOptions.IgnoreCase);
            return regex.IsMatch(email);
        }

        public static string FormatDecimal(this decimal? value)
        {
            if (!value.HasValue || (value.HasValue && value == 0))
            {
                return 0.ToString("0.00");
            }

            decimal roundedValue = Math.Floor(value.Value * 100) / 100;
            return roundedValue.ToString("0.00");
        }

        public static string FormatDecimal(this decimal value)
        {
            if (value == 0)
            {
                return 0.ToString("0.00");
            }

            decimal roundedValue = Math.Floor(value * 100) / 100;
            return roundedValue.ToString("0.00");
        }

        public static string FormatCurrency(decimal? currency)
        {
            if (!currency.HasValue || (currency.HasValue && currency == 0))
            {
                return 0.ToString("0");
            }

            var nfi = new NumberFormatInfo
            {
                CurrencyDecimalDigits = 6,
                CurrencySymbol = string.Empty,
                CurrencyGroupSeparator = Symbol.Comma,
                CurrencyDecimalSeparator = Symbol.Dot
            };

            string formattedAmount = currency.Value.ToString("C", nfi).TrimEnd('0').TrimEnd('.');
            return formattedAmount; // string.Format("{0:#.00}", currency.Value / 100);
        }

        public static string Base64Encode(this string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }

        public static string Base64Decode(this string base64EncodedData)
        {
            var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
            return Encoding.UTF8.GetString(base64EncodedBytes);
        }
    }
}
