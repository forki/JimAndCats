﻿module Jim.DuplicateEmailChecker.Program

open EventStore.YetAnotherClient
open FSharp.Data
open Jim.DuplicateEmailChecker.AppSettings
open System

[<Literal>]
let insertUser = "INSERT INTO Jim.UserEmail VALUES (@Id, @Email)"

[<Literal>]
let checkForDuplicateEmail =
    "select exists (select * from Jim.UserEmail where Email = @Email)"

type InsertUserCommand = SqlCommandProvider<insertUser, "name=Jim">
type CheckForDuplicateEmailQuery = SqlCommandProvider<checkForDuplicateEmail, "name=Jim">

type EmailAddress = EmailAddress of string

[<AutoOpen>]
module Events =
    type Event =
        | UserCreated of UserCreated
        | EmailChanged of EmailChanged
    
    
    (* Only deserialize the relevant parts of the domain model *)
    and UserCreated = {
        Id: Guid
        Email: EmailAddress
    }

    and EmailChanged = {
        Id: Guid
        Email: EmailAddress
    }

module private EventHandlers =
    let private userCreated (event: UserCreated) =
        ()

    let private emailChanged (event : EmailChanged) =
        ()

    let handleEvent = function
        | UserCreated event -> userCreated event
        | EmailChanged event -> emailChanged event

let subscribe() =
    let store = new EventStore<Event>(appSettings.PrivateEventStoreIp, appSettings.PrivateEventStorePort) :> IEventStore<Event>
    store.SubscribeToStreamFrom appSettings.PublicIdentityStream 0 (fun e -> EventHandlers.handleEvent repository e |> Async.RunSynchronously)

[<EntryPoint>]
let main argv = 
    subscribe()
    0
