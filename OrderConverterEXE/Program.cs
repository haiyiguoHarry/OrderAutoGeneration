using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;
using OrderConverterEXE.IpProtection;
using System.Runtime.Versioning;

namespace OrderConverterEXE;

[SupportedOSPlatform("windows")]
class Program
{
    static async Task Main(string[] args)
    {
        // 注册编码提供程序以支持中文
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // 便捷命令：打印机器指纹（用于你签发 license）
        if (args.Any(a => a.Equals("--print-fingerprint", StringComparison.OrdinalIgnoreCase)))
        {
            var fp = MachineFingerprint.GetFingerprintSha256Hex();
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine(fp);
            return;
        }

        // 创建主机
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<Configuration>();
                services.AddSingleton<IIpGuard, IpGuard>();
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

        // 启动硬门禁：无授权/过期/回拨/机器不匹配 -> 直接退出
        var guard = host.Services.GetRequiredService<IIpGuard>();
        var ipResult = guard.ValidateStartup();
        if (!ipResult.Allowed)
        {
            Console.Error.WriteLine($"[IP] {ipResult.ErrorCode}: {ipResult.Message}");
            Environment.ExitCode = 2;
            return;
        }

        await host.RunAsync();
    }
}
