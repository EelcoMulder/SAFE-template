open System.IO
open System.Threading.Tasks

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open FSharp.Control.Tasks.V2
open Giraffe
open Saturn
open Shared

#if (remoting)
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
#else
open Giraffe.Serialization
#endif
#if (deploy == "azure")
open Microsoft.WindowsAzure.Storage
#endif

//#if (deploy == "azure")
let tryGetEnv = System.Environment.GetEnvironmentVariable >> function null | "" -> None | x -> Some x
let publicPath = tryGetEnv "public_path" |> Option.defaultValue "../Client/public" |> Path.GetFullPath
let storageAccount = tryGetEnv "STORAGE_CONNECTIONSTRING" |> Option.defaultValue "UseDevelopmentStorage=true" |> CloudStorageAccount.Parse
//#else
let publicPath = Path.GetFullPath "../Client/public"
//#endif
let port = 8085us

#if (application == "counter")
let getInitCounter () : Task<Counter> = task { return 42 }
#else
let getInitMessage () : Task<Message> = task { return "Pong" }
#endif

#if (remoting)
#if (application == "counter")
let counterApi = {
    initialCounter = getInitCounter >> Async.AwaitTask
}
#else
let messageApi = {
    initialMessage = getInitMessage >> Async.AwaitTask
}
#endif

let webApp =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
#if (application == "counter")
    |> Remoting.fromValue counterApi
#else
    |> Remoting.fromValue messageApi
#endif
    |> Remoting.buildHttpHandler

#else
let webApp = router {
#if (application == "counter")
    get "/api/init" (fun next ctx ->
        task {
                let! counter = getInitCounter()
                return! Successful.OK counter next ctx
#else
    get "/api/message" (fun next ctx ->
        task {
                let! message = getInitMessage()
                return! Successful.OK message next ctx
#endif
        })
}

#endif
#if (!remoting)
let configureSerialization (services:IServiceCollection) =
    let fableJsonSettings = Newtonsoft.Json.JsonSerializerSettings()
    fableJsonSettings.Converters.Add(Fable.JsonConverter())
    services.AddSingleton<IJsonSerializer>(NewtonsoftJsonSerializer fableJsonSettings)

#endif
#if (deploy == "azure")
let configureAzure (services:IServiceCollection) =
    tryGetEnv "APPINSIGHTS_INSTRUMENTATIONKEY"
    |> Option.map services.AddApplicationInsightsTelemetry
    |> Option.defaultValue services

#endif
let app = application {
    url ("http://0.0.0.0:" + port.ToString() + "/")
    use_router webApp
    memory_cache
    use_static publicPath
    #if (!remoting)
    service_config configureSerialization
    #endif
    #if (deploy == "azure")
    service_config configureAzure
    #endif
    use_gzip
}

run app
