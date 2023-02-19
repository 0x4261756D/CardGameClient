using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using CardGameUtils;
using CardGameUtils.Structs;
using static CardGameUtils.Functions;
using static CardGameUtils.Structs.NetworkingStructs;

namespace CardGameClient;

public partial class DeckEditWindow : Window
{
	private Flyout moveFlyout = new Flyout();
	private CardStruct[]? cardpool;
	public DeckEditWindow()
	{
		InitializeComponent();
		DataContext = new DeckEditWindowViewModel();
		if (DeckSelectBox.SelectedItem == null && DeckSelectBox.ItemCount > 0)
		{
			DeckSelectBox.SelectedIndex = 0;
		}
		LoadSidebar("");
		DecklistPanel.LayoutUpdated += DecklistPanelInitialized;
	}

	private void DecklistPanelInitialized(object? sender, EventArgs e)
	{
		LoadDeck(DeckSelectBox.SelectedItem!.ToString()!);
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
	public void LoadSidebar(string fil)
	{
		GameConstants.PlayerClass playerClass = (GameConstants.PlayerClass?)ClassSelectBox.SelectedItem ?? GameConstants.PlayerClass.All;
		List<byte> payload = Request(new DeckPackets.SearchRequest() { filter = fil, playerClass = playerClass },
			Program.config.deck_edit_url.address, Program.config.deck_edit_url.port);
		cardpool = DeserializePayload<DeckPackets.SearchResponse>(payload).cards;
		List<Control> items = new List<Control>();
		foreach (CardStruct c in cardpool)
		{
			Viewbox v = UIUtils.CreateGenericCard(c);
			v.PointerEnter += CardHover;
			Button b = new Button
			{
				Content = v,
			};
			if(c.card_type == GameConstants.CardType.Quest)
			{
				b.Click += SetCardAsQuestClick;
			}
			else
			{
				b.Click += AddCardToDeckClick;
			}
			items.Add(b);
		}
		SidebarList.Items = items;
	}

	private void CardHover(object? sender, PointerEventArgs args)
	{
		if (sender == null) return;
		if (args.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
		CardImagePanel.Children.Clear();
		CardStruct c = ((CardStruct)(((Viewbox)sender).DataContext!));
		Viewbox v = UIUtils.CreateGenericCard(c);
		CardImagePanel.Children.Add(v);
		CardTextBlock.Text = c.Format(inDeckEdit: true);
	}

	public void AddCardToDeckClick(object? sender, RoutedEventArgs args)
	{
		if (sender != null)
		{
			if (cardpool != null /* && args.AddedItems.Count > 0 */ &&
				DecklistPanel.Children.Count < GameConstants.DECK_SIZE)
			{
				CardStruct c = (CardStruct)((Viewbox)(((Button)sender).Content)).DataContext!;
				if (DecklistPanel.Children.Count(x => ((CardStruct)(((Viewbox)(((Button)x).Content)).DataContext!)).name == c.name) == GameConstants.MAX_CARD_MULTIPLICITY)
				{
					return;
				}
				DecklistPanel.Children.Add(CreateDeckButton(c));
			}
			DeckSizeBlock.Text = DecklistPanel.Children.Count.ToString();
		}
	}
	private void SetCardAsQuestClick(object? sender, RoutedEventArgs e)
	{
		if(sender != null)
		{
			Button ClassQuestButton = this.Find<Button>("ClassQuestButton");
			Viewbox v = UIUtils.CreateGenericCard((CardStruct)((Viewbox)((Button)sender).Content).DataContext!);
			ClassQuestButton.Content = v;
		}
	}

	private void ContentRemoveClick(object sender, RoutedEventArgs args)
	{
		((Button)sender).Content = null;
	}

	public Button CreateDeckButton(CardStruct c)
	{
		Button b = new Button()
		{
			Width = (DecklistPanel.Bounds.Size.Width - 5) / 10,
			Height = (DecklistPanel.Bounds.Size.Height / 4 - 5) - 1,
		};
		b.Padding = new Avalonia.Thickness(0, 0, 0, 0);
		Viewbox v = UIUtils.CreateGenericCard(c);
		v.PointerEnter += CardHover;
		b.Content = v;
		b.PointerPressed += RemoveCardClick;
		/* b.PointerEnter += CardHover; */
		b.Click += MoveClick;
		return b;
	}
	private void RemoveCardClick(object? sender, RoutedEventArgs args)
	{
		if (sender == null) return;
		var DecklistPanel = this.Find<WrapPanel>("DecklistPanel");
		DecklistPanel.Children.Remove((Button)sender);
		this.Find<TextBlock>("DeckSizeBlock").Text = DecklistPanel.Children.Count.ToString();
	}
	private void MoveClick(object? sender, RoutedEventArgs e)
	{
		Button button = (Button)sender!;
		int index = DecklistPanel.Children.IndexOf(button);
		int max = DecklistPanel.Children.Count - 1;
		StackPanel panel = new StackPanel();

		NumericUpDown numeric = new NumericUpDown
		{
			AllowSpin = true,
			Value = index,
			Minimum = 0,
			Maximum = max,
			Increment = 1,
		};
		panel.Children.Add(numeric);
		Button submitButton = new Button
		{
			Content = new TextBlock
			{
				Text = "Move"
			}
		};
		submitButton.Click += (_, _) =>
		{
			int newInd = (int)numeric.Value;
			if (newInd < 0 || newInd > max) return;
			DecklistPanel.Children.RemoveAt(index);
			DecklistPanel.Children.Insert(newInd, button);
		};
		CardStruct c = ((CardStruct)((Viewbox)button.Content).DataContext!);
		if(c.can_be_class_ability)
		{
			Button setAbilityButton = new Button
			{
				Content = new TextBlock
				{
					Text = "Set as ability"
				}
			};
			setAbilityButton.Click += (_, _) =>
			{
				ClassAbilityButton.Content = UIUtils.CreateGenericCard(c);
			};
			panel.Children.Add(setAbilityButton);
		}
		panel.Children.Add(submitButton);
		moveFlyout.Content = panel;
		moveFlyout.ShowAt(button, true);
	}
	public void DeckSelectionChanged(object sender, SelectionChangedEventArgs args)
	{
		if (args != null && args.AddedItems.Count > 0 && args.AddedItems[0] != null && !DecklistPanel.Bounds.IsEmpty)
		{
			LoadDeck(args.AddedItems[0]!.ToString()!);
		}
	}
	public void LoadDeck(string deckName)
	{
		List<byte> payload = Request(new DeckPackets.ListRequest
		{
			name = deckName
		}, Program.config.deck_edit_url.address, Program.config.deck_edit_url.port);
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
			ClassAbilityButton.Content = UIUtils.CreateGenericCard(response.ability);
		}
		ClassQuestButton.Content = null;
		if(response.quest != null)
		{
			ClassQuestButton.Content = UIUtils.CreateGenericCard(response.quest);
		}
		DeckSizeBlock.Text = DecklistPanel.Children.Count.ToString();
	}
	public void ClassSelectionChanged(object sender, SelectionChangedEventArgs args)
	{
		GameConstants.PlayerClass? playerClass = args.AddedItems.Count > 0 ? (GameConstants.PlayerClass?)args.AddedItems?[0] : null;
		LoadSidebar(SidebarTextBox?.Text ?? "");
		foreach(Button child in DecklistPanel.Children)
		{
												// Oh boy, do I love GUI programming...
			GameConstants.PlayerClass cardClass = ((CardStruct)((Viewbox)child.Content).DataContext!).card_class;
			if(cardClass != GameConstants.PlayerClass.All && playerClass != GameConstants.PlayerClass.All &&
				cardClass != playerClass)
			{
				child.BorderBrush = Brushes.Red;
				child.BorderThickness = new Avalonia.Thickness(5);
			}
			else
			{
				child.BorderBrush = null;
				child.BorderThickness = new Avalonia.Thickness(1);
			}
		}
		if(ClassQuestButton.Content != null)
		{
			GameConstants.PlayerClass cardClass = ((CardStruct)((Viewbox)ClassQuestButton.Content).DataContext!).card_class;
			if(cardClass != GameConstants.PlayerClass.All && playerClass != GameConstants.PlayerClass.All &&
				cardClass != playerClass)
			{
				ClassQuestButton.BorderBrush = Brushes.Red;
				ClassQuestButton.BorderThickness = new Avalonia.Thickness(5);
			}
			else
			{
				ClassQuestButton.BorderBrush = null;
				ClassQuestButton.BorderThickness = new Avalonia.Thickness(1);
			}
		}
		else
		{
			ClassQuestButton.BorderBrush = null;
			ClassQuestButton.BorderThickness = new Avalonia.Thickness(1);
		}
		if(ClassAbilityButton.Content != null)
		{
			GameConstants.PlayerClass cardClass = ((CardStruct)((Viewbox)ClassAbilityButton.Content).DataContext!).card_class;
			if(cardClass != GameConstants.PlayerClass.All && playerClass != GameConstants.PlayerClass.All &&
				cardClass != playerClass)
			{
				ClassAbilityButton.BorderBrush = Brushes.Red;
				ClassAbilityButton.BorderThickness = new Avalonia.Thickness(5);
			}
			else
			{
				ClassAbilityButton.BorderBrush = null;
				ClassAbilityButton.BorderThickness = new Avalonia.Thickness(1);
			}
		}
		else
		{
			ClassAbilityButton.BorderBrush = null;
			ClassAbilityButton.BorderThickness = new Avalonia.Thickness(1);
		}
	}
	public void CreateNewDeckClick(object? sender, RoutedEventArgs args)
	{
		string newName = this.Find<TextBox>("NewDeckName").Text;
		if (newName == "") return;
		Request(new DeckPackets.ListUpdateRequest
		{
			deck = new DeckPackets.Deck
			{
				cards = new CardStruct[0],
				name = newName,
			}
		}, Program.config.deck_edit_url.address, Program.config.deck_edit_url.port);
		((DeckEditWindowViewModel)DataContext!).Decknames.Add(newName);
		DeckSelectBox.SelectedItem = newName;
		this.Find<TextBlock>("DeckSizeBlock").Text = "0";
		NewDeckName.Text = "";
	}
	public void SaveDeckClick(object? sender, RoutedEventArgs args)
	{
		if(DeckSelectBox.SelectedItem == null || (string)DeckSelectBox.SelectedItem == "")
		{
			return;
		}
		GameConstants.PlayerClass playerClass = (GameConstants.PlayerClass?)ClassSelectBox.SelectedItem ?? GameConstants.PlayerClass.UNKNOWN;
		if(playerClass == GameConstants.PlayerClass.All)
		{
			playerClass = GameConstants.PlayerClass.UNKNOWN;
		}
		Viewbox? abilityBox = (Viewbox)this.Find<Button>("ClassAbilityButton").Content;
		CardStruct? ability = abilityBox == null ? null : (CardStruct?)(abilityBox).DataContext;
		Viewbox? questBox = (Viewbox)this.Find<Button>("ClassQuestButton").Content;
		CardStruct? quest = questBox == null ? null : (CardStruct?)(questBox).DataContext;
		Request(new DeckPackets.ListUpdateRequest
		{
			deck = new DeckPackets.Deck
			{
				cards = DecklistPanel.Children.ToList().ConvertAll(x => (CardStruct)((Viewbox)((Button)x).Content).DataContext!).ToArray(),
				ability = ability,
				quest = quest,
				player_class = playerClass,
				name = ((string)DeckSelectBox.SelectedItem!)
			}
		}, Program.config.deck_edit_url.address, Program.config.deck_edit_url.port);
	}
	public void DeleteDeckClick(object? sender, RoutedEventArgs args)
	{
		Request(new DeckPackets.ListUpdateRequest
		{
			deck = new DeckPackets.Deck
			{
				name = ((string)DeckSelectBox.SelectedItem!)
			}
		}, Program.config.deck_edit_url.address, Program.config.deck_edit_url.port);
		((DeckEditWindowViewModel)DataContext!).Decknames.Remove((string)DeckSelectBox.SelectedItem!);
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
		List<byte> payload = Request(new DeckPackets.NamesRequest(), Program.config.deck_edit_url.address, Program.config.deck_edit_url.port);
		Decknames.Clear();
		foreach (string name in DeserializePayload<DeckPackets.NamesResponse>(payload).names)
		{
			Decknames.Add(name);
		}
		classes.Remove(GameConstants.PlayerClass.UNKNOWN);
	}

	public event PropertyChangedEventHandler? PropertyChanged;


	private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	private ObservableCollection<GameConstants.PlayerClass> classes = new ObservableCollection<GameConstants.PlayerClass>(Enum.GetValues<GameConstants.PlayerClass>());
	public ObservableCollection<GameConstants.PlayerClass> Classes
	{
		get => classes;
	}
	private ObservableCollection<string> decknames = new ObservableCollection<string>();
	public ObservableCollection<string> Decknames
	{
		get => decknames;
		set
		{
			if (value != decknames)
			{
				decknames = value;
				NotifyPropertyChanged();
			}
		}
	}
}
