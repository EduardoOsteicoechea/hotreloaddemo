using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Xml.Linq;
using UIFramework;

namespace hotreloaddemo;

public class MainDockablePaneCreator : IFrameworkElementCreator
{
	public FrameworkElement CreateFrameworkElement()
	{
		return new MainPage();
	}
}

public class MainDockablePaneProvider : Autodesk.Revit.UI.IDockablePaneProvider
{
	//public MainDockablePaneProvider()
	//{
	//	SetupDockablePane(new DockablePaneProviderData());
	//}

	public void SetupDockablePane(DockablePaneProviderData data)
	{
		data.VisibleByDefault = true;

		data.InitialState = new DockablePaneState()
		{
			DockPosition = DockPosition.Tabbed
		};

		data.FrameworkElementCreator = new MainDockablePaneCreator();
	}
}

public class App : IExternalApplication
{
	public Result OnStartup(UIControlledApplication application)
	{
		string applicationName = MethodBase.GetCurrentMethod().DeclaringType.Namespace;

		string configDirectoryPath = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
			applicationName
		);

		string configFilePath = Path.Combine(
			configDirectoryPath,
			"ApplicationConfiguration.json"
		);

		ApplicationManager.LoadApplicationConfiguration(applicationName, configDirectoryPath, configFilePath);

		ApplicationManager.StoreAssemblyLocation();

		ApplicationManager.GenerateMainDockablePane(application, new MainDockablePaneProvider());

		ApplicationManager.CreateRibbonTab(application);

		ApplicationManager.GenerateApplicationRibbonPanel(application);

		ApplicationManager.GenerateApplicationRibbonTabButtons();

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
		ApplicationManager.ShowMainDockablePane(commandData);

		// --- HOT RELOAD UI LOGIC ---
		try
		{
			// 1. Define what we are looking for
			string targetDllName = "SayHello.dll";
			string commandClassName = "SayHelloView";
			string viewClassName = $"SayHello.{commandClassName}"; // The UserControl class name in your other project

			string sourcePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), ApplicationManager.ApplicationName, targetDllName);

			if (!File.Exists(sourcePath))
			{
				TaskDialog.Show("Error", "DLL not found: " + sourcePath);
				return Result.Failed;
			}

			byte[] assemblyBytes = File.ReadAllBytes(sourcePath);
			Assembly loadedAssembly = Assembly.Load(assemblyBytes);

			Type? viewType = loadedAssembly
				.GetTypes()
				.FirstOrDefault(
					t =>
					t.FullName.EndsWith(commandClassName)
						||
					t.Name == commandClassName
				);

			if (viewType == null)
			{
				TaskDialog.Show("Error", $"Could not find a UserControl named '{viewClassName}' inside {targetDllName}");
				return Result.Failed;
			}

			if (ApplicationManager.HotswapContainer != null)
			{
				ApplicationManager.HotswapContainer.Dispatcher.Invoke(() =>
				{
					// Create the new UI element
					object newView = Activator.CreateInstance(viewType);

					// Inject it into our Shell
					ApplicationManager.HotswapContainer.Content = newView;
				});
			}
		}
		catch (Exception ex)
		{
			TaskDialog.Show("Hot Reload Error", ex.ToString());
			return Result.Failed;
		}

		return Result.Succeeded;
	}
}

//[Transaction(TransactionMode.Manual)]
//public class LaunchMainDockablePane : IExternalCommand
//{




//public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
//{
//	ApplicationManager.MainDockablePane = commandData.Application.GetDockablePane(ApplicationManager.DockablePaneId);

//	ApplicationManager.MainDockablePane.Show();

//	LoadDll(commandData, ref message, elements);

//	return Result.Succeeded;
//}
//	public Result LoadDll(ExternalCommandData commandData, ref string message, ElementSet elements)
//	{
//		try
//		{
//			string targetDllName = "SayHello.dll";
//			string commandClassName = "SayHello";

//			// A. Resolve Path
//			string sourcePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), ApplicationManager.ApplicationName, targetDllName);

//			if (!File.Exists(sourcePath))
//			{
//				TaskDialog.Show("Hot Reload Error", $"Could not find '{targetDllName}' at:\n{sourcePath}");
//				return Result.Failed;
//			}

//			// B. "Copy Content & Release" Logic
//			// 1. Read the file into memory (RAM) immediately.
//			// File.ReadAllBytes opens the file, reads it, and closes it instantly.
//			byte[] assemblyBytes = File.ReadAllBytes(sourcePath);

//			// 2. Load the Assembly from the byte array in memory.
//			// This leaves NO lock on the actual .dll file on disk.
//			Assembly loadedAssembly = Assembly.Load(assemblyBytes);

//			// C. Find the Command using Reflection
//			Type? commandType = loadedAssembly
//				.GetTypes()
//				.FirstOrDefault(
//					type =>
//					typeof(IExternalCommand).IsAssignableFrom(type) &&
//					type.Name.Equals(commandClassName, StringComparison.OrdinalIgnoreCase)
//				);

