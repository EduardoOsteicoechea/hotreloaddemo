using System.Windows.Controls;

namespace hotreloaddemo;

public partial class MainPage : Page
{
	public MainPage()
	{
		InitializeComponent();
		GlobalVariables.HotswapContainer = this.DynamicUiContainer;
	}
}
