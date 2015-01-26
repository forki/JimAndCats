﻿module Jim.Tests.QueryIntegrationTests

open System
open System.Text
open System.Net.Http

open Jim.Domain
open Jim.UserModel
open Jim.Tests.IntegrationTestHelpers
open Jim.ApplicationService
open Jim.WebServer

open Suave
open Suave.Types
open Suave.Web
open Suave.Testing
open Fuchu
open Swensen.Unquote.Assertions

[<Tests>]
let commandTests =
    testList "Command integration tests"
        [
        testCase "Should be able to fetch a user" (fun () ->
            let store = storeWithEvents [UserCreated { Id = guid1; Name=Username "Bob Holness"; Email=EmailAddress "bob.holness@itv.com"; PasswordHash=PasswordHash "p4ssw0rd"; CreationTime = epoch} ]

            let actual = (run_with' (webApp <| new AppService(store, streamId))) |> req HttpMethod.GET "/users/3C71C09A-2902-4682-B8AB-663432C8867B" None

            test <@ actual.Contains("Bob Holness") @>)
        ]
