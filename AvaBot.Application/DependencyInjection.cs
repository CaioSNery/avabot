using AvaBot.Application.Services;
using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AvaBot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PortalOptions>(configuration.GetSection(PortalOptions.SectionName));
        services.Configure<AvaOptions>(configuration.GetSection(AvaOptions.SectionName));
        services.Configure<ActivityNavigationOptions>(
            configuration.GetSection(ActivityNavigationOptions.SectionName));
        services.Configure<ActivityDiscoveryOptions>(
            configuration.GetSection(ActivityDiscoveryOptions.SectionName));
        services.Configure<QuizSubmissionOptions>(
            configuration.GetSection(QuizSubmissionOptions.SectionName));
        services.Configure<DidacticBookOptions>(
            configuration.GetSection(DidacticBookOptions.SectionName));
        services.Configure<AnswerFillOptions>(
            configuration.GetSection(AnswerFillOptions.SectionName));

        services.AddTransient<IBrowserService, BrowserService>();
        services.AddTransient<IPortalLoginService, PortalLoginService>();
        services.AddTransient<IAuthenticationFlowService, AuthenticationFlowService>();
        services.AddTransient<IPageReadyService, PageReadyService>();
        services.AddTransient<IModalService, ModalService>();
        services.AddTransient<IDashboardService, DashboardService>();
        services.AddTransient<IPortalNavigationService, PortalNavigationService>();
        services.AddTransient<IAvaLoginService, AvaLoginService>();
        services.AddTransient<IAvaAuthenticationService, AvaAuthenticationService>();
        services.AddTransient<IAvaPageInspectionService, AvaPageInspectionService>();
        services.AddTransient<IAvaExplorerService, AvaExplorerService>();
        services.AddTransient<IDisciplinaNavigationService, DisciplinaNavigationService>();
        services.AddTransient<ICookieBannerService, CookieBannerService>();
        services.AddTransient<IDidacticBookService, DidacticBookService>();
        services.AddTransient<IUnitExplorerService, UnitExplorerService>();
        services.AddTransient<IUnitNavigationService, UnitNavigationService>();
        services.AddTransient<IUnitAvailabilityService, UnitAvailabilityService>();
        services.AddTransient<ILinearCourseExplorerService, LinearCourseExplorerService>();
        services.AddSingleton<DisciplineExecutionPolicy>();
        services.AddTransient<ISectionExplorerService, SectionExplorerService>();
        services.AddTransient<ISectionNavigationService, SectionNavigationService>();
        services.AddTransient<IActivityExplorerService, ActivityExplorerService>();
        services.AddTransient<IActivityNavigationService, ActivityNavigationService>();
        services.AddTransient<IAnswerFillService, AnswerFillService>();
        services.AddTransient<IQuizSubmissionService, QuizSubmissionService>();
        services.AddSingleton<QuestionBatchBuilder>();
        services.AddSingleton<InferenceValidator>();
        services.AddSingleton<IQuestionParser, QuestionParserService>();
        services.AddSingleton<IQuestionJsonExporter, QuestionJsonExporter>();
        services.AddTransient<IInferenceConsoleFlow, InferenceConsoleFlow>();
        services.AddTransient<IPipelinePauseService, PipelinePauseService>();
        services.AddTransient<IBotRunner, BotRunner>();

        return services;
    }
}
