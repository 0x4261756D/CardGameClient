using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using CardGameUtils.Structs;

namespace CardGameClient;

public partial class ViewCardsWindow : Window
{
	// DONT USE THIS
	// This only exists because Avalonia requires it
	public ViewCardsWindow()
	{
		this.showCardAction = (_) => {};
		InitializeComponent();
	}
	private Action<CardStruct> showCardAction;

	public ViewCardsWindow(CardStruct[] cards, string? message, int playerIndex, Action<CardStruct> showCardAction)
	{
		InitializeComponent();
		this.Width = Program.config.width / 2;
		this.Height = Program.config.height / 2;
		this.showCardAction = showCardAction;
		CardSelectionList.MaxHeight = Program.config.height / 3;
		CardSelectionList.DataContext = cards;
		CardSelectionList.Items = cards;
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
			border.PointerEnter += CardPointerEnter;
			return border;
		});
		if(message != null)
		{
			Message.Text = message;
		}

	}
	private void CardPointerEnter(object? sender, PointerEventArgs args)
	{
		if(sender == null) return;
		if(args.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
		showCardAction((CardStruct)((Control)(sender)).DataContext!);
	}
	public void CloseClick(object? sender, RoutedEventArgs args)
	{
		this.Close();
	}
}