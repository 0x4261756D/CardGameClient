using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using CardGameUtils;
using CardGameUtils.Structs;
using static CardGameUtils.Structs.NetworkingStructs;

namespace CardGameClient;

public class UIUtils
{
	public static (byte, byte[]?)? TryRequest(PacketContent request, string address, int port, Window? window)
	{
		try
		{
			return Functions.Request(request, address, port);
		}
		catch(Exception ex)
		{
			if(window != null && window.IsVisible)
			{
				new ErrorPopup(ex.Message).ShowDialog(window);
			}
			else
			{
				new ErrorPopup(ex.Message).Show();
			}
			return null;
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
	public static int[] CardListBoxSelectionToUID(ListBox box)
	{
		int[] uids = new int[box.SelectedItems.Count];
		for(int i = 0; i < box.SelectedItems.Count; i++)
		{
			uids[i] = ((CardStruct)((TextBlock)box.SelectedItems[i]!).DataContext!).uid;
		}
		return uids;
	}
}