using System;
using System.Collections.Generic;
using CommandLine;

namespace bms2csv.Model;

public class CommandLineOption
{
    private const string ViewerVerbSetName = "ViewerVerb";
    
    [Option('V', Default = false, HelpText = "ビューアモードとして起動")]
    public bool IsViewerMode { get; set; }
    
    [Option('P', SetName = ViewerVerbSetName, HelpText = "通常再生（ビューアモード用）")]
    public bool PlayBack { get; set; }
    
    [Option('R', SetName = ViewerVerbSetName, HelpText = "ループ再生（ビューアモード用）")]
    public bool Loop { get; set; }
    
    [Option('S', SetName = ViewerVerbSetName, HelpText = "何もせず終了（ビューアモード用）")]
    public bool Shutdown { get; set; }
    
    [Option('N', Default = 0, HelpText = "開始小節（ビューアモード用）")]
    public int Measure { get; set; }
    
    // ハイフンなしの指定オプション
    [Value(0)]
    public IEnumerable<string> ValueOptions { get; set; }
}