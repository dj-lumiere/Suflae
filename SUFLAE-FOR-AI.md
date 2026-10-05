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

The second axis is **machine visibility**. Suflae hides the machine. Unsuffixed numbers are `Integer`
(arbitrary precision, never overflows) and `Decimal` (base-10, so `0.1 + 0.2 == 0.3`). Running out of
memory is one wall with no "stack" vocabulary. There are no threads to create: concurrency is `suspended`
coroutines on the runtime's scheduler, and shared entities are locked for you. Machine-level code is still
available behind explicit markers: `danger` blocks, `dangerous` routines and foreign routines
`routine C::name(...)`.

## 2. The rules most likely to break your assumptions

1. **Multi-parameter calls REQUIRE named arguments.** `gcd(252, 105)` is an error (SF-S510);
   `gcd(a: 252, b: 105)` is right. Single-argument calls may be positional. Routines annotated
   `@positional` accept all-positional calls. Mixing named and positional in one call is always an error
   (SF-S512).
2. **Every routine you write ends with an explicit `return`**, even one that returns nothing. A failable
   routine may also end with `throw` or `absent`. Script-mode top-level code (rule 6) needs none.
3. **Entities are shared; records are copied.** `var b = a` on an entity gives a second name for the same
   object (no `steal`, no copy, no error). On a record it makes an independent copy. Collections (`List`,
   `Dict`, `Set`, …) are entities, so they are shared too: copy one explicitly with `xs.duplicate()`.
4. **Bare numbers are `Integer` and `Decimal`**, not 64-bit machine numbers. `42` is an `Integer`, `3.14` a
   `Decimal`. A literal adapts to an expected type (`var x: S32 = 5`, `xs[0]`), but variables of different
   number types never mix without an explicit conversion.
5. **`//` is floor division, `/` is true division, and `Integer` has no `/`.** `7 // 2` is `3`. For a
   fractional result, compute with `Decimal` values, and write the literals as decimals: `1.0 / 3.0`
   (`1.0 / 3` is an error, SF-S502: a whole-number literal is an `Integer`, not a `Decimal`).
6. **`routine start()` is optional.** The entry file may be a plain list of top-level statements (script
   mode). `show(...)` needs no import.
7. **Failable routines carry `!` on the DECLARATION only** (`routine parse!(...)`); call sites never write
   it. A bare call crashes loudly on failure. To recover, prefix the call with `try` / `grab` / `lookup` and
   take the result apart with `when`. There is no try/catch and no `try_parse` name form (§11).
8. **Conditional expressions are top-level only.** `return if c then a else b` works;
   `f(x: if c then a else b)` and `(if c then a else b) + 1` are parse errors by design. Use `when` or a
   named intermediate.
9. **Changing a collection while `each`-looping it is banned.** `each x in xs` with `xs.add_last(...)` in the
   body is a build error (SF-S625); the same change made inside a called routine crashes at run time with
   `ReshapingWhileInUseError`. Change it after the loop.
10. **Ignored `Bool` results need `discard`**: `discard seen.add(value: v)` (SF-W007).
11. **A module-level `var` is an error (SF-S435).** Module-level mutable state is `global name: Type = init`
    (§6). Constants are `preset`.
12. **Blocks are indentation, four spaces per level.** No braces, no semicolons, no `:` after `if`.
13. **Type arguments use `[...]`, never `<...>`:** `List[Text]`, `Dict[Text, Integer]`. `<` is a
    comparison, and chained comparisons (`0 <= x <= 10`) are one expression.
14. **Containment is container-first:** `xs have 3`, `d have "key"`, `"hello" have "ell"`, `xs lack 3`.
    `x in xs` as a test is a grammar error (SF-G212); `in` belongs only to `each x in xs`.
15. **There is no `class`, `def`, `fn`, `let`, `const`, `for`, `match`, `async`, `await`, `try`/`catch`,
    `null`, `print`, `main` or `pass` statement.** The words are `entity`/`record`, `routine`, `var`,
    `preset`, `each`, `when`, `suspended`, `retrieve()`, `try`/`grab`/`lookup`, `none`, `show`, `start`.

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
`List`/`Dict`/`Set`, `Maybe`, errors, `Agent`, channels), `Integer`, console output and input
(`IO/Console`), and files (`IO/File`). Import the rest: `Collections` (`SortedDict`, `BitList`,
`PriorityQueue`, …), `IterTools` (iterator adapters), `Numerics` (`Real`, `Complex`), `IO/FileSystem`
(path strings), `Signals`, `BuilderQuery` (`x.type_name()`), `ObjectHacker`. Writing an import that is
already in the prelude (many fixtures write `import IO/Console`) is harmless.

