namespace Workspace.Domain;

public class Class1
{
	public static void PrintDomain()
	{
		Console.WriteLine("\n\n");
		Console.WriteLine("Origin: Workspace.Domain");
		Kernel.Tecnico.Domain.Class1.PrintDomain();
		Console.WriteLine($"Estou em {typeof(Class1).Namespace}");
	}
}
