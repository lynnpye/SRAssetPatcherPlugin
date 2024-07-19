using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SRPlugin
{
    public class StringListTypeConverter : TypeConverter
    {
        public static string EscapeDoubleQuotesAndBackslashesUsingRegex(string input)
        {
            // First, escape all backslashes that are not already used for escaping
            // This looks for a backslash that is not followed by another backslash or a double quote
            string escapedBackslashes = System.Text.RegularExpressions.Regex.Replace(input, @"\\(?!\\|"")", @"\\\\");

            // Then, escape all double quotes that are not already escaped
            // After escaping backslashes, we can safely assume that unescaped double quotes are not preceded by a backslash
            return System.Text.RegularExpressions.Regex.Replace(escapedBackslashes, @"(?<!\\)""", @"\""");
        }

        public string[] ParseStringArray(string input)
        {
            // Check if the input starts and ends with square brackets
            if (input.StartsWith("[") && input.EndsWith("]"))
            {
                // Remove the starting and ending square brackets
                var trimmedInput = input.Substring(1, input.Length - 2);

                // Use a regular expression to match all instances of double-quoted strings, taking escaped quotes into account
                var matches = System.Text.RegularExpressions.Regex.Matches(trimmedInput, @"\""(?:\\.|[^\\""])*\""");

                // Convert the matches to a string array
                var result = new string[matches.Count];
                for (int i = 0; i < matches.Count; i++)
                {
                    // Remove the surrounding quotes and unescape any escaped characters for each matched string
                    result[i] = System.Text.RegularExpressions.Regex.Unescape(matches[i].Value.Substring(1, matches[i].Value.Length - 2));
                }

                return result;
            }
            else
            {
                return [];
            }
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

                    var stringifiedStringList = $"[\"{stringArray.ToList().Join(v => EscapeDoubleQuotesAndBackslashesUsingRegex(v), "\", \"")}\"]";

                    return stringifiedStringList;
                }

                return "[]";
            };

            this.ConvertToObject = (string value, Type type) =>
            {
                return ParseStringArray(value);
            };
        }
    }
}
