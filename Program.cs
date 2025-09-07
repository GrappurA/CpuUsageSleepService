using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Runtime.InteropServices;
using Serilog;
using Serilog.Sinks.File;


namespace CpuUsageSleep
{
	public class Program
	{
		[DllImport("kernel32.dll")]
		static extern IntPtr GetConsoleWindow();

		[DllImport("user32.dll")]
		static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

		const int SW_HIDE = 0;

		public static void Main(string[] args)
		{
			var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CpuUsageSleep", "CpuUsageSleep.log");
			Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

			var handle = GetConsoleWindow();
			ShowWindow(handle, SW_HIDE);
			var isService = !(Environment.UserInteractive);

			Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Information()
			.WriteTo.File(
				logPath,
				rollingInterval: RollingInterval.Day,  // new file each day
				retainedFileCountLimit: 70,             // keep last 7 logs
				shared: true)
			.CreateLogger();

			var builder = Host.CreateDefaultBuilder(args)
				.UseSerilog()
				.ConfigureServices((hostContext, services) =>
				{
					services.AddHostedService<Worker>();
				});
			if (isService)
			{
				builder.UseWindowsService();
			}
			else
			{
				builder.UseConsoleLifetime();
			}

			builder.Build().Run();
		}
	}
}