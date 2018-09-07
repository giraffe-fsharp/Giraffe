namespace Giraffe.StringBuilders

open System
open System.Text

type private StringBuilderCache =

    [<ThreadStatic>]
    [<DefaultValue>]
    static val mutable private sb : StringBuilder

    [<ThreadStatic>]
    [<DefaultValue>]
    static val mutable private inUse : bool

    static member Get (capacity : int) (maxCapacity : int) : StringBuilder =
        match StringBuilderCache.inUse with
        | true  -> new StringBuilder(capacity)
        | false ->
            StringBuilderCache.inUse <- true

            let sb = StringBuilderCache.sb

            match sb <> null && sb.Capacity >= capacity with
            | true  -> sb.Clear()
            | false ->
                let sb' = new StringBuilder(capacity)
                if capacity <= maxCapacity then
                    StringBuilderCache.sb <- sb'
                sb'

    static member Release() : unit =
        StringBuilderCache.inUse <- false

[<AutoOpen>]
module StringBuilderProvider =

    type IStringBuilderProvider =
        inherit IDisposable
        abstract member Get : unit -> StringBuilder

    type DefaultStringBuilderProvider() =
        interface IStringBuilderProvider with
            member __.Get() = new StringBuilder()
            member __.Dispose() = ()

    type ThreadStaticStringBuilderCache (defaultCapacity, maxCapacity) =
        interface IStringBuilderProvider with
            member __.Get()     = StringBuilderCache.Get defaultCapacity maxCapacity
            member __.Dispose() = StringBuilderCache.Release()