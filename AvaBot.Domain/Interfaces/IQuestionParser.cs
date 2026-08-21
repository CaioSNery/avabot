using AvaBot.Domain.Models.Questions;

namespace AvaBot.Domain.Interfaces;

public interface IQuestionParser
{
    Quiz Parse(ExtractedQuizData extractedQuiz);
}
