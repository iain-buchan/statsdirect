// The Randomization menu: checks by calculation.  A series x to y, intervention-control pairs, two independent groups, blocks and
// allocation by preference are given thousands of seeds and sizes.  What is checked is what an allocation is to be whatever the
// seed (every subject allocated once, the sizes of the groups, the blocks, the capacities and the preferences), that the order is
// the one that the shuffle gives with the random numbers of the seed, how often each arrangement comes over many seeds, and that
// the report has the seed with which the allocation can be made again.
using System.Globalization;
using System.Reflection;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;

internal static class Program
{
    private const double M = double.MinValue;
    private static int failures, checks;
    private static readonly CultureInfo inv = CultureInfo.InvariantCulture;

    private static void Say(bool ok, string what)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("FAIL  " + what); }
    }

    private static string Message(Exception ex)
    {
        Exception inner = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
        return inner.GetType().Name + ": " + inner.Message.Replace("\n", " ").Replace("\r", " ");
    }

    private static string Refused(Func<ParameterBag> report)
    {
        try { report(); return null; } catch (Exception ex) { return Message(ex); }
    }

    private static List<ParameterBag> Rows(ParameterBag bag, string block) =>
        !bag.ContainsKey(block) || bag[block].AsObject == null ? new List<ParameterBag>() : ((System.Collections.IEnumerable)bag[block].AsObject).Cast<ParameterBag>().ToList();

    private static DataFrame Frame(params double[][] columns)
    {
        DataFrame frame = new();
        for (int i = 0; i < columns.Length; i++) frame.Variables.Add(new DoubleVariable(columns[i], "c" + (i + 1).ToString(inv)));
        return frame;
    }

    // ---- the reports of the menu; a seed of null is a seed that is left blank
    private static ParameterBag Bag(int? seed, params (string name, object value)[] inputs)
    {
        ParameterBag p = new();
        if (seed.HasValue) p.AddInput("seed", seed.Value);
        foreach (var (name, value) in inputs) p.AddInput(name, value);
        return p;
    }

    private static ParameterBag Series(int low, int high, int? seed) => Formula.RptRandomXY(Bag(seed, ("low", low), ("high", high))).ParameterBag;
    private static ParameterBag Pairs(int pairs, bool balance, int? seed) => Formula.RptRandomPairs(Bag(seed, ("pairs", pairs), ("balance", balance))).ParameterBag;
    private static ParameterBag Groups(int subjects, int? seed) => Formula.RptRandomUnPaired(Bag(seed, ("high", subjects))).ParameterBag;
    private static ParameterBag Blocks(int subjects, int block, int treatments, int? seed) =>
        Formula.RptRandomBlock(block > 0 ? Bag(seed, ("n", subjects), ("b", block), ("t", treatments)) : Bag(seed, ("n", subjects), ("t", treatments))).ParameterBag;
    private static ParameterBag Preferences(double[] capacities, double[][] preferences, int seed) =>
        Describe.RptPreferences(Bag(seed, ("capacities", Frame(capacities)), ("preferences", Frame(preferences)))).ParameterBag;

    private static int[] SeriesOf(ParameterBag report) => Rows(report, "*allocations").Select(r => r["random"].AsInt32).ToArray();
    private static string PairsOf(ParameterBag report) => new(Rows(report, "*pairs").Select(r => r["random"].AsString == "Control - Intervention" ? 'C' : r["random"].AsString == "Intervention - Control" ? 'I' : '?').ToArray());
    private static string[] BlocksOf(ParameterBag report) => Rows(report, "*subjects").Select(r => r["rx"].AsString).ToArray();
    private static int[] PreferencesOf(ParameterBag report) => Rows(report, "*groups").Select(r => r["grp"].AsInt32).ToArray();

    // The shuffle: from the last place to the second, a place is exchanged with one drawn from the places up to it.  Every order
    // is then equally likely, if the numbers drawn are
    private static void Shuffle<T>(MersenneTwister mt, T[] x, int low, int high)
    {
        for (int j = high; j > low; j--)
        {
            int k = low + (int)Math.Floor((j - low + 1) * mt.NextDouble());
            (x[j], x[k]) = (x[k], x[j]);
        }
    }

    // chi-square of counts that are to be equal
    private static double ChiSquare(IEnumerable<int> counts, int kinds)
    {
        int[] c = counts.ToArray();
        double expected = (double)c.Sum() / kinds;
        return c.Sum(x => (x - expected) * (x - expected) / expected) + (kinds - c.Length) * expected;
    }

    private static void Counted(Dictionary<string, int> counts, string key) => counts[key] = counts.GetValueOrDefault(key) + 1;

    private static void Always()
    {
        Console.WriteLine("What an allocation is to be, whatever the seed");
        int before = failures;
        System.Random random = new(20260929);
        for (int seed = 1; seed <= 400; seed++)
        {
            // a series: every number from x to y once, whichever of the two is given first
            int low = random.Next(-50, 50), high = low + random.Next(0, 300);
            int[] series = SeriesOf(seed % 2 == 0 ? Series(low, high, seed) : Series(high, low, seed));
            Say(series.Length == high - low + 1 && series.OrderBy(v => v).SequenceEqual(Enumerable.Range(low, high - low + 1)), $"the series {low} to {high} with the seed {seed} has every number once");

            // pairs: as many one way as the other if that is asked for and the number is even
            int pairs = 1 + random.Next(200);
            string order = PairsOf(Pairs(pairs, true, seed));
            Say(order.Length == pairs && !order.Contains('?') && (pairs % 2 == 1 || order.Count(c => c == 'C') == pairs / 2), $"{pairs} balanced pairs with the seed {seed}: {order.Count(c => c == 'C')} have the control first");
            Say(Pairs(pairs, true, seed)["seedAndNote"].AsObject.ToString() == (pairs % 2 == 0 ? seed + ",  balanced allocation" : seed.ToString()), $"{pairs} pairs: the report says that the allocation is balanced if it is");

            // two groups: every subject in one of two groups of one size, each in the order of the subjects
            int subjects = 2 * (1 + random.Next(150));
            List<ParameterBag> rows = Rows(Groups(subjects, seed), "*allocations");
            int[] first = rows.Select(r => r["case"].AsInt32).ToArray(), second = rows.Select(r => r["control"].AsInt32).ToArray();
            Say(first.Length == subjects / 2 && first.Concat(second).OrderBy(v => v).SequenceEqual(Enumerable.Range(1, subjects)) && first.SequenceEqual(first.OrderBy(v => v)) && second.SequenceEqual(second.OrderBy(v => v)),
                $"two groups of {subjects} subjects with the seed {seed} have every subject once");

            // blocks: every treatment as often as another in every block
            int treatments = 2 + random.Next(5), block = seed % 3 == 0 ? 0 : treatments * (1 + random.Next(4));
            int n = treatments * (1 + random.Next(40));
            ParameterBag blocks = Blocks(n, block, treatments, seed);
            string[] given = BlocksOf(blocks);
            string differs = given.Length != n ? $"{given.Length} subjects are allocated" : null;
            if (block > 0)
            {
                for (int start = 0; start < n && differs == null; start += block)
                {
                    string[] one = given.Skip(start).Take(block).ToArray();
                    if (one.GroupBy(t => t).Count() != treatments || one.GroupBy(t => t).Any(g => g.Count() != one.Length / treatments)) differs = $"the block from subject {start + 1} is {string.Join("", one)}";
                }
                Say(Rows(blocks, "*blockSizeWarn").Count == (n % block == 0 ? 0 : 1), $"{n} subjects in blocks of {block}: the report says if the last block is smaller");
            }
            else
            {
                // Blocks of 2, 3 or 4 times the number of treatments, and a last block of what is left: the allocation can be
                // cut into such blocks, each with every treatment as often as another.  The places that a block can start at
                // are found from the start of the allocation
                bool Even(int from, int size)
                {
                    string[] one = given.Skip(from).Take(size).ToArray();
                    return one.GroupBy(t => t).Count() == treatments && one.GroupBy(t => t).All(g => g.Count() == size / treatments);
                }
                bool[] starts = new bool[n + 1];
                starts[0] = true;
                for (int start = 0; start < n; start++)
                {
                    if (!starts[start]) continue;
                    foreach (int times in new[] { 2, 3, 4 })
                        if (start + times * treatments <= n && n - start >= 4 * treatments && Even(start, times * treatments)) starts[start + times * treatments] = true;
                    if (n - start < 4 * treatments && Even(start, n - start)) starts[n] = true;
                }
                if (!starts[n]) differs = $"the allocation is not one of blocks: {string.Join("", given.Take(60))}";
            }
            Say(differs == null && given.All(t => t.Length == 1 && t[0] >= 'A' && t[0] < 'A' + treatments), $"{n} subjects, {treatments} treatments, blocks of {(block > 0 ? block.ToString() : "random size")}, seed {seed}: {differs}");

            // Preferences: no group has more subjects than places; a subject has a group that it preferred unless all that it
            // preferred are full; and it has none of its lower preferences while a higher one has a place left
            int groups = 2 + random.Next(6), students = 1 + random.Next(40), columns = 1 + random.Next(Math.Min(3, groups));
            double[] capacities = Enumerable.Range(0, groups).Select(_ => (double)random.Next(0, 12)).ToArray();
            if (capacities.Sum() < students) capacities[random.Next(groups)] += students - capacities.Sum() + random.Next(30);
            double[][] preferred = Enumerable.Range(0, columns).Select(_ => Enumerable.Range(0, students).Select(_ => random.Next(12) == 0 ? M : 1.0 + random.Next(groups)).ToArray()).ToArray();
            ParameterBag allocation = Preferences(capacities, preferred, seed);
            int[] group = PreferencesOf(allocation);
            int[] used = new int[groups + 1];
            foreach (int g in group) if (g >= 1 && g <= groups) used[g]++;
            differs = group.Length != students || group.Any(g => g < 1 || g > groups) ? "a subject has no group" : null;
            for (int g = 1; g <= groups && differs == null; g++) if (used[g] > capacities[g - 1]) differs = $"group {g} has {used[g]} subjects and {capacities[g - 1]} places";
            for (int s = 0; s < students && differs == null; s++)
            {
                // the preferences of the subject in their order; one that is missing is the first again
                int[] wants = Enumerable.Range(0, columns).Select(c => preferred[c][s] == M ? (preferred[0][s] == M ? 0 : (int)preferred[0][s]) : (int)preferred[c][s]).ToArray();
                int level = Array.IndexOf(wants, group[s]);
                for (int c = 0; c < (level < 0 ? columns : level) && differs == null; c++)
                    if (wants[c] > 0 && used[wants[c]] < capacities[wants[c] - 1]) differs = $"subject {s + 1} has the group {group[s]}, and group {wants[c]} of its preference {c + 1} has a place left";
            }
            Say(differs == null && Math.Abs(allocation["capacity"].AsDouble - capacities.Sum()) < 0.5, $"{students} subjects with {columns} preferences of {groups} groups, seed {seed}: {differs}");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  400 seeds, each with a series, pairs, two groups, blocks and preferences");
    }

    private static void Order()
    {
        Console.WriteLine();
        Console.WriteLine("The order is that of the shuffle, with the random numbers of the seed");
        int before = failures;
        for (int seed = 1; seed <= 300; seed++)
        {
            int n = 2 + seed % 61;
            // a series
            int[] expected = Enumerable.Range(10, n).ToArray();
            Shuffle(new MersenneTwister(seed), expected, 0, n - 1);
            Say(SeriesOf(Series(10, 9 + n, seed)).SequenceEqual(expected), $"the series 10 to {9 + n} with the seed {seed}");
            // balanced pairs: the control first in the first pair, the third and so on, and then the shuffle
            int pairs = 2 * (1 + seed % 40);
            char[] order = Enumerable.Range(0, pairs).Select(i => i % 2 == 0 ? 'C' : 'I').ToArray();
            Shuffle(new MersenneTwister(seed), order, 0, pairs - 1);
            Say(PairsOf(Pairs(pairs, true, seed)) == new string(order), $"{pairs} balanced pairs with the seed {seed}: {PairsOf(Pairs(pairs, true, seed))}, and the shuffle gives {new string(order)}");
            // pairs that are not balanced: the control first if the number drawn is a half or more
            MersenneTwister mt = new(seed);
            string coins = new(Enumerable.Range(0, pairs + 1).Select(_ => mt.NextDouble() >= 0.5 ? 'C' : 'I').ToArray());
            Say(PairsOf(Pairs(pairs + 1, true, seed)) == coins && PairsOf(Pairs(pairs + 1, false, seed)) == coins, $"{pairs + 1} pairs with the seed {seed}");
            // two groups: the first half of the shuffled subjects and the second
            int[] subjects = Enumerable.Range(1, 2 * n).ToArray();
            Shuffle(new MersenneTwister(seed), subjects, 0, 2 * n - 1);
            List<ParameterBag> rows = Rows(Groups(2 * n, seed), "*allocations");
            Say(rows.Select(r => r["case"].AsInt32).SequenceEqual(subjects.Take(n).OrderBy(v => v)) && rows.Select(r => r["control"].AsInt32).SequenceEqual(subjects.Skip(n).OrderBy(v => v)), $"two groups of {2 * n} subjects with the seed {seed}");
            // blocks of one size: each block is the treatments in their order, as often as the block has room for, and then shuffled
            int treatments = 2 + seed % 4, block = treatments * (1 + seed % 3), total = block * (1 + seed % 5) + (seed % 2) * treatments;
            mt = new MersenneTwister(seed);
            List<string> blocks = new();
            for (int start = 0; start < total; start += block)
            {
                int size = Math.Min(block, total - start);
                string[] one = Enumerable.Range(0, size).Select(i => ((char)('A' + i % treatments)).ToString()).ToArray();
                Shuffle(mt, one, 0, size - 1);
                blocks.AddRange(one);
            }
            Say(BlocksOf(Blocks(total, block, treatments, seed)).SequenceEqual(blocks), $"{total} subjects in blocks of {block} with {treatments} treatments and the seed {seed}");
            // preferences: a group of fewer places than the subjects who prefer it has the first of them in the shuffled order
            int candidates = 3 + seed % 20, places = 1 + seed % (candidates - 1);
            int[] shuffled = Enumerable.Range(1, candidates).ToArray();
            Shuffle(new MersenneTwister(seed), shuffled, 0, candidates - 1);
            int[] group = PreferencesOf(Preferences(new double[] { places, candidates }, new[] { Enumerable.Repeat(1.0, candidates).ToArray() }, seed));
            Say(Enumerable.Range(1, candidates).Where(s => group[s - 1] == 1).SequenceEqual(shuffled.Take(places).OrderBy(v => v)) && group.All(g => g == 1 || g == 2),
                $"{places} places for {candidates} subjects with the seed {seed}: the subjects {string.Join(",", Enumerable.Range(1, candidates).Where(s => group[s - 1] == 1))} have them, and the shuffle gives {string.Join(",", shuffled.Take(places).OrderBy(v => v))}");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  300 seeds, each with a series, pairs, two groups, blocks and a group with fewer places than candidates");
    }

    private static void Chances()
    {
        Console.WriteLine();
        Console.WriteLine("How often each arrangement comes, over 30,000 seeds");
        int before = failures;
        Dictionary<string, int> series = new(), pairs = new(), coins = new(), groups = new(), blocks = new(), random = new(), places = new(), left = new();
        for (int seed = 1; seed <= 30000; seed++)
        {
            Counted(series, string.Join(",", SeriesOf(Series(1, 4, seed))));
            Counted(pairs, PairsOf(Pairs(6, true, seed)));
            Counted(coins, PairsOf(Pairs(3, false, seed)));
            Counted(groups, string.Join(",", Rows(Groups(6, seed), "*allocations").Select(r => r["case"].AsInt32)));
            Counted(blocks, string.Join("", BlocksOf(Blocks(8, 4, 2, seed))));
            Counted(random, string.Join("", BlocksOf(Blocks(8, 0, 2, seed))).Substring(0, 4));
            // 2 places for 5 subjects who all prefer the group: which two have them
            int[] group = PreferencesOf(Preferences(new double[] { 2, 1, 2 }, new[] { new double[] { 1, 1, 1, 1, 1 } }, seed));
            Counted(places, string.Join("", group.Select(g => g == 1 ? '1' : '-')));
            // the three who are left have the three places of the other two groups: which of them has the place of the second group
            Counted(left, Array.IndexOf(group, 2).ToString());
        }
        // The values of chi-square that are passed once in a million times if the arrangements are equally likely are 73 for 23
        // degrees of freedom, 66 for 19, 58 for 15, 44 for 9, 40 for 7 and 38 for 5
        void Equal(string what, Dictionary<string, int> counts, int kinds, double limit)
        {
            double chi = ChiSquare(counts.Values, kinds);
            Say(counts.Count == kinds && chi < limit, $"{what}: {counts.Count} arrangements of {kinds}, the least {counts.Values.Min()} times and the greatest {counts.Values.Max()}; chi-square {chi:F1}, which is to be below {limit}");
        }
        Equal("the orders of a series of 4", series, 24, 73);
        Equal("the arrangements of 6 balanced pairs", pairs, 20, 66);
        Equal("the arrangements of 3 pairs that are not balanced", coins, 8, 40);
        Equal("the first of two groups of 6 subjects", groups, 20, 66);
        Equal("two blocks of 4 with 2 treatments", blocks, 36, 110);
        Equal("which 2 of 5 subjects have the places of the group that all prefer", places, 10, 44);
        // the first block of random size has 4, 6 or 8 subjects: its first four are any 4 of A and B but AAAA and BBBB with a
        // block of 4, and those with a block of 6 too
        Say(random.Count >= 6 && random.Count <= 16 && random.Keys.All(k => k.Length == 4), $"the first four subjects of blocks of random size have {random.Count} arrangements");
        Equal("which of the 5 subjects has the one place of the second group", left.Where(c => c.Key != "-1").ToDictionary(c => c.Key, c => c.Value), 5, 38);
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the arrangements are as often as each other, as nearly as 30,000 seeds can show");
    }

    private static void Seeds()
    {
        Console.WriteLine();
        Console.WriteLine("The seed, and the limits");
        int before = failures;
        // the same seed gives the same allocation, and another seed another
        Say(SeriesOf(Series(1, 40, 10)).SequenceEqual(SeriesOf(Series(1, 40, 10))) && !SeriesOf(Series(1, 40, 10)).SequenceEqual(SeriesOf(Series(1, 40, 11))), "a series with the seed 10 twice, and with the seed 11");
        Say(PairsOf(Pairs(50, true, 10)) == PairsOf(Pairs(50, true, 10)) && PairsOf(Pairs(50, true, 10)) != PairsOf(Pairs(50, true, 11)), "pairs with the seed 10 twice, and with the seed 11");
        Say(BlocksOf(Blocks(40, 0, 2, 10)).SequenceEqual(BlocksOf(Blocks(40, 0, 2, 10))) && !BlocksOf(Blocks(40, 0, 2, 10)).SequenceEqual(BlocksOf(Blocks(40, 0, 2, 11))), "blocks with the seed 10 twice, and with the seed 11");
        // A seed that is left blank: the report has the seed that was used, and that seed gives the allocation again
        {
            ParameterBag blank = Series(1, 30, null);
            bool has = blank.ContainsKey("seed_out");
            Say(has && SeriesOf(Series(1, 30, blank["seed_out"].AsInt32)).SequenceEqual(SeriesOf(blank)), $"a series without a seed: the report {(has ? "has the seed " + blank["seed_out"].AsObject : "has no seed")}");
            blank = Groups(30, null);
            has = blank.ContainsKey("seed_out");
            Say(has && Rows(Groups(30, blank["seed_out"].AsInt32), "*allocations").Select(r => r["case"].AsInt32).SequenceEqual(Rows(blank, "*allocations").Select(r => r["case"].AsInt32)), $"two groups without a seed: the report {(has ? "has the seed " + blank["seed_out"].AsObject : "has no seed")}");
            blank = Blocks(30, 0, 3, null);
            Say(blank.ContainsKey("seed_out") && BlocksOf(Blocks(30, 0, 3, blank["seed_out"].AsInt32)).SequenceEqual(BlocksOf(blank)), "blocks without a seed: the report has the seed");
            blank = Pairs(30, true, null);
            string seed = blank["seedAndNote"].AsObject.ToString().Split(',')[0];
            Say(int.TryParse(seed, out int used) && PairsOf(Pairs(30, true, used)) == PairsOf(blank), "pairs without a seed: the report has the seed");
            Say(Series(1, 5, 77)["seed_out"].AsInt32 == 77 && Groups(6, 77)["seed_out"].AsInt32 == 77 && Blocks(6, 0, 2, 77)["seed_out"].AsInt32 == 77, "a seed that is entered is the seed of the report");
        }
        // the names of the treatments: A to Z, and then AA, AB and so on
        {
            string[] names = BlocksOf(Blocks(60, 30, 30, 7)).Take(30).OrderBy(t => t.Length).ThenBy(t => t, StringComparer.Ordinal).ToArray();
            Say(names.SequenceEqual(Enumerable.Range(0, 26).Select(i => ((char)('A' + i)).ToString()).Concat(new[] { "AA", "AB", "AC", "AD" })), $"30 treatments are named {string.Join(" ", names)}");
            names = BlocksOf(Blocks(1406, 703, 703, 7)).Take(703).OrderBy(t => t.Length).ThenBy(t => t, StringComparer.Ordinal).ToArray();
            Say(names.Distinct().Count() == 703 && names[26] == "AA" && names[701] == "ZZ" && names[702] == "AAA", $"703 treatments: the 27th is {names[26]}, the 702nd {names[701]} and the 703rd {names[702]}");
        }
        // what is refused, and the words of it
        void Refuses(string what, Func<ParameterBag> report, string words)
        {
            string refused = Refused(report);
            Say(refused != null && refused.Contains(words), $"{what} is refused, and the refusal says why ({refused ?? "it is not refused"})");
        }
        Refuses("no pairs", () => Pairs(0, true, 1), "At least one pair");
        Refuses("an odd number of subjects for two groups", () => Groups(7, 1), "must be even");
        Refuses("one subject in blocks", () => Blocks(1, 0, 2, 1), "At least two subjects");
        Refuses("10 subjects for 4 treatments", () => Blocks(10, 4, 4, 1), "divisible by the number of treatments");
        Refuses("blocks of 6 for 4 treatments", () => Blocks(12, 6, 4, 1), "Block size must be divisible");
        Refuses("a capacity of 2.5", () => Preferences(new[] { 2.5, 3 }, new[] { new double[] { 1, 2 } }, 1), "whole number of places");
        Refuses("a capacity below zero", () => Preferences(new double[] { -1, 3 }, new[] { new double[] { 1, 2 } }, 1), "whole number of places");
        Refuses("a preference of 1.5", () => Preferences(new double[] { 2, 3 }, new[] { new[] { 1.5, 2 } }, 1), "not a whole number");
        Refuses("a preference for group 3 of 2", () => Preferences(new double[] { 2, 3 }, new[] { new double[] { 1, 3 } }, 1), "is not the number of a group (1 to 2)");
        Refuses("more preferences than groups", () => Preferences(new double[] { 2, 3 }, new[] { new double[] { 1, 2 }, new double[] { 2, 1 }, new double[] { 1, 1 } }, 1), "fewer groups than preferences");
        Refuses("more subjects than places", () => Preferences(new double[] { 1, 1 }, new[] { new double[] { 1, 2, 1 } }, 1), "more subjects (3) than total capacity of groups (2)");
        // Preferences: more places left than subjects, and capacities of thousands of millions
        {
            string refused = Refused(() => Preferences(new double[] { 1, 50 }, new[] { new double[] { 1, 1, 1 } }, 12345));
            Say(refused == null && PreferencesOf(Preferences(new double[] { 1, 50 }, new[] { new double[] { 1, 1, 1 } }, 12345)).OrderBy(g => g).SequenceEqual(new[] { 1, 2, 2 }), $"three subjects who prefer a group of 1 place, with 50 places in the other ({refused})");
            refused = Refused(() => Preferences(new double[] { 2, 1e9, 2e9 }, new[] { new double[] { 1, 1, 1, 1 } }, 12345));
            Say(refused == null && Preferences(new double[] { 2, 1e9, 2e9 }, new[] { new double[] { 1, 1, 1, 1 } }, 12345)["capacity"].AsDouble == 3000000002.0, $"capacities of 2, 1,000 million and 2,000 million ({refused})");
            // the subjects that are left have the groups in proportion to the places that the groups have left: 1 to 3 here
            int second = 0;
            for (int seed = 1; seed <= 8000; seed++) if (PreferencesOf(Preferences(new double[] { 0, 1e9, 3e9 }, new[] { new double[] { 1 } }, seed))[0] == 2) second++;
            Say(Math.Abs(second - 2000) < 5 * Math.Sqrt(8000 * 0.25 * 0.75), $"a subject that is left has the group of 1,000 million places {second} times of 8,000, and the group of 3,000 million the other times");
            // no preference at all, and a first preference that is missing
            int[] none = PreferencesOf(Preferences(new double[] { 1, 1, 1 }, new[] { new[] { M, M, M } }, 5));
            Say(none.OrderBy(g => g).SequenceEqual(new[] { 1, 2, 3 }), $"three subjects without a preference have the groups {string.Join(" ", none)}");
        }
        // a million subjects
        {
            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
            string[] million = BlocksOf(Blocks(1000000, 0, 2, 3));
            int[] series = SeriesOf(Series(1, 1000000, 3));
            double seconds = clock.Elapsed.TotalSeconds;
            Say(million.Count(t => t == "A") == 500000 && series.Select(v => (long)v).Sum() == 500000500000L && seconds < 10, $"a million subjects in blocks and a series of a million take {seconds:F1} seconds");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the seed of the report, the names of the treatments, what is refused, places and capacities, a million subjects");
    }

    private static int Main(string[] args)
    {
        string which = args.Length > 0 ? args[0] : "all";
        if (which is "all" or "always") Always();
        if (which is "all" or "order") Order();
        if (which is "all" or "chances") Chances();
        if (which is "all" or "seeds") Seeds();
        Console.WriteLine();
        Console.WriteLine(failures == 0 ? $"ALL {checks} CHECKS PASS" : $"{failures} OF {checks} CHECKS FAILED");
        return failures == 0 ? 0 : 1;
    }
}
