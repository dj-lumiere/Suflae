# Changelog

All notable changes to Suflae are documented here. This project follows
[Keep a Changelog](https://keepachangelog.com/) conventions.

## [0.5.0] — unreleased

Suflae's version number now moves with its builder's release, so the release after 0.1.0 is 0.5.0. This section
lists what differs from 0.1.0.

0.5 changes the language surface in many places. Read **Breaking changes** before you move a 0.1 project.

To move a project, run `suflae migrate <folder>` first. It applies every default rewrite listed below, skips
files whose `config.toml` already says `suflae-version = "0.5.0"`, and marks the manifest when it is done. The
named steps (`suflae migrate --step <name> <folder>`) run one rewrite on any source. Where no step exists, the
build error says what to write instead.

`migrate` first indents a 0.1 file in steps of 4 spaces and renames the 0.1 manifest file to `config.toml`, then
rewrites it. Run the named steps after it and `--step suffix` last: the suffix step needs analysis, which reads a
file only once its other old spellings are gone.

### Breaking changes

**Source layout and tools**
- Indentation is a multiple of 4 spaces. Any other indent is `SF-G053`. 0.1 sources used 2 spaces: default
  `migrate` doubles the leading spaces of each line of such a file first (`migrate --step indent` does only that,
  and leaves the lines inside a text literal as they are).
- The project manifest is `config.toml`. The 0.1 manifest file with no `config.toml` next to it is a build error
  that names `config.toml`, and default `migrate` renames it. Settings that were environment variables are
  `[target]` and `[debug]` keys in it: when the old deadlock variable is still set, every command prints a warning
  that names `deadlock-check` under `[debug]`.
- The commands are `check`, `build`, `run`, `test`, `fmt`, `lint`, `lsp`, `new`, `migrate` and `docs`. A bare file
  name is not a command: the error shows `suflae run <file>`.
- Every runtime crash exits with code 82.

**Failure and recovery**
- A routine that can fail says so on its declaration: `failable routine parse(...)`. `routine parse!(...)` is
  `SF-G222` (`migrate --step failable`). A call never writes `!` (`SF-G211`).
- Recovery is a keyword at the call: `try f(x)` gives a `Maybe[T]`, `check f(x)` gives a `Check[T]`. The
  `try_f` / `check_f` / `lookup_f` routines are gone. Default `migrate` writes `try f(x)` for `try_f(x)` (and
  `try r.f(x)` for `r.try_f(x)`) and `check f(x)` for `check_f(x)` and `lookup_f(x)`, and the build error on an old
  name names the keyword.
- Failures rise only through failable routines. A `throw` in a routine that is not `failable` crashes there with the
  crashable's message, so `pierce` is gone and is an ordinary name: `migrate --step pierce` writes `throw` for each
  `pierce` in a routine that is not failable and lists the ones in failable routines, where a `throw` goes to the
  caller instead.
- A routine that is not failable is a wall: a failure inside it crashes there, even under a `try` or `check` above
  its caller. Declare the routine `failable` to let its failures go up.
- `try` or `check` in front of an expression that reaches no failable call is `SF-S760`. Remove the keyword, or
  declare the routine `failable`.
- A lambda that can fail goes only to a parameter of type `failable Callable[...]`: handing one to a plain
  `Callable[...]` is `SF-S761`. The call that hands it over can fail through it
  (`try items.select(transform: x => 10 // x).to[List]()`). The standard library routines that take a lambda
  (`select`, `where`, `any`, `all`, `sort_by`, ...) take it as `failable Callable[...]`.
- `Result[T]` is `Check[T]`. `Lookup[T]` is gone. The build errors say so.
- `absent` is gone: a routine with nothing to give throws, `throw AbsentValueError()` (default `migrate`).
- The `Crashable` protocol is the `Crashables` record. Every crashable writes its own `crash_message()` (`SF-S704`).
- `MemoryAllocationError` is `OutOfMemoryError` (default `migrate`, `migrate --step type-names`).
- `Integer` is `Int` (default `migrate`, `migrate --step type-names`). There is no alias: `Integer` is a build
  error that names `Int`. An `Int` is 16 bytes: a value in the `S64` range is held in the value itself, and a
  value outside it shares a read-only list of limbs between copies. A literal outside the `S64` range is laid down
  once by the builder and shared by each use. `List[Int]` takes 16 bytes per element (40 before), and a
  Fibonacci-products program over four 20000-digit tests runs in 1.45 s (1.86 s before).

**Syntax**
- A loop is `until c` (it stops when `c` becomes true) or `loop` (it ends only with `break`, `return` or `throw`).
  `while` is not a keyword: `while c` is `SF-G224` (`migrate --step until`). `until false` warns (`SF-W304`, quick
  fix `loop`). A loop takes no `else` (`SF-G219`). A loop's `then` block runs after `break` too.
- A line with no work is `nothing`. `pass` is `SF-G225` (`migrate --step nothing`). A type declaration with an empty
  body is `SF-S211` (write `nothing` under the header).
- A number takes its type from where it goes or from its declaration (`var x: U8 = 5`). Type suffixes (`5_u8`,
  `10n`, `2.5dn`) are `SF-G010` (`migrate --step suffix`). `inf` and `nan` are literals.
- Fixed-width numbers are visible without an import. Default `migrate` removes `import Numerics`.
- Binary floats are `B16`, `B32`, `B64`, `B128` (the build error names the `B` type for an `F` name). Complex types
  are named by their total bits: 0.1's `C64` is `C128` and 0.1's `C128` is `C256`, and `C64` is now two `B32`
  parts. Default `migrate` renames them in a project it moves (a file with no `config.toml` above it gets them
  listed instead, because the old names still build).
- `S64 / S64` is `SF-S065`. Write `a // b`, or `a.to[Decimal]() / b.to[Decimal]()`.
- Containment is a call on the collection: `xs.has(x)`, `xs.lacks(x)`, `d.has_key(k)`, `d.lacks_key(k)`. The
  `have` / `lack` operators are gone (`migrate --step have`), `contains` is `has` (default `migrate`), `x in xs` as
  a test is `SF-G212`, and `notin` is gone.
- `x but y` is `x.without(y)`, `unless c` is `if not (c)` (default `migrate`). `disobeys` is gone.
- A pattern binds with `as`: `is T as x`, `else as p` (default `migrate`). `else as x` needs exactly one type left
  (`SF-S359`).
- `Character` is `Char` and `Routine[...]` is `Callable[...]` (default `migrate`).
- Operator levels: `and` and `or` share one level, and so do the bitwise operators, so mixing them needs
  parentheses (`SF-G216`). A range binds looser than arithmetic and tighter than comparisons. `**` binds tighter
  than a leading `-`: `-2 ** 2` is `-4`. Default `migrate` writes `(-x) ** n` for each `-x ** n`, which keeps what
  the line computed in 0.1, and lists each one.
- A range counts down only with a negative step: `10 to 1` is empty, write `10 to 1 by -1`. Default `migrate` adds
  the step where both ends are literals and lists the ranges whose start is not a literal.
- A chained comparison goes one way. There is no `<<<`.
- A constructor is `routine T(...)`. `routine T.create(...)` declares an ordinary member routine named `create`.
  Default `migrate` rewrites it to `routine T(...)` (and a call `T.create(...)` to `T(...)`), and a member routine
  `create` that gives back its own type is warning `SF-W104`.
- A `var` that hides a parameter or an outer local is `SF-S008`. An unknown annotation is `SF-G221`.
- The right side of a compound assignment is checked like a binary expression (`Integer += S64` is `SF-S502`).
- `==` compares values and `===` tells whether two names refer to the same entity. Two entities with equal member
  variables are `==` (`Player("Ada") == Player("Ada")` is true) and not `===`. `===` and `!==` take entities only.
- `discard` is gone. A result you do not need takes no marker: write the call on its own line (`discard` and
  `drop` are `SF-G150`, default `migrate` removes `discard`).
- A program does not write `destruct` (`SF-S821`). An entity ends when the last name that refers to it goes away or
  when the cycle collector finds it unreachable. Default `migrate` renames `destroy`.
- `@readonly` and `@reshaping` are gone (`SF-S706`).
- A program's own files cannot use `danger`, `dangerous` or foreign routines. Code that calls C goes into a library
  the program imports (default `migrate` lists these lines).
- Choice values (`OK: 200`) and flags bits (`MINE: 0`) are written on every member or on none, and with none the
  members count 0, 1, 2, ... in order. Some with and some without is `SF-S417`.
- The flags value with no member on is `none` (`var marks: CellMarks = none`, also in presets and record presets).
  `all_off()` is gone, and its build error names `none`.
- A number type built from one number with `T(x)` (`U8(x)`, `S64(ratio)`) is `SF-S655`, because it reads the bits
  of `x` and gives another number. Convert with `x.to[T]()`, or write a number where the type is expected
  (`var level: U8 = 200`). `migrate --step numbers` rewrites the calls between binary integer and float types and
  lists the rest.

**Standard library**
- A module is its folder. The base module is `System` and every file sees it and the modules under it
  (`System/Numerics`, `System/Text`, `System/Temporal`, `System/Concurrency`, `System/Cpu`, ...) without an import.
  Importing a parent does not import its children (`migrate --step modules`).
- Math routines use the shared names: `ln`, `exp_m1`, `sin_pi`, `ln_gamma`, `mul_add`, `mod_trunc`,
  `rem`, `fract`, `compare`, `floordiv`, `shift_left`, `arith_shift_right` (`migrate --step names`).
- Float operations and math routines crash on an inf or NaN result. The raw result is `+! -! *! /! **!` and the
  `_unchecked` routines.
- A conversion is `x.to[T]()`: `S32(from: x)` and `x.S32()` are rewritten by default `migrate`, and a missing
  conversion is `SF-S458`. Collecting is a conversion too: `xs.List()` is `xs.to[List]()` (`SF-S458` names it).
- What walks a value is an emitter: `Iterable[T]` is `Emittable[T]`, 0.1's `Emittable[T]` is `Emitter[T]`, and
  `iter()` is `emitter()`. The build error on an old name names the new one.
- `x.type_name()` is `type_name(of: x)`.
- `Decimal` holds 34 significant digits and has one form per value: every result is normalized, so `1.0` and `1.00`
  are the same value, and a zero is never `-0` (`(-0.5).fixed(0)` is `0`). `+`, `-` and `*` are exact or crash with
  `NumericOverflowError`. Every rounding a `Decimal` does (`/`, `round()`, `fixed()`, text, literals, `pow`, `sqrt`
  and the other math routines) is half up: `2.5` goes to `3` and `-2.5` to `-2`. The arbitrary-precision `Real` is
  gone.
- `Decimal`'s `rem` is the Euclidean remainder, like integer `%`: `0 <= r < |b|` (`-7` by `2` is `1`).
- `Matrix4R4C.inverse()` is failable: a singular matrix throws `SingularMatrixError` (0.1 gave the zero matrix).
  The inverse is Gaussian elimination with partial pivoting, so a result that isn't exact can differ from 0.1's in
  its last bits. `inverse_unchecked()` gives the raw result (infinities or NaN for a singular matrix).
- Channels are `Mailbox[T]` (`add_last`, `add_last_if_room`, `remove_first`, `close`, `each x in m`). Default
  `migrate` lists each `make_channel` for a rewrite by hand.
- `List` keeps its elements from the first place on. The ring operations (`rotate_left`, `keep_last`, ...) are
  `RotatableList`'s (default `migrate` lists them).
- `Signals` is `CpuEventManager` in `System/Cpu`, with one handler slot per event kind (`migrate --step
  cpu-events`).
- The time is read with `Clock.now()`, `Clock.monotonic()` and `CpuClock.now()` (the build error on `Moment.now`
  says so). A `Moment` is written with two digits for each part after the year: `2026-01-15 12:30:00` (0.1 wrote
  `2026-1-15 12:30:0`).
- A collection shows itself the way its literal looks. A `ByteSize` shows its byte count grouped by `_` with a `B`
  (`{4kib}` is `4_096B`).
- `FileManager` does every file system task, and `IO/FileSystem` is gone (the import error names `FileManager`).
  Reading or writing a missing file fails, `make_file` fails on an existing file unless you pass `replacing: true`,
  and there is no `touch`. The directories and the environment variables are `Environment`'s, random bytes are
  `Entropy`'s.
- `ask_line` and `ask_word` fail with `EndOfInputError` at the end of input.
- `Dict.items()` gives `(key, value)` tuples: `each (k, v) in d.items()`. `d[k] = v` adds a missing key, and
  `d.add(key:, value:)` is rewritten to it.
- `PriorityQueue` holds `(priority, element)` tuples and has `unordered`, `ordered` and `drain`.
- `Duration` `//` and `%` follow the integer rules, and overflow or division by zero crashes.
- `Indexable` is `ViewingIndexable` and `MutableIndexable` is `ModifyingIndexable` (the build error names the new
  name, and so does the one on `PQEntry`, `gather` and `race`).
- Concurrency: `suspended` is gone. Any routine runs concurrently through `f.to[Work](args)` (default `migrate`
  adds `.to[Work]` to the calls). A piece of handed-off work is a `Work[T]` (it was `Agent[T]`), started with
  `start_now()`. `migrate --step work` rewrites `Agent` to `Work`, and a remaining `Agent` is a build error that
  names `Work`. `execute()` is gone (default `migrate` writes `start_now()`). `gather()` and `race()` are routines on
  the Work list: `jobs.gather()`. When the last name that refers to a Work goes away, the work is cancelled and
  waited for, and a failure its work came to crashes the program there. `job.cancel()` throws the failure away.
- A number several jobs change is a `global` or an entity's member variable: the builder makes each change safe.
  There is no `Atomic` (`SF-S447` names these two ways).
- Byte flows (`FileSource`, `FileSink`, `stdin()`, `stdout()`) read with `pull(max:)` and write with `push(data:)`
  (`migrate --step verbs`).

### Added

**Language**
- A routine that returns nothing ends without `return`.
- `Integer / Integer` is a `Decimal` (`7 / 2` is `3.5`). An `Integer` and a `Decimal` mix in arithmetic and give a
  `Decimal`.
- Loops carry values: `until c with total = 0`, `continue with total: v`, and `then` sees them.
- A `when` that gives a value ends a block arm with `becomes value`. One arm can match several values
  (`== 4, 6, 9, 11 =>`). An arm can be `throw ...`.
- Records and entities have `==` and `!=` without `obeys Equatable` when every member variable has `==` (a written
  `eq` replaces it). Records hash the same way, and an entity hashes only with `obeys Hashable`, so it can be a `Set`
  or `Dict` key only when it says so. A `T?` has `==` and a hash when `T` does. Entities whose member variables form
  a cycle compare and hash by shape. A missing `==` or hash is a build error that names the member variable that
  lacks it.
- Backtick names, tuple swap `(a, b) = (b, a)`, record patterns, inline variants `A | B`, `define` type aliases,
  generic variants, `@infix` routines, named and short arguments mixed (`area(width, height: 4)`), `todo`.
- Presets are computed at buildtime. Hex float literals (`0x1.8p3`) for binary floats.
- `@throws([...])` lists what a routine throws and the builder checks it.
- A crashable has no size limit. A `Check` bound to a name keeps its own error after later failures, and a failure
  in one job never overwrites another's.
- A file without a `module` header takes its module path from its place under `module_paths`.
- Collection literals take their element type from the expected type, like numbers do.

**ObjectHacker** (`import ObjectHacker`, working on live objects while the program runs)
- `Unknown` keeps a value of any type: `Unknown(from: v)`, `u.is[T]()`, `try u.to[T]()`, and `==` and a hash by the
  kept type's own, so an `Unknown` can be a `Set` or `Dict` key.
- `type_name(of:)` and `type_info(of:)`: a type's name, module, kind, member variables, protocols, cases, arms,
  routines and creators. `memvars(of:)`, `types(in:)` and `routines(in:)`.
- `peek(target:, memvar:)` gives an `Unknown`. `poke(target:, memvar:, value:)` changes an entity in place, and
  `poked(...)` gives a changed copy of a record. `watch(target:)` observes an entity without keeping it alive.
- `call(target:, name:, args:)` calls a member routine by name, `call(info:, args:)` a free routine, a common routine
  or a creator, and `create(type:, args:)` makes a value. Only routines the build reached are listed and callable
  (`RoutineNotBuiltError`): mark a type `@reflectable` to keep every routine it has.
- A program's own annotations: a record that obeys `Annotation` is written `@Memoize(limit: 100)` on a routine, a
  record or an entity. Its `apply(to:)` runs once per declaration when the program starts, and its
  `around(call: Invocation)` runs in place of every call of the routine, with `call.proceed()` running the routine.
- `when_changed(target:, handler:)` calls a handler after each change of one object's member variables, with the
  name and the old and new values as `Unknown`s, and `remove_hook` takes it off.
- `implant(target:, replacing: Hero.attack, with: ...)` replaces a member routine for one object, checked against the
  routine's signature (`SF-S201`), and `remove_implant` gives it back.
- Hooks and implants may be added, removed and used from several jobs at once.

**Standard library**
- `x.rsqrt()` is 1 / sqrt(x) rounded once (not `1 / x.sqrt()`, which rounds twice) on `B16`, `BF16`, `B32`, `B64`,
  `B128`, `D32`, `D64`, `D128` and `Decimal`. `x <= 0` crashes like the other checked float math (raw:
  `rsqrt_unchecked`), and `Decimal.rsqrt` of `x <= 0` is a `NumericDomainError`.
- `powi(n)` on `D32`, `D64`, `D128` and `Decimal` is rounded once, not once per multiplication (`1.1.powi(-7)` on a
  `D64` is `0.5131581182307068`, it was `...069`). An exact `D` power keeps the exponent `n` times `x`'s (`2.0.powi(2)`
  is `4.00`, it was `4.000`) and `x.powi(0)` is `1` (it was `1.0`). `midpoint` is `(a + b) / 2` rounded once, so two
  finite values have a finite midpoint (`D64_MAX.midpoint(D64_MAX)` is `D64_MAX`, it was `inf`) and the sum is not
  rounded first (on a `D64`, `9999999999999999.midpoint(9999999999999998)` is `9999999999999998`, it was `1.00000000000000E+16`).
