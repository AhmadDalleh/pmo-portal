namespace DataGenerator.Generation;

/// <summary>A seeded random source. Same seed, same sequence, same CSV files.</summary>
public sealed class Rng
{
    private readonly Random _random;

    public Rng(int seed)
    {
        _random = new Random(seed);
    }

    /// <summary>Whole number between min and max, both included.</summary>
    public int Between(int min, int max) => _random.Next(min, max + 1);

    public bool Chance(double probability) => _random.NextDouble() < probability;

    public T Pick<T>(IReadOnlyList<T> items) => items[_random.Next(items.Count)];

    public List<T> Shuffle<T>(IEnumerable<T> items) =>
        items.Select(item => (item, key: _random.Next())).OrderBy(x => x.key).Select(x => x.item).ToList();

    public Guid NextGuid()
    {
        var bytes = new byte[16];
        _random.NextBytes(bytes);
        return new Guid(bytes);
    }

    /// <summary>A random weight in [0.5, 1.5), used to split a total into uneven parts.</summary>
    public decimal Weight() => 0.5m + (decimal)_random.NextDouble();
}
