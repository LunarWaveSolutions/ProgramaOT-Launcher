# Plano de atualização do ProgramaOT Launcher (sem `clientVersion`)

Este documento descreve como o launcher passa a detectar e instalar a última versão do cliente SEM depender do campo `clientVersion` do `launcher_config.json`. Em vez disso, ele consulta diretamente a última Release do GitHub e compara com uma informação persistida localmente.

## Objetivo

- Mostrar o launcher sempre ao iniciar (independente de haver ou não cliente instalado).
- Dentro do launcher, decidir entre:
  - Baixar/Atualizar (se o cliente instalado não corresponde à última Release), ou
  - Play (se a última Release já foi instalada).
- Não exigir que você altere `launcher_config.json` manualmente no seu repositório.

## Fonte da verdade

- A “versão” passa a ser a tag da última Release do GitHub em `ProgramaOT`.
- URL consultada: `https://api.github.com/repos/LunarWaveSolutions/ProgramaOT-Cliente/releases/latest`
  - Campo lido: `tag_name` (ex.: `v2025.11.09-22.13.46`).
- O arquivo ZIP do cliente continua sendo baixado de: `https://github.com/LunarWaveSolutions/ProgramaOT-Cliente/releases/latest/download/client-to-update.zip`.

## Persistência local

- Criar um arquivo `version_control.json` (ou `versions.json`) na pasta base do launcher (a mesma onde o cliente é instalado). Exemplo:

```json
{
  "installedTag": "v2025.11.09-22.13.46",
  "installedAt": "2025-11-09T22:15:03Z",
  "sourceUrl": "https://github.com/RafaelRodriguesDev/ProgramaOT/releases/download/v2025.11.09-22.13.46/client-to-update.zip"
}
```

- Esse arquivo é criado/atualizado ao final de cada instalação/atualização bem-sucedida.

## Fluxo de execução

1. Iniciar app:
   - SplashScreen aparece rapidamente e abre o MainWindow.
   - Sempre mostra o launcher, sem tentar abrir o cliente automaticamente.

2. MainWindow carrega:
   - Consulta a última Release via API do GitHub (`releases/latest`) e obtém `latestTag`.
   - Lê `installedTag` do `version_control.json` (se existir).
   - Decisão:
     - Se `installedTag` estiver vazio ou diferente de `latestTag`: mostrar ação “Download/Update”.
     - Se `installedTag` for igual a `latestTag`: mostrar ação “Play”.

3. Ação de Download/Update:
   - Baixa o ZIP de `newClientUrl` (que já aponta para `releases/latest/download/client-to-update.zip`).
   - Extrai para a pasta do cliente e aplica `replaceFolders` conforme `launcher_config.json`.
   - Atualiza `version_control.json` com `installedTag = latestTag` e `installedAt`.
   - Após concluir, UI muda para “Play”.

4. Ação de Play:
   - Executa `clientExecutable` da pasta instalada.

## UI/Comportamento desejado

- O launcher aparece sempre primeiro.
- Botões previstos:
  - Primário dinâmico: “Download” (primeira instalação) ou “Update” (há cliente instalado mas não é a última tag).
  - “Play”: aparece e fica habilitado apenas quando há cliente instalado e `installedTag == latestTag`.
- Mensagens/labels podem exibir a tag em uso (ex.: `v2025.11.09-22.13.46`).

## Implementação técnica

- Arquivos envolvidos:
  - `src/MainWindow.xaml.cs`: adicionar métodos
    - `GetLatestReleaseTag()` para consultar `tag_name` via GitHub API (HTTP GET com cabeçalho `User-Agent`), com fallback em caso de erro.
    - `GetInstalledTag()` e `SaveInstalledTag()` para ler/gravar `version_control.json`.
    - Ajustar lógica de inicialização para não depender de `clientVersion` e decidir estado da UI com base em `installedTag` vs `latestTag`.
  - `src/SplashScreen.xaml.cs`: manter apenas a transição rápida para o launcher (ex.: 1s). Não iniciar o cliente automaticamente.
  - `src/ClientConfig.cs`: sem mudanças (mantém `newClientUrl` apontando para `releases/latest/download/client-to-update.zip`).
  - `launcher_config.json`: não precisa alterar `clientVersion`. Esse campo será ignorado para detecção.

- Tratamento de erros:
  - Se a consulta à API falhar (limite de requisições, offline, etc.), assumir estado “Download” quando não há cliente instalado, e “Play” quando há.
  - Possível retry simples para a API.
  - Exibir mensagens amigáveis ao usuário.

## Testes previstos

1. Primeira execução sem cliente instalado: UI mostra “Download” e (se desejar) oculta “Play”.
2. Execução após instalação: `version_control.json` presente e `installedTag == latestTag` → UI mostra “Play”.
3. Nova Release publicada: `latestTag` muda, `installedTag` fica desatualizado → UI mostra “Update”.
4. Falha de rede/API: sem bloquear UI; aciona comportamento padrão conforme existência da instalação local.

## Próximos passos

1. Reverter quaisquer alterações que tenham introduzido dependência em `clientVersion` (caso estejam ativas) — manteremos a detecção baseada apenas em tag da Release e no `version_control.json` local.
2. Implementar os métodos e ajustes descritos em `MainWindow.xaml.cs` e `SplashScreen.xaml.cs`.
3. Ajustar UI (se necessário) para refletir os estados “Download/Update/Play” conforme a nova lógica.
4. Testar localmente.

Se você aprovar este plano, eu aplico as alterações no código conforme descrito acima (apenas neste projeto local), mantendo o `launcher_config.json` intacto e sem necessidade de mudanças manuais de versão no repositório.


