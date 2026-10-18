# Suflae — Reference for AI Assistants

You are reading this because Suflae (`.sf`, v0.1, 2026) is **not in your training data**. Do not guess
syntax from Python, Ruby or Kotlin intuitions; this file lists where those intuitions are wrong. It is a
complete reference on its own: you do not need any other document to write correct Suflae.

When unsure, consult ground truth in the repo:

- `Suflae/tests/Fixtures/StdlibSf/*.sf` — Suflae programs with their expected output (`.expected.txt`).
  Start from the closest one.
- `RazorForge/Standard/` — the standard library every Suflae build reads (`Core`, `Collections`, `IO`,
  `IterTools`, `Signals`, …). It is written in RazorForge, the sibling language, but every public routine
  in it is callable from Suflae exactly as declared.
- `Suflae/Standard/` — the modules only Suflae builds read (today: `ObjectHacker`).

---

## 1. What Suflae is

RazorForge and Suflae share one object model, one grammar and one standard library, and differ on two
axes. The first is the **ownership dial: how many names one entity may have.** In Suflae, many. An
`entity` is shared: `var b = a` makes a second name for the same object, passing it to a routine hands
over another name for it, and memory is reclaimed by reference counting plus a cycle collector. Because
names are free, Suflae has things a single-owner language cannot: module-level mutable state (`global`),
cyclic structures with no weak-reference annotations, and runtime checks where the builder cannot follow
aliases (changing a collection while it is being looped over crashes at run time when the change is hidden
behind a call). A `record` is a value: copied on assignment, no identity. An `entity` is the one-of-a-kind
object: it has identity, compared with `===`.

The second axis is **machine visibility**. Suflae hides the machine. Numbers are `Integer`
(arbitrary precision, never overflows) and `Decimal` (base-10, so `0.1 + 0.2 == 0.3`). Running out of
memory is one wall with no "stack" vocabulary. There are no threads to create: concurrency is `f.to[Agent](args)`
whose work the runtime schedules, and shared entities are locked for you. A Suflae program has no
machine-level code at all: no `danger` blocks, no `dangerous` routines, no foreign `C::` routines. C is reached
through a `.rf` module the program imports (section 18).

## 2. The rules most likely to break your assumptions

1. **Calls with three or more parameters REQUIRE named arguments (SF-S510).** Two parameters may be positional
   (no warning), naming them is still clearer: `gcd(a: 252, b: 105)`. Single-argument calls may be positional. Routines annotated
   `@positional` accept all-positional calls. Mixing named and positional in one call is always an error
   (SF-S512).
2. **A routine that returns a value returns it on every path** (`return x`). A routine that returns nothing
   needs no closing `return`: it ends where its body ends. `return` still leaves one early.
3. **Entities are shared; records are copied.** `var b = a` on an entity gives a second name for the same
   object (no `steal`, no copy, no error). On a record it makes an independent copy. Collections (`List`,
   `Dict`, `Set`, …) are entities, so they are shared too: copy one explicitly with `xs.duplicate()`.
4. **Bare numbers are `Integer` and `Decimal`**, not 64-bit machine numbers. `42` is an `Integer`, `3.14` a
   `Decimal`. A literal adapts to an expected type (`var x: S32 = 5`, `xs[0]`, and a whole number written in base 10
   to a float or decimal type: `price: 5` for a `Decimal` parameter, but not `0xFF`), but variables of different
   number types never mix without an explicit conversion (`n.to[S64]()`, §5). The one exception is the two
   default types: an `Integer` and a `Decimal` mix (`+ - * /`, `==`, `<`), the result is a `Decimal`
   (`3.5 * 2` is `7`), and an `Integer` with more significant digits than a `Decimal` holds (34) crashes. Mixing
   two chosen types (`S32` and `U32`) is SF-S767, whose message names a type holding both
   (`a.to[S64]() + b.to[S64]()`).
5. **`/` divides as decimals, `//` is floor division.** `7 / 2` is `3.5` (a `Decimal`), `1 / 3` is `0.333…` to 34
   significant digits, `7 // 2` is `3` and `7 % 2` is `1`. `/` of two `Integer` values turns each into a `Decimal`
   first, and an `Integer` with more significant digits than a `Decimal` holds (34) crashes instead of rounding.
   `1.0 / 3` is `0.333…` too (§4).
6. **`routine start()` is optional.** The entry file may be a plain list of top-level statements (script
   mode). `show(...)` needs no import.
7. **Failable routines carry `!` on the DECLARATION only** (`routine parse!(...)`); call sites never write
   it. A bare call crashes loudly on failure. To recover, prefix the call with `try` / `check` and
   take the result apart with `when`. There is no try/catch and no `try_parse` name form (§11).
8. **Conditional expressions are top-level only.** `return if c then a else b` works;
   `f(x: if c then a else b)` and `(if c then a else b) + 1` are parse errors by design. Use `when` or a
   named intermediate.
9. **Changing a collection while `each`-looping it is banned.** `each x in xs` with `xs.add_last(...)` in the
   body is a build error (SF-S625); the same change made inside a called routine crashes at run time with
   `ReshapingWhileInUseError`. Change it after the loop.
10. **A result you do not need takes no marker**: `seen.add(value: v)` on its own line simply drops the `Bool`.
11. **A module-level `var` is an error (SF-S435).** Module-level mutable state is `global name: Type = init`
    (§6). Constants are `preset`.
12. **Blocks are indentation, four spaces per level.** No braces, no semicolons, no `:` after `if`.
13. **Type arguments use `[...]`, never `<...>`:** `List[Text]`, `Dict[Text, Integer]`. `<` is a
    comparison, and chained comparisons (`0 <= x <= 10`) are one expression.
14. **Containment is a call on the container:** `xs.has(3)`, `"hello".has("ell")`, `xs.lacks(3)`, and a dict's
    keys are `d.has_key("key")` / `d.lacks_key("key")` (not `.has`). Flags test with `has` / `has_any` / `lacks`.
    `x in xs` as a test is a grammar error (SF-G212); `in` belongs only to `each x in xs`.
15. **There is no `class`, `def`, `fn`, `let`, `const`, `for`, `match`, `async`, `await`, `try`/`catch`,
    `null`, `print`, `main` or `pass` statement.** The words are `entity`/`record`, `routine`, `var`,
    `preset`, `each`, `when`, `to[Agent]`, `retrieve()`, `try`/`check`, `none`, `show`, `start`.

## 3. Program shape

Script mode — loose statements run top to bottom as the body of an implicit `start()`:

```suflae
var total = 0
each n in 1 to 5
    total += n
show(f"total = {total}")
```

With an explicit entry point (most fixtures use this form):

```suflae
module Tools/Greeter

routine greet(name: Text) -> Text
    return f"Hello, {name}!"

routine start()
    show(greet(name: "Suflae"))
    return
```

- Declarations (`import`, types, routines, `preset`, `global`) may sit among script statements; they are
  hoisted.
- Mixing loose statements with an explicit `routine start()` is an error (SF-G150). Choose one.
- Only the program's entry file may be a script. Loose statements in an imported file are SF-S443.
- A top-level `var` in a script belongs to the script's own scope, not the module: routines in the file
  cannot see it (SF-S444). Pass it as an argument, or make it a `global`.
- Comments start with `#`. Doc comments use `###` on the lines before a declaration.

**Modules.** `module <Path>` is optional: without it the module path comes from the file's location
relative to `config.toml` (each folder and the file name PascalCased, joined with `/`:
`tools/text utils.sf` → `Tools/TextUtils`). Declare `module` only to override that.

**Imports.** `import Foo/Bar` imports a module; `import Foo.bar` imports one member (a type, routine,
`preset` or `global`) of module `Foo`. Always available without an import: `Core` (the basic types,
`List`/`Dict`/`Set`, `Maybe`, errors, `Agent`, `Mailbox`), `Integer`, console output and input
(`IO/Console`), and files (`IO/File`). Import the rest: `Collections` (`SortedDict`, `BitList`,
`PriorityQueue`, …), `IterTools` (iterator adapters), `Numerics`,
`Signals`, `BuilderQuery` (`x.type_name()`), `ObjectHacker`. Writing an import that is
already in the prelude (many fixtures write `import IO/Console`) is harmless.

## 4. Naming and vocabulary

- function → **routine**; method → **member routine**, declared outside the type as
  `routine Type.name(...)`; the receiver is `me`. `Me` (capital) is the receiver's type.
- class/struct → **`entity`** (shared object with identity) or **`record`** (value). Enum → **`choice`**;
  bit set → **`flags`**; sum type → **`variant`**; error type → **`crashable`**; interface/trait →
  **`protocol`**, adopted with **`obeys`**.
- field → **member variable**, a bare `name: Type` line in the type body (no `var`). Visibility is written
  only when restricted: `posted` (anyone reads, only the module writes) and `secret` (module-private).
  Giving a `secret` or `posted` member variable a value in `T(field: value, ...)` is allowed only inside the
  type's module (SF-S403 / SF-S404): outside, build it with a routine the module opens (`HttpResult.ok(body: b)`).
