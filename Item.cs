public enum ItemType
{
    CHEQUING,
    CREDIT
}

public class Item
{
    public int Month { get; set; }
    public int Year { get; set; }
    public ItemType Type { get; set; }

    public override string ToString()
    {
        return $"{Year}:{Month}:{Type}";
    }
}