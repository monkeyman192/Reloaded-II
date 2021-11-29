﻿using System;
using System.IO;
using Reloaded.Mod.Interfaces.Utilities;
using Reloaded.Mod.Loader.IO.Config;
using Reloaded.Mod.Loader.IO.Structs;
using Reloaded.Mod.Loader.IO.Utility;
using Reloaded.Mod.Loader.Update.Interfaces;
using Reloaded.Mod.Loader.Update.Structures;
using Sewer56.Update.Interfaces;
using Sewer56.Update.Resolvers.GitHub;

namespace Reloaded.Mod.Loader.Update.Resolvers
{
    public class GitHubReleasesResolverFactory : IResolverFactory
    {
        public string ResolverId { get; } = "GitHubRelease";

        /// <inheritdoc/>
        public void Migrate(PathTuple<ModConfig> mod, PathTuple<ModUserConfig> userConfig)
        {
            MigrateFromLegacyModConfig(mod);
            MigrateFromLegacyUserConfig(mod, userConfig);
        }

        /// <inheritdoc/>
        public IPackageResolver GetResolver(PathTuple<ModConfig> mod, PathTuple<ModUserConfig> userConfig, UpdaterData data)
        {
            if (!mod.Config.PluginData.TryGetValue<GitHubConfig>(ResolverId, out var githubConfig))
                return null;

            return new GitHubReleaseResolver(new GitHubResolverConfiguration()
            {
                RepositoryName = githubConfig.RepositoryName,
                UserName = githubConfig.UserName,
                LegacyFallbackPattern = githubConfig.AssetFileName
            }, data.CommonPackageResolverSettings);
        }

        private void MigrateFromLegacyModConfig(PathTuple<ModConfig> mod)
        {
            // Performs migration from legacy separate file config to integrated config.
            var gitHubConfigPath = GitHubConfig.GetFilePath(GetModDirectory(mod));
            if (File.Exists(gitHubConfigPath))
            {
                var githubConfig = IConfig<GitHubConfig>.FromPath(gitHubConfigPath);
                mod.Config.PluginData[ResolverId] = githubConfig;
                mod.Save();
                IOEx.TryDeleteFile(gitHubConfigPath);
            }
        }

        private void MigrateFromLegacyUserConfig(PathTuple<ModConfig> mod, PathTuple<ModUserConfig> userConfig)
        {
            // Performs migration from legacy separate file config to integrated config.
            var gitHubConfigPath = GitHubUserConfig.GetFilePath(GetModDirectory(mod));
            if (File.Exists(gitHubConfigPath))
            {
                var githubConfig = IConfig<GitHubUserConfig>.FromPath(gitHubConfigPath);
                userConfig.Config.AllowPrereleases = githubConfig.EnablePrereleases;
                userConfig.Save();
                IOEx.TryDeleteFile(gitHubConfigPath);
            }
        }

        private static string GetModDirectory(PathTuple<ModConfig> mod)
        {
            return Path.GetDirectoryName(mod.Path);
        }

        public class GitHubConfig : IConfig<GitHubConfig>
        {
            public const string ConfigFileName = "ReloadedGithubUpdater.json";

            /// <summary>
            /// [Legacy] Gets the file path for an existing legacy GitHub config.
            /// </summary>
            public static string GetFilePath(string directoryFullPath) => $"{directoryFullPath}\\{ConfigFileName}";

            public string UserName       { get; set; }

            public string RepositoryName { get; set; }

            /// <summary>
            /// [Legacy] Fallback file name pattern if no metadata file is found.
            /// </summary>
            public string AssetFileName { get; set; } = "Mod.zip";
        }
        
        /// <summary>
        /// Legacy. No longer in use, kept for migration only.
        /// </summary>
        public class GitHubUserConfig : IConfig<GitHubUserConfig>
        {
            public const string ConfigFileName = "ReloadedGithubUserConfig.json";

            public static string GetFilePath(string directoryFullPath) => $"{directoryFullPath}\\{ConfigFileName}";

            public bool EnablePrereleases { get; set; } = false;
        }
    }
}
