using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using CardGameUtils;
using CardGameUtils.Structs;
using static CardGameUtils.Structs.NetworkingStructs;

namespace CardGameClient;

public class UIUtils
{
	public static bool TryRequest(PacketContent request, out List<byte>? payload, string address, int port, Window? window)
	{
		try
		{
			payload = Functions.Request(request, address, port);
			return true;
		}
		catch (System.Exception ex)
		{
			if (window != null && window.IsVisible)
			{
				new ErrorPopup(ex.Message).ShowDialog(window);
			}
			else
			{
				new ErrorPopup(ex.Message).Show();
			}
			payload = null;
			return false;
		}
	}

	public static Viewbox CreateGenericCard(CardStruct c)
	{
		Viewbox box = new Viewbox();
		box.Stretch = Stretch.Uniform;
		box.VerticalAlignment = VerticalAlignment.Stretch;
		box.HorizontalAlignment = HorizontalAlignment.Stretch;
		Panel p = new Panel();
		TextBlock block = new TextBlock
		{
			Text = c.name,
			FontSize = 18,
		};
		if(c.card_type == GameConstants.CardType.Creature)
		{
			p.Background = Brushes.Orange;
			block.Foreground = Brushes.Black;
		}
		else if(c.card_type == GameConstants.CardType.Spell)
		{
			p.Background = Brushes.Blue;
			if(c.can_be_class_ability)
			{
				block.FontStyle = FontStyle.Oblique;
			}
		}
		else if(c.card_type == GameConstants.CardType.Quest)
		{
			p.Background = Brushes.Green;
		}
		p.Children.Add(block);
		box.Child = p;
		box.DataContext = c;
		return box;
	}
}