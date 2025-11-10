# ProgramaOT-Launcher — Análise Técnica e Fluxo de Execução

Este documento resume como o projeto funciona de ponta a ponta: como inicia, qual sua finalidade, como atua durante a atualização do cliente e do próprio launcher, quais artefatos cria/precisa confirmar, e uma avaliação sobre logs/observabilidade. Será usado como base para melhorias futuras.

## 1) Visão geral

- Stack: C# WPF (.NET Framework 4.8) para a UI principal; utilitário auxiliar de atualização (UpdaterHelper) em .NET 9 para aplicação de updates do launcher.
- Propósito: ser um launcher do ProgramaOT que:
  - Baixa/atualiza o cliente quando necessário;
  - Verifica e aplica atualização do próprio launcher;
  - Executa o cliente após estar atualizado;
  - Exibe progresso e ações de “Download”, “Update” e “Play”.

## 2) Como a aplicação inicia

- Ponto de entrada: `App.xaml.cs::OnStartup`.
- Fluxo de inicialização:
  1. Exibe uma `SplashScreen` por ~1 segundo.
  2. Verifica argumentos de linha de comando:
     - `--apply-update` ou `--download-update` → abre `UpdateProgressWindow` (janela de progresso de atualização do launcher) e encerra a aplicação ao terminar.
     - Caso contrário → abre `MainWindow` (janela principal do launcher) e define `ShutdownMode=OnMainWindowClose`.

## 3) Configuração e carregamento

- Arquivos e classes:
  - `UpdateConfig.cs`: centraliza donos/repos e endpoints derivados para Releases e `launcher_config.json` remoto. Lê token do ambiente `PROGRAMAOT_GITHUB_TOKEN` (opcional) para acesso privado.
  - `launcher_config.json`: configurações principais do cliente e do launcher (versões, nome da pasta do cliente, nome do executável, endpoint de update do launcher, nome do asset e URL de checksum).
  - `ClientConfig.cs`: modelo de configuração e `loadFromFile(url)` que tenta baixar remoto e faz fallback para o `launcher_config.json` local.
- Onde é carregado:
  - `SplashScreen.xaml.cs` e `MainWindow.xaml.cs` carregam `ClientConfig.loadFromFile(UpdateConfig.RawLauncherConfigUrl)`. Observação: isso implica carregar a configuração duas vezes; pode ser simplificado.

## 4) Fluxo de atualização do Launcher

- Checagem: `AtualizaLauncher.CheckForUpdateAsync()` chama `LauncherUpdateService.CheckAsync(config, installedVersion)`.
  - Obtém `tag_name` da última release e o asset correspondente (via `launcherAssetName`) e preenche `AssetUrl` (download público) e `AssetApiUrl` (download via API de assets para repositório privado com `Accept: application/octet-stream`).
  - Define `HasUpdate` comparando `installedVersion` com a `LatestTag` (normaliza `v` e SemVer); marca `Mandatory` se `installedVersion < launcherMinVersion`.
- Disparo da atualização: `AtualizaLauncher.TriggerUpdateAsync()`
  - Solicita confirmação do usuário (se não obrigatória) e reinicia o processo atual com flags `--download-update --url=... --version=...`.
  - O `App` redireciona para `UpdateProgressWindow`.
- `UpdateProgressWindow` executa duas etapas:
  1. Download do zip de atualização do launcher (com suporte a `Authorization` quando há token), escreve progresso e log em `UpdateLauncher\update.log`. Extrai o zip para `UpdateLauncher\payload`.
  2. Inicia `UpdaterHelper.exe` com parâmetros para copiar arquivos do `payload` para o diretório do launcher e, ao final, tenta reiniciar o `ProgramaOT-Launcher.exe`. Encerra a aplicação atual.

Observações importantes:
- Mismatch de parâmetros do UpdaterHelper: `UpdateProgressWindow` chama `UpdaterHelper.exe` com flags nomeadas (`--source-dir`, `--target-dir`, `--pid`), mas `UpdaterHelper/Program.cs` espera 3 argumentos posicionais (`<sourceDir> <targetDir> <processId>`). Isso impede o helper de funcionar como esperado sem ajuste.
- `AssetApiUrl` não é repassado em `AtualizaLauncher.StartUpdateProcessAsync`. Em cenários privados, o ideal é passar também `--api-url="..."` para o `UpdateProgressWindow` usar o endpoint de assets com `Accept: application/octet-stream`.
- `launcherChecksumUrl` é carregado no `LauncherUpdateService` mas não há validação de checksum no fluxo de update.

