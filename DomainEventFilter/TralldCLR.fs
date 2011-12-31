namespace TralldCLR

open Tralld
open Extensions
open System

// CLR-flavoured types
type Event =
  interface
    abstract Version : uint64
    abstract Type : string
    abstract ArId : Guid
  end

type Internals = 
  struct
     val MaxAcceptedItem : Event
     val MinPendingItem : Event
     val Futures : Event seq
     val Duplicates : int
     new(mai, mpi, fs, ds) = {
       MaxAcceptedItem = mai;
       MinPendingItem = mpi;
       Futures = fs;
       Duplicates = ds
       }
  end

/// filter for domain events!
type MultiFilter() =
    
  /// convert the internal event interface to the public equivalent
  let toE e = { new Event with 
                  member y.Version = e.Version
                  member y.Type = e.Type
                  member y.ArId = e.ArId }

  /// convert from the CLR event to the internal event representation
  let fromE (e : Event) = { Version = e.Version; Type = e.Type; ArId = e.ArId }
  
  /// create a struct instead of the record type of f#
  let intern i = 
    let mai = i.MaxAcceptedItem.BindOrNull toE
    let mpi = i.MinPendingItem.BindOrNull toE
    let evts = (Set.toSeq i.Futures) |> Seq.map toE
    Internals(mai, mpi, evts, i.Duplicates)
  
  /// the supervisor actor (it's really just a proxy though!)
  let supervisor = MailboxProcessor<SupMsg>.Start(fun inbox ->
    let target = multiFilter Options.def inbox
    let rec loop subscribers = async {
      let! message = inbox.Receive()
      System.Diagnostics.Debug.WriteLine "received msg"
      match message with
      | Yield(event) -> 
        System.Diagnostics.Debug.WriteLine (sprintf "%s - got actual event: %A" (DateTime.Now.ToString()) event)
        return! loop()
      | QueryAllInternals(chan) ->
        let res = target.PostAndReply(fun chan'-> QueryAllInternals(chan'))
        System.Diagnostics.Debug.WriteLine "query internals done"
        chan.Reply res
        return! loop()
      | InsertEvent(_) -> 
        System.Diagnostics.Debug.WriteLine "inserting message"
        target.Post message
        return! loop()
      | Exit -> return ()
    }
    loop())

  member x.QueryState(timeout : System.TimeSpan) =
    System.Diagnostics.Debug.WriteLine "querying state"
    let result = supervisor.PostAndReply(fun chan -> QueryAllInternals(chan))
    result |> Seq.map (fun (g, i) -> Tuple.Create(g, (intern i)))

  member x.Receive (evt : Event) =
    let evt' = fromE evt
    let msg = InsertEvent evt'
    supervisor.Post msg
    ()