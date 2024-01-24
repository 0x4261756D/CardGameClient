using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Sockets;
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
	private readonly int playerIndex;
	private readonly TcpClient client;
	private readonly Stream stream;
	private readonly Task networkingTask;
	private readonly Flyout optionsFlyout = new();
	readonly Queue<DuelPackets.FieldUpdateRequest> fieldUpdateQueue = new();
	private Task? fieldUpdateTask;
	private bool closing;
	private bool shouldEnablePassButtonAfterUpdate;
	private Window? windowToShowAfterUpdate;
	private readonly ObservableCollection<TextBlock> activities = [];

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
		Closed += (sender, args) =>
		{
			Cleanup();
		};
		OppField.LayoutUpdated += FieldInitialized;
		OwnField.LayoutUpdated += FieldInitialized;
	}

	private void FieldInitialized(object? sender, EventArgs e)
	{
		if(sender == null)
		{
			return;
		}
		Panel panel = (Panel)sender;
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
		Close();
	}
	private void TrySend(byte[] packet)
	{
		if(client.Connected)
		{
			stream.Write(packet);
		}
		else
		{
			new ErrorPopup("Stream was closed").Show(this);
		}
	}
	private void PassClick(object? sender, RoutedEventArgs args)
	{
		TrySend(GeneratePayload(new DuelPackets.PassRequest { }));
	}
	private async void HandleNetwork()
	{
		Log("Socketthread started");
		bool hasPassed = false;
		while(!closing)
		{
			if(client.Connected)
			{
				(byte, byte[]?)? payload = await Task.Run(() => TryReceiveRawPacket((NetworkStream)stream, 100)).ConfigureAwait(false);
				if(payload != null && await Dispatcher.UIThread.InvokeAsync(() => HandlePacket(payload.Value)))
				{
					return;
				}
				if(fieldUpdateQueue.Count > 0)
				{
					hasPassed = false;
					if(fieldUpdateTask == null || fieldUpdateTask.IsCompleted)
					{
						await Dispatcher.UIThread.InvokeAsync(() =>
						{
							optionsFlyout.Hide();
							PassButton.IsEnabled = false;
						});
						fieldUpdateTask = Task.Delay(Program.config.animation_delay_in_ms).ContinueWith((_) => Dispatcher.UIThread.InvokeAsync(UpdateField));
					}
				}
				else
				{
					if(shouldEnablePassButtonAfterUpdate)
					{
						await Dispatcher.UIThread.InvokeAsync(() =>
						{
							if(!hasPassed && (KeepPassingBox.IsChecked ?? false))
							{
								if(stream.CanWrite)
								{
									PassClick(null, new RoutedEventArgs());
									hasPassed = true;
								}
							}
							else
							{
								PassButton.IsEnabled = true;
							}
						});
					}
					if(windowToShowAfterUpdate != null)
					{
						await Dispatcher.UIThread.InvokeAsync(() =>
						{
							windowToShowAfterUpdate.Show();
							windowToShowAfterUpdate = null;
						});
					}
				}
			}
		}
	}

	private bool HandlePacket((byte type, byte[]? bytes) packet)
	{
		if(packet.type >= (byte)NetworkingConstants.PacketType.PACKET_COUNT)
		{
			Log($"Unrecognized packet type ({packet.type})");
			throw new Exception($"Unrecognized packet type ({packet.type})");
		}
		NetworkingConstants.PacketType type = (NetworkingConstants.PacketType)packet.type;
		switch(type)
		{
			case NetworkingConstants.PacketType.DuelFieldUpdateRequest:
			{
				EnqueueFieldUpdate(DeserializeJson<DuelPackets.FieldUpdateRequest>(packet.bytes!));
			}
			break;
			case NetworkingConstants.PacketType.DuelYesNoRequest:
			{
				Log("Received a yesno requets", severity: LogSeverity.Error);
				windowToShowAfterUpdate = new YesNoWindow(DeserializeJson<DuelPackets.YesNoRequest>(packet.bytes!).question, stream);
			}
			break;
			case NetworkingConstants.PacketType.DuelCustomSelectCardsRequest:
			{
				DuelPackets.CustomSelectCardsRequest request = DeserializeJson<DuelPackets.CustomSelectCardsRequest>(packet.bytes!);
				windowToShowAfterUpdate = new CustomSelectCardsWindow(request.desc!, request.cards, request.initialState, stream, playerIndex, ShowCard);
			}
			break;
			case NetworkingConstants.PacketType.DuelGetOptionsResponse:
			{
				UpdateCardOptions(DeserializeJson<DuelPackets.GetOptionsResponse>(packet.bytes!));
			}
			break;
			case NetworkingConstants.PacketType.DuelSelectZoneRequest:
			{
				windowToShowAfterUpdate = new SelectZoneWindow(DeserializeJson<DuelPackets.SelectZoneRequest>(packet.bytes!).options, stream);
			}
			break;
			case NetworkingConstants.PacketType.DuelGameResultResponse:
			{
				windowToShowAfterUpdate = new GameResultWindow(this, DeserializeJson<DuelPackets.GameResultResponse>(packet.bytes!));
			}
			break;
			case NetworkingConstants.PacketType.DuelSelectCardsRequest:
			{
				DuelPackets.SelectCardsRequest request = DeserializeJson<DuelPackets.SelectCardsRequest>(packet.bytes!);
				windowToShowAfterUpdate = new SelectCardsWindow(request.desc!, request.amount, request.cards, stream, playerIndex, ShowCard);
			}
			break;
			case NetworkingConstants.PacketType.DuelViewCardsResponse:
			{
				DuelPackets.ViewCardsResponse request = DeserializeJson<DuelPackets.ViewCardsResponse>(packet.bytes!);
				windowToShowAfterUpdate = new ViewCardsWindow(cards: request.cards, message: request.message, showCardAction: ShowCard);
			}
			break;
			default:
				Log($"Unimplemented: {type}");
				throw new NotImplementedException($"{type}");
		}
		return false;
	}

	private void ShowCard(CardStruct c)
	{
		UIUtils.CardHover(CardImagePanel, CardTextBlock, c, false);
	}

	private void UpdateCardOptions(DuelPackets.GetOptionsResponse response)
	{
		if(response.location == GameConstants.Location.Hand)
		{
			foreach(Control b in OwnHandPanel.Children)
			{
				if(((CardStruct)b.DataContext!).uid == response.uid)
				{
					if(response.options.Length == 0)
					{
						return;
					}
					StackPanel p = new();
					foreach(CardAction action in response.options)
					{
						Button option = new()
						{
							Content = new TextBlock
							{
								Text = action.description
							}
						};
						option.Click += (_, _) => SendCardOption(action, response.uid, response.location);
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
			foreach(Control b in OwnField.Children)
			{
				if(b.DataContext == null || b.DataContext == DataContext)
				{
					continue;
				}
				if(((CardStruct)b.DataContext).uid == response.uid)
				{
					StackPanel p = new();
					foreach(CardAction action in response.options)
					{
						Button option = new()
						{
							Content = new TextBlock
							{
								Text = action.description
							}
						};
						option.Click += (_, _) => SendCardOption(action, response.uid, response.location);
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
			StackPanel p = new();
			foreach(CardAction action in response.options)
			{
				Button option = new()
				{
					Content = new TextBlock
					{
						Text = action.description
					}
				};
				option.Click += (_, _) => SendCardOption(action, response.uid, response.location);
				p.Children.Add(option);
			}
			optionsFlyout.Content = p;
			optionsFlyout.ShowAt(OwnQuestPanel, true);
		}
		else if(response.location == GameConstants.Location.Ability)
		{
			StackPanel p = new();
			foreach(CardAction action in response.options)
			{
				Button option = new()
				{
					Content = new TextBlock
					{
						Text = action.description
					}
				};
				option.Click += (_, _) => SendCardOption(action, response.uid, response.location);
				p.Children.Add(option);
			}
			optionsFlyout.Content = p;
			optionsFlyout.ShowAt(OwnAbilityPanel, true);
		}
		else
		{
			throw new NotImplementedException($"Updating card options at {Enum.GetName(response.location)}");
		}
	}

	public void OppGraveClick(object? sender, RoutedEventArgs args)
	{
		TrySend(GeneratePayload(new DuelPackets.ViewGraveRequest(opponent: true)));
	}
	public void OwnGraveClick(object? sender, RoutedEventArgs args)
	{
		TrySend(GeneratePayload(new DuelPackets.ViewGraveRequest(opponent: false)));
	}
	private void SendCardOption(CardAction action, int uid, GameConstants.Location location)
	{
		TrySend(GeneratePayload(new DuelPackets.SelectOptionRequest
		(
			location: location,
			uid: uid,
			cardAction: action
		)));
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
		string turnText = $"Turn {request.turn}";
		if(TurnBlock.Text != turnText)
		{
			KeepPassingBox.IsChecked = false;
		}
		TurnBlock.Text = turnText;
		InitBlock.Text = request.hasInitiative ? "You have initiative" : "Your opponent has initiative";
		DirectionBlock.Text = "Battle direction: " + (request.battleDirectionLeftToRight ? "->" : "<-");
		if(request.hasInitiative)
		{
			Background = Brushes.Purple;
		}
		else
		{
			ClearValue(BackgroundProperty);
		}
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
		Avalonia.Thickness oppBorderThickness = new(2, 2, 2, 0);
		PhaseBlock.Text = (request.markedZone != null) ? "Battle Phase" : "Main Phase";
		if(request.ownField.shownInfo.card != null && request.ownField.shownInfo.description != null)
		{
			TextBlock text = new() { Text = $"You: {request.ownField.shownInfo.card.name}: {request.ownField.shownInfo.description}" };
			text.PointerEntered += (sender, args) =>
			{
				if(sender == null)
				{
					return;
				}
				if(args.KeyModifiers.HasFlag(KeyModifiers.Control))
				{
					return;
				}
				if(request.ownField.shownInfo.card != null)
				{
					UIUtils.CardHover(CardImagePanel, CardTextBlock, request.ownField.shownInfo.card, false);
				}
			};
			activities.Insert(0, text);
		}
		if(request.oppField.shownInfo.card != null && request.oppField.shownInfo.description != null)
		{
			TextBlock text = new() { Text = $"Opp: {request.oppField.shownInfo.card.name}: {request.oppField.shownInfo.description}" };
			text.PointerEntered += (sender, args) =>
			{
				if(sender == null)
				{
					return;
				}
				if(args.KeyModifiers.HasFlag(KeyModifiers.Control))
				{
					return;
				}
				if(request.oppField.shownInfo.card != null)
				{
					UIUtils.CardHover(CardImagePanel, CardTextBlock, request.oppField.shownInfo.card, false);
				}
			};
			activities.Insert(0, text);
		}
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
				Button b = new()
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
		if(request.oppField.shownInfo.card != null)
		{
			OppShowPanel.Children.Add(CreateCardButton(request.oppField.shownInfo.card));
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
		Avalonia.Thickness ownBorderThickness = new(2, 0, 2, 2);
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
				Button b = new()
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
		if(request.ownField.shownInfo.card != null)
		{
			OwnShowPanel.Children.Add(CreateCardButton(request.ownField.shownInfo.card));
		}
		ActivityLogList.ItemsSource = activities;
	}

	private Button CreateCardButton(CardStruct card)
	{
		Button b = new()
		{
			DataContext = card,
			Background = (card.card_type == GameConstants.CardType.Quest && card.text.Contains("REWARD CLAIMED")) ? Brushes.Green : null,
		};
		if(!card.location.HasFlag(GameConstants.Location.Hand))
		{
			b.MinWidth = OwnField.Bounds.Width / GameConstants.FIELD_SIZE;
		}
		if(card.location == GameConstants.Location.Field)
		{
			b.Width = (OwnField.Bounds.Width - 10) / GameConstants.FIELD_SIZE;
		}
		b.Height = OwnField.Bounds.Height - 10;
		b.PointerEntered += (sender, args) =>
		{
			if(sender == null)
			{
				return;
			}
			if(args.KeyModifiers.HasFlag(KeyModifiers.Control))
			{
				return;
			}
			UIUtils.CardHover(CardImagePanel, CardTextBlock, card, false);
		};
		if(card.controller == playerIndex)
		{
			b.Click += (sender, args) =>
			{
				args.Handled = true;
				OptionsRequest(card.location, card.uid);
			};
		}
		if(card.card_type == GameConstants.CardType.UNKNOWN)
		{
			b.Background = Brushes.DimGray;
		}
		else
		{
			b.Content = UIUtils.CreateGenericCard(card);
		}
		return b;
	}

	private void OptionsRequest(GameConstants.Location location, int uid)
	{
		TrySend(GeneratePayload(new DuelPackets.GetOptionsRequest(location: location, uid: uid)));
	}

	private void Cleanup()
	{
		if(closing)
		{
			return;
		}
		closing = true;
		Monitor.Enter(stream);
		if(client.Connected)
		{
			try
			{
				stream.Write(GeneratePayload(new DuelPackets.SurrenderRequest { }));
			}
			catch(Exception e)
			{
				Log($"Exception while sending cleanup message: {e}", severity: LogSeverity.Warning);
			}
		}
		Monitor.Exit(stream);
		networkingTask.Dispose();
		client.Close();
	}
}
