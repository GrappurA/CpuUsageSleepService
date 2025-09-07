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
			public bool UseRam { get; set; }
			public int RamUsageThresholdGb { get; set; }
			public bool UseBandwidth { get; set; }
			public int BandwidthUsageThresholdKb { get; set; }

			public ServiceConfig()
			{
				CheckIntervalSeconds = 10;
				IdleMinutesBeforeSleep = 15;

				CpuUsageThreshold = 15;

				UseRam = true;
				RamUsageThresholdGb = 3;

				UseBandwidth = true;
				BandwidthUsageThresholdKb = 500;

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
			PerformanceCounter cpuCounter = new("Processor", "% Processor Time", "_Total");
			PerformanceCounter ramCounter = new("Processor", "% Processor Time", "_Total");
			PerformanceCounter bandwidthCounter = new("Processor", "% Processor Time", "_Total");

			cpuCounter.NextValue();
			ramCounter.NextValue();
			bandwidthCounter.NextValue();

			

			while (!stoppingToken.IsCancellationRequested)
			{
				_config = LoadConfig();
				TimeSpan idleTime = GetIdleTime();

				//logging
				if (_logger.IsEnabled(LogLevel.Information))
				{
					_logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
				}

				int cpuUsage = (int)cpuCounter.NextValue();
				int ramUsage = (int)ramCounter.NextValue();
				int bandwidthUsage = (int)bandwidthCounter.NextValue();//??

				if (idleTime.TotalMinutes >= _config.IdleMinutesBeforeSleep && cpuUsage < _config.CpuUsageThreshold)
				{
					//pc sleep logic
					Console.BackgroundColor = ConsoleColor.Red;
					Console.WriteLine("PC IS ASLEEP BY NOW");
					PowerHelper.Sleep();
				}

				Console.Clear();
				Console.WriteLine($"idle time: \t{idleTime.TotalSeconds}");
				Console.WriteLine("Cpu Usage: \t" + cpuUsage + "%");
				_logger.LogInformation($"Idle time: {idleTime.TotalMinutes} minutes\n \"Cpu Usage: {cpuUsage}%");

				//*1000 to convert to milliseconds
				await Task.Delay(_config.CheckIntervalSeconds * 1000, stoppingToken);
			}
		}

		//private bool IsUsingInternet()
		//{
		//	System.Net.NetworkInformation.IPv4InterfaceStatistics stats;
		//	stats.
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
