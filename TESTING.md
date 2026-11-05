# Testing SignalSafe

This guide shows how to run SignalSafe on your own machine, try it on the sample layouts, and send feedback.

## 1. One-time setup

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and Git. Check them in Terminal:

```bash
dotnet --list-sdks
```

```bash
git --version
```

Get the code (first time only):

```bash
git clone https://github.com/velam-varsha/signalsafe.git
```

```bash
cd signalsafe
```

Later, to get the newest version:

```bash
git pull
```

## 2. Run the automated tests

```bash
dotnet test
```

Expected: `Passed! - Failed: 0` and the total number of tests. Any `Failed` line is a bug: copy the whole output into your feedback.

## 3. Check a layout with the command-line tool

```bash
dotnet run --project src/SignalSafe.Cli -- samples/westbrook-junction.json
```

```bash
dotnet run --project src/SignalSafe.Cli -- samples/kestrel-lane-crossover.json
```

The tool prints:

1. **Layout check**: data problems in the file, or `OK`.
2. **Routes**: every route the engine found, with the sections, points positions and overlap it needs.
3. **Conflicting routes**: pairs that must never be set together, and why.

## 4. The sample layouts

Compare the tool's output with these drawings. Arrows show the direction each signal reads.

### Westbrook Junction

```
                              T5              T6
                         .----------- S5> -----------  Millbrook branch
                        /
UP MAIN   T1       T2  / P1      T3            T4
  ------------ S1> ---*---------------- S3> ----------  (trains run west to east)


DOWN MAIN  T9        T10      T12          T13          T14
  ----------- <S6 ---*------ <S4 --------------- <S2 ----------  (trains run east to west)
                      \ P4
                       \ T20
                        <S7 ---- T22 ----|  siding (buffer stop)
```

Expected routes: `S1(M)-S3`, `S1(M)-S5`, `S2(M)-S4`, `S4(M)-S6`, `S7(S)-S6` (shunt from the siding).

### Kestrel Lane Crossover

```
UP MAIN (west to east)
      T31             T32               T33               T34
  --------- <S30 S31> ------ P11 -------------- S33> -----------
                               \
                                \ TX (crossover)
                                 \
  --------- <S40 ----------------- P12 ------ <S42 S43> ------ <S44 ------
      T40             T41                 T42           T43          T44
DOWN MAIN (east to west)
```

P11 and P12 are the two ends of one crossover. S30 and S43 are for wrong-direction moves over the crossover.

Expected routes: `S31(M)-S33`, `S31(M)-S43` (across the crossover), `S42(M)-S40`, `S42(M)-S30` (across the crossover), `S44(M)-S42`.

## 5. Experiments to try

Copy a sample first, so the original stays unchanged:

```bash
cp samples/westbrook-junction.json my-test.json
```

Open `my-test.json` in VS Code, make one change, save, and run:

```bash
dotnet run --project src/SignalSafe.Cli -- my-test.json
```

| Try this | What should happen |
| --- | --- |
| Change P1's `"reverse": "T5"` to `"reverse": "T3"` | Layout problem: toe, normal and reverse must be different |
| Give two signals the same ID | Layout problem: ID used twice |
| Delete the whole `points` entry for P4 | Layout problem: three sections meet at P4 without points |
| Change S5's `"protects"` to `"T3"` | Layout problem: T3 does not start at S5's position |
| Delete signal S3 | Route `S1(M)-S3` disappears; the search runs on to the line end |
| Add a signal `{ "id": "S8", "type": "Main", "atNode": "J8", "protects": "T22" }` | No new route: S8 reads into the buffer stop |
| Delete a comma or bracket | A clear "not valid JSON" message, not a crash |

## 6. What to look at as a signalling engineer

- Are the routes the ones you would expect from the drawing? Any missing or extra?
- Are the point positions right for each route, including trailing points?
- Are the conflicting pairs right? Any you would add or remove?
- Do the three MVP simplifications in the [README](README.md#current-simplifications) match UK practice well enough, or what should change first?

## 7. Sending feedback

Copy this into a message:

```
Test date:
dotnet test result (pass/fail, number of tests):

Westbrook output correct? yes / no, details:
Kestrel output correct? yes / no, details:

Experiments: anything that did not behave as described?

Signalling feedback (routes, points, conflicts, overlaps):

Anything confusing in the output or this guide:
```
