# Resumo do ProgramaOT Launcher (AutoLauncher de OTServer)

Este projeto é um launcher/atualizador automático para clientes de OTServer (similar ao Tibia Global). Ele verifica se há nova versão do cliente, baixa o pacote do cliente (ZIP) a partir de uma URL configurada, aplica a atualização e inicia o executável do jogo.

Referência principal para preparação e uso: [Windows][VC2022][Solution] Compiling ProgramaOT Launcher Sources | OpenTibiaBR (https://docs.opentibiabr.com/opentibiabr/downloads/tools/launchers/canary-launcher/getting-started/windows/windows-vc2022-solution-compiling-canary-launcher-sources).

## Visão geral
- Plataforma: Windows
- Tecnologia: C# WPF
- Framework alvo (neste repositório): .NET Framework 4.8
- Pacotes: Newtonsoft.Json, Ionic.Zip
- Interface: SplashScreen (carregamento/verificação) e MainWindow (botão Play/Update, progresso)
- Configuração externa: `launcher_config.json` hospedado em repositório público (ex. GitHub Raw)

## Estrutura de pastas
- `src/`
  - `App.xaml` e `App.xaml.cs`: define inicialização pela `SplashScreen.xaml`.
  - `SplashScreen.xaml` e `SplashScreen.xaml.cs`: tela de abertura; verifica versão remota e decide iniciar direto o cliente ou abrir a janela principal.
  - `MainWindow.xaml` e `MainWindow.xaml.cs`: UI principal; verifica necessidade de download/atualização, baixa o ZIP, extrai, mostra progresso e inicia o cliente.
  - `ClientConfig.cs`: modelo da configuração lida do `launcher_config.json` remoto.
- `Assets/`: imagens e ícones usados pela UI.
- `launcher_config.json`: arquivo de exemplo local com campos suportados (a versão usada pelo launcher é buscada remotamente pela URL definida no código).

## Fluxo de execução
1. A aplicação inicia em `SplashScreen` (configurado em `App.xaml`).
2. `SplashScreen` baixa o `launcher_config.json` remoto (hardcoded) e lê:
   - `clientVersion`: versão remota do cliente.
   - `newClientUrl`: URL para baixar o ZIP do cliente.
   - `clientExecutable`: executável dentro de `bin/` (ex.: `client.exe`).
   - `clientFolder`: nome da pasta base onde o cliente será instalado (se vazio, instala na pasta base do launcher).
3. Se já existe `launcher_config.json` local e a versão local é igual à remota, com cliente instalado, o launcher inicia diretamente o executável do cliente e fecha.
4. Caso contrário, abre `MainWindow`:
   - Se versões diferem ou instalação está ausente, exibe botão “Update/Download”.
   - Ao clicar, baixa `tibia.zip` de `newClientUrl` para a pasta alvo e mostra progresso.
   - Se `replaceFolders = true`, apaga as pastas listadas em `replaceFolderName` (ex.: `assets`, `storeimages`, `bin`).
   - Extrai o ZIP sobre a pasta do cliente e apaga o arquivo `tibia.zip`.
   - Baixa uma cópia do `launcher_config.json` remoto para a pasta base (para que a versão local fique sincronizada).
   - Define alguns arquivos de cache como somente leitura, cria atalho na área de trabalho e atualiza a UI para “Play”.
5. No próximo clique em “Play” (ou quando versões já são iguais), executa `bin/<clientExecutable>` e fecha o launcher.

## Configuração via `launcher_config.json`
Campos suportados (conforme `src/ClientConfig.cs` e exemplo de `launcher_config.json`):
- `clientVersion` (string): versão do cliente no formato "major.minor.patch".
- `launcherVersion` (string): versão do launcher (apresentada na UI).
- `replaceFolders` (bool): se verdadeiro, remove as pastas especificadas antes da extração.
- `replaceFolderName` (array de objetos `{ name: "<pasta>" }`): lista de pastas a serem removidas quando `replaceFolders = true`.
- `clientFolder` (string): nome da pasta base onde o cliente será instalado. Deixe vazio para instalar na pasta do launcher.
- `newClientUrl` (string): URL para baixar o zip do cliente (ex.: arquivo anexado em uma Release do GitHub).
- `clientExecutable` (string): nome do executável dentro de `bin/` (ex.: `client.exe`).

Observações importantes:
- O código atualmente usa uma URL hardcoded para buscar o `launcher_config.json` remoto, tanto em `src/MainWindow.xaml.cs` quanto em `src/SplashScreen.xaml.cs`. Você deve alterar essa string para apontar para o seu JSON público (ex.: `https://raw.githubusercontent.com/<seu-usuario>/<seu-repo>/main/launcher_config.json`).
- O campo `newConfigUrl` existe no modelo (`ClientConfig.cs`), mas não é utilizado pelo código; a URL do config é definida diretamente nos arquivos citados acima.
- O método de leitura da versão local (`GetClientVersion`) retorna o primeiro valor do JSON; por convenção, mantenha `clientVersion` como primeira propriedade no arquivo para evitar inconsistencia.

## Preparação e build (VS 2022)
Passos resumidos, conforme a documentação oficial e este projeto:
1. Instale Visual Studio 2022 (Workload “Desktop Development with .NET”).
2. Instale o runtime compatível com este projeto. Observação: este repositório está configurado para .NET Framework 4.8 (`ProgramaOTLauncher.csproj`). Se desejar usar .NET 6.0 como na documentação, será necessário retargeting do projeto.
3. Abra a solução `ProgramaOTLauncher.sln` e selecione a configuração `Release`.
4. Antes de compilar, atualize a URL do `launcher_config.json` nos arquivos:
   - `src/MainWindow.xaml.cs`: variável `launcerConfigUrl`.
   - `src/SplashScreen.xaml.cs`: variável `launcerConfigUrl`.
5. Compile: Build > Build Solution.

## Empacotamento do cliente e distribuição
- Publique um ZIP do cliente contendo, no mínimo, as pastas necessárias (ex.: `assets`, `storeimages`, `bin`) e o executável em `bin/`.
- Hospede o ZIP em uma Release do seu repositório (GitHub Releases) e aponte `newClientUrl` para o link da Release.
- Ao lançar uma nova versão do cliente:
  - Atualize `clientVersion` no seu `launcher_config.json` remoto.
  - Se usar novo ZIP/Release, atualize também `newClientUrl`.
  - Na próxima execução, o launcher fará o download/atualização automaticamente.

## Dependências
- `Newtonsoft.Json` para desserialização do `launcher_config.json`.
- `Ionic.Zip` para leitura e extração do arquivo ZIP.
- `HttpClient`/`WebClient` para requisições HTTP e download de arquivos.

## Comportamentos adicionais
- Cria atalho na Área de Trabalho com o nome definido em `clientFolder`.
- Define como somente leitura alguns arquivos de cache se existirem (`cache/eventschedule.json`, `cache/boostedcreature.json`, `cache/onlinenumbers.json`).
- UI exibe progresso de download e troca dinamicamente o visual do botão entre “Update” e “Play”.

## Pontos de atenção e limitações
- URL do `launcher_config.json` é codificada no fonte. Para ambientes diferentes, mantenha uma branch/fork ou automatize a troca.
- O método que lê a versão local (`GetClientVersion`) depende da ordem dos campos no JSON.
- O caminho do cliente combina base do launcher com `clientFolder`. Se `clientFolder` estiver vazio, tudo será extraído na pasta do launcher.
- O uso simultâneo de `HttpClient` e `WebClient` é funcional, mas pode ser padronizado.

## Personalização visual
- Substitua imagens em `Assets/` para branding do seu servidor (logo, ícones, background).
- `App.xaml` contém estilos para botões e tooltips.

## Exemplo de `launcher_config.json`
```
{
  "clientVersion": "13.20.13560",
  "launcherVersion": "1.0",
  "replaceFolders": true,
  "replaceFolderName": [
    { "name": "assets" },
    { "name": "storeimages" },
    { "name": "bin" }
  ],
  "clientFolder": "Tibia",
  "newClientUrl": "https://github.com/LunarWaveSolutions/ProgramaOT-Cliente/releases/download/1.0.0/client-to-update.zip",
  "clientExecutable": "client.exe"
}
```

## Referências
- Documentação oficial: [Windows][VC2022][Solution] Compiling ProgramaOT Launcher Sources | OpenTibiaBR — guia de setup, publicação e atualização (https://docs.opentibiabr.com/opentibiabr/downloads/tools/launchers/canary-launcher/getting-started/windows/windows-vc2022-solution-compiling-canary-launcher-sources).

