using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CardGameClient;

public partial class SettingsWindow : Window
{
	public SettingsWindow()
	{
		InitializeComponent();
		WidthInput.Value = Program.config.width;
		HeightInput.Value = Program.config.height;
		ShouldSpawnCoreInput.IsChecked = Program.config.should_spawn_core;
		ShouldSavePlayerNameInput.IsChecked = Program.config.should_save_player_name;
		AnimationDelayInput.Value = Program.config.animation_delay_in_ms;
		CoreArgsInput.Text = Program.config.core_info.Arguments;
	}
	public void BackClick(object sender, RoutedEventArgs args)
	{
		new MainWindow
		{
			WindowState = this.WindowState,
		}.Show();
		Program.config.width = (int)WidthInput.Value;
		Program.config.height = (int)HeightInput.Value;
		Program.config.should_spawn_core = ShouldSpawnCoreInput.IsChecked ?? false;
		Program.config.should_save_player_name = ShouldSavePlayerNameInput.IsChecked ?? false;
		Program.config.animation_delay_in_ms = (int)AnimationDelayInput.Value;
		Program.config.core_info.Arguments = CoreArgsInput.Text;
		this.Close();
	}
}