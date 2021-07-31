using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bot600.Utils;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Bot600.Commands
{
    public class HelpCommandModule : ModuleBase<SocketCommandContext>
    {
        private readonly BotMain botMain;

        public HelpCommandModule(BotMain bm)
        {
            botMain = bm;
        }

        [Command("help", RunMode = RunMode.Async)]
        [Summary("Lists available commands")]
        public async Task Help()
        {
            SocketUser author = Context.User;
            bool isModerator = await IsModerator(author);
            bool[] permission =
                await Task.WhenAll(botMain.CommandService.Commands.Select(c => HasPermission(c)
                                                                              .ContinueWith(hasPermission =>
                                                                                  hasPermission.Result
                                                                                  && isModerator
                                                                                  || !HideCommand(c))));

            string message = string.Join('\n', botMain.CommandService.Commands
                                                      .FilterZip(permission)
                                                      .Select(MakeBriefHelp));

            IDMChannel dm = await author.GetOrCreateDMChannelAsync();
            await dm.SendMessageAsync(message);
        }

        [Command("help", RunMode = RunMode.Async)]
        [Summary("Lists detailed information about a given command")]
        public async Task Help([Summary("Command name to search for")]
                               string search)
        {
            SocketUser author = Context.User;
            StringBuilder stringBuilder = new();

            IEnumerable<CommandInfo> searchResult =
                botMain.CommandService.Search(Context, search).Commands.Select(m => m.Command);
            CommandInfo[] commandInfos = searchResult as CommandInfo[] ?? searchResult.ToArray();
            bool[] permission = await Task.WhenAll(commandInfos.Select(HasPermission));

            string message =
                string.Join('\n',
                            commandInfos.FilterZip(permission)
                                        .Select(command => MakeDetailedHelp(stringBuilder, command)));
            IDMChannel dm = await author.GetOrCreateDMChannelAsync();
            await dm.SendMessageAsync(message);
        }

        private async Task<bool> HasPermission(CommandInfo command) =>
            (await command.CheckPreconditionsAsync(Context, botMain.CommandServiceProvider)).IsSuccess;

        private async Task<bool> IsModerator(IUser user) =>
            await botMain.IsUserModerator(user) == Utils.IsModerator.Yes;

        private static bool HideCommand(CommandInfo commandInfo) =>
            commandInfo.Attributes.Any(a => a is HideFromHelpAttribute)
            || commandInfo.Module.Attributes.Any(a => a is HideFromHelpAttribute);

        private static string SummaryOrDefault(string summary, string orElse = "_No summary provided_") =>
            string.IsNullOrWhiteSpace(summary) ? orElse : summary;

        private static string MakeBriefHelp(CommandInfo command)
        {
            static string FormatParameter(ParameterInfo parameterInfo) =>
                $"{parameterInfo.Type.Name}{(parameterInfo.IsMultiple ? "[]" : "")} {parameterInfo.Name}";

            return
                $"!{command.Name}({string.Join(", ", command.Parameters.Select(FormatParameter))}) - {SummaryOrDefault(command.Summary)}";
        }


        private static string MakeDetailedHelp(StringBuilder stringBuilder, CommandInfo command)
        {
            stringBuilder.Clear();

            static string FormatParameter(ParameterInfo parameter) =>
                $"{parameter.Type.Name}{(parameter.IsMultiple ? "[]" : "")} {parameter.Name} - {SummaryOrDefault(parameter.Summary)}";

            stringBuilder.AppendLine($"**{string.Join(", ", command.Aliases)}**");
            stringBuilder.AppendLine($"{SummaryOrDefault(command.Summary)}");

            if (command.Parameters.Any())
            {
                stringBuilder.AppendLine();
            }

            foreach (ParameterInfo parameter in command.Parameters)
            {
                stringBuilder.AppendLine(FormatParameter(parameter));
            }

            return stringBuilder.ToString();
        }
    }
}
