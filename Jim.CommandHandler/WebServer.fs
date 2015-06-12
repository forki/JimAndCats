﻿module Jim.CommandHandler.WebServer

open Jim.CommandHandler
open Jim.CommandHandler.Domain
open Jim.CommandHandler.AppSettings
open Suave
open Logary //must be opened after Suave
open Suave.Http
open Suave.Http.Applicatives
open Suave.Web
open Suave.Extensions.ConfigDefaults
open Suave.Extensions.Guids
open Suave.Extensions.Json
open System
open System.IO

let swaggerSpec = Files.browseFileHome <| Path.Combine("static", "api-docs.json")

let index = Successful.OK "Hello from JIM Command Handler"

let webApp
    (commandToWebPart : Guid -> Command -> Types.WebPart)
    (getAggregate: Guid -> Async<User option * int>)
    (saveEventToNewStream : Guid -> Event -> Async<unit>) =
    choose [
        GET >>= choose [
            url "/api-docs" >>= swaggerSpec
            url "/" >>= index
            (* This method is just a utility for debugging etc, other services should listen to Event Store events and build their own read models *)
            urlScanGuid "/users/%s" (fun id -> AppService.DiagnosticQueries.getUser getAggregate id)]        

        POST >>= url "/users/create" >>= (tryMapJson <| AppService.createUser saveEventToNewStream)

        PUT >>= choose [ 
            urlScanGuid "/users/%s/name" (fun id -> tryMapJson <| AppService.setName commandToWebPart id)
            urlScanGuid "/users/%s/email" (fun id -> tryMapJson <| AppService.setEmail commandToWebPart id)
            urlScanGuid "/users/%s/password"  (fun id -> tryMapJson <| AppService.setPassword commandToWebPart id) ]

        RequestErrors.NOT_FOUND "404 not found" ] 

[<EntryPoint>]
let main argv = 
    let web_config = makeConfig appSettings.Port (Suave.SuaveAdapter(Logging.logary.GetLogger "suave"))
    printfn "Starting JIM on %d" appSettings.Port

    try     
        let postCommand, getAggregate, saveEventToNewStream = AppService.getAppServices()
        let commandToWebPart = AppService.createCommandToWebPartMapper postCommand
        startWebServer web_config (webApp commandToWebPart getAggregate saveEventToNewStream)
    with
    | e -> Logger.fatal (Logging.getCurrentLogger()) (e.ToString())

    Logging.logary.Dispose()
    0

