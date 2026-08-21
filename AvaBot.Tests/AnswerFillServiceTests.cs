using AvaBot.Application.Services;
using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AvaBot.Tests;

public sealed class AnswerFillServiceTests
{
    private const string QuestionSelector = "questions";
    private const string AlternativeSelector = "alternatives";
    private const string NavigationSelector = "navigation";
    private const string NextPageSelector = "next-page";
    private const string InputSelector =
        "input[type='radio'], input[type='checkbox']";
    private const string CheckedChoiceSelector =
        "input[type='radio']:checked, input[type='checkbox']:checked";

    [Fact]
    public async Task WaitForManualAnswerAsync_ReturnsTrueForSelectedQuestion()
    {
        var question = TestLocator.Leaf(
            new Dictionary<string, string?> { ["data-question-number"] = "1" });
        question.Children[CheckedChoiceSelector] = TestLocator.Leaf();
        var page = new TestAutomationPage(new Dictionary<string, TestLocator>
        {
            [QuestionSelector] = TestLocator.Collection(question),
            [NavigationSelector] = TestLocator.Empty()
        });

        var answered = await CreateService().WaitForManualAnswerAsync(
            page,
            "1",
            TimeSpan.FromSeconds(30));

        Assert.True(answered);
    }

    [Fact]
    public async Task WaitForManualAnswerAsync_ReturnsFalseAfterUnansweredQuestionTimeout()
    {
        var question = TestLocator.Leaf(
            new Dictionary<string, string?> { ["data-question-number"] = "1" });
        question.Children[CheckedChoiceSelector] = TestLocator.Empty();
        var page = new TestAutomationPage(new Dictionary<string, TestLocator>
        {
            [QuestionSelector] = TestLocator.Collection(question),
            [NavigationSelector] = TestLocator.Empty()
        });

        var answered = await CreateService().WaitForManualAnswerAsync(
            page,
            "1",
            TimeSpan.FromSeconds(30));

        Assert.False(answered);
    }

