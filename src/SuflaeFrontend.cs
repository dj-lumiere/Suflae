using Builder.Frontends;
using RazorForge;

namespace Suflae;

/// <summary>
/// The Suflae front end: registers Suflae's rules with the builder core, together with RazorForge's, whose
/// standard library a Suflae build analyzes.
/// </summary>
public static class SuflaeFrontend
{
    /// <summary>Registers Suflae's and RazorForge's rules (idempotent).</summary>
    public static void Register()
    {
        RazorForgeFrontend.Register();
        Languages.Register(rules: SuflaeRules.Instance);
    }
}