## 4. Naming and vocabulary

- function → **routine**; method → **member routine**, declared outside the type as
  `routine Type.name(...)`; the receiver is `me`. `Me` (capital) is the receiver's type.
- class/struct → **`entity`** (shared object with identity) or **`record`** (value). Enum → **`choice`**;
  bit set → **`flags`**; sum type → **`variant`**; error type → **`crashable`**; interface/trait →
  **`protocol`**, adopted with **`obeys`**.
- field → **member variable**, a bare `name: Type` line in the type body (no `var`). Visibility is written
  only when restricted: `posted` (anyone reads, only the module writes) and `secret` (module-private).
- Generic constraints use **`needs`** (`needs T obeys Ordered`), never `where`.
- Types and protocols are `PascalCase`; routines, variables and member variables `snake_case`; `choice` /
  `flags` cases and `preset` constants `SCREAMING_SNAKE_CASE`.
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
| Arbitrary, by import    | `Real` (binary), `Complex` — need `import Numerics`                        |
| Text and bytes          | `Text` (Unicode), `Character`, `Bytes`, `Byte`                             |
| Other                   | `Bool`, `Duration`, `ByteSize`, `Moment`/`LocalMoment`, tuples `(A, B)`    |

- Every type in the table except `Real`/`Complex` is usable with no import.
- **Counts, positions, widths and amounts are always `Integer`**, whatever the value's type: a fixed-width
  number keeps its own type, but `x.count_ones()`, `x.leading_zeros()`, `x.ilog2()` and `x.signum()` return
  `Integer`, and shift and rotate amounts (`x.rotate_left(bits: 3)`, `x.ashr(bits: 1)`), radixes
  (`x.to_text(radix: 16)`), places (`f.fixed(places: 2)`) and exponents (`f.scalbn(n: 2)`) are `Integer`.
- **`Integer`** never overflows. **`Decimal`** is exact for decimal fractions (`0.1 + 0.2 == 0.3`, money is
  safe) with 34 significant digits; a result needing more digits (`1 / 3`) is rounded to 34, and a result
  beyond about `1e6144` crashes. It displays canonically: `2.50` shows as `2.5`.
- Literals: `1_000_000`, `0xFF`, `0o777`, `0b1010`, `3.2e5`. Typed suffixes: `5_s32`, `7s32`, `1.5_b64`,
  `0_u64`, `10n` (`Integer`), `2.5dn` (`Decimal`). Imaginary: `3 + 4i` (`C128` with no context).
- A hex float (`0x1.8p3`, `0x1p-23_b32`) is binary-float only and exact, and defaults to `B64`, never
  `Decimal`.
- A literal out of range for its type is a build error (SF-S010): `-1` never fits an unsigned type; write
  `U8_MAX`, `U64_MAX`.
- Durations: `50ms`, `5s`, `2m`, `1h`, `1d`, `1w` (also `us`, `ns`). Sizes: `4kb`, `4kib`, `2mb`, `2mib`,
  `1gb`, `1gib`.
- Text: `"..."`, formatted `f"x={x}"`, raw `r"C:\path"`, raw formatted `rf"..."`. `{{` / `}}` are literal
  braces in an f-string. Escapes: `\n \t \r \\ \" \' \0` and `\uXXXXXX` (six hex digits). Characters:
  `'A'`, `'한'`. Bytes: `b"abc"`, `br"..."`, a single byte `b'x'` (ASCII only; `\xFF` escapes).
- Tuples: `(1, "a")` has type `(Integer, Text)`; read `t.item0`, `t.item1`; destructure with
  `var (q, r) = pair`.

**Optionals and `none`.**

