using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using CardGameUtils;
using CardGameUtils.Structs;
using static CardGameUtils.Structs.NetworkingStructs;

namespace CardGameClient;

public class UIUtils
{
	public static ThemeVariant ConvertThemeVariant(ClientConfig.ThemeVariant? theme)
	{
		return theme switch
		{
			ClientConfig.ThemeVariant.Default => ThemeVariant.Default,
			ClientConfig.ThemeVariant.Dark => ThemeVariant.Dark,
			ClientConfig.ThemeVariant.Light => ThemeVariant.Light,
			_ => ThemeVariant.Default,
		};
	}

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
				new ErrorPopup(ex.Message).Show(window);
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
		if(topLevel == null)
		{
			return null;
		}
		IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
		{
			Title = title,
			AllowMultiple = allowMultiple,
		}).ConfigureAwait(true);
		if(files.Count > 0)
		{
			return files[0].Path.AbsolutePath;
		}
		return null;
	}
	public static async Task<string?> SelectAndReadFileAsync(Window window, string title = "Select file", bool allowMultiple = false)
	{
		TopLevel? topLevel = TopLevel.GetTopLevel(window);
		if(topLevel == null)
		{
			return null;
		}
		IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
		{
			Title = title,
			AllowMultiple = allowMultiple,
		}).ConfigureAwait(true);
		if(files.Count > 0)
		{
#pragma warning disable CA2007
			await using Stream stream = await files[0].OpenReadAsync();
#pragma warning restore CA2007
			using StreamReader reader = new(stream);
			return await reader.ReadToEndAsync().ConfigureAwait(true);
		}
		return null;
	}

	private static readonly Dictionary<string, Bitmap> ArtworkCache = [];
	private static Bitmap? DefaultArtwork;
	public static Bitmap? FetchArtwork(string name)
	{
		if(ArtworkCache.TryGetValue(name, out Bitmap? bitmap))
		{
			return bitmap;
		}
		if(Program.config.picture_path == null)
		{
			return null;
		}
		string pathNoExtension = Path.Combine(Program.config.picture_path, name);
		if(File.Exists(pathNoExtension + ".png"))
		{
			Bitmap ret = new(pathNoExtension + ".png");
			ArtworkCache[name] = ret;
			return ret;
		}
		if(File.Exists(pathNoExtension + ".jpg"))
		{
			Bitmap ret = new(pathNoExtension + ".jpg");
			ArtworkCache[name] = ret;
			return ret;
		}
		if(DefaultArtwork == null && File.Exists(Path.Combine(Program.config.picture_path, "default_artwork.png")))
		{
			DefaultArtwork = new Bitmap(Path.Combine(Program.config.picture_path, "default_artwork.png"));
		}
		return DefaultArtwork;
	}
	public static Viewbox CreateGenericCard(CardStruct c)
	{
		Viewbox box = new()
		{
			Stretch = Stretch.Uniform,
		};
		RelativePanel insidePanel = new()
		{
			Width = 1000,
			Height = 1500,
		};
		Border outsideBorder = new()
		{
			Child = insidePanel,
			BorderBrush = Brushes.Black,
			BorderThickness = new Thickness(5),
		};
		Border headerBorder = new()
		{
			Child = new TextBlock
			{
				Text = c.name,
				FontSize = 50,
				TextAlignment = TextAlignment.Center,
			},
			Margin = new Thickness(30),
			Padding = new Thickness(10),
			Background = Brushes.Gray,
			BorderBrush = Brushes.Black,
			BorderThickness = new Thickness(3),
			CornerRadius = new CornerRadius(10),
		};
		insidePanel.Children.Add(headerBorder);
		RelativePanel.SetAlignLeftWithPanel(headerBorder, true);
		RelativePanel.SetAlignRightWithPanel(headerBorder, true);
		Border imageBorder = new()
		{
			Child = new Viewbox
			{
				Child = new Image
				{
					Source = FetchArtwork(c.name)
				},
			},
			Margin = new Thickness(50, 0),
			BorderBrush = Brushes.Black,
			BorderThickness = new Thickness(3),
		};
		insidePanel.Children.Add(imageBorder);
		RelativePanel.SetBelow(imageBorder, headerBorder);
		Border textBorder = new()
		{
			Child = new TextBlock
			{
				Text = c.text,
				TextWrapping = TextWrapping.Wrap,
				FontSize = 40,
				Foreground = Brushes.White,
				Margin = new Thickness(0, 0, 0, 100)
			},
			Margin = new Thickness(40),
			BorderBrush = Brushes.Black,
			BorderThickness = new Thickness(3),
			CornerRadius = new CornerRadius(30),
			Padding = new Thickness(20),
			Background = Brush.Parse("#515151")
		};
		insidePanel.Children.Add(textBorder);
		RelativePanel.SetBelow(textBorder, imageBorder);
		RelativePanel.SetAlignLeftWithPanel(textBorder, true);
		RelativePanel.SetAlignRightWithPanel(textBorder, true);
		switch(c.card_type)
		{
			case GameConstants.CardType.Creature:
			{
				outsideBorder.Background = Brushes.Orange;
				Border costBorder = new()
				{
					Child = new TextBlock
					{
						Text = $"Cost: {c.cost} Power/Life: {c.power}/{c.life}",
						FontSize = 50,
						TextAlignment = TextAlignment.Center,
						Margin = new Thickness(20),
					},
					BorderBrush = Brushes.Black,
					BorderThickness = new Thickness(3),
					Margin = new Thickness(30),
					Background = Brushes.Gray,
				};
				insidePanel.Children.Add(costBorder);
				RelativePanel.SetAlignBottomWith(costBorder, textBorder);
			}
			break;
			case GameConstants.CardType.Spell:
			{
				outsideBorder.Background = Brushes.SkyBlue;
				Border costBorder = new()
				{
					Child = new TextBlock
					{
						Text = $"Cost: {c.cost}",
						FontSize = 50,
						TextAlignment = TextAlignment.Center,
						Margin = new Thickness(20),
					},
					BorderBrush = Brushes.Black,
					BorderThickness = new Thickness(3),
					Margin = new Thickness(30),
					Background = Brushes.Gray,
				};
				insidePanel.Children.Add(costBorder);
				RelativePanel.SetAlignBottomWith(costBorder, textBorder);
			}
			break;
			case GameConstants.CardType.Quest:
			{
				outsideBorder.Background = Brushes.Green;
				Border goalBorder = new()
				{
					Child = new TextBlock
					{
						Text = $"{c.position}/{c.cost}",
						FontSize = 50,
						TextAlignment = TextAlignment.Center,
						Margin = new Thickness(20),
					},
					BorderBrush = Brushes.Black,
					BorderThickness = new Thickness(3),
					Margin = new Thickness(30),
					Background = Brushes.Gray,
				};
				insidePanel.Children.Add(goalBorder);
				RelativePanel.SetAlignBottomWith(goalBorder, textBorder);
			}
			break;
		}
		RelativePanel.SetAlignBottomWithPanel(textBorder, true);
		box.Child = outsideBorder;
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
		Viewbox v = CreateGenericCard(c);
		CardImagePanel.Children.Add(v);

		CardTextBlock.Text = c.Format(inDeckEdit);
		CardTextBlock.PointerMoved += CardTextHover;
	}
	private static void CardTextHover(object? sender, PointerEventArgs e)
	{
		if(sender is null)
		{
			return;
		}
		TextBlock block = (TextBlock)sender;
		if(block.Text is not null)
		{
			TextLayout layout = block.TextLayout;
			Point pointerPoint = e.GetPosition(block);
			TextHitTestResult hitTestResult = layout.HitTestPoint(pointerPoint);
			int position = hitTestResult.TextPosition;
			if(position >= 0 && position < block.Text.Length)
			{
				int start = block.Text.LastIndexOf('[', position);
				int end = block.Text.IndexOf(']', position);
				if(start >= 0 && end >= 0 && end < block.Text.Length && start != end)
				{
					string possibleKeyword = block.Text.Substring(start + 1, end - start - 1);
					if(!possibleKeyword.Contains(' ') && ClientConstants.KeywordDescriptions.TryGetValue(possibleKeyword, out string? value))
					{
						string description = value;
						ToolTip.SetTip(block, description);
						ToolTip.SetIsOpen(block, true);
						ToolTip.SetShowDelay(block, 0);
						return;
					}
				}
			}
		}
		_ = block.SetValue(ToolTip.IsOpenProperty, false);
	}

}
