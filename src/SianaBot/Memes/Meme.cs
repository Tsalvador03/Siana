using Discord.WebSocket;

namespace SianaBot.Memes;

/// <summary>Un meme stocké : le fichier <see cref="FileName"/> vit dans le dossier <see cref="MemeOptions.StoragePath"/>.</summary>
public sealed record Meme(string FileName, ulong GuildId, ulong AuthorId, DateTimeOffset AddedAt);

public sealed class MemeOptions
{
    public const string Section = "Memes";

    /// <summary>Dossier des memes : les fichiers et leur index <c>memes.json</c>.</summary>
    public string StoragePath { get; set; } = "data/memes";

    /// <summary>Rôle autorisé à utiliser les commandes, si <see cref="RoleId"/> n'est pas renseigné.</summary>
    public string RoleName { get; set; } = "MEC PUISSANT";

    public ulong? RoleId { get; set; }

    /// <summary>Salon où sont postés les memes, si <see cref="ChannelId"/> n'est pas renseigné.</summary>
    public string ChannelName { get; set; } = "mecs-puissants";

    public ulong? ChannelId { get; set; }

    /// <summary>Taille maximale d'un meme, en octets (10 Mo : limite d'envoi d'un bot sans boost).</summary>
    public long MaxFileSize { get; set; } = 10 * 1024 * 1024;

    public bool HasRole(SocketGuildUser user) => RoleId is { } roleId
        ? user.Roles.Any(r => r.Id == roleId)
        : user.Roles.Any(r => string.Equals(r.Name, RoleName, StringComparison.OrdinalIgnoreCase));

    public SocketTextChannel? Channel(SocketGuild guild) => ChannelId is { } channelId
        ? guild.GetTextChannel(channelId)
        : guild.TextChannels.FirstOrDefault(c => string.Equals(c.Name, ChannelName, StringComparison.OrdinalIgnoreCase));
}
