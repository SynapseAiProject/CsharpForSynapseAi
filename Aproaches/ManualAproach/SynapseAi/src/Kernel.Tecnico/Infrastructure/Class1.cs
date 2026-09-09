using Kernel.Tecnico;

namespace Kernel.Tecnico.Infrastructure;

public class Class1
{
	public static void PrintInfrastructure()
	{
		Console.WriteLine("\n\n");
		Console.WriteLine("Origin: Kernel.Tecnico.Infrastructure");
		Application.Class1.PrintApplication();
		Domain.Class1.PrintDomain();
		Console.WriteLine($"Estou em {typeof(Class1).Namespace}");
	}
}
