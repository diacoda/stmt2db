using Microsoft.Extensions.Logging;

public class StatementAnalyzer
{
    ILogger<StatementAnalyzer> _logger;

    public StatementAnalyzer(ILogger<StatementAnalyzer> logger)
    {
        _logger = logger;
    }

    public void Analyze(IOrderedEnumerable<KeyValuePair<Item, string>>? items)
    {
        if (items == null)
        {
            return;
        }

        foreach (var kvp in items)
        {
            Item item = kvp.Key;   // the Item object
            string path = kvp.Value; // the associated string (e.g., file path)

            if (item.Type == ItemType.CHEQUING)
                AnalyzeChequingFile(item, path);
            else
                AnalyzeCreditFile(item, path);
        }
    }

    private void AnalyzeChequingFile(Item item, string path)
    {
        var lines = File.ReadAllLines(path).Skip(1); // skip header
        if (lines.Count() == 0)
        {
            _logger.LogCritical($"zero line for {path}");
            return;
        }
        var monthlyData = new Dictionary<string, MonthlySummary>();

        decimal lastKnownBalance = 0;

        foreach (var line in lines)
        {
            var parts = line.Split(',');
            if (parts.Length < 5) continue;

            string description = parts[0].Trim();
            string withdrawals = parts[1].Trim();
            string deposits = parts[2].Trim();
            string dateStr = parts[3].Trim();
            string balanceStr = parts[4].Trim();

            if (!DateTime.TryParse(dateStr, out var date)) continue;

            decimal withdrawalAmt = decimal.TryParse(withdrawals, out var w) ? w : 0m;
            decimal depositAmt = decimal.TryParse(deposits, out var d) ? d : 0m;
            decimal balance = decimal.TryParse(balanceStr, out var b) ? b : lastKnownBalance;

            string key = $"{date.Year}-{date.Month:D2}";

            if (!monthlyData.ContainsKey(key))
            {
                monthlyData[key] = new MonthlySummary
                {
                    Year = date.Year,
                    Month = date.Month
                };
            }

            monthlyData[key].TotalDeposits += depositAmt;
            monthlyData[key].TotalWithdrawals += withdrawalAmt;
            monthlyData[key].EndingBalance = balance;

            lastKnownBalance = balance;
        }

        // Output monthly summary
        foreach (var m in monthlyData.Values.OrderBy(x => x.Year).ThenBy(x => x.Month))
        {
            _logger.LogInformation($"Chequing {item.Year}-{item.Month:D2} | Deposits: {m.TotalDeposits:C} | Withdrawals: {m.TotalWithdrawals:C} | Ending Balance: {m.EndingBalance:C}");
        }
    }

    private void AnalyzeCreditFile(Item item, string path)
    {
        var lines = File.ReadAllLines(path).Skip(1); // skip header
        var monthlyData = new Dictionary<string, MonthlySummary>();

        decimal lastKnownBalance = 0;

        foreach (var line in lines)
        {
            var parts = line.Split(',');
            if (parts.Length < 5) continue;

            string dateStr = parts[0].Trim(); // TxnDate
            string amountStr = parts[3].Trim();
            string balanceStr = parts[4].Trim();

            if (!DateTime.TryParse(dateStr, out var date)) continue;

            decimal amount = decimal.TryParse(amountStr, out var a) ? a : 0m;
            decimal balance = decimal.TryParse(balanceStr, out var b) ? b : lastKnownBalance;

            string key = $"{date.Year}-{date.Month:D2}";

            if (!monthlyData.ContainsKey(key))
            {
                monthlyData[key] = new MonthlySummary
                {
                    Year = date.Year,
                    Month = date.Month
                };
            }

            if (amount > 0)
                monthlyData[key].TotalWithdrawals += amount; // credit card charges
            else
                monthlyData[key].TotalDeposits += -amount; // payments/credits

            monthlyData[key].EndingBalance = balance;
            lastKnownBalance = balance;
        }

        foreach (var m in monthlyData.Values.OrderBy(x => x.Year).ThenBy(x => x.Month))
        {
            _logger.LogInformation($"Credit {item.Year}-{item.Month:D2} | Charges: {m.TotalWithdrawals:C} | Payments: {m.TotalDeposits:C} | Ending Balance: {m.EndingBalance:C}");
        }
    }
}