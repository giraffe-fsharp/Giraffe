[<AutoOpenAttribute>]
module Giraffe.ValueTask
    open System
    open System.Collections
    open System.Collections.Generic

    open System.Threading
    open System.Threading.Tasks

    let TaskMap (t: Task) = t :?> Task<unit>      
    let toAsync (t: Task<'T>): Async<'T> =
        let abegin (cb: AsyncCallback, state: obj) : IAsyncResult = 
            match cb with
            | null -> upcast t
            | cb -> 
                t.ContinueWith(fun (_ : Task<_>) -> cb.Invoke t) |> ignore
                upcast t
        let aend (r: IAsyncResult) = 
            (r :?> Task<'T>).Result
        Async.FromBeginEnd(abegin, aend)

    /// Transforms a Task's first value by using a specified mapping function.
    let inline mapWithOptions (token: CancellationToken) (continuationOptions: TaskContinuationOptions) (scheduler: TaskScheduler) f (m: Task<_>) =
        m.ContinueWith((fun (t: Task<_>) -> f t.Result), token, continuationOptions, scheduler)

    /// Transforms a Task's first value by using a specified mapping function.
    let inline map f (m: Task<_>) =
        m.ContinueWith(fun (t: Task<_>) -> f t.Result)

    let inline bindWithOptions (token: CancellationToken) (continuationOptions: TaskContinuationOptions) (scheduler: TaskScheduler) (f: 'T -> Task<'U>) (m: Task<'T>) =
        if m.IsCompleted then f m.Result
        else
            let tcs =  new TaskCompletionSource<_>() // (Runtime.CompilerServices.AsyncTaskMethodBuilder<_>.Create())
            let t = tcs.Task
            let awaiter = m.GetAwaiter()
            awaiter.OnCompleted(fun _ -> tcs.SetResult(f m.Result))
            t.Unwrap()
            //m.ContinueWith((fun (x: Task<_>) -> f x.Result), token, continuationOptions, scheduler).Unwrap()
    // let inline vtbind (f: 'T -> ValueTask<'U>) (m: ValueTask<'T>) =
    //     if m.IsCompleted then f m.Result
    //     else
    //         let tcs =  new TaskCompletionSource<'U>() // (Runtime.CompilerServices.AsyncTaskMethodBuilder<_>.Create())
    //         let t = tcs.Task
    //         let vt = ValueTask<'U>(t)
    //         let awaiter = m.GetAwaiter()
    //         awaiter.OnCompleted(fun _ -> tcs.SetResult((f m.Result).Result))
    //             // let vt2 = f m.Result
    //             // vt.GetAwaiter().OnCompleted(fun _ -> tcs.SetResult((vt2.Result) )))
    //         vt
            //t.Unwrap()
            //m.ContinueWith((fun (x: Task<_>) -> f x.Result)).Unwrap()
    let inline trbind (m: Task<'T>) (f: 'T -> ValueTask<'U>)  =
        if m.IsCompleted then f m.Result
        else
            let tcs =  new TaskCompletionSource<_>() // (Runtime.CompilerServices.AsyncTaskMethodBuilder<_>.Create())
            let t = tcs.Task
            let awaiter = m.GetAwaiter()
            awaiter.OnCompleted(fun _ -> 
                        (f m.Result).AsTask()
                        |> tcs.SetResult )
            t.Unwrap()
            |> ValueTask<_>
            //m.ContinueWith((fun (x: Task<_>) -> f x.Result)).Unwrap()
    let inline taskBind (m:Task<unit>) (f:unit -> ValueTask<_>) =
        if m.IsCompleted then f ()
        else
            let tcs =  new TaskCompletionSource<_>() // (Runtime.CompilerServices.AsyncTaskMethodBuilder<_>.Create())
            let t = tcs.Task
            let awaiter = m.GetAwaiter()
            awaiter.OnCompleted(fun _ ->
                        (f ()).AsTask() 
                        |> tcs.SetResult)
            t.Unwrap()
            |> ValueTask<_>
            
    let inline vtbind (m: ValueTask<'T>) (f: 'T -> ValueTask<'U>)  =
        if m.IsCompleted then f m.Result
        else
            let tcs =  new TaskCompletionSource<_>() // (Runtime.CompilerServices.AsyncTaskMethodBuilder<_>.Create())
            let t = tcs.Task
            let awaiter = m.GetAwaiter()
            awaiter.OnCompleted(fun _ -> 
                        (f m.Result).AsTask()
                        |> tcs.SetResult )
            t.Unwrap()
            |> ValueTask<_>
            //m.ContinueWith((fun (x: Task<_>) -> f x.Result)).Unwrap()

    // let inline vvbind (m: ValueTask<'T>) (f: 'T -> ValueTask<'U>)  =
    //     if m.IsCompleted then f m.Result
    //     else
    //         let tcs =  new TaskCompletionSource<'U>() // (Runtime.CompilerServices.AsyncTaskMethodBuilder<_>.Create())
    //         let t = tcs.Task
    //         let vt = ValueTask<'U>(t)
    //         let awaiter = m.GetAwaiter()
    //         awaiter.OnCompleted(fun _ -> tcs.SetResult(f m.Result))
    //         //t.Unwrap()
    //         vt
    //         //m.ContinueWith((fun (x: Task<_>) -> f x.Result)).Unwrap()

    let inline returnM (a:'T) = ValueTask<'T>(a)

    type TaskBuilder(?continuationOptions, ?scheduler, ?cancellationToken) =
        let contOptions = defaultArg continuationOptions TaskContinuationOptions.None
        let scheduler = defaultArg scheduler TaskScheduler.Default
        let cancellationToken = defaultArg cancellationToken CancellationToken.None

        member this.Return x = returnM x

        member this.Zero() = returnM ()

        member this.ReturnFrom (a: ValueTask<'T>) = a
        
        member this.ReturnFrom (a: Task<'T>) = ValueTask<'T>(a)

        //member this.Bind(m, f) = vtbind f m // bindWithOptions cancellationToken contOptions scheduler f m

        member this.Bind(m, f) = trbind m f // bindWithOptions cancellationToken contOptions scheduler f m

        member this.Bind(m , f) = vtbind m f
        member this.Bind(m , f) = taskBind m f

        member this.Bind(m , f) = vvbind m f

        member this.Combine(comp1, comp2) = vtbind comp1 comp2
        member this.Combine(comp1, comp2) = trbind comp1 comp2 
//            this.Bind(comp1, comp2)
        member this.Combine(comp1, comp2) = taskBind comp1 comp2
        member this.Combine(comp1, comp2) = vvbind comp1 comp2 
            // this.Bind(comp1, comp2)
            
        member this.While(guard, m:unit -> Task<_>) =
            let rec whileRec(guard, m:unit -> Task<_>) = 
                if not(guard()) then this.Zero() else
                    taskBind (m()) ( fun () -> whileRec(guard, m))
            whileRec(guard, m)
        
        member this.TryWith(m:unit->Task<_>,exFn) =
            try m() |> ValueTask<_>
            with ex -> exFn ex

        member this.TryWith(m:unit->ValueTask<_>,exFn) =
            try m()
            with ex -> exFn ex

        member this.TryFinally(m:unit->ValueTask<_>, compensation) =
            try m()
            finally compensation()

        member this.TryFinally(m:unit->Task<_>, compensation) =
            try m() |> ValueTask<_>
            finally compensation()        

        member this.Using(res: #IDisposable, body: #IDisposable -> Task<_>) =
            try fun () -> body res 
            finally match res with null -> () | disp -> disp.Dispose()

        member this.For(sequence: seq<_>, body: 'T->Task<_>) =
            this.Using(sequence.GetEnumerator(),
                        fun enum -> this.While(enum.MoveNext, fun () -> body enum.Current))

        member this.Delay (f: unit -> ValueTask<'T>) = f

        member this.Run (f: unit -> ValueTask<'T>) = f()

    let task = TaskBuilder(scheduler = TaskScheduler.Current)