- `T?` is the optional type (`Maybe[T]` for values). For an entity, `E?` is a handle that may be empty.
- `None` (capital) is the type and pattern (`is None`, `isnot None`); `none` (lowercase) is the value.
- `none` is legal only where the target type can be absent: `T?`, `Lookup[T]`, a variant with a `None`
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
  f-string built from other presets.
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
- **Every global is safe to use from concurrent coroutines.** A single-statement read-modify-write
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
  Division by zero crashes (`DivisionByZeroError`), on `Real` and `Complex` too. On the floats, `Decimal`,
  `Real` and `Complex`, `0 / 0` crashes with `NumericDomainError` instead.
- `Real` (from `import Numerics`) is finite-only like `Decimal`: nothing hands back an infinity or NaN,
  the operation crashes instead (recover with `try`/`grab`). `log(0)` and `atanh(±1)` crash with
  `DivisionByZeroError`, `sqrt`/`log` of a negative, `asin`/`acos` outside [-1, 1], `acosh` below 1 and
  a negative base to a non-integer power with `NumericDomainError`, and `exp`, `sinh`, `cosh`, `**` or
  any result too large for `Real` with `NumericOverflowError`. `+ - *` are exact, and one whose exact result
  would need more than 2^32 bits (adding numbers whose exponents are very far apart) crashes with
  `NumericOverflowError` too, before allocating anything. `Real(text: "nan")` / `"inf"` is an
  `InvalidValueError`, and a `Real` made from an infinite or NaN `B64` crashes too.
- Comparison: `== != < <= > >=`, chained in one expression: `0 <= x <= 10`. Every operand of a chain is
  evaluated once, eagerly, left to right, before any comparison (`a < b < f()` calls `f` even when `a < b`
  is false), and only the comparisons stop at the first false link, so a failure in any operand is a failure
  like any other (`try 1 < 5 < 10 // n` recovers it). `==` compares values (it calls `eq`); `===` / `!==`
  ask whether two entities are the same object (§10).
- Logic: `and`, `or`, `not` (short-circuiting). Bits on integers: `&`, `|`, `^`, `~`, shifts `<<`, `>>`
  (sign-filling), `>>>` (zero-filling); there is no `<<<`. Shifting by the width or more gives 0 (or the
  sign fill for `>>`).
- Containment: `coll have x`, `coll lack x`. Ranges: `1 to 5` (inclusive), `1 til 5` (exclusive),
  `1 to 10 by 2` (step); `10 to 1` counts down (the direction comes from the endpoints, `by` is always
  positive). `0 to 10 have 10` is `true`.
- Optionals: `m ?? fallback` takes the value or the fallback; `m!!` takes the value and crashes if absent.

## 8. Control flow

```suflae
if x == 3
    show("three")
elseif x > 3
    show("big")
else
    show("small")

unless list.is_empty()
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

`each` works over ranges, collections, text (one `Character` at a time), dictionaries (each entry has
`.key` and `.value`) and anything that `obeys Iterable`. `enumerate()` and `zip(a:, b:)` (from
`IterTools`) produce tuples that `each (a, b) in ...` takes apart.

A `while` or `each` may end with an `else`, which runs only when the body ran zero times: the condition was
false at the very first check, or the source was empty. Once the body has run, the `else` never runs, however
the loop ended (condition false, source exhausted, or `break`).

```suflae
each item in items
    show(item)
else
    show("no items")
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
        is S64 n and n > 100 => return f"big {n}"
        is S64 n => return f"int {n}"
        is Text t => return f"text {t}"
        is None => return "nothing"
        else => return "other"
```

- Arms: comparison arms (`== value`, `< value`, …), type arms (`is T`, `is T name`), a guard after a
  binding (`is S64 n and n > 100`), tuple arms (`(0, y) => ...` on a tuple subject), and `else` /
  `else name` last. A failed guard falls through to the next arm.
- Without a subject, `when` picks the first true condition:

```suflae
var label = when
    n > 10 => "big"
    n > 3 => "medium"
    else => "small"
