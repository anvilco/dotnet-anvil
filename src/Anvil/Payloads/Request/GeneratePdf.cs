using System;
using System.Collections.Generic;

using Anvil.Payloads.Request.Types;

namespace Anvil.Payloads.Request
{
    public class GeneratePdf
    {
        private object? _data;
        private string? _type;

        public string? Title { get; set; }
        public object? Logo { get; set; }

        public string? Type
        {
            get => _type;
            set
            {
                if (value != null && (value.ToLower() == "markdown" || value.ToLower() == "html"))
                {
                    _type = value;
                    return;
                }

                throw new ArgumentException("value must be either `markdown` or `html`");
            }
        }

        // Union types not really a thing in C#, but `Data` must be something like:
        // `GeneratePDFHtml|Array<IGeneratePDFListable>`
        public object? Data
        {
            get => _data;
            set
            {
                if (value is List<IGeneratePdfListable> | value is GeneratePdfHtml)
                {
                    _data = value;
                }
                else
                {
                    throw new ArgumentException("data provided must be a `List` or `GeneratePdfHtml` instance");
                }
            }
        }
    }
}