using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
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

	private bool shouldReallyClose = false;

	public SelectZoneWindow(bool[] options, Stream stream)
	{
		InitializeComponent();
		this.Width = Program.config.width / 2;
		this.Height = Program.config.height / 2;
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
				shouldReallyClose = true;
				this.Close();
			};
			b.IsEnabled = options[i];
			OptionsPanel.Children.Add(b);
		}
		this.Closing += (_, args) =>
		{
			args.Cancel = !shouldReallyClose;
		};
	}
}