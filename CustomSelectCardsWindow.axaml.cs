using System;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using CardGameUtils.Structs;
using static CardGameUtils.Functions;
using static CardGameUtils.Structs.NetworkingStructs;

namespace CardGameClient;

public partial class CustomSelectCardsWindow : Window
{
	private readonly Stream stream;
	private bool shouldReallyClose;
	private readonly Action<CardStruct> showCardAction;

	public CustomSelectCardsWindow(string text, CardStruct[] cards, bool initialState, Stream stream, int playerIndex, Action<CardStruct> showCardAction)
	{
		this.stream = stream;
		this.showCardAction = showCardAction;
		Monitor.Enter(stream);
		DataContext = new CustomSelectCardViewModel(text, initialState);
		InitializeComponent();
		Closed += (_, _) => Monitor.Exit(stream);
		Width = Program.config.width / 2;
		Height = Program.config.height / 2;
		CardSelectionList.MaxHeight = Program.config.height / 3;
		CardSelectionList.DataContext = cards;
		CardSelectionList.ItemsSource = cards;
		CardSelectionList.ItemTemplate = new FuncDataTemplate<CardStruct>((value, namescope) =>
		{
			TextBlock block = new()
			{
				Text = value.name,
				TextAlignment = (value.controller == playerIndex) ? TextAlignment.Left : TextAlignment.Right,
			};
			Border border = new()
			{
				Child = block,
				Background = Brushes.Transparent,
			};
			border.PointerEntered += CardPointerEntered;
			return border;
		});
		Closing += (_, args) =>
		{
			args.Cancel = !shouldReallyClose;
		};
	}
	private void CardPointerEntered(object? sender, PointerEventArgs args)
	{
		if(sender == null)
		{
			return;
		}
		if(args.KeyModifiers.HasFlag(KeyModifiers.Control))
		{
			return;
		}
		showCardAction((CardStruct)((Control)sender).DataContext!);
	}

	public void ConfirmClick(object? sender, RoutedEventArgs args)
	{
		stream.Write(GeneratePayload(new DuelPackets.CustomSelectCardsResponse(uids: UIUtils.CardListBoxSelectionToUID(CardSelectionList))));
		shouldReallyClose = true;
		Close();
	}

	public void CardSelectionChanged(object sender, SelectionChangedEventArgs args)
	{
		stream.Write(GeneratePayload(new DuelPackets.CustomSelectCardsIntermediateRequest(uids: UIUtils.CardListBoxSelectionToUID((ListBox)sender))));
		((CustomSelectCardViewModel)DataContext!).CanConfirm = DeserializePayload<DuelPackets.CustomSelectCardsIntermediateResponse>(ReceiveRawPacket((NetworkStream)stream)!).isValid;
	}
}
public class CustomSelectCardViewModel : INotifyPropertyChanged
{
	public CustomSelectCardViewModel(string message, bool initialState)
	{
		Message = message;
		NotifyPropertyChanged("Message");
		CanConfirm = initialState;
	}

	public event PropertyChangedEventHandler? PropertyChanged;
	private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public readonly string Message;
	private bool canConfirm;
	public bool CanConfirm
	{
		get => canConfirm;
		set
		{
			if(value != canConfirm)
			{
				canConfirm = value;
				NotifyPropertyChanged();
			}
		}
	}
}
