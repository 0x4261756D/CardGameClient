using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CardGameUtils;
using static CardGameUtils.Structs.NetworkingStructs;

namespace CardGameClient;

public partial class RoomWindow : Window
{
	private readonly Task networkTask;
	private readonly TcpClient client;
	private readonly string name, address;
	public bool closed = false;
	public RoomWindow(string address, TcpClient client, string name, string? opponentName = null)
	{
		this.client = client;
		this.address = address;
		networkTask = new Task(HandleNetwork, TaskCreationOptions.LongRunning);
		networkTask.Start();
		this.name = name;
		DataContext = new RoomWindowViewModel(name);
		Closed += (sender, args) => CloseRoom();
		InitializeComponent();
		OpponentNameBlock.Text = opponentName;
		if(DeckSelectBox.ItemCount <= 0)
		{
			CloseRoom();
		}
		else
		{
			if(DeckSelectBox.SelectedItem == null && DeckSelectBox.ItemCount > 0)
			{
				if(Program.config.last_deck_name != null)
				{
					foreach(var item in DeckSelectBox.Items)
					{
						if((string?)item == Program.config.last_deck_name)
						{
							DeckSelectBox.SelectedItem = item;
						}
					}
				}
				else
				{
					DeckSelectBox.SelectedIndex = 0;
				}
			}
		}
	}

	private async void HandleNetwork()
	{
		while(!closed)
		{
			if(client.Connected)
			{
				(byte, byte[]?)? payload = await Task.Run(() => Functions.TryReceiveRawPacket(client.GetStream(), 100));
				if(payload != null)
				{
					await Dispatcher.UIThread.InvokeAsync(() => HandlePacket(payload.Value));
				}
			}
		}
	}

	private void HandlePacket((byte type, byte[]? bytes) packet)
	{
		if(packet.type >= (byte)NetworkingConstants.PacketType.PACKET_COUNT)
		{
			Functions.Log($"Unrecognized packet type ({packet.type})");
			throw new Exception($"Unrecognized packet type ({packet.type})");
		}
		NetworkingConstants.PacketType type = (NetworkingConstants.PacketType)packet.type;
		switch(type)
		{
			case NetworkingConstants.PacketType.ServerOpponentChangedResponse:
			{
				string? name = Functions.DeserializeJson<ServerPackets.OpponentChangedResponse>(packet.bytes!).name;
				OpponentNameBlock.Text = name;
			}
			break;
			default:
			{
				throw new Exception($"Unexpected packet of type {type}");
			}
		}
	}

	public void DeckSelectionChanged(object sender, SelectionChangedEventArgs args)
	{
		Program.config.last_deck_name = args.AddedItems[0]?.ToString();
	}
	public void BackClick(object? sender, RoutedEventArgs? args)
	{
		CloseRoom();
		new ServerWindow
		{
			WindowState = WindowState,
		}.Show();
		Close();
	}
	public void CloseRoom()
	{
		if(!closed)
		{
			Functions.Request(new ServerPackets.LeaveRequest(name: name), address, 7043);
			client.Close();
			networkTask.Dispose();
			closed = true;
		}
	}
	private async void TryStartClick(object? sender, RoutedEventArgs args)
	{
		if(DeckSelectBox.SelectedItem is not string deckname || deckname == "")
		{
			await new ErrorPopup("No deck selected").ShowDialog(this);
			return;
		}
		if(OpponentNameBlock.Text is null || OpponentNameBlock.Text == "")
		{
			await new ErrorPopup("You have no opponent").ShowDialog(this);
			return;
		}
		(byte, byte[]?)? responseBytes = UIUtils.TryRequest(new DeckPackets.ListRequest(name: deckname),
			Program.config.deck_edit_url.address, Program.config.deck_edit_url.port, this);
		if(responseBytes == null)
		{
			return;
		}
		string[]? decklist = Functions.DeserializePayload<DeckPackets.ListResponse>(responseBytes.Value).deck.ToString()?.Split('\n');
		if(decklist == null)
		{
			await new ErrorPopup("Deck list could not be loaded properly").ShowDialog(this);
			return;
		}
		try
		{
			using TcpClient startClient = new(address, 7043);
			using NetworkStream stream = startClient.GetStream();
			stream.Write(Functions.GeneratePayload(new ServerPackets.StartRequest
			(
				decklist: decklist,
				name: ((RoomWindowViewModel)DataContext!).PlayerName,
				noshuffle: NoShuffleBox.IsChecked ?? false
			)));
			byte[]? bytes = Functions.ReceivePacket<ServerPackets.StartResponse>(stream);
			ServerPackets.StartResponse response = (bytes == null) ? new ServerPackets.StartResponse() : Functions.DeserializeJson<ServerPackets.StartResponse>(bytes);
			if(response.success == ServerPackets.StartResponse.Result.Failure)
			{
				new ErrorPopup(response.reason!).Show();
				return;
			}
			else
			{
				((Button)sender!).IsEnabled = false;
				if(response.success == ServerPackets.StartResponse.Result.Success)
				{
					StartGame(response.port, response.id!);
				}
				else
				{
					await Task.Run(() =>
					{
						bytes = Functions.ReceivePacket<ServerPackets.StartResponse>(stream);
						response = (bytes == null) ? new ServerPackets.StartResponse() : Functions.DeserializeJson<ServerPackets.StartResponse>(bytes);
						if(response.success != ServerPackets.StartResponse.Result.Success)
						{
							_ = new ErrorPopup(response.reason ?? "Duel creation failed for unknown reason");
						}
						else
						{
							StartGame(response.port, response.id!);
						}
					});
				}
			}
		}
		catch(Exception ex)
		{
			new ErrorPopup(ex.Message).Show();
		}
	}

	public async void StartGame(int port, string id)
	{
		TcpClient duelClient = new();
		await duelClient.ConnectAsync(address, port);
		byte[] idBytes = Encoding.UTF8.GetBytes(id);
		await duelClient.GetStream().WriteAsync(idBytes);
		byte[] playerIndex = new byte[1];
		await duelClient.GetStream().ReadExactlyAsync(playerIndex, 0, 1);
		await Dispatcher.UIThread.InvokeAsync(() =>
		{
			new DuelWindow(playerIndex[0], duelClient)
			{
				WindowState = WindowState,
			}.Show();
			Close();
		}, priority: DispatcherPriority.Background);
	}
}
public class RoomWindowViewModel : INotifyPropertyChanged
{
	public RoomWindowViewModel(string name)
	{
		playerName = name;
		LoadDecks();
	}

	public void LoadDecks()
	{
		(byte, byte[]?)? payload = UIUtils.TryRequest(new DeckPackets.NamesRequest(), Program.config.deck_edit_url.address, Program.config.deck_edit_url.port, null);
		if(payload == null)
		{
			return;
		}
		Decknames.Clear();
		Decknames.AddRange(Functions.DeserializePayload<DeckPackets.NamesResponse>(payload.Value).names);
	}

	public event PropertyChangedEventHandler? PropertyChanged;
	private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	private string playerName;
	public string PlayerName
	{
		get => playerName;
		set
		{
			if(value != playerName)
			{
				playerName = value;
				NotifyPropertyChanged();
			}
		}
	}

	private List<string> decknames = [];
	public List<string> Decknames
	{
		get => decknames;
		set
		{
			if(value != decknames)
			{
				decknames = value;
				NotifyPropertyChanged();
			}
		}
	}
}