- `D32`/`D64`/`D128` `is_subnormal()` and `fpclassify()` count every finite nonzero value below the smallest normal
  one (`5e-390` on a `D64`), not only those at the smallest exponent.
- `Decimal.to[B32]()` and `to[B64]()` crash with `NumericOverflowError` past the float's range, as documented
  (they gave an infinity).
- `C64`, `C128` and `C256`: `*`, `/`, `reciprocal()`, `norm()` and `abs()` round each part once from the exact value.
  On a `C128`, `(0.1+0.1i) * (0.1+0.3i)` is `-0.02+0.04i` (it was `-0.019999999999999997+0.04i`), `(0.1+0.1i) /
  (0.1+0.3i)` is `0.4-0.2i` (it was `0.39999999999999997-0.2i`) and the modulus of `0.1+0.1i` is `0.1414213562373095`
  (it was `0.14142135623730953`). A squared modulus past the range no longer crashes when the result fits:
  `(1+1i) / (1e-200+0i)` is `1e+200+1e+200i` and the modulus of `1e200+1e200i` is `1.414213562373095e+200` (both
  crashed).
- The complex square root, logarithms and inverse functions give their principal values, and a `-0` part picks the
  side of a branch cut: the square root of `-4 - 0i` is `0-2i` (it was `0+2i`), `acosh` of `-2+0i` is
  `1.3169578969248166+3.141592653589793i` (the real part was negative), `atan` of `2i` is
  `1.5707963267948966+0.5493061443340549i` (the real part was negative), and `asin`/`atanh` of `2+0i` take the side
  above the cut. `tan`, `tanh` and the inverse functions no longer lose digits to cancellation, and `sin`, `cos`,
  `sinh`, `cosh` and `exp` are made from the correctly rounded real functions, so every elementary function is within
  a few units in the last place. The other complex results that use them (`ln`, `log2`, `log10`, `pow`) can change in
  the last digit.
