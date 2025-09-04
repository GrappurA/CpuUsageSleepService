using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Runtime.InteropServices;

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
			var handle = GetConsoleWindow();
			ShowWindow(handle, SW_HIDE);
			var isService = !(Environment.UserInteractive);

			var builder = Host.CreateDefaultBuilder(args).ConfigureServices((hostContext, services) =>
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