using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Options;

namespace SianaBot.Memes;

/// <summary>Banque de memes réservée au rôle <see cref="MemeOptions.RoleName"/> (MEC PUISSANT).</summary>
[Group("meme", "Banque de memes des mecs puissants")]
[CommandContextType(InteractionContextType.Guild)]
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

        var file = new FileAttachment(store.PathOf(meme), meme.FileName);
        var caption = $"🎲 Meme ajouté par <@{meme.AuthorId}>";

        // Depuis le salon des memes, on répond directement ; ailleurs, on y poste et on confirme à l'auteur.
        if (Context.Channel.Id == channel.Id)
        {
            await RespondWithFileAsync(file, caption, allowedMentions: AllowedMentions.None);
            return;
        }

        await DeferAsync(ephemeral: true);
        await channel.SendFileAsync(file, caption, allowedMentions: AllowedMentions.None);
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
