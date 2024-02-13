using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Reactive;
using CardGameUtils;
using CardGameUtils.Structs;
using static CardGameUtils.Functions;
using static CardGameUtils.Structs.NetworkingStructs;

namespace CardGameClient;

public partial class DeckEditWindow : Window
{
	private readonly Flyout moveFlyout = new();
	private CardStruct[]? cardpool;

	public DeckEditWindow()
	{
		InitializeComponent();
		DataContext = new DeckEditWindowViewModel();
		if(DeckSelectBox.SelectedItem == null && DeckSelectBox.ItemCount > 0)
		{
			if(Program.config.last_deck_name != null)
			{
				foreach(var item in DeckSelectBox.Items)
				{
					if((string?)item == Program.config.last_deck_name)
					{
						DeckSelectBox.SelectedItem = item;
						break;
					}
				}
				if(DeckSelectBox.SelectedItem == null)
				{
					DeckSelectBox.SelectedIndex = 0;
				}
			}
			else
			{
				DeckSelectBox.SelectedIndex = 0;
			}
		}
		DecklistPanel.LayoutUpdated += DecklistPanelInitialized;
	}

	private void DecklistPanelInitialized(object? sender, EventArgs e)
	{
		LoadDeck(DeckSelectBox.SelectedItem!.ToString()!);
		LoadSidebar("");
		DecklistPanel.LayoutUpdated -= DecklistPanelInitialized;
	}

	public void BackClick(object sender, RoutedEventArgs args)
	{
		new MainWindow
		{
			WindowState = this.WindowState,
		}.Show();
		this.Close();
	}
	public void SidebarGenericIncludeBoxClick(object? sender, RoutedEventArgs args)
	{
		LoadSidebar(SidebarTextBox?.Text ?? "");
	}
	public void LoadSidebar(string fil)
	{
		GameConstants.PlayerClass playerClass = (GameConstants.PlayerClass?)ClassSelectBox.SelectedItem ?? GameConstants.PlayerClass.All;
		(byte, byte[]?) payload = Request(new DeckPackets.SearchRequest(filter: fil, playerClass: playerClass, includeGenericCards: SidebarGenericIncludeBox.IsChecked ?? false),
			Program.config.deck_edit_url.address, Program.config.deck_edit_url.port);
		cardpool = DeserializePayload<DeckPackets.SearchResponse>(payload).cards;
		List<Control> items = [];
		foreach(CardStruct c in cardpool)
		{
			Viewbox v = UIUtils.CreateGenericCard(c);
			v.PointerEntered += CardHover;
			items.Add(v);
		}
		SidebarList.ItemsSource = items;
	}

	private void CardHover(object? sender, PointerEventArgs args)
	{
		if(sender == null)
		{
			return;
		}
		if(args.KeyModifiers.HasFlag(KeyModifiers.Control))
		{
			return;
		}
		CardStruct c = (CardStruct)((Control)sender).DataContext!;
		UIUtils.CardHover(CardImagePanel, CardTextBlock, c, true);
	}

	public void SidebarSelectionChanged(object? sender, SelectionChangedEventArgs args)
	{
		if(sender == null || cardpool == null || args.AddedItems.Count != 1 || args.RemovedItems.Count != 0)
		{
			return;
		}
		args.Handled = true;
		SidebarList.SelectedItem = null;
		Viewbox v = (Viewbox)args.AddedItems[0]!;
		CardStruct card = (CardStruct)v.DataContext!;
		if(card.card_type == GameConstants.CardType.Quest)
		{
			ClassQuestButton.Content = UIUtils.CreateGenericCard(card);
			ColorWrongThings((GameConstants.PlayerClass?)ClassSelectBox.SelectedItem);
		}
		else
		{
			if(DecklistPanel.Children.Count >= GameConstants.DECK_SIZE)
			{
				return;
			}
			int i = 0;
			foreach(Control c in DecklistPanel.Children)
			{
				if(((CardStruct)((Viewbox)((Button)c).Content!).DataContext!).name == card.name)
				{
					i++;
					if(i >= GameConstants.MAX_CARD_MULTIPLICITY)
					{
						return;
					}
				}
			}
			DecklistPanel.Children.Add(CreateDeckButton(card));
		}
		DeckSizeBlock.Text = DecklistPanel.Children.Count.ToString();
	}

