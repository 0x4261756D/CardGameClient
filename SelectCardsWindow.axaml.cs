using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CardGameUtils.Structs;
using static CardGameUtils.Functions;
using static CardGameUtils.Structs.NetworkingStructs;

namespace CardGameClient;

public partial class SelectCardsWindow : Window
{
	// DONT USE THIS
	// This only exists because Avalonia requires it
	public SelectCardsWindow()
	{
		stream = new TcpClient().GetStream();
		InitializeComponent();
	}
	private NetworkStream stream;
	private bool reallyClose = false;

	public SelectCardsWindow(string text, int amount, CardStruct[] cards, NetworkStream stream, int playerIndex, Action<CardStruct> showCardAction)
	{
		if(cards.Length < amount)
		{
			throw new Exception($"Tried to create a SelectCardWindow requiring to select more cards than possible: {cards.Length}/{amount}");
		}
		this.stream = stream;
		DataContext = new SelectedCardViewModel(amount);
		InitializeComponent();
		this.Width = Program.config.width / 2;
		this.Height = Program.config.height / 2;
		this.Find<ListBox>("CardSelectionList").MaxHeight = Program.config.height / 3;
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
		this.Find<ListBox>("CardSelectionList").Items = contents;
		this.Find<TextBlock>("Message").Text = text;
		this.Find<TextBlock>("Amount").Text = $"/ {amount}";
		this.Closing += (sender, args) =>
		{
			args.Cancel = !reallyClose;
		};
	}

	public void CardSelectionChanged(object sender, SelectionChangedEventArgs args)
	{
		int newCount = ((ListBox)sender).SelectedItems.Count;
		((SelectedCardViewModel)DataContext!).SelectedCount = newCount;
	}

	public void ConfirmClick(object? sender, RoutedEventArgs args)
	{
		List<byte> payload = GeneratePayload<DuelPackets.SelectCardsResponse>(new DuelPackets.SelectCardsResponse
		{
			uids = UIUtils.CardListBoxSelectionToUID(this.Find<ListBox>("CardSelectionList"))
		});
		stream.Write(payload.ToArray(), 0, payload.Count);
		reallyClose = true;
		this.Close();
	}
}

public class SelectedCardViewModel : INotifyPropertyChanged
{
	public SelectedCardViewModel(int amount)
	{
		Amount = amount;
		NotifyPropertyChanged("Amount");
	}

	public event PropertyChangedEventHandler? PropertyChanged;
	private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public readonly int Amount;

	public bool CanConfirm
	{
		get => SelectedCount == Amount;
	}

	private int selectedCount;
	public int SelectedCount
	{
		get => selectedCount;
		set
		{
			if(selectedCount != value)
			{
				selectedCount = value;
				NotifyPropertyChanged();
				NotifyPropertyChanged("CanConfirm");
			}
		}
	}
}