- Generic constraints use **`needs`** (`needs T obeys Comparable`), never `where`.
- Types and protocols are `PascalCase`; routines, variables and member variables `snake_case`; `choice` /
  `flags` cases and `preset` constants `SCREAMING_SNAKE_CASE`.
- No kind or scope prefixes on names (`m_pos`, `_count`, `p_node`, `bDone`, `IComparable`): `lint` hints them.
- Generic parameters: a type parameter is `T` alone only when it is the only one and its role is obvious
  (`List[T]`, `Set[T]`, `Agent[T]`). Otherwise every type parameter is `T` + a PascalCase role (`Dict[TKey, TValue]`,
  `select[TResult]`, `zip[TFirst, TSecond, ...]`), never another single letter (`K`, `V`, `U`, ...). A const generic
  is `SCREAMING_SNAKE_CASE` with a meaning (`needs U64 CAPACITY`, `COUNT`, `BITS`), never `N`. The `T` prefix shows
  at a use site (`var k: TKey`, a hover `get(key: TKey) -> TValue?`) that a name is a type parameter and not a real
  type, and SCREAMING marks a buildtime constant like a `preset`, so case alone tells type, type parameter, buildtime
  constant and runtime value apart. A receiver pattern's own parameter shadows the owner's parameter of the same name
  (`routine List[Agent[T]].gather() -> List[T]`: that `T` is the pattern's).
- Builder-written per-type routines (the constructor, `represent`, `diagnose`, `eq`, `cmp`, `hash`, …) are
  **wired routines**. They carry no sigil; you may write your own to replace one.
- Say "build error" and "builder", not "compile error" and "compiler".

## 5. Types and literals

| Kind                    | Types                                                                      |
|-------------------------|----------------------------------------------------------------------------|
| Default numbers         | `Integer` (arbitrary precision), `Decimal` (base-10)                       |
| Fixed-width integers    | `S8 S16 S32 S64 S128 S256`, `U8 U16 U32 U64 U128 U256`                    |
| Binary floats           | `B16 B32 B64 B128` (`BF16` is a storage-only format)                       |
| Decimal floats          | `D32 D64 D128`                                                             |
| Complex / quaternion    | `C64 C128 C256`, `Q128 Q256`                                               |
| Text and bytes          | `Text` (Unicode), `Char`, `Bytes`, `Byte`                             |
| Other                   | `Bool`, `Duration`, `ByteSize`, `Moment`/`LocalMoment`, tuples `(A, B)`    |

- Every type in the table is usable with no import. There is no arbitrary-precision binary float or complex:
  `B128` and `C256` are the widest, and exact arithmetic is `Integer` and the decimals.
- **Counts, positions, widths and amounts are always `Integer`**, whatever the value's type: a fixed-width
  number keeps its own type, but `x.count_ones()`, `x.leading_zeros()`, `x.ilog2()` and `x.signum()` return
  `Integer`, and shift and rotate amounts (`x.rotate_left(bits: 3)`, `x.ashr(bits: 1)`), radixes
  (`x.to_text(radix: 16)`), places (`f.fixed(places: 2)`) and exponents (`f.scalbn(n: 2)`) are `Integer`.
- **`Integer`** never overflows. **`Decimal`** is exact for decimal fractions (`0.1 + 0.2 == 0.3`, money is
  safe) with 34 significant digits; a result needing more digits (`1 / 3`) is rounded to 34, and a result
  beyond about `1e6144` crashes. It displays canonically: `2.50` shows as `2.5`.
- Literals: `1_000_000`, `0xFF`, `0o777`, `0b1010`, `3.2e5`. Imaginary: `3 + 4i` (`C128` with no context).
- A number carries no type of its own: it takes the type of the place it goes (an argument, an assignment,
  the other side of an operator, and a whole number written in base 10 goes where a float or `Decimal`
  goes too), and otherwise it is an `Integer` (a `Decimal` with a fraction point). Where another type is
  wanted with nothing around to give it, the declaration states it: `var x: U8 = 5`. A variable's type is
  never guessed from its later uses. A number that names its type (`5_u8`, `7s32`, `10n`, `2.5dn`) is
  SF-G010, and `migrate --step suffix` rewrites old code.
- `inf` and `nan` are number literals (reserved words) that take their type from where they go: `var x: B64 = inf`,
  `limit: B32 = -inf` (negative infinity is `-` on `inf`), `return nan`, `x < inf`. The binary floats (B16 B32 B64
  B128) and D32 D64 D128 take them. With nothing giving a type (`var x = inf`) it is SF-S019, and where a
  `Decimal`, `Integer` or integer type goes it is SF-S020 (a `Decimal` is always finite).
- A hex float (`0x1.8p3`, `0x1p-23` where a `B32` goes) is binary-float only and exact, and defaults to
  `B64`, never `Decimal`.
- A literal out of range for its type is a build error (SF-S010): `-1` never fits an unsigned type; write
  `U8_MAX`, `U64_MAX`.
- Durations: `50ms`, `5s`, `2m`, `1h`, `1d`, `1w` (also `us`, `ns`). Sizes: `4kb`, `4kib`, `2mb`, `2mib`,
  `1gb`, `1gib`.
- Duration `//` and `%` leave a remainder that is never negative (`-10s // 3s` is `-4`), and overflow or `// 0s`
  crashes. `Moment(year:, …)` crashes with `InvalidMomentError` on a part out of range (month 13, February 30,
  hour 25). `Char` case routines follow Unicode (one character to one, `ß` stays `ß`). `lines()` and `ask_lines()`
  start no new line after a final line ending.
- `var x: T` with no value is `lateinit var x: T`: every path gives it a value before a read, or SF-S202 (a `return`
  path doesn't count, a loop is left with what its `break`s assigned). An empty flags value shows as `none`, a
  one-item tuple as `(1,)`. `try sv.to[List[Integer]]()` recovers a bad element too. A cancelled agent nothing
  started is done (`is_done()` is true).
- A `Moment` shows as `2026-03-05 09:00:00` (each part zero-padded), a `LocalMoment` adds `Z` or `+09:00`.
- Text: `"..."`, formatted `f"x={x}"`, raw `r"C:\path"`, raw formatted `rf"..."`. `{{` / `}}` are literal
  braces in an f-string. Escapes: `\n \t \r \\ \" \' \0` and `\uXXXXXX` (six hex digits). Characters:
  `'A'`, `'한'`. Bytes: `b"abc"`, `br"..."`, a single byte `b'x'` (ASCII only; `\xFF` escapes).
- Tuples: `(1, "a")` has type `(Integer, Text)`; read `t.item0`, `t.item1`; destructure with
  `var (q, r) = pair`. `(a, b) = (b, a)` swaps places (locals, member variables); any other tuple on the left is
  a build error.

**Conversions: `x.to[T]()`, the one way to convert.**

```suflae
var small = count.to[U8]()        # Integer to U8 (a bare literal is an Integer: 200.to[U8]())
var n = "42".to[S64]()             # text parsing (also Integer, Decimal, B64, ...)
var code = 'A'.to[U32]()           # a Char's codepoint (b.to[U8]() for a Byte)
var letter = code.to[Char]()       # 'A' when code is a U32 of 65, and 'x'.to[Text]() makes one-character text
var byte = bits.to[Byte]()         # the same bits as a Byte, from a U8
var raw = status.to[S32]()         # a choice's value (also to[S64]()), perms.to[U64]() a flags bitmask
var wide: U64 = count.to()         # T left out: the declared type gives it
var fits: U8? = try count.to()     # none when it does not fit
var low = count.to_wrap[U8]()      # the low 8 bits (300 -> 44), never fails
var near = count.to_clamp[U8]()    # the nearest U8 (300 -> 255), never fails
var why: Check[S64] = check line.to()
```

- `Integer` converts to every fixed-width integer (`S8..S256`, `U8..U256`), to `Decimal`, and to
  `B32`/`B64` (the nearest value, as does `Decimal`). Widening
  never fails. A value that does not fit `T`, or text that does not read as a `T`, crashes loudly
  (`IntegerOverflowError`, `InvalidValueError`, ...). Recover with `try` (gives `T?`) or `check` (gives
  `Check[T]`, holding the error).
- `[T]` may be left out where the surrounding code says the type: a declared variable type, the routine's
  return type (`return count.to()`), a parameter (`takes(n: x.to())`), and through `try`/`check`. Where
  nothing gives it (`var n = count.to()`, mid-chain `count.to().add(1)`) it is SF-S645: write
  `.to[S64]()`.
- A pair with no conversion is SF-S646 at the call ("There is no conversion from 'Text' to 'Bool'.").
  Converting a value to its own type is SF-S646 too (use the value as it is).
- Constructor or conversion, by one checkable rule: a constructor's arguments become parts of the new value as
  they are (`Point(x: 1, y: 2)`, `Retained(from: steal e)`, `SerialValue(from: hp)` putting an `S64` in an arm);
  a value computed from one value of another type is a conversion, `x.to[T]()` (`"hi".to[CStr]()`,
  `local.to[Moment]()`, `ratio.to[Q128]()` from a `B32`). So `S64(from: x)` is not a routine. A variant's arm comes out
  the same way: `sv.to[S64]()` / `sv.to[Dict]()` is failable (another arm is active → crash, `try` gives
  `None`). The old spellings `sv.S64()`, `S64(from: sv)`, `Dict[Text, SerialValue](from: sv)` are gone (SF-S458 /
  SF-S646 point at `.to[...]()`).
- Each conversion is its own routine, written with the target type in the bracket:

  ```suflae
  routine Fahrenheit.to[Celsius]() -> Celsius
      return Celsius(degrees: (me.degrees - 32.0) * 5.0 / 9.0)
  ```

  A call picks the one whose bracket matches (`f.to[Celsius]()`, or `var c: Celsius = f.to()`). A
  conversion that can fail is declared `routine Text.to![S64]() -> S64`.

**Optionals and `none`.**

- `T?` is the optional type (`Maybe[T]` for values). For an entity, `E?` is a handle that may be empty.
- `None` (capital) is the type and pattern (`is None`, `isnot None`); `none` (lowercase) is the value.
- `none` is legal only where the target type can be absent: `T?`, a variant with a `None`
  member, an `E?` slot. `var x = none` is an error: `none` has no type of its own.
- Reading through an `E?` before checking it is SF-S619; check with `if e isnot None` (or return early on
  `if e is None`) and the name is narrowed after the check. Putting `none` into a non-optional `E` slot is
  SF-S252: declare the slot `E?`.

## 6. Variables, constants and globals

```suflae
var count = 0                       # Integer
var ratio: Decimal = 0.75
var name: Text                      # declared now, assigned before it is read
preset MAX_RETRIES: Integer = 5     # constant: explicit type, inlined at each use
```

- There is no `let` and no `const`: `var` for variables, `preset` for constants. A `preset` may be an
  f-string built from other presets. A preset is computed when the program is built: one that calls a routine,
  does arithmetic or fills in a text runs once during the build and becomes the value it came to (a number, a
  Bool, a Text, a Char, or a record, tuple or choice of those; SF-S258 if it fails).
- **No shadowing inside a routine**: a `var` may not reuse the name of a parameter or of a variable of an
  enclosing block (SF-S008). Sibling blocks may each declare their own `i`.
- Compound assignment works: `+=`, `-=`, `*=` and the like.

**`global` — module-level mutable state.**

```suflae
global hits: Integer = 0
global greeting: Text = f"hits so far: {hits}"

routine record_hit()
    hits = hits + 1
    return

routine start()
    record_hit()
    record_hit()
    show(f"{hits}")
    return
```

- The type annotation and the initializer are both required; there is no `var` (a global is mutable by
  definition). A bare module-level `var` is SF-S435.
- A global is read and written by bare name from any routine in the module, and another module can
  `import Mod.hits` and read and write the same storage.
- Globals are initialized once, before any of your code runs, in **dependency order**: when one global's
  initializer reads another (directly or through free-routine calls), the one it reads goes first, so
  declaration order does not matter. A cycle, a self-reference included, is SF-S436. A global whose
  initializer reads another global must be a number, `Text` or `Bool` (SF-S437). A read hidden behind a
  member-routine call (`global a: Integer = x.foo()`) is not followed by this ordering.
- A program with globals needs an entry point (an explicit `start()` or a script) (SF-S438).
- **Every global is safe to use from concurrent agents.** A single-statement read-modify-write
  (`hits = hits + 1`, `name = name + "!"`) is atomic. For fixed-width integers (`S8`..`S64`, `U8`..`U64`)
  and `B32`/`B64`, `g = g + d` and `g = g - d` compile to one lock-free atomic operation, which **wraps on
  overflow** instead of crashing. Every other type (`Integer`, `Decimal`, `Text`, records, wider numbers)
  is serialized through a lock. A logical update spread over several statements
  (`var t = g` … `g = t + 1`) is not atomic; serialize it yourself.
- Use a global for state the whole process has one of (a log, configuration, a registry); pass per-task
  context as parameters.

## 7. Operators

- Arithmetic: `+ - * / // % **`. On `Integer`, `+ - *` never fail; `/` does not exist. On fixed-width
  integers, `+ - *` are **checked** (overflow crashes; recover with `try a + b`), `+% -% *%` wrap, and
  `+^ -^ *^` clamp to the type's range. On binary floats `B16..B128` and `D32..D128`, `+ - * / **` crash
  on a non-finite result and `+! -! *! /! **!` follow raw IEEE 754 (infinity and NaN pass through).
  Division by zero crashes (`DivisionByZeroError`), on the complex types too. On the floats, `Decimal`
  and the complex types, `0 / 0` crashes with `NumericDomainError` instead.
- Comparison: `== != < <= > >=`, chained in one expression: `0 <= x <= 10`. A chain goes one way: `<`, `<=`,
  `==` together or `>`, `>=`, `==` together (`a < b > c`, `!=` and `<=>` in a chain are errors). Every operand of a chain is
  evaluated once, eagerly, left to right, before any comparison (`a < b < f()` calls `f` even when `a < b`
  is false), and only the comparisons stop at the first false link, so a failure in any operand is a failure
  like any other (`try 1 < 5 < 10 // n` recovers it). `==` compares values (it calls `eq`); `===` / `!==`
  ask whether two entities are the same object (§10).
- Logic: `and`, `or`, `not` (short-circuiting). Bits on integers: `&`, `|`, `^`, `~`, shifts `<<`, `>>`
  (sign-filling), `>>>` (zero-filling); there is no `<<<`. Shifting by the width or more gives 0 (or the
  sign fill for `>>`). A shift amount and a subscript index may be an `Integer` or any fixed-width integer
  (`xs[i]` with `i: S64`); a negative one crashes.
- Infix routines: a member routine with one parameter, marked `@infix`, is written between its operands
  (`a dot b` = `a.dot(b)`), left to right, looser than ranges and tighter than comparisons
  (`(a dot b) / n` needs the parentheses, `a dot b > 0` does not). Unmarked: SF-S706.
- Containment: `coll.has(x)`, `coll.lacks(x)`, on a dict `d.has_key(k)` / `d.lacks_key(k)`. Ranges: `1 to 5` (inclusive), `1 til 5` (exclusive),
  `1 to 10 by 2` (step). Without `by` a range counts up: when the end is below the start it is empty
  (`2 to 1` yields nothing, `(2 to 1).has(1)` is `false`). A negative step counts down: `10 to 1 by -1`,
  `10 to 0 by -2`, also on unsigned types (`3 to 0 by -1` over `U64` yields 3 2 1 0). A step held in a variable
  decides the direction by its sign. A literal `by 0` is SF-S516, and a zero step held in a variable crashes
  with `RangeStepZeroError` when the range is iterated. `(0 to 10).has(10)` is `true`, and `has` follows the
  step (`(10 to 0 by -2).has(4)` is `true`). Slicing with a counting-down range reverses: `text[4 to 0 by -1]`.
- Optionals: `m ?? fallback` takes the value or the fallback; `m!!` takes the value and crashes if absent.

**Precedence**, tightest first:

1. indexing, member access, calls
2. `**` (right-associative, the exponent may carry a sign: `2 ** -1`)
3. unary `-`, `not`, `~` (`-x ** 2` is `-(x ** 2)`)
4. `* / // %` (and their `%`/`^`/`!` variants)
5. `+ -` (and their variants)
6. bitwise `<< >> >>> & ^ |`, one level: different bitwise operators side by side need parentheses
   (`(a & b) | c`, `(a >>> 58) << 2`), a chain of one does not (`a | b | c`). Shifts are looser than
   arithmetic: `1 << n + 1` shifts by `n + 1`.
7. ranges `to` / `til` / `by` (`0 to n - 1` reads as written, a range test is a call: `(0 to 10).has(x)`)
8. comparisons `== != < <= > >= <=>` (chained), `is` `isnot` `obeys` (`x & 1 == 0` is
   `(x & 1) == 0`)
9. `and` / `or`, one level: mixing them needs parentheses (`a or (b and c)`), a chain of one does not
10. `??`

Mixing `and` with `or`, or two different bitwise operators, without parentheses is SF-G216.

## 8. Control flow

```suflae
if x == 3
    show("three")
elseif x > 3
    show("big")
else
    show("small")

if not (list.is_empty())
    show("has items")

each x in 1 to 5
    if x == 2
        continue
    if x > 4
        break
    show(f"x={x}")

each (i, word) in words.enumerate()
    show(f"{i}: {word}")

while n < 10
    n += 1

loop
    n += 1
    if n >= 20
        break
```

`each` works over ranges, collections, text (one `Char` at a time), dictionaries (each entry has
`.key` and `.value`) and anything that `obeys Iterable`. `enumerate()` and `zip(a:, b:)` (from
`IterTools`) produce tuples that `each (a, b) in ...` takes apart.

`until c` loops while `c` is false (`while not (c)`). A `while`, `until`, `each` or `loop` may end with a `then`
block. It runs once whenever the loop is left for the line after it: when the loop ends by itself and after its own
`break` (a `break` of a loop inside only leaves that loop). A `return` or `throw` leaves the routine and skips it. A
loop whose `then` ends in `return` always returns, so the routine needs nothing after it. Loops have no `else`
(SF-G219): the case where the loop would not run even once is an `if` before the loop.
A loop may carry values from one round to the next: `while n > 1 with n: Integer = start, steps: Integer = 0`
(`each x in xs with sum: Integer = 0` too). Each carried value is bound anew every round, from what the round's
`continue with n: after, steps: steps + 1` passes (every carried name once, in any order, all computed before any
is stored, so `continue with x: y, y: x % y` swaps). The names are visible in the condition, the body and the
`then` block, and nowhere after the loop. A body that carries values must not run off its end: every path ends in
`continue with`, `break` or a `return` (SF-G217), a bare `continue` in such a loop is SF-G218, as is `continue with`
in a loop without `with`. `break` takes no values.

```suflae
routine collatz_steps(start: Integer) -> Integer
    while n > 1 with n: Integer = start, steps: Integer = 0
        var after = if n % 2 == 0 then n // 2 else 3 * n + 1
        continue with n: after, steps: steps + 1
    then
        return steps
```

**`when` is the pattern match**, as a statement or an expression:

```suflae
routine classify(n: Integer) -> Text
    when n
        == 0 => return "zero"
        < 0 => return "negative"
        > 100 => return "huge"
        else => return "positive"

routine describe(v: Number) -> Text
    when v
        is S64 as n and n > 100 => return f"big {n}"
        is S64 as n => return f"int {n}"
        is Text as t => return f"text {t}"
        is None => return "nothing"
        else => return "other"
```

One arm takes several values after `==`: `== 4, 6, 9, 11 => return 30`.

- Arms: comparison arms (`== value`, `< value`, …), type arms (`is T`, `is T as name`), a guard after a
  binding (`is S64 as n and n > 100`), tuple arms (`(0, y) => ...` on a tuple subject), and `else` /
  `else as name` last. A failed guard falls through to the next arm. `else as name` is allowed only when one
  type is left after the earlier arms, and `name` has that type (the value of a `T?` after `is None`, the last
  member type of a variant, every error of a `Check` taken as one type). With more left it is `SF-S359`.
- Record patterns, written like construction: `is Monster(hp: 0) as m`, `is Monster(hp: < 5, name)`,
  `is Chest(item: is Weapon as w)`, `is Monster(hp: as health)`. `field` alone names it; a value equals; an arm
  that tests a member variable does not cover its type. Also after `if`: `if c is Monster(hp: 0) as m`.
- Without a subject, `when` picks the first true condition:

```suflae
var label = when
    n > 10 => "big"
    n > 3 => "medium"
    else => "small"
```

- In a `when` statement an arm may be a block: break the line after `=>` and indent the statements one level,
  as in an `if` body. Keep a one-statement arm on one line. In a `when` that gives a value, a block arm ends with
  `becomes value`, the arm's value (without it, SF-S307).

```suflae
when terrain
    == Terrain.SPIKE_TRAP =>
        hero.position = cell
        spring_trap()
    == Terrain.MONSTER => battle(cell)
    else => hero.position = cell
```

- An expression `when` takes one expression per arm after `=>`. Several statements in an arm are a build error
  (SF-S307): compute the value first, or write a `when` statement that sets a variable.
- `if x is T as name` tests and binds in one step; the binding exists only inside that branch.
- `when` must cover every case of a `choice`, a `variant`, or a carrier's success arm; `else` covers the
  rest.

## 9. Routines

```suflae
routine add(a: Integer, b: Integer) -> Integer
    return a + b

routine greet(name: Text, prefix: Text = "Hello") -> Text    # default value
    return f"{prefix}, {name}!"

routine Account.deposit(amount: Integer)                       # member routine, receiver `me`
    me.balance = me.balance + amount
    return
```

- Call with named arguments in any order: `add(b: 2, a: 1)`, `greet(name: "Ada")`. A variable named like its
  parameter goes by its name alone, also beside written names: `area(width, height: 4)`. A member routine is
  called on its receiver: `acct.deposit(amount: 5)`.
- Return types are always written, and a routine with no `->` returns nothing. A parameter may be written
  without a type (`routine describe(shape) -> Text`): the routine is made and checked for each type it is called
  with, and a value that lacks what the body uses is a build error naming that use ("(in 'describe' used with
  shape: Blob)"). A variadic parameter is written `name...: Type`.
- Routines are declared at module level or as `routine Type.name`; there are no nested routines.
  `secret routine helper(...)` keeps a helper module-private. `common routine Type.name(...)` is a
  type-level routine with no `me`, called as `Type.name(...)`.
- A constructor is the type's name: `Point(x: 1, y: 2)` (the wired memberwise one), or one you write as
  `routine Celsius(degrees: B64) -> Celsius` (`routine Celsius!(...)` when it can fail). A conversion is
  a routine of its own, `routine Fahrenheit.to[Celsius]() -> Celsius` (§5).
- **Lambdas** are single expressions: `x => x * 2`, `(a, b) => a + b`. They may read the surrounding
  variables directly. Routine types are written `Callable[(Integer,), Integer]` (the parameter list is
  always a tuple: `()`, `(T,)`, `(A, B)`). A free routine's bare name is a value:
  `nums.select(transform: double)`.
- Any routine can run concurrently: `f.to[Agent](...)` defers a call into an `Agent[T]` (§16).
- **Annotations are a fixed set** the builder gives an effect (`@positional`, `@infix`, `@inline`, `@crash_only`,
  `@test`/`@case`/`@skip`/`@time_limit`, …). Any other name is SF-G221, and a misspelling names the close one. There
  is no `@deprecated`, no `@initonly` and no `@bench` yet.
- **Routine tests.** `suflae test` builds a file once per `@test` routine, calling it in place of the program's own
  statements: once per `@case(input: (a, b), output: r)` (arguments in parameter order, the result compared with
  `==`), or once with no arguments when there is no case. `@skip("reason")` reports it skipped, `@time_limit(1s)`
  stops it (units `ns us ms s m h d w`), `@memory_limit(64kib)` fails it when the program holds more memory than that
  at any point (units `b kb mb gb kib mib gib`). Lines read `ok    file::routine`.

## 10. Records, entities, choices, flags, variants

```suflae
record Point                 # value: copied, no identity
    x: Integer
    y: Integer

entity Account               # object: shared, has identity
    owner: Text
    balance: Integer

entity Node
    value: Integer
    next: Node?              # an entity slot that may be empty

routine start()
    var p = Point(x: 3, y: 4)
    var q = p                # independent copy
    q.x = 99                 # p.x is still 3

    var a = Account(owner: "Ada", balance: 100)
    var b = a                # the same account under a second name
    b.balance = 150          # a.balance is now 150
    var c = Account(owner: "Ada", balance: 150)
    show(a === b)            # true: same object
    show(a === c)            # false: another object with the same contents
    return
```

- Construction is memberwise and named: `Point(x: 3, y: 4)`.
- An entity passed to a routine, stored in a collection or put in another entity's member variable is the
  same object everywhere; a change through any name is seen through every name.
- **Memory is automatic.** Each entity is reference-counted and freed when its last name goes away, and a
  cycle collector reclaims structures that point at each other (a doubly-linked list, a parent ↔ child
  pair). There are no weak-reference annotations, no `destruct` to write or call, and no lifetimes. For a
  resource that must be released at a known point (a file), use `using` (§15).
- **Identity**: `===` / `!==` compare whether two entity names refer to the same object. On a record or a
  number they are a build error (SF-S440): values have no identity, use `==`.
- **Equality and order are opt-in.** `obeys Equatable` gives `==`/`!=` (the builder compares member by
  member unless you write `routine T.eq(you: T) -> Bool`); `obeys Hashable` lets the type be a `Dict` key
  or `Set` element; `obeys Comparable` with a `routine T.cmp(you: T) -> ComparisonSign` (returning
  `ComparisonSign.ME_SMALL` / `SAME` / `ME_LARGE`) gives `< <= > >=`. Text and numbers already have all
  of these.
- Every type gets `represent()` (what `show` and `f"{x}"` print, e.g. `Point(x: 3, y: 4)`) and
  `diagnose()` (the debug form, `f"{x:?}"`). Write your own to change them.

```suflae
choice Color
    RED
    GREEN
    BLUE

choice HttpStatus
    OK: 200
    NOT_FOUND: 404

flags Permission
    READ
    WRITE
    EXECUTE

variant Number
    S64
    B64
    Text
    None
```

- **choice**: one of a fixed set, compared with `==` (`c == Color.RED`, `when c` with `== Color.RED =>`
  arms). Cases may carry values (`OK: 200`). Equality, order and hashing come with every choice.
  `Color.all_cases()` gives every case as a `List[Color]`, and `Color.count()` how many there are (an
  `Integer`); flags have the same two for their members.
- **flags**: a set of options combined with `and` (`Permission.READ and Permission.WRITE`), tested with
  `has` / `has_any` / `lacks` / `==` (`perms.has(WRITE)`, `perms.has(READ and WRITE)` is all of them,
  `perms.has_any(READ and WRITE)` at least one, `perms.lacks(READ and WRITE)` none), and taken off with `without` (`perms.without(WRITE)`: where a
  value of the flags type goes, its members are named bare).
- **variant**: one value of one of the listed types; assign any member directly (`var n: Number = "hi"`,
  `var d: Number = none`) and take it apart with `when` / `is T as name`. Written in place: `-> Integer | Text`,
  `List[Circle | Square]` (same types in any order = one type; a smaller one goes where a larger one is expected;
  `define Circle | Square as Shape` names it). A value of a member goes in as an argument, initializer, `return` or
  new value.
- **crashable**: an error type (§11).

## 11. Errors and failure

**Failable routines.** A routine that can fail is declared with `!` and fails with `throw`. A routine with
no value to give throws `AbsentValueError()`, an ordinary standard error:

```suflae
crashable NopeError
    what: Text

routine NopeError.crash_message() -> Text
    return f"nope: {me.what}"

routine parse!(n: Integer) -> Integer
    if n < 0
        throw NopeError(what: f"{n} is negative")
    return n * 10

routine find!(items: List[Text], name: Text) -> Integer
    each i in 0 til items.count()
        if items[i] == name
            return i
    throw AbsentValueError()
```

- `throw` in a routine declared without `!` is SF-S750. A `!` routine that never throws is an error too.
- Call sites never write `!` (`parse!(n: 1)` is SF-G211). **A bare call crashes loudly on failure**: the
  error's title and message, where it was thrown, and a stack trace go to stderr.

**Recovering.** Put a keyword in front of the expression:

| Keyword       | Result                           | Holds                                      |
|---------------|----------------------------------|--------------------------------------------|
| `try EXPR`    | `Maybe[T]`, written `T?`         | the value, or `None` (any failure)         |
| `check EXPR`  | `Check[T]` = `T \| Crashables`   | the value, or the caught error             |

```suflae
routine start()
    when check parse(n: -1)
        is Integer as v => show(f"ok {v}")
        is NopeError as e => show(f"caught: {e.what}")
        is Crashables as e => show(f"{e.crash_title()}: {e.crash_message()}")

    var m = try parse(n: 4)
    when m
        is None => show("failed")
        else as v => show(f"got {v}")

    var safe = try parse(n: -5) ?? 0
    show(f"{safe}")

    when check find(items: ["a", "b"], name: "z")
        is AbsentValueError => show("not found")
        is Integer as i => show(f"at {i}")
        is Crashables as e => show(f"failed: {e}")
    return
```

- **With a keyword in front, every failure beneath it is recovered, at any depth; only `pierce` still
  crashes.** That covers a `throw` in the called routine and in every routine it calls, bare calls
  included (failable or not: `try f()` also recovers a division by zero inside a plain helper `f` calls), a
  checked operation (`try a + b` recovers a fixed-width overflow, `check a // b` a division by zero,
  `x += d`), an index out of range or a missing key (`try items[9]`), and a failing conversion
  (`300.to[S8]()`, `"x".to[Integer]()`). Each becomes the result's failure (`try`: `None`, `check`: the
  error), and evaluation stops at the first one. A bare call still crashes loudly. Lambdas and routine values are covered the same way: a failure
  inside a lambda handed to another routine (`try items.select(transform: x => 10 // x).to[List]()`,
  `check items.sort_by(compare: f)`, a routine that calls the value it was given, a routine value passed down
  several levels or kept in a record) is recovered by the keyword above the call that reaches it, at any
  depth. A routine value called with no keyword above that call still crashes on failure. An `each` loop over
  an adapter holding a lambda ends only when its source ends: a failure inside the lambda is the loop's
  failure, recovered by a keyword above the loop or crashing without one. Not covered: a crash inside a
  library routine declared without `!` (other than in a routine value you handed it) stays a crash, and the
  fatal walls below stay fatal.
- The keyword reaches up to `??`: `try f() ?? d` means `(try f()) ?? d`.
- **`pierce`** is the failure that must not be recovered: `pierce NopeError()` crashes like an unrecovered
  `throw` (status 82), and it crashes the same way under `try`/`check` at any depth. Use it for a
  broken invariant (state the program can no longer trust), never for a condition a caller could handle. It
  does not make a routine failable (no `!` needed for it).
- **`todo`** is a statement for code not written yet (`routine parse(text: Text) -> Header` with the body `todo`):
  running it crashes with `NotImplementedError` naming the routine, like `pierce`. It does not make the routine
  failable and ends its path, so a body that is only `todo` needs no `return`. The build warns once per routine
  that still has one (SF-W303). `pass` is an empty body on purpose, a different thing.
- Typical use: `try` when only success matters, `check` when the kind of failure matters. "Not found" is
  the arm `is AbsentValueError` of a `check` result, so it can be told apart from every other failure.
- A routine that returns nothing gives `Check[None]` under `check` (its success arm is `is None`) and `Bool`
  under `try` (`true` when it succeeded).
- A `when` on a `Check` **must** have the success arm (`is T as v`, or `else as v` when every error is already
  taken by `is Crashables as e`); leaving it out is
  a build error. `is NopeError as e` takes only that error and binds it as a `NopeError` (its member variables
  are readable); `is Crashables as e` takes any error, so write it after the specific ones; `else as e` after the
  success arm takes every error that is left, as `is Crashables as e` would. `x is NopeError` also works as an expression.
- When the success value is itself a variant, the arms may name the types it holds instead of the whole value
  (`is Deposit as d`, `is Withdraw as w` on `check parse(...)`), and together they must name every one of them.
  The same holds on a plain variant that holds another variant.
- **An error arm is optional.** With no arm for the error, the `when` passes it on: inside a `!` routine
  as if `is Crashables as e => throw e` were written (the caller receives it), anywhere else the program
  crashes loudly. `throw e` on a caught `Crashables` rethrows the same error. A caught error keeps the trace of
  its first throw: if it ends the program after being rethrown or passed on, the report shows that trace and a
  `Thrown again at file:line:col` line. Every crash report is headed by the error's crash title (`Parse error: ...`,
  the type name in sentence case unless the crashable writes its own `crash_title`), runtime crashes too.
