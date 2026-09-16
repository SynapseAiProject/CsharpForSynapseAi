using Xunit;
using BoundedContexts.Workspace;

using BoundedContexts.Workspace.Domain.Workspace.Hub;
using BoundedContexts.Workspace.Domain.Workspace.Workspace;
using BoundedContexts.Workspace.Domain.Workspace.App;
using BoundedContexts.Kernel.Tecnico.Domain;

namespace BoundedContexts.Workspace.Tests.HubTest;

public class UnitTest1
{
    /* [Theory]
	[InlineData(2, 3, 5)]
	[InlineData(10, 5, 15)]
	[InlineData(-2, 5, 3)]
    public void Test1()
    {
		// Arrange

		// Act

		// Assert
    } */

	[Fact(DisplayName = "Criar Hub para Workspace funciona")]
	public void Criar_Hub_Para_Workspace_Funciona()
	{
		// Arrange
		var workspace = new Domain.Workspace.Workspace.Workspace(Domain.Workspace.Workspace.WorkspaceId.New(), "brunofer");
		// Act
		var hub = Hub.CreateFor(workspace.Id);

		// Assert
		Assert.Equal(workspace.Id, hub.WorkspaceId);
	}

	[Fact(DisplayName = "Instalar novo App no Hub funciona")]
	public void Instalar_App_Novo_No_Hub_Funciona()
	{
		// Arrange
		var workspace = new Domain.Workspace.Workspace.Workspace(Domain.Workspace.Workspace.WorkspaceId.New(), "brunofer");
		var hub = Hub.CreateFor(workspace.Id);
		Assert.Equal(workspace.Id, hub.WorkspaceId);
		var app = new App(AppId.New(), "Drive");
		// Act
		hub.Install(app);
		// Assert
		Assert.Single(hub.Installations);
		Assert.Single(hub.Installations.Added);
		Assert.Empty(hub.Installations.Updated);
		Assert.Empty(hub.Installations.Removed);
	}

	[Fact(DisplayName = "Instalar App já instalado no Hub lança erro")]
	public void Instalar_Mesmo_App_No_Hub_Lanca_Erro()
	{
		// Arrange
		var workspace = new Domain.Workspace.Workspace.Workspace(Domain.Workspace.Workspace.WorkspaceId.New(), "brunofer");
		var hub = Hub.CreateFor(workspace.Id);
		Assert.Equal(workspace.Id, hub.WorkspaceId);
		var app = new App(AppId.New(), "Drive");
		hub.Install(app);
		var exception = Assert.Throws<DomainException>(() => hub.Install(app));
		// Act
		Assert.Equal($"App {app.Id} já está instalado e ativo neste hub.", exception.Message);
	}
}
