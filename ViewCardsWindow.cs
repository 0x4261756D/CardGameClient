using System;
using System.Collections.Generic;
using Avalonia.Controls;
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
		InitializeComponent();
	}

	public ViewCardsWindow(CardStruct[] cards, string? message, int playerIndex, Action<CardStruct> showCardAction)
	{
		InitializeComponent();
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
		CardSelectionList.Items = contents;
		if(message != null)
		{
			Message.Text = message;
		}

	}
	public void CloseClick(object? sender, RoutedEventArgs args)
	{
		this.Close();
	}
}