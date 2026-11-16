using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Options;

namespace SianaBot.Memes;

/// <summary>
/// Banque de memes réservée au rôle <see cref="MemeOptions.RoleName"/> (MEC PUISSANT). Discord ne permet pas à un bot
/// de masquer une commande selon un rôle : elle est masquée à tous sauf aux administrateurs, et le serveur l'ouvre
/// au rôle dans Paramètres du serveur → Intégrations. <see cref="RequireMemeRoleAttribute"/> vérifie quand même le rôle.
/// </summary>
[Group("meme", "Banque de memes des mecs puissants")]
[CommandContextType(InteractionContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.Administrator)]
[RequireMemeRole]
public sealed class MemeModule(MemeStore store, IOptions<MemeOptions> options, ILogger<MemeModule> logger)
    : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("add", "Ajoute un meme à la banque")]
    public async Task AddAsync([Summary("fichier", "Image, GIF ou vidéo")] IAttachment fichier)
    {
        var settings = options.Value;
        if (fichier.ContentType is not { } type || !(type.StartsWith("image/") || type.StartsWith("video/")))
        {
            await RespondAsync("❌ Le meme doit être une image, un GIF ou une vidéo.", ephemeral: true);
            return;
        }
        if (fichier.Size > settings.MaxFileSize)
        {
            await RespondAsync($"❌ Fichier trop lourd (max {settings.MaxFileSize / (1024 * 1024)} Mo).", ephemeral: true);
            return;
        }

        await DeferAsync(ephemeral: true);

        Meme meme;
        try
        {
            meme = await store.AddAsync(fichier.Url, fichier.Filename, Context.Guild.Id, Context.User.Id);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Meme : téléchargement de {Url} impossible", fichier.Url);
            await FollowupAsync("❌ Impossible de récupérer le fichier, réessaie.", ephemeral: true);
            return;
        }

        logger.LogInformation("Meme {File} ajouté par {User} ({UserId})", meme.FileName, Context.User.Username, Context.User.Id);
        await FollowupAsync($"✅ Meme ajouté ! La banque en compte {store.ForGuild(Context.Guild.Id).Count}.", ephemeral: true);
    }

    [SlashCommand("random", "Poste un meme au hasard dans le salon des mecs puissants")]
    public async Task RandomAsync()
    {
        var settings = options.Value;
        var channel = settings.Channel(Context.Guild);
        if (channel is null)
        {
            await RespondAsync($"❌ Salon #{settings.ChannelName} introuvable.", ephemeral: true);
            return;
        }

        var meme = store.PickRandom(Context.Guild.Id);
        if (meme is null)
        {
            await RespondAsync("❌ Aucun meme dans la banque. Ajoutes-en un avec `/meme add`.", ephemeral: true);
            return;
        }

        // Anonyme : le bot poste lui-même le meme (une réponse à la commande afficherait « X a utilisé /meme random »),
        // sans dire qui l'a ajouté ni qui l'a demandé. En spoiler : il faut cliquer pour le voir.
        await DeferAsync(ephemeral: true);
        await channel.SendFileAsync(new FileAttachment(store.PathOf(meme), meme.FileName, isSpoiler: true), "🎲");

        if (Context.Channel.Id == channel.Id)
            await DeleteOriginalResponseAsync();
        else
            await FollowupAsync($"✅ Meme posté dans {channel.Mention}.", ephemeral: true);
    }
}

/// <summary>Réserve les commandes aux membres ayant le rôle configuré dans <see cref="MemeOptions"/>.</summary>
internal sealed class RequireMemeRoleAttribute : PreconditionAttribute
{
    public override Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptions<MemeOptions>>().Value;
        return Task.FromResult(context.User is SocketGuildUser user && options.HasRole(user)
            ? PreconditionResult.FromSuccess()
            : PreconditionResult.FromError($"Il te faut le rôle **{options.RoleName}**."));
    }
}
