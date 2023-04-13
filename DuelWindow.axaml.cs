using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using CardGameUtils;
using CardGameUtils.Structs;
using static CardGameUtils.Functions;
using static CardGameUtils.Structs.NetworkingStructs;

namespace CardGameClient;

public partial class DuelWindow : Window
{
	private int playerIndex;
	private TcpClient client;
	private Stream stream;
	private Task networkingTask;
	private Flyout optionsFlyout = new Flyout();
	Queue<DuelPackets.FieldUpdateRequest> fieldUpdateQueue = new Queue<DuelPackets.FieldUpdateRequest>();
	private Task? fieldUpdateTask = null;
	private int animationDelayInMs = 250;
	private bool closing = false;
	private bool shouldEnablePassButtonAfterUpdate = false;

	// This constructor creates a completely empty duel window with no interaction possibility
	public DuelWindow()
	{
		InitializeComponent();
		client = new TcpClient();
		stream = new MemoryStream();
		networkingTask = new Task(() => { });
		OppField.LayoutUpdated += FieldInitialized;
		OwnField.LayoutUpdated += FieldInitialized;
	}
	public DuelWindow(int playerIndex, TcpClient client)
	{
		this.playerIndex = playerIndex;
		InitializeComponent();
		this.client = client;
		stream = client.GetStream();
		networkingTask = new Task(HandleNetwork, TaskCreationOptions.LongRunning);
		networkingTask.Start();
		this.Closed += (sender, args) =>
		{
			Cleanup();
		};
		OppField.LayoutUpdated += FieldInitialized;
		OwnField.LayoutUpdated += FieldInitialized;
	}

	private void FieldInitialized(object? sender, EventArgs e)
	{
		if(sender == null) return;
		WrapPanel panel = (WrapPanel)sender;
		for(int i = 0; i < GameConstants.FIELD_SIZE; i++)
		{
			panel.Children.Add(new Button
			{
				Width = (panel.Bounds.Width - 10) / GameConstants.FIELD_SIZE,
				Height = panel.Bounds.Height - 10,
			});
		}
		panel.LayoutUpdated -= FieldInitialized;
	}

	private void SurrenderClick(object? sender, RoutedEventArgs args)
	{
		new ServerWindow().Show();
		Cleanup();
		this.Close();
	}
	private void PassClick(object? sender, RoutedEventArgs args)
	{
		List<byte> payload = GeneratePayload<DuelPackets.PassRequest>(new DuelPackets.PassRequest { });
		stream.Write(payload.ToArray(), 0, payload.Count);
	}
	private async void HandleNetwork()
	{
		Log("Socketthread started");
		while(!closing)
		{
			if(client.Connected)
			{
				Monitor.Enter(stream);
				Log("data available");
				List<byte>? bytes = ReceiveRawPacket((NetworkStream)stream, 1000);
				Monitor.Exit(stream);
				if(bytes != null && bytes.Count != 0)
				{
					Log("Client received a request of length " + bytes.Count);
					if(await Dispatcher.UIThread.InvokeAsync(() => HandlePacket(bytes)))
					{
						return;
					}
				}
				if(fieldUpdateQueue.Count > 0 && (fieldUpdateTask == null || (fieldUpdateTask != null && fieldUpdateTask.IsCompleted)))
				{
					await Dispatcher.UIThread.InvokeAsync(() => 
					{
						optionsFlyout.Hide();
						PassButton.IsEnabled = false;
					});
					fieldUpdateTask = Task.Delay(animationDelayInMs).ContinueWith((_) => Dispatcher.UIThread.InvokeAsync(UpdateField));
				}
				else if(shouldEnablePassButtonAfterUpdate)
				{
					await Dispatcher.UIThread.InvokeAsync(() => 
					{
						PassButton.IsEnabled = true;
					});					
				}
			}
		}
	}

