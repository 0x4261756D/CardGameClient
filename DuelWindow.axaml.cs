using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CardGameUtils;
using static CardGameUtils.Functions;
using static CardGameUtils.Structs.NetworkingStructs;

namespace CardGameClient;

public partial class DuelWindow : Window
{
	// DONT USE THIS
	// This only exists because Avalonia requires it
	private string playerName;
	private int playerIndex;
	private TcpClient client;
	private NetworkStream stream;
	private Thread networkingThread;
	private bool closing = false; public DuelWindow()
	{
		InitializeComponent();
		playerName = "THIS SHOULD NOT HAPPEN";
		client = new TcpClient();
		stream = client.GetStream();
		networkingThread = new Thread(() => { });
	}
	public DuelWindow(string name, int playerIndex, TcpClient client)
	{
		this.playerName = name;
		this.playerIndex = playerIndex;
		InitializeComponent();
		this.client = client;
		stream = client.GetStream();
		networkingThread = new Thread(() => HandleNetwork());
		networkingThread.Start();
		this.Closed += (sender, args) =>
		{
			Cleanup();
		};
	}
	private void SurrenderClick(object? sender, RoutedEventArgs args)
	{
		new ServerWindow().Show();
		Cleanup();
		this.Close();
	}
	private void HandleNetwork()
	{
		Log("Socketthread started");
		while (!closing)
		{
			if (client.Connected)
			{
				Monitor.Enter(stream);
				Log("data available");
				List<byte>? bytes = ReceiveRawPacket(stream, 1000);
				Monitor.Exit(stream);
				if (bytes != null && bytes.Count != 0)
				{
					Log("Client received a request of length " + bytes.Count);
					Task<bool> t = Dispatcher.UIThread.InvokeAsync(() => HandlePacket(bytes));
					t.Wait();
					if (t.Result)
					{
						return;
					}
				}
			}
		}
	}

	private bool HandlePacket(List<byte> bytes)
	{
		if(bytes[0] >= (byte)NetworkingConstants.PacketType.PACKET_COUNT)
		{
			throw new Exception($"Unrecognized packet type ({bytes[0]})");
		}
		NetworkingConstants.PacketType type = (NetworkingConstants.PacketType)bytes[0];
		bytes.RemoveAt(0);
		string payload = Encoding.UTF8.GetString(bytes.ToArray());
		Functions.Log(payload);
		switch (type)
		{
			case NetworkingConstants.PacketType.DuelFieldUpdateRequest:
			{
				UpdateField(DeserializeJson<DuelPackets.FieldUpdateRequest>(payload));
			}
			break;
			default:
				throw new NotImplementedException($"{type}");
		}
		return true;
	}

	private void UpdateField(DuelPackets.FieldUpdateRequest request)
	{
		throw new NotImplementedException();
	}

	private void Cleanup()
	{
		if (closing)
		{
			return;
		}
		closing = true;
		List<byte> payload = GeneratePayload<DuelPackets.SurrenderRequest>(new DuelPackets.SurrenderRequest { });
		stream.Write(payload.ToArray(), 0, payload.Count);
		while (networkingThread.IsAlive)
		{ }
		client.Close();
	}
}