using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Avalonia;
using CardGameUtils;
using CardGameUtils.Structs;

namespace CardGameClient;

class Program
{
	private static string configPath = "./config/config.json";
	public static string baseDir = AppDomain.CurrentDomain.BaseDirectory;
	private static PlatformClientConfig platformConfig = new();
	public static ClientConfig config = new(deck_edit_url: new URL("127.0.0.1", 7042),
		width: 1080, height: 720, core_info: new CoreInfo(), should_spawn_core: false, should_save_player_name: true,
		server_address: "127.0.0.1", animation_delay_in_ms: 120, theme: ClientConfig.ThemeVariant.Default,
		picture_path: "./pictures/");
	private static Process? core;
	private static bool couldReadConfig;

	// Initialization code. Don't use any Avalonia, third-party APIs or any
	// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
	// yet and stuff might break.
	[STAThread]
	public static void Main(string[] args)
	{
		for(int i = 0; i < args.Length; i++)
		{
			string[] parts = args[i].Split('=');
			if(parts.Length == 2)
			{
				switch(parts[0])
				{
					case "--config":
						configPath = Path.Combine(baseDir, parts[1]);
						break;
					default:
						Functions.Log($"Unknown option {args[i]}");
						break;
				}
			}
		}
		if(File.Exists(configPath))
		{
			couldReadConfig = true;
			platformConfig = JsonSerializer.Deserialize<PlatformClientConfig>(File.ReadAllText(configPath), GenericConstants.platformClientConfigSerialization)!;
			if(Environment.OSVersion.Platform == PlatformID.Unix)
			{
				config = platformConfig.linux!;
			}
			else
			{
				config = platformConfig.windows!;
			}
		}
		if(config.should_spawn_core)
		{
			if(config.core_info.FileName == null)
			{
				throw new Exception("No Core file name provided");
			}
			ProcessStartInfo info = new()
			{
				Arguments = config.core_info.Arguments,
				CreateNoWindow = config.core_info.CreateNoWindow,
				FileName = Path.Combine(baseDir, config.core_info.FileName),
				ErrorDialog = config.core_info.ErrorDialog,
				UseShellExecute = config.core_info.UseShellExecute,
				WorkingDirectory = Path.Combine(baseDir, config.core_info.WorkingDirectory),
			};
			if(!File.Exists(info.FileName))
			{
				throw new Exception($"No core found at {Path.GetFullPath(info.FileName)}");
			}
			core = Process.Start(info);
			if(core == null)
			{
				Functions.Log("Could not load the core", severity: Functions.LogSeverity.Error);
			}
		}

		_ = BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
	}

	// Avalonia configuration, don't remove; also used by visual designer.
	public static AppBuilder BuildAvaloniaApp()
		=> AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.LogToTrace();
	public static void Cleanup(object? sender, EventArgs e)
	{
		if(core != null)
		{
			if(!core.HasExited)
			{
				Functions.Log("Closing the core");
				core.Kill();
			}
		}
		if(!config.should_save_player_name)
		{
			config.player_name = null;
		}
		if(couldReadConfig)
		{
			if(Environment.OSVersion.Platform == PlatformID.Unix)
			{
				platformConfig.linux = config;
			}
			else
			{
				platformConfig.windows = config;
			}
			File.WriteAllText(configPath, JsonSerializer.Serialize(platformConfig, options: GenericConstants.platformClientConfigSerialization).Replace("  ", "\t"));
		}
	}
}
