using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;

namespace OrderConverterEXE;

class Program
{
    static async Task Main(string[] args)
    {
        // 注册编码提供程序以支持中文
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // 创建主机
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<Configuration>();
                services.AddHostedService<XlsxConverterService>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddFile("xlsx_converter.log");
            })
            .Build();

        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("=".PadRight(60, '='));
        Console.WriteLine("XLSX Converter Service (xlsx-converter) 启动");
        Console.WriteLine("=".PadRight(60, '='));
        Console.WriteLine("程序运行中，按Ctrl+C 停止...");
        Console.WriteLine();

        await host.RunAsync();
    }
}
