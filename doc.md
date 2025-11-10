# Progresso: renomeação para ProgramaOT-Launcher e preparação de instalador

Este documento registra as alterações aplicadas no projeto Programa OT Launcher e orienta os testes locais antes de qualquer commit.

## Objetivo
- Padronizar o nome do executável para "ProgramaOT-Launcher.exe".
- Remover referências antigas a "Canary" em namespaces/usings.
- Ajustar template do InstallSimple para apontar para bin\Release e criar atalhos corretos.
- Documentar como testar localmente.

## Alterações aplicadas
1. ProgramaOTLauncher.csproj
   - AssemblyName alterado de `ProgramaOTLauncher` para `ProgramaOT-Launcher` (gera `ProgramaOT-Launcher.exe` em bin).  
2. Assets/icons.xaml
   - `xmlns:local` atualizado de `clr-namespace:CanaryLauncherUpdate.Assets` para `clr-namespace:ProgramaOTLauncher.Assets`.  
3. src/ClientConfig.cs
   - `using CanaryLauncherUpdate;` atualizado para `using ProgramaOTLauncher;`.  
4. src/UpdateConfig.cs
   - Comentário ajustado e remoção do fallback para `CANARY_GITHUB_TOKEN`. Mantido apenas `PROGRAMAOT_GITHUB_TOKEN`.  
5. Instalador/InstallSimple 3.5/template-ProgramaOT.ispro
   - `SourceFolder` ajustado para `bin\Release`.  
   - Atalho alterado de `CanaryLauncher.exe` para `ProgramaOT-Launcher.exe`.  
   - `NetFramework` ajustado para `4.8`.

## Como testar localmente
1) Build Release
- Abra o projeto no Visual Studio e compile em Release.  
- Verifique se o binário gerado é `bin\Release\ProgramaOT-Launcher.exe`.

2) Executar o Launcher
- Rode o executável e valide:  
  - Splash e janela principal abrem sem erros.  
  - Ícones/recursos carregam normalmente (icons.xaml).  
  - Busca do `launcher_config.json` remoto e fallback local funcionam.  
  - User-Agent enviado é `programaot-launcher` e, se definido, o token `PROGRAMAOT_GITHUB_TOKEN` é utilizado.

3) Gerar Instalador com InstallSimple
- Abra `Instalador/InstallSimple 3.5/InstallSimple.exe`.  
- Use o template `template-ProgramaOT.ispro`.  
- Garanta que a pasta de origem está em `bin\Release` e o atalho aponta para `ProgramaOT-Launcher.exe`.  
- Gere o instalador .exe e instale em uma pasta limpa.  
- Verifique criação de atalho, execução e desinstalação.

## Próximos passos (opcionais)
- Preparar script Inno Setup (.iss) para um instalador mais profissional.  
- Assinar digitalmente o instalador (.exe).  
- Ajustar pipelines da pasta .github para refletir o novo nome de artefatos.  

Status: alterações aplicadas localmente; commits e publicação ainda pendentes conforme instrução.