## 5) Fluxo de atualização do Cliente

- Componente: `AtualizaCliente`。
- Passos:
  1. `CheckForUpdateAsync()`:
     - Lê a tag instalada do cliente em `Tibia\versions.json` (se existir) usando `installedTag`; exibe a versão mais recente no UI;
     - Se a pasta do cliente não existir ou estiver vazia → mostra botão “Download” e marca que precisa atualizar;
     - Caso exista, compara `latestReleaseTag` com `installedTag` (ou `clientVersion` do `launcher_config.json` local, como fallback) e decide entre “Play” ou “Update”.
  2. `TriggerUpdateAsync()` → chama `UpdateClientAsync()`.
  3. `UpdateClientAsync()`:
     - Cria diretório de instalação `PathHelper.GetLauncherPath(clientConfig)` (base + `clientFolder`, ex.: `Tibia`);
     - Baixa `client-to-update.zip` via:
       - Repositório privado: `UpdateConfig.ReleasesApiLatest` + token para encontrar o asset e usar `browser_download_url`;
       - Repositório público: `UpdateConfig.AssetClientZipLatestPublic`;
     - Exibe progresso via UI; após download, extrai usando `Ionic.Zip` e salva `installedTag` em `Tibia\versions.json`.
  4. Ao final da extração, atualiza UI para “Play”.
- Execução do cliente: botão “Play” chama `Process.Start(PathHelper.GetLauncherPath(...)/bin/<clientExecutable>)` e fecha a janela principal.

Observações:
- Campos de configuração `newClientUrl` e `newConfigUrl` existem em `ClientConfig`, mas não são utilizados no fluxo atual.
- Opções `replaceFolders`/`replaceFolderName` não são utilizadas no processo de extração/substituição.

## 6) O que o projeto cria no disco (artefatos)

