﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Reloaded.Mod.Launcher.Pages.Dialogs;
using Reloaded.Mod.Launcher.Utility;
using Reloaded.Mod.Loader.IO.Config;
using Reloaded.Mod.Loader.IO.Services;
using Reloaded.Mod.Loader.IO.Structs;
using Reloaded.Mod.Loader.Update;
using Reloaded.Mod.Loader.Update.Structures;
using Reloaded.Mod.Loader.Update.Utilities.Nuget;
using Reloaded.Mod.Loader.Update.Utilities.Nuget.Structs;
using Reloaded.WPF.Utilities;
using Sewer56.Update;
using Sewer56.Update.Extractors.SevenZipSharp;
using Sewer56.Update.Packaging.Structures;
using Sewer56.Update.Resolvers.GitHub;
using Sewer56.Update.Structures;
using Constants = Reloaded.Mod.Launcher.Misc.Constants;
using MessageBox = System.Windows.MessageBox;
using Version = Reloaded.Mod.Launcher.Utility.Version;

namespace Reloaded.Mod.Launcher
{
    /// <summary>
    /// Contains static methods related to downloading loader, mods and updating them.
    /// </summary>
    public static class Update
    {
        /* Strings */
        private static XamlResource<string> _xamlCheckUpdatesFailed = new XamlResource<string>("ErrorCheckUpdatesFailed");
        private static bool _hasInternetConnection = CheckForInternetConnection();
        
