# 📋 Fluxo de Versionamento do Launcher

## 🎯 Problema Resolvido

O launcher estava comparando versões **numéricas** (1.0) com **timestamps** (20251110-0756), causando loops infinitos de atualização.

## ✅ Solução Implementada

### 1. Sistema de Comparação Inteligente

O `LauncherUpdateService` agora suporta **três formatos de versão**:

#### Formato 1: Versão Numérica (SemVer)
```
1.0
2.0
1.2.3
```
**Uso**: Releases oficiais numeradas

#### Formato 2: Timestamp Automático
```
20251110-0756
20251111-1430
```
**Uso**: Builds automáticos do CI/CD (formato: YYYYMMDD-HHMM)

#### Formato 3: Prefixos Especiais
```
auto-20251110-0756
v1.0
v2.0
```
**Processamento**: Prefixos `auto-` e `v` são removidos automaticamente

### 2. Lógica de Comparação

```csharp
// Ordem de tentativa:
1. Compara como string exata (se iguais → sem update)
2. Tenta parse numérico (1.0 vs 2.0)
3. Tenta parse timestamp (20251110-0756 vs 20251111-0800)
4. Se um é timestamp e outro numérico → timestamp é mais novo
5. Fallback: compara como strings diferentes
```

### 3. Arquivo de Controle: `versionlauncher.json`

**Localização**: `bin\Debug\versionlauncher.json` ou `bin\Release\versionlauncher.json`

**Conteúdo após atualização**:
```json
{
  "installedAtUtc": "2025-11-11T02:14:15.6000679Z",
  "versionTag": "20251110-0756",
  "sourceUrl": "https://github.com/.../launcher-update.zip",
  "zipChecksumSha256": "643191e35...",
  "appFileVersion": "0.0.0.0"
}
```

**Campo crítico**: `versionTag` - usado para comparação na próxima inicialização

## 🎛️ Padrão recomendado: Releases com Versão Numérica (SemVer)

Para simplificar e evitar confusão entre formatos, adote como padrão versões numéricas (SemVer) nas releases do launcher.

Como publicar uma release numérica:
- Dentro do ZIP de atualização, inclua `launcher_config.json` com:
  ```json
  {
    "launcherVersion": "2.1.0",
    "launcherMinVersion": "2.0.0" // opcional; use para tornar update obrigatório a partir de determinada versão
  }
  ```
- Crie a tag da release no GitHub como `v2.1.0` (ou `2.1.0`). O código remove o prefixo `v` automaticamente, se existir.
- Preferencialmente use tag com prefixo `v` (ex.: `v2.1.0`) para acionar o workflow de release no GitHub Actions; o pipeline atual está configurado com `on.push.tags: v*`.
- Ao aplicar o update, o `UpdaterHelper` grava `versionlauncher.json` com `versionTag = "2.1.0"` (limpo de prefixos).

Observações importantes:
- `launcherMinVersion` torna o update obrigatório quando a versão instalada é menor que o mínimo configurado. Configure com cuidado.
- O projeto atual atualiza o `launcher_config.json` do destino, ajustando os campos `launcherVersion` e, se existir, também `launcherMinVersion`, para refletir a versão recém-instalada.
- A comparação entre instalado e release é feita primeiro como versão numérica; se não for possível, tenta timestamp; por fim, fallback de string.

## 🔄 Fluxo Completo de Atualização

### Passo 1: Launcher Inicia
```
1. Lê versionlauncher.json
2. Obtém versionTag atual (ex: "1.0")
3. Faz request para GitHub API
4. Recebe latest tag (ex: "auto-20251110-0756")
5. Compara versões usando lógica inteligente
```

### Passo 2: Detecta Update Disponível
```
- Se versionTag atual < latest → Mostra botão de update
- Se versionTag atual >= latest → Oculta botão
```

### Passo 3: Usuário Clica em "Update"
```
1. Confirm dialog aparece
2. Launcher fecha
3. Reinicia com argumentos: --download-update --version=...
4. UpdateProgressWindow baixa o ZIP
5. Extrai para UpdateLauncher\payload\
6. Chama UpdaterHelper.exe
```

### Passo 4: UpdaterHelper Aplica Update
```
1. Aguarda launcher fechar (wait PID)
2. Copia arquivos de payload\ → raiz
3. Salva versionlauncher.json com NOVA versionTag
4. Atualiza launcher_config.json
5. Reinicia o launcher
```

### Passo 5: Launcher Reinicia (Pós-Update)
```
1. Lê versionlauncher.json (ATUALIZADO!)
2. Obtém versionTag = "20251110-0756"
3. Compara com GitHub API
4. Se for a mesma → Botão de update OCULTO ✅
```

## 🐛 Debugging

### Verificar se a atualização foi aplicada

**Abra**: `logs\launcher.log`

