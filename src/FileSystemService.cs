using System;
using System.Threading.Tasks;
using Archive.FileSystemService.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using static Archive.FileSystemService.V1.FileSystemService;

namespace Archive.FileSystemServer;

public sealed class FileSystemService : FileSystemServiceBase
{
    private readonly ILogger<FileSystemService> logger;

    private readonly FileSystemServiceConfiguration fileSystemServiceConfiguration;

    public FileSystemService(ILogger<FileSystemService> logger, FileSystemServiceConfiguration fileSystemServiceConfiguration)
    {
        this.logger = logger;
        this.fileSystemServiceConfiguration = fileSystemServiceConfiguration;

        logger.LogInformation("Starting file system service.");

        foreach (Archive.FileSystemService.V1.FileSystemInfo fileSystemInfo in fileSystemServiceConfiguration.FileSystems)
        {
            logger.LogInformation("Servicing file system '{Name}' at '{RootPath}'.", fileSystemInfo.Name, fileSystemInfo.RootPath);
        }
    }

    public sealed override Task<Identification> Identify(Empty request, ServerCallContext context)
    {
        return Task.FromResult(new Identification
        {
            Version = new Archive.FileSystemService.V1.Version
            {
                Major = 1,
                Minor = 0,
                Patch = 0
            }
        });
    }

    public sealed override Task<FileSystemInfos> GetFileSystemInfos(Empty request, ServerCallContext context)
    {
        FileSystemInfos fileSystemInfos = new();

        foreach (Archive.FileSystemService.V1.FileSystemInfo fileSystemInfo in fileSystemServiceConfiguration.FileSystems)
        {
            fileSystemInfos.FileSystemInfos_.Add(fileSystemInfo);
        }

        return Task.FromResult(fileSystemInfos);
    }

    public sealed override async Task ReadFile(ReadFileRequest request, IServerStreamWriter<ReadFileResponse> responseStream, ServerCallContext context)
    {
        string absolutePath = System.IO.Path.Combine(request.FileSystemInfo.RootPath, request.Path);

        if (!System.IO.File.Exists(absolutePath))
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"File '{absolutePath}' not found."));
        }

        byte[] data = System.IO.File.ReadAllBytes(absolutePath);

        if (request.ChunkSize == 0)
        {
            await responseStream.WriteAsync(new ReadFileResponse
            {
                Data = Google.Protobuf.ByteString.CopyFrom(data),
            });
        }
        else
        {
            for (int i = 0; i < data.Length; i += request.ChunkSize)
            {
                byte[] chunk = data[i..Math.Min(i + request.ChunkSize, data.Length)];

                await responseStream.WriteAsync(new ReadFileResponse
                {
                    Data = Google.Protobuf.ByteString.CopyFrom(chunk),
                });
            }
        }
    }

    public sealed override Task<ReadDirectoryResponse> ReadDirectory(ReadDirectoryRequest request, ServerCallContext context)
    {
        string absolutePath = System.IO.Path.Combine(request.FileSystemInfo.RootPath, request.Path);

        if (!System.IO.Directory.Exists(absolutePath))
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Directory '{request.Path}' not found."));
        }

        System.IO.DirectoryInfo directoryInfo = new(absolutePath);

        ReadDirectoryResponse response = new();

        foreach (System.IO.FileInfo fileInfo in directoryInfo.GetFiles())
        {
            if (!request.ShowHiddenFiles && fileInfo.Attributes.HasFlag(System.IO.FileAttributes.Hidden))
            {
                continue;
            }

            response.FileInfos.Add(new FileInfo
            {
                AbsolutePath = fileInfo.FullName,
                Name = fileInfo.Name,
                Size = fileInfo.Length,
            });
        }

        foreach (System.IO.DirectoryInfo subDirectoryInfo in directoryInfo.GetDirectories())
        {
            if (!request.ShowHiddenDirectories && subDirectoryInfo.Attributes.HasFlag(System.IO.FileAttributes.Hidden))
            {
                continue;
            }

            response.DirectoryInfos.Add(new DirectoryInfo
            {
                AbsolutePath = subDirectoryInfo.FullName,
                Name = subDirectoryInfo.Name,
            });
        }

        return Task.FromResult(response);
    }
}