        /// <summary>
        /// Checks if there are any updates for the mod loader.
        /// </summary>
        public static async Task CheckForLoaderUpdatesAsync()
        {
            if (!_hasInternetConnection)
                return;

            // Check for loader updates.
            UpdateManager<Empty> manager = null;
            try
            {
                var resolver = new GitHubReleaseResolver(new GitHubResolverConfiguration()
                {
                    LegacyFallbackPattern = Constants.GitRepositoryReleaseName,
                    RepositoryName = Constants.GitRepositoryName,
                    UserName = Constants.GitRepositoryAccount
                });

                var metadata = new ItemMetadata(Version.GetReleaseVersion(), Constants.ApplicationPath, null);
                manager  = await UpdateManager<Empty>.CreateAsync(metadata, resolver, new SevenZipSharpExtractor());

                // Check for new version and, if available, perform full update and restart
                var result = await manager.CheckForUpdatesAsync();
                if (result.CanUpdate)
                {
                    ActionWrappers.ExecuteWithApplicationDispatcher(() =>
                    {
                        var dialog = new ModLoaderUpdateDialog(manager, result.LastVersion);
                        dialog.ShowDialog();
                    });
                }
            }
            catch (Exception ex)
            {
                manager?.Dispose();
                MessageBox.Show($"{_xamlCheckUpdatesFailed.Get()}\n" +
                                $"Actual error: {ex.Message}\n" +
                                $"{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Checks if there are updates for any of the installed mods and/or new dependencies to fetch.
        /// </summary>
        public static async Task<bool> CheckForModUpdatesAsync()
        {
            if (!_hasInternetConnection)
                return false;

            var loaderConfig = IoC.Get<LoaderConfig>();
            var modConfigService = IoC.Get<ModConfigService>();
            var modUserConfigService = IoC.Get<ModUserConfigService>();
            var allMods = modConfigService.Items.Select(x => new PathTuple<ModConfig>(x.Path, (ModConfig) x.Config)).ToArray();

            try
            {
                // TODO: Work from here too.
                var nugetFeeds       = IoC.Get<AggregateNugetRepository>().Sources.Select(x => x.SourceUrl).ToList();
                var resolverSettings = new CommonPackageResolverSettings() { AllowPrereleases = loaderConfig.ForceModPrereleases };
                var updaterData      = new UpdaterData(nugetFeeds, resolverSettings);
                var updater          = new Updater(allMods, modUserConfigService.ItemsById, updaterData);
                var updateDetails    = await updater.GetUpdateDetails();

                if (updateDetails.HasUpdates())
                {
                    ActionWrappers.ExecuteWithApplicationDispatcher(() =>
                    {
                        var dialog = new ModUpdateDialog(updater, updateDetails);
                        dialog.ShowDialog();
                    });

                    return true;
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message + "|" + e.StackTrace);
                return false;
            }

            return false;
        }

        /// <summary>
        /// Downloads mods in an asynchronous fashion provided a list of Mod IDs to download.
        /// </summary>
        /// <param name="modIds">IDs of all of the mods to download.</param>
        /// <param name="includePrerelease">Include pre-release packages.</param>
        /// <param name="includeUnlisted">Include unlisted packages.</param>
        /// <param name="token">Used to cancel the operation.</param>
        public static async Task DownloadNuGetPackagesAsync(IEnumerable<string> modIds, bool includePrerelease, bool includeUnlisted, CancellationToken token = default)
        {
            if (!_hasInternetConnection)
                return;

            var aggregateRepository = IoC.Get<AggregateNugetRepository>();
            var packages            = new List<NugetTuple<IPackageSearchMetadata>>();
            var missingPackages     = new List<string>();

            /* Get details of every mod. */
            foreach (var modId in modIds)
            {
                var packageDetails = await aggregateRepository.GetPackageDetails(modId, includePrerelease, includeUnlisted, token);
                var newest = aggregateRepository.GetNewestPackage(packageDetails);
                if (newest != null)
                    packages.Add(newest);
                else
                    missingPackages.Add(modId);
            }

            await DownloadNuGetPackagesAsync(packages, missingPackages, includePrerelease, includeUnlisted, token);
        }


        /// <summary>
        /// Downloads mods in an asynchronous fashion provided a list of known and missing packages.
        /// </summary>
        /// <param name="package">Existing known package.</param>
        /// <param name="missingPackages">List of packages known to be missing.</param>
        /// <param name="includePrerelease">Include pre-release packages.</param>
        /// <param name="includeUnlisted">Include unlisted packages.</param>
        /// <param name="token">Used to cancel the operation.</param>
        public static async Task DownloadNuGetPackagesAsync(NugetTuple<IPackageSearchMetadata> package, List<string> missingPackages, bool includePrerelease, bool includeUnlisted, CancellationToken token = default)
        {
            if (!_hasInternetConnection)
                return;

            await DownloadNuGetPackagesAsync(new List<NugetTuple<IPackageSearchMetadata>>() { package }, missingPackages, includePrerelease, includeUnlisted, token);
        }

        /// <summary>
        /// Downloads mods in an asynchronous fashion provided a list of known and missing packages.
        /// </summary>
        /// <param name="packages">List of existing known packages.</param>
        /// <param name="missingPackages">List of packages known to be missing.</param>
        /// <param name="includePrerelease">Include pre-release packages.</param>
        /// <param name="includeUnlisted">Include unlisted packages.</param>
        /// <param name="token">Used to cancel the operation.</param>
        public static async Task DownloadNuGetPackagesAsync(List<NugetTuple<IPackageSearchMetadata>> packages, List<string> missingPackages, bool includePrerelease, bool includeUnlisted, CancellationToken token = default)
        {
            if (!_hasInternetConnection)
                return;

            /* Get dependencies of every mod. */
            foreach (var package in packages.ToArray())
            {
                var repository = package.Repository;
                var searchResult = await repository.FindDependencies(package.Generic, includePrerelease, includeUnlisted, token);

                packages.AddRange(
                    searchResult.Dependencies.Select(x => new NugetTuple<IPackageSearchMetadata>(package.Repository, x)));
                missingPackages.AddRange(searchResult.PackagesNotFound);
            }

            /* Remove already existing packages. */
            var allMods = IoC.Get<ModConfigService>().Items.ToArray();
            HashSet<string> allModIds = new HashSet<string>(allMods.Length);
            foreach (var mod in allMods)
                allModIds.Add(mod.Config.ModId);

            // Remove mods we already have.
            packages = packages.Where(x => !allModIds.Contains(x.Generic.Identity.Id)).ToList();
            missingPackages = missingPackages.Where(x => !allModIds.Contains(x)).ToList();

            ActionWrappers.ExecuteWithApplicationDispatcher(() =>
            {
                var dialog = new NugetFetchPackageDialog(packages, missingPackages);
                dialog.ShowDialog();
            });
        }

        /// <summary>
        /// Verifies if all mods have all of their required dependencies and
        /// returns a list of missing dependencies by ModId.
        /// </summary>
        /// <returns>True if there ar missing dependencies, else false.</returns>
        public static bool CheckMissingDependencies(out List<string> missingDependencies)
        {
            var modConfigService = IoC.Get<ModConfigService>();

            // Get all mods and build list of IDs
            var allMods = modConfigService.Items.ToArray();
            HashSet<string> allModIds = new HashSet<string>(allMods.Length);
            foreach (var mod in allMods)
                allModIds.Add(mod.Config.ModId);

            // Build list of missing dependencies.
            var missingDeps = new HashSet<string>(allModIds.Count);
            foreach (var mod in allMods)
            {
                foreach (var dependency in mod.Config.ModDependencies)
                {
                    if (! allModIds.Contains(dependency))
                        missingDeps.Add(dependency);
                }
            }

            missingDependencies = missingDeps.ToList();
            return missingDependencies.Count > 0;
        }

        /// <summary>
        /// Checks if the user is connected to the internet using the same method Chromium OS does.
        /// </summary>
        /// <returns></returns>
        public static bool CheckForInternetConnection()
        {
            var urls = new List<string>()
            {
                "http://clients1.google.com/generate_204",
                "http://clients2.google.com/generate_204",
                "http://clients3.google.com/generate_204",
                "https://google.com",
                "https://github.com",
                "https://en.wikipedia.org",
                "https://baidu.com" // In case of Firewall of People's Republic of China.
            };

            foreach (var url in urls)
            {
                try
                {
                    using var client = new WebClient();
                    using (client.OpenRead(url))
                        return true;
                }
                catch
                {
                    // ignored
                }
            }

            return false;
        }
    }
}