    [Fact]
    public async Task FillAnswerAsync_ClicksSelectedOptionInsideMatchingQuestion()
    {
        var optionA = TestLocator.Leaf(
            new Dictionary<string, string?> { ["data-option"] = "A" });
        var optionB = TestLocator.Leaf(
            new Dictionary<string, string?> { ["data-option"] = "B" });
        optionA.Children[InputSelector] = TestLocator.Empty();
        optionB.Children[InputSelector] = TestLocator.Empty();

        var question = TestLocator.Leaf(
            new Dictionary<string, string?> { ["data-question-number"] = "1" });
        question.Children[AlternativeSelector] = TestLocator.Collection(optionA, optionB);

        var page = new TestAutomationPage(new Dictionary<string, TestLocator>
        {
            [QuestionSelector] = TestLocator.Collection(question),
            [NavigationSelector] = TestLocator.Empty()
        });
        var service = CreateService();

        var result = await service.FillAnswerAsync(
            page,
            "1",
            "B",
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(optionA.Clicked);
        Assert.True(optionB.Clicked);
    }

    [Fact]
    public async Task FillAnswerAsync_ReturnsFailureWhenQuestionDoesNotExist()
    {
        var page = new TestAutomationPage(new Dictionary<string, TestLocator>
        {
            [QuestionSelector] = TestLocator.Empty(),
            [NavigationSelector] = TestLocator.Empty()
        });
        var service = CreateService();

        var result = await service.FillAnswerAsync(
            page,
            "7",
            "A",
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("não foi encontrada", result.Message);
    }

    [Fact]
    public async Task FillAnswerAsync_ScopesAlternativeToTheMatchingQuestionContainer()
    {
        var questionOneA = TestLocator.Leaf(text: "a. Questão um A");
        var questionOneB = TestLocator.Leaf(text: "b. Questão um B");
        var questionTwoA = TestLocator.Leaf(text: "a. Questão dois A");
        var questionTwoB = TestLocator.Leaf(text: "b. Questão dois B");

        foreach (var option in new[] { questionOneA, questionOneB, questionTwoA, questionTwoB })
        {
            option.Children[InputSelector] = TestLocator.Empty();
        }

        var questionOne = TestLocator.Leaf(
            new Dictionary<string, string?> { ["data-question-number"] = "1" });
        questionOne.Children[AlternativeSelector] =
            TestLocator.Collection(questionOneA, questionOneB);
        var questionTwo = TestLocator.Leaf(
            new Dictionary<string, string?> { ["data-question-number"] = "2" });
        questionTwo.Children[AlternativeSelector] =
            TestLocator.Collection(questionTwoA, questionTwoB);
        var nextPageButton = TestLocator.Leaf();
        var page = new TestAutomationPage(new Dictionary<string, TestLocator>
        {
            [QuestionSelector] = TestLocator.Collection(questionOne, questionTwo),
            [NavigationSelector] = TestLocator.Empty(),
            [NextPageSelector] = TestLocator.Collection(nextPageButton)
        });

        var result = await CreateService().FillAnswerAsync(
            page,
            "2",
            "B",
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(questionOneA.Clicked);
        Assert.False(questionOneB.Clicked);
        Assert.False(questionTwoA.Clicked);
        Assert.True(questionTwoB.Clicked);
        Assert.False(nextPageButton.Clicked);
    }

    [Fact]
    public async Task FillAnswerAsync_DoesNotUseUnsafeOrdinalFallback()
    {
        var unidentifiedA = TestLocator.Leaf(text: "Primeira opção sem identificador");
        var unidentifiedB = TestLocator.Leaf(text: "Segunda opção sem identificador");
        unidentifiedA.Children[InputSelector] = TestLocator.Empty();
        unidentifiedB.Children[InputSelector] = TestLocator.Empty();
        var question = TestLocator.Leaf(
            new Dictionary<string, string?> { ["data-question-number"] = "1" });
        question.Children[AlternativeSelector] =
            TestLocator.Collection(unidentifiedA, unidentifiedB);
        var page = new TestAutomationPage(new Dictionary<string, TestLocator>
        {
            [QuestionSelector] = TestLocator.Collection(question),
            [NavigationSelector] = TestLocator.Empty()
        });

        var result = await CreateService().FillAnswerAsync(
            page,
            "1",
            "B",
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(unidentifiedA.Clicked);
        Assert.False(unidentifiedB.Clicked);
    }

    [Fact]
    public async Task FillAnswerAsync_DoesNotTreatSoleVisibleQuestionAsAnotherNumber()
    {
        var option = TestLocator.Leaf(
            new Dictionary<string, string?> { ["data-option"] = "A" });
        option.Children[InputSelector] = TestLocator.Empty();
        var visibleQuestion = TestLocator.Leaf(
            new Dictionary<string, string?> { ["data-question-number"] = "1" });
        visibleQuestion.Children[AlternativeSelector] = TestLocator.Collection(option);
        var page = new TestAutomationPage(new Dictionary<string, TestLocator>
        {
            [QuestionSelector] = TestLocator.Collection(visibleQuestion),
            [NavigationSelector] = TestLocator.Empty()
        });

        var result = await CreateService().FillAnswerAsync(
            page,
            "2",
            "A",
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(option.Clicked);
    }

    [Fact]
    public async Task FillAnswerAsync_DryRunValidatesMappingWithoutClicking()
    {
        var option = TestLocator.Leaf(
            new Dictionary<string, string?> { ["data-option"] = "A" });
        option.Children[InputSelector] = TestLocator.Empty();
        var question = TestLocator.Leaf(
            new Dictionary<string, string?> { ["data-question-number"] = "1" });
        question.Children[AlternativeSelector] = TestLocator.Collection(option);
        var page = new TestAutomationPage(new Dictionary<string, TestLocator>
        {
            [QuestionSelector] = TestLocator.Collection(question),
            [NavigationSelector] = TestLocator.Empty()
        });

        var result = await CreateService(dryRun: true).FillAnswerAsync(
            page,
            "1",
            "A",
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(option.Clicked);
        Assert.Contains("dry-run", result.Message);
    }

    [Fact]
    public async Task FillAnswerAsync_UsesNextPageForPaginatedQuestionnaire()
    {
        var questionOneOption = TestLocator.Leaf(
            new Dictionary<string, string?> { ["data-option"] = "A" });
        questionOneOption.Children[InputSelector] = TestLocator.Empty();
        var questionOne = TestLocator.Leaf(
            new Dictionary<string, string?> { ["data-question-number"] = "1" });
        questionOne.Children[AlternativeSelector] =
            TestLocator.Collection(questionOneOption);

        var questionTwoOption = TestLocator.Leaf(
            new Dictionary<string, string?> { ["data-option"] = "B" });
        questionTwoOption.Children[InputSelector] = TestLocator.Empty();
        var questionTwo = TestLocator.Leaf(
            new Dictionary<string, string?> { ["data-question-number"] = "2" });
        questionTwo.Children[AlternativeSelector] =
            TestLocator.Collection(questionTwoOption);

        var locators = new Dictionary<string, TestLocator>();
        var nextPageButton = TestLocator.Leaf(
            onClick:
            () => locators[QuestionSelector] = TestLocator.Collection(questionTwo));
        locators[QuestionSelector] = TestLocator.Collection(questionOne);
        locators[NavigationSelector] = TestLocator.Empty();
        locators[NextPageSelector] = TestLocator.Collection(nextPageButton);
        var page = new TestAutomationPage(locators);

        var result = await CreateService().FillAnswerAsync(
            page,
            "2",
            "B",
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(nextPageButton.Clicked);
        Assert.False(questionOneOption.Clicked);
        Assert.True(questionTwoOption.Clicked);
    }

    [Fact]
    public async Task FillAnswerAsync_UsesQuestionIndexToReturnFromLastPage()
    {
        var questionOneOption = TestLocator.Leaf(
            new Dictionary<string, string?> { ["data-option"] = "A" });
        questionOneOption.Children[InputSelector] = TestLocator.Empty();
        var questionOne = TestLocator.Leaf(
            new Dictionary<string, string?> { ["data-question-number"] = "1" });
        questionOne.Children[AlternativeSelector] =
            TestLocator.Collection(questionOneOption);

        var questionThree = TestLocator.Leaf(
            new Dictionary<string, string?> { ["data-question-number"] = "3" });
        var navigationToQuestionOne = TestLocator.Leaf(
            new Dictionary<string, string?>
            {
                ["href"] = "/mod/quiz/attempt.php?attempt=1&page=0",
                ["data-question-number"] = "1"
            },
            text: "1");
        var nextPageButton = TestLocator.Leaf();
        var locators = new Dictionary<string, TestLocator>
        {
            [QuestionSelector] = TestLocator.Collection(questionThree),
            [NavigationSelector] = TestLocator.Collection(navigationToQuestionOne),
            [NextPageSelector] = TestLocator.Collection(nextPageButton)
        };
        var page = new TestAutomationPage(
            locators,
            _ => locators[QuestionSelector] = TestLocator.Collection(questionOne));

        var result = await CreateService().FillAnswerAsync(
            page,
            "1",
            "A",
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("page=0", page.Url);
        Assert.False(navigationToQuestionOne.Clicked);
        Assert.False(nextPageButton.Clicked);
        Assert.True(questionOneOption.Clicked);
    }

    [Fact]
    public async Task FillAnswerAsync_DoesNotUseNextPageToReachEarlierQuestion()
    {
        var questionThree = TestLocator.Leaf(
            new Dictionary<string, string?> { ["data-question-number"] = "3" });
        var nextPageButton = TestLocator.Leaf();
        var page = new TestAutomationPage(new Dictionary<string, TestLocator>
        {
            [QuestionSelector] = TestLocator.Collection(questionThree),
            [NavigationSelector] = TestLocator.Empty(),
            [NextPageSelector] = TestLocator.Collection(nextPageButton)
        });

        var result = await CreateService().FillAnswerAsync(
            page,
            "1",
            "A",
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(nextPageButton.Clicked);
        Assert.Contains("attempt.php", page.Url);
    }

    private static AnswerFillService CreateService(bool dryRun = false) =>
        new(
            Options.Create(new AnswerFillOptions
            {
                QuestionSelector = QuestionSelector,
                AlternativeSelector = AlternativeSelector,
                QuestionNavigationSelector = NavigationSelector,
                NextPageSelector = NextPageSelector,
                ClickTimeout = 1000,
                DryRun = dryRun
            }),
            NullLogger<AnswerFillService>.Instance);

    private sealed class TestAutomationPage(
        IReadOnlyDictionary<string, TestLocator> locators,
        Action<string>? onNavigate = null) : IAutomationPage
    {
        public string Url { get; private set; } =
            "https://www.avaeduc.com.br/mod/quiz/attempt.php?attempt=1";

        public IAutomationLocator GetLocator(string selector) =>
            locators.TryGetValue(selector, out var locator)
                ? locator
                : TestLocator.Empty();

        public IAutomationLocator GetFirstLocator(string selector) =>
            GetLocator(selector).Nth(0);

        public IAutomationLocator GetButtonByRole(string accessibleName) =>
            throw new NotSupportedException();

        public IAutomationLocator GetLinkByRole(string accessibleName) =>
            throw new NotSupportedException();

        public IAutomationLocator GetLinksByRole(string accessibleName) =>
            throw new NotSupportedException();

        public Task<IAutomationPage> OpenLinkInNewPageAsync(
            string accessibleName,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string> GetTitleAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Empty);

        public Task<string> GetContentAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Empty);

        public Task NavigateAsync(
            string url,
            CancellationToken cancellationToken = default)
        {
            Url = url;
            onNavigate?.Invoke(url);
            return Task.CompletedTask;
        }

        public Task GoBackAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WaitForUrlContainingAsync(
            string value,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> TryWaitForUrlContainingAsync(
            string value,
            TimeSpan timeout,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task WaitForUrlChangeAsync(
            string previousUrl,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WaitForDomContentLoadedAsync(
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WaitForNetworkIdleAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WaitForClientRenderAsync(
            TimeSpan duration,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task FillAsync(
            string selector,
            string value,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task PressAsync(
            string selector,
            string key,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task ClickAsync(
            string selector,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task WaitForVisibleAsync(
            string selector,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> IsVisibleAsync(
            string selector,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> IsEnabledAsync(
            string selector,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> IsDisabledAsync(
            string selector,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task ScreenshotAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WaitForLoadAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class TestLocator : IAutomationLocator
    {
        private readonly IReadOnlyList<TestLocator> _items;
        private readonly IReadOnlyDictionary<string, string?> _attributes;
        private readonly string _text;
        private readonly Action? _onClick;

        private TestLocator(
            IReadOnlyList<TestLocator>? items = null,
            IReadOnlyDictionary<string, string?>? attributes = null,
            string text = "",
            Action? onClick = null)
        {
            _items = items ?? [this];
            _attributes = attributes ?? new Dictionary<string, string?>();
            _text = text;
            _onClick = onClick;
        }

        public Dictionary<string, TestLocator> Children { get; } = [];

        public bool Clicked { get; private set; }

        public static TestLocator Empty() => new([]);

        public static TestLocator Leaf(
            IReadOnlyDictionary<string, string?>? attributes = null,
            string text = "",
            Action? onClick = null) =>
            new(attributes: attributes, text: text, onClick: onClick);

        public static TestLocator Collection(params TestLocator[] items) =>
            new(items);

        public IAutomationLocator Nth(int index) => _items[index];

        public IAutomationLocator GetLocator(string selector) =>
            Children.TryGetValue(selector, out var child)
                ? child
                : Empty();

        public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.Count);

        public Task<string> InnerTextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_text);

        public Task<string?> GetAttributeAsync(
            string attributeName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                _attributes.TryGetValue(attributeName, out var value)
                    ? value
                    : null);

        public Task HighlightAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ScrollIntoViewAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> IsVisibleAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> TryWaitForVisibleAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.Count > 0);

        public Task WaitForAttributeAsync(
            string attributeName,
            string expectedValue,
            TimeSpan timeout,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ClickAsync(CancellationToken cancellationToken = default)
        {
            Clicked = true;
            _onClick?.Invoke();
            return Task.CompletedTask;
        }

        public Task WaitForHiddenAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