```

- An expression `when` takes one expression per arm after `=>`. Several statements in an arm are a build error
  (SF-S307): compute the value first, or write a `when` statement that sets a variable.
- `if x is T name` tests and binds in one step; the binding exists only inside that branch.
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

- Call with named arguments in any order: `add(b: 2, a: 1)`, `greet(name: "Ada")`. A member routine is
  called on its receiver: `acct.deposit(amount: 5)`.
- Parameter and return types are always written. A routine with no `->` returns nothing. A variadic
  parameter is written `name...: Type`.
- Routines are declared at module level or as `routine Type.name`; there are no nested routines.
  `secret routine helper(...)` keeps a helper module-private. `common routine Type.name(...)` is a
  type-level routine with no `me`, called as `Type.name(...)`.
- A constructor is the type's name: `Point(x: 1, y: 2)` (the wired memberwise one), or one you write as
  `routine Point(from_text: Text) -> Point` (`routine Point!(...)` when it can fail).
- **Lambdas** are single expressions: `x => x * 2`, `(a, b) => a + b`. They may read the surrounding
  variables directly. Routine types are written `Routine[(Integer,), Integer]` (the parameter list is
  always a tuple: `()`, `(T,)`, `(A, B)`). A free routine's bare name is a value:
  `nums.select(transform: double)`.
- `suspended routine` declares a coroutine (§16).

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
  pair). There are no weak-reference annotations, no `destroy` to write or call, and no lifetimes. For a
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
  `have` / `lack` / `==` (`perms have Permission.WRITE`).
- **variant**: one value of one of the listed types; assign any member directly (`var n: Number = "hi"`,
  `var d: Number = none`) and take it apart with `when` / `is T name`.
- **crashable**: an error type (§11).

## 11. Errors and failure

**Failable routines.** A routine that can fail is declared with `!` and fails with `throw` (an error) or
`absent` (no value):

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
    absent
```

- `throw` or `absent` in a routine declared without `!` is SF-S750 / SF-S751. A `!` routine with neither is
  an error too.
- Call sites never write `!` (`parse!(n: 1)` is SF-G211). **A bare call crashes loudly on failure**: the
  error's title and message, where it was thrown, and a stack trace go to stderr.

**Recovering.** Put a keyword in front of the expression:

| Keyword       | Result                           | Holds                                      |
|---------------|----------------------------------|--------------------------------------------|
| `try EXPR`    | `Maybe[T]`, written `T?`         | the value, or `None` (error or absence)    |
| `grab EXPR`   | `Check[T]` = `T \| Crashables`   | the value, or the caught error             |
| `lookup EXPR` | `Lookup[T]` = `None \| T \| Crashables` | the value, `None` (absent), or the error |

```suflae
routine start()
    when grab parse(n: -1)
        is Integer v => show(f"ok {v}")
        is NopeError e => show(f"caught: {e.what}")
        is Crashables e => show(f"{e.crash_title()}: {e.crash_message()}")

    var m = try parse(n: 4)
    when m
        is None => show("failed")
        else v => show(f"got {v}")

    var safe = try parse(n: -5) ?? 0
    show(f"{safe}")
    return
```

- **With a keyword in front, every failure beneath it is recovered, at any depth; only `pierce` still
  crashes.** That covers a `throw`/`absent` in the called routine and in every routine it calls, bare calls
  included (failable or not: `try f()` also recovers a division by zero inside a plain helper `f` calls), a
  checked operation (`try a + b` recovers a fixed-width overflow, `grab a // b` a division by zero,
  `x += d`), an index out of range or a missing key (`try items[9]`), and a failing conversion
  (`S8(from: 300)`, `Integer(text: "x")`). Each becomes the result's failure (`try`: `None`; `grab`: the
  error, an absence as `AbsentValueError`; `lookup`: the error or `None`), and evaluation stops at the first
  one. A bare call still crashes loudly. Lambdas and routine values are covered the same way: a failure
  inside a lambda handed to another routine (`try items.select(transform: x => 10 // x).List()`,
  `grab items.sort_by(compare: f)`, a routine that calls the value it was given, a routine value passed down
  several levels or kept in a record) is recovered by the keyword above the call that reaches it, at any
  depth. A routine value called with no keyword above that call still crashes on failure. An `each` loop over
  an adapter holding a lambda ends only when its source ends: a failure inside the lambda is the loop's
  failure, recovered by a keyword above the loop or crashing without one. Not covered: a crash inside a
  library routine declared without `!` (other than in a routine value you handed it) stays a crash, and the
  fatal walls below stay fatal.
- The keyword reaches up to `??`: `try f() ?? d` means `(try f()) ?? d`.
- **`pierce`** is the failure that must not be recovered: `pierce NopeError()` crashes like an unrecovered
  `throw` (status 82), and it crashes the same way under `try`/`grab`/`lookup` at any depth. Use it for a
  broken invariant (state the program can no longer trust), never for a condition a caller could handle. It
  does not make a routine failable (no `!` needed for it).
