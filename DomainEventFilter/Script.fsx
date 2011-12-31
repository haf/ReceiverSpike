#load "Tralld.fs"
open Tralld
open System

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