- `is None` is an arm of `Maybe` only (and the success arm of `Check[None]`). A `Check` has no
  absent state: "not found" in a `Check` is `is AbsentValueError`.
- Users never write `Maybe`/`Check` as a return type (SF-S806), only a keyword produces one.
  `Check` is meant to be taken apart where it is made: they cannot be a parameter type
  (SF-S754), a member variable type (SF-S755), or copied from one variable to another (SF-S758). `Maybe` is
  an ordinary optional value and may be stored.

**Crashables.** `crashable X` declares an error type. It is a record (a value, no identity) with its own
category: only a crashable can be thrown.

- It **must** write `routine X.crash_message() -> Text` (SF-S704).
- `crash_title()` comes from the name in sentence case (`NopeError` → "Nope error",
  `AbsentValueError` → "Absent value error"); write it only to say something else.
- `Crashables` (plural) is the standard record for "any caught error". On it, `crash_title()`,
  `crash_message()`, `represent()` (also `f"{e}"`), `diagnose()` and `crash_type_id()` reach the actual
  error's own routines.
- Standard errors include `AbsentValueError`, `DivisionByZeroError`, `IndexOutOfBoundsError`,
  `KeyNotFoundError`, `EmptyCollectionError`, `IntegerOverflowError`, `NumericOverflowError`,
  `NumericDomainError`, `InvalidValueError`, `IOError`, `TaskTimeoutError`, `AgentStartError`,
  `CancelRequested`, `ChannelClosedError`, `ReshapingWhileInUseError`. `stop()` and `breach()` crash on purpose (`UserTerminationError`,
  `LogicBreachedError`).