- Typical use: `try` when only absence matters, `grab` for a routine that throws, `lookup` for one that can
  both throw and be absent.
- A routine that returns nothing gives `Check[None]` under `grab` (its success arm is `is None`) and `Bool`
  under `try` (`true` when it succeeded).
- A `when` on a `Check` or `Lookup` **must** have the success arm (`is T v` or `else v`); leaving it out is
  a build error. `is NopeError e` takes only that error and binds it as a `NopeError` (its member variables
  are readable); `is Crashables e` takes any error, so write it after the specific ones; `else e` after the
  other arms binds whatever is left. `x is NopeError` also works as an expression.
- **An error arm is optional.** With no arm for the error, the `when` passes it on: inside a `!` routine
  as if `is Crashables e => throw e` were written (the caller receives it), anywhere else the program
  crashes loudly. `throw e` on a caught `Crashables` rethrows the same error.
- `is None` is an arm of `Lookup` and `Maybe` only (and the success arm of `Check[None]`); a `Check` has no
  absent state.
- Users never write `Maybe`/`Check`/`Lookup` as a return type (SF-S806); only a keyword produces one.
  `Check` and `Lookup` are meant to be taken apart where they are made: they cannot be a parameter type
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
  `NumericDomainError`, `InvalidValueError`, `IOError`, `TaskTimeoutError`, `ChannelClosedError`,
  `ReshapingWhileInUseError`. `stop()` and `breach()` crash on purpose (`UserTerminationError`,
  `LogicBreachedError`).

**Fatal walls.** The line between recoverable and fatal is: a **data condition** the program can respond to
(file missing, key absent, bad input) is recoverable through a `!` routine and a keyword; a **physical or
logic limit** is fatal and cannot be caught.

- Out of memory is fatal, loud and uncatchable (a handler would itself need memory). Very deep recursion
  is the same wall: Suflae has no "stack" in its vocabulary, frames are memory, and running out of it is
  reported as out of memory.
- A segmentation fault cannot happen in Suflae without `danger`. If one appears outside `danger` code, it
  is a builder or runtime bug, not a language failure mode.
- Fatal messages describe what the program did, never the machine.

**Exit status.** Every crash (an unrecovered `throw`/`absent`, a checked-arithmetic failure, out of memory)
exits with **status 82** (`0x52`, `'R'`). A run that ended with 82 crashed: read stderr for the error and
the stack trace. Status 1 is not a crash. A process killed from outside (SIGKILL, the OS out-of-memory
killer) dies before Suflae gets control and reports nothing. A run that returns from `start` normally exits with 0,
or with the status `set_exit_code(code)` (an `S32`, in Core) set last: one atomic value, so any task may set it
and the last call wins. A crash keeps 82 whatever was set.

## 12. Generics and protocols

```suflae
routine largest[T](items: List[T]) -> T
needs T obeys Ordered
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
  (several are comma-separated), or inline: `routine f[T obeys Ordered](...)`.
- `obeys` lines sit under the type's header. A protocol lists the routines a conforming type must have,
  with `Me` for the conforming type.
- Common protocols: `Equatable`, `Ordered`, `Comparable`, `Hashable`, `Iterable[T]`, `Copyable`.
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
  `to_lowercase()`, `encode_as_utf8()`, and `+` to join. Substring test: `s have "ell"`. Slices take ranges:
  `s[0 til 3]`, `s[2 til ^0]` (to the end).
- `Character` has `codepoint()`, `is_alphabetic()`, `is_digit()`, `is_whitespace()`, `to_uppercase()`, ….
- A binary float shows as the shortest decimal that reads back to the same value
  (`0.1_b64 + 0.2_b64` shows `0.30000000000000004`); a `Decimal` shows exactly (`0.1 + 0.2` shows `0.3`).

## 14. Collections

```suflae
var nums = [3, 1, 2]                      # List[Integer]
var ages = {"ada": 36, "alan": 41}        # Dict[Text, Integer]
var tags = {"red", "blue"}                # Set[Text]
var empty = List[Text]()

nums.add_last(value: 4)
show(nums[0])                             # 3
show(nums[^1])                            # 4, the last element
discard ages.add(key: "grace", value: 85)
show(ages["ada"])
if ages have "alan"
    show("found")
