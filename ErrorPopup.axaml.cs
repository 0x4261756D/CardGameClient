using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CardGameClient;

public partial class ErrorPopup : Window
{
	public ErrorPopup(string msg)
	{
		DataContext = new ErrorPopupViewModel(msg);
		InitializeComponent();
		Width = Program.config.width / 2;
		Height = Program.config.height / 2;
		Topmost = true;
	}

	private void CloseClick(object? sender, RoutedEventArgs args)
	{
		Close();
	}
}
public class ErrorPopupViewModel(string msg) : INotifyPropertyChanged
{
	private string message = msg;
	public string Message
	{
		get => message;
		set
		{
			if(value != message)
			{
				message = value;
				NotifyPropertyChanged();
			}
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;
	private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
