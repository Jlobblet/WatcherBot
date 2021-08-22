module Bot600.FSharp.Utils

open DisCatSharp.Entities

[<RequireQualifiedAccess>]
module Option =
    let ofOptional (optional: Optional<_>) =
        match optional.HasValue with
        | true -> Some optional.Value
        | false -> None

    let toOptional =
        function
        | Some x -> Optional.FromValue x
        | None -> Optional.FromNoValue()
