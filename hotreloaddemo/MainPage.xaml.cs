using Autodesk.Revit.UI;
using System.Windows.Controls;

namespace hotreloaddemo;

public partial class MainPage : Page, IDockablePaneProvider
{
	public MainPage()
	{
		InitializeComponent();

		ApplicationManagerService.Instance().HotswapContainer = this.DynamicUiContainer;
	}

	public void SetupDockablePane(DockablePaneProviderData data)
	{
		data.VisibleByDefault = true;

		data.InitialState = new DockablePaneState()
		{
			DockPosition = DockPosition.Tabbed
		};

		data.FrameworkElement = this;
	}
}
