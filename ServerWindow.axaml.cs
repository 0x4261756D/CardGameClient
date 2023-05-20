using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
		(byte, byte[]?)? payload = ServerTryRequest(new ServerPackets.RoomsRequest());
		if(payload == null)
		{
			new ErrorPopup("Connection to the server timed out").Show();
			return;
		}
		((ServerWindowViewModel)DataContext!).ServerRooms = Functions.DeserializePayload<ServerPackets.RoomsResponse>(payload.Value).rooms;
	}
	private void HostClick(object? sender, RoutedEventArgs args)
	{
		string playerName = ((ServerWindowViewModel)DataContext!).PlayerName;
		(byte, byte[]?)? payload = ServerTryRequest(new ServerPackets.CreateRequest
		{
			name = playerName,
		});
		if(payload == null)
		{
			return;
		}
		ServerPackets.CreateResponse response = Functions.DeserializePayload<ServerPackets.CreateResponse>(payload.Value);
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
		(byte, byte[]?)? payload = ServerTryRequest(new ServerPackets.JoinRequest
		{
			name = PlayerNameBox.Text,
			targetName = (string)((Button)sender).Content
		});
		if(payload == null)
		{
			new ErrorPopup("Connection to the server timed out").ShowDialog(this);
			return;
		}
		ServerPackets.JoinResponse response = Functions.DeserializePayload<ServerPackets.JoinResponse>(payload.Value);
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
	private (byte, byte[]?)? ServerTryRequest(PacketContent request)
	{
		return UIUtils.TryRequest(request, ServerAddressBox.Text, 7043, this);
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
