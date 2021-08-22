module Bot600.FSharp.Ban

open Argu
open DisCatSharp.CommandsNext.Converters
open DisCatSharp.Entities
open FSharpPlus
open Bot600.FSharp.Utils
open FSharpPlus.Data

type Anonymous =
    | Yes = 0
    | No = 1

[<Struct; CliPrefix(CliPrefix.Dash); DisableHelpFlags; NoComparison>]
type Arguments =
    | [<Unique>] Anon of anonymous: Anonymous option
    | [<Unique>] Purge of days: uint
    | [<MainCommand; Last; ExactlyOnce>] ``User and Reason`` of ``user and reason``: string list
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Anon _ -> "If set do not include your username in the appeal message."
            | Purge _ -> "If specified deletes messages from the last N days sent by the banned user."
            | ``User and Reason`` _ ->
                "The user to ban, either by a mention/ping, their username and discriminator, or user id, and the reason provided for the ban."

let Parser =
    ArgumentParser.Create<Arguments>(programName = "!ban")

let private memberConverter =
    DiscordMemberConverter() :> IArgumentConverter<DiscordMember>

[<Struct; NoComparison>]
type BanSettings =
    { Members: DiscordMember []
      Reason: string
      PurgeDays: uint option
      Anonymous: Anonymous }

[<NoEquality; NoComparison>]
type BanSettingsConverter() =
    interface IArgumentConverter<BanSettings> with
        member this.ConvertAsync(value, ctx) =
            monad {
                let results =
                    Parser.ParseCommandLine(value.Split(' '))

                monad {
                    let! userAndReason = results.TryGetResult <@ ``User and Reason`` @>

                    let (|Member|_|) s =
                        memberConverter.ConvertAsync(s, ctx).Result
                        |> Option.ofOptional

                    let members, reason =
                        let rec inner parsed rest =
                            match rest with
                            | Member m :: tail -> inner (m :: parsed) tail
                            | _ -> parsed, rest

                        inner [] userAndReason
                        |> fun (members, reasonParts) ->
                            members |> Array.ofList |> Array.rev, reasonParts |> String.concat " "

                    let purgeDays = results.TryGetResult <@ Purge @>

                    let anonymous =
                        results.TryGetResult <@ Anon @>
                        |> function
                            | Some None -> Anonymous.Yes
                            | Some (Some anon) -> anon
                            | None -> Anonymous.No

                    { Members = members
                      Reason = reason
                      PurgeDays = purgeDays
                      Anonymous = anonymous }
                }
                |> Option.toOptional
            }
