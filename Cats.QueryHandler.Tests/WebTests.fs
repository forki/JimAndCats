﻿module Cats.QueryHandler.Tests.WebTests

open Cats.QueryHandler.WebServer
open EventStore.YetAnotherClient
open Fuchu
open TestingHelpers.SuaveHelpers
open NodaTime
open System
open System.Text
open System.Net
open Suave
open Suave.Testing
open Suave.Types

open Swensen.Unquote.Assertions

let guid1 = new Guid("3C71C09A-2902-4682-B8AB-663432C8867B")
let ownerGuid1 = new Guid("9F2FFD7A-7B24-4B72-A4A5-8EF507306038")
let epoch = new Instant(0L)
let catHasBeenCreated = [CatCreated {Id = guid1; Title = PageTitle "My lovely cat"; Owner=ownerGuid1; CreationTime=epoch}]

let streamId = "testStream"

let getWebServer events =
    let store =
        match appSettings.WriteToInMemoryStoreOnly with
        | false -> new EventStore<Event>(appSettings.PrivateEventStoreIp, appSettings.PrivateEventStorePort) :> IEventStore<Event>
        | true -> new InMemoryStore<Event>() :> IEventStore<Event>
    if not (List.isEmpty events) then
        store.AppendToStream streamId -1 events |> Async.RunSynchronously
    let repository = new GenericInMemoryRepository<Cat>()
    let initialVersion = RepositoryLoader.handleAllEventsInStream store streamId (handleEvent repository) |> Async.RunSynchronously
    let postCommand, repo = (CommandAgent.getCommandPoster store repository handleCommandWithAutoGeneration handleEvent streamId initialVersion), repository
    webApp postCommand repo

let getWebServerWithNoEvents() = getWebServer []
let getWebServerWithACat() = getWebServer catHasBeenCreated

[<Tests>]
let queryTests =
    testList "Query web API tests"
        [
        testCase "Should be able to fetch a CAT" (fun () ->
            let actual = get getWebServerWithACat "/cats/3C71C09A-2902-4682-B8AB-663432C8867B" contentString

            """{"Id":"3c71c09a-2902-4682-b8ab-663432c8867b","CreationTime":"1970-01-01T00:00:00Z"}""" =? actual)

        testCase "Should get 404 for non-existent CAT" (fun () ->
            let actual = get getWebServerWithNoEvents "/cats/3C71C09A-2902-4682-B8AB-663432C8867B" statusCode

            HttpStatusCode.NotFound =? actual)
        ]