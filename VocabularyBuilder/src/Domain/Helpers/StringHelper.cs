using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
}