- `exp_unchecked`, `sinh_unchecked` and `cosh_unchecked` of a complex number give an infinity past the range (they
  crashed): `exp_unchecked` of `100+0i` on a `C64` is `inf+0i`.
- Reading a complex number from text that is not one fails with `InvalidValueError` naming the whole text and the
  complex type (it named the part and its float type), and a NaN part reads as the positive quiet NaN.
- A complex number's `hash` is the same for `+0` and `-0` parts, as `==` treats them.
- `C256.to[C64]()` rounds each part once (it went through `B64`, rounding twice).
- Collections, `Text`, `Bytes` and the IO routines count with `Integer`. `Memory` and `Span`. `List`, `Dict`,
  `Set`, `PriorityQueue` and the sorted collections are built for Suflae.
- Emitters: `var it = xs.emitter()`, then `it.emit()` or `each x in it`, which goes on from where `it` is. An
  emitter is an emittable, so `it.to[List]()`, `it.sum()` and the adapters work on it. An entity a program writes
  that obeys `Emitter[T]` or `Emittable[T]` works with every library routine written for emittables.
- An emittable collects into any collection with `to[...]`: `to[List]`, `to[Set]`, `to[Dict]` (of `(key, value)`
  pairs), `to[SortedList]`, `to[SortedSet]`, `to[SortedDict]`, `to[RotatableList]`, `to[BitList]`,
  `to[PriorityQueue]` (of `(priority, element)` pairs), and `to[Array[T, N]]`.
