using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.EnterpriseServices;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace CpuUsageSleep
{
	public class Worker : BackgroundService
	{
		private readonly ILogger<Worker> _logger;
		private ServiceConfig _config = new();

		public Worker(ILogger<Worker> logger)
		{
			_logger = logger;
			_config = LoadConfig();
		}

		public class ServiceConfig
		{
			public int CheckIntervalSeconds { get; set; }
			public int CpuUsageThreshold { get; set; }
			public int IdleMinutesBeforeSleep { get; set; }

			public ServiceConfig()
			{
				CheckIntervalSeconds = 10;
				CpuUsageThreshold = 15;
				IdleMinutesBeforeSleep = 15;
			}
		}

		private ServiceConfig LoadConfig()
		{
			//string dirPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\CpuUsageSleep\\";
			string dirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CpuUsageSleep");

			if (!Directory.Exists(dirPath))
			{
				Directory.CreateDirectory(dirPath);
			}
			string configPath = Path.Combine(dirPath, "CpuUsageConfig.json");

			if (!File.Exists(configPath))
			{
				File.Create(configPath).Close();
				var json = JsonSerializer.Serialize(new ServiceConfig(), new JsonSerializerOptions { WriteIndented = true });

				File.WriteAllText(configPath, json);
			}
			try
			{
				var serviceConfig = JsonSerializer.Deserialize<ServiceConfig>(File.ReadAllText(configPath));
				return serviceConfig ?? new ServiceConfig();

			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
				throw;
			}
		}

		public static class IdleTimeHelper
		{
			[StructLayout(LayoutKind.Sequential)]
			struct LASTINPUTINFO
			{
				public uint cbSize;
				public uint dwTime;
			}

			[DllImport("user32.dll")]
			static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

			/// <summary>
			/// Returns the idle time (in milliseconds) since last user input.
			/// </summary>
			public static TimeSpan GetIdleTime()
			{
				LASTINPUTINFO lastInputInfo = new LASTINPUTINFO();
				lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);

				if (!GetLastInputInfo(ref lastInputInfo))
					throw new InvalidOperationException("GetLastInputInfo failed.");

				uint idleTicks = unchecked((uint)Environment.TickCount - lastInputInfo.dwTime);
				return TimeSpan.FromMilliseconds(idleTicks);
			}
		}

		private TimeSpan GetIdleTime()
		{
			return IdleTimeHelper.GetIdleTime();
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			PerformanceCounter CpuUsage = new("Processor", "% Processor Time", "_Total");
			CpuUsage.NextValue();

			while (!stoppingToken.IsCancellationRequested)
			{
				_config = LoadConfig();
				TimeSpan idleTime = GetIdleTime();
				if (_logger.IsEnabled(LogLevel.Information))
				{
					_logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
				}

				int value = (int)CpuUsage.NextValue();

				if (idleTime.TotalMinutes >= _config.IdleMinutesBeforeSleep && value < _config.CpuUsageThreshold)
				{
					//pc sleep logic
					Console.BackgroundColor = ConsoleColor.Red;
					Console.WriteLine("PC IS ASLEEP BY NOW");
					PowerHelper.Sleep();
				}

				Console.Clear();
				Console.WriteLine($"idle time: \t{idleTime.TotalSeconds}");
				Console.WriteLine("Cpu Usage: \t" + value + "%");
				_logger.LogInformation($"Idle time: {idleTime.TotalMinutes} minutes");
				_logger.LogInformation($"Cpu Usage: {value}%");

				//*1000 to convert to milliseconds
				await Task.Delay(_config.CheckIntervalSeconds * 1000, stoppingToken);
			}
		}

		private class PowerHelper
		{
			[DllImport("PowrProf.dll")]
			[return: MarshalAs(UnmanagedType.Bool)]
			private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

			public static bool Sleep()
			{
				return SetSuspendState(false, false, false);
			}

		}
	}
}
