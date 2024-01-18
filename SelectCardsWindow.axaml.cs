using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using CardGameUtils.Structs;
using static CardGameUtils.Functions;
using static CardGameUtils.Structs.NetworkingStructs;

namespace CardGameClient;

public partial class SelectCardsWindow : Window
{
	private readonly Stream stream;
	private bool shouldReallyClose;
	private readonly Action<CardStruct> showCardAction;

	public SelectCardsWindow(string text, int amount, CardStruct[] cards, Stream stream, int playerIndex, Action<CardStruct> showCardAction)
	{
		if(cards.Length < amount)
		{
			throw new Exception($"Tried to create a SelectCardWindow requiring to select more cards than possible: {cards.Length}/{amount}");
		}
		this.showCardAction = showCardAction;
		this.stream = stream;
		DataContext = new SelectedCardViewModel(amount);
		InitializeComponent();
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
				TextAlignment = (playerIndex == value.controller) ? TextAlignment.Left : TextAlignment.Right,
			};
			Border border = new()
			{
				Child = block,
				Background = Brushes.Transparent,
			};
			border.PointerEntered += CardPointerEntered;
			return border;
		});
		Message.Text = text;
		Amount.Text = $"/ {amount}";
		Closing += (sender, args) =>
		{
			args.Cancel = !shouldReallyClose;
		};
		if(amount == 1)
		{
			CardSelectionList.SelectionMode = SelectionMode.Single | SelectionMode.Toggle;
		}
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

	public void CardSelectionChanged(object? sender, SelectionChangedEventArgs args)
	{
		if(sender == null)
		{
			return;
		}
		int newCount = ((ListBox)sender).SelectedItems?.Count ?? 0;
		((SelectedCardViewModel)DataContext!).SelectedCount = newCount;
	}

	public void ConfirmClick(object? sender, RoutedEventArgs args)
	{
		stream.Write(GeneratePayload(new DuelPackets.SelectCardsResponse(uids: UIUtils.CardListBoxSelectionToUID(CardSelectionList))));
		shouldReallyClose = true;
		Close();
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
				NotifyPropertyChanged(nameof(CanConfirm));
			}
		}
	}
}
