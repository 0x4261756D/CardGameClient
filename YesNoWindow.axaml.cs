using System.Collections.Generic;
using System.Net.Sockets;
using Avalonia.Controls;
using Avalonia.Interactivity;
using static CardGameUtils.Functions;
using static CardGameUtils.Structs.NetworkingStructs;

namespace CardGameClient;
public partial class YesNoWindow : Window
{

	// DONT USE THIS
	// This only exists because Avalonia requires it
	public YesNoWindow()
	{
		InitializeComponent();
		stream = new TcpClient().GetStream();
	}
	NetworkStream stream;
	private bool reallyClose = true;
	public YesNoWindow(string description, NetworkStream stream)
	{
		InitializeComponent();
		this.Find<TextBlock>("MessageBlock").Text = description;
		this.stream = stream;
		this.Width = Program.config.width / 2;
		this.Height = Program.config.height / 2;
		this.Closing += (sender, args) =>
		{
			args.Cancel = !reallyClose;
		};
	}

	public void YesClick(object? sender, RoutedEventArgs args)
	{
		List<byte> payload = GeneratePayload<DuelPackets.YesNoResponse>(new DuelPackets.YesNoResponse
		{
			result = true
		});
		stream.Write(payload.ToArray(), 0, payload.Count);
		reallyClose = true;
		Close();
	}

	public void NoClick(object? sender, RoutedEventArgs args)
	{
		List<byte> payload = GeneratePayload<DuelPackets.YesNoResponse>(new DuelPackets.YesNoResponse
		{
			result = false
		});
		stream.Write(payload.ToArray(), 0, payload.Count);
		reallyClose = true;
		Close();
	}
}
