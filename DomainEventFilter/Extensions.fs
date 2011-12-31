module Extensions

type Option<'a> with
  member x.BindOrNull (f : 'a -> 'b) = match x with | Some(item) -> f item | None -> Unchecked.defaultof<'b>