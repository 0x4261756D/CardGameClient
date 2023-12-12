using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace CardGameClient;

public partial class App : Application
{
	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			RequestedThemeVariant = UIUtils.ConvertThemeVariant(Program.config.theme);
			desktop.MainWindow = new MainWindow();
			((IClassicDesktopStyleApplicationLifetime)ApplicationLifetime).ShutdownRequested += Program.Cleanup;
		}

		base.OnFrameworkInitializationCompleted();
	}
}