- Durante o update do cliente:
  - `Tibia\tibia.zip` (arquivo baixado, removido após extração);
  - Conteúdo extraído do zip dentro de `Tibia\` (inclui `bin\client.exe` e demais pastas como `assets`, `storeimages`, `cache`, etc. conforme o pacote);
  - `Tibia\versions.json` com `installedTag` e `installedAt`.
- Durante o update do launcher:
  - `UpdateLauncher\update.log` (log do progresso usado pela `UpdateProgressWindow`);
  - `UpdateLauncher\payload\` (conteúdo extraído da atualização antes de aplicar);
  - `UpdateLauncher\update_helper_log.txt` (log do `UpdaterHelper.exe`);
  - `versions.json` na raiz (do launcher) com `installedLauncherTag` e `installedAt`.
- Na primeira execução da janela principal:
  - `Desktop\Tibia.lnk` (atalho que aponta para o launcher, não diretamente para o cliente).

## 7) O que precisa ser criado e confirmado

Checklist prático (após execução/atualização):
- Cliente
  - Pasta do cliente existe: `<BaseDirectory>\Tibia\` (ou nome definido em `clientFolder`).
  - `bin\<clientExecutable>` existe e é executável.
  - `Tibia\versions.json` contém `installedTag` coerente com a última release baixada.
  - Pastas esperadas (ex.: `assets`, `storeimages`, `cache`) foram extraídas corretamente.
- Launcher
  - `versions.json` na raiz tem `installedLauncherTag` atualizado quando há update.
  - `UpdateLauncher\update.log` e `UpdateLauncher\update_helper_log.txt` foram gerados durante atualização.
  - Após update do launcher, o `ProgramaOT-Launcher.exe` é reiniciado corretamente (depende do `UpdaterHelper.exe` funcionar com argumentos corretos).

## 8) Logs e observabilidade

Estado atual:
- Há logs de arquivo somente em dois pontos:
  - `UpdateProgressWindow`: `UpdateLauncher\update.log` (download/extrair e handoff para UpdaterHelper).
  - `UpdaterHelper`: `UpdateLauncher\update_helper_log.txt` (aguardo do processo, cópia de arquivos, limpeza, tentativa de restart).
- Demais componentes (cliente e serviço de update) usam `MessageBox` e silenciam exceções em vários lugares, sem trilha persistente.

Recomendações:
- Introduzir um logger central (ex.: Serilog ou NLog) com sink de arquivo (ex.: `logs\launcher.log`) e contexto:
  - OnStartup (args, caminho base, versão);
  - Carregamento de configuração (origem remoto/local, sucesso/falha);
  - Checagem de update (launcher e cliente) com detalhes de versão/tags;
  - Download (URL, tamanho, progresso em etapas, sucesso/falha);
  - Extração (arquivo, destino, resultado);
  - Salvamento de `versions.json` (conteúdo mínimo: tag, timestamp);
  - Execução do cliente (caminho, resultado);
  - Chamadas a `UpdaterHelper` (parâmetros efetivos).
- Evitar `catch {}` sem log; sempre registrar exceções com stacktrace.
- Se `launcherChecksumUrl` existir, validar checksum antes de aplicar atualização (integridade/segurança).

## 9) Lacunas e riscos identificados

- UpdaterHelper não recebe os argumentos no formato esperado (posicionais). Resultado: pode falhar com “Argumentos insuficientes”.
- `AssetApiUrl` não é propagado para `UpdateProgressWindow`; em repositórios privados a atualização pode falhar.
- Sem validação de checksum para o update do launcher (existe `launcherChecksumUrl`, mas não é utilizado).
- Campos de configuração `newClientUrl`, `newConfigUrl`, `replaceFolders`/`replaceFolderName` não são utilizados.
- Duplicidade no carregamento de configuração (Splash e MainWindow); pode ser centralizado.
- Exceções frequentemente silenciadas; dificultam troubleshooting.
- Uso de barras “/” em algumas combinações de caminho (preferir `Path.Combine` por portabilidade e robustez).

## 10) Projeção (roadmap de melhorias)

1. Corrigir parâmetros do `UpdaterHelper` e propagar `AssetApiUrl` quando houver token.
2. Implementar validação de checksum/SHA256 do pacote do launcher antes da extração/aplicação.
3. Introduzir logging central com níveis (Info/Warning/Error) e correlação por operação (update cliente, update launcher).
4. Unificar carregamento de configuração e padronizar o uso dos campos do `launcher_config.json` (usar `newClientUrl` e `newConfigUrl` quando aplicável).
5. Tratar robustamente exceções (sem `catch {}`) e expor mensagens amigáveis ao usuário + log detalhado ao arquivo.
6. Avaliar assinatura digital dos binários e verificação de integridade do zip.
7. Opcional: implementar canal de telemetria/diagnósticos (opt-in) para entender taxa de sucesso dos updates.

## 11) Principais arquivos e responsabilidades (resumo)

- `App.xaml.cs`: define o fluxo inicial (Splash → MainWindow ou UpdateProgressWindow conforme args).
- `MainWindow.xaml/.cs`: UI principal; integra `AtualizaLauncher` (update do launcher) e `AtualizaCliente` (update do cliente); cria atalho; define atributos de somente leitura para alguns arquivos de cache.
- `SplashScreen.xaml/.cs`: tela inicial breve; também carrega configuração (pode ser enxugada em favor de um ponto único de carga).
- `AtualizaLauncher.cs`: orquestra a checagem e disparo da atualização do launcher; reabre o processo com flags de update.
- `LauncherUpdateService.cs`: obtém informações da última release e mapeia asset/URLs; define `HasUpdate` e `Mandatory`.
- `UpdateProgressWindow.xaml/.cs`: baixa/extrai a atualização e repassa para `UpdaterHelper.exe` aplicá-la.
- `UpdaterHelper/Program.cs`: aguarda o processo principal encerrar, copia arquivos do `payload` para o diretório do launcher, limpa e tenta reiniciar.
- `AtualizaCliente.cs`: checa a necessidade de atualização, baixa/mostra progresso e extrai o cliente; salva `installedTag` do cliente.
- `PathHelper.cs`: resolve caminho de instalação do cliente (base + `clientFolder`).
- `ClientConfig.cs` e `UpdateConfig.cs`: modelos/configuração e endpoints para releases.
- `launcher_config.json`: arquivo de configuração distribuído junto ao executável do launcher.

---

Este resumo pode ser expandido conforme novas necessidades (ex.: diagramas, sequência de chamadas, métricas de desempenho). A seção de “Projeção” serve como plano inicial para melhorias estruturais e de observabilidade.