**Procure por**:
```
GetInstalledLauncherTag: Lido do versionlauncher.json = 'VERSAO_AQUI'
CompareVersions: cleanInstalled='...', cleanLatest='...'
CheckLauncherUpdateAndSyncButtonsAsync: IsUpdatePending=False
```

### Verificar versionlauncher.json

**Windows PowerShell**:
```powershell
Get-Content .\versionlauncher.json | ConvertFrom-Json
```

**Saída esperada após update**:
```
versionTag      : 20251110-0756
installedAtUtc  : 2025-11-11T02:14:15.6000679Z
```

### Forçar nova verificação

**Deletar versionlauncher.json**:
```cmd
del versionlauncher.json
```

Na próxima execução, o launcher irá criar um novo com a versão padrão de `launcher_config.json`.

## 📝 Checklist de Testes

- [ ] 1. Launcher inicia sem erros
- [ ] 2. Se versão instalada < release → botão aparece
- [ ] 3. Clicar no botão inicia download
- [ ] 4. UpdateProgressWindow mostra progresso
- [ ] 5. UpdaterHelper copia arquivos
- [ ] 6. versionlauncher.json é atualizado
- [ ] 7. Launcher reinicia automaticamente
- [ ] 8. Botão de update DESAPARECE ✅
- [ ] 9. Client pode ser atualizado/iniciado normalmente

## 🚀 Configuração de Release no GitHub

### Para Versões Numéricas
```json
// launcher_config.json dentro do launcher-update.zip
{
  "launcherVersion": "2.0",
  "launcherMinVersion": "1.0"
}
```

**Tag da Release**: `v2.0` ou `2.0`

**Assets publicados pelo workflow**:
- `launcher-update.zip` (pacote do launcher)
- `launcher-update.zip.sha256` (checksum SHA256)

### Para Builds Automáticos (CI/CD)
```json
{
  "launcherVersion": "20251110-0756",
  "launcherMinVersion": "1.0"
}
```

**Tag da Release**: `auto-20251110-0756`

## ⚠️ Importante

1. **Sempre incremente a versão** no `launcher_config.json` dentro do ZIP de atualização
2. **Tag da release deve ser maior** que a versão instalada (numericamente ou cronologicamente)
3. **Não misture formatos**: Escolha UM formato (numérico OU timestamp) e mantenha consistência
4. **versionlauncher.json é a fonte da verdade** - não edite manualmente
5. Padrão recomendado: use versões numéricas (SemVer) para releases do launcher (ex.: `1.0.1`, `2.0.0`).
6. Preferencialmente use tag com prefixo `v` (ex.: `v2.1.0`) para acionar o workflow de release; ele está configurado com `on.push.tags: v*` e publica os assets esperados (`launcher-update.zip` e `launcher-update.zip.sha256`).

## 🔎 O projeto realmente faz isso?

Sim. Pontos de verificação no código:
- `src/LauncherUpdateService.cs`: limpa prefixos `v`/`auto-` e compara versões primeiro como numéricas (`TryParseVersion`), depois como timestamps; registra logs de comparação.
- `src/MainWindow.xaml.cs`: lê exclusivamente `versionlauncher.json` para obter a versão instalada do launcher e exibir em `labelLauncherVersion`.
- `src/UpdaterHelper/Program.cs`:
  - `WriteVersionLauncherJson(...)` grava `versionlauncher.json` com o `versionTag` limpo de prefixos (`v`, `auto-`).
  - `UpdateLauncherConfigVersion(...)` atualiza `launcher_config.json` no destino, substituindo `launcherVersion` e, se existir, `launcherMinVersion` pela versão instalada.
- `ProgramaOTLauncher.csproj`: se `versionlauncher.json` ainda não existir no `bin`, cria um inicial com base em `launcher_config.json` (campo `launcherVersion`) ou na `FileVersion` do executável.
- `launcher_config.json` (raiz do repositório): contém `launcherVersion` e `launcherMinVersion` em formato numérico (ex.: `1.0`), já compatível com o fluxo.

Recomendação: mantenha o fluxo padronizado com releases numéricas e atualize `launcherMinVersion` somente quando quiser tornar uma atualização obrigatória.

## 🔧 Troubleshooting

### Problema: Botão de update não desaparece

**Causa**: versionlauncher.json não foi atualizado

**Solução**:
1. Verifique logs do UpdaterHelper
2. Confirme que `versionTag` foi salvo corretamente
3. Compare com `latest tag` do GitHub

### Problema: "Sempre tem update disponível"

**Causa**: Versões em formatos incomparáveis

**Solução**:
1. Use MESMO formato em local e release
2. Se mudou de numérico → timestamp, incremente manualmente uma vez
3. Verifique logs: `CompareVersions: ...`

### Problema: UpdaterHelper não inicia

**Causa**: Argumentos incorretos ou UAC

**Solução**:
1. Verifique logs: `Reiniciando launcher com argumentos...`
2. Execute como administrador se necessário
3. Confirme que UpdaterHelper.exe existe em `UpdateLauncher\`
