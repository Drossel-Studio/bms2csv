using bms2csv.Model;
using CommandLine;

namespace TestBms2csv;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void TestConverterMode()
    {
        var args = new[] { "bms_path", "output_path" };
        var cmdOpt = ParseArgs(args);

        Assert.That(cmdOpt.IsViewerMode, Is.EqualTo(false));
        var opt = new ConverterModeValueOption(cmdOpt.ValueOptions);
        Assert.Multiple(() =>
        {
            Assert.That(opt.InputPath, Is.EqualTo("bms_path"));
            Assert.That(opt.OutputPath, Is.EqualTo("output_path"));
        });
    }

    [Test]
    public void TestViewerPlayback()
    {
        var args = new[] { "-V", "-P", "-N", "123", "filePath", "exeName" };
        var cmdOpt = ParseArgs(args);
        Assert.Multiple(() =>
        {
            Assert.That(cmdOpt.IsViewerMode, Is.EqualTo(true));
            Assert.That(cmdOpt.PlayBack, Is.EqualTo(true));
            Assert.That(cmdOpt.Loop, Is.EqualTo(false));
            Assert.That(cmdOpt.Shutdown, Is.EqualTo(false));
            Assert.That(cmdOpt.Measure, Is.EqualTo(123));
        });
        var opt = new ViewerModeValueOption(cmdOpt.ValueOptions);
        Assert.Multiple(() =>
        {
            Assert.That(opt.FileName, Is.EqualTo("filePath"));
            Assert.That(opt.ExeName, Is.EqualTo("exeName"));
        });
    }

    [Test]
    public void TestViewerPlaybackWithoutMeasure()
    {
        var args = new[] { "-V", "-P", "filePath", "exeName" };
        var cmdOpt = ParseArgs(args);
        Assert.Multiple(() =>
        {
            Assert.That(cmdOpt.IsViewerMode, Is.EqualTo(true));
            Assert.That(cmdOpt.PlayBack, Is.EqualTo(true));
            Assert.That(cmdOpt.Loop, Is.EqualTo(false));
            Assert.That(cmdOpt.Shutdown, Is.EqualTo(false));
            Assert.That(cmdOpt.Measure, Is.EqualTo(0));
        });
        var opt = new ViewerModeValueOption(cmdOpt.ValueOptions);
        Assert.Multiple(() =>
        {
            Assert.That(opt.FileName, Is.EqualTo("filePath"));
            Assert.That(opt.ExeName, Is.EqualTo("exeName"));
        });
    }

    [Test]
    public void TestViewerLoop()
    {
        var args = new[] { "-V", "-R", "-N", "123", "filePath", "exeName" };
        var cmdOpt = ParseArgs(args);
        Assert.Multiple(() =>
        {
            Assert.That(cmdOpt.IsViewerMode, Is.EqualTo(true));
            Assert.That(cmdOpt.PlayBack, Is.EqualTo(false));
            Assert.That(cmdOpt.Loop, Is.EqualTo(true));
            Assert.That(cmdOpt.Shutdown, Is.EqualTo(false));
            Assert.That(cmdOpt.Measure, Is.EqualTo(123));
        });
        var opt = new ViewerModeValueOption(cmdOpt.ValueOptions);
        Assert.Multiple(() =>
        {
            Assert.That(opt.FileName, Is.EqualTo("filePath"));
            Assert.That(opt.ExeName, Is.EqualTo("exeName"));
        });
    }

    [Test]
    public void TestViewerShutdown()
    {
        var args = new[] { "-V", "-S" };
        var cmdOpt = ParseArgs(args);
        Assert.Multiple(() =>
        {
            Assert.That(cmdOpt.IsViewerMode, Is.EqualTo(true));
            Assert.That(cmdOpt.PlayBack, Is.EqualTo(false));
            Assert.That(cmdOpt.Loop, Is.EqualTo(false));
            Assert.That(cmdOpt.Shutdown, Is.EqualTo(true));
        });
    }

    private static CommandLineOption ParseArgs(IEnumerable<string> args)
    {
        var result = Parser.Default.ParseArguments<CommandLineOption>(args);
        if (result.Tag == ParserResultType.NotParsed) Assert.Fail("コマンドライン引数のパースに失敗");

        return result.Value;
    }
}