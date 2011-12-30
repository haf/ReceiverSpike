module Tralld

open System

type Logger = abstract trace : Printf.StringFormat<'a,unit> -> 'a

[<Measure>] type s // seconds

type Options = {
  maxBufSize : int;
  reqPullTimeout : float<s>;
  logger : Logger
  } with 
    static member def = {
      maxBufSize = 4;
      reqPullTimeout = 30.0<s>;
      logger = { new Logger with member __.trace format = Printf.kprintf (printfn "%A: %s" System.DateTime.Now) format } 
    }

type Event = {
  Version : uint64;
  Type : string;
  ArId : Guid
  }

type Internals = {
  MaxAcceptedItem : Event option;
  MinPendingItem : Event option;
  Futures : Event Set;
  Duplicates : int
  }

/// The type of messages that a single-filter can work on
type SubMsg = QueryInternals of Internals AsyncReplyChannel
              | InsertEvent of Event
              | Drain // drain the future "queue"
              | Exit

/// The type of messages that a multi-filter can work on
type SupMsg = QueryAllInternals of (Guid * Internals) seq AsyncReplyChannel
              | InsertEvent of Event
              | Yield of Event
              | Exit

let singleFilter options (supervisor : MailboxProcessor<SupMsg>) =
  options.logger.trace "sf: started"
  MailboxProcessor.Start(fun inbox ->
    let rec loop maxAI minPI duplicates futures = async {
      options.logger.trace "sf: waiting for msg"
      let! msg = inbox.Receive()
      let state = {MaxAcceptedItem = maxAI; MinPendingItem = minPI; Futures = futures; Duplicates = duplicates}
      let mai = match maxAI with | Some(max) -> max.Version | None -> 0UL
      let mpi = match minPI with | Some(min) -> min.Version | None -> UInt64.MaxValue
      match msg with
      | QueryInternals(rchan) ->
          rchan.Reply state
          options.logger.trace "sf: query internals"
          return! loop maxAI minPI duplicates futures
      | SubMsg.InsertEvent(evt) ->
          options.logger.trace "sf: got msg to insert event #%d" evt.Version
          match evt with
          // historical event:
          | { Version = v } when v <= mai 
              -> return! loop maxAI minPI (duplicates+1) futures
          // duplicate future event:
          | { Version = v } when futures.Contains(evt) // it's up to Drain to remove from the futures list
              -> return! loop maxAI minPI (duplicates+1) futures
          // new, future event:
          | { Version = v } when v > mai+1UL ->
            let minPI' = match minPI with
                         | Some({ Version = piV }) when piV < v -> minPI // already have minimum
                         | _ -> Some(evt)                                // new minumum
            return! loop maxAI minPI' duplicates (futures.Add(evt))
          // next event:
          |  _ ->
              options.logger.trace "got next event, yielding it!"
              supervisor.Post (Yield evt)
              inbox.Post Drain
              return! loop (Some(evt)) minPI duplicates futures
        | Drain -> // check if we've closed a gap, and if we have, then insert the minimum pending item
            options.logger.trace "sf: got msg to drain!"
            match minPI with
            // now we are able to yield the next pending item!
            | Some(next) when mpi = mai + 1UL ->
                options.logger.trace "sf: posting InsertEvent to myself, event#%d" next.Version
                let futures' = futures.Remove(next)
                inbox.Post (SubMsg.InsertEvent next)
                // check the new minimum pending item, if there is one
                let minPI' = if futures'.IsEmpty then None else Some(futures'.MinimumElement)
                return! loop maxAI minPI' duplicates futures'
            | _ -> return! loop maxAI minPI duplicates futures
        | SubMsg.Exit -> return ()
    }
    loop None None 0 Set.empty)

let multiFilter options (supervisor : MailboxProcessor<_>) =
  options.logger.trace "af: started"
  MailboxProcessor.Start(fun inbox ->
    let rec loop (children : Map<Guid, MailboxProcessor<SubMsg>>) = async {
      options.logger.trace "af: waiting for msg"
      let! msg = inbox.Receive()
      match msg with
      | QueryAllInternals(replyChan) ->
          let internals = Map<Guid, Internals>(Seq.empty)
          let allReplies = 
            children |> Seq.map (fun kv -> kv.Key, kv.Value.PostAndReply(fun chan -> QueryInternals chan))
          replyChan.Reply allReplies
          return! loop children
      | InsertEvent(evt) ->
          let msg' = (SubMsg.InsertEvent(evt))
          match children.TryFind evt.ArId with
          | Some(child) -> // child found
              child.Post msg'
              return! loop children
          | None      -> // new child
              let child = singleFilter options inbox
              child.Post msg'
              return! loop (children.Add(evt.ArId, child))
      | Yield(event)  -> // got event from subordinate actors
          supervisor.Post msg
      | Exit          -> return ()
      }
    loop (Map.empty))
    

// test-code:
let aaaa = Guid.NewGuid()
let mutable sub = None;
let coord = MailboxProcessor<SupMsg>.Start(fun inbox ->
  printfn "test: started"
  let s = singleFilter Options.def inbox
  let m = multiFilter Options.def inbox
  sub <- Some(s)
  let rec loop() = async {
    let internals = s.PostAndReply(fun chan -> QueryInternals(chan))
    printfn "%s - test: internals: %A" (DateTime.Now.ToString()) internals
    let! gotten = inbox.Receive()
    match gotten with 
    | Yield(event) -> printfn "%s - got actual event: %A" (DateTime.Now.ToString()) event ; return! loop()
    | _ -> return! loop()
  }
  loop())
  
sub.Value.Post (SubMsg.InsertEvent({Version = 1UL; Type = ""; ArId = aaaa }))
sub.Value.Post (SubMsg.InsertEvent({Version = 2UL; Type = ""; ArId = aaaa }))
sub.Value.Post (SubMsg.InsertEvent({Version = 3UL; Type = ""; ArId = aaaa }))
sub.Value.Post (SubMsg.InsertEvent({Version = 4UL; Type = ""; ArId = aaaa }))
let res = sub.Value.PostAndReply(fun chan -> QueryInternals(chan))
