using TypeModel.Enums;

namespace Suflae;

/// <summary>The <c>suflae</c> command line.</summary>
internal static class SuflaeCli
{
    /// <summary>Registers the Suflae front end and runs the command line.</summary>
    public static int Main(string[] args)
    {
        SuflaeFrontend.Register();
        return Builder.Execution.Program.Run(args: args, cliLanguage: Language.Suflae);
    }
}