each entry in ages
    show(f"{entry.key} -> {entry.value}")
```

- `List`, `Dict` and `Set` are always available; `[]`, `{k: v}` and `{a, b}` are their literals. The
  specialized containers (`SortedDict`, `SortedList`, `SortedSet`, `CircularList`, `PriorityQueue`,
  `BitList`, `Array[T, COUNT]`) are constructor-only and come from `import Collections`.
- `List(from: value)` reads a list back from the `SerialValue` that `xs.serialize()` gives; it is failable
  (`try List[Integer](from: v)`).
- Collections are entities: shared, not copied. `xs.duplicate()` makes an independent copy, and a range
  slice `xs[1 til 3]` returns a new list.
- Indices and counts are `Integer`: `xs.count()`, `xs[i]`, `xs[1 til 3]`, `remove_at(index:)`,
  `xs.enumerate()` (pairs `(Integer, T)`). A negative index crashes with `NegativeIndexError`; count from the
  end with `^` (`xs[^1]`).
- The collections are Suflae's own, written in Suflae: `List`, `Dict`, `Set`, `SortedList`, `SortedSet`,
  `SortedDict`, `CircularList`, `PriorityQueue`, `BitList`, `BitArray`. `Array[T, COUNT]` is RazorForge's
  fixed-size record, counted and indexed with `Integer` too. RazorForge's `SplitList` / `SplitArray`
  (structure-of-arrays layouts) are not available in Suflae.
- Indexing `xs[i]` and `d[k]` crash when the index or key is missing; recover with `try xs[i]` /
  `try d[k]`. `remove_first()`, `remove_last()`, `remove_at(index:)`, `first()`, `last()` are failable the
  same way.
- `set.add(value:)`, `set.remove(value:)`, `dict.add(key:, value:)`, `dict.remove(key:)` return `Bool`:
  `discard` it when unused (SF-W007). `Dict` and `Set` iterate in insertion order.
- Lists: `add_first`, `add_last`, `add_at`, `contains`, `sort()`, `sorted()`, `sort_by(...)`,
  `reverse_in_place()`, `clear()`, `is_empty()`. `SortedList`/`SortedSet` have no positional index.
- Iterator adapters (`import IterTools`): `where`, `select`, `take`, `skip`, `take_while`, `distinct`,
  `reverse`, `enumerate`, `zip(a:, b:)`, `chain`, `min_by`, … are lazy; finish with `.List()` or `each`:
  `nums.where(predicate: x => x % 2 == 0).List()`.

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
- When the change is inside a called routine, the builder cannot see that `xs` and `nums` are the same
  list, so the loop marks the list as in use and the change crashes at run time with
  `ReshapingWhileInUseError`.
- Changing the list after the loop, after `break`, or after a `return` out of the loop is fine. Changing an
  element's contents (`boxes[0].n = 5`) is not an add or remove and is always fine.
- The check assumes a single coroutine; two coroutines changing and looping over the same list
  concurrently are not protected.

## 15. Console, files and resources

- Output: `show(...)`, `alert(...)` (stderr). Input: `ask_line()`, `ask_word()`, `ask_all()`,
  `ask_lines()`, `ask_words()`.
- Files are values that name a path (no I/O happens at construction): `File(at: "notes.txt")` with
  `exists()`, `read_text()`, `write_text(content:)`, `size()`, `extension()` (no leading dot), `parent()`,
  `copy_to(...)`, `move_to(...)`, `delete()`, `touch()`; `Directory(at: "data")` with `exists()`,
  `create()`, `create_all()`, `list()`, `entries()`, `walk()`, `file(name:)`, `subdir(name:)`. Routines
  that touch the disk and can fail (`size`, `delete`, `list`, …) are failable: recover with
  `try`/`grab`. `current_dir()`, `home_dir()`, `temp_dir()` are free routines. Pure path-string helpers
  (`join_path(a:, b:)`, `file_name`, `extension`, …) live in `import IO/FileSystem`.
- `read_text` / `write_text` are safe inside a coroutine: the coroutine waits while other coroutines run.
- **`using`** binds a resource for a block and releases it at the block's end, however the block is left:

```suflae
using File(at: "out.txt").open_write() as sink
    discard sink.store(data: "hello\n".encode_as_utf8())
