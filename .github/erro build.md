
16s
Run msbuild $env:SOLUTION_PATH /t:Build /p:Configuration=Release
MSBuild version 17.14.23+b0019275e for .NET Framework
Build started 11/10/2025 4:13:04 AM.
Project "D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\ProgramaOTLauncher.sln" on node 1 (Build target(s)).
ValidateSolutionConfiguration:
  Building solution configuration "Release|Any CPU".
Project "D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\ProgramaOTLauncher.sln" (1) is building "D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\ProgramaOTLauncher.csproj" (2) on node 1 (default targets).
PrepareForBuild:
  Creating directory "bin\Release\".
  Creating directory "obj\Release\".
C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Microsoft\NuGet\17.0\Microsoft.NuGet.targets(198,5): error : Your project file doesn't list 'win' as a "RuntimeIdentifier". You should add 'win' to the "RuntimeIdentifiers" property in your project file and then re-run NuGet restore. [D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\ProgramaOTLauncher.csproj]
Done Building Project "D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\ProgramaOTLauncher.csproj" (default targets) -- FAILED.
Project "D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\ProgramaOTLauncher.sln" (1) is building "D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\Updater\Updater.csproj" (3) on node 1 (default targets).
PrepareForBuild:
  Creating directory "bin\Release\".
  Creating directory "obj\Release\".
CoreCompile:
  C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe /noconfig /nowarn:1701,1702 /fullpaths /nostdlib+ /platform:anycpu32bitpreferred /errorreport:prompt /warn:4 /define:TRACE /highentropyva+ /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\mscorlib.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Core.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.IO.Compression.FileSystem.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Net.Http.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Xml.dll" /debug:pdbonly /filealign:512 /optimize+ /out:obj\Release\Updater.exe /subsystemversion:6.00 /target:ex
  CompilerServer: server - server processed compilation - Updater
CopyFilesToOutputDirectory:
  Copying file from "D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\Updater\obj\Release\Updater.exe" to "D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\Updater\bin\Release\Updater.exe".
  Updater -> D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\Updater\bin\Release\Updater.exe
  Copying file from "D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\Updater\obj\Release\Updater.pdb" to "D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\Updater\bin\Release\Updater.pdb".
Done Building Project "D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\Updater\Updater.csproj" (default targets).
Done Building Project "D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\ProgramaOTLauncher.sln" (Build target(s)) -- FAILED.
Build FAILED.
"D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\ProgramaOTLauncher.sln" (Build target) (1) ->
"D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\ProgramaOTLauncher.csproj" (default target) (2) ->
(ResolveNuGetPackageAssets target) -> 
  C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Microsoft\NuGet\17.0\Microsoft.NuGet.targets(198,5): error : Your project file doesn't list 'win' as a "RuntimeIdentifier". You should add 'win' to the "RuntimeIdentifiers" property in your project file and then re-run NuGet restore. [D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\ProgramaOTLauncher.csproj]
    0 Warning(s)
    1 Error(s)
Time Elapsed 00:00:11.42
Error: Process completed with exit code 1.
Run msbuild $env:SOLUTION_PATH /t:Build /p:Configuration=Release
MSBuild version 17.14.23+b0019275e for .NET Framework
Build started 11/10/2025 4:13:04 AM.
Project "D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\ProgramaOTLauncher.sln" on node 1 (Build target(s)).
ValidateSolutionConfiguration:
  Building solution configuration "Release|Any CPU".
Project "D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\ProgramaOTLauncher.sln" (1) is building "D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\ProgramaOTLauncher.csproj" (2) on node 1 (default targets).
PrepareForBuild:
  Creating directory "bin\Release\".
  Creating directory "obj\Release\".
C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Microsoft\NuGet\17.0\Microsoft.NuGet.targets(198,5): error : Your project file doesn't list 'win' as a "RuntimeIdentifier". You should add 'win' to the "RuntimeIdentifiers" property in your project file and then re-run NuGet restore. [D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\ProgramaOTLauncher.csproj]
Done Building Project "D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\ProgramaOTLauncher.csproj" (default targets) -- FAILED.
Project "D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\ProgramaOTLauncher.sln" (1) is building "D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\Updater\Updater.csproj" (3) on node 1 (default targets).
PrepareForBuild:
  Creating directory "bin\Release\".
  Creating directory "obj\Release\".
CoreCompile:
  C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe /noconfig /nowarn:1701,1702 /fullpaths /nostdlib+ /platform:anycpu32bitpreferred /errorreport:prompt /warn:4 /define:TRACE /highentropyva+ /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\mscorlib.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Core.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.IO.Compression.FileSystem.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Net.Http.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Xml.dll" /debug:pdbonly /filealign:512 /optimize+ /out:obj\Release\Updater.exe /subsystemversion:6.00 /target:ex
  CompilerServer: server - server processed compilation - Updater
CopyFilesToOutputDirectory:
  Copying file from "D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\Updater\obj\Release\Updater.exe" to "D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\Updater\bin\Release\Updater.exe".
  Updater -> D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\Updater\bin\Release\Updater.exe
  Copying file from "D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\Updater\obj\Release\Updater.pdb" to "D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\Updater\bin\Release\Updater.pdb".
Done Building Project "D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\Updater\Updater.csproj" (default targets).
Done Building Project "D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\ProgramaOTLauncher.sln" (Build target(s)) -- FAILED.
Build FAILED.
"D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\ProgramaOTLauncher.sln" (Build target) (1) ->
"D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\ProgramaOTLauncher.csproj" (default target) (2) ->
(ResolveNuGetPackageAssets target) -> 
  C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Microsoft\NuGet\17.0\Microsoft.NuGet.targets(198,5): error : Your project file doesn't list 'win' as a "RuntimeIdentifier". You should add 'win' to the "RuntimeIdentifiers" property in your project file and then re-run NuGet restore. [D:\a\ProgramaOT-Launcher\ProgramaOT-Launcher\ProgramaOTLauncher.csproj]
    0 Warning(s)
    1 Error(s)
Time Elapsed 00:00:11.42
Error: Process completed with exit code 1.