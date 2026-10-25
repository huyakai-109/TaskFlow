using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace TaskFlow.Common.Helpers
{
    public static class EnumHelper<T>
    {
        public static IList<T> GetValues(Enum value)
        {
            var enumValues = new List<T>();

            foreach (var fi in value.GetType().GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                enumValues.Add((T)Enum.Parse(value.GetType(), fi.Name, false));
            }

            return enumValues;
        }

        public static T ToEnum(string value)
        {
            if (Enum.TryParse(typeof(T), value, true, out var result))
            {
                return (T)result!;
            }

            return default!;
        }

        public static T ToEnum(int? value)
        {
            if (!value.HasValue || !Enum.IsDefined(typeof(T), value))
            {
                return default!;
            }

            return ToEnum(Enum.GetName(typeof(T), value)!);
        }

        public static IList<string> GetNames(Enum value)
        {
            return value.GetType().GetFields(BindingFlags.Static | BindingFlags.Public).Select(fi => fi.Name).ToList();
        }

        public static IList<string> GetDisplayValues(Enum value)
        {
            return GetNames(value).Select(GetDisplayValue).ToList();
        }

        public static string GetDisplayValue(T value)
        {
            var fieldName = value!.ToString();

            return GetDisplayValue(fieldName!);
        }

        public static string GetDisplayValue(int? value)
        {
            if (!value.HasValue || !Enum.IsDefined(typeof(T), value))
            {
                return string.Empty;
            }

            var fieldName = ((T)(object)value).ToString();

            return GetDisplayValue(fieldName!);
        }

        public static string GetDisplayValue(string value)
        {
            var fieldInfo = typeof(T).GetField(value);

            if (fieldInfo == null
                || fieldInfo.GetCustomAttributes(typeof(DisplayAttribute), false) is not DisplayAttribute[] descriptionAttributes)
            {
                return string.Empty;
            }

            if (descriptionAttributes.Length > 0 && descriptionAttributes[0].ResourceType != null)
            {
                return CommonHelper.LookupResource(
                    descriptionAttributes[0].ResourceType!,
                    descriptionAttributes[0].Name!);
            }

            return descriptionAttributes.Length > 0
                ? descriptionAttributes[0].Name ?? string.Empty
                : value;
        }

        public static IList<T> GetEnums(int[] values)
        {
            return values.Cast<T>().ToList();
        }
    }
}
