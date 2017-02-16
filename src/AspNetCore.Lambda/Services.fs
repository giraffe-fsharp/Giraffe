module AspNetCore.Lambda.Services

open RazorLight
open Microsoft.Extensions.DependencyInjection

type IServiceCollection with
    member this.AddRazorEngine (viewsFolderPath : string) =
        this.AddSingleton<IRazorLightEngine>(EngineFactory.CreatePhysical(viewsFolderPath));