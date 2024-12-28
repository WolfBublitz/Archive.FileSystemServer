using System.Collections.Generic;
using System.Net;
using Archive.FileSystemService.V1;

namespace Archive.FileSystemServer;

public sealed class FileSystemServiceConfiguration
{
    public IPAddress Address { get; init; } = IPAddress.Loopback;

    public int Port { get; init; } = 5001;

    public IEnumerable<FileSystemInfo> FileSystems { get; init; } = [];
}