- `Math3D` has `Matrix2R2C` and `Matrix3R3C` beside `Matrix4R4C`, with the same routines, `vec2` and `vec3`, a 2D
  `rotation`, `rotation_x`, `rotation_y`, `rotation_z` and `from_quaternion` on `Matrix3R3C`, and `scaling`. All three
  have `inverse()`, `inverse_unchecked()` and `solve(b:)` (A x = b), computed by Gaussian elimination with partial
  pivoting. `inverse()` and `solve(b:)` throw `SingularMatrixError` when a pivot is exactly 0, and an infinity or NaN
  during the computation crashes like the other checked float operations.
- `RotatableList[T]`: a list that adds and removes at both ends and rotates.
- `jobs.start_now()` on a list of Works starts every Work in it and lets them all go together, so none runs before
  the others are ready. `gather` and `race` start the ones not started yet the same way.
- `RoundingRule` (`HALF_UP`, `HALF_EVEN`, `HALF_AWAY`, `FLOOR`, `CEIL`, `TRUNC`) for the routines that round on
  purpose: `x.round(places:, rule:)`, `x.fixed(places:, rule:)`, and on `Decimal` `divide(by:, places:, rule:)`,
  `add`, `subtract` and `multiply` with `places:` and `rule:` (computed exactly, rounded once). Leaving out `rule` gives
  the type's own rule.
