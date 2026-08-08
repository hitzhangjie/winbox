using WinBox.Host;
using WinBox.Search;

var registry = new PluginRegistry();
registry.Register(new SearchPlugin());
await registry.StartAllAsync();

var search = registry.GetRequired<WinBox.Abstractions.ISearchService>();
await search.IndexPathsAsync(
[
    @"C:\Users\demo\Documents\report.docx",
    @"C:\Users\demo\Downloads\winbox-notes.md",
    @"D:\Github\winbox\README.md",
]);

Console.WriteLine("WinBox host started. Type a query (empty to quit).");

while (true)
{
    Console.Write("> ");
    var query = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(query))
    {
        break;
    }

    var hits = await search.SearchAsync(query);
    if (hits.Count == 0)
    {
        Console.WriteLine("  (no results)");
        continue;
    }

    foreach (var hit in hits)
    {
        Console.WriteLine($"  {hit.Score,6:0.0}  {hit.Path}");
    }
}

await registry.StopAllAsync();
