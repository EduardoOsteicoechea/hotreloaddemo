using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace SayHello
{
	[Transaction(TransactionMode.Manual)]
	public class SayHello : IExternalCommand
	{
		public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
		{
			System.Windows.MessageBox.Show("zzzzzzzzzzzz");

			return Result.Succeeded;
		}
	}
}