**Fatal walls.** The line between recoverable and fatal is: a **data condition** the program can respond to
(file missing, key absent, bad input) is recoverable through a `!` routine and a keyword; a **physical or
logic limit** is fatal and cannot be caught.

- Out of memory is fatal, loud and uncatchable (a handler would itself need memory). Very deep recursion
  is the same wall: Suflae has no "stack" in its vocabulary, frames are memory, and running out of it is
  reported as out of memory.
- A segmentation fault cannot happen in Suflae code. If one appears, it is a builder or runtime bug, or it
  comes from a `.rf` module the program imports, not a language failure mode.
- Fatal messages describe what the program did, never the machine.

**Exit status.** Every crash (an unrecovered `throw`, a checked-arithmetic failure, out of memory)
exits with **status 82** (`0x52`, `'R'`). A run that ended with 82 crashed: read stderr for the error and
the stack trace. Status 1 is not a crash. A process killed from outside (SIGKILL, the OS out-of-memory
killer) dies before Suflae gets control and reports nothing. A run that returns from `start` normally exits with 0,
or with the status `set_exit_code(code)` (an `S32`, in Core) set last: one atomic value, so any task may set it
and the last call wins. A crash keeps 82 whatever was set.

## 12. Generics and protocols

```suflae
routine largest[T](items: List[T]) -> T
needs T obeys Comparable
    var best = items[0]
    each x in items
        if x > best
            best = x
    return best

record Pair[TFirst, TSecond]
    first: TFirst
    second: TSecond

protocol Greetable
    routine Me.greet() -> Text

record Cat
obeys Greetable
    name: Text

routine Cat.greet() -> Text
    return "Meow, I am " + me.name

routine introduce[T](thing: T) -> Text
needs T obeys Greetable
    return thing.greet()
```

