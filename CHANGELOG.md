# Changelog

All notable changes to Suflae are documented here. This project follows
[Keep a Changelog](https://keepachangelog.com/) conventions.

## [0.5.0] — unreleased

Suflae's version number now moves with its builder's release, so the release after 0.1.0 is 0.5.0.

0.5 changes the language surface in many places. Read **Breaking changes** before you move a 0.1 project.

To move a project, run `suflae migrate <folder>` first. It applies every default rewrite listed below, skips
files whose `config.toml` already says `suflae-version = "0.5.0"`, and marks the manifest when it is done. The
named steps (`suflae migrate --step <name> <folder>`) run one rewrite on any source. Where no step exists, the
build error says what to write instead.

`migrate` reads sources as 0.5 code, so indent 0.1 sources in steps of 4 spaces before you run it
(see the first item below).

### Breaking changes

**Source layout and tools**
- Indentation is a multiple of 4 spaces. Any other indent is SF-G053. 0.1 sources used 2 spaces: double the
  leading spaces of each line before running `migrate`.
- The project manifest is `config.toml`. Rename the 0.1 manifest file to it. Settings that were environment
  variables are `[target]` and `[debug]` keys in it.
- The commands are `check`, `build`, `run`, `test`, `fmt`, `lint`, `lsp`, `new`, `migrate` and `docs`. A bare file
  name is not a command: the error shows `suflae run <file>`.
- Every runtime crash exits with code 82.

**Failure and recovery**
- A routine that can fail says so on its declaration: `failable routine parse(...)`. `routine parse!(...)` is
  SF-G222 (`migrate --step failable`). A call never writes `!` (SF-G211).
- Recovery is a keyword at the call: `try f(x)` gives a `Maybe[T]`, `check f(x)` gives a `Check[T]`. The
  `try_f` / `check_f` / `lookup_f` routines are gone. Write `try f(x)` for `try_f(x)` and `check f(x)` for
  `check_f(x)` and `lookup_f(x)`.
- `Result[T]` is `Check[T]`. `Lookup[T]` is gone.
- `absent` is gone: a routine with nothing to give throws, `throw AbsentValueError()` (default `migrate`).
- The `Crashable` protocol is the `Crashables` record. Every crashable writes its own `crash_message()` (SF-S704).
  A crashable larger than 64 bytes is a build error.

**Syntax**
- A loop is `until c` (it stops when `c` becomes true) or `loop` (it ends only with `break`, `return` or `throw`).
  `while` is not a keyword: `while c` is SF-G224 (`migrate --step until`). `until false` warns (SF-W304, quick fix
  `loop`). A loop takes no `else` (SF-G219). A loop's `then` block runs after `break` too.
- A line with no work is `nothing`. `pass` is SF-G225 (`migrate --step nothing`). A type declaration with an empty
  body is SF-S211 (write `nothing` under the header).
- A number takes its type from where it goes or from its declaration (`var x: U8 = 5`). Type suffixes (`5_u8`,
  `10n`, `2.5dn`) are SF-G010 (`migrate --step suffix`). `inf` and `nan` are literals.
- Fixed-width numbers are visible without an import. Default `migrate` removes `import Numerics`.
- Binary floats are `B16`, `B32`, `B64`, `B128` (the build error names the `B` type for an `F` name). Complex types
  are named by their total bits: 0.1's `C64` is `C128` and 0.1's `C128` is `C256`, and `C64` is now two `B32`
  parts. Change these by hand: the old names still build.
- `S64 / S64` is SF-S065. Write `a // b`, or `a.to[Decimal]() / b.to[Decimal]()`.
- Containment is a call on the collection: `xs.has(x)`, `xs.lacks(x)`, `d.has_key(k)`, `d.lacks_key(k)`. The
  `have` / `lack` operators are gone (`migrate --step have`), `contains` is `has` (default `migrate`), `x in xs` as
  a test is SF-G212, and `notin` is gone.
- `x but y` is `x.without(y)`, `unless c` is `if not (c)` (default `migrate`). `disobeys` is gone.
- A pattern binds with `as`: `is T as x`, `else as p` (default `migrate`). `else as x` needs exactly one type left
  (SF-S359).
- `Character` is `Char` and `Routine[...]` is `Callable[...]` (default `migrate`).
- Operator levels: `and` and `or` share one level, and so do the bitwise operators, so mixing them needs
  parentheses (SF-G216). A range binds looser than arithmetic and tighter than comparisons. `**` binds tighter than
  a leading `-`: `-2 ** 2` is `-4`. Check every `-x ** n` by hand.
- A range counts down only with a negative step: `10 to 1` is empty, write `10 to 1 by -1`. Default `migrate` adds
  the step where both ends are literals. Check the other ranges by hand.
- A chained comparison goes one way. There is no `<<<`.
- A constructor is `routine T(...)`. `routine T.create(...)` declares an ordinary member routine named `create`.
- A `var` that hides a parameter or an outer local is SF-S008. An unknown annotation is SF-G221.
- The right side of a compound assignment is checked like a binary expression (`Integer += S64` is SF-S502).
- `===` and `!==` take entities only.
- `discard` is gone. A result you do not need takes no marker: write the call on its own line (`discard` and
  `drop` are SF-G150, default `migrate` removes `discard`).
- A program does not write `destruct` (SF-S821). An entity ends when the last name that refers to it goes away or
  when the cycle collector finds it unreachable. Default `migrate` renames `destroy`.
- `@readonly` and `@reshaping` are gone (SF-S706).
- A program's own files cannot use `danger`, `dangerous` or foreign routines. Code that calls C goes into a library
  the program imports (default `migrate` lists these lines).

**Standard library**
- A module is its folder. The base module is `System` and every file sees it and the modules under it
  (`System/Numerics`, `System/Text`, `System/Temporal`, `System/Concurrency`, `System/Cpu`, ...) without an import.
  Importing a parent does not import its children (`migrate --step modules`).
- Math routines use the shared names: `ln`, `exp_m1`, `sin_pi`, `recip_sqrt`, `ln_gamma`, `mul_add`, `mod_trunc`,
  `rem`, `fract`, `compare`, `floordiv`, `shift_left`, `arith_shift_right` (`migrate --step names`).
- Float operations and math routines crash on an inf or NaN result. The raw result is `+! -! *! /! **!` and the
  `_unchecked` routines.
- A conversion is `x.to[T]()`: `S32(from: x)` and `x.S32()` are rewritten by default `migrate`, and a missing
  conversion is SF-S458.
- `Decimal` holds 34 significant digits. `+`, `-` and `*` are exact or crash with `NumericOverflowError`. `/` and
  the math routines round to 34 digits. The arbitrary-precision `Real` is gone.
- Channels are `Mailbox[T]` (`add_last`, `add_last_if_room`, `remove_first`, `close`, `each x in m`). Default
  `migrate` lists each `make_channel` for a rewrite by hand.
- `List` keeps its elements from the first place on. The ring operations (`rotate_left`, `keep_last`, ...) are
  `CircularList`'s (default `migrate` lists them).
- `Signals` is `CpuEventManager` in `System/Cpu`, with one handler slot per event kind (`migrate --step
  cpu-events`).
- The time is read with `Clock.now()`, `Clock.monotonic()` and `CpuClock.now()`.
- `FileManager` does every file system task, and `IO/FileSystem` is gone. Reading or writing a missing file fails,
  `make_file` fails on an existing file unless you pass `replacing: true`, and there is no `touch`. The
  directories and the environment variables are `Environment`'s, random bytes are `Entropy`'s.
- `ask_line` and `ask_word` fail with `EndOfInputError` at the end of input.
- `Dict.items()` gives `(key, value)` tuples: `each (k, v) in d.items()`. `d[k] = v` adds a missing key, and
  `d.add(key:, value:)` is rewritten to it.
- `PriorityQueue` holds `(priority, element)` tuples and has `unordered`, `ordered` and `drain`.
- `Duration` `//` and `%` follow the integer rules, and overflow or division by zero crashes.
- `Indexable` is `ViewingIndexable` and `MutableIndexable` is `ModifyingIndexable`.
- Concurrency: `suspended` is gone. Any routine runs concurrently through `f.to[Agent](args)` (default `migrate`
  adds `.to[Agent]` to the calls). Start an agent with `start_now()`, `start_here()` or `start_apart()`.
  `execute()` is gone (default `migrate` writes `start_now()`). `gather()` and `race()` are routines on the agent
  list: `agents.gather()`. When the last name that refers to an agent goes away, the work is cancelled and waited
  for.
- Byte flows (`FileSource`, `FileSink`, `stdin()`, `stdout()`) read with `pull(max:)` and write with `push(data:)`
  (`migrate --step verbs`).

### Added

**Language**
- A routine that returns nothing ends without `return`.
- `Integer / Integer` is a `Decimal` (`7 / 2` is `3.5`). An `Integer` and a `Decimal` mix in arithmetic and give a
  `Decimal`.
- `global name: Type = init`: module-level state. Changes to a global from several agents are serialized.
- Loops carry values: `until c with total = 0`, `continue with total: v`, and `then` sees them.
- A `when` that gives a value ends a block arm with `becomes value`. One arm can match several values
  (`== 4, 6, 9, 11 =>`).
- Backtick names, tuple swap `(a, b) = (b, a)`, record patterns, inline variants `A | B`, `define` type aliases,
  generic variants, `@infix` routines, named and short arguments mixed (`area(width, height: 4)`), `todo`.
- Presets are computed at buildtime. Hex float literals (`0x1.8p3`) for binary floats.
- `@throws([...])` lists what a routine throws and the builder checks it.
- A file without a `module` header takes its module path from its place under `module_paths`.
- Collection literals take their element type from the expected type, like numbers do.

**Standard library**
- Collections, `Text`, `Bytes` and the IO routines count with `Integer`. `Memory` and `Span`. `List`, `Dict`,
  `Set`, `PriorityQueue` and the sorted collections are built for Suflae.
- `type_name`, `memvars`, `peek`, `poke` and `watch` for looking into values at run time. `T(from: SerialValue)`
  builds a value from serial data (`SerialShapeError`).
- Changing a collection's shape during `each` over it crashes with `ReshapingWhileInUseError`.
- `TextPattern` and `BytesPattern`: patterns built from named pieces, matched in time linear in the input, with
  `from_regex`, `matches`, `find`, `find_all`, `replace` and `split`.
- Deadlock detection in debug builds.
- `@test` routines and the `test` command, with `@case`, `@skip`, `@time_limit` and `@memory_limit`.
- `Environment`, `Entropy`, `Clock`, `CpuClock`, `CpuEventManager`.
- `ask[T]()`, `set_exit_code`, `to_wrap[T]()`, `to_clamp[T]()`, `to_text(radix:)`, `hex()`, `bin()`, `oct()`,
  `fixed(places)`.
- Agents: `cancel()`, `next_finished`, `gather_all`, `take_finished`, `cancel_all`.
- `Dict.set_if_absent`, `List.swap`, `first_true` / `count_trues` / `is_any_true` on bit collections.
- Float math routines are correctly rounded on every platform, and printed floats are the shortest decimal that
  reads back to the same bits. `BF16` does arithmetic.
- `Char` case mapping follows Unicode 17.0.

**Tools**
- A resident build daemon and a JIT mode for the edit loop: `[target] mode = "debug-jit"`, `use-daemon` and
  `incremental`.
- `new`, `migrate`, `docs`, `fmt`, `lint`, `test`, `lsp` and `check --json`.
- The language server: diagnostics, formatting, hover, go to definition, rename and quick fixes.
- Rider plugin and VS Code extension.

### Fixed
- Two agents adding to one `List` lost updates.
- `total += xs[0]` crashed, and a `global` `S64` lost digits.
- `x in dict` gave a wrong error.
- A `when` with no subject, a typed `E?` local, member defaults, destructuring arms and `??` were rejected or built
  wrongly.
- Nullable entity parameters, inferred collection elements, and reassigning an entity member.
- Compound assignment to an entity member or a collection element.
- Importing a module that has globals.
- A stack overflow crashes with exit code 82 and reports that the program ran out of memory.
- A crash inside the standard library points to the caller's line.
- Two or more recovered calls inside one generic routine no longer stop the build.
- `Integer ** -n` crashes with `NumericDomainError`.
- `each (i, xs) in make().enumerate()` crashed. `.List()` now points to `x.to[List]()` (SF-S458).
- `List[S64](capacity: x)` with a wrong argument names the constructor's parameters (SF-S502).

### Removed
- `Result[T]`, `Lookup[T]`, `Crashable`, `absent`, `discard`, and the `try_` / `check_` / `lookup_` routines.
- `unless`, `but`, `disobeys`, `notin`, `have`, `lack`, `while`, `pass`, `suspended`.
- `Agent.execute()`, channels and `make_channel` (use `Mailbox`).
- The `Signals` module and `IO/FileSystem`.
- `Real`, arbitrary-precision `Complex`, the `?.` operator.
- `@readonly`, `@reshaping`, `import Numerics`.
- Number type suffixes and the `B*_INFINITY` / `B*_NAN` presets.

## [0.1.0] — 2026-08-28

The first Suflae release.