	private void ContentRemoveClick(object sender, RoutedEventArgs args)
	{
		((Button)sender).Content = null;
		ColorWrongThings((GameConstants.PlayerClass?)ClassSelectBox.SelectedItem);
	}

	private void SortDeckClick(object sender, RoutedEventArgs args)
	{
		Control[] children = new Control[DecklistPanel.Children.Count];
		DecklistPanel.Children.CopyTo(children, 0);
		// This is fun, see no problem with this...
		Array.Sort(children, (child1, child2) => ((CardStruct)((Control)((Button)child1).Content!).DataContext!).name.CompareTo(((CardStruct)((Control)((Button)child2).Content!).DataContext!).name));
		DecklistPanel.Children.Clear();
		DecklistPanel.Children.AddRange(children);
	}

	public Button CreateDeckButton(CardStruct c)
	{
		Button b = new()
		{
			DataContext = c,
			Padding = new Thickness(0, 0, 0, 0),
		};
		double xAmount = 10;
		double yAmount = Math.Ceiling(GameConstants.DECK_SIZE / xAmount);
		_ = DecklistBorder.GetObservable(BoundsProperty).Subscribe(new AnonymousObserver<Rect>((a) =>
		{
			b.Width = (a.Width - DecklistBorder.BorderThickness.Left - DecklistBorder.BorderThickness.Right - 20) / xAmount - (b.BorderThickness.Left + b.BorderThickness.Right);
			b.Height = (a.Height - DecklistBorder.BorderThickness.Top - DecklistBorder.BorderThickness.Bottom - 20) / yAmount - (b.BorderThickness.Top + b.BorderThickness.Bottom);
		}));
		Viewbox v = UIUtils.CreateGenericCard(c);
		b.Content = v;
		b.PointerPressed += RemoveCardClick;
		b.Click += MoveClick;
		b.PointerEntered += CardHover;
		return b;
	}
	private void RemoveCardClick(object? sender, RoutedEventArgs args)
	{
		if(sender == null)
		{
			return;
		}
		_ = DecklistPanel.Children.Remove((Button)sender);
		DeckSizeBlock.Text = DecklistPanel.Children.Count.ToString();
	}
	private void MoveClick(object? sender, RoutedEventArgs e)
	{
		Button button = (Button)sender!;
		int index = DecklistPanel.Children.IndexOf(button);
		int max = DecklistPanel.Children.Count - 1;
		StackPanel panel = new();

		NumericUpDown numeric = new()
		{
			AllowSpin = true,
			Value = index,
			Minimum = 0,
			Maximum = max,
			Increment = 1,
		};
		panel.Children.Add(numeric);
		Button submitButton = new()
		{
			Content = new TextBlock
			{
				Text = "Move"
			}
		};
		submitButton.Click += (_, _) =>
		{
			int newInd = (int)numeric.Value;
			if(newInd < 0 || newInd > max)
			{
				return;
			}
			DecklistPanel.Children.RemoveAt(index);
			DecklistPanel.Children.Insert(newInd, button);
		};
		CardStruct c = (CardStruct)((Viewbox)button.Content!).DataContext!;
		if(c.can_be_class_ability)
		{
			Button setAbilityButton = new()
			{
				Content = new TextBlock
				{
					Text = "Set as ability"
				}
			};
			setAbilityButton.Click += (_, _) =>
			{
				Viewbox v = UIUtils.CreateGenericCard(c);
				v.PointerEntered += CardHover;
				ClassAbilityButton.Content = v;
				ColorWrongThings((GameConstants.PlayerClass?)ClassSelectBox.SelectedItem);
			};
			panel.Children.Add(setAbilityButton);
		}
		panel.Children.Add(submitButton);
		moveFlyout.Content = panel;
		moveFlyout.ShowAt(button, true);
	}
	public void DeckSelectionChanged(object sender, SelectionChangedEventArgs args)
	{
		if(args.AddedItems.Count > 0 && DecklistPanel.Bounds.Width > 0 && DecklistPanel.Bounds.Height > 0)
		{
			Program.config.last_deck_name = args.AddedItems[0]?.ToString();
			LoadDeck(args?.AddedItems[0]!.ToString()!);
		}
	}
	public void LoadDeck(string deckName)
	{
		(byte, byte[]?) payload = Request(new DeckPackets.ListRequest(name: deckName),
			Program.config.deck_edit_url.address, Program.config.deck_edit_url.port);
		DecklistPanel.Children.Clear();
		DeckPackets.Deck response = DeserializePayload<DeckPackets.ListResponse>(payload).deck;
		if(response.player_class == GameConstants.PlayerClass.UNKNOWN)
		{
			ClassSelectBox.SelectedIndex = -1;
		}
		else
		{
			ClassSelectBox.SelectedItem = response.player_class;
		}
		foreach(CardStruct c in response.cards)
		{
			DecklistPanel.Children.Add(CreateDeckButton(c));
		}
		ClassAbilityButton.Content = null;
		if(response.ability != null)
		{
			Viewbox v = UIUtils.CreateGenericCard(response.ability);
			v.PointerEntered += CardHover;
			ClassAbilityButton.Content = v;
		}
		ClassQuestButton.Content = null;
		if(response.quest != null)
		{
			Viewbox v = UIUtils.CreateGenericCard(response.quest);
			v.PointerEntered += CardHover;
			ClassQuestButton.Content = v;
		}
		DeckSizeBlock.Text = DecklistPanel.Children.Count.ToString();
		ColorWrongThings(response.player_class);
	}
	public void ClassSelectionChanged(object sender, SelectionChangedEventArgs args)
	{
		GameConstants.PlayerClass? playerClass = args.AddedItems.Count > 0 ? (GameConstants.PlayerClass?)args.AddedItems?[0] : null;
		LoadSidebar(SidebarTextBox?.Text ?? "");
		ColorWrongThings(playerClass);
	}