- Type parameters go in `[...]` after the name; constraints go on a `needs` line under the signature
  (several are comma-separated), or inline: `routine f[T obeys Comparable](...)`.
- `obeys` lines sit under the type's header. A protocol lists the routines a conforming type must have,
  with `Me` for the conforming type. Writing `obeys` is optional: a type that has the routines (same names,
  parameter count, types per place and return type, parameter names and `!` aside) obeys the protocol anyway.
  Write it to ask the builder for derived routines (`obeys Hashable`) or to hear about a missing routine at the
  declaration. A type that does not fit is SF-S150 where it is passed, naming the missing or mismatched routine.
- Common protocols: `Equatable`, `Comparable`, `Hashable`, `Iterable[T]`, `Copyable`.
- Every generic use is specialized per concrete type at build time; there is no runtime dispatch.

## 13. Text and formatting

```suflae
var name = "Ada"
var n: S64 = 42
var price = 1.5
show(f"Hello, {name}! n={n}")
show(f"{n:?}")            # the diagnose (debug) form: S64(42)
show(f"{n:=}")            # n=42
show(f"{n.hex()} {price.fixed(2)} {name.pad_end(10)}|")
```

- `show(value)` prints `value.represent()` and a newline; `show(value: v, end: "")` changes the ending
  (all arguments named once there are two).
  `alert(value)` writes the `diagnose()` form to stderr.
