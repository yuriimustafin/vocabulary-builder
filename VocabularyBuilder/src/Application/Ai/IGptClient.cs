using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VocabularyBuilder.Application.Ai;
public interface IGptClient
{
    Task<string?> SendMessageAsync(string message);
}
