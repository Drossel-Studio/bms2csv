using System;
using System.Collections.Generic;
using System.Linq;

namespace bms2csv.Model;

public class ViewerModeValueOption
{
    public string FileName { get; private set; }
    public string ExeName { get; private set; }

    public ViewerModeValueOption(IEnumerable<string> valueOptions)
    {
        var listOptions = valueOptions.ToList();

        this.FileName = listOptions[0];
        this.ExeName = listOptions[1] ?? "Miyabi.exe";
    }
}