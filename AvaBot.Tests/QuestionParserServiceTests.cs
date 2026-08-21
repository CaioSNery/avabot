using AvaBot.Application.Services;
using AvaBot.Domain.Models.Questions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AvaBot.Tests;

public sealed class QuestionParserServiceTests
{
    [Fact]
    public void Parse_ConvertsExtractedDataIntoPureTypedModels()
    {
        var extracted = new ExtractedQuizData(
            "  Quiz   de Teste ",
            [
                new ExtractedQuestionData(
                    "Questão 7",
                    "  Qual   alternativa? ",
                    [
                        new ExtractedAlternativeData("", " Primeira "),
                        new ExtractedAlternativeData("B", " Segunda ")
                    ],
                    [new ExtractedImageData(" Figura ", "image.png")],
                    [new ExtractedTableData([" Coluna "], [[" Valor "]])],
                    [new ExtractedCodeBlockData("csharp", " var x = 1; ")],
                    ["  a_n = a_(n-1) + 2n  "],
                    "data:image/png;base64,AQID")
            ]);
        var parser = new QuestionParserService(
            NullLogger<QuestionParserService>.Instance);

        var quiz = parser.Parse(extracted);

        Assert.Equal("Quiz de Teste", quiz.Title);
        Assert.Equal(7, quiz.Questions[0].Number);
        Assert.Equal("Qual alternativa?", quiz.Questions[0].Statement);
        Assert.Equal("A", quiz.Questions[0].Alternatives[0].Id);
        Assert.Equal("Figura", quiz.Questions[0].Images[0].Description);
        Assert.Equal("Valor", quiz.Questions[0].Tables[0].Rows[0][0]);
        Assert.Equal("var x = 1;", quiz.Questions[0].CodeBlocks[0].Content);
        Assert.Equal("a_n = a_(n-1) + 2n", quiz.Questions[0].Formulas![0]);
        Assert.Equal(
            "data:image/png;base64,AQID",
            quiz.Questions[0].ScreenshotDataUrl);
    }
}