- f-string specs are only `=`, `?` and `=?`. Everything else is a member routine called inside the braces:
  integers have `hex()`, `bin()`, `oct()`, `to_text(radix:)`; `Decimal` and floats have `fixed(places)`;
  `Text` has `pad_start(width)`, `pad_end(width)`, `center(width)`, `zero_pad(width)`.
- `Text` counts and positions with `Integer`: `count()`, `count_of(...)`, `s[i]`, `find(other:)` /
  `find_last(other:)` (failable: `try s.find(other: "x")`), `repeat(times:)`, and the widths of
  `pad_start`/`pad_end`/`center`/`zero_pad`. `split(separator:)`, `lines()` and `words()` give a `List[Text]`.
- `Text` is a value. Useful members: `count()`, `is_empty()`, `starts_with(prefix:)`,
  `ends_with(suffix:)`, `split(...)`, `words()`, `replace(old:, new:)`, `to_uppercase()`,
  `to_lowercase()`, `encode_as_utf8()`, and `+` to join. Substring test: `s.has("ell")`. Slices take ranges:
  `s[0 til 3]`, `s[2 til ^0]` (to the end).
- `Char` has `codepoint()`, `is_alphabetic()`, `is_digit()`, `is_whitespace()`, `to_uppercase()`, ….
- A binary float shows as the shortest decimal that reads back to the same value
  (`x + 0.2` with `var x: B64 = 0.1` shows `0.30000000000000004`); a `Decimal` shows exactly (`0.1 + 0.2` shows `0.3`).

## 14. Collections

```suflae
var nums = [3, 1, 2]                      # List[Integer]
var ages = {"ada": 36, "alan": 41}        # Dict[Text, Integer]
var tags = {"red", "blue"}                # Set[Text]
var empty = List[Text]()

nums.add_last(value: 4)
show(nums[0])                             # 3
show(nums[^1])                            # 4, the last element
ages["grace"] = 85
show(ages["ada"])
if ages.has_key("alan")
    show("found")
each entry in ages
    show(f"{entry.key} -> {entry.value}")
```

- `List`, `Dict` and `Set` are always available; `[]`, `{k: v}` and `{a, b}` are their literals. The
  specialized containers (`SortedDict`, `SortedList`, `SortedSet`, `PriorityQueue`,
  `BitList`, `Array[T, COUNT]`) are constructor-only and come from `import Collections`.
- `sv.to[List[Integer]]()` reads a list back from the `SerialValue` that `xs.serialize()` gives, as
  `sv.to[Point]()` does any other value; it is failable (`try sv.to[List[Integer]]()`). A type rebuilds itself in
  `common routine T.from_serial!(value: SerialValue)`, which the builder writes for every type. The name is the
  builder's own: a program that writes or calls `from_serial` gets SF-S809 and uses `sv.to[T]()` instead.
- Collections are entities: shared, not copied. `xs.duplicate()` makes an independent copy, and a range
  slice `xs[1 til 3]` returns a new list.
- Indices and counts are `Integer`: `xs.count()`, `xs[i]`, `xs[1 til 3]`, `remove_at(index:)`,
  `xs.enumerate()` (pairs `(Integer, T)`). A negative index crashes with `NegativeIndexError`; count from the
  end with `^` (`xs[^1]`).
- The collections are Suflae's own, written in Suflae: `List`, `Dict`, `Set`, `SortedList`, `SortedSet`,
  `SortedDict`, `PriorityQueue`, `BitList`, `BitArray`, `CircularList`. A `List` keeps its elements from the first
  place on: it is the stack (O(1) at the back), and adding or removing at its front moves the rest (O(n)).
  `CircularList` (`import Collections`) is the queue and deque: O(1) at both ends, `rotate_left`/`rotate_right`,
  and a fixed-size latest-N history (`add_last_keeping`, `keep_last`). `Array[T, COUNT]` is RazorForge's
  fixed-size record, counted and indexed with `Integer` too. RazorForge's `SplitList` / `SplitArray`
  (structure-of-arrays layouts) are not available in Suflae.
- Indexing `xs[i]` and `d[k]` crash when the index or key is missing; recover with `try xs[i]` /
  `try d[k]`. `remove_first()`, `remove_last()`, `remove_at(index:)`, `first()`, `last()` are failable the
  same way.
- `dict[k] = v` replaces the value of a key that is there and adds the key otherwise. `set.add(value:)`,
  `set.remove(value:)`, `dict.remove(key:)` return `Bool`:
  a result you do not need is simply left (no marker). `Dict` and `Set` iterate in insertion order.
- Lists: `add_first`, `add_last`, `add_at`, `contains`, `sort()`, `sorted()`, `sort_by(...)`,
  `reverse_in_place()`, `clear()`, `is_empty()`. `SortedList`/`SortedSet` have no positional index.
- Iterator adapters (`import IterTools`): `where`, `select`, `take`, `skip`, `take_while`, `distinct`,
  `reverse`, `enumerate`, `zip(a:, b:)`, `chain`, `min_by`, … are lazy; finish with `.to[List]()` or `each`:
  `nums.where(predicate: x => x % 2 == 0).to[List]()`.

**Changing a collection while looping over it.** After an add or remove, the loop can no longer trust that
its next element is really the next one, so it is not allowed:

```suflae
routine grow(xs: List[Integer])
    xs.add_last(value: 99)
    return

routine start()
    var nums = [1, 2]
    each x in nums
        grow(xs: nums)        # crashes: ReshapingWhileInUseError
    return
```

- Writing `nums.add_last(...)` directly in the loop body is a build error (SF-S625).
- A dictionary's `d[k] = v` may add the key, so writing a key of the dictionary being looped over crashes with
  `ReshapingWhileInUseError`, even when the key is already there. A list's `xs[i] = v` only replaces an
  element and is fine.
- When the change is inside a called routine, the builder cannot see that `xs` and `nums` are the same
  list, so the loop marks the list as in use and the change crashes at run time with
  `ReshapingWhileInUseError`.
- Changing the list after the loop, after `break`, or after a `return` out of the loop is fine. Changing an
  element's contents (`boxes[0].n = 5`) is not an add or remove and is always fine.
- The check assumes a single job; two agents changing and looping over the same list concurrently are
  not protected.

## 15. Console, files and resources

- Output: `show(...)`, `alert(...)` (stderr). Input: `ask_line()`, `ask[T]()` (a line as a `T`: `var n: Integer = ask()`,
  failing like `to[T]()`; a tuple reads the line's words, one item each: `var (a, b): (Integer, Integer) = ask()`,
  `"3 4".to[(Integer, Integer)]()`, a `Text` item is the word itself), `ask_word()`, `ask_all()`,
  `ask_lines()`, `ask_words()`. A blank line is `""`. At the end of the input `ask_line()`/`ask_word()` crash with
  `EndOfInputError` (`IO/Errors`): read until the end with `when check ask_line()` + `is EndOfInputError => break`.
  An `ask_line()` right after `ask_word()` skips the line end the word left.
- Files: `FileManager` is the one place that touches the disk, its routines called on the type. `File(at:
  "notes.txt")` and `Directory(at: "data")` only say where (no I/O at construction), with path members `name`,
  `stem`, `extension` (no leading dot), `parent`, `with_extension(ext:)`, `with_name(name:)`, `file(name:)`,
  `subdir(name:)`. `FileManager.read_text(at:)`, `write_text(at:, content:)`, `append_text`, `read_bytes`,
  `write_bytes`, `open_read`/`open_write`/`open_append`, `size`, `metadata`, `exists`, `is_file`, `is_directory`,
  `delete`, `delete_all`, `move(from:, to:)`, `copy(from:, to:)`, `list`, `entries`, `walk`, `current_directory()`,
  `home_directory()`, `temp_directory()`. **A file must exist before it is read, written or opened**: a missing one
  is an `IOError`, nothing is created silently. `FileManager.make_file(at:, replacing: false)` makes one (an
  existing file is an `IOError` unless `replacing: true`, which empties it), `make_directory(at:, parents: false)`
  makes a directory. `move`/`copy` refuse an existing destination unless `replacing: true`. There is no `touch`
  and no `IO/FileSystem`. Failures are recovered with `try`/`check`.
