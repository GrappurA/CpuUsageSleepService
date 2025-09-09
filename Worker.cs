using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.EnterpriseServices;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.VisualBasic;
using System.Threading.Tasks;

namespace CpuUsageSleep
{
	public class Worker : BackgroundService
	{
		private readonly ILogger<Worker> _logger;
		private ServiceConfig _config = new();
		//FileSystemWatcher _watcher = new();
		private string dirPath;
		private string configPath;

		public Worker(ILogger<Worker> logger)
		{
			_logger = logger;
			_config = LoadConfig();
		}

		public class ServiceConfig
		{
			public int CheckIntervalSeconds { get; set; }
			public int IdleMinutesBeforeSleep { get; set; }
			public bool UseCpu { get; set; }
			public int CpuUsageThreshold { get; set; }
			public bool UseRam { get; set; }
			public int RamUsageThresholdGb { get; set; }
			public bool UseBandwidth { get; set; }
			public int BandwidthUsageThreshold { get; set; }

			public ServiceConfig()
			{
				CheckIntervalSeconds = 15;
				IdleMinutesBeforeSleep = 20;

				UseCpu = true;
				CpuUsageThreshold = 15;

				UseRam = true;
				RamUsageThresholdGb = 3;

				UseBandwidth = true;
				BandwidthUsageThreshold = 10;
			}
		}

		private ServiceConfig LoadConfig()
		{
			dirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CpuUsageSleep");

			if (!Directory.Exists(dirPath))
			{
				Directory.CreateDirectory(dirPath);
			}
			configPath = Path.Combine(dirPath, "CpuUsageConfig.json");

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

		[DllImport("kernel32.dll", SetLastError = true)]
		static extern uint SetThreadExecutionState(uint esFlags);

		const uint ES_CONTINUOUS = 0x80000000;
		const uint ES_SYSTEM_REQUIRED = 0x00000001;
		const uint ES_DISPLAY_REQUIRED = 0x00000002;
		public static void PreventSleep()
		{
			// Pretend the system is active
			SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
		}

		public static void AllowSleep()
		{
			// Revert back to normal idle behavior
			SetThreadExecutionState(ES_CONTINUOUS);
		}

		private TimeSpan GetIdleTime()
		{
			return IdleTimeHelper.GetIdleTime();
		}

		private async Task<PerformanceCounter> GetCpuUsage()
		{
			PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
			cpuCounter.NextValue();
			return cpuCounter;
		}

		private async Task<double> GetRamUsage()
		{
			double totalGb = new Microsoft.VisualBasic.Devices
				.ComputerInfo().TotalPhysicalMemory / 1024.0 / 1024.0 / 1024.0;

			PerformanceCounter ramCounter = new PerformanceCounter("Memory", "Available MBytes");
			ramCounter.NextValue();

			return totalGb - (ramCounter.NextValue() / 1024);
		}

		private async Task<PerformanceCounter> GetBandwidthUsage()
		{
			var category = new PerformanceCounterCategory("Network Interface");
			var instances = category.GetInstanceNames();

			var counters = instances.Select(name => new
			{
				Name = name,
				Received = new PerformanceCounter("Network Interface", "Bytes Received/sec", name),
				Sent = new PerformanceCounter("Network Interface", "Bytes Sent/sec", name),
			}).ToList();

			foreach (var c in counters)
			{
				c.Received.NextValue();
				c.Sent.NextValue();

				return c.Received;
			}
			return new PerformanceCounter();

		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			PerformanceCounter cpuCounter = await GetCpuUsage();
			PerformanceCounter ramCounter = new("Memory", "Available MBytes");
			PerformanceCounter bandwidthCounter = await GetBandwidthUsage();

			//_watcher.Path = dirPath;
			//_watcher.Filter = "CpuUsageConfig.json";
			//_watcher.NotifyFilter = NotifyFilters.LastWrite;
			//_watcher.Changed += OnChanged;
			//_watcher.EnableRaisingEvents = true;

			while (!stoppingToken.IsCancellationRequested)
			{
				TimeSpan idleTime = GetIdleTime();

				//logging
				if (_logger.IsEnabled(LogLevel.Information))
				{
					_logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
				}

				_config = LoadConfig();

				int cpuUsage = (int)cpuCounter.NextValue();
				double ramUsage = await GetRamUsage();
				double bandwidthUsage = bandwidthCounter.NextValue();
				bool shouldPrevent = false;

				Console.Clear();
				_logger.LogInformation(
					$"Idle time: {idleTime.TotalMinutes} minutes\n" +
					$"Cpu Usage: {cpuUsage}%\n" +
					$"Ram Usage: {ramUsage}\n" +
					$"Bandwidth Usage: {bandwidthUsage}");

				Console.WriteLine(
					$"Idle time: {idleTime.TotalMinutes} minutes\n" +
					$"Cpu Usage: {cpuUsage}%\n" +
					$"Ram Usage: {ramUsage}\n" +
					$"Bandwidth Usage: {bandwidthUsage}");

				if (_config.UseCpu == true && cpuUsage > _config.CpuUsageThreshold)
					shouldPrevent = true;

				if (_config.UseRam == true && ramUsage > _config.RamUsageThresholdGb)
					shouldPrevent = true;

				if (_config.UseBandwidth == true && bandwidthUsage > _config.BandwidthUsageThreshold)
					shouldPrevent = true;

				if (shouldPrevent)
				{
					PreventSleep();
				}
				else
				{
					AllowSleep();
					Console.WriteLine("sleeping");
					_logger.LogInformation("PC going to sleep... zzz");
					if (idleTime.Minutes > _config.IdleMinutesBeforeSleep)
					{
						PowerHelper.Sleep();
					}
				}

				//*1000 to convert to milliseconds
				await Task.Delay(_config.CheckIntervalSeconds * 1000, stoppingToken);
			}
		}

		//private async void OnChanged(object sender, FileSystemEventArgs e)
		//{
		//	try
		//	{
		//		await Task.Delay(300);
		//		_logger.LogInformation("Config changed, reloading...");
		//		_config = LoadConfig();
		//	}
		//	catch (Exception ex)
		//	{
		//		_logger.LogError(ex, "Failed to reload config");
		//	}
		//}

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
