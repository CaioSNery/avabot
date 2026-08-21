# AvaBot

Automação local e de uso pessoal do Portal do Aluno e AVA Educ com .NET 8, Microsoft Playwright, Dependency Injection e Serilog.

## Configuração

Edite somente `AvaBot.Console/appsettings.json`:

```json
{
  "Portal": {
    "Url": "https://endereco-do-portal",
    "Login": "seu-login",
    "Senha": "sua-senha"
  },
  "Ava": {
    "Login": "",
    "Senha": ""
  },
  "OpenAI": {
    "ApiKey": "sua-chave",
    "Model": "gpt-5"
  },
  "Browser": {
    "Headless": false,
    "Timeout": 30000
  }
}
```

Quando `Ava:Login` ou `Ava:Senha` estiver vazio, o AvaBot reutiliza individualmente o valor correspondente configurado em `Portal`. Preencha a seção `Ava` somente quando o AVA utilizar credenciais diferentes.

As demais propriedades do arquivo configuram o logging estruturado do Serilog.

## Preparação

```powershell
dotnet build AvaBot.sln
pwsh AvaBot.Console/bin/Debug/net8.0/playwright.ps1 install chromium
```

## Execução

```powershell
dotnet run --project AvaBot.Console
```

Quando `Browser:Headless` for `false`, o navegador permanece visível. Enquanto o fluxo de exploração ainda está em desenvolvimento, pressione `Ctrl+C` para encerrar e liberar o Playwright de maneira controlada.

O AvaBot lê exclusivamente o `appsettings.json`. Ele não utiliza variáveis de ambiente, User Secrets ou serviços externos de configuração.
