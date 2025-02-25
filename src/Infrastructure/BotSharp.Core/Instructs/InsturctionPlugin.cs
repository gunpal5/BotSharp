using BotSharp.Abstraction.Plugins.Models;
using Microsoft.Extensions.Configuration;

namespace BotSharp.Core.Instructs;

public class InsturctionPlugin : IBotSharpPlugin
{
    public string Id => "8189e133-819c-4505-9f82-84f793bc1be0";
    public string Name => "Instruction";
    public string Description => "Handle agent instruction request";

    public void RegisterDI(IServiceCollection services, IConfiguration config)
    {
        
    }

    public bool AttachMenu(List<PluginMenuDef> menu)
    {
        var section = menu.First(x => x.Label == "Apps");
        menu.Add(new PluginMenuDef("Instruction", link: "page/instruction", icon: "bx bx-book-content", weight: section.Weight + 5));
        return true;
    }
}
