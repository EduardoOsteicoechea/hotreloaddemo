using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace hotreloaddemo;

public class App : IExternalApplication
{
	public Result OnStartup(UIControlledApplication application)
	{
		ApplicationManager manager = ApplicationManagerService.Instance();

		manager.Initialize(
			application,
			ApplicationManagerService.DefaultConfigDirectoryPath,
			ApplicationManagerService.DefaultConfigFilePath,
			ApplicationManagerService.DefaultDockablePaneProvider
			);

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
		ApplicationManagerService.Instance().ReloadApplication();

		return Result.Succeeded;
	}
}



public class HotReloadableApplicationModel
{
	public string? HostDllName { get; set; }
	public string? StartupClassNamespace { get; set; }
	public string? StartupClassName { get; set; }
	public string? PanelName { get; set; }
	public string? TooltipText { get; set; }
}

public static class ApplicationManagerService
{
	public static string? ApplicationNamespace = MethodBase.GetCurrentMethod().DeclaringType.Namespace;

	public static string? DefaultConfigDirectoryPath = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
		ApplicationNamespace
	);

	public static string? DefaultConfigFilePath = Path.Combine(
		DefaultConfigDirectoryPath,
		"ApplicationConfiguration.json"
	);

	public static string? DefaultApplicationName = ApplicationNamespace;

	public static IDockablePaneProvider? DefaultDockablePaneProvider = new MainPage();

	private static ApplicationManager? _instance { get; set; }
	public static ApplicationManager Instance(string applicationName = "", string mainCommandName = "Launch")
	{
		if (string.IsNullOrEmpty(applicationName))
		{
			applicationName = DefaultApplicationName;
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
	public string? _mainNamespace { get; set; }
	public string? MainNamespace { get { return _mainNamespace; } }

	private string? _applicationName { get; set; }
	public string? ApplicationName { get { return _applicationName; } }

	private string? _mainCommandUIName { get; set; }
	public string? MainCommandUIName { get { return _mainCommandUIName; } }

	private string? _assemblyFullPath { get; set; }
	public string? AssemblyFullPath { get { return _assemblyFullPath; } }

	private string? _assemblyDirectoryPath { get; set; }
	public string? AssemblyDirectoryPath { get { return _assemblyDirectoryPath; } }

	public ApplicationManager(string applicationName, string mainCommandName)
	{
		_mainNamespace = MethodBase.GetCurrentMethod().DeclaringType.Namespace;
		_applicationName = applicationName;
		_mainCommandUIName = mainCommandName;
		_assemblyFullPath = Assembly.GetExecutingAssembly().Location;
		_assemblyDirectoryPath = Path.GetDirectoryName(_assemblyFullPath);
	}

	private JsonSerializerOptions _jsonSerializerOptions { get; set; }
	public JsonSerializerOptions JsonSerializerOptions { get { return _jsonSerializerOptions; } }

	public DockablePaneId DockablePaneId { get; } = new DockablePaneId(new Guid("E82A15A8-66A6-4B05-AAE4-B114EF9AAB14"));
	private DockablePane? _mainDockablePane { get; set; }
	public DockablePane? MainDockablePane { get { return _mainDockablePane; } }

	private UIControlledApplication _application { get; set; }
	public UIControlledApplication Application { get { return _application; } }

	private string _configurationDirectoryPath { get; set; }
	public string ConfigurationDirectoryPath { get { return _configurationDirectoryPath; } }

	private string _configurationFilePath { get; set; }
	public string ConfigurationFilePath { get { return _configurationFilePath; } }

	public IDockablePaneProvider _paneProvider { get; set; }
	public IDockablePaneProvider PaneProvider { get { return _paneProvider; } }

	private HotReloadableApplicationModel _applicationResourcesModel { get; set; }
	public HotReloadableApplicationModel HotReloadableApplication { get { return _applicationResourcesModel; } }

	public void Initialize(UIControlledApplication application, string configurationDirectoryPath, string configurationFilePath, IDockablePaneProvider paneProvider)
	{
		_application = application;
		_configurationDirectoryPath = configurationDirectoryPath;
		_configurationFilePath = configurationFilePath;
		_paneProvider = paneProvider;

		SetJsonSerializerOptions();

		ValidateConfigurationDirectoryPath();

		ValidateConfigurationFilePath();

		GetConfiguration();

		GenerateMainDockablePane();

		CreateRibbonTab();

		GenerateApplicationRibbonPanel();

		GenerateApplicationRibbonTabButton();
	}

	public void SetJsonSerializerOptions()
	{
		_jsonSerializerOptions = new JsonSerializerOptions
		{
			WriteIndented = true,
			PropertyNameCaseInsensitive = true
		};
	}

	public void ValidateConfigurationDirectoryPath()
	{
		if (!Directory.Exists(ConfigurationDirectoryPath))
		{
			Directory.CreateDirectory(ConfigurationDirectoryPath);
		}
	}

	public void ValidateConfigurationFilePath()
	{
		if (!File.Exists(ConfigurationFilePath))
		{
			string content = JsonSerializer
				.Serialize(
					new HotReloadableApplicationModel(),
					JsonSerializerOptions
				);

			File.WriteAllText(ConfigurationFilePath, content);
		}
	}

	public void GetConfiguration()
	{
		string content = File.ReadAllText(ConfigurationFilePath);

		_applicationResourcesModel = JsonSerializer.Deserialize<HotReloadableApplicationModel>(content, JsonSerializerOptions);
	}

	public void GenerateMainDockablePane()
	{
		Application.RegisterDockablePane(
			DockablePaneId,
			ApplicationName,
			PaneProvider
		);
	}

	public void StoreMainDockablePane()
	{
		if (_mainDockablePane is null)
		{
			_mainDockablePane = Application.GetDockablePane(DockablePaneId);
		}
	}

	public void ShowMainDockablePane()
	{
		StoreMainDockablePane();

		MainDockablePane?.Show();
	}

	public void GenerateApplicationRibbonTabButton()
	{
		PushButtonData buttonData = new PushButtonData(
			ApplicationName,
			HotReloadableApplication.PanelName,
			AssemblyFullPath,
			$"{HotReloadableApplication.StartupClassNamespace}.{HotReloadableApplication.StartupClassName}");

		buttonData.ToolTip = HotReloadableApplication.TooltipText;

		ApplicationRibbonPanel.AddItem(buttonData);
	}


	public void CreateRibbonTab()
	{
		Application.CreateRibbonTab(ApplicationName);
	}

	private RibbonPanel _applicationRibbonPanel { get; set; }
	public RibbonPanel ApplicationRibbonPanel { get { return _applicationRibbonPanel; } }
	public void GenerateApplicationRibbonPanel()
	{
		_applicationRibbonPanel = Application.CreateRibbonPanel(
			ApplicationName,
			MainCommandUIName
		);
	}


	public System.Windows.Controls.ContentControl? HotswapContainer { get; set; }
	public void ReloadApplication()
	{
		ShowMainDockablePane();

		System.Windows.MessageBox.Show($"{HotReloadableApplication.HostDllName}: {HotReloadableApplication.HostDllName}, {HotReloadableApplication.StartupClassNamespace}: {HotReloadableApplication.StartupClassNamespace}, {HotReloadableApplication.StartupClassName}: {HotReloadableApplication.StartupClassName}");

		ReloadUI(HotReloadableApplication.HostDllName, HotReloadableApplication.StartupClassNamespace, HotReloadableApplication.StartupClassName);
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
			throw new Exception($"Hot Reload Error {ex.Message}\n {uiHostDllName}, {uiClassNameFullNamespace}, {uiClassName}");
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