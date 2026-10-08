# Suflae for VS Code

Suflae (`.sf`) in VS Code: highlighting from `../Suflae.tmbundle`, comment toggling and bracket pairing,
and the Suflae language server (`Suflae lsp`): errors as you type, semantic colors, hover, completion, go to
definition, rename, find usages and formatting.

## Which server runs

The setting `suflae.serverPath` sets it: `Suflae.dll` (run with `dotnet`) or a `Suflae` executable. Left empty, the
extension uses the dev build in the workspace, `<workspace>/Suflae/bin/Debug/net10.0/Suflae.dll`, else `Suflae` on the
PATH. The server runs from a copy of its build folder in the extension's storage, so it never holds the files a rebuild
overwrites, and a rebuild restarts it. The command "Suflae: Restart the language server" restarts it by hand.

## Build

Needs Node.js. The grammar, the language configuration and the icon are copied in from `../Suflae.tmbundle` and
`../rider-plugin`, their one source.

```
npm install
npm run package          # -> dist/suflae.vsix
code --install-extension dist/suflae.vsix
```
