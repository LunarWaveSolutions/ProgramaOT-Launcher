# Plano de Autoatualização do ProgramaOT Launcher (cadeia: Launcher → Client)

Este documento descreve como implementar a autoatualização do ProgramaOT Launcher antes de verificar e atualizar o cliente do jogo. A ordem é obrigatória: primeiro garantir que o Launcher está atualizado; somente depois verificar/atualizar o Client.

## Objetivo

- Ao abrir o Launcher, ele verifica se há uma versão mais recente dele mesmo.
- Se houver atualização do Launcher:
  - Exibir a mensagem: "Launcher desatualizado, precisa atualizar para atualizar o client." e o botão "Atualizar Launcher".
  - Após atualizar o Launcher, relançar o aplicativo automaticamente.
- Com o Launcher já atualizado, verificar a versão do Client como já ocorre hoje:
  - Se o Client estiver desatualizado, mostrar botão de "Atualizar" e executar o fluxo existente.
  - Se estiver atualizado, habilitar o botão "Play" imediatamente.

## Fonte de versão e distribuição

Propomos utilizar GitHub Releases do repositório:
https://github.com/LunarWaveSolutions/ProgramaOT-Launcher

- Versão do Launcher: semântica (ex.: 1.0.0) lida do Assembly (AssemblyInformationalVersion) ou `AssemblyVersion`.
- Última versão disponível: consultar o endpoint de Releases do GitHub:
  - `GET https://api.github.com/repos/LunarWaveSolutions/ProgramaOT-Launcher/releases/latest`
  - Extrair `tag_name` (ex.: `v1.1.0`) e os assets anexados (ex.: `ProgramaOT-Launcher.zip`).
- Distribuição recomendada:
  - Anexar aos Releases um ZIP contendo os binários do Launcher (todos os arquivos necessários do diretório de instalação, não apenas o `.exe`).
  - Opcional: anexar um arquivo `.sha256` para validação de integridade.

Alternativas:
- Se não houver Releases, usar Tags ou consultar a versão em um arquivo JSON hospedado (ex.: `https://raw.githubusercontent.com/.../latest.json`).

## Fluxo de verificação (startup)

1. Recuperar a versão atual do Launcher em execução (ex.: `Assembly.GetEntryAssembly()` → `InformationalVersion`).
2. Consultar o GitHub para obter a última versão publicada e o asset (ZIP) a baixar.
3. Comparar versões (normalizar `vX.Y.Z` → `X.Y.Z`).
4. Se a versão remota for maior:
   - Bloquear atualização do Client.
   - Exibir mensagem "Launcher desatualizado, precisa atualizar para atualizar o client." e o botão "Atualizar Launcher".
5. Se a versão remota for igual/menor:
   - Prosseguir com o fluxo atual de verificação/atualização do Client.

## Atualização segura do Launcher (Windows)

Windows não permite substituir o executável enquanto ele está em execução. Por isso, o processo deve ocorrer em duas etapas:

