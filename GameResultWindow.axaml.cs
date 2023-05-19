using Avalonia.Controls;
using Avalonia.Interactivity;
using CardGameUtils;
using static CardGameUtils.Structs.NetworkingStructs;

namespace CardGameClient;

public partial class GameResultWindow : Window
{
	// DONT USE THIS
	// This only exists because Avalonia requires it
	public GameResultWindow()
	{
		InitializeComponent();
		parent = new Window();
	}

	Window parent;

	public GameResultWindow(Window parent, DuelPackets.GameResultResponse response)
	{
		this.parent = parent;
		InitializeComponent();
		this.Width = Program.config.width / 2;
		this.Height = Program.config.height / 2;
		this.Closed += (_, _) =>
		{
			this.parent.Close();
		};
		ResultBlock.Text = (response.result == GameConstants.GameResult.Draw) ?
			"It was a draw" : $"You {response.result}";
		this.Topmost = true;
	}
	public void BackClick(object? sender, RoutedEventArgs args)
	{
		new ServerWindow
		{
			WindowState = this.WindowState,
		}.Show();
		this.Close();
	}
}