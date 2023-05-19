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
	private string address;
	private int port;
	private string name;
	public bool closed = false;
	public RoomWindow()
	{
		InitializeComponent();
		address = "DONT USE THIS";
		name = "DONT USE THIS";
	}
	public RoomWindow(string address, int port, string name)
	{
		this.address = address;
		this.port = port;
		this.name = name;
		DataContext = new RoomWindowViewModel(name);
		this.Closed += (sender, args) => CloseRoom();
		InitializeComponent();
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
						if((string)item == Program.config.last_deck_name)
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

	public void DeckSelectionChanged(object sender, SelectionChangedEventArgs args)
	{
		Program.config.last_deck_name = args.AddedItems[0]?.ToString();
	}
	public void BackClick(object? sender, RoutedEventArgs? args)
	{
		CloseRoom();
		new ServerWindow
		{
			WindowState = this.WindowState,
		}.Show();
		this.Close();
	}
	public void CloseRoom()
	{
		if(!closed)
		{
			Functions.Request(new ServerPackets.LeaveRequest
			{
				name = name
			}, address, port);
			closed = true;
		}
	}
	private async void TryStartClick(object? sender, RoutedEventArgs args)
	{
		string? deckname = DeckSelectBox.SelectedItem as string;
		if(deckname == null || deckname == "")
		{
			await new ErrorPopup("No deck selected").ShowDialog(this);
			return;
		}
		List<byte>? payload;
		if(
			!UIUtils.TryRequest(new DeckPackets.ListRequest
			{
				name = deckname
			}, out payload, Program.config.deck_edit_url.address, Program.config.deck_edit_url.port, this)
			||
			payload == null)
		{
			return;
		}
		string[]? decklist = Functions.DeserializePayload<DeckPackets.ListResponse>(payload).deck.ToString()?.Split('\n');
		if(decklist == null)
		{
			await new ErrorPopup("Deck list could not be loaded properly").ShowDialog(this);
			return;
		}
		try
		{

			using(TcpClient client = new TcpClient(address, port))
			{
				using(NetworkStream stream = client.GetStream())
				{
					payload = Functions.GeneratePayload<ServerPackets.StartRequest>(new ServerPackets.StartRequest
					{
						decklist = decklist,
						name = ((RoomWindowViewModel)DataContext!).PlayerName,
						noshuffle = NoShuffleBox.IsChecked ?? false
					});
					stream.Write(payload.ToArray(), 0, payload.Count);
					ServerPackets.StartResponse response = Functions.DeserializePayload<ServerPackets.StartResponse>(Functions.ReceivePacket<ServerPackets.StartResponse>(stream)!);
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
								response = Functions.DeserializePayload<ServerPackets.StartResponse>(Functions.ReceivePacket<ServerPackets.StartResponse>(stream)!);
								if(response.success != ServerPackets.StartResponse.Result.Success)
								{
									new ErrorPopup(response.reason ?? "Duel creation failed for unknown reason");
								}
								else
								{
									StartGame(response.port, response.id!);
								}
							});
						}
					}
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
		TcpClient client = new TcpClient();
		await client.ConnectAsync(address, port);
		byte[] idBytes = Encoding.UTF8.GetBytes(id);
		await client.GetStream().WriteAsync(idBytes, 0, idBytes.Length);
		byte[] playerIndex = new byte[1];
		await client.GetStream().ReadExactlyAsync(playerIndex, 0, 1);
		await Dispatcher.UIThread.InvokeAsync(() =>
		{
			new DuelWindow(playerIndex[0], client)
			{
				WindowState = this.WindowState,
			}.Show();
			this.Close();
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
		List<byte>? payload;
		if(!UIUtils.TryRequest(new DeckPackets.NamesRequest(), out payload, Program.config.deck_edit_url.address, Program.config.deck_edit_url.port, null) || payload == null)
		{
			return;
		}
		Decknames.Clear();
		Decknames.AddRange(Functions.DeserializePayload<DeckPackets.NamesResponse>(payload).names);
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

	private List<string> decknames = new List<string>();
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