- `Decimal`, `D32`, `D64` and `D128` are `Hashable`: equal values hash the same whatever their digits.
- `T(from: SerialValue)` builds a value from serial data (`SerialShapeError`).
- Changing a collection's shape during `each` over it crashes with `ReshapingWhileInUseError`, also when the change
  comes from a lambda that captured the collection, goes through an element, or the loop walks an adapter of it.
- `TextPattern` and `BytesPattern`: patterns built from named pieces, matched in time linear in the input, with
  `from_regex`, `matches`, `find`, `find_all`, `replace` and `split`.
- Deadlock detection in debug builds.
- `@test` routines and the `test` command, with `@case`, `@skip`, `@time_limit` and `@memory_limit`.
- `Environment`, `Entropy`, `Clock`, `CpuClock`, `CpuEventManager`.
- `ask[T]()`, `set_exit_code`, `to_wrap[T]()`, `to_clamp[T]()`, `to_text(radix:)`, `hex()`, `bin()`, `oct()`,
  `fixed(places)`.
- Work lists: `cancel()`, `next_finished`, `gather_all`, `take_finished`, `cancel_all`.
- `Dict.set_if_absent`, `List.swap`, `first_true` / `count_trues` / `is_any_true` on bit collections.
- Float math routines are correctly rounded on every platform, and printed floats are the shortest decimal that
  reads back to the same bits. `BF16` does arithmetic.
