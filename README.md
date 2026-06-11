# YamanariNotes

YamanariNotes e um bloco de notas moderno desenvolvido em C# com WPF e .NET 8. O projeto foi criado como portfolio principal para demonstrar organizacao de codigo, persistencia local, boas praticas de Git e uma experiencia desktop completa.

## Objetivo

Construir um editor de texto simples de usar, mas completo o bastante para mostrar dominio de desenvolvimento desktop em C#, incluindo manipulacao de arquivos, atalhos, configuracoes persistentes, temas, estatisticas de texto e documentacao profissional.

## Tecnologias utilizadas

- C#
- .NET 8
- WPF
- Windows Forms FontDialog
- JSON local para configuracoes
- Git

## Funcionalidades

- Criar, abrir e salvar arquivos `.txt` e `.md`
- Salvar como
- Confirmacao ao sair com alteracoes nao salvas
- Recortar, copiar, colar, desfazer, refazer e selecionar tudo
- Localizar e substituir texto
- Barra de status com caracteres, palavras, linha, coluna, zoom e estado do arquivo
- Tema claro e tema escuro
- Fonte configuravel
- Tamanho de fonte configuravel
- Quebra automatica de linha
- Zoom aumentar, diminuir e restaurar
- Exibir ou ocultar barra de status
- Salvamento automatico opcional
- Lista de arquivos recentes
- Atalhos de teclado
- Tela Sobre com autor e tecnologias
- Preferencias salvas em JSON local

## Atalhos

- `Ctrl+N`: novo arquivo
- `Ctrl+O`: abrir arquivo
- `Ctrl+S`: salvar
- `Ctrl+Shift+S`: salvar como
- `Ctrl+F`: localizar
- `Ctrl+H`: substituir
- `Ctrl+0`: restaurar zoom

## Capturas de tela

As capturas de tela podem ser adicionadas futuramente nesta secao.

```text
docs/screenshots/main-window.png
docs/screenshots/dark-theme.png
```

## Como executar

```bash
dotnet run --project YamanariNotes/YamanariNotes.csproj
```

## Como compilar

```bash
dotnet build YamanariNotes.sln
```

## Repositorio GitHub

```bash
git remote add origin https://github.com/YamanariMatt/YamanariNotes.git
git branch -M main
git push -u origin main
```

## Estrutura de pastas

```text
YamanariNotes/
  Helpers/
  Models/
  Services/
  Views/
  App.xaml
  App.xaml.cs
  YamanariNotes.csproj
README.md
CHANGELOG.md
LICENSE
.gitignore
```

## Aprendizados demonstrados

- Organizacao de projeto WPF
- Separacao de responsabilidades entre Views, Services, Models e Helpers
- Persistencia de configuracoes em arquivo JSON
- Manipulacao segura de arquivos locais
- UX desktop com menus, atalhos e dialogs
- Controle de estado de arquivo salvo/nao salvo
- Commits pequenos e historico Git legivel

## Autor

Matheus Victor Moreira Yamanari

E-mail: matheusvictormy@gmail.com

## Licenca

Este projeto esta licenciado sob a licenca MIT. Consulte o arquivo `LICENSE`.
