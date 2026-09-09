using Workspace;

namespace Workspace.Infrastructure;

public class Class1
{
	public static void PrintInfrastructure()
	{
		Console.WriteLine("\n\n");
		Console.WriteLine("Origin: Workspace.Infrastructure");
		Kernel.Tecnico.Infrastructure.Class1.PrintInfrastructure();
		Domain.Class1.PrintDomain();
		Application.Class1.PrintApplication();
		Console.WriteLine($"Estou em {typeof(Class1).Namespace}");
	}
}
