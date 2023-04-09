using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CardGameUtils;
using CardGameUtils.Structs;
using static CardGameUtils.Functions;
using static CardGameUtils.Structs.NetworkingStructs;

namespace CardGameClient;

public partial class ReplaysWindow : Window
{
	private DuelWindow? window;
	private Replay? replay;
	private int playerIndex, actionIndex;

	public ReplaysWindow()
	{
		InitializeComponent();
		this.Width = Program.config.width / 5;
		this.Topmost = true;
	}
	public void BackClick(object sender, RoutedEventArgs args)
	{
		new MainWindow
		{
			WindowState = this.WindowState,
		}.Show();
		this.Close();
	}

	public void SelectFileClick(object sender, RoutedEventArgs args)
	{
		Task<string[]?> t = new OpenFileDialog() { AllowMultiple = false }.ShowAsync(this);
		t.Wait();
		FilePathBox.Text = t.Result?[0];
	}

	public void StartClick(object sender, RoutedEventArgs args)
	{
		if(!File.Exists(FilePathBox.Text))
		{
			new ErrorPopup($"Replay {FilePathBox.Text} does not exist");
			return;
		}
		replay = JsonSerializer.Deserialize<Replay>(File.ReadAllText(FilePathBox.Text), NetworkingConstants.jsonIncludeOption);
		if(replay == null)
		{
			new ErrorPopup($"Could not open replay {FilePathBox.Text}");
			return;
		}
		playerIndex = (FirstPlayerBox.IsChecked ?? false) ? 0 : 1;
		actionIndex = 0;
		window = new DuelWindow();
		window.Show(this);
		Next();
	}
	public void Next()
	{
		if(replay == null || window == null || actionIndex > replay.actions.Count)
		{
			return;
		}
		Replay.GameAction action = replay.actions[actionIndex];
		while(action.player != playerIndex || action.clientToServer || action.packet[0] != (byte)NetworkingConstants.PacketType.DuelFieldUpdateRequest)
		{
			if(action.packet[0] == (byte)NetworkingConstants.PacketType.DuelGameResultResponse)
			{
				window.Close();
				return;
			}
			actionIndex++;
			if(actionIndex > replay.actions.Count)
			{
				window.Close();
				return;
			}
			action = replay.actions[actionIndex];
		}
		window.EnqueueFieldUpdate(DeserializePayload<NetworkingStructs.DuelPackets.FieldUpdateRequest>(replay.actions[actionIndex].packet.GetRange(0, replay.actions[actionIndex].packet.Count - Packet.ENDING.Length)));
		window.UpdateField();
		actionIndex++;
	}

	public void NextClick(object sender, RoutedEventArgs args)
	{
		Next();
	}
}