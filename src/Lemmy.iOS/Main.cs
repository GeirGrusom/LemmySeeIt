using Lemmy.Services;
using UIKit;

namespace Lemmy.iOS;

/// <summary>The iOS entry point.</summary>
internal static class Application
{
    private static void Main(string[] args)
    {
        // This head ships a different set of packages from the shared project, so it hands its own
        // generated list to the licences page.
        Attribution.Use(Generated.GeneratedAttribution.Packages);

        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}
