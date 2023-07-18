using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform.Storage;
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

	public static async Task<string?> SelectFileAsync(Window window, string title = "Select file", bool allowMultiple = false)
	{
		TopLevel? topLevel = TopLevel.GetTopLevel(window);
		if(topLevel == null) return null;
		IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
		{
			Title = title,
			AllowMultiple = allowMultiple,
		});
		if(files.Count > 0)
		{
			return files[0].Path.AbsolutePath;
		}
		return null;
	}
	public static async Task<string?> SelectAndReadFileAsync(Window window, string title = "Select file", bool allowMultiple = false)
	{
		TopLevel? topLevel = TopLevel.GetTopLevel(window);
		if(topLevel == null) return null;
		IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
		{
			Title = title,
			AllowMultiple = allowMultiple,
		});
		if(files.Count > 0)
		{
			await using Stream stream = await files[0].OpenReadAsync();
			using StreamReader reader = new StreamReader(stream);
			return await reader.ReadToEndAsync();
		}
		return null;
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
		int[] uids = new int[box.SelectedItems?.Count ?? 0];
		for(int i = 0; i < (box.SelectedItems?.Count ?? 0); i++)
		{
			uids[i] = ((CardStruct)box.SelectedItems?[i]!).uid;
		}
		return uids;
	}

	public static void CardHover(Panel CardImagePanel, TextBlock CardTextBlock, CardStruct c, bool inDeckEdit)
	{
		CardImagePanel.Children.Clear();
		Viewbox v = UIUtils.CreateGenericCard(c);
		CardImagePanel.Children.Add(v);

		CardTextBlock.Text = c.Format();
		CardTextBlock.PointerMoved += CardTextHover;
	}
	private static void CardTextHover(object? sender, PointerEventArgs e)
	{
		if(sender == null) return;
		TextBlock block = (TextBlock)sender;
		string? current = ToolTip.GetTip(block)?.ToString();
		block.ClearValue(ToolTip.TipProperty);
		if(block.Text == null) return;
		TextLayout layout = block.TextLayout;
		Point pointerPoint = e.GetPosition(block);
		TextHitTestResult hitTestResult = layout.HitTestPoint(pointerPoint);
		int position = hitTestResult.TextPosition;
		if(position < 0 || position >= block.Text.Length) return;
		int start = block.Text.LastIndexOf('[', position);
		int end = block.Text.IndexOf(']', position);
		if(start < 0 || end < 0 || end >= block.Text.Length || start == end) return;
		string possibleKeyword = block.Text.Substring(start + 1, end - start - 1);
		if(possibleKeyword.Contains(' ')) return;
		if(ClientConstants.KeywordDescriptions.ContainsKey(possibleKeyword))
		{
			string description = ClientConstants.KeywordDescriptions[possibleKeyword];
			ToolTip.SetTip(block, description);
			ToolTip.SetIsOpen(block, true);
		}
	}
}