- `Char` case mapping follows Unicode 17.0.

**Tools**
- A resident build daemon and a JIT mode for the edit loop: `[target] mode = "debug-jit"`, `use-daemon` and
  `incremental`.
- `new`, `migrate`, `docs`, `fmt`, `lint`, `test`, `lsp` and `check --json`.
- `migrate --step indent` (indent in steps of 4 spaces). Default `migrate` and the `have` and `until` steps read a
  file whose numbers still have type suffixes, and leave the suffixes for `--step suffix`.
- The language server: diagnostics, formatting, hover, go to definition, rename and quick fixes.
- Rider plugin and VS Code extension.
- A crash trace holds the program's routines only: a standard library routine keeps no frame, and a crash
  inside one reports the line where the program called it. `[debug] trace-stdlib = true` keeps the library's
  frames too (for work on the library).

### Fixed

- An entity captured by a lambda that several jobs call, sent between jobs, or reached as an element of a list the
  jobs share is changed one statement at a time, so no update is lost.
- Two jobs adding to one `List` lost updates.
- `total += xs[0]` crashed, and a `global` `S64` lost digits.
- Globals of one name in two modules stay apart.
- `x in dict` gave a wrong error.
- A `when` with no subject, a typed `E?` local, member defaults, destructuring arms and `??` were rejected or built
  wrongly.
