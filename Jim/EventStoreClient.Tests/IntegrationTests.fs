﻿module EventStoreClient.IntegrationTests

open EventStoreClient.Client

open Xunit

type TestRecordParent =
    | Child1 of Child1
    | Child2 of Child2

and Child1 =
    int * int

and Child2 =
    string * string

[<Fact>]
let ``Should be able to serialize and deserialize events to/from event store``() =
    let streamId = "integrationTest"
    let eventHandler event = printfn event
    let store = EventStoreClient.Client.create() |> subscribe streamId eventHandler
    appendToStream store streamId -1 [Child1(2, 3); Child2("a", "b")] |> Async.RunSynchronously