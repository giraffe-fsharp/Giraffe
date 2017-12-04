namespace Giraffe.Razor

[<AutoOpen>]
module Middleware =

    open Microsoft.AspNetCore.Mvc.Razor
    open Microsoft.Extensions.DependencyInjection
    open Microsoft.Extensions.FileProviders

    type IServiceCollection with
        member this.AddRazorEngine (viewsFolderPath : string) =
            this.Configure<RazorViewEngineOptions>(
                fun (options : RazorViewEngineOptions) ->
                    options.FileProviders.Clear()
                    options.FileProviders.Add(new PhysicalFileProvider(viewsFolderPath)))
                .AddMvc()
            |> ignore