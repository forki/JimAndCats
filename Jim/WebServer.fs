﻿module Jim.WebServer

open Jim
open Jim.AppSettings
open Jim.Hawk
open Suave
open Logary //must be opened after Suave
open Suave.Http
open Suave.Http.Applicatives
open Suave.Types
open Suave.Web
open Suave.Extensions.ConfigDefaults
open Suave.Extensions.Guids
open Suave.Extensions.Json
open System.IO

let swaggerSpec = Files.browse_file' <| Path.Combine("static", "api-docs.json")

let index = Successful.OK "Hello from JIM"

let authenticateWithRepo repository partNeedingAuth =
    Hawk.authenticate'
        (hawkSettings repository)
        (fun err -> RequestErrors.UNAUTHORIZED (err.ToString()))
        (fun (attr, creds, user) -> partNeedingAuth)

let webApp postCommand repository =
    let requireAuth = authenticateWithRepo repository

    choose [
        GET >>= choose [
            url "/api-docs" >>= swaggerSpec
            url "/" >>= index
            url "/users" >>= (requireAuth <| QueryEndpoints.listUsers repository)
            url_scan_guid "/users/%s" (fun id -> requireAuth <| QueryEndpoints.getUser repository id) ]

        (* TODO this endpoint should have some sort of security - only open to pre-existing users sending their own Hawk credentials, or only available via HTTPS *)
        POST >>= url "/users/create" >>= tryMapJson (CommandEndpoints.createUser postCommand)

        PUT >>= choose [ 
            url_scan_guid "/users/%s/name" (fun id -> requireAuth (tryMapJson <| CommandEndpoints.setName postCommand id))
            url_scan_guid "/users/%s/email" (fun id -> requireAuth (tryMapJson <| CommandEndpoints.setEmail postCommand id))
            url_scan_guid "/users/%s/password"  (fun id -> requireAuth (tryMapJson <| CommandEndpoints.setPassword postCommand id)) ]

        RequestErrors.NOT_FOUND "404 not found" ] 

[<EntryPoint>]
let main argv = 
    let web_config = makeConfig appSettings.Port (Suave.SuaveAdapter(Logging.logary.GetLogger "suave"))
    printfn "Starting JIM on %d" appSettings.Port

    try     
        let postCommand, repository = CommandEndpoints.getCommandPosterAndRepository()
        web_server web_config (webApp postCommand repository)
    with
    | e -> Logger.fatal (Logging.getCurrentLogger()) (e.ToString())

    Logging.logary.Dispose()
    0

