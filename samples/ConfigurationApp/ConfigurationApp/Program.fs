module ConfigurationApp.App

open Microsoft.AspNetCore
open Microsoft.AspNetCore.Hosting
open ConfigurationApp.Startup

[<EntryPoint>]
let main argv =
    WebHost.CreateDefaultBuilder()
        .UseStartup<Startup>()
        .Build()
        .Run()
    0