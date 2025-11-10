# Guia de correção do erro de build no GitHub Actions (RuntimeIdentifier "win" ausente)

Este documento registra a análise do erro atual de build e as tentativas/correções recomendadas, para evitar mudanças precipitadas ("não fazer cagada").

## Sintomas observados

Durante o passo de build com MSBuild no Windows, o pipeline falha com:

```
Microsoft.NuGet.targets(198,5): error : Your project file doesn't list 'win' as a "RuntimeIdentifier". You should add 'win' to the "RuntimeIdentifiers" property in your project file and then re-run NuGet restore. [ProgramaOTLauncher.csproj]
```

Observações do log:
- O erro ocorre no projeto ProgramaOTLauncher.csproj (WPF .NET Framework 4.8).
- O Updater compila normalmente.
- O pipeline está restaurando pacotes com `dotnet restore` e depois usando `msbuild` para compilar.

## Diagnóstico (causa raiz)

O projeto `ProgramaOTLauncher.csproj` usa `PackageReference` (Newtonsoft.Json, Ionic.Zip) em um projeto .NET Framework no formato clássico (não SDK-style). Em alguns cenários, quando qualquer pacote fornece assets específicos de runtime (pasta `runtimes/`), o alvo `ResolveNuGetPackageAssets` exige que um Runtime Identifier (RID) seja definido.

No pipeline atual, a restauração é feita com `dotnet restore`, que não é o caminho mais adequado para projetos .NET Framework no formato clássico com `PackageReference`. Com isso, a resolução de assets de NuGet acaba sem RID explícito e o MSBuild acusa a ausência de `RuntimeIdentifier`.

## Estratégias de correção seguras

Existem duas correções complementares e de baixo risco. Recomenda-se aplicar ambas:

1. Ajuste no projeto (csproj)
   - Adicionar o RID `win` ao arquivo `ProgramaOTLauncher.csproj`.
   - Exemplo de trecho a inserir em um `<PropertyGroup>` (de preferência no primeiro, junto de `TargetFrameworkVersion`):
     ```xml
     <RuntimeIdentifiers>win</RuntimeIdentifiers>
     ```
   - Observação: Se houver necessidade de amarrar arquitetura, usar `win-x86` ou `win-x64`. Como o projeto está em `AnyCPU`, `win` é suficiente para a maioria dos pacotes puramente gerenciados.

2. Ajuste no workflow do Windows
   - Substituir o passo de restauração de `dotnet restore` por `msbuild -t:Restore` e garantir que o RID seja passado na restauração e na compilação.
   - Exemplos de comandos:
     ```powershell
     msbuild $env:SOLUTION_PATH /t:Restore /p:RuntimeIdentifier=win
     msbuild $env:SOLUTION_PATH /t:Build /p:Configuration=Release /p:RuntimeIdentifier=win
     ```
   - Motivo: Para projetos .NET Framework com `PackageReference`, a restauração via MSBuild integra-se às targets do NuGet de forma mais confiável do que `dotnet restore`.

## Passo-a-passo recomendado

1) Projeto
   - Editar `ProgramaOTLauncher.csproj` e incluir `<RuntimeIdentifiers>win</RuntimeIdentifiers>` no `PropertyGroup` principal.
   - Não alterar outras propriedades por enquanto (minimizando impacto).

2) Workflow
   - Em `.github/workflows/build-windows.yml`, trocar:
     - `Restore NuGet packages` → de `dotnet restore` para `msbuild /t:Restore`.
     - Adicionar `/p:RuntimeIdentifier=win` tanto no `Restore` quanto no `Build`.

3) Reexecutar o Actions
   - Após commit das mudanças acima, executar o workflow. Espera-se que:
     - O `Updater` continue compilando normalmente.
     - O `ProgramaOTLauncher` restaure e compile sem o erro de RuntimeIdentifier.

## Alternativas e considerações

- Definir arquitetura explícita:
  - Se desejarmos build 32-bit, podemos setar `PlatformTarget` como `x86` e usar RID `win-x86` (e alinhar a solução para `Release|x86`).
  - Para 64-bit, usar `x64` + `win-x64`.
  - Caso contrário, manter `AnyCPU` com `win`.

- Não usar `dotnet publish` para este projeto .NET Framework WPF:
  - Isso pode invocar targets de ClickOnce e levar a erros como `UpdateManifest` (MSB4803) no ambiente de MSBuild .NET Core.
  - O caminho estável é usar `msbuild` para Build e empacotar os binários (zip/installer) separadamente.

- Converter para SDK-style (opcional, mais trabalhoso):
  - Migrar o projeto para SDK-style pode simplificar integração com `dotnet restore/build`. Porém, é uma mudança maior e não é necessária para resolver o erro atual.

- Voltar para `packages.config` (não recomendado):
  - É uma alternativa possível, mas regride o gerenciamento de pacotes e não resolve o problema raiz quando há assets de runtime.

## Checklist para não fazer cagada

- [ ] Criar branch para a correção (ex.: `fix/runtimeid-win`) e abrir PR.
- [ ] Alterar somente:
  - [ ] Inserir `<RuntimeIdentifiers>win</RuntimeIdentifiers>` no `ProgramaOTLauncher.csproj`.
  - [ ] Trocar `dotnet restore` por `msbuild /t:Restore` no workflow do Windows.
  - [ ] Passar `/p:RuntimeIdentifier=win` no Restore e no Build do workflow.
- [ ] Executar o Actions no PR; validar que o erro sumiu.
- [ ] Mesclar o PR apenas após CI verde.

## Anexos

Trecho do erro coletado em `.github/erro build.md`:

```
C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Microsoft\NuGet\17.0\Microsoft.NuGet.targets(198,5): error : Your project file doesn't list 'win' as a "RuntimeIdentifier". You should add 'win' to the "RuntimeIdentifiers" property in your project file and then re-run NuGet restore. [D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\ProgramaOTLauncher.csproj]
```

Se precisar, podemos automatizar a parametrização (x86/x64) no pipeline conforme a matriz de builds.

