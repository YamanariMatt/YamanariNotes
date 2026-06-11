# Contributing

Obrigado por considerar contribuir com o YamanariNotes.

## Ambiente recomendado

- Windows 10 ou superior
- .NET SDK 8 ou superior
- Visual Studio 2022 ou Visual Studio Code

## Como validar mudancas

```bash
dotnet restore YamanariNotes.sln
dotnet build YamanariNotes.sln
```

## Padrao de commits

Use mensagens curtas e descritivas, preferencialmente no formato:

```text
feat: add new editor action
fix: correct settings persistence
docs: update README
chore: adjust project metadata
ci: update build workflow
```

## Checklist antes de abrir PR

- O projeto compila localmente.
- A mudanca tem escopo claro.
- O README ou CHANGELOG foi atualizado quando necessario.
- Arquivos temporarios nao foram adicionados.