	private bool HandlePacket(List<byte> bytes)
	{
		if(bytes[0] >= (byte)NetworkingConstants.PacketType.PACKET_COUNT)
		{
			Log($"Unrecognized packet type ({bytes[0]})");
			throw new Exception($"Unrecognized packet type ({bytes[0]})");
		}
		NetworkingConstants.PacketType type = (NetworkingConstants.PacketType)bytes[0];
		string payload = Encoding.UTF8.GetString(bytes.GetRange(1, bytes.Count - 1).ToArray());
		Functions.Log(payload);
		switch(type)
		{
			case NetworkingConstants.PacketType.DuelFieldUpdateRequest:
			{
				EnqueueFieldUpdate(DeserializeJson<DuelPackets.FieldUpdateRequest>(payload));
			}
			break;
			case NetworkingConstants.PacketType.DuelYesNoRequest:
			{
				Log("Received a yesno requets", severity: LogSeverity.Error);
				new YesNoWindow(DeserializeJson<DuelPackets.YesNoRequest>(payload).question, stream).Show();
			}
			break;
			case NetworkingConstants.PacketType.DuelCustomSelectCardsRequest:
			{
				DuelPackets.CustomSelectCardsRequest request = DeserializeJson<DuelPackets.CustomSelectCardsRequest>(payload);
				new CustomSelectCardsWindow(request.desc!, request.cards, request.initialState, stream, playerIndex, ShowCard).Show();
			}
			break;
			case NetworkingConstants.PacketType.DuelGetOptionsResponse:
			{
				UpdateCardOptions(DeserializeJson<DuelPackets.GetOptionsResponse>(payload));
			}
			break;
			case NetworkingConstants.PacketType.DuelSelectZoneRequest:
			{
				new SelectZoneWindow(DeserializeJson<DuelPackets.SelectZoneRequest>(payload).options, stream).Show();
			}
			break;
			case NetworkingConstants.PacketType.DuelGameResultResponse:
			{
				new GameResultWindow(this, DeserializeJson<DuelPackets.GameResultResponse>(payload)).Show();
			}
			break;
			case NetworkingConstants.PacketType.DuelSelectCardsRequest:
			{
				DuelPackets.SelectCardsRequest request = DeserializeJson<DuelPackets.SelectCardsRequest>(payload);
				new SelectCardsWindow(request.desc!, request.amount, request.cards, stream, playerIndex, ShowCard).Show();
			}
			break;
			case NetworkingConstants.PacketType.DuelViewCardsResponse:
			{
				DuelPackets.ViewCardsResponse request = DeserializeJson<DuelPackets.ViewCardsResponse>(payload);
				new ViewCardsWindow(cards: request.cards, message: request.message, playerIndex: playerIndex, showCardAction: ShowCard).Show();
			}
			break;
			default:
				Log($"Unimplemented: {type}");
				throw new NotImplementedException($"{type}");
		}
		return false;
	}

	private void UpdateCardOptions(DuelPackets.GetOptionsResponse response)
	{
		if(response.location == GameConstants.Location.Hand)
		{
			foreach(Button b in OwnHandPanel.Children)
			{
				if(((CardStruct)b.DataContext!).uid == response.uid)
				{
					StackPanel p = new StackPanel();
					foreach(string text in response.options)
					{
						Button option = new Button
						{
							Content = new TextBlock
							{
								Text = text
							}
						};
						option.Click += (_, _) => SendCardOption(text, response.uid, response.location);
						p.Children.Add(option);
					}
					optionsFlyout.Content = p;
					optionsFlyout.ShowAt(b, true);
					return;
				}
			}
		}
		else if(response.location == GameConstants.Location.Field)
		{
			foreach(Button b in OwnField.Children)
			{
				if(b.DataContext == null || b.DataContext == this.DataContext)
				{
					continue;
				}
				if(((CardStruct)b.DataContext).uid == response.uid)
				{
					StackPanel p = new StackPanel();
					foreach(string text in response.options)
					{
						Button option = new Button
						{
							Content = new TextBlock
							{
								Text = text
							}
						};
						option.Click += (_, _) => SendCardOption(text, response.uid, response.location);
						p.Children.Add(option);
					}
					optionsFlyout.Content = p;
					optionsFlyout.ShowAt(b, true);
					return;
				}
			}
		}
		else if(response.location == GameConstants.Location.Quest)
		{
			StackPanel p = new StackPanel();
			foreach(string text in response.options)
			{
				Button option = new Button
				{
					Content = new TextBlock
					{
						Text = text
					}
				};
				option.Click += (_, _) => SendCardOption(text, response.uid, response.location);
				p.Children.Add(option);
			}
			optionsFlyout.Content = p;
			optionsFlyout.ShowAt(OwnQuestPanel, true);
		}
		else if(response.location == GameConstants.Location.Ability)
		{
			StackPanel p = new StackPanel();
			foreach(string text in response.options)
			{
				Button option = new Button
				{
					Content = new TextBlock
					{
						Text = text
					}
				};
				option.Click += (_, _) => SendCardOption(text, response.uid, response.location);
				p.Children.Add(option);
			}
			optionsFlyout.Content = p;
			optionsFlyout.ShowAt(OwnAbilityPanel, true);
		}
		else
		{
			throw new NotImplementedException($"Updating card options at {Enum.GetName<GameConstants.Location>(response.location)}");
		}
	}

