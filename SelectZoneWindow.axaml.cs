using System.IO;
using Avalonia.Controls;
using static CardGameUtils.Functions;
using static CardGameUtils.Structs.NetworkingStructs;

namespace CardGameClient;

public partial class SelectZoneWindow : Window
{
	private bool shouldReallyClose;

	public SelectZoneWindow(bool[] options, Stream stream)
	{
		InitializeComponent();
		Width = Program.config.width / 2;
		Height = Program.config.height / 2;
		for(int i = 0; i < options.Length; i++)
		{
			Button b = new()
			{
				Content = i
			};
			b.Click += (sender, _) =>
			{
				int zone = (int)((Button)sender!).Content!;
				stream.Write(GeneratePayload(new DuelPackets.SelectZoneResponse(zone: zone)));
				shouldReallyClose = true;
				Close();
			};
			b.IsEnabled = options[i];
			OptionsPanel.Children.Add(b);
		}
		Closing += (_, args) =>
		{
			args.Cancel = !shouldReallyClose;
		};
	}
}
