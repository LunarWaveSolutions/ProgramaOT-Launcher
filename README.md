# ProgramaOT-Launcher

Launcher do ProgramaOT (C# WPF, .NET Framework 4.8) responsável por:
- Baixar/atualizar o cliente quando necessário
- Verificar versão de release
- Executar o cliente

Este documento explica como o projeto funciona, como realizar build local, como alterar configurações, como gerar o instalador com InstallSimple PRO e como trocar o ícone do instalador.

## Alterações recentes (renomeação e ajustes)

Foram realizados ajustes para padronizar o nome do projeto e do executável:
- AssemblyName alterado para `ProgramaOT-Launcher` no `ProgramaOTLauncher.csproj` (gera `ProgramaOT-Launcher.exe`).
- Namespaces atualizados para `ProgramaOTLauncher` (ex.: `Assets/icons.xaml` e `src/ClientConfig.cs`).
- `launcher_config.json` agora é copiado automaticamente para o diretório de saída (Item `None` com `CopyToOutputDirectory=PreserveNewest` no `.csproj`).
- Template do InstallSimple (`Instalador/InstallSimple 3.5/template-ProgramaOT.ispro`) ajustado para:
  - `SourceFolder` apontando para `bin\Release`
  - Atalho principal apontando para `ProgramaOT-Launcher.exe`
  - `NetFramework=4.8`
- Removido fallback para `CANARY_GITHUB_TOKEN` em `UpdateConfig.cs`. Agora utiliza somente `PROGRAMAOT_GITHUB_TOKEN` (se definido).

## Estrutura de arquivos relevante

- `ProgramaOTLauncher.csproj`: arquivo do projeto WPF (.NET Framework 4.8)
  - `ApplicationIcon=icon.ico` (ícone do executável do launcher)
  - `AssemblyName=ProgramaOT-Launcher`
  - Item `None Include="launcher_config.json"` com `CopyToOutputDirectory=PreserveNewest`
- `src/`:
  - `App.xaml`, `MainWindow.xaml`, `SplashScreen.xaml` e seus `.cs`
  - `ClientConfig.cs`: configurações do cliente e User-Agent (`programaot-launcher`)
  - `UpdateConfig.cs`: busca token no ambiente (`PROGRAMAOT_GITHUB_TOKEN`)
- `assets/` e `Assets/`: imagens e recursos visuais (inclui `icons.xaml`)
- `launcher_config.json`: configurações do launcher (copiado para `bin/Debug` e `bin/Release`)
- `Instalador/InstallSimple 3.5/template-ProgramaOT.ispro`: template do InstallSimple PRO

## Configuração: `launcher_config.json`

O arquivo `launcher_config.json` define as URLs e opções necessárias para o launcher funcionar corretamente. Ele é copiado para o diretório de build automaticamente.

Coloque o arquivo na raiz do projeto (já presente) e edite conforme seu ambiente. Certifique-se de que no momento da execução o arquivo esteja ao lado do `ProgramaOT-Launcher.exe`.

## Build local

### Usando Visual Studio
1. Abrir a solução `ProgramaOTLauncher.sln`
2. Selecionar configuração `Release` (ou `Debug` para testes)
3. Build/Rebuild Solution
4. Saída esperada:
   - `bin\Release\ProgramaOT-Launcher.exe` (ou `bin\Debug\ProgramaOT-Launcher.exe`)
   - `launcher_config.json` presente no mesmo diretório (copiado automaticamente)

### Usando MSBuild (PowerShell)
1. Abrir um terminal do Developer Command Prompt ou garantir `msbuild` no PATH
2. Executar:
   - `msbuild ProgramaOTLauncher.csproj /t:Build /p:Configuration=Release`
3. Verificar saída em `bin\Release`

## Executando o launcher

Após o build, execute `bin\Release\ProgramaOT-Launcher.exe`.
Certifique-se de que `launcher_config.json` está presente no mesmo diretório.

## Gerar o instalador com InstallSimple PRO

Há um template em `Instalador/InstallSimple 3.5/template-ProgramaOT.ispro`. Passos gerais:

1. Abrir o InstallSimple PRO
2. Carregar/criar projeto com base no template `template-ProgramaOT.ispro`
3. Conferir seções principais:
   - `[Setup]`: `WindowTitle` e `ProductName`
   - `[Graphics]`: `SplashScreen` (BMP), `Header`, `WizardBitmap` conforme desejar
   - `[SetupFiles]`:
     - `SourceFolder=J:\Projeto\Ot\ProgramaOT-Launcher\bin\Release`
     - `SpecialFolder=Application Data`
     - `SetupPath=ProgramaOT`
     - `SetupFile=J:\Projeto\Ot\ProgramaOT-Launcher\Instalador\InstallSimple 3.5`
   - `[Shortcuts]`:
     - `ProgramaOT-Launcher.exe=` (atalho para o executável principal)
   - `[Requirements]`:
     - `NetFramework=4.8`
     - `AdminRights=1` se desejar solicitar privilégio de administrador
4. Gerar o instalador (EXE). O InstallSimple criará
   `ProgramaOT-Launcher.exe` na pasta do `SetupFile` (instalador do cliente, não confundir com o launcher da aplicação).

### Trocar o ícone do instalador (EXE do InstallSimple)

O InstallSimple usa um ícone padrão para o instalador. Para trocar:

Opção A — rcedit (recomendado)
1. Baixar `rcedit-x64.exe` (ferramenta do projeto Electron) e colocar, por exemplo, em `C:\tools\rcedit-x64.exe`
2. Executar o comando (PowerShell):
   - `$exe = "j:\Projeto\Ot\ProgramaOT-Launcher\Instalador\InstallSimple 3.5\ProgramaOT-Launcher.exe"`
   - `$ico = "j:\Projeto\Ot\ProgramaOT-Launcher\icon.ico"`
   - `$rcedit = "C:\tools\rcedit-x64.exe"`
   - `& $rcedit $exe --set-icon $ico`

Opção B — Resource Hacker
1. Instalar `ResHacker.exe` (ex.: `C:\tools\ResHacker.exe`)
2. Executar o comando (PowerShell):
   - `$exe = "j:\Projeto\Ot\ProgramaOT-Launcher\Instalador\InstallSimple 3.5\ProgramaOT-Launcher.exe"`
   - `$ico = "j:\Projeto\Ot\ProgramaOT-Launcher\icon.ico"`
   - `$reshacker = "C:\tools\ResHacker.exe"`
   - `& $reshacker -open $exe -save $exe -action addoverwrite -res $ico -mask ICONGROUP,MAINICON,`

Recomenda-se usar um `.ico` com múltiplos tamanhos: 16, 32, 48, 64, 256 px.

## Trocar o ícone do launcher (aplicação WPF)

O projeto já define `ApplicationIcon=icon.ico` no `.csproj`. Para trocar o ícone do executável da aplicação:
1. Substituir o arquivo `icon.ico` na raiz do projeto pelo novo ícone
2. Rebuild (Release) e verificar o executável em `bin\Release`

## Comandos úteis (exemplos em PowerShell)

- Build Release via MSBuild:
  - `msbuild ProgramaOTLauncher.csproj /t:Build /p:Configuration=Release`
- Trocar ícone do instalador via rcedit:
  - `& C:\tools\rcedit-x64.exe "j:\Projeto\Ot\ProgramaOT-Launcher\Instalador\InstallSimple 3.5\ProgramaOT-Launcher.exe" --set-icon "j:\Projeto\Ot\ProgramaOT-Launcher\icon.ico"`

## Observações

- O arquivo `launcher_config.json` NÃO é compilado no executável; ele deve estar ao lado do `.exe`. O `.csproj` já copia automaticamente.
- O `UpdateConfig.cs` pode utilizar `PROGRAMAOT_GITHUB_TOKEN` do ambiente, se necessário.
- O executável do instalador gerado pelo InstallSimple tem o mesmo nome do launcher por padrão (`ProgramaOT-Launcher.exe`). Você pode renomear o instalador para algo como `ProgramaOT-Installer.exe` para evitar confusão.

## Troubleshooting

- Ícone não muda no instalador: verifique o caminho do `.ico` e se o comando (rcedit/Resource Hacker) foi executado após gerar o instalador.
- Ao abrir o launcher, ocorre erro de configuração: confira se `launcher_config.json` está presente e com os campos corretos.
- Exige .NET Framework: certifique-se de ter o .NET Framework 4.8 instalado.

---

Dúvidas ou melhorias: abra uma issue ou entre em contato.

