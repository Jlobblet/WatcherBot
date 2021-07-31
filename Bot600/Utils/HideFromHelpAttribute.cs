using System;
using Discord.Commands;

namespace Bot600.Utils
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class HideFromHelpAttribute : CommandAttribute
    {
    }
}
