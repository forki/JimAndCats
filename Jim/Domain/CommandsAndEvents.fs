﻿module Jim.Domain.CommandsAndEvents

open NodaTime

open System
open System.Text.RegularExpressions

open MicroCQRS.Common
open MicroCQRS.Common.CommandFailure
open MicroCQRS.Common.Result
open Jim.Domain.AuthenticationService
open Jim.Domain.UserAggregate

[<AutoOpen>]
module Commands =
    type Command =
        | CreateUser of CreateUser
        | SetName of SetName
        | SetEmail of SetEmail
        | SetPassword of SetPassword

    and CreateUser = {
        Name: string
        Email: string
        Password: string
    }

    and SetName = {
        Id: Guid
        Name: string    
    }

    and SetEmail = {
        Id: Guid
        Email: string   
    }

    and SetPassword = {
        Id: Guid
        Password: string   
    }

[<AutoOpen>]
module Events =
    type Event =
        | UserCreated of UserCreated
        | NameChanged of NameChanged
        | EmailChanged of EmailChanged
        | PasswordChanged of PasswordChanged
    
    and UserCreated = {
        Id: Guid
        Name: Username
        Email: EmailAddress
        PasswordHash: PasswordHash
        CreationTime: Instant
    }

    and NameChanged = {
        Id: Guid
        Name: Username
    }

    and EmailChanged = {
        Id: Guid
        Email: EmailAddress
    }

    and PasswordChanged = {
        Id: Guid
        PasswordHash: PasswordHash
    }

[<AutoOpen>]
module private EventHandlers =
    let userCreated (repository:ISimpleRepository<User>) (event: UserCreated) =
        repository.Put event.Id
            {
                User.Id = event.Id
                Name = event.Name
                Email = event.Email
                PasswordHash = event.PasswordHash
                CreationTime = event.CreationTime
            }

    let nameChanged (repository:ISimpleRepository<User>) (event : NameChanged) =
        match repository.Get(event.Id) with
        | Some user -> repository.Put event.Id {user with Name = event.Name}
        | None -> ()

    let emailChanged (repository:ISimpleRepository<User>) (event : EmailChanged) =
        match repository.Get(event.Id) with
        | Some user -> repository.Put user.Id {user with Email = event.Email}
        | None -> ()

    let passwordChanged (repository:ISimpleRepository<User>) (event : PasswordChanged) =
        match repository.Get(event.Id) with
        | Some user -> repository.Put user.Id {user with PasswordHash = event.PasswordHash}
        | None -> ()

let handleEvent (repository : ISimpleRepository<User>) = function
    | UserCreated event -> userCreated repository event
    | NameChanged event -> nameChanged repository event
    | EmailChanged event -> emailChanged repository event
    | PasswordChanged event -> passwordChanged repository event

[<AutoOpen>]
module private CommandHandlers =
    module private Constants =
        let minPasswordLength = 7 //using PBKDF2 with lots of iterations so needn't be huge
        let minUsernameLength = 5

    let createUsername (s:string) =
        let trimmedName = s.Trim()
     
        if trimmedName.Length < Constants.minUsernameLength then
            Failure (BadRequest (sprintf "Username must be at least %d characters" Constants.minUsernameLength))
        else
            Success (Username trimmedName)

    let canonicalizeEmail (input:string) =
        input.Trim().ToLower()

    let createEmailAddress (s:string) =
        let canonicalized = canonicalizeEmail s
        if Regex.IsMatch(canonicalized, @"^\S+@\S+\.\S+$") 
            then Success (EmailAddress canonicalized)
            else Failure (BadRequest "Invalid email address")

    let createPasswordHash hashFunc (s:string) =
        let trimmedPassword = s.Trim()

        if trimmedPassword.Length < Constants.minPasswordLength then
            Failure (BadRequest (sprintf "Password must be at least %d characters" Constants.minPasswordLength))
        else
            Success (PasswordHash (hashFunc (trimmedPassword)))

    let createUser
        (createGuid: unit -> Guid)
        (createTimestamp: unit -> Instant)
        hashFunc
        (command : CreateUser) =   
        resultBuilder {
            let! name = createUsername command.Name
            let! email = createEmailAddress command.Email
             //password hashing expensive so should come last
            let! hash = createPasswordHash hashFunc command.Password

            return Success (UserCreated {
                    Id = createGuid()
                    Name = name
                    Email = email
                    PasswordHash = hash
                    CreationTime = createTimestamp()
            })
        }

    let runCommandIfUserExists (repository : ISimpleRepository<User>) id command f =
        match repository.Get(id) with
        | None -> Failure NotFound
        | _ -> f command

    let setName (command : SetName) =
        match createUsername command.Name with
        | Success name -> Success (NameChanged { Id = command.Id; Name = name; })
        | Failure f -> Failure f

    let setEmail (command : SetEmail) =
        match createEmailAddress command.Email with
        | Success email -> Success (EmailChanged { Id = command.Id; Email = email; })
        | Failure f -> Failure f

    let setPassword hashFunc (command : SetPassword) =
        match createPasswordHash hashFunc command.Password with
        | Success hash -> Success (PasswordChanged { Id = command.Id; PasswordHash = hash; })
        | Failure f -> Failure f

let handleCommand (createGuid: unit -> Guid) (createTimestamp: unit -> Instant) (hashFunc: string -> string) (command:Command) (repository : ISimpleRepository<User>) =
    match command with
        | CreateUser command -> createUser createGuid createTimestamp hashFunc command
        | SetName command -> runCommandIfUserExists repository command.Id command  setName
        | SetEmail command -> runCommandIfUserExists repository command.Id command setEmail
        | SetPassword command -> runCommandIfUserExists repository command.Id command (setPassword hashFunc)

let handleCommandWithAutoGeneration (command:Command) (repository : ISimpleRepository<User>) =
    handleCommand
        Guid.NewGuid
        (fun () -> SystemClock.Instance.Now)
        PBKDF2.getHash
        command
        repository

(* End Command Handlers *)