using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DisCatSharp;
using DisCatSharp.Entities;
using DisCatSharp.EventArgs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WatcherBot.Models;

namespace WatcherBot.Utils
{
    public class MessageDeleters : IDisposable
    {
        private readonly BotMain botMain;
        private readonly WatcherDatabaseContext databaseContext;
        private readonly ILogger logger;

        public MessageDeleters(BotMain botMain)
        {
            this.botMain    = botMain;
            logger          = botMain.Client.Logger;
            databaseContext = new WatcherDatabaseContext();
        }

        public void Dispose()
        {
            databaseContext.Dispose();
            GC.SuppressFinalize(this);
        }

        private bool GeneralCondition(DiscordMessage msg) => !(msg.Author.IsBot || msg.Channel is DiscordDmChannel);

        public Task ContainsDisallowedInvite(DiscordClient sender, MessageCreateEventArgs args)
        {
            Delete DeletionCondition()
            {
                if (botMain.Config.InvitesAllowedOnChannels.Contains(args.Message.Channel.Id))
                {
                    return Delete.No;
                }

                if (args.Guild is null || botMain.Config.InvitesAllowedOnServers.Contains(args.Guild.Id))
                {
                    return Delete.No;
                }

                return TextContainsInvite(args.Message.Content)
                           ? Delete.Yes
                           : Delete.No;
            }
            
            if (!GeneralCondition(args.Message) || DeletionCondition() != Delete.Yes)
            {
                return Task.CompletedTask;
            }


            logger.LogInformation("Deleting message sent by {User} for reason {Reason}",
                                  args.Message.Author.UsernameWithDiscriminator, DeletionReason.DisallowedInvite);
            return DeleteMsg(args.Message);
        }

        public static bool TextContainsInvite(string messageContent)
        {
            string[] invites = { "discord.gg/", "discord.com/invite", "discordapp.com/invite" };
            return invites.Any(i => messageContent.Contains(i, StringComparison.OrdinalIgnoreCase));
        }

        public Task MessageWithinAttachmentLimits(DiscordClient sender, MessageCreateEventArgs args)
        {
            Delete DeletionCondition()
            {
                if (!botMain.Config.AttachmentLimits.ContainsKey(args.Channel.Id))
                {
                    return Delete.No;
                }

                bool insecureLink = args.Message.Content.Contains("http://", StringComparison.OrdinalIgnoreCase);
                int numberWellSizedAttachments = args.Message.Attachments.Count(a => a.Width is null
                                                                                    || a.Height is null
                                                                                    || a.Width >= 16
                                                                                    && a.Height >= 16);
                int numberLinks = args.Message.Content.CountSubstrings("https://");
                int sum         = numberWellSizedAttachments + numberLinks;

                bool attachmentCountWithinLimits = botMain.Config.AttachmentLimits[args.Channel.Id].Contains(sum);
                bool allAttachmentsWellSized     = numberWellSizedAttachments == args.Message.Attachments.Count;

                return attachmentCountWithinLimits && allAttachmentsWellSized && !insecureLink ? Delete.No : Delete.Yes;
            }

            if (!GeneralCondition(args.Message) || DeletionCondition() != Delete.Yes)
            {
                return Task.CompletedTask;
            }

            logger.LogInformation("Deleting message sent by {User} for reason {Reason}",
                                  args.Message.Author.UsernameWithDiscriminator,
                                  DeletionReason.ViolateAttachmentLimits);
            return DeleteMsg(args.Message);
        }

        public Task ProhibitFormattingFromUsers(DiscordClient sender, MessageCreateEventArgs args)
        {
            if (!GeneralCondition(args.Message)
                || !botMain.Config.ProhibitFormattingFromUsers.Contains(args.Author.Id)
                || !botMain.Config.FormattingCharacters.Overlaps(args.Message.Content))
            {
                return Task.CompletedTask;
            }

            logger.LogInformation("Deleting message sent by {User} for reason {Reason}",
                                  args.Message.Author.UsernameWithDiscriminator, DeletionReason.ProhibitedFormatting);
            return DeleteMsg(args.Message);
        }

        public Task DeleteCringeMessages(DiscordClient sender, MessageCreateEventArgs args)
        {
            Task _ = Task.Run(async () =>
            {
                IsCringe UserIsCringe()
                {
                    User user = User.GetOrCreateUser(databaseContext, args.Message.Author.Id);
                    IsCringe channelIsCringe =
                        botMain.Config.CringeChannels.Contains(args.Message.Channel.Id)
                            ? IsCringe.Yes
                            : IsCringe.No;
                    if (args.Message.Channel is not DiscordDmChannel)
                    {
                        user.NewMessage(channelIsCringe);
                        try
                        {
                            databaseContext.SaveChanges();
                        }
                        catch (DbUpdateException exc)
                        {
                            Console
                                .WriteLine($"{nameof(databaseContext.SaveChanges)} threw an exception:");
                            Console.WriteLine($"{exc.InnerException?.Message ?? exc.Message}");
                            Console.WriteLine($"{exc.InnerException?.StackTrace ?? exc.StackTrace}");
                        }
                    }

                    // it's cringe to bool to cringe
                    return (channelIsCringe.ToBool() && user.IsCringe.ToBool()).ToCringe();
                }

                if (GeneralCondition(args.Message) && UserIsCringe() == IsCringe.Yes)
                {
                    logger.LogInformation("Deleting message sent by {User} for reason {Reason}",
                                          args.Message.Author.UsernameWithDiscriminator,
                                          DeletionReason.CringeMessage);
                    await DeleteMsg(args.Message);
                }
            });
            return Task.CompletedTask;
        }

