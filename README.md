<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="branding/suflae-dark.svg">
    <img src="branding/suflae.svg" alt="Suflae logo" width="112">
  </picture>
</p>

<h1 align="center">Suflae</h1>

<p align="center"><strong>Make programming sweet again.</strong></p>

<p align="center">
  <img src="https://img.shields.io/badge/version-0.1.0-informational.svg" alt="Version 0.1.0">
  <img src="https://img.shields.io/badge/status-early%20alpha-orange.svg" alt="Status: early alpha">
  <img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License: MIT">
</p>

<p align="center">
  <a href="https://suflae.lumi-dev.xyz/">Documentation</a> ·
  <a href="#quick-start">Quick start</a> ·
  <a href="SUFLAE-FOR-AI.md">Reference for AI assistants</a> ·
  <a href="https://github.com/dj-lumiere/RazorForge/blob/master/CHANGELOG.md">Changelog</a> ·
  <a href="https://github.com/dj-lumiere/RazorForge">RazorForge</a>
</p>

Suflae (`.sf`) is a natively compiled language for building applications without managing memory by
hand. It is the sibling of [RazorForge](https://github.com/dj-lumiere/RazorForge): the same grammar,
the same standard library, the same loud failures, with the ownership machinery out of sight. A file
can be just its statements, and running it is one command, like `python hello.py`.

```suflae
import IO/Console

entity Point
    x: Integer
    y: Integer

global moves: Integer = 0

routine nudge(p: Point)
    p.x = p.x + 1
    moves = moves + 1
    return

var p = Point(x: 3, y: 4)
var q = p                   # both names refer to the same Point
nudge(p: q)
show(f"p.x = {p.x}")        # 4
show(f"moves = {moves}")    # 1

show(0.1 + 0.2 == 0.3)      # true: bare numbers are Integer and Decimal
show(99999999999999999999 * 99999999999999999999)
```

> **Early alpha.** Suflae 0.1 is the first release. Programs build and run on Windows, Linux, and
> macOS, and every commit runs the Suflae test suite and its end-to-end programs in CI, several of
> them locked to produce the same output as their RazorForge twins. APIs will change, and you will
> find bugs.

## What it is like

**Entities are shared.** A Suflae `entity` is a reference-counted handle: `var q = p` gives you a
second name for the same object, with no `steal` and no ownership errors. The count is cheap while an
entity stays on one task and becomes thread-safe when it escapes to another, and a cycle collector
reclaims reference cycles. There are no access tokens, no lifetimes, and no weak-reference
annotations. An entity that may be missing is written `Point?` and must be checked against `None`
before you use it.

**Numbers are exact by default.** A bare `42` is an arbitrary-precision `Integer` and a bare `3.14`
is a base-10 `Decimal`, so `0.1 + 0.2 == 0.3` holds and big numbers don't overflow. The fixed-width
types (`S32`, `U64`, `B64`, …) are still there when you name them.

**A file can be just its statements.** Top-level statements run top to bottom with no
`routine start()`, and `module` is optional. Routines you define still end with `return`.

**`global` is module state that tasks can share.** `global name: Type = value` declares module-level
mutable state. Globals initialize once, in dependency order, and every global is thread-safe: a
single-statement update such as `count = count + 1` is atomic across parallel tasks. (RazorForge has
no module-level mutable state; this is a Suflae feature.)

**Failure is loud, and recovery takes one keyword.** As in RazorForge, a routine that can `throw` or
go `absent` carries `!` on its declaration. A bare call that fails crashes with a message (exit status
82); `try`, `grab`, or `lookup` in front of the call recovers it as `Maybe[T]`, `Check[T]`, or
`Lookup[T]`. Out of memory and runaway recursion are fatal and not recoverable.

**Nothing unsafe is reachable.** `danger` blocks, `dangerous` routines, raw handles, and `steal` are
not part of Suflae. Changing a list while an `each` loop walks it is stopped with a clear error: at
build time when you do it directly, at run time when a called routine does it.

## Quick start

### From a release package

Prebuilt packages for win-x64, linux-x64, and osx-arm64 are on the
[releases page](https://github.com/dj-lumiere/Suflae/releases). Each one bundles the LLVM toolchain
it needs, so unpack it, put it on your `PATH`, and run:

```bash
suflae hello.sf
```

### From source

Suflae is built from four repositories checked out side by side: the builder core
([Anvila](https://github.com/dj-lumiere/Anvila)), the shared library
([Ingrid](https://github.com/dj-lumiere/Ingrid)), [RazorForge](https://github.com/dj-lumiere/RazorForge)
(whose standard library Suflae uses), and this repository.

You need the .NET 10 SDK, LLVM 22 (`clang` and `opt` on `PATH`), CMake 3.20+, and Ninja on Windows.

```bash
mkdir LumiFoundry && cd LumiFoundry
git clone https://github.com/dj-lumiere/Anvila.git
git clone https://github.com/dj-lumiere/Ingrid.git
git clone https://github.com/dj-lumiere/RazorForge.git
git clone https://github.com/dj-lumiere/Suflae.git

# The native runtime builds against libuv and libco, which are not vendored.
git clone --depth 1 https://github.com/libuv/libuv.git Ingrid/native/libuv
git clone --depth 1 https://github.com/higan-emu/libco.git Ingrid/native/libco

dotnet build Suflae/Suflae.csproj                # also builds the native runtime
dotnet test Suflae/tests/Suflae.Tests.csproj     # optional
```

### Hello, world

```suflae
# hello.sf
import IO/Console

show("Hello from Suflae!")
```

```bash
./Suflae/bin/Debug/net10.0/Suflae hello.sf
# Windows: .\Suflae\bin\Debug\net10.0\Suflae.exe hello.sf
```

A bare `suflae <file>.sf` builds the file and runs it. `suflae build hello.sf` builds a native
executable without running it.

### Using an AI assistant?

Suflae is not in any model's training data yet, so assistants tend to guess Python-flavored syntax
that does not build. Point yours at [`SUFLAE-FOR-AI.md`](SUFLAE-FOR-AI.md) together with
[`RAZORFORGE-FOR-AI.md`](https://github.com/dj-lumiere/RazorForge/blob/master/RAZORFORGE-FOR-AI.md)
(the shared grammar). Both ship in every release package. The CI-verified programs in
[`tests/Fixtures/StdlibSf/`](tests/Fixtures/StdlibSf/) make good starting points.

## Command line

```
suflae <source-file>                  Build and run the file
suflae buildandrun [entry-file]       Build and run
suflae build [entry-file]             Build a native executable for this OS (no run)
suflae check [entry-file]             Type-check only
suflae codegen [entry-file] [out.ll]  Stop at LLVM IR
suflae parse | tokenize <source-file> Front-end inspection
suflae --lsp                          Run the language server over stdio
suflae help | version
```

There are no build flags. Projects are configured in a `config.toml` manifest, the same one
RazorForge uses (see the
[RazorForge README](https://github.com/dj-lumiere/RazorForge#command-line)), and the standard library
is found next to the `suflae` executable, so a `.sf` file anywhere on disk builds.

## Mixing with RazorForge

A `.sf` file can `import` a RazorForge module and use its types and routines directly. An entity
that comes from RazorForge keeps RazorForge's single-ownership model: you can use it freely as a
local or a field, and to pass one across a Suflae routine's signature you hold it in a
`Retained[T]`. This is how a Suflae program reaches lower-level code written in RazorForge.

## Not there yet

The REPL, runtime reflection, and hot reload are planned and not implemented. Async networking is
missing in both languages. See the status note at the end of [`SUFLAE-FOR-AI.md`](SUFLAE-FOR-AI.md)
for the current state.

## Documentation

The documentation lives at [suflae.lumi-dev.xyz](https://suflae.lumi-dev.xyz/):

- [Hello World](https://suflae.lumi-dev.xyz/Hello-World) ·
  [Entities](https://suflae.lumi-dev.xyz/Entities) ·
  [Numeric Types](https://suflae.lumi-dev.xyz/Numeric-Types)
- [Error Handling](https://suflae.lumi-dev.xyz/Error-Handling) ·
  [Collections](https://suflae.lumi-dev.xyz/Collections) ·
  [Concurrency Model](https://suflae.lumi-dev.xyz/Concurrency-Model)
- [Choosing a Language](https://suflae.lumi-dev.xyz/Choosing-Language) ·
  [RazorForge Interop](https://suflae.lumi-dev.xyz/RazorForge-Interop) ·
  [Design Philosophy](https://suflae.lumi-dev.xyz/Design-Philosophy)

## This repository

```
Suflae/
├── src/
│   ├── Lexer/             # The Suflae lexer
│   ├── Passes/            # Suflae-only lowering: shared entities, module globals, loop shape checks
│   └── SuflaeCli.cs       # The `suflae` command line
├── tests/                 # Suflae.Tests: unit tests and end-to-end fixtures
│   └── Fixtures/StdlibSf/ #   programs with expected output
├── Suflae.tmbundle/       # TextMate grammar
└── SUFLAE-FOR-AI.md       # Reference for AI assistants
```

The builder itself is [Anvila](https://github.com/dj-lumiere/Anvila), the standard library lives in
[RazorForge](https://github.com/dj-lumiere/RazorForge/tree/master/Standard), and the shared library
under it is [Ingrid](https://github.com/dj-lumiere/Ingrid).

## Contributing

Bug reports, feature suggestions, documentation fixes, and code are all welcome. Report bugs at
[github.com/dj-lumiere/Suflae/issues](https://github.com/dj-lumiere/Suflae/issues).

## License

MIT; see [`LICENSE`](LICENSE).
