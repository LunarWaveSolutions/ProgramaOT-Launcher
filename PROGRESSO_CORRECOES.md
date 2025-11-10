# Progresso de Correções e Implementações do ProgramaOT-Launcher

Este documento acompanha, passo a passo, as correções e melhorias implementadas no projeto, conforme o plano definido na análise técnica.

## 2025-11-10 — Commit de base
- Ação: Criado commit local de base com o estado atual do launcher.
- Comando: `git init; git add -A; git commit -m "chore: commit de base - estado atual do launcher"`
- Objetivo: Garantir um ponto de restauração e referência antes de iniciar alterações.

## 2025-11-10 — Ajuste do UpdaterHelper (suporte a flags nomeadas)
- Ação: O `UpdaterHelper.exe` passou a aceitar tanto argumentos posicionais quanto flags nomeadas:
  - Posicionais: `UpdaterHelper.exe <sourceDir> <targetDir> <processId>`
  - Nomeadas: `--source-dir <path> --target-dir <path> --pid <id>` e também a forma `--flag=valor`.
- Arquivo modificado: `src/UpdaterHelper/Program.cs`
- Motivação: Corrigir incompatibilidade com os argumentos enviados pelo `UpdateProgressWindow`, tornando o fluxo de atualização mais robusto e menos propenso a erros.

Próximas etapas:
1. Propagar `AssetApiUrl` e `ChecksumUrl` nos argumentos de atualização do launcher (AtualizaLauncher).
2. Implementar validação de checksum (SHA256) do pacote baixado antes da extração.
3. Adicionar logger central simples e instrumentar pontos críticos.

