﻿open System.IO
open System.Net

open Suave
open Suave.Files
open Suave.Successful
open Suave.Filters
open Suave.Operators

open Shared

#if (remoting)
open Fable.Remoting.Server
open Fable.Remoting.Suave
#endif
#if (deploy == "azure")
open Microsoft.WindowsAzure.Storage
#endif

//#if (deploy == "azure")
let publicPath = Azure.tryGetEnv "public_path" |> Option.defaultValue "../Client/public" |> Path.GetFullPath
let port = Azure.tryGetEnv "HTTP_PLATFORM_PORT" |> Option.map System.UInt16.Parse |> Option.defaultValue 8085us
let storageAccount = Azure.tryGetEnv "STORAGE_CONNECTIONSTRING" |> Option.defaultValue "UseDevelopmentStorage=true" |> CloudStorageAccount.Parse
//#else
let publicPath = Path.GetFullPath "../Client/public"
let port = 8085us
//#endif

let config =
    { defaultConfig with
          homeFolder = Some publicPath
          bindings = [ HttpBinding.create HTTP (IPAddress.Parse "0.0.0.0") port ] }
#if (application == "counter")
let getInitCounter() : Async<Counter> = async { return 42 }
#else
let getInitMessage() : Async<Message> = async { return "Pong" }
#endif
#if (remoting)
#if (application == "counter")
let counterApi = {
    initialCounter = getInitCounter
}
#else
let messageApi = {
    initialMessage = getInitMessage
}
#endif
let webApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
#if (application == "counter")
    |> Remoting.fromValue counterApi
#else
    |> Remoting.fromValue messageApi
#endif
    |> Remoting.buildWebPart
#else
let webApi =
#if (application == "counter")
    path "/api/init" >=>
        fun ctx ->
            async {
                let! counter = getInitCounter()
                return! OK (string counter) ctx
#else
    path "/api/message" >=>
        fun ctx ->
            async {
                let! message = getInitMessage()
                let json = message |> toJson<Message> |> System.Text.Encoding.UTF8.GetString
                return! OK (json) ctx
#endif
            }
#endif

let webApp =
    choose [
        webApi
        path "/" >=> browseFileHome "index.html"
        browseHome
        RequestErrors.NOT_FOUND "Not found!"
#if (deploy == "azure")
    ] |> Azure.AI.withAppInsights Azure.AI.buildApiOperationName

Azure.AppServices.addTraceListeners()
Azure.tryGetEnv "APPINSIGHTS_INSTRUMENTATIONKEY"
|> Option.iter(fun appInsightsKey ->
    Azure.AI.configure { AppInsightsKey = appInsightsKey; DeveloperMode = false; TrackDependencies = true })
#else
    ]
#endif

startWebServer config webApp