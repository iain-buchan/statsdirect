# The Randomization menu: checks by calculation

Build the program, and then run with the .NET 10 SDK:

```
dotnet build StatsDirectUI/StatsDirectUI.csproj -c Debug
dotnet run --project tests/Randomization -c Release
```

The checks are made on a build of the program, as those of `tests/ExactTests` are: `StatsDirect.dll` of the Debug configuration, or of the folder given with `-p:StatsDirectBin=...`. They take about six seconds. One part can be run alone by naming it: `always`, `order`, `chances` or `seeds`.

The functions are the randomization of a series x to y, of intervention-control pairs, of subjects into two independent groups and into blocks of treatments, and the allocation of subjects to groups by their preferences. An allocation is made of random numbers, and has no figure to be compared with a benchmark; what is checked is what it is to be whatever the numbers are, and that it is made of them as it is to be.

**What an allocation is to be, whatever the seed** (`always`). 400 seeds, each with a series, pairs, two groups, blocks and preferences, of sizes drawn at random. A series has every number from x to y once, whichever of the two is given first. Balanced pairs have the control first in half of them, if their number is even, and the report says that the allocation is balanced if it is. Two groups have every subject once, and are of one size. Blocks of one size have every treatment as often as another in every block, and the report says if the last block is smaller; an allocation in blocks of random size can be cut into blocks of 2, 3 or 4 times the number of treatments, and a last block of what is left, each with every treatment as often as another. With preferences no group has more subjects than places; a subject has a group that it preferred unless all that it preferred are full; and it has none of its lower preferences while a higher one has a place left.

**The order is that of the shuffle** (`order`). The shuffle exchanges each place, from the last to the second, with a place drawn from the places up to it: every order is then equally likely, if the numbers drawn are. For 300 seeds the series, the balanced pairs, the two groups, the blocks and the subjects who are given the places of a group that more prefer than it has places are compared with what the shuffle gives, worked out in the tests with the generator of the program and the same seed. Pairs that are not balanced have the control first if the number drawn for them is a half or more.

**How often each arrangement comes** (`chances`). Over 30,000 seeds the 24 orders of a series of 4, the 20 arrangements of 6 balanced pairs, the 8 of 3 pairs that are not balanced, the 20 first groups of 6 subjects, the 36 allocations of two blocks of 4, the 10 pairs of subjects that can have the 2 places of a group that 5 prefer, and the 5 subjects who can have the one place of another group are counted: chi-square of the counts is to be below the value that is passed once in a million times if the arrangements are equally likely. This part shows a fault that is gross. It does not show the difference that the shuffle of three passes had, which the balanced pairs and the preferences used before: the chances of the arrangements of 6 balanced pairs were 0.04999 to 0.05001. That difference is shown by the part before.

**The seed, and the limits** (`seeds`). The same seed gives the same allocation, and another seed another. With the seed left blank the report has the seed that was used, and that seed gives the allocation again. Treatments are named A to Z, and then AA, AB and so on. What is refused, and the words of it. With preferences: more places left than subjects; capacities of thousands of millions; a subject that is left has a group with a chance in proportion to the places that the group has left; subjects without a preference. A million subjects in blocks and a series of a million take less than ten seconds.

**What is not checked here.** The reports as they are shown. The generator of the random numbers (the Mersenne Twister), which is taken as it is.

The run is the same every time. The exit code is 0 if every check passes.
