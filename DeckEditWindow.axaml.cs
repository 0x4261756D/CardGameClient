using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
		List<byte> payload = Request(new DeckPackets.SearchRequest() { filter = fil }, Program.config.deck_edit_url.address, Program.config.deck_edit_url.port);
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
			b.Click += AddCardClick;
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

	public void AddCardClick(object? sender, RoutedEventArgs args)
	{
		if (sender != null)
		{
			if (cardpool != null /* && args.AddedItems.Count > 0 */ &&
				DecklistPanel.Children.Count < GameConstants.DECK_SIZE)
			{
				var DecklistPanel = this.Find<WrapPanel>("DecklistPanel");
				CardStruct c = (CardStruct)((Viewbox)(((Button)sender).Content)).DataContext!;
				if (DecklistPanel.Children.Count(x => ((CardStruct)(((Viewbox)(((Button)x).Content)).DataContext!)).name == c.name) == GameConstants.MAX_CARD_MULTIPLICITY)
				{
					return;
				}
				DecklistPanel.Children.Add(CreateDeckButton(c));
			}
			this.Find<TextBlock>("DeckSizeBlock").Text = DecklistPanel.Children.Count.ToString();
		}
	}
	public Button CreateDeckButton(CardStruct c)
	{
		Button b = new Button()
		{
			Width = Program.config.width / 14,
			Height = Program.config.height / 7,
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
		int index = this.Find<WrapPanel>("DecklistPanel").Children.IndexOf(button);
		int max = this.Find<WrapPanel>("DecklistPanel").Children.Count - 1;
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
		submitButton.Click += (sender, _) =>
		{
			int newInd = (int)numeric.Value;
			if (newInd < 0 || newInd > max) return;
			this.Find<WrapPanel>("DecklistPanel").Children.RemoveAt(index);
			this.Find<WrapPanel>("DecklistPanel").Children.Insert(newInd, button);
		};
		panel.Children.Add(submitButton);
		moveFlyout.Content = panel;
		moveFlyout.ShowAt(button, true);
	}
	public void DeckSelectionChanged(object sender, SelectionChangedEventArgs args)
	{
		if (args != null && args.AddedItems.Count > 0)
		{
			List<byte> payload = Request(new DeckPackets.ListRequest
			{
				name = args.AddedItems[0]!.ToString()!
			}, Program.config.deck_edit_url.address, Program.config.deck_edit_url.port);
			DecklistPanel.Children.Clear();
			foreach (CardStruct c in DeserializePayload<DeckPackets.ListResponse>(payload).cards)
			{
				DecklistPanel.Children.Add(CreateDeckButton(c));
			}
		}
		DeckSizeBlock.Text = DecklistPanel.Children.Count.ToString();
	}
	public void ClassSelectionChanged(object sender, SelectionChangedEventArgs args)
	{
		Log($"{args.ToString()}");
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
		var DecklistPanel = this.Find<WrapPanel>("DecklistPanel");
		Request(new DeckPackets.ListUpdateRequest
		{
			deck = new DeckPackets.Deck
			{
				cards = DecklistPanel.Children.ToList().ConvertAll(x => (CardStruct)((Viewbox)((Button)x).Content).DataContext!).ToArray(),
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
		if (sender == null || tb?.Text == null) return;
		LoadSidebar(tb.Text);
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
		classes.Remove(GameConstants.PlayerClass.All);
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
