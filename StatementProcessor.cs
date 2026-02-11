using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;

public class StatementProcessor
{
    private readonly ILogger<StatementProcessor> _logger;
    private readonly StatementAnalyzer _analyzer;

    public StatementProcessor(StatementAnalyzer analyzer, ILogger<StatementProcessor> logger)
    {
        _analyzer = analyzer;
        _logger = logger;
    }

    public async Task Run(string path)
    {
        if (!Directory.Exists(path))
        {
            _logger.LogError("Statements directory not found: {Dir}", path);
            return;
        }

        _logger.LogInformation("Scanning CSV files in: {Dir}", path);

        //var csvFiles = Directory.GetFiles(path, "*.csv");
        var csvFiles = Directory.GetFiles(path, "*.pdf");

        if (csvFiles.Length == 0)
        {
            _logger.LogInformation("No CSV files found.");
            return;
        }

        Dictionary<Item, string> items = new Dictionary<Item, string>();

        foreach (var csvPath in csvFiles)
        {
            try
            {
                Item item = ItemizeFile(csvPath);
                items.Add(item, csvPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR processing {File}", csvPath);
            }
        }
        _logger.LogInformation(items.Count.ToString());

        IOrderedEnumerable<KeyValuePair<Item, string>>? chequingItems = items
            .Where(kvp => kvp.Key.Type == ItemType.CHEQUING)
            .OrderBy(kvp => kvp.Key.Year)
            .ThenBy(kvp => kvp.Key.Month);
        _analyzer.Analyze(chequingItems);

        IOrderedEnumerable<KeyValuePair<Item, string>>? creditItems = items
            .Where(kvp => kvp.Key.Type == ItemType.CREDIT)
            .OrderBy(kvp => kvp.Key.Year)
            .ThenBy(kvp => kvp.Key.Month);
        //_analyzer.Analyze(creditItems);
    }

    public async Task ProcessStatement(string pathFile)
    {
        Item item = ItemizeFile(pathFile);
        _logger.LogInformation(item.ToString());
    }

    public Item ItemizeFile(string pathFile)
    {
        Item item = new Item();
        string directory = Path.GetDirectoryName(pathFile)!;
        string fileName = Path.GetFileNameWithoutExtension(pathFile);

        // Detect statement type
        bool isChequing = fileName.Contains("ALL-INCLUSIVE", StringComparison.OrdinalIgnoreCase);
        bool isCredit = fileName.Contains("CASH_BACK_VISA", StringComparison.OrdinalIgnoreCase);

        if (!isChequing && !isCredit)
        {
            _logger.LogCritical("Skipping unknown statement type: {File}", fileName);
            return item;
        }

        // --- Extract date part properly ---
        string datePart;

        if (isChequing)
        {
            // Chequing: last underscore before the date range
            fileName = fileName.Replace("TD_ALL", "");
            var parts = fileName.Split('-');
            datePart = parts[1].Replace("-", "_"); // "Nov_30_2022"
        }
        else
        {
            // Credit card: date starts after the last double underscore or last underscore in prefix
            // Example: 
            datePart = fileName.Replace("", "").Replace("-", "_");
        }

        // Parse the date
        DateTime parsedDate;
        var allowedFormats = new[] { "MMM_dd_yyyy", "MMM_d_yyyy" };

        if (!DateTime.TryParseExact(datePart, allowedFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
        {
            _logger.LogCritical("Unable to parse date from: {File}", fileName);
            return item;
        }

        // Build new file name: YYYYMM-chequing/credit.csv
        string prefix = isChequing ? "debit" : "credit";
        //string newName = $"{parsedDate:yyyyMM}-{prefix}.csv";
        string newName = $"{parsedDate:yyyyMM}-{prefix}.pdf";
        string newPath = Path.Combine($"~/statements/orderpdf/", newName);

        // Optionally rename the file
        File.Move(pathFile, newPath, overwrite: true);

        _logger.LogInformation("Renamed: {Old} → {New}", fileName, newName);

        // Populate Item
        item.Type = isChequing ? ItemType.CHEQUING : ItemType.CREDIT;
        item.Month = parsedDate.Month;
        item.Year = parsedDate.Year;

        return item;
    }
}