```

  A type works with `using` when it has `enter` (its result is bound by `as`) and `exit` member routines.
  There is no `defer`.

## 16. Concurrency

Calling a `suspended routine` returns an `Agent[T]`: a recipe with its arguments bound. **Nothing runs until
a verb runs it.**

```suflae
suspended routine fetch(id: Integer) -> Integer
    waitfor(50ms)            # this coroutine waits; others run meanwhile
    return id * 10

routine start()
    var a = fetch(id: 1)                 # Agent[Integer], not started
    show(f"one => {a.retrieve()}")       # run it and wait for the value

    var jobs = List[Agent[Integer]]()
    jobs.add_last(value: fetch(id: 2))
    jobs.add_last(value: fetch(id: 3))
    var results = jobs.gather()          # run both at once, wait for all
    show(f"{results[0]} {results[1]}")
    return
```

- `agent.retrieve()` runs the agent and waits for its value; inside a coroutine it parks (siblings keep
  running). It is failable: a failure inside the routine crashes the caller unless recovered
  (`try a.retrieve()`).
- `agent.execute()` runs it in the background and returns at once; the value is discarded, and the work
  still finishes.
- `agents.gather()` runs a `List[Agent[T]]` concurrently and returns every result in input order;
  `agents.race()` returns the first finisher's value; `agents.cancel_all()` asks every agent to stop and
  returns their final values.
- `agent.waitfor(5s).retrieve()` waits at most 5 seconds and crashes with `TaskTimeoutError` past it;
  `try agent.waitfor(5s).retrieve()` gives `None` instead.
- Free routines: `waitfor(duration)` pauses (parks a coroutine), `yield_now()` lets siblings run,
  `cancellation_requested()` tells a long loop to stop early.
- An agent that no verb runs never runs; dropping one is warning SF-W008.
- Coroutines are cooperative: one that computes without ever waiting keeps the others from running.
- Coroutines that talk to each other (over a channel) must all be started before you wait on one:
  `producer(...).execute()` first, then `consumer(...).retrieve()`.
- **Shared entities are safe to share between coroutines.** The runtime may run coroutines on several
  cores. An entity that reaches another coroutine gets an atomic reference count and a lock taken around
  each access, with no code from you. As with globals, one statement is atomic; a read-then-write spread
  over several statements is not.
- There is no `threaded` routine and no thread type in Suflae.

**Channels** (always available):

```suflae
var (tx, rx) = make_channel[Integer](capacity: 4)
tx.send(item: 10)
tx.send(item: 20)
tx.close()
each n in rx
    show(f"{n}")
```

`make_channel[T](capacity:)` has one consumer (`capacity: 0` makes each send wait for a taker);
`make_shared_channel[T](capacity:)` lets several consumers compete for items. `send` is failable
(`ChannelClosedError`).

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

## 18. Unsafe code and foreign routines

Suflae is memory-safe without `danger`. When a library must manage memory itself or call C:

- `danger` opens a block where memory-unsafe operations are allowed: raw handles (`Hijacked[T]`),
  addresses, `peek`/`poke` of raw memory, strides.
- `dangerous routine name(...)` marks a routine whose signature exposes that unsafety; it can only be
  called inside a `danger` block.
- A foreign C function is declared as a routine qualified by its realm, with no body:
  `routine C::labs(n: S64) -> S64`. Calls use the same qualifier, with named arguments:
  `C::labs(n: x)`. LLVM intrinsics use `LLVM::`. There is no `external` keyword.
- Mark a foreign routine that may block for long (a read from a pipe, a sleep, a network wait) `@blocking`:
  called from a `suspended` coroutine it runs on one of the runtime's I/O threads while the coroutine parks,
  so other coroutines keep running; called anywhere else it is an ordinary call. Suflae has no OS threads of
  its own, so this is how a blocking library call stays out of the scheduler's way.
- Mark only what can actually corrupt memory. Arithmetic, collections and failable calls are never wrapped
  in `danger`.

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
    poke(target: hero, memvar: "hp", value: SerialValue(from: 25_s64))
    show(alias.hp)                                     # 25: every name sees the change
    var seen = watch(target: hero)
    show(seen.is_alive())                              # true
    return
```

