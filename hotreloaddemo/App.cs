using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace hotreloaddemo;

//public class MainDockablePaneCreator : IFrameworkElementCreator
//{
//	public FrameworkElement CreateFrameworkElement()
//	{
//		return new MainPage();
//	}
//}

//public class MainDockablePaneProvider : Autodesk.Revit.UI.IDockablePaneProvider
//{
//	//public MainDockablePaneProvider()
//	//{
//	//	SetupDockablePane(new DockablePaneProviderData());
//	//}

//	public void SetupDockablePane(DockablePaneProviderData data)
//	{
//		data.VisibleByDefault = true;

//		data.InitialState = new DockablePaneState()
//		{
//			DockPosition = DockPosition.Tabbed
//		};

//		data.FrameworkElementCreator = new MainDockablePaneCreator();
//	}
//}

public class App : IExternalApplication
{
	public Result OnStartup(UIControlledApplication application)
	{
		string configDirectoryPath = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
			MethodBase.GetCurrentMethod().DeclaringType.Namespace
		);

		string configFilePath = Path.Combine(
			configDirectoryPath,
			"ApplicationConfiguration.json"
		);

		ApplicationManager manager = ApplicationManagerService.Instance();

		manager.LoadApplicationConfiguration(configDirectoryPath, configFilePath);

		manager.StoreAssemblyLocation();

		//manager.GenerateMainDockablePane(application, new MainDockablePaneProvider());
		manager.GenerateMainDockablePane(application, new MainPage());

		manager.CreateRibbonTab(application);

		manager.GenerateApplicationRibbonPanel(application);

		manager.GenerateApplicationRibbonTabButtons();

		return Result.Succeeded;
	}

	public Result OnShutdown(UIControlledApplication application)
	{
		return Autodesk.Revit.UI.Result.Succeeded;
	}
}




[Transaction(TransactionMode.Manual)]
public class LaunchMainDockablePane : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		var applicationManager = ApplicationManagerService.Instance();

		applicationManager.ReloadApplication(commandData);

		return Result.Succeeded;
	}
}



public class ApplicationResourcesModel
{
	public List<ApplicationButtonModel> ApplicationButtons { get; set; } = new List<ApplicationButtonModel>();
}

public class ApplicationCommandModel
{
	public string? HostDllName { get; set; }
	public string? Namespace { get; set; }
	public string? CommandName { get; set; }
}

public class ApplicationButtonModel
{
	public ApplicationCommandModel? Command { get; set; }
	public string? Namespace { get; set; }
	public string? ClassName { get; set; }
	public string? TooltipText { get; set; }
}

public static class ApplicationManagerService
{
	private static ApplicationManager _instance { get; set; }
	public static ApplicationManager Instance(string applicationName = "", string mainCommandName = "Launch") 
	{
		if (string.IsNullOrEmpty(applicationName)) 
		{
			applicationName = MethodBase.GetCurrentMethod().DeclaringType.Namespace;
		}

		if (_instance is null) 
		{
			_instance = new ApplicationManager(applicationName, mainCommandName);
		}

		return _instance;
	}
}

public class ApplicationManager
{
	public string _mainNamespace { get; set; }
	public string MainNamespace { get { return _mainNamespace; } }
	private string _applicationName { get; set; }
	public string ApplicationName { get { return _applicationName; } }
	private string _mainCommandUIName { get; set; }
	public string MainCommandUIName { get { return _mainCommandUIName; } }
	public ApplicationManager(string applicationName, string mainCommandName)
	{
		_mainNamespace = MethodBase.GetCurrentMethod().DeclaringType.Namespace;
		_applicationName = applicationName;
		_mainCommandUIName = mainCommandName;
	}



	private string _assemblyFullPath { get; set; }
	public string AssemblyFullPath { get { return _assemblyFullPath; } }
	private string _assemblyDirectoryPath { get; set; }
	public string AssemblyDirectoryPath { get { return _assemblyDirectoryPath; } }
	public void StoreAssemblyLocation()
	{
		_assemblyFullPath = Assembly.GetExecutingAssembly().Location;
		_assemblyDirectoryPath = Path.GetDirectoryName(_assemblyFullPath);
	}

	private DockablePaneId _dockablePaneId { get; } = new DockablePaneId(System.Guid.NewGuid());
	public DockablePaneId DockablePaneId { get { return _dockablePaneId; } }
	private DockablePane? _mainDockablePane { get; set; }
	public DockablePane? MainDockablePane { get { return _mainDockablePane; } }
	public void GenerateMainDockablePane(UIControlledApplication application, IDockablePaneProvider paneProvider)
	{
		application.RegisterDockablePane(
			DockablePaneId,
			ApplicationName,
			paneProvider
		);
	}

	public void StoreMainDockablePane(ExternalCommandData commandData)
	{
		_mainDockablePane = commandData.Application.GetDockablePane(DockablePaneId);
	}