- Nullable entity parameters, inferred collection elements, and reassigning an entity member.
- Compound assignment to an entity member or a collection element.
- Importing a module that has globals.
- A stack overflow crashes with exit code 82 and reports that the program ran out of memory.
- A crash inside the standard library points to the caller's line.
- `Integer ** -n` crashes with `NumericDomainError`.
- `List[S64](capacity: x)` with a wrong argument names the constructor's parameters (`SF-S502`).
- A syntax error inside a value `when` no longer reports the rest of the routine as top-level statements.
- Converting the smallest `S128` or `S256` to `B128` crashed. `B128.rem` crashed on operands near the top of the
  range.
- A range that reaches the ends of its type runs to the end and stops there.

### Removed
- `Result[T]`, `Lookup[T]`, `Crashable`, `absent`, `discard`, `pierce`, and the `try_` / `check_` / `lookup_`
  routines.
- `unless`, `but`, `disobeys`, `notin`, `have`, `lack`, `while`, `pass`, `suspended`.
- `Agent.execute()`, channels and `make_channel` (use `Mailbox`).
- `Atomic` (use a `global` or an entity's member variable).
- `Iterable` (now `Emittable`), `iter()` (now `emitter()`), and the `List()` terminal (now `to[List]()`).
- The `Signals` module and `IO/FileSystem`.
- `Real`, arbitrary-precision `Complex`, the `?.` operator.
- `@readonly`, `@reshaping`, `import Numerics`.
- Number type suffixes and the `B*_INFINITY` / `B*_NAN` presets.
- Flags' `all_off()` (write `none`).

## [0.1.0] — 2026-08-28

The first Suflae release.
