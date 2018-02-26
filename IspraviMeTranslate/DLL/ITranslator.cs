﻿using System.Collections.Generic;
using System.Text;

namespace SubtitleEdit
{
    interface ITranslator
    {
        string GetName();
        string GetUrl();
        string Translate(string sourceLanguage, string targetLanguage, string text, StringBuilder log);
        string Key { get; set; }
    }
}
