using System.Net.Http;
using System.Threading.Tasks;
using Bot600.Utils;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace Bot600.Commands
{
    [HideFromHelp]
    public class HyenaCommandModule : ModuleBase<SocketCommandContext>
    {
        private const string Endpoint = "https://api.yeen.land";
        private static readonly HttpClient HttpClient = new();
        private readonly BotMain botMain;

        public HyenaCommandModule(BotMain bm)
        {
            botMain = bm;
        }

        [Command("sus", RunMode = RunMode.Async)]
        [Summary("smh my head")]
        public async Task Sus()
        {
            SocketUser? author = Context.Message.Author;
            IDMChannel? dm = await author.GetOrCreateDMChannelAsync();
            await dm.SendMessageAsync("https://tenor.com/view/keanu-reeves-knife-gif-19576998");
        }

        [Command("hyena", RunMode = RunMode.Async)]
        [Summary("Fetch a random hyena image")]
        [Alias("yeen")]
        public async Task Hyena()
        {
            string reply = await GetReply(Endpoint);

            await ReplyAsync(reply);
        }

        [Command("hyena", RunMode = RunMode.Async)]
        [Summary("Fetch a hyena image by id")]
        [Alias("yeen")]
        public async Task Hyena(ulong id)
        {
            var requestUriString = $"{Endpoint}/id/{id}";
            string reply = await GetReply(requestUriString);

            await ReplyAsync(reply);
        }

        private static async Task<HyenaUrl?> QueryApi(string requestUriString)
        {
            string response = await HttpClient.GetStringAsync(requestUriString);
            var url = JsonConvert.DeserializeObject<HyenaUrl>(response);

            return url;
        }

        private static async Task<string> GetReply(string requestUriString)
        {
            string reply;
            if (await QueryApi(requestUriString) is { } url)
            {
                reply = url.Url;
            }
            else
            {
                reply = ":question:";
            }

            return reply;
        }

        private record HyenaUrl(string Url);
    }
}