- `FileManager.read_text` / `write_text` are safe inside an agent's work: it waits while other jobs run.
- **`using`** binds a resource for a block and releases it at the block's end, however the block is left:

```suflae
var out = FileManager.make_file(at: File(at: "out.txt"), replacing: true)
using FileManager.open_write(at: out) as sink
    sink.store(data: "hello\n".encode_as_utf8())
```

  A type works with `using` when it has `enter` (its result is bound by `as`) and `exit` member routines.
  There is no `defer`.

## 16. Concurrency

Any routine can run concurrently. **`f.to[Agent](args)`** computes the arguments now (and a receiver that is a
value, as in `p.total.to[Agent]()`), defers the call into an `Agent[T]`, and starts
nothing. **Nothing runs until a verb starts it.** Where the work runs is the runtime's business.

```suflae
routine fetch(id: Integer) -> Integer
    waitfor(50ms)            # this job waits, others run meanwhile
    return id * 10

routine start()
    var a = fetch.to[Agent](id: 1)           # Agent[Integer], not started
    a.start_now()                        # starts it now
    show(f"one => {a.retrieve()}")       # wait for the value

    var jobs = [fetch.to[Agent](id: 2), fetch.to[Agent](id: 3)]
    var results = jobs.gather()          # start both, wait for all
    show(f"{results[0]} {results[1]}")
    return
```

- The routine is named, not called, and the conversion takes the routine's own arguments. Only that routine is the
  work: `f.to[Agent](x).retrieve()` makes the Agent and collects it at once. `agent f(x)` is the old spelling
  (SF-G220, `migrate --step agent`).
- A failure is recovered where the Agent is collected: `try job.retrieve()`, `check job.retrieve()`.
- An agent of a routine that returns nothing is `Agent[None]`, and the type can be inferred:
  `var jobs = [work.to[Agent](), work.to[Agent]()]` then `jobs.gather()`.
- `job.start_now()` starts the work now. Collect it later with `retrieve()` or `gather()`.
- `job.retrieve()` waits for its value, starting it first if nothing has. Inside a job it parks (other
  jobs keep running).
- A failure stays in its agent. A failure inside the work does not end the program where the work runs,
  and the other agents keep running. It surfaces where the agent is retrieved: `a.retrieve()` rethrows it
  (bare, it crashes loudly there like any failure), `check a.retrieve()` / `try a.retrieve()` recover it
  The crash report's trace ends with `agent in start()` (the line that made the
  Agent) and `Thrown again at` names the `retrieve()` line.
- `job.cancel()` asks the work to stop. Work nothing started yet never runs. Running work sees the request
  at its next wait (`waitfor`, `retrieve`, a channel) or through `cancellation_requested()`, and stops
  there. Retrieving a cancelled agent waits for it to stop, then crashes with `CancelRequested` (the name
  carries no `Error` because cancelling is not a failure). Recover it with `check`/`try`.