	private void ColorWrongThings(GameConstants.PlayerClass? playerClass)
	{
		foreach(Control c in DecklistPanel.Children)
		{
			Button child = (Button)c;
			// Oh boy, do I love GUI programming...
			GameConstants.PlayerClass cardClass = ((CardStruct)((Viewbox)child.Content!).DataContext!).card_class;
			if(cardClass != GameConstants.PlayerClass.All && playerClass != GameConstants.PlayerClass.All &&
				cardClass != playerClass)
			{
				child.BorderBrush = Brushes.Red;
				child.BorderThickness = new Thickness(5);
			}
			else
			{
				child.BorderBrush = null;
				child.BorderThickness = new Thickness(1);
			}
		}
		if(ClassQuestButton.Content != null)
		{
			GameConstants.PlayerClass cardClass = ((CardStruct)((Viewbox)ClassQuestButton.Content).DataContext!).card_class;
			if(cardClass != GameConstants.PlayerClass.All && playerClass != GameConstants.PlayerClass.All &&
				cardClass != playerClass)
			{
				ClassQuestButton.BorderBrush = Brushes.Red;
				ClassQuestButton.BorderThickness = new Thickness(5);
			}
			else
			{
				ClassQuestButton.BorderBrush = null;
				ClassQuestButton.BorderThickness = new Thickness(1);
			}
		}
		else
		{
			ClassQuestButton.BorderBrush = null;
			ClassQuestButton.BorderThickness = new Thickness(1);
		}
		if(ClassAbilityButton.Content != null)
		{
			GameConstants.PlayerClass cardClass = ((CardStruct)((Viewbox)ClassAbilityButton.Content).DataContext!).card_class;
			if(cardClass != GameConstants.PlayerClass.All && playerClass != GameConstants.PlayerClass.All &&
				cardClass != playerClass)
			{
				ClassAbilityButton.BorderBrush = Brushes.Red;
				ClassAbilityButton.BorderThickness = new Thickness(5);
			}
			else
			{
				ClassAbilityButton.BorderBrush = null;
				ClassAbilityButton.BorderThickness = new Thickness(1);
			}
		}
		else
		{
			ClassAbilityButton.BorderBrush = null;
			ClassAbilityButton.BorderThickness = new Thickness(1);
		}
	}

