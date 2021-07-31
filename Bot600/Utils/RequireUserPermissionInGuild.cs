using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Rest;

namespace Bot600.Utils
{
    public class RequireUserPermissionInGuild : PreconditionAttribute
    {
        private readonly GuildPermission permission;

        public RequireUserPermissionInGuild(GuildPermission permission)
        {
            this.permission = permission;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public override async Task<PreconditionResult> CheckPermissionsAsync(
            ICommandContext context, CommandInfo command,
            IServiceProvider services)
        {
            var botMain = (BotMain?) services.GetService(typeof(BotMain));

            if (botMain is null)
            {
                return PreconditionResult.FromError($"Could not retrieve {nameof(BotMain)}");
            }

            if (botMain.DiscordConfig is null)
            {
                return PreconditionResult.FromError($"{nameof(botMain.DiscordConfig)} is null");
            }

            RestGuildUser? user = context.User is RestGuildUser rgu
                                      ? rgu
                                      : await botMain.DiscordConfig.OutputGuild.GetUserAsync(context.User.Id);

            if (user is null)
            {
                return PreconditionResult.FromError($"Could not find user {context.User} in guild");
            }

            return user.GuildPermissions.Has(permission)
                       ? PreconditionResult.FromSuccess()
                       : PreconditionResult.FromError(ErrorMessage);
        }
    }
}
