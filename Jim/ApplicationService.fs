﻿namespace Jim.ApplicationService

open EventPersistence
open Jim.AppSettings
open Jim.Domain

type AppService () =
    let streamId = appSettings.UserStream

    let projection = fun (x: Event) -> ()

    let store = match appSettings.UseEventStore with
    | true -> new EventPersistence.EventStore<Event>(streamId, projection) :> IEventStore<Event>
    | false -> new EventPersistence.InMemoryStore<Event>(projection) :> IEventStore<Event>

    let load =
        let rec fold (state: State) version =
            async {
            let! events, lastEvent, nextEvent = 
                store.ReadStream streamId version 500

            let state = List.fold handleEvent state events
            match nextEvent with
            | None -> return lastEvent, state
            | Some n -> return! fold state n }
        fold (new State()) 0

    let save expectedVersion events = store.AppendToStream streamId expectedVersion events

    let agent = MailboxProcessor.Start <| fun inbox -> 
        let rec messageLoop version state = async {
            let! command = inbox.Receive()
            let newEvents = handleCommand command state
            do! save version newEvents
            let newState = List.fold handleEvent state newEvents
            return! messageLoop version state
            }
        async {
            let! version, state = load
            return! messageLoop version state
            }

    member this.createUser () = agent.Post <| CreateUser { 
            Name="Bob Holness"
            Email="bob.holness@itv.com"
            Password="p4ssw0rd"
            }

    member this.listUsers () = []