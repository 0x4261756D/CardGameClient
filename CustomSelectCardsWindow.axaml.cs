using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CardGameUtils.Structs;
using static CardGameUtils.Structs.NetworkingStructs;
using static CardGameUtils.Functions;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System;
using System.IO;
using Avalonia.Controls.Templates;

namespace CardGameClient;

public partial class CustomSelectCardsWindow : Window
{
	// DONT USE THIS
	// This only exists because Avalonia requires it
	public CustomSelectCardsWindow()
	{
		InitializeComponent();
		stream = new TcpClient().GetStream();
		showCardAction = (_) => {};
	}
	private Stream stream;
	private bool shouldReallyClose = false;
	private Action<CardStruct> showCardAction;

	public CustomSelectCardsWindow(string text, CardStruct[] cards, bool initialState, Stream stream, int playerIndex, Action<CardStruct> showCardAction)
	{
		this.stream = stream;
		this.showCardAction = showCardAction;
		Monitor.Enter(stream);
		DataContext = new CustomSelectCardViewModel(text, initialState);
		InitializeComponent();
		this.Closed += (_, _) => Monitor.Exit(stream);
		this.Width = Program.config.width / 2;
		this.Height = Program.config.height / 2;
		CardSelectionList.MaxHeight = Program.config.height / 3;
		CardSelectionList.DataContext = cards;
		CardSelectionList.ItemsSource = cards;
		CardSelectionList.ItemTemplate = new FuncDataTemplate<CardStruct>((value, namescope) =>
		{
			TextBlock block = new TextBlock
			{
				Text = value.name,
			};
			Border border = new Border
			{
				Child = block,
				Background = Avalonia.Media.Brushes.Transparent,
			};
			border.PointerEntered += CardPointerEntered;
			return border;
		});
		this.Closing += (_, args) =>
		{
			args.Cancel = !shouldReallyClose;
		};
	}
	private void CardPointerEntered(object? sender, PointerEventArgs args)
	{
		if(sender == null) return;
		if(args.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
		showCardAction((CardStruct)((Control)(sender)).DataContext!);
	}

	public void ConfirmClick(object? sender, RoutedEventArgs args)
	{
		List<byte> result = GeneratePayload<DuelPackets.CustomSelectCardsResponse>(new DuelPackets.CustomSelectCardsResponse
		{
			uids = UIUtils.CardListBoxSelectionToUID(CardSelectionList),
		});
		stream.Write(result.ToArray(), 0, result.Count);
		shouldReallyClose = true;
		this.Close();
	}

	public void CardSelectionChanged(object sender, SelectionChangedEventArgs args)
	{
		List<byte> payload = GeneratePayload<DuelPackets.CustomSelectCardsIntermediateRequest>(new DuelPackets.CustomSelectCardsIntermediateRequest
		{
			uids = UIUtils.CardListBoxSelectionToUID((ListBox)sender),
		});
		stream.Write(payload.ToArray(), 0, payload.Count);
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
