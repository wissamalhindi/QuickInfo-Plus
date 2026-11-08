using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace QuickInfoPlus
{
    // Only registers the Options page; loaded by VS when the page is opened.
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid("7f3cdf04-b04a-47aa-8578-4c9abe09672b")]
    [ProvideOptionPage(typeof(GeneralOptionsPage), "QuickInfo Plus", "General", 0, 0, true, SupportsProfiles = true)]
    public sealed class QuickInfoPlusPackage : AsyncPackage
    {
    }
}
