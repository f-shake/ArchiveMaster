using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace ArchiveMaster.Models;

public class ChatOptions
{
    public ChatOptions()
    {
    }

    public double? Temperature { get; set; }

    public int? MaxOutputTokens { get; set; }

    public double? TopP { get; set; }

    public int? TopK { get; set; }

    public bool OutputJson { get; set; }
}