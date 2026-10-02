# Suflae for Rider

A Rider plugin for Suflae (`.sf`):

- Syntax highlighting from `../Suflae.tmbundle`, plus `#` comment toggling and bracket pairing (`bundle/`).
- The Suflae file icon.
- The Suflae language server (`Suflae lsp`), through Rider's LSP client: diagnostics, completion, hover, go to
  definition, references, rename, signature help, inlay hints, code actions, symbols and formatting.

## Which server runs

Settings | Languages & Frameworks | Suflae sets the server: `Suflae.dll` (run with `dotnet`) or a Suflae executable.
Left empty, the plugin uses the dev build, `<project>/Suflae/bin/Debug/net10.0/Suflae.dll` (the LumiFoundry
workspace) or `<project>/bin/Debug/net10.0/Suflae.dll`.

The server runs from a copy of its folder in Rider's system directory (`suflae-lsp/`), never from `bin/`, so it never
holds the files the next `dotnet build` overwrites. When the build folder changes and then stays the same for a few
seconds, the plugin restarts the server from a fresh copy, so a rebuilt builder takes over by itself.

## Build

Needs Rider 2026.2 (build 262) or later. The build compiles against an installed Rider and uses Rider's own JDK, set
once for every plugin in `%USERPROFILE%\.gradle\gradle.properties`:

```
riderLocalPath=C:/Users/<you>/AppData/Local/Programs/Rider
org.gradle.java.installations.paths=C:/Users/<you>/AppData/Local/Programs/Rider/jbr
```

Without `riderLocalPath`, the build downloads the Rider named by `platformVersion` in `gradle.properties`.

```
gradlew buildPlugin    # -> build/distributions/suflae-rider-<version>.zip
gradlew runIde         # a sandbox Rider with the plugin loaded
```

Install the zip with Settings | Plugins | ⚙ | Install Plugin from Disk.