	public void OppGraveClick(object? sender, RoutedEventArgs args)
	{
		List<byte> payload = GeneratePayload<DuelPackets.ViewGraveRequest>(new DuelPackets.ViewGraveRequest { opponent = true });
		stream.Write(payload.ToArray(), 0, payload.Count);
	}
	public void OwnGraveClick(object? sender, RoutedEventArgs args)
	{
		List<byte> payload = GeneratePayload<DuelPackets.ViewGraveRequest>(new DuelPackets.ViewGraveRequest { opponent = false });
		stream.Write(payload.ToArray(), 0, payload.Count);
	}
	private void SendCardOption(string option, int uid, GameConstants.Location location)
	{
		List<byte> payload = GeneratePayload<DuelPackets.SelectOptionRequest>(new DuelPackets.SelectOptionRequest
		{
			desc = option,
			location = location,
			uid = uid
		});
		stream.Write(payload.ToArray(), 0, payload.Count);
	}

	public void EnqueueFieldUpdate(DuelPackets.FieldUpdateRequest request)
	{
		fieldUpdateQueue.Enqueue(request);
	}

	public void UpdateField()
	{
		Log($"{fieldUpdateQueue.Count}");
		if(fieldUpdateQueue.Count == 0)
		{
			return;
		}
		DuelPackets.FieldUpdateRequest request = fieldUpdateQueue.Dequeue();
		TurnBlock.Text = $"Turn {request.turn}";
		InitBlock.Text = request.hasInitiative ? "You have initiative" : "Your opponent has initiative";
		DirectionBlock.Text = "Battle direction: " + (request.battleDirectionLeftToRight ? "->" : "<-");
		Background = request.hasInitiative ? Brushes.Purple : Brushes.Black;
		shouldEnablePassButtonAfterUpdate = request.hasInitiative;

		OppNameBlock.Text = request.oppField.name;
		OppLifeBlock.Text = $"Life: {request.oppField.life}";
		OppMomentumBlock.Text = $"Momentum: {request.oppField.momentum}";
		OppDeckButton.Content = request.oppField.deckSize;
		OppGraveButton.Content = request.oppField.graveSize;
		OppAbilityPanel.Children.Clear();
		OppAbilityPanel.Children.Add(CreateCardButton(request.oppField.ability));
		OppQuestPanel.Children.Clear();
		OppQuestPanel.Children.Add(CreateCardButton(request.oppField.quest));
		Avalonia.Thickness oppBorderThickness = new Avalonia.Thickness(2, 2, 2, 0);
		for(int i = 0; i < GameConstants.FIELD_SIZE; i++)
		{
			CardStruct? c = request.oppField.field[GameConstants.FIELD_SIZE - i - 1];
			if(c != null)
			{
				Button b = CreateCardButton(c);
				if(request.markedZone != null && i == request.markedZone)
				{
					b.BorderBrush = Brushes.Yellow;
					b.BorderThickness = oppBorderThickness;
				}
				OppField.Children[i] = b;
			}
			else
			{
				Button b = new Button
				{
					Width = (OppField.Bounds.Width - 10) / GameConstants.FIELD_SIZE,
					Height = OppField.Bounds.Height - 10,
				};
				if(request.markedZone != null && i == request.markedZone)
				{
					b.BorderBrush = Brushes.Yellow;
					b.BorderThickness = oppBorderThickness;
				}
				OppField.Children[i] = b;
			}
		}
		OppHandPanel.Children.Clear();
		for(int i = 0; i < request.oppField.hand.Length; i++)
		{
			OppHandPanel.Children.Add(CreateCardButton(request.oppField.hand[i]));
		}
		OppShowPanel.Children.Clear();
		if(request.oppField.shownCard != null)
		{
			OppShowPanel.Children.Add(CreateCardButton(request.oppField.shownCard));
		}

		OwnNameBlock.Text = request.ownField.name;
		OwnLifeBlock.Text = $"Life: {request.ownField.life}";
		OwnMomentumBlock.Text = $"Momentum: {request.ownField.momentum}";
		OwnDeckButton.Content = request.ownField.deckSize;
		OwnGraveButton.Content = request.ownField.graveSize;
		OwnAbilityPanel.Children.Clear();
		OwnAbilityPanel.Children.Add(CreateCardButton(request.ownField.ability));
		OwnQuestPanel.Children.Clear();
		OwnQuestPanel.Children.Add(CreateCardButton(request.ownField.quest));
		Avalonia.Thickness ownBorderThickness = new Avalonia.Thickness(2, 0, 2, 2);
		for(int i = 0; i < GameConstants.FIELD_SIZE; i++)
		{
			CardStruct? c = request.ownField.field[i];
			if(c != null)
			{
				Button b = CreateCardButton(c);
				if(request.markedZone != null && i == request.markedZone)
				{
					b.BorderBrush = Brushes.Yellow;
					b.BorderThickness = ownBorderThickness;
				}
				OwnField.Children[i] = b;
			}
			else
			{
				Button b = new Button
				{
					Width = (OppField.Bounds.Width - 10) / GameConstants.FIELD_SIZE,
					Height = OppField.Bounds.Height - 10,
				};
				if(request.markedZone != null && i == request.markedZone)
				{
					b.BorderBrush = Brushes.Yellow;
					b.BorderThickness = ownBorderThickness;
				}
				OwnField.Children[i] = b;
			}
		}
		OwnHandPanel.Children.Clear();
		for(int i = 0; i < request.ownField.hand.Length; i++)
		{
			OwnHandPanel.Children.Add(CreateCardButton(request.ownField.hand[i]));
		}
		OwnShowPanel.Children.Clear();
		if(request.ownField.shownCard != null)
		{
			OwnShowPanel.Children.Add(CreateCardButton(request.ownField.shownCard));
		}
	}

