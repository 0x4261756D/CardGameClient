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
			WindowState = WindowState,
		}.Show();
		Close();
	}

	private void ToDeckEditClick(object sender, RoutedEventArgs e)
	{
		new DeckEditWindow
		{
			WindowState = WindowState,
		}.Show();
		Close();
	}

	private void ToReplaysClick(object sender, RoutedEventArgs e)
	{
		new ReplaysWindow
		{
			WindowState = WindowState,
		}.Show();
		Close();
	}

	private void ToSettingsClick(object sender, RoutedEventArgs e)
	{
		new SettingsWindow
		{
			WindowState = WindowState,
		}.Show();
		Close();
	}
}
