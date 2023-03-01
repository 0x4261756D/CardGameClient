using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CardGameUtils;
using CardGameUtils.Structs;
using static CardGameUtils.Structs.NetworkingStructs;

namespace CardGameClient;

public partial class RoomWindow : Window
{
	private string address;
	private int port;
	private string name;
	private bool closed = false;
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
			new ServerWindow
			{
				WindowState = this.WindowState,
			}.Show();
			this.Close();
		}
		if(DeckSelectBox.SelectedItem == null && DeckSelectBox.ItemCount > 0)
		{
			DeckSelectBox.SelectedIndex = 0;
		}
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
	private void TryStartClick(object? sender, RoutedEventArgs args)
	{
		string? deckname = DeckSelectBox.SelectedItem as string;
		if(deckname == null || deckname == "")
		{
			new ErrorPopup("No deck selected").ShowDialog(this);
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
			new ErrorPopup("Deck list could not be loaded properly").ShowDialog(this);
			return;
		}
		if(!UIUtils.TryRequest(new ServerPackets.StartRequest
		{
			decklist = decklist,
			name = ((RoomWindowViewModel)DataContext!).PlayerName,
			noshuffle = NoShuffleBox.IsChecked ?? false
		}, out payload, address, port, this) || payload == null)
		{
			return;
		}
		ServerPackets.StartResponse response = Functions.DeserializePayload<ServerPackets.StartResponse>(payload);
		if(response.success)
		{
			Dispatcher.UIThread.InvokeAsync(() =>
			{
				int playerIndex = -1;
				TcpClient client = CheckForReady(response.id!, response.port, out playerIndex);
				new DuelWindow(((RoomWindowViewModel)DataContext!).PlayerName, playerIndex, client)
				{
					WindowState = this.WindowState,
				}.Show();
				this.Close();
			}, priority: DispatcherPriority.Background);
			((Button)sender!).IsEnabled = false;
		}
		else
		{
			new ErrorPopup(response.reason!).ShowDialog(this);
		}
	}
	private TcpClient CheckForReady(string id, int gamePort, out int playerIndex)
	{
		// AAAAAAAAAAAAAAAHHHHHHHHHH UGLY CODE....
		// TODO: Rework Room to work with a listener instead
		while(true)
		{
			TcpClient c = new TcpClient();
			try
			{
				c.Connect(address, gamePort);
				if(c.Connected)
				{
					byte[] idBytes = Encoding.UTF8.GetBytes(id);
					c.GetStream().Write(idBytes, 0, idBytes.Length);
					do
					{
						playerIndex = c.GetStream().ReadByte();
					}
					while(playerIndex == -1);
					return c;
				}
				else
				{
					c.Close();
				}
			}
			catch(SocketException)
			{
				Thread.Sleep(100);
			}
		}
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