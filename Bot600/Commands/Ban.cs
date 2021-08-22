﻿using System;
using Bot600.Config;
using Bot600.FSharp;
using Bot600.Utils;
using DisCatSharp;
using DisCatSharp.CommandsNext;
using DisCatSharp.CommandsNext.Attributes;
using DisCatSharp.Entities;
using FSharpPlus;
using Microsoft.FSharp.Core;
using Task = System.Threading.Tasks.Task;

namespace Bot600.Commands
{
    // ReSharper disable once UnusedType.Global
    public class BanCommandModule : BaseCommandModule
    {
        private readonly BotMain botMain;

        public BanCommandModule(BotMain bm) => botMain = bm;

        private BanTemplate BanTemplate => botMain.Config.BanTemplate;

        private async Task BanMember(
            CommandContext context,
            DiscordMember member,
            string? reason,
            Anonymous anon = Anonymous.No)
        {
            DiscordUser banner = context.User;
            if (await botMain.IsUserModerator(banner) == IsModerator.No)
            {
                await context.RespondAsync($"Error executing !ban: {banner.Mention} is not a moderator");
                return;
            }

            DiscordDmChannel baneeDm = await member.CreateDmChannelAsync();

            string bannerStr;
            if (anon == Anonymous.Yes)
            {
                bannerStr = BanTemplate.DefaultAppeal;
            }
            else
            {
                bannerStr = banner.ToString();
                bannerStr = bannerStr != BanTemplate.DefaultAppeal
                    ? $"`{bannerStr}` or `{BanTemplate.DefaultAppeal}`"
                    : $"`{bannerStr}`";
            }

            string banMsg = BanTemplate.Template.Replace("[reason]", reason ?? "No reason provided")
                                       .Replace("[banner]", bannerStr);

            string feedback = $"{member.Mention} has been banned. The appeal message could not be sent.\n";
            try
            {
                DiscordMessage directMsg = await baneeDm.SendMessageAsync(banMsg);
                if (directMsg is not null && directMsg.Id != 0)
                {
                    feedback = $"{member.Mention} has been banned. The message sent was the following:\n{banMsg}";
                }
            }
            catch
            {
                // ignored
            }

            await Task.WhenAll(context.Message.Channel.SendMessageAsync(feedback),
                               botMain.DiscordConfig.OutputGuild.BanMemberAsync(member, reason: reason));
        }

        [Command("ban")]
        [Description("Ban a member and send them an appeal message via DMs, including your username for contact.")]
        [RequirePermissionInGuild(Permissions.BanMembers)]
        [RequireModeratorRoleInGuild]
        [RequireDmOrOutputGuild]
        public async Task Ban(CommandContext context, DiscordMember member, [RemainingText] string? reason = null) =>
            await BanMember(context, member, reason);


        [Command("ban_anon")]
        [Description("Ban a member and send them an appeal message via DMs with a default username for contact.")]
        [RequirePermissionInGuild(Permissions.BanMembers)]
        [RequireModeratorRoleInGuild]
        [RequireDmOrOutputGuild]
        public async Task BanAnon(
            CommandContext context,
            DiscordMember member,
            [RemainingText] string? reason = null) =>
            await BanMember(context, member, reason, Anonymous.Yes);

        [Command("ban_test")]
        public async Task BanTest(CommandContext context, [RemainingText] Ban.BanSettings settings)
        {
            await context.RespondAsync($"{OptionModule.DefaultValue(0u, settings.PurgeDays)}");
        }
    }
}
