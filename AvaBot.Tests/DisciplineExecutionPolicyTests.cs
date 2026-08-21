using AvaBot.Application.Services;

namespace AvaBot.Tests;

public sealed class DisciplineExecutionPolicyTests
{
    private readonly DisciplineExecutionPolicy _policy = new();

    [Theory]
    [InlineData("Projeto de Extensão")]
    [InlineData("PROJETO DE EXTENSAO")]
    [InlineData("  Projeto   de   Extensão  ")]
    public void ShouldSkipWithoutOpening_RecognizesProjectExtension(string name)
    {
        Assert.True(_policy.ShouldSkipWithoutOpening(name));
    }

    [Theory]
    [InlineData("Processo Seletivo")]
    [InlineData("Fisioterapia Neurofuncional")]
    [InlineData("Pensamento Crítico")]
    public void ShouldSkipWithoutOpening_AllowsSupportedDisciplines(string name)
    {
        Assert.False(_policy.ShouldSkipWithoutOpening(name));
    }

    [Theory]
    [InlineData("Certificado")]
    [InlineData(" CERTIFICADO ")]
    public void IsExcludedLinearTopic_ExcludesOnlyCertificate(string name)
    {
        Assert.True(_policy.IsExcludedLinearTopic(name));
    }

    [Fact]
    public void IsExcludedLinearTopic_DoesNotExcludeProcessSelectionTopics()
    {
        Assert.False(_policy.IsExcludedLinearTopic("Seleção: Sucesso e Propósito!"));
    }

    [Theory]
    [InlineData("Orientações")]
    [InlineData(" ORIENTACOES ")]
    [InlineData("Genética")]
    [InlineData("Questões de Fixação")]
    [InlineData("Atividade Avaliativa")]
    [InlineData("Boas Vindas")]
    public void AllowsLocalCompletionForLinearTopic_AllowsVerifiedRequiredTopics(
        string topicName)
    {
        Assert.True(_policy.AllowsLocalCompletionForLinearTopic(topicName));
    }

    [Fact]
    public void AllowsLocalCompletionForLinearTopic_RejectsCertificate()
    {
        Assert.False(_policy.AllowsLocalCompletionForLinearTopic("Certificado"));
    }

    [Theory]
    [InlineData("Pensamento Científico", "Unidade de Ensino 1")]
    [InlineData("PENSAMENTO CIENTIFICO", "UNIDADE DE ENSINO 4")]
    public void RequiresLiveCompletionForLinearTopic_RevalidatesScientificThinkingUnits(
        string disciplineName,
        string topicName)
    {
        Assert.True(_policy.RequiresLiveCompletionForLinearTopic(
            disciplineName,
            topicName));
    }

    [Theory]
    [InlineData("Pensamento Científico", "Atividade Discursiva")]
    [InlineData("Ciências Biológicas", "Unidade de Ensino 1")]
    [InlineData("Competências para a Vida", "Boas Vindas")]
    public void RequiresLiveCompletionForLinearTopic_PreservesOtherFlows(
        string disciplineName,
        string topicName)
    {
        Assert.False(_policy.RequiresLiveCompletionForLinearTopic(
            disciplineName,
            topicName));
    }
}
