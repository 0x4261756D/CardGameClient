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

namespace CardGameClient;

public partial class CustomSelectCardsWindow : Window
{
	// DONT USE THIS
	// This only exists because Avalonia requires it
	public CustomSelectCardsWindow()
	{
		InitializeComponent();
		stream = new TcpClient().GetStream();
	}
	private Stream stream;
	private bool shouldReallyClose = false;
	public CustomSelectCardsWindow(string text, CardStruct[] cards, bool initialState, Stream stream, int playerIndex, Action<CardStruct> showCardAction)
	{
		this.stream = stream;
		Monitor.Enter(stream);
		DataContext = new CustomSelectCardViewModel(text, initialState);
		InitializeComponent();
		this.Closed += (_, _) => Monitor.Exit(stream);
		this.Width = Program.config.width / 2;
		this.Height = Program.config.height / 2;
		CardSelectionList.MaxHeight = Program.config.height / 3;
		List<TextBlock> contents = new List<TextBlock>();
		foreach(CardStruct card in cards)
		{
			// TODO: Make this nicer. e.g. group by stuff, etc.
			TextBlock newBlock = new TextBlock
			{
				DataContext = card,
				Text = card.name,
				HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
				VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
				TextAlignment = (card.controller == playerIndex) ? Avalonia.Media.TextAlignment.Left : Avalonia.Media.TextAlignment.Right,
			};
			newBlock.PointerEnter += (sender, args) =>
			{
				if(sender == null) return;
				if(args.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
				showCardAction(card);
			};
			contents.Add(newBlock);
		}
		this.Closing += (_, args) =>
		{
			args.Cancel = !shouldReallyClose;
		};
		CardSelectionList.Items = contents;
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
