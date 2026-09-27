using System;
using System.Text;

namespace LibraryOfRuina.features.moontext;

internal static class MoonTextBbcodeParser
{
    internal readonly record struct ParsedCharacterStream(
        IReadOnlyList<string> WrappedCharacters,
        IReadOnlyList<char> RawCharacters);

    public static ParsedCharacterStream ParsePerCharacter(string input)
    {
        List<string> wrappedCharacters = [];
        List<char> rawCharacters = [];
        List<string> colorStack = [];

        int i = 0;
        while (i < input.Length)
        {
            if (input[i] == '[')
            {
                int end = input.IndexOf(']', i);
                if (end < 0)
                {
                    break;
                }

                string tag = input.Substring(i, end - i + 1);
                if (tag.StartsWith("[color=", StringComparison.Ordinal))
                {
                    colorStack.Add(tag);
                }
                else if (string.Equals(tag, "[/color]", StringComparison.Ordinal))
                {
                    if (colorStack.Count > 0)
                    {
                        colorStack.RemoveAt(colorStack.Count - 1);
                    }
                }

                i = end + 1;
                continue;
            }

            char character = input[i];
            rawCharacters.Add(character);

            StringBuilder wrapped = new StringBuilder(32);
            foreach (string tag in colorStack)
            {
                wrapped.Append(tag);
            }

            wrapped.Append(character);

            for (int j = colorStack.Count - 1; j >= 0; j--)
            {
                wrapped.Append("[/color]");
            }

            wrappedCharacters.Add(wrapped.ToString());
            i++;
        }

        return new ParsedCharacterStream(wrappedCharacters, rawCharacters);
    }
}