	public void ShowMainDockablePane(ExternalCommandData commandData)
	{
		StoreMainDockablePane(commandData);

		MainDockablePane?.Show();
	}

	private ApplicationResourcesModel _applicationResourcesModel { get; set; } = new ApplicationResourcesModel();
	public ApplicationResourcesModel ApplicationResources { get { return _applicationResourcesModel; } }
	public void LoadApplicationConfiguration(string configDirectoryPath, string configFilePath)
	{
		if (!Directory.Exists(configDirectoryPath))
		{
			Directory.CreateDirectory(configDirectoryPath);
		}

		var jsonSerializerOptions = new JsonSerializerOptions
		{
			WriteIndented = true,
			PropertyNameCaseInsensitive = true
		};

		if (!File.Exists(configFilePath))
		{
			string defaultApplicationResourceModelText = JsonSerializer
				.Serialize<ApplicationResourcesModel>(
					new ApplicationResourcesModel(),
					jsonSerializerOptions
				);

			File.WriteAllText(configFilePath, defaultApplicationResourceModelText);
		}

		string configFileContent = File.ReadAllText(configFilePath);

		ApplicationResourcesModel arm = JsonSerializer.Deserialize<ApplicationResourcesModel>(configFileContent, jsonSerializerOptions);

		_applicationResourcesModel = arm;
	}

	public void GenerateApplicationRibbonTabButtons()
	{
		for (int i = 0; i < ApplicationResources.ApplicationButtons.Count; i++)
		{
			var btnModel = ApplicationResources.ApplicationButtons[i];

			PushButtonData buttonData = new PushButtonData(
				ApplicationName,
				btnModel.ClassName,
				AssemblyFullPath,
				$"{btnModel.Namespace}.{btnModel.Command.CommandName}");

			buttonData.ToolTip = btnModel.TooltipText;

			ApplicationRibbonPanel.AddItem(buttonData);
		}
	}


	public void CreateRibbonTab(UIControlledApplication application)
	{
		application.CreateRibbonTab(ApplicationName);
	}

	private RibbonPanel _applicationRibbonPanel { get; set; }
	public RibbonPanel ApplicationRibbonPanel { get { return _applicationRibbonPanel; } }
	public void GenerateApplicationRibbonPanel(UIControlledApplication application)
	{
		_applicationRibbonPanel = application.CreateRibbonPanel(
			ApplicationName,
			MainCommandUIName
		);
	}


	public System.Windows.Controls.ContentControl? HotswapContainer { get; set; }
	public void ReloadApplication(ExternalCommandData commandData)
	{
		ShowMainDockablePane(commandData);

		int commandsCount = ApplicationResources.ApplicationButtons.Count;

		for (int i = 0; i < commandsCount; i++)
		{
			var cmd = ApplicationResources.ApplicationButtons[i].Command;

			ReloadUI(cmd.HostDllName, cmd.Namespace, cmd.CommandName);
		}
	}

	public void ReloadUI(string uiHostDllName, string uiClassNameFullNamespace, string uiClassName)
	{
		try
		{
			string viewClassName = $"{uiClassNameFullNamespace}.{uiClassName}";

			string sourcePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), ApplicationName, uiHostDllName);

			if (!File.Exists(sourcePath))
			{
				throw new Exception($"DLL not found: {sourcePath}");
			}

			byte[] assemblyBytes = File.ReadAllBytes(sourcePath);

			Assembly loadedAssembly = Assembly.Load(assemblyBytes);

			Type? viewType = loadedAssembly
				.GetTypes()
				.FirstOrDefault(
					t =>
					t.FullName.EndsWith(uiClassName)
						||
					t.Name == uiClassName
				);

			if (viewType == null)
			{
				throw new Exception($"Could not find a UserControl named '{viewClassName}' inside {uiHostDllName}");
			}

			if (HotswapContainer != null)
			{
				HotswapContainer.Dispatcher.Invoke(() =>
				{
					object newView = Activator.CreateInstance(viewType);

					HotswapContainer.Content = newView;
				});
			}
		}
		catch (Exception ex)
		{
			throw new Exception($"Hot Reload Error {ex.Message}");
		}
	}
}

//<?xml version="1.0" encoding="utf-8"?>
//<RevitAddIns>
//<AddIn Type="Application">
//<Name>hotreloaddemo</Name>
//<Assembly>hotreloaddemo.dll</Assembly>
//<AddInId>E82A15A8-66A6-4B05-AAE4-B114EF9AAB14</AddInId>
//<FullClassName>hotreloaddemo.App</FullClassName>
//<VendorId>Eduardoos</VendorId>
//<VendorDescription>Eduardoos, www.eduardoos.com</VendorDescription>
//</AddIn>
//</RevitAddIns>