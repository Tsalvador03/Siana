using Microsoft.Extensions.Options;
using SianaBot.Storage;

namespace SianaBot.Memes;

/// <summary>
/// Memes téléchargés dans <see cref="MemeOptions.StoragePath"/> : les liens des pièces jointes Discord expirent,
/// on garde donc une copie du fichier.
/// </summary>
public sealed class MemeStore
{
    private static readonly HttpClient Http = new();

    private readonly string _directory;
    private readonly JsonListStore<Meme> _store;

    public MemeStore(IOptions<MemeOptions> options)
    {
        _directory = Path.GetFullPath(options.Value.StoragePath);
        _store = new JsonListStore<Meme>(Path.Combine(_directory, "memes.json"));
    }

    public IReadOnlyList<Meme> ForGuild(ulong guildId) => _store.All().Where(m => m.GuildId == guildId).ToList();

    /// <summary>Un meme du serveur tiré au hasard, dont le fichier existe encore.</summary>
    public Meme? PickRandom(ulong guildId)
    {
        var memes = ForGuild(guildId).Where(m => File.Exists(PathOf(m))).ToList();
        return memes.Count == 0 ? null : memes[Random.Shared.Next(memes.Count)];
    }

    public string PathOf(Meme meme) => Path.Combine(_directory, meme.FileName);

    public async Task<Meme> AddAsync(string url, string originalName, ulong guildId, ulong authorId)
    {
        var fileName = Guid.NewGuid().ToString("N") + SafeExtension(originalName);
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, fileName);

        await using (var source = await Http.GetStreamAsync(url))
        await using (var target = File.Create(path))
            await source.CopyToAsync(target);

        var meme = new Meme(fileName, guildId, authorId, DateTimeOffset.UtcNow);
        await _store.MutateAsync(items =>
        {
            items.Add(meme);
            return true;
        });
        return meme;
    }

    /// <summary>Extension d'origine en minuscules, si elle est sûre pour un nom de fichier (sinon aucune).</summary>
    private static string SafeExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension.Length is > 1 and <= 6 && extension.Skip(1).All(char.IsAsciiLetterOrDigit) ? extension : "";
    }
}