	public void CreateNewDeckClick(object? sender, RoutedEventArgs args)
	{
		string? newName = NewDeckName.Text;
		if(newName is null or "")
		{
			return;
		}
		Send(new DeckPackets.ListUpdateRequest
		(
			deck: new DeckPackets.Deck
			{
				cards = [],
				name = newName,
			}
		), Program.config.deck_edit_url.address, Program.config.deck_edit_url.port);
		((DeckEditWindowViewModel)DataContext!).Decknames.Add(newName);
		DeckSelectBox.SelectedItem = newName;
		DeckSizeBlock.Text = "0";
		NewDeckName.Text = "";
	}
	public void SaveDeckClick(object? sender, RoutedEventArgs args)
	{
		if(DeckSelectBox.SelectedItem == null || string.IsNullOrEmpty((string)DeckSelectBox.SelectedItem))
		{
			return;
		}
		GameConstants.PlayerClass playerClass = (GameConstants.PlayerClass?)ClassSelectBox.SelectedItem ?? GameConstants.PlayerClass.UNKNOWN;
		if(playerClass == GameConstants.PlayerClass.All)
		{
			playerClass = GameConstants.PlayerClass.UNKNOWN;
		}
		Viewbox? abilityBox = (Viewbox?)ClassAbilityButton.Content;
		CardStruct? ability = abilityBox == null ? null : (CardStruct?)abilityBox.DataContext;
		Viewbox? questBox = (Viewbox?)ClassQuestButton.Content;
		CardStruct? quest = questBox == null ? null : (CardStruct?)questBox.DataContext;
		Control[] children = new Control[DecklistPanel.Children.Count];
		DecklistPanel.Children.CopyTo(children, 0);
		Send(new DeckPackets.ListUpdateRequest
		(
			deck: new DeckPackets.Deck
			{
				cards = Array.ConvertAll(children, child => (CardStruct)((Viewbox)((Button)child).Content!).DataContext!),
				ability = ability,
				quest = quest,
				player_class = playerClass,
				name = (string)DeckSelectBox.SelectedItem!
			}
		), Program.config.deck_edit_url.address, Program.config.deck_edit_url.port);
	}
	public void DeleteDeckClick(object? sender, RoutedEventArgs args)
	{
		Send(new DeckPackets.ListUpdateRequest
		(
			deck: new DeckPackets.Deck
			{
				name = (string)DeckSelectBox.SelectedItem!
			}
		), Program.config.deck_edit_url.address, Program.config.deck_edit_url.port);
		_ = ((DeckEditWindowViewModel)DataContext!).Decknames.Remove((string)DeckSelectBox.SelectedItem!);
		DeckSelectBox.SelectedIndex = DeckSelectBox.ItemCount - 1;
	}
	public void SidebarTextInput(object? sender, KeyEventArgs args)
	{
		TextBox? tb = (TextBox?)sender;
		LoadSidebar(tb?.Text ?? "");
	}
}


public class DeckEditWindowViewModel : INotifyPropertyChanged
{
	public DeckEditWindowViewModel()
	{
		LoadDecks();
	}

	public void LoadDecks()
	{
		(byte, byte[]?) payload = Request(new DeckPackets.NamesRequest(), Program.config.deck_edit_url.address, Program.config.deck_edit_url.port);
		string[] names = DeserializePayload<DeckPackets.NamesResponse>(payload).names;
		Array.Sort(names);
		Decknames.Clear();
		foreach(string name in names)
		{
			Decknames.Add(name);
		}
		_ = classes.Remove(GameConstants.PlayerClass.UNKNOWN);
	}

	public event PropertyChangedEventHandler? PropertyChanged;


	private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	private readonly ObservableCollection<GameConstants.PlayerClass> classes = new(Enum.GetValues<GameConstants.PlayerClass>());
	public ObservableCollection<GameConstants.PlayerClass> Classes
	{
		get => classes;
	}
	private ObservableCollection<string> decknames = [];
	public ObservableCollection<string> Decknames
	{
		get => decknames;
		set
		{
			if(value != decknames)
			{
				decknames = value;
				NotifyPropertyChanged();
			}
		}
	}
}