- Starting an agent twice, or retrieving it twice, crashes with `AgentStartError`.
- `agents.gather()` runs a `List[Agent[T]]` concurrently and returns every result in input order, and
  throws the first failure it meets (in list order). `agents.race()` returns the first finisher's value,
  and throws the winner's failure. `agents.cancel_all()` returns nothing: it cancels every agent and
  waits until each has stopped, dropping what each came to. Work nothing started yet never runs.
  `agents.next_finished()` waits for the first agent to finish, takes it out of the list and returns it (call it
  in a loop to handle results in finishing order). `agents.gather_all()` waits for all and returns a
  `List[Check[T]]`: each agent's value or failure, so one failure does not hide the others' results.
  `agents.take_finished()` takes the finished agents out of the list in place and returns them (a server
  collecting its departed clients' work: rebuilding the list with `where` would drop the work still running).
- When the last holder of an agent lets go of it before anything retrieved it, its work is cancelled and waited
  for (work nothing started never runs).
- `job.waitfor(5s).retrieve()` waits at most 5 seconds and crashes with `TaskTimeoutError` past it;
  `try job.waitfor(5s).retrieve()` gives `None` instead. Give the deadline before starting the agent:
  one already started with `start_now()` is refused with `AgentStartError`.
- `job.is_done()` and `job.is_running()` are snapshots for polling.
- Free routines: `waitfor(duration)` pauses (parks a job), `yield_now()` lets other jobs run,
  `cancellation_requested()` tells a long loop to stop early. Cancellation is cooperative.
- An agent that nothing starts never runs. `f.to[Agent]()` as a statement is warning
  SF-W008. There is no fire-and-forget: start it with `start_now()` and collect it later.
- Jobs that talk to each other (over a channel) must all be started before you wait on one:
  `producer.start_now()` first, then `consumer.retrieve()`.
- **Shared entities are safe to share between jobs.** The runtime may run jobs on several cores. An entity
  that reaches another job gets an atomic reference count and a lock taken around each access, with no
  code from you. As with globals, one statement is atomic; a read-then-write spread over several
  statements is not.
- There is no thread type in Suflae.

```suflae
routine checked!(n: Integer) -> Integer
    if n < 0
        throw VerificationFailedError()
    return n

routine slow(n: Integer) -> Integer
    each i in 0 til 2000
        if cancellation_requested()
            return -1
        waitfor(1ms)
    return n

routine start()
    var bad = checked.to[Agent](n: -1)
    var good = checked.to[Agent](n: 3)
    bad.start_now()
    good.start_now()
    show(f"good: {good.retrieve()}")     # 3
    when check bad.retrieve()
        is Integer as v => show(f"bad: {v}")
        is Crashables as e => show(f"bad failed: {e.crash_message()}")
    var running = slow.to[Agent](n: 1)
    running.start_now()
    running.cancel()
    when check running.retrieve()
        is CancelRequested => show("cancelled")
        else => show("finished")
    return
```

**Mailboxes** (always available): one `Mailbox[T]` that every agent that adds or takes holds.

```suflae
routine produce(m: Mailbox[Integer], n: Integer) -> Integer
    var i = 1
    while i <= n
        m.add_last(value: i)
        i = i + 1
    m.close()
    return n

routine start()
    var m = Mailbox[Integer](capacity: 2)
    var job = produce.to[Agent](m: m, n: 5)
    job.start_now()
    var first = m.remove_first()
    var total = first
    each x in m
        total = total + x
    show(f"total: {total}")
    return
```

`add_last` waits while the mailbox is full (`capacity: 0`: each `add_last` waits for a taker) and crashes with
`ChannelClosedError` once the mailbox is closed. `remove_first()` takes one value, waiting while it is empty, and
crashes once it is closed and empty. `each x in m` waits while it is empty and ends when it is closed and empty.
Nothing closes a mailbox by itself: the side that is done calls `close()`. Several agents taking from one mailbox
each get different values. `m.count()` and `m.is_closed()` are snapshots another agent can change at once.
`m.add_last_if_room(value: x)` never waits: it adds and returns `true` when there is room, returns `false` when the
mailbox is full, and crashes with `ChannelClosedError` once it is closed (a broadcast over subscribers' mailboxes uses
it, so one full subscriber does not hold up the rest).

A statement is one step for the agents that share an entity, so two statements can be split by another agent. A
shared `Dict` has one-step forms for that: `d.set_if_absent(key: k, value: v)` adds the key only when it is missing
and says whether it did (`if d.lacks_key(k)` then `d[k] = v` can let two agents both add), and `d.items()` returns a copy
of the pairs to walk, `each (k, v) in d.items()` (`each e in d` crashes when another agent adds or removes an entry during the walk).

## 17. Process signals

`import Signals`; `when_interrupted(handler:)` (Ctrl-C) and `when_terminated(handler:)` register a named
routine with no parameters and no result. Registering replaces the default termination (the program no
longer dies on Ctrl-C), the handler runs on its own dispatch thread, and the program must keep running its
own loop. A `global` flag is the natural hand-off:

```suflae
import Signals

global should_quit: Bool = false

routine on_interrupt()
    should_quit = true
    return

routine start()
    when_interrupted(handler: on_interrupt)
    while not should_quit
        waitfor(200ms)
    show("exiting gracefully")
    return
```

## 18. C and other machine-level code

A Suflae program never handles memory directly and never calls C directly. `danger` blocks (SF-S800),
`dangerous` routines (SF-S800) and foreign `C::` / `LLVM::` routines (SF-S810) are build errors in a program's
`.sf` files. The words `danger` and `dangerous` stay reserved.

To use a C library, write a small `.rf` module that declares and wraps it, and import that module:

```razorforge
# cmath.rf
routine C::labs(n: S64) -> S64

routine absolute(n: S64) -> S64
    return C::labs(n: n)
```

```suflae
# main.sf
import Cmath

routine start()
    show(f"{absolute(n: -42)}")   # 42
    return
```

The `.rf` module is checked by RazorForge's rules (section 23), and inside it `danger`, `dangerous` and `C::`
are available. Mark a foreign routine that may block for long (a read from a pipe, a sleep, a network wait)
`@blocking` there: called from an agent's work it runs on one of the runtime's I/O threads while the job parks.

The standard library uses `danger` internally; that is never visible to a program.

## 19. ObjectHacker (runtime reflection)

`import ObjectHacker` works on live objects from the outside. Values travel as `SerialValue`.

```suflae
import ObjectHacker

entity Hero
    name: Text
    hp: S64
    secret seed: S64

routine start()
    var hero = Hero(name: "Ada", hp: 10, seed: 7)
    var alias = hero
    show(type_name(of: hero))                          # Hero
    each info in memvars(of: hero)
        show(f"{info.order}: {info.name}: {info.type_name}, secret: {info.is_secret}")
    show(peek(target: hero, memvar: "hp"))             # SerialValue(10)
    var hp: S64 = 25
    poke(target: hero, memvar: "hp", value: SerialValue(from: hp))
    show(alias.hp)                                     # 25: every name sees the change
    var seen = watch(target: hero)
    show(seen.is_alive())                              # true
    return
```

- `type_name(of:)`, `memvars(of:)` (every member variable, `secret` ones included, in declaration order),
  `peek(target:)` (the whole object serialized), `peek(target:, memvar:)` (failable: throws `AbsentValueError`
  for an unknown name), `poke(target:, memvar:, value:)` (entities only; throws `NoSuchMemberVariableError`),
  `watch(target:)` (an observation that does not keep the entity alive; `is_alive()`).

## 20. CLI and `config.toml`

```
suflae new [folder]          # start a project: config.toml, main.sf, .gitignore, .vscode/ (keeps existing files)
suflae run hello.sf          # build and run
suflae build hello.sf        # build a native executable, don't run it
suflae check hello.sf        # check the program without building it
suflae test <dir-or-file>... # build and run each .sf against <name>.expected / .exit / .error / .input
suflae fmt [--check] <paths> # rewrite sources in the canonical layout
suflae lint <paths>          # report code that is not in the canonical layout
suflae lsp                   # language server, for an editor
suflae help | version
```

- A bare file is not a command: `suflae hello.sf` is an error that points to `suflae run hello.sf`.
- **There are no build flags.** Configuration lives in `config.toml`; with no entry file, the CLI walks up
  from the current directory to find it.

```toml
[package]
name = "my-app"

[target]
executable = "MainModule"   # the entry MODULE name, not a file path
library = ["../shared"]     # dependency directories (optional)
mode = "debug"              # debug | debug-jit | release | release-time | release-space
```

- A single script needs no manifest: `suflae run script.sf`. Without a manifest the build mode is `debug`.

## 21. Reading build errors

Format: `error[SF-S###]: file:line:col: message`, then the source line with a caret. Families: `SF-G###`
grammar, `SF-S###` semantic, `SF-W###` warning (warnings print as `warning[SF-W###]`). The prefix follows
the file the diagnostic points into (an error inside an imported `.rf` module is spelled `RF-` with the same
number). At most 20 are shown per batch.

| Code          | Usual cause                                         | Fix                                               |
|---------------|-----------------------------------------------------|---------------------------------------------------|
| SF-S510/S512  | positional arguments in a multi-parameter call      | name every argument                               |
| SF-S010       | literal out of range for its type                   | use the right constant (`U8_MAX`)                 |
| SF-G010       | a number names its type (`5_u8`)                    | `var x: U8 = 5`, or let the place it goes type it |
| SF-S435       | `var` at module level                               | `global name: Type = init`, or move it            |
| SF-S444       | routine reads a script's top-level `var`            | pass it as an argument, or make it a `global`     |
| SF-S619       | reading through an `E?` before checking it          | `if e isnot None`                                 |
| SF-S625       | adding/removing on the list being `each`-looped     | change it after the loop                          |
| SF-S704       | crashable without `crash_message()`                 | write `routine X.crash_message() -> Text`         |
| SF-S750       | `throw` in a routine without `!`                    | declare it `routine name!(...)`                   |
| SF-G211       | `!` written at a call site                          | drop it; recover with `try`/`check`               |
| SF-G212       | `x in coll` as a test                               | `coll.has(x)`                                     |
| SF-G216       | `and`/`or`, or `&`/`\|`/shifts, mixed bare          | add parentheses: `a or (b and c)`                 |
| SF-S516       | `by 0` on a range                                   | a non-zero step, negative to count down           |
| SF-W008       | an agent that nothing starts                        | `.start_now()` or `.retrieve()` it                |
| SF-S642       | `to[Agent]` on a non-routine, or over generic values | convert a routine, with concrete types           |

## 22. Keywords

A keyword can still be a name when written between backticks (`` `check` ``), and so can words with one space
between them (`` `total count` ``); `` `count` `` and `count` are the same name.

Every reserved word in Suflae. There is NO `for`, `def`, `fn`, `let`, `const`, `class`, `struct`, `enum`,
`match`, `trait`, `impl`, `async`, `await`, `spawn`, `external`, `null`, `self` or `this`.

- **Declarations**: `routine` `entity` `record` `choice` `flags` `crashable` `variant` `protocol`
- **Bindings**: `var` `preset` `global` `lateinit`
- **Visibility / receiver**: `secret` `posted` `common` · **Self**: `me` `Me`
- **Protocols and constraints**: `obeys` `needs` `relates` `everywhere`
- **Control flow**: `if` `elseif` `else` `then` `when` `is` `isnot` `loop` `while` `each` `with`
  `break` `continue` `return` `throw` `pierce` `todo` `becomes` `until`
- **Recovery**: `try` `check`
- **Ranges**: `in` (loops only) `to` `til` `by`
- **Modules**: `import` `module`
- **Other**: `using` `as` `define` `given` `pass` (an empty body: a type's, or a block's that
  does nothing)
- **Logic**: `and` `or` `not`
- **Literals**: `true` `false` `None` `none`
- **Concurrency**: `f.to[Agent](args)`
- **Reserved, not usable in a program**: `danger` `dangerous`

`!` (failable, on declarations) is a mark on the name, not a keyword. `steal` and `expand` are not Suflae
words.

The word operators `to` `til` `by` `is` `isnot` `in` `as` `and` `or` are keywords only where an
operator stands, right after something that ends an expression (`0 to 10`, `a and b`, `each x in xs`). Anywhere
else they are names: `var to = 3`, `routine step(by: Integer, to: Integer)`, `by + to`. Two places keep them
keywords: a `when` arm that starts with `is`/`isnot`, and `else as p`. `not` starts an expression, so it is always a
keyword.

## 23. Relation to RazorForge and interop

RazorForge is the same object model with the ownership dial at one name per entity (a single owner,
explicit `steal` transfers, deterministic teardown at scope exit) and the machine visible (fixed-width
numbers by default, OS threads, build-time reflection). Suflae turns the dial to many names and hides the
machine. The two share the grammar and the standard library, which is written in RazorForge.

A Suflae program may import RazorForge (`.rf`) modules; a RazorForge program may not import Suflae files.

- **Values cross freely.** `Text`, numbers, `Bytes`, records, tuples, `Maybe` and `Range` are the same
  types in both languages.
- **An entity keeps its language.** An entity declared in an imported `.rf` module stays a RazorForge
  entity (single owner, no reference count); it is not shared like a Suflae entity. As a local, or as a
  member variable of a Suflae entity, it just works: construct it, call its routines (mutating ones
  included), and it is released once. It has one holder at a time: keeping it under a second name
  (`var d = c`), putting a named one into a member variable, or handing it to a `.rf` routine that
  keeps what it is given is SF-S413. Make it where it goes (`Board(counter: Counter(...))`).
- **A RazorForge entity may not cross a Suflae routine signature by value (SF-S439).** As a parameter or a
  (non-constructor) return type it would be released more than once. This holds however the type is
  written, `Counter` or `RF::Counter`. Keep it in a member variable of a
  Suflae entity and pass that entity, or do the work on it inside the `.rf` module.
- `RF::Name` and `SF::Name` name a type in a specific language when both have one with that name.
- **Collections do not cross as-is.** Suflae's `List` is not RazorForge's (`RF::List`, counted with `U64`).
  Convert at the boundary: `rf_list.to[List]()` makes a Suflae list from a RazorForge one, and
  `xs.to[RF::List]()` makes a RazorForge list from a Suflae one.

## 24. Status (do not generate as if shipped)

Built and tested: the language described above, end to end: shared entities with cycle collection,
nullable entities, script mode, globals, `Integer`/`Decimal` defaults, the error model, Agent
concurrency, the iterate-and-mutate checks, C through imported `.rf` modules, and the basic `ObjectHacker`
surface. The `StdlibSf` fixtures run in CI with their expected output.

Not built yet:

- a REPL and hot reload;
- stronger runtime reflection in `ObjectHacker` beyond reading, writing and watching member variables.

When generating Suflae: start from the closest `Suflae/tests/Fixtures/StdlibSf/*.sf` program, name every
argument, return a value on every path of a routine that has one, use bare `Integer`/`Decimal` numbers, and
prefer `when` over nested conditionals.
