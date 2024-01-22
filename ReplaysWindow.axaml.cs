using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CardGameUtils;
using CardGameUtils.Structs;
using static CardGameUtils.Functions;

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
		Width = Program.config.width / 5;
		Topmost = true;
	}
	public void BackClick(object sender, RoutedEventArgs args)
	{
		new MainWindow
		{
			WindowState = WindowState,
		}.Show();
		Close();
	}

	public async void SelectFileClick(object sender, RoutedEventArgs args)
	{
		FilePathBox.Text = await UIUtils.SelectFileAsync(this, "Select Replay").ConfigureAwait(true);
	}

	public void ToEndClick(object sender, RoutedEventArgs args)
	{
		if(replay != null && window != null)
		{
			for(; actionIndex < replay.actions.Count; actionIndex++)
			{
				Replay.GameAction action = replay.actions[actionIndex];
				((ReplaysViewModel)DataContext!).ActionList.Insert
				(
					0,
					$"{(IsFieldUpdateForCurrentPlayer(action) ? "*" : "")}{actionIndex}: Player {action.player}: {(action.clientToServer ? "<-" : "->")} {Enum.GetName((NetworkingConstants.PacketType)action.packetType) ?? "UNKNOWN"}"
				);
			}
			Replay.GameAction lastAction = replay.actions.FindLast(IsFieldUpdateForCurrentPlayer)!;
			window.EnqueueFieldUpdate(DeserializePayload<NetworkingStructs.DuelPackets.FieldUpdateRequest>(lastAction.packetType, lastAction.PacketContentBytes()));
			window.UpdateField();
		}
	}

	private bool IsFieldUpdateForCurrentPlayer(Replay.GameAction action)
	{
		return action.player == (((ReplaysViewModel)DataContext!).IsFirstPlayer ? 0 : 1) && action.packetType == (byte)NetworkingConstants.PacketType.DuelFieldUpdateRequest;
	}

	public void StartClick(object sender, RoutedEventArgs args)
	{
		if(!File.Exists(FilePathBox.Text))
		{
			_ = new ErrorPopup($"Replay {FilePathBox.Text} does not exist");
			return;
		}
		string text = File.ReadAllText(FilePathBox.Text);
		replay = JsonSerializer.Deserialize<Replay>(text, GenericConstants.replaySerialization);
		if(replay == null)
		{
			_ = new ErrorPopup($"Could not open replay {FilePathBox.Text}");
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
		if(replay == null || window == null || actionIndex >= replay.actions.Count - 1)
		{
			return;
		}
		Replay.GameAction action = replay.actions[actionIndex];
		int playerIndex = ((ReplaysViewModel)DataContext!).IsFirstPlayer ? 0 : 1;
		while(action.player != playerIndex || action.clientToServer || action.packetType != (byte)NetworkingConstants.PacketType.DuelFieldUpdateRequest)
		{
			((ReplaysViewModel)DataContext!).ActionList.Insert(0, $"{actionIndex}: Player {action.player}: {(action.clientToServer ? "<-" : "->")} {Enum.GetName((NetworkingConstants.PacketType)action.packetType) ?? "UNKNOWN"}");
			actionIndex++;
			if(actionIndex >= replay.actions.Count - 1)
			{
				return;
			}
			action = replay.actions[actionIndex];
		}
		((ReplaysViewModel)DataContext!).ActionList.Insert(0, $"* {actionIndex}: Player {action.player}: {(action.clientToServer ? "<-" : "->")} {Enum.GetName((NetworkingConstants.PacketType)action.packetType) ?? "UNKNOWN"}");
		window.EnqueueFieldUpdate(DeserializePayload<NetworkingStructs.DuelPackets.FieldUpdateRequest>(replay.actions[actionIndex].packetType, replay.actions[actionIndex].PacketContentBytes()));
		window.UpdateField();
		actionIndex++;
	}

	public void Prev()
	{
		if(replay == null || window == null || actionIndex < 2)
		{
			return;
		}
		actionIndex -= 2;
		((ReplaysViewModel)DataContext!).ActionList.RemoveAt(0);
		Replay.GameAction action = replay.actions[actionIndex];
		int playerIndex = ((ReplaysViewModel)DataContext!).IsFirstPlayer ? 0 : 1;
		while(action.player != playerIndex || action.clientToServer || action.packetType != (byte)NetworkingConstants.PacketType.DuelFieldUpdateRequest)
		{
			((ReplaysViewModel)DataContext!).ActionList.RemoveAt(0);
			if(action.packetType == (byte)NetworkingConstants.PacketType.DuelGameResultResponse)
			{
				window.Close();
				return;
			}
			actionIndex--;
			if(actionIndex < 0)
			{
				actionIndex = 0;
				window.Close();
				return;
			}
			action = replay.actions[actionIndex];
		}
		window.EnqueueFieldUpdate(DeserializePayload<NetworkingStructs.DuelPackets.FieldUpdateRequest>(replay.actions[actionIndex].packetType, replay.actions[actionIndex].PacketContentBytes()));
		window.UpdateField();
		actionIndex++;
	}

	public void NextClick(object sender, RoutedEventArgs args)
	{
		Next();
	}

	public void PrevClick(object sender, RoutedEventArgs args)
	{
		Prev();
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

	private readonly ObservableCollection<string> actionList = [];
	public ObservableCollection<string> ActionList
	{
		get => actionList;
	}
}
