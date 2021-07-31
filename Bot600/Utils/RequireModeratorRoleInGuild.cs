using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Discord.Commands;

namespace Bot600.Utils
{
    public class RequireModeratorRoleInGuild : PreconditionAttribute
    {
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

            return await botMain.IsUserModerator(context.User) == IsModerator.Yes
                       ? PreconditionResult.FromSuccess()
                       : PreconditionResult.FromError(ErrorMessage);
        }
    }
}
