using System.Collections.Generic;
using System.Net.Sockets;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using static CardGameUtils.Functions;
using static CardGameUtils.Structs.NetworkingStructs;

namespace CardGameClient;

public partial class SelectZoneWindow : Window
{
	// DONT USE THIS
	// This only exists because Avalonia requires it
	public SelectZoneWindow()
	{
		InitializeComponent();
	}

	public SelectZoneWindow(bool[] options, NetworkStream stream)
	{
		InitializeComponent();
		for(int i = 0; i < options.Length; i++)
		{
			Button b = new Button
			{
				Content = i
			};
			b.Click += (sender, _) =>
			{
				int zone = (int)((Button)sender!).Content;
				List<byte> payload = GeneratePayload<DuelPackets.SelectZoneResponse>(new DuelPackets.SelectZoneResponse
				{
					zone = zone
				});
				stream.Write(payload.ToArray(), 0, payload.Count);
				this.Close();
			};
			b.IsEnabled = options[i];
			OptionsPanel.Children.Add(b);
		}
	}
}