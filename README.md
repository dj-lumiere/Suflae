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

## What we are trying to keep

- **No ceremony.** Nothing you write is there only because the language asks for it.
- **Code is the expression of intent.** What you write says what you mean.
- **What should work, works.** Code that looks like it should work does, and it does what it looks like it does.
- **One acceptable way.** There is a way that works for everyone, even if it isn't everyone's favorite.

And for Suflae, the joy of having made something.

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

### 1. Install

Download the package for your platform from the
[releases page](https://github.com/dj-lumiere/Suflae/releases). Suflae and RazorForge ship in one
package, so the file name starts with `razorforge-`:

| Platform              | File                                 |
|-----------------------|--------------------------------------|
| Windows x64           | `razorforge-v<version>-win-x64.zip`    |
| Linux x64             | `razorforge-v<version>-linux-x64.tar.gz` |
| macOS (Apple Silicon) | `razorforge-v<version>-osx-arm64.tar.gz` |

The package is self-contained: the builder, the standard library, the runtime, and the LLVM
toolchain are all inside it. Unpack it anywhere and run the installer from that folder:

```bat
:: Windows: adds the folder to your user PATH
install.cmd
```

```bash
# Linux / macOS: links the commands into ~/.local/bin
./install.sh
```

Open a new terminal and check that it works:

```bash
suflae version
```

The short alias `sf` works as well.

> **Linux:** linking needs the C library's development files. Most machines have them; otherwise
> install `libc6-dev` (Debian/Ubuntu) or `glibc-devel` (Fedora) once.
>
> **macOS:** linking uses Apple's Command Line Tools; run `xcode-select --install` once if you have
> never built anything on this Mac. This alpha is not notarized, and `install.sh` clears the
> Gatekeeper quarantine on the unpacked folder for you.

### 2. Hello, world

```suflae
# hello.sf
import IO/Console

show("Hello from Suflae!")
```

```bash
suflae run hello.sf
```

`run` builds the file and runs it. `suflae build hello.sf` stops after building
and leaves `hello.exe` next to the source.

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

## Building from source

You only need this to work on the builder or the standard library. Suflae is built from five
repositories checked out side by side: the builder core
([Anvila](https://github.com/dj-lumiere/Anvila)), the shared library
([Ingrid](https://github.com/dj-lumiere/Ingrid)), [RazorForge](https://github.com/dj-lumiere/RazorForge)
(whose standard library Suflae uses), this repository, and
[Tessera](https://github.com/dj-lumiere/Tessera), whose builder compiles part of the native runtime.

You need the .NET 10 SDK, LLVM 22 (`clang` and `opt` on `PATH`), CMake 3.20+, and Ninja on Windows.

```bash
mkdir LumiFoundry && cd LumiFoundry
git clone https://github.com/dj-lumiere/Anvila.git
git clone https://github.com/dj-lumiere/Ingrid.git
git clone https://github.com/dj-lumiere/RazorForge.git
git clone https://github.com/dj-lumiere/Suflae.git
git clone https://github.com/dj-lumiere/Tessera.git

dotnet build Suflae/Suflae.csproj                # also builds the native runtime
dotnet test Suflae/tests/Suflae.Tests.csproj     # optional
```

The built command is `Suflae/bin/Debug/net10.0/Suflae` (`Suflae.exe` on Windows).

## Contributing

Bug reports, feature suggestions, documentation fixes, and code are all welcome. Report bugs at
[github.com/dj-lumiere/Suflae/issues](https://github.com/dj-lumiere/Suflae/issues).

## License

MIT; see [`LICENSE`](LICENSE).
