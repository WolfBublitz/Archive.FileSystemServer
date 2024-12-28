using System;

namespace Archive.FileSystemService.V1;

public partial class FileSystemInfo
{
    public FileSystemInfo(string commandlineValue)
    {
        string[] parts = commandlineValue.Split(':');

        Name = parts[0];

        if (parts.Length > 1)
        {
            RootPath = parts[1];
        }
        else
        {
            throw new ArgumentException("The file system must be provided in the format <name>:<path>.");
        }
    }
}