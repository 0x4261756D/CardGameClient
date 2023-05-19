using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CardGameClient;

public partial class ErrorPopup : Window
{
	// DONT USE THIS
	// This only exists because Avalonia requires it
	public ErrorPopup()
	{
		InitializeComponent();
	}

	public ErrorPopup(string msg)
	{
		DataContext = new ErrorPopupViewModel(msg);
		InitializeComponent();
		this.Width = Program.config.width / 2;
		this.Height = Program.config.height / 2;
		this.Topmost = true;
	}

	private void CloseClick(object? sender, RoutedEventArgs args)
	{
		this.Close();
	}
}
public class ErrorPopupViewModel : INotifyPropertyChanged
{
	public ErrorPopupViewModel(string msg)
	{
		message = msg;
	}

	private string message;
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