//			// Fallback: If exact name isn't found, find the first available command
//			if (commandType == null)
//			{
//				commandType = loadedAssembly
//					.GetTypes()
//					.FirstOrDefault(
//						t =>
//						typeof(IExternalCommand)
//						.IsAssignableFrom(t) 
//							&& 
//						!t.IsInterface 
//							&& 
//						!t.IsAbstract
//					);
//			}

//			if (commandType == null)
//			{
//				TaskDialog.Show("Error", $"No class implementing IExternalCommand found inside {targetDllName}");
//				return Result.Failed;
//			}

//			// D. Instantiate and Run
//			IExternalCommand? commandInstance = Activator.CreateInstance(commandType) as IExternalCommand;

//			if (commandInstance != null)
//			{
//				return commandInstance.Execute(commandData, ref message, elements);
//			}

//			return Result.Failed;
//		}
//		catch (Exception ex)
//		{
//			message = ex.Message;
//			TaskDialog.Show("Hot Reload Exception", ex.ToString());
//			return Result.Failed;
//		}
//	}
//}



public class ApplicationResourcesModel
{
	public string? ApplicationConfigurationResourcesDirectoryPath { get; set; }
	public List<ApplicationButtonModel> ApplicationButtons { get; set; } = new List<ApplicationButtonModel>();
}

public class ApplicationButtonModel
{
	public string? CommandName { get; set; }
	public string? FullClassName { get; set; }
	public string? TooltipText { get; set; }
}

public static class ApplicationManager
{
	private static string _assemblyFullPath { get; set; }
	public static string AssemblyFullPath { get { return _assemblyFullPath; } }
	private static string _assemblyDirectoryPath { get; set; }
	public static string AssemblyDirectoryPath { get { return _assemblyDirectoryPath; } }
	public static void StoreAssemblyLocation()
	{
		_assemblyFullPath = Assembly.GetExecutingAssembly().Location;
		_assemblyDirectoryPath = Path.GetDirectoryName(_assemblyFullPath);
	}

	private static DockablePaneId _dockablePaneId { get; } = new DockablePaneId(System.Guid.NewGuid());
	public static DockablePaneId DockablePaneId { get { return _dockablePaneId; } }
	private static DockablePane? _mainDockablePane { get; set; }
	public static DockablePane? MainDockablePane { get { return _mainDockablePane; } }
	public static void GenerateMainDockablePane(UIControlledApplication application, IDockablePaneProvider paneProvider)
	{
		application.RegisterDockablePane(
			ApplicationManager.DockablePaneId,
			ApplicationManager.ApplicationName,
			paneProvider
		);
	}

	public static void StoreMainDockablePane(ExternalCommandData commandData)
	{
		_mainDockablePane = commandData.Application.GetDockablePane(ApplicationManager.DockablePaneId);
	}

	public static void ShowMainDockablePane(ExternalCommandData commandData)
	{
		StoreMainDockablePane(commandData);

		MainDockablePane?.Show();
	}

	private static ApplicationResourcesModel _applicationResourcesModel { get; set; } = new ApplicationResourcesModel();
	public static ApplicationResourcesModel ApplicationResourcesModel { get; } = new ApplicationResourcesModel();
	public static void LoadApplicationConfiguration(string applicationName, string configDirectoryPath, string configFilePath)
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

	public static void GenerateApplicationRibbonTabButtons()
	{
		for (int i = 0; i < ApplicationResourcesModel.ApplicationButtons.Count; i++)
		{
			var btnModel = ApplicationResourcesModel.ApplicationButtons[i];

			PushButtonData buttonData = new PushButtonData(
				ApplicationManager.ApplicationName,
				btnModel.FullClassName,
				ApplicationManager.AssemblyFullPath,
				$"{ApplicationManager.MainNamespace}.{btnModel.CommandName}");

			buttonData.ToolTip = btnModel.TooltipText;

			ApplicationRibbonPanel.AddItem(buttonData);
		}
	}


	public static void CreateRibbonTab(UIControlledApplication application)
	{
		application.CreateRibbonTab(ApplicationName);
	}

	private static RibbonPanel _applicationRibbonPanel { get; set; }
	public static RibbonPanel ApplicationRibbonPanel { get { return _applicationRibbonPanel; } }
	public static void GenerateApplicationRibbonPanel(UIControlledApplication application)
	{
		_applicationRibbonPanel = application.CreateRibbonPanel(
			ApplicationName,
			MainCommandUIName
		);
	}


	public static string MainNamespace { get; } = MethodBase.GetCurrentMethod().DeclaringType.Namespace;
	public static string MainCommandInternalName { get; } = "LaunchMainDockablePane";
	public static string ApplicationName { get; } = "hotreloaddemo";
	public static string MainCommandUIName { get; } = "Launch";
	public static string MainCommandToolTipText { get; } = $"Launch The {ApplicationName} Revit API Assistance toolkit";


	public static System.Windows.Controls.ContentControl? HotswapContainer { get; set; }
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