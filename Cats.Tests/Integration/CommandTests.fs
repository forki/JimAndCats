﻿module Cats.Tests.Integration.CommandTests

open Cats.Domain.CommandsAndEvents
open Cats.Domain.CatAggregate
open Cats.Tests.Integration.CreateWebServer
open Fuchu
open MicroCQRS.Common.Testing.SuaveHelpers
open NodaTime
open Suave.Testing
open Suave.Types
open System
open System.Text
open System.Net
open Swensen.Unquote.Assertions

let guid1 = new Guid("3C71C09A-2902-4682-B8AB-663432C8867B")
let epoch = new Instant(0L)
let catHasBeenCreated = [CatCreated {Id = guid1; Title = PageTitle "My lovely cat"; CreationTime=epoch}]

let getWebServerWithNoEvents() = getWebServer []
let getWebServerWithACat() = getWebServer catHasBeenCreated

[<Tests>]
let commandTests =
    testList "Command integration tests"
        [
        testCase "Should be able to create a cat" (fun () ->
            let content, statusCode = requestResponseWithPostData getWebServerWithNoEvents HttpMethod.POST "/cats/create" """{"title":"My lovely cat"}""" statusCodeAndContent

            test <@ content.Contains("\"Id\":") && statusCode = HttpStatusCode.OK @>)

        testCase "Creating a cat with too short a name returns bad request" (fun () ->
            let actualContent, actualStatusCode = requestResponseWithPostData getWebServerWithNoEvents HttpMethod.POST "/cats/create" """{"title":"a"}""" statusCodeAndContent

            test <@ actualContent.Contains("Title must be at least") && actualStatusCode = HttpStatusCode.BadRequest @>)

        testCase "Should be able to change title" (fun () ->
            let actual = requestResponseWithPostData getWebServerWithACat HttpMethod.PUT "/cats/3C71C09A-2902-4682-B8AB-663432C8867B/title" """{"title":"My new lovely cat name"}""" status_code

            test <@ actual = HttpStatusCode.OK @>)

        testCase "Should not be able to change title to something too short" (fun () ->
            let actualContent, actualStatusCode = requestResponseWithPostData getWebServerWithACat HttpMethod.PUT "/cats/3C71C09A-2902-4682-B8AB-663432C8867B/title" """{"title":"a"}""" statusCodeAndContent

            test <@ actualContent.Contains("Title must be at least") && actualStatusCode = HttpStatusCode.BadRequest @>)

        testCase "Should get 404 trying to set title of non-existent cat" (fun () ->
            let actual = requestResponseWithPostData getWebServerWithNoEvents HttpMethod.PUT "/cats/3C71C09A-2902-4682-B8AB-663432C8867B/title" """{"title":"My new lovely cat name"}""" status_code

            HttpStatusCode.NotFound =? actual)
        ]