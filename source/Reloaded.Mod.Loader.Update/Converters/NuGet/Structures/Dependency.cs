﻿using System.Xml.Serialization;
#pragma warning disable CS1591

namespace Reloaded.Mod.Loader.Update.Converters.NuGet.Structures;

public class Dependency
{
    [XmlAttribute(AttributeName = "id")]
    public string Id { get; set; }

    [XmlAttribute(AttributeName = "version")]
    public string Version { get; set; }

    public Dependency() { }
    public Dependency(string id, string version)
    {
        Id = id;
        Version = version;
    }
}