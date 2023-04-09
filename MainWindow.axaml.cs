using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CardGameClient;

public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
		Width = Program.config.width;
		Height = Program.config.height;
	}

	private void ToDuelClick(object sender, RoutedEventArgs e)
	{
		new ServerWindow
		{
			WindowState = this.WindowState,
		}.Show();
		this.Close();
	}

	private void ToDeckEditClick(object sender, RoutedEventArgs e)
	{
		new DeckEditWindow
		{
			WindowState = this.WindowState,
		}.Show();
		this.Close();
	}

	private void ToReplaysClick(object sender, RoutedEventArgs e)
	{
		new ReplaysWindow
		{
			WindowState = this.WindowState,
		}.Show();
		this.Close();
	}

	private void ToSettingsClick(object sender, RoutedEventArgs e)
	{
		new SettingsWindow
		{
			WindowState = this.WindowState,
		}.Show();
		this.Close();
	}
}