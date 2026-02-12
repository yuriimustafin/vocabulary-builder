using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VocabularyBuilder.Domain.Helpers;
public static class StringHelper
{
    public static int ExtractDigits(string? str)
    {
        string digitStr = new string(str?.Where(char.IsDigit).ToArray());

        if (!string.IsNullOrEmpty(digitStr))
        {
            return int.Parse(digitStr);
}
        else
        {
            return 0;
        }
    }

    /// <summary>
    /// Extracts the page number from a Kindle heading.
    /// Handles formats like "Highlight(yellow) - Page 6 · Location 144" or "Highlight(blue) - Page 1"
    /// </summary>
    public static string ExtractPageNumber(string? heading)
    {
        if (string.IsNullOrEmpty(heading))
        {
            return "0";
        }

        // Match "Page " followed by digits
        var match = Regex.Match(heading, @"Page\s+(\d+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        return "0";
    }
}