1) Download e preparação
- Baixar o asset do Release (ZIP) para `{tmp}`.
- Validar checksum, se disponível (SHA-256).
- Extrair o ZIP para uma pasta temporária, ex.: `{tmp}\LauncherUpdate\`.

2) Substituição usando um helper (Updater)
- Incluir um utilitário leve `Updater.exe` (console app) no projeto.
- Fluxo:
  - O Launcher chama `Updater.exe` com parâmetros:
    - `--install-dir` (ex.: `%AppData%\ProgramaOT`)
    - `--update-dir` (ex.: `{tmp}\LauncherUpdate`)
    - `--restart` (ex.: `%AppData%\ProgramaOT\ProgramaOT-Launcher.exe`)
    - `--pid` do processo do Launcher (opcional, para aguardar término com precisão)
  - O Launcher encerra.
  - O `Updater.exe` aguarda o término do processo do Launcher.
  - Copia/substitui arquivos de `update-dir` para `install-dir` (incluindo o `.exe`).
  - Inicia o novo Launcher (`--restart`) e encerra.

Notas técnicas:
- Usar `Process.WaitForExit()` com o PID passado para garantir que o `.exe` não está mais bloqueado.
- Copy/replace com tolerância a falhas (tentar novamente em caso de locks residuais).
- Em caso de erro, manter backup do executável anterior para rollback (opcional) ou exibir mensagem com link para download manual.

## UX (WPF)

- Ao iniciar:
  - Exibir status do Launcher (Atualizado/Desatualizado).
  - Se desatualizado:
    - Mostrar botão "Atualizar Launcher".
    - Desabilitar botão de "Atualizar Client" e "Play".
    - Barra de progresso durante download/extração/substituição.
  - Após atualizar e relançar:
    - Executar verificação do Client como hoje.

Mensagens:
- "Launcher desatualizado, precisa atualizar para atualizar o client."
- Em caso de erro de rede: "Não foi possível verificar a versão do Launcher. Tente novamente ou baixe manualmente a versão mais recente."

## Configurações novas (launcher_config.json)

Adicionar chaves para parametrizar a origem da atualização do Launcher:
- `launcherUpdateEndpoint`: URL do endpoint (padrão: releases/latest do GitHub).
- `launcherAssetName`: nome do arquivo asset para download (ex.: `ProgramaOT-Launcher.zip`).
- `launcherChecksumUrl` (opcional): URL de um `.sha256` para validação.
- `launcherMinVersion` (opcional): versão mínima obrigatória (forçar atualização crítica).

## Integração com GitHub Actions

- Workflow de build (Windows) gera artefato ZIP do Launcher.
- Workflow de release publica `ProgramaOT-Launcher.zip` + (opcional) `ProgramaOT-Launcher.zip.sha256`.
- Tag `vX.Y.Z` sincronizada com `AssemblyInformationalVersion`.

## Esqueleto de implementação (C#)

1) Serviço de verificação de versão (LauncherVersionService)
```csharp
public class LauncherVersionInfo {
    public Version Current { get; set; }
    public Version Remote { get; set; }
    public Uri AssetUrl { get; set; }
}

public interface ILauncherVersionService {
    Task<LauncherVersionInfo> CheckAsync(CancellationToken ct);
}
```

2) Download e extração
```csharp
public class LauncherUpdater {
    public async Task<string> DownloadAndExtractAsync(Uri assetUrl, string tempDir, CancellationToken ct) {
        // Baixa ZIP para temp, valida checksum (opcional), extrai para tempDir e retorna o caminho.
    }
}
```

3) Updater.exe (projeto separado)
```csharp
static int Main(string[] args) {
    // parse args: installDir, updateDir, restartPath, pid
    // aguarda processo pid encerrar
    // copia updateDir -> installDir substituindo arquivos
    // inicia restartPath
    return 0;
}
```

4) UI/Fluxo
```csharp
// Startup:
var info = await versionService.CheckAsync(ct);
if (info.Remote > info.Current) {
    // Mostrar UI de "Atualizar Launcher" e bloquear Client update/play
} else {
    // Verificar/Atualizar Client (fluxo existente)
}
```

## Tratamento de erros e limites

- Rate limit da API do GitHub: usar User-Agent customizado, cache local e backoff.
- Sem internet: exibir opção de tentar novamente e link para download manual.
- Falha na substituição de arquivos: instruir usuário a fechar o Launcher, ou executar o Updater com privilégios elevados (se necessário), embora o diretório `%AppData%` normalmente não exija admin.

## Critérios de aceite

- O Launcher detecta corretamente quando há nova versão publicada no GitHub Releases.
- Em caso de desatualização, o Client não pode ser atualizado/executado antes do Launcher.
- Botão "Atualizar Launcher" realiza o download, substituição via Updater e relança o app.
- Após relançar, a verificação do Client ocorre normalmente.
- Logs informativos e mensagens de erro amigáveis.

## Próximos passos

1. Confirmar a estratégia de distribuição (ZIP único no Release do GitHub).
2. Implementar `LauncherVersionService` e `LauncherUpdater` no projeto.
3. Criar projeto `Updater.exe` e integrá-lo ao build.
4. Ajustar a UI (mensagens e botão "Atualizar Launcher").
5. Atualizar `launcher_config.json` com parâmetros de origem.
6. Integrar ao workflow de CI/CD (GitHub Actions) para publicar o ZIP do Launcher.

---

Se aprovar este plano, inicio a implementação com commits incrementais (serviço de versão, download, Updater.exe e ajustes de UI), garantindo cobertura de logs e testes manuais em Windows.

