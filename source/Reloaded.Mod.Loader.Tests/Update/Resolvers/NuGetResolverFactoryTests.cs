﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Reloaded.Mod.Loader.IO.Utility;
using Reloaded.Mod.Loader.Tests.SETUP;
using Reloaded.Mod.Loader.Update.Interfaces;
using Reloaded.Mod.Loader.Update.Resolvers;
using Reloaded.Mod.Loader.Update.Structures;
using Sewer56.Update.Misc;
using Sewer56.Update.Resolvers;
using Sewer56.Update.Structures;
using Xunit;

namespace Reloaded.Mod.Loader.Tests.Update.Resolvers;

public class NuGetResolverFactoryTests : IDisposable
{
    private TestEnvironmoent _testEnvironmoent;

    public NuGetResolverFactoryTests() => _testEnvironmoent = new TestEnvironmoent();

    public void Dispose() => _testEnvironmoent.Dispose();

    [Fact]
    public void Migrate_MigratesNuSpecIntoMainConfig()
    {
        // Arrange
        var mod = _testEnvironmoent.TestModConfigATuple;
        var modDirectory = Path.GetDirectoryName(mod.Path);
        var nuspecFilePath = Path.Combine(modDirectory!, $"{IOEx.ForceValidFilePath(mod.Config.ModId)}.nuspec");
        File.Create(nuspecFilePath).Dispose();

        // Act
        var factory = new NuGetResolverFactory();
        factory.Migrate(mod, null);
        using var disposalHelper = new RemoveConfiguration<NuGetResolverFactory.NuGetConfig>(mod, factory);

        // Assert
        Assert.False(File.Exists(nuspecFilePath));
        Assert.True(factory.TryGetConfigurationOrDefault(mod, out var config));
    }

    [Fact]
    public void GetResolver_RespectsMainConfigUrls()
    {
        // Arrange
        var mod = _testEnvironmoent.TestModConfigATuple;
        
        // Act
        var resolverFactory  = new NuGetResolverFactory();
        var resolvers = (AggregatePackageResolver) resolverFactory.GetResolver(mod, null, new UpdaterData(new List<string>() { "Sample NuGet Feed" }, new CommonPackageResolverSettings()));

        // Assert
        Assert.Equal(1, resolvers.Count);
    }

    [Fact]
    public void GetResolver_RespectsModConfigUrls()
    {
        // Arrange
        var mod = _testEnvironmoent.TestModConfigATuple;

        // Act
        var resolverFactory = new NuGetResolverFactory();
        resolverFactory.SetConfiguration(mod, new NuGetResolverFactory.NuGetConfig()
        {
            DefaultRepositoryUrls = new ObservableCollection<string>() { "Sample Repository" }
        });

        using var disposalHelper = new RemoveConfiguration<NuGetResolverFactory.NuGetConfig>(mod, resolverFactory);

        var resolver = (AggregatePackageResolver)resolverFactory.GetResolver(mod, null, new UpdaterData(new List<string>(), new CommonPackageResolverSettings()));

        // Assert
        Assert.Equal(1, resolver.Count);
        Assert.True(resolverFactory.TryGetConfigurationOrDefault(mod, out var config));
    }

    [Fact]
    public void GetResolver_DoesNotDuplicateFeeds()
    {
        // Arrange
        var mod = _testEnvironmoent.TestModConfigATuple;

        // Act
        var resolverFactory = new NuGetResolverFactory();
        resolverFactory.SetConfiguration(mod, new NuGetResolverFactory.NuGetConfig()
        {
            DefaultRepositoryUrls = new ObservableCollection<string>() { "Sample Repository" }
        });

        using var disposalHelper = new RemoveConfiguration<NuGetResolverFactory.NuGetConfig>(mod, resolverFactory);

        var resolver = (AggregatePackageResolver)resolverFactory.GetResolver(mod, null, new UpdaterData(new List<string>() { "Sample Repository" }, new CommonPackageResolverSettings()));

        // Assert
        Assert.Equal(1, resolver.Count);
    }

    [Fact]
    public void GetResolver_ReturnsNullOnNoFeeds()
    {
        // Arrange
        var mod = _testEnvironmoent.TestModConfigATuple;

        // Act
        var resolverFactory = new NuGetResolverFactory();
        var resolver = resolverFactory.GetResolver(mod, null, new UpdaterData(new List<string>() {  }, new CommonPackageResolverSettings()));

        // Assert
        Assert.Null(resolver);
    }
}