- `type_name(of:)`, `memvars(of:)` (every member variable, `secret` ones included, in declaration order),
  `peek(target:)` (the whole object serialized), `peek(target:, memvar:)` (failable: absent for an unknown
  name), `poke(target:, memvar:, value:)` (entities only; throws `NoSuchMemberVariableError`),
  `watch(target:)` (an observation that does not keep the entity alive; `is_alive()`).

## 20. CLI and `config.toml`

```
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
| SF-W007       | ignored `Bool` result                               | `discard expr`                                    |
| SF-S435       | `var` at module level                               | `global name: Type = init`, or move it            |
| SF-S444       | routine reads a script's top-level `var`            | pass it as an argument, or make it a `global`     |
| SF-S619       | reading through an `E?` before checking it          | `if e isnot None`                                 |
| SF-S625       | adding/removing on the list being `each`-looped     | change it after the loop                          |
| SF-S704       | crashable without `crash_message()`                 | write `routine X.crash_message() -> Text`         |
| SF-S750/S751  | `throw`/`absent` in a routine without `!`           | declare it `routine name!(...)`                   |
| SF-G211       | `!` written at a call site                          | drop it; recover with `try`/`grab`/`lookup`       |
| SF-G212       | `x in coll` as a test                               | `coll have x`                                     |
| SF-W008       | an agent that is never run                          | `.retrieve()` or `.execute()` it                  |

## 22. Keywords

Every reserved word in Suflae. There is NO `for`, `def`, `fn`, `let`, `const`, `class`, `struct`, `enum`,
`match`, `trait`, `impl`, `async`, `await`, `spawn`, `external`, `null`, `self` or `this`.

- **Declarations**: `routine` `entity` `record` `choice` `flags` `crashable` `variant` `protocol`
- **Bindings**: `var` `preset` `global` `lateinit`
- **Visibility / receiver**: `secret` `posted` `common` · **Self**: `me` `Me`
- **Protocols and constraints**: `obeys` `disobeys` `needs` `onlyif` `relates` `everywhere`
- **Control flow**: `if` `elseif` `else` `then` `unless` `when` `is` `isnot` `loop` `while` `each`
  `break` `continue` `return` `throw` `pierce` `absent`
- **Recovery**: `try` `grab` `lookup`
- **Ranges and containment**: `in` (loops only) `have` `lack` `to` `til` `by`
- **Modules**: `import` `module`
- **Other**: `using` `as` `define` `given` `discard` `pass` (an empty body: a type's, or a block's that
  does nothing)
- **Logic**: `and` `or` `not` `but`
- **Literals**: `true` `false` `None` `none`
- **Concurrency**: `suspended`
- **Unsafe code**: `danger` `dangerous`

`!` (failable, on declarations) is a mark on the name, not a keyword. `steal`, `threaded` and `expand` are
not Suflae words.

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
  included), and it is released once.
- **A RazorForge entity may not cross a Suflae routine signature by value (SF-S439).** As a parameter or a
  (non-constructor) return type it would be released more than once. Hand it across as `Retained[T]` (to
  keep it), as `Consulting[T]` / `Amending[T]` (to read / write it during the call), or keep it in a member
  variable of a Suflae entity.
- `RF::Name` and `SF::Name` name a type in a specific language when both have one with that name.
- **Collections do not cross as-is.** Suflae's `List` is not RazorForge's (`RF::List`, counted with `U64`).
  Convert at the boundary: `List(from: rf_list)` makes a Suflae list from a RazorForge one, and
  `xs.to_razorforge()` makes a RazorForge list from a Suflae one.

## 24. Status (do not generate as if shipped)

Built and tested: the language described above, end to end: shared entities with cycle collection,
nullable entities, script mode, globals, `Integer`/`Decimal` defaults, the error model, `suspended`
coroutines and agents, the iterate-and-mutate checks, `danger`/`C::`, and the basic `ObjectHacker`
surface. The `StdlibSf` fixtures run in CI with their expected output.

Not built yet:

- a REPL and hot reload;
- stronger runtime reflection in `ObjectHacker` beyond reading, writing and watching member variables.

When generating Suflae: start from the closest `Suflae/tests/Fixtures/StdlibSf/*.sf` program, name every
argument, end every routine with `return`, use bare `Integer`/`Decimal` numbers, and prefer `when` over
nested conditionals.
