using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SRPlugin
{
    public class StringListTypeConverter : TypeConverter
    {
        private static string EscapeString(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string UnescapeString(string value)
        {
            return value.Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        public StringListTypeConverter()
        {
            this.ConvertToString = (object value, Type type) =>
            {
                if (value == null)
                {
                    return "[]";
                }

                if (value is string[] stringArray)
                {
                    if (stringArray.Length == 0)
                    {
                        return "[]";
                    }

                    var stringListBuilder = new StringBuilder();
                    stringListBuilder.Append("[");

                    for (int i = 0; i < stringArray.Length; i++)
                    {
                        stringListBuilder.Append("\"");
                        stringListBuilder.Append(EscapeString(stringArray[i]));
                        stringListBuilder.Append("\"");

                        if (i < stringArray.Length - 1)
                        {
                            stringListBuilder.Append(", ");
                        }
                    }

                    stringListBuilder.Append("]");

                    return stringListBuilder.ToString();
                }

                return "[]";
            };

            this.ConvertToObject = (string value, Type type) =>
            {
                if (string.IsNullOrEmpty(value) || value == "[]")
                {
                    return new string[0];
                }

                var content = value.Trim('[', ']').Trim();
                var matches = Regex.Matches(content, "\"(.*?)\"");

                var result = new List<string>();

                foreach (Match match in matches)
                {
                    result.Add(UnescapeString(match.Groups[1].Value));
                }

                return result.ToArray();
            };
        }
    }
}
