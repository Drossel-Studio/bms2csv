using System;
using System.Collections.Generic;
using System.Linq;

namespace bms2csv.Model;

public class ConverterModeValueOption
{
    public string InputPath { get; private set; }
    public string OutputPath { get; private set; }

    public ConverterModeValueOption(IEnumerable<string> valueOptions)
    {
        var listOptions = valueOptions.ToList();

        if (listOptions.Count < 2)
        {
            throw new ArgumentException("コンストラクタに渡された要素数が2以下です");
        }

        this.InputPath = listOptions[0];
        this.OutputPath = listOptions[1];
    }
}