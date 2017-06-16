[<AutoOpenAttribute>]
module Giraffe.Task
open System
open System.Collections
open System.Collections.Generic

open System.Threading
open System.Threading.Tasks

let inline wait (task:Task<_>) = task.Wait()

let inline awaitTask (t:Task) = 
   let tcs = TaskCompletionSource()
   t.ContinueWith(fun t -> 
      match t.IsFaulted with
      | false -> if t.IsCanceled then tcs.SetCanceled()
                 else tcs.SetResult()     
      | true  -> tcs.SetException(t.Exception.GetBaseException())) |> ignore
   tcs.Task

let inline delay (delay:TimeSpan) = 
   let tcs = TaskCompletionSource()
   Task.Delay(delay).ContinueWith(fun _ -> tcs.SetResult()) |> ignore
   tcs.Task

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
   m.ContinueWith((fun (x: Task<_>) -> f x.Result), token, continuationOptions, scheduler).Unwrap()

let inline bind (f: 'T -> Task<'U>) (m: Task<'T>) = 
   m.ContinueWith(fun (x: Task<_>) -> f x.Result).Unwrap()

let inline returnM a = 
   let s = TaskCompletionSource()
   s.SetResult a
   s.Task

let inline whenAll f (tasks : Task<_> seq) = Task.WhenAll(tasks) |> map(f)

let inline private flip f a b = f b a

let inline private konst a _ = a
    
type TaskBuilder(?continuationOptions, ?scheduler, ?cancellationToken) =
   let contOptions = defaultArg continuationOptions TaskContinuationOptions.None
   let scheduler = defaultArg scheduler TaskScheduler.Default
   let cancellationToken = defaultArg cancellationToken CancellationToken.None

   member this.Return x = returnM x

   member this.Zero() = returnM ()

   member this.ReturnFrom (a: Task<'T>) = a

   member this.Bind(m, f) = bindWithOptions cancellationToken contOptions scheduler f m

   member this.Combine(comp1, comp2) =
      this.Bind(comp1, comp2)

   member this.While(guard, m) =
      if not(guard()) then this.Zero() else
            this.Bind(m(), fun () -> this.While(guard, m))

   member this.TryWith(body:unit -> Task<_>, catchFn:exn -> Task<_>) =  
      try
         body()
          .ContinueWith(fun (t:Task<_>) ->
             match t.IsFaulted with
             | false -> t
             | true  -> catchFn(t.Exception.GetBaseException()))
          .Unwrap()
      with e -> catchFn(e)

   member this.TryFinally(body:unit->Task<'T>, compensation) =
            try body ()
            finally compensation()

   member this.Using(res: #IDisposable, body: #IDisposable -> Task<'T>) =
      this.TryFinally(
            (fun () -> body res),
            (fun () -> match res with null -> () | disp -> disp.Dispose())
            )

   member this.For(sequence: seq<_>, body) =
      this.Using(sequence.GetEnumerator(),
                     fun enum -> this.While(enum.MoveNext, fun () -> body enum.Current))

   member this.Delay (f: unit -> Task<'T>) = f

   member this.Run (f: unit -> Task<'T>) = f()

let task = TaskBuilder(scheduler = TaskScheduler.Current)
