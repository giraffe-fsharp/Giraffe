[<AutoOpenAttribute>]
module Giraffe.ValueTask
    open System
    open System.Collections
    open System.Collections.Generic

    open System.Threading
    open System.Threading.Tasks

    let TaskMap (t: Task) = ValueTask<unit>(t :?> Task<unit>) 
       
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
    let inline tbind (f: 'T -> Task<'U>) (m: Task<'T>) =
        if m.IsCompleted then f m.Result
        else
            let tcs =  new TaskCompletionSource<_>() // (Runtime.CompilerServices.AsyncTaskMethodBuilder<_>.Create())
            let t = tcs.Task
            let awaiter = m.GetAwaiter()
            awaiter.OnCompleted(fun _ -> tcs.SetResult(f m.Result))
            t.Unwrap()
            //m.ContinueWith((fun (x: Task<_>) -> f x.Result)).Unwrap()
    let inline taskBind (f:unit -> Task<'U>) (m:Task<unit>) =
        if m.IsCompleted then f ()
        else
            let tcs =  new TaskCompletionSource<_>() // (Runtime.CompilerServices.AsyncTaskMethodBuilder<_>.Create())
            let t = tcs.Task
            let awaiter = m.GetAwaiter()
            awaiter.OnCompleted(fun _ -> tcs.SetResult(f ()))
            t.Unwrap()

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

        member this.Bind(m, f) = tbind f m // bindWithOptions cancellationToken contOptions scheduler f m

        member this.Bind(m , f) = taskBind f m

        member this.Combine(comp1, comp2) = tbind comp2 comp1
//            this.Bind(comp1, comp2)
        member this.Combine(comp1, comp2) = taskBind comp2 comp1
            // this.Bind(comp1, comp2)
            
        member this.While(guard, m:unit -> Task<_>) =
            let rec whileRec(guard, m:unit -> Task<_>) = 
                if not(guard()) then this.Zero() else
                    this.Bind(m(), fun () -> whileRec(guard, m))
            whileRec(guard, m)
        
        member this.TryWith(m:unit->Task<_>,exFn) =
            try this.ReturnFrom  (m())
            with ex -> exFn ex

        member this.TryFinally(m:unit->Task<_>, compensation) =
            try this.ReturnFrom (m())
            finally compensation()

        member this.Using(res: #IDisposable, body: #IDisposable -> Task<_>) =
            this.TryFinally((fun () -> body res), fun () -> match res with null -> () | disp -> disp.Dispose())

        member this.For(sequence: seq<_>, body: 'T->Task<'U>) =
            this.Using(sequence.GetEnumerator(),
                        fun enum -> this.While(enum.MoveNext, fun () -> body enum.Current))

        member this.Delay (f: unit -> ValueTask<'T>) = f

        member this.Run (f: unit -> ValueTask<'T>) = f()

    let task = TaskBuilder(scheduler = TaskScheduler.Current)