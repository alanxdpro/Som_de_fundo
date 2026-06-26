# Som de Fundo Pro

Som de Fundo Pro e um aplicativo desktop em WPF/.NET 8 para organizar e tocar fundos musicais em botoes, com playlists, capas, controle remoto local e biblioteca online.

## Projeto

- Aplicativo principal: `SomDeFundoCSharp/SomDeFundoCSharp.csproj`
- Aplicativo administrador: `SomDeFundoCSharp/SomDeFundoCSharp.Admin.csproj`
- Instalador Inno Setup: `installer/SomDeFundoPro.iss`
- Site GitHub Pages: `docs/index.html`

## Requisitos

- Windows
- .NET SDK 8
- Inno Setup, somente para gerar instalador

## Executar em desenvolvimento

```powershell
dotnet restore SomDeFundoCSharp/SomDeFundoCSharp.csproj
dotnet run --project SomDeFundoCSharp/SomDeFundoCSharp.csproj
```

## Build

```powershell
dotnet build SomDeFundoCSharp/SomDeFundoCSharp.csproj
dotnet build SomDeFundoCSharp/SomDeFundoCSharp.Admin.csproj
```

## Publicar executaveis

```powershell
.\publish-apps.ps1
```

Saidas locais:

- `publish-user/Som de Fundo Pro.exe`
- `publish-admin/Som de Fundo Pro Admin.exe`

Essas pastas sao geradas localmente e nao entram no Git.

## Instalador

Depois de publicar o app de usuario, compile:

```text
installer/SomDeFundoPro.iss
```

O instalador gerado em `installer-output/` tambem nao entra no Git.

## Configuracoes locais

Arquivos `.env` e configuracoes locais nao sao versionados. Secrets e chaves internas devem ficar fora do repositorio.

## Site

O site estatico fica em `docs/` e pode ser publicado pelo GitHub Pages usando a branch `main` e a pasta `/docs`.