        public Task DeletePotentialSpam(DiscordClient sender, MessageCreateEventArgs args)
        {
            Task _ = Task.Run(async () =>
            {
                if (!args.Channel.IsPrivate && botMain.DiscordConfig.OutputGuild.Id != args.Guild.Id)
                {
                    return;
                }

                if (args.Author.IsBot)
                {
                    return;
                }

                if (await botMain.IsUserModerator(args.Author) == IsModerator.Yes)
                {
                    return;
                }

                if (await botMain.IsUserExemptFromSpamFilter(args.Author)
                    == IsExemptFromSpamFilter.Yes)
                {
                    return;
                }

                if (botMain.Config.InvitesAllowedOnChannels.Contains(args.Channel.Id)
                    && TextContainsInvite(args.Message.Content))
                {
                    return;
                }

                //if (args.Channel.Id != botMain.Config.SpamReportChannel) { return; }

                string messageContent       = args.Message.Content;
                string messageContentToTest = messageContent.ToLowerInvariant();
                foreach (var safeSubstr in botMain.Config.KnownSafeSubstrings)
                {
                    messageContentToTest = messageContentToTest.Replace(safeSubstr, "");
                }

                if (messageContentToTest.CountSubstrings("https://")
                    + messageContentToTest.CountSubstrings("http://")
                    <= 0)
                {
                    return;
                }

                char[] whitespace = new[] { '\n', '\t', '\r', ' ' }
                                    .Concat(messageContentToTest.Where(char.IsWhiteSpace)).Distinct()
                                    .ToArray();
                string[]  messageContentSplit = messageContentToTest.Split(whitespace);
                Stopwatch sw                  = new();
                sw.Start();
                (string InText, string InFilter, float Weight)[] hits
                    = messageContentSplit.Where(s1
                                                    => !string.IsNullOrWhiteSpace(s1))
                                         .Select(s1 => botMain.Config.SpamSubstrings.FirstOrDefault(s2
                                                               => LevenshteinDistance
                                                                      .Calculate(s1,
                                                                          s2
                                                                              .Substring)
                                                                  <= s2.MaxDistance) is
                                                           { } h
                                                           ? (s1, h.Substring, h.Weight)
                                                           : ("", "", 0.0f))
                                         .Cast<(string InText, string InFilter, float Weight)>()
                                         .Where(t => t.Weight > 0.0f)
                                         .ToArray();
                /*botMain.Config.SpamSubstrings
                    .Select(s => LevenshteinDistance.Calculate(messageContentToTest, s.Substring, s.MaxDistance))
                    .Where(r => r is not null)
                    .Cast<(int Index, int Length, int Distance)>()
                    .ToArray();*/
                sw.Stop();

                if (hits.Sum(h => h.Weight) >= 2)
                {
                    logger
                        .LogInformation("Deleting message sent by and muting {User} for reason {Reason}",
                                        args.Message.Author.UsernameWithDiscriminator,
                                        DeletionReason.PotentialSpam);
                    DiscordChannel    reportChannel = botMain.SpamReportChannel;
                    DiscordMember     member        = await botMain.GetMemberFromUser(args.Author);
                    DiscordDmChannel? dmChannel     = await member.CreateDmChannelAsync();
                    DiscordMessage? dm = await dmChannel.SendMessageAsync(
                                                                          $"You have been automatically muted on the Undertow Games server for sending the following message in {args.Channel.Mention}:\n\n"
                                                                          + $"```\n{messageContent.Replace("`", "")}\n```\n\n"
                                                                          + "This is a spam prevention measure. If this was a false positive, please contact a moderator or administrator.");
                    if (!args.Channel.IsPrivate)
                    {
#pragma warning disable 4014
                        reportChannel.SendMessageAsync(
                                                       $"{args.Author.Mention} has been muted for sending the following message in {args.Channel.Mention}:\n\n"
                                                       + $"```\n{messageContent.Replace("`", "")}\n```\n\n"
                                                       + $"*Hits*: {string.Join(", ", hits.Select(h => $"{h.InText} (matched \"{h.InFilter}\")"))}\n\n"
                                                       + "If this was a false positive, you may revert this by removing the `Muted` role and granting the `Spam filter exemption` role.\n\n"
                                                       + (dm != null
                                                              ? "The user has been informed via DM."
                                                              : "The user **could not** be informed via DM."));
                    }

                    botMain.MuteUser(args.Author, $"Potential spam {DateTime.UtcNow}");
                    args.Message.DeleteAsync();
#pragma warning restore 4014
                }
            });

            return Task.CompletedTask;
        }

        public Task ReplyInNoConversationChannel(DiscordClient sender, MessageCreateEventArgs args)
        {
            if (botMain.Config.AttachmentLimits.ContainsKey(args.Channel.Id)
                && args.Message.ReferencedMessage is not null)
            {
                logger.LogInformation("Deleting message sent by {User} for reason {Reason}",
                                      args.Message.Author.UsernameWithDiscriminator,
                                      DeletionReason.ReplyInNoConversationChannel);
                return DeleteMsg(args.Message);
            }

            return Task.CompletedTask;
        }

        private Task DeleteMsg(DiscordMessage msg)
        {
            async Task Delete(Task<IsModerator> t)
            {
                if (t.Result == IsModerator.No)
                {
                    await msg.DeleteAsync();
                }
            }

            return botMain.IsUserModerator(msg.Author).ContinueWith(Delete);
        }

        private enum DeletionReason
        {
            DisallowedInvite,
            ViolateAttachmentLimits,
            ProhibitedFormatting,
            CringeMessage,
            PotentialSpam,
            ReplyInNoConversationChannel,
        }

        private enum Delete
        {
            No,
            Yes,
        }
    }
}
