using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CardGameUtils;
using static CardGameUtils.Structs.NetworkingStructs;

namespace CardGameClient;

public partial class ServerWindow : Window
{
	public ServerWindow()
	{
		DataContext = new ServerWindowViewModel();
		InitializeComponent();
		UpdateRoomList();
	}
	public void BackClick(object sender, RoutedEventArgs args)
	{
		new MainWindow
		{
			WindowState = this.WindowState,
		}.Show();
		this.Close();
	}
	private void UpdateRoomList()
	{
		List<byte>? payload;
		if(!ServerTryRequest(new ServerPackets.RoomsRequest(), out payload) || payload == null) return;
		((ServerWindowViewModel)DataContext!).ServerRooms = Functions.DeserializePayload<ServerPackets.RoomsResponse>(payload).rooms;
	}
	private void HostClick(object? sender, RoutedEventArgs args)
	{
		List<byte>? payload;
		string playerName = ((ServerWindowViewModel)DataContext!).PlayerName;
		if(!ServerTryRequest(new ServerPackets.CreateRequest
		{
			name = playerName,
		},
			out payload))
		{
			return;
		}
		if(payload == null)
		{
			new ErrorPopup("Could not get a request from the server").ShowDialog(this);
			return;
		}
		ServerPackets.CreateResponse response = Functions.DeserializePayload<ServerPackets.CreateResponse>(payload);
		if(response.success)
		{
			RoomWindow w = new RoomWindow(ServerAddressBox.Text, 7043, playerName)
			{
				WindowState = this.WindowState,
			};
			if(!w.closed)
			{
				w.Show();
				this.Close();
			}
		}
		else
		{
			new ErrorPopup(response.reason!).ShowDialog(this);
		}
	}
	void RefreshClick(object? sender, RoutedEventArgs args)
	{
		UpdateRoomList();
	}
	private void RoomClick(object sender, RoutedEventArgs args)
	{
		if(PlayerNameBox.Text == "") return;
		List<byte>? payload;
		if(!ServerTryRequest(new ServerPackets.JoinRequest
		{
			name = PlayerNameBox.Text,
			targetName = (string)((Button)sender).Content
		}, out payload) || payload == null)
		{
			return;
		}
		ServerPackets.JoinResponse response = Functions.DeserializePayload<ServerPackets.JoinResponse>(payload);
		if(response.success)
		{
			new RoomWindow(ServerAddressBox.Text, 7043, ((ServerWindowViewModel)DataContext!).PlayerName)
			{
				WindowState = this.WindowState,
			}.Show();
			this.Close();
		}
		else
		{
			new ErrorPopup(response.reason!).ShowDialog(this);
		}
	}
	private bool ServerTryRequest(PacketContent request, out List<byte>? payload)
	{
		return UIUtils.TryRequest(request, out payload, ServerAddressBox.Text, 7043, this, 10000);
	}
}
public class ServerWindowViewModel : INotifyPropertyChanged
{
	public ServerWindowViewModel()
	{
		if(PlayerName == null)
		{
			PlayerName = Convert.ToBase64String(Encoding.UTF8.GetBytes(DateTime.Now.Millisecond + DateTime.Now.ToLongTimeString()));
		}
		if(Program.config.server_address == null)
		{
			Program.config.server_address = "127.0.0.1";
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;
	private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public string ServerAddress
	{
		get
		{
			if(Program.config.server_address == null)
			{
				Program.config.server_address = "127.0.0.1";
			}
			return Program.config.server_address;
		}
		set
		{
			if(value != Program.config.server_address)
			{
				Program.config.server_address = value;
				NotifyPropertyChanged();
			}
		}
	}

	public string PlayerName
	{
		get
		{
			if(Program.config.player_name == null)
			{
				Program.config.player_name = Convert.ToBase64String(Encoding.UTF8.GetBytes(DateTime.Now.Millisecond + DateTime.Now.ToLongTimeString()));
			}
			return Program.config.player_name;
		}
		set
		{
			if(value != Program.config.player_name)
			{
				Program.config.player_name = value;
				NotifyPropertyChanged();
			}
		}
	}
	private string[] serverRooms = new string[0];
	public string[] ServerRooms
	{
		get => serverRooms;
		set
		{
			if(value != serverRooms)
			{
				serverRooms = value;
				NotifyPropertyChanged();
			}
		}
	}
}
