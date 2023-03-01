using System;
using System.Collections.Generic;
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
	// DONT USE THIS
	// This only exists because Avalonia requires it
	private string playerName;
	private int playerIndex;
	private TcpClient client;
	private NetworkStream stream;
	private Task networkingTask;
	private Flyout optionsFlyout = new Flyout();
	private bool closing = false; public DuelWindow()
	{
		InitializeComponent();
		playerName = "THIS SHOULD NOT HAPPEN";
		client = new TcpClient();
		stream = client.GetStream();
		networkingTask = new Task(() => { });
		OppField.LayoutUpdated += FieldInitialized;
		OwnField.LayoutUpdated += FieldInitialized;
	}
	public DuelWindow(string name, int playerIndex, TcpClient client)
	{
		this.playerName = name;
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
				List<byte>? bytes = ReceiveRawPacket(stream, 1000);
				Monitor.Exit(stream);
				if(bytes != null && bytes.Count != 0)
				{
					Log("Client received a request of length " + bytes.Count);
					if(await Dispatcher.UIThread.InvokeAsync(() => HandlePacket(bytes)))
					{
						return;
					}
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
		bytes.RemoveAt(0);
		string payload = Encoding.UTF8.GetString(bytes.ToArray());
		Functions.Log(payload);
		switch(type)
		{
			case NetworkingConstants.PacketType.DuelFieldUpdateRequest:
				{
					UpdateField(DeserializeJson<DuelPackets.FieldUpdateRequest>(payload));
				}
				break;
			case NetworkingConstants.PacketType.DuelYesNoRequest:
				{
					new YesNoWindow(DeserializeJson<DuelPackets.YesNoRequest>(payload).question, stream).Show();
				}
				break;
			case NetworkingConstants.PacketType.DuelCustomSelectCardsRequest:
				{
					DuelPackets.CustomSelectCardsRequest request = DeserializeJson<DuelPackets.CustomSelectCardsRequest>(payload);
					new CustomSelectCardsWindow(request.desc!, request.cards, request.initialState, stream, playerIndex, x => ShowCard(x)).Show();
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
					new SelectCardsWindow(request.desc!, request.amount, request.cards, stream, playerIndex, x => ShowCard(x)).Show();
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

	private void UpdateField(DuelPackets.FieldUpdateRequest request)
	{
		TurnBlock.Text = $"Turn {request.turn}";
		InitBlock.Text = request.hasInitiative ? "You have initiative" : "Your opponent has initiative";
		Background = request.hasInitiative ? Brushes.Green : Brushes.Black;

		OppNameBlock.Text = request.oppField.name;
		OppLifeBlock.Text = $"Life: {request.oppField.life}";
		OppMomentumBlock.Text = $"Momentum: {request.oppField.momentum}";
		OppDeckButton.Content = request.oppField.deckSize;
		OppGraveButton.Content = request.oppField.graveSize;
		OppAbilityPanel.Children.Clear();
		OppAbilityPanel.Children.Add(CreateCardButton(request.oppField.ability));
		OppQuestPanel.Children.Clear();
		OppQuestPanel.Children.Add(CreateCardButton(request.oppField.quest));
		for(int i = 0; i < GameConstants.FIELD_SIZE; i++)
		{
			CardStruct? c = request.oppField.field[GameConstants.FIELD_SIZE - i - 1];
			if(c != null)
			{
				OppField.Children[i] = CreateCardButton(c);
			}
			else
			{
				OppField.Children[i] = new Button
				{
					Width = (OppField.Bounds.Width - 10) / GameConstants.FIELD_SIZE,
					Height = OppField.Bounds.Height - 10,
				};
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
		for(int i = 0; i < GameConstants.FIELD_SIZE; i++)
		{
			CardStruct? c = request.ownField.field[i];
			if(c != null)
			{
				OwnField.Children[i] = CreateCardButton(c);
			}
			else
			{
				OwnField.Children[i] = new Button
				{
					Width = (OppField.Bounds.Width - 10) / GameConstants.FIELD_SIZE,
					Height = OppField.Bounds.Height - 10,
				};

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
			DataContext = card
		};
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