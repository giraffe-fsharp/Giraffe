module ConfigurationApp.Startup

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Giraffe.HttpHandlers
open Giraffe.Middleware

type Startup(configuration : IConfiguration) =
    let webApp =
        choose [
            GET >=>
                choose [
                    route "/" >=> text configuration.["HelloMessage"]
                ]
            setStatusCode 404 >=> text "Not Found" ]

    member this.Configure(app : IApplicationBuilder, env : IHostingEnvironment) =
        app.UseGiraffe webApp
