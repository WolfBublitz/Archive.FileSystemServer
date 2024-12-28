// using System.Net;
// using Archive.FileSystemServer;
// using Microsoft.AspNetCore.Builder;
// using Microsoft.AspNetCore.Hosting;
// using Microsoft.AspNetCore.Server.Kestrel.Core;
// using Microsoft.Extensions.DependencyInjection;

using System;
using System.CommandLine;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Archive.FileSystemServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

class Program
{
    public static async Task<int> Main(string[] args)
    {
        int exitCode = 0;

        Option<string> addressOption = new("--address", getDefaultValue: () => "127.0.0.1")
        {
            IsRequired = false,
        };

        Option<int> portOption = new("--port", getDefaultValue: () => 5001)
        {
            IsRequired = false,
        };

        Option<Archive.FileSystemService.V1.FileSystemInfo[]> fileSystemOption = new(
            "--file-system",
            parseArgument: result =>
            {
                if (result.Tokens.Count == 0)
                {
                    throw new ArgumentException("The --file-system option requires an argument.");
                }
                else
                {
                    return result.Tokens.Select(token => new Archive.FileSystemService.V1.FileSystemInfo(token.Value)).ToArray();
                }
            })
        {
            IsRequired = false,
            Arity = ArgumentArity.OneOrMore
        };

        RootCommand rootCommand = new("File System Server");
        rootCommand.AddOption(fileSystemOption);
        rootCommand.AddOption(addressOption);
        rootCommand.AddOption(portOption);
        rootCommand.SetHandler(async (Archive.FileSystemService.V1.FileSystemInfo[] fileSystems, string address, int port) =>
        {
            FileSystemServiceConfiguration fileSystemServiceConfiguration = new()
            {
                Address = IPAddress.Parse(address),
                Port = port,
                FileSystems = fileSystems,
            };

            exitCode = await RunServerAsync(fileSystemServiceConfiguration);
        }, fileSystemOption, addressOption, portOption);

        await rootCommand.InvokeAsync(args).ConfigureAwait(false);

        return exitCode;
    }

    private static async Task<int> RunServerAsync(FileSystemServiceConfiguration fileSystemServiceConfiguration)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        // adding gRPC services to the DI container
        builder.Services.AddGrpc();
        builder.Services.AddGrpcReflection();

        // adding FileSystemServiceConfiguration to the DI container
        builder.Services.AddSingleton(fileSystemServiceConfiguration);

        builder.WebHost.ConfigureKestrel(options =>
        {

            options.Listen(fileSystemServiceConfiguration.Address, fileSystemServiceConfiguration.Port, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http2;
                //listenOptions.UseHttps();
            });
        });

        WebApplication webApplication = builder.Build();

        webApplication.MapGrpcService<FileSystemService>();
        webApplication.MapGrpcReflectionService();

        try
        {
            await webApplication.RunAsync().ConfigureAwait(false);

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);

            return 1;
        }
    }
}