	private Button CreateCardButton(CardStruct card)
	{
		Button b = new Button
		{
			DataContext = card,
		};
		b.MinWidth = OwnField.Bounds.Width / GameConstants.FIELD_SIZE;
		if(card.location == GameConstants.Location.Field)
		{
			b.Width = (OwnField.Bounds.Width - 10) / GameConstants.FIELD_SIZE;
		}
		b.Height = OwnField.Bounds.Height - 10;
		b.PointerEnter += (sender, args) =>
		{
			if(sender == null) return;
			if(args.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
			ShowCard(card);
		};
		if(card.controller == playerIndex)
		{
			b.Click += (sender, args) =>
			{
				args.Handled = true;
				OptionsRequest((Button)sender!, card.location, card.uid);
			};
		}
		if(card.card_type != GameConstants.CardType.UNKNOWN)
		{
			if(card.card_type == GameConstants.CardType.Creature)
			{
				b.Background = Brushes.Orange;
			}
			else if(card.card_type == GameConstants.CardType.Spell)
			{
				b.Background = Brushes.Blue;
			}
			else if(card.card_type == GameConstants.CardType.Quest)
			{
				b.Background = Brushes.Green;
			}
			StackPanel contentPanel = new StackPanel();
			contentPanel.Children.Add(new TextBlock { Text = card.name });
			b.Content = contentPanel;
		}
		return b;
	}

	private void OptionsRequest(Button button, GameConstants.Location location, int uid)
	{
		List<byte> payload = GeneratePayload<DuelPackets.GetOptionsRequest>(new DuelPackets.GetOptionsRequest
		{
			location = location,
			uid = uid
		});
		stream.Write(payload.ToArray(), 0, payload.Count);
	}

	public void ShowCard(CardStruct c)
	{
		CardImagePanel.Children.Clear();
		Viewbox v = UIUtils.CreateGenericCard(c);
		CardImagePanel.Children.Add(v);
		CardTextBlock.Text = c.Format();
	}

	private void Cleanup()
	{
		if(closing)
		{
			return;
		}
		closing = true;
		Monitor.Enter(stream);
		List<byte> payload = GeneratePayload<DuelPackets.SurrenderRequest>(new DuelPackets.SurrenderRequest { });
		if(client.Connected)
		{
			stream.Write(payload.ToArray(), 0, payload.Count);
		}
		Monitor.Exit(stream);
		networkingTask.Dispose();
		client.Close();
	}
}