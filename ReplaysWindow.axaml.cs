using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
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
	private int actionIndex;

	public ReplaysWindow()
	{
		InitializeComponent();
		DataContext = new ReplaysViewModel();
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
		actionIndex = 0;
		((ReplaysViewModel)DataContext!).ActionList.Clear();
		window?.Close();
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
		int playerIndex = ((ReplaysViewModel)DataContext!).IsFirstPlayer ? 0 : 1;
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
			((ReplaysViewModel)DataContext!).ActionList.Insert(0, $"{actionIndex}: Player {action.player}: {Enum.GetName<NetworkingConstants.PacketType>((NetworkingConstants.PacketType)action.packet[0]) ?? "UNKNOWN"}");
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

public class ReplaysViewModel : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;

	private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	private bool isFirstPlayer = true;
	public bool IsFirstPlayer
	{
		get => isFirstPlayer;
		set
		{
			if(isFirstPlayer != value)
			{
				isFirstPlayer = value;
				NotifyPropertyChanged();
			}
		}
	}

	private ObservableCollection<string> actionList = new ObservableCollection<string>();
	public ObservableCollection<string> ActionList
	{
		get => actionList;
	}
}