using Workspace;

namespace Workspace.Application;

public class Class1
{
	public static void PrintApplication()
	{
		Console.WriteLine("\n\n");
		Console.WriteLine("Origin: Workspace.Application");
		Kernel.Tecnico.Application.Class1.PrintApplication();
		Domain.Class1.PrintDomain();
		Console.WriteLine($"Estou em {typeof(Class1).Namespace}");
	}

	public static void UnsafePrint()
	{
		Console.WriteLine("WARNING: UNSAFE CODE!!");
		Console.WriteLine("Origin: Workspace.Application");
	}
}
