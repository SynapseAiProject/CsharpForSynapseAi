using Kernel.Tecnico;

namespace Kernel.Tecnico.Application;

public class Class1
{
	public static void PrintApplication()
	{
		Console.WriteLine("\n\n");
		Console.WriteLine("Origin: Kernel.Tecnico.Application");
		Domain.Class1.PrintDomain();
		Console.WriteLine($"Estou em {typeof(Class1).Namespace}");
	}
}
