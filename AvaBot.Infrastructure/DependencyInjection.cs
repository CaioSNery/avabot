using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using AvaBot.Infrastructure.Browser;
using AvaBot.Infrastructure.Extraction;
using AvaBot.Infrastructure.OpenAI;
using AvaBot.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AvaBot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<BrowserOptions>(configuration.GetSection(BrowserOptions.SectionName));
        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));
        services.Configure<QuestionExtractionOptions>(
            configuration.GetSection(QuestionExtractionOptions.SectionName));
        services.Configure<ManualQuizNavigationOptions>(
            configuration.GetSection(ManualQuizNavigationOptions.SectionName));
        services.AddTransient<IBrowserFactory, BrowserFactory>();
        services.AddTransient<IQuestionExtractor, QuestionExtractorService>();
        services.AddTransient<IQuestionConsoleFlow, QuestionConsoleFlow>();
        services.AddTransient<IManualQuizNavigationService, ManualQuizNavigationService>();
        services.AddSingleton<IActivityProgressStore, ActivityProgressStore>();
        services.AddHttpClient<IOpenAiClient, OpenAiClient>((serviceProvider, client) =>
        {
            var options = serviceProvider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenAiOptions>>()
                .Value;
            client.BaseAddress = new Uri("https://api.openai.com/v1/");
            client.Timeout = TimeSpan.FromSeconds(
                options.RequestTimeoutSeconds > 0
                    ? options.RequestTimeoutSeconds
                    : 300);
        });
        services.AddSingleton<PromptLoader>();
        services.AddSingleton<PromptRepository>();
        services.AddSingleton<InferenceCache>();
        services.AddTransient<IInferenceEngine, OpenAiInferenceEngine>();

        return services;
    }
}
