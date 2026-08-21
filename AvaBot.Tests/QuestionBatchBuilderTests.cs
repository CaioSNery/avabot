using AvaBot.Application.Services;
using AvaBot.Domain.Exceptions;
using AvaBot.Domain.Models;

namespace AvaBot.Tests;

public sealed class QuestionBatchBuilderTests
{
    private readonly QuestionBatchBuilder _builder = new();

    [Fact]
    public void Build_RemovesEquivalentDuplicatesAndOrdersByNumber()
    {
        var questionTwo = TestQuestionFactory.Create(2);
        var questionOne = TestQuestionFactory.Create(1);

        var result = _builder.Build(
            new InferenceRequestDto([questionTwo, questionOne, questionTwo]));

        Assert.Equal([1, 2], result.Questions.Select(question => question.QuestionNumber));
    }

    [Fact]
    public void Build_RejectsConflictingQuestionsWithSameNumber()
    {
        var questions = new[]
        {
            TestQuestionFactory.Create(1, "Primeiro enunciado"),
            TestQuestionFactory.Create(1, "Outro enunciado")
        };

        Assert.Throws<InferenceValidationException>(() =>
            _builder.Build(new InferenceRequestDto(questions)));
    }
}
