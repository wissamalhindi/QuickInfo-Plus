using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using Microsoft.VisualStudio.Threading;
using QuickInfoPlus.Core;

namespace QuickInfoPlus
{
    // Stored in the user settings store so hovering can read the values without loading the package.
    internal static class QuickInfoPlusOptions
    {
        private const string Collection = "QuickInfoPlus";

        private static volatile bool _loaded;

        public static bool ShowExamples { get; private set; } = true;

        public static bool ShowExceptions { get; private set; } = true;

        public static int ExamplesMaxHeight { get; private set; } = SectionLimits.DefaultExamplesHeight;

        public static async Task EnsureLoadedAsync(JoinableTaskFactory joinableTaskFactory, CancellationToken cancellationToken)
        {
            if (_loaded)
            {
                return;
            }

            await joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            Load();
            await TaskScheduler.Default;
        }

        public static void Load()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var store = GetStore();
                ShowExamples = store.GetBoolean(Collection, nameof(ShowExamples), true);
                ShowExceptions = store.GetBoolean(Collection, nameof(ShowExceptions), true);
                ExamplesMaxHeight = SectionLimits.ClampExamplesHeight(
                    store.GetInt32(Collection, nameof(ExamplesMaxHeight), SectionLimits.DefaultExamplesHeight));
            }
            catch (Exception)
            {
                ShowExamples = true;
                ShowExceptions = true;
                ExamplesMaxHeight = SectionLimits.DefaultExamplesHeight;
            }

            _loaded = true;
        }

        public static void Save(bool showExamples, bool showExceptions, int examplesMaxHeight)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ShowExamples = showExamples;
            ShowExceptions = showExceptions;
            ExamplesMaxHeight = SectionLimits.ClampExamplesHeight(examplesMaxHeight);
            _loaded = true;
            try
            {
                var store = GetStore();
                if (!store.CollectionExists(Collection))
                {
                    store.CreateCollection(Collection);
                }

                store.SetBoolean(Collection, nameof(ShowExamples), ShowExamples);
                store.SetBoolean(Collection, nameof(ShowExceptions), ShowExceptions);
                store.SetInt32(Collection, nameof(ExamplesMaxHeight), ExamplesMaxHeight);
            }
            catch (Exception)
            {
            }
        }

        private static WritableSettingsStore GetStore()
        {
            return new ShellSettingsManager(ServiceProvider.GlobalProvider).GetWritableSettingsStore(SettingsScope.UserSettings);
        }
    }
}
