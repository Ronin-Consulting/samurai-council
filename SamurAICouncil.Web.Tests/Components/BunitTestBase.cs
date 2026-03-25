using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using MudBlazor.Services;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Services;
using SamurAICouncil.Web.Services;

namespace SamurAICouncil.Web.Tests.Components;

/// <summary>
/// Base class for bUnit component tests providing common setup.
/// </summary>
public abstract class BunitTestBase : BunitContext
{
    protected Mock<ICouncilService> MockCouncilService { get; private set; } = null!;
    protected Mock<IExportService> MockExportService { get; private set; } = null!;
    protected ConversationState ConversationState { get; private set; } = null!;

    [TestInitialize]
    public virtual void Setup()
    {
        MockCouncilService = new Mock<ICouncilService>();
        MockExportService = new Mock<IExportService>();
        ConversationState = new ConversationState();

        Services.AddSingleton(MockCouncilService.Object);
        Services.AddSingleton(MockExportService.Object);
        Services.AddSingleton(ConversationState);
        Services.AddMudServices();

        // Register ChartDataTransformer for chart components
        var chartLogger = new Mock<ILogger<ChartDataTransformer>>();
        Services.AddSingleton<IChartDataTransformer>(new ChartDataTransformer(chartLogger.Object));

        // Setup MudBlazor JSInterop mocks
        JSInterop.Mode = JSRuntimeMode.Loose;
    }
}
