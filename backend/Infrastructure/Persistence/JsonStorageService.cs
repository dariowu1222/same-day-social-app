using System.Text.Json;
using System.Text.Json.Serialization;

namespace SameDaySocialApp.Infrastructure.Persistence;

public sealed class JsonStorageService
{
    private readonly string dataPath;
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonStorageService(IWebHostEnvironment environment)
    {
        dataPath = Path.Combine(environment.ContentRootPath, "Data");
        Directory.CreateDirectory(dataPath);
    }

    public List<T> ReadCollection<T>(string name)
    {
        var path = GetPath(name);
        if (!File.Exists(path))
        {
            File.WriteAllText(path, "[]");
            return [];
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<T>>(json, jsonOptions) ?? [];
    }

    public void WriteCollection<T>(string name, List<T> data)
    {
        File.WriteAllText(GetPath(name), JsonSerializer.Serialize(data, jsonOptions));
    }

    public T InsertOne<T>(string name, T item)
    {
        var data = ReadCollection<T>(name);
        data.Add(item);
        WriteCollection(name, data);
        return item;
    }

    public T? UpdateOne<T>(string name, string id, Action<T> update) where T : class
    {
        var data = ReadCollection<T>(name);
        var item = data.FirstOrDefault(x => GetId(x) == id);
        if (item == null)
        {
            return null;
        }

        update(item);
        WriteCollection(name, data);
        return item;
    }

    public T? FindById<T>(string name, string id) where T : class
    {
        return ReadCollection<T>(name).FirstOrDefault(x => GetId(x) == id);
    }

    private string GetPath(string name)
    {
        return Path.Combine(dataPath, $"{name}.json");
    }

    private static string? GetId<T>(T item)
    {
        return item?.GetType().GetProperty("Id")?.GetValue(item)?.ToString();
    }
}
