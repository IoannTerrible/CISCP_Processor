using CISCProcessorLibrary;
using CISCProcessorWeb;
using static CISCProcessorWeb.Dtos;

// CISCProcessorWeb hosts the visualizer page (copied from ../../web into wwwroot at build time) and exposes
// a small JSON API so the page can run programs on the real .NET processor instead of its JavaScript mirror.

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = ResolveWebRoot(),
});
builder.Services.AddSingleton<SessionStore>();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);

WebApplication app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

string runtime = $".NET {Environment.Version}";

RouteGroupBuilder api = app.MapGroup("/api");

api.MapGet("/info", () => new Info($"{SimulatorInfo.Name} .NET core", SimulatorInfo.Version, runtime, InstructionSet.Default.All.Count));

api.MapGet("/instructions", () => InstructionSet.Default.All.Select(ToDto));

api.MapGet("/samples", () => SamplePrograms.All);

api.MapPost("/assemble", (SourceRequest request) =>
{
    try
    {
        return Results.Ok(ToDto(Assembler.Assemble(request.Source ?? string.Empty)));
    }
    catch (AssemblyException ex)
    {
        return Results.BadRequest(new AssemblyError(ex.Line, ex.Message, ex.Detail));
    }
});

api.MapPost("/sessions", (SourceRequest request, SessionStore store) =>
{
    AssembledProgram program;
    try
    {
        program = Assembler.Assemble(request.Source ?? string.Empty);
    }
    catch (AssemblyException ex)
    {
        return Results.BadRequest(new AssemblyError(ex.Line, ex.Message, ex.Detail));
    }

    MachineSession session = store.Create(program);
    return Results.Ok(new SessionDto(session.Id, ToDto(program), ToDto(session.Cpu.GetState(), null)));
});

api.MapGet("/sessions/{id}", (string id, SessionStore store) =>
{
    MachineSession? session = store.Find(id);
    return session is null
        ? Results.NotFound(new { message = "Unknown session." })
        : Results.Ok(new SessionDto(session.Id, ToDto(session.Program), session.Locked(() => ToDto(session.Cpu.GetState(), session.Cpu.FaultMessage))));
});

api.MapPost("/sessions/{id}/step", (string id, SessionStore store) =>
{
    MachineSession? session = store.Find(id);
    if (session is null) return Results.NotFound(new { message = "Unknown session." });
    if (session.Cpu.Halted) return Results.Conflict(new { message = "The processor is halted. Reset it first." });

    TraceDto trace = session.Locked(() => ToDto(session.Cpu.Step(), session.Cpu.FaultMessage));
    return Results.Ok(new { trace });
});

api.MapPost("/sessions/{id}/run", (string id, RunRequest request, SessionStore store) =>
{
    MachineSession? session = store.Find(id);
    if (session is null) return Results.NotFound(new { message = "Unknown session." });

    int budget = Math.Clamp(request.MaxInstructions ?? 512, 1, 5000);
    var stopAt = new HashSet<int>(request.StopAt ?? Array.Empty<int>());
    List<TraceDto> traces = session.Locked(() =>
    {
        var list = new List<TraceDto>();
        CISCProcessor cpu = session.Cpu;
        while (list.Count < budget && !cpu.Halted)
        {
            if (list.Count > 0 && stopAt.Contains(cpu.InstructionPointer)) break;
            list.Add(ToDto(cpu.Step(), cpu.FaultMessage));
        }
        return list;
    });
    return Results.Ok(new { traces });
});

api.MapPost("/sessions/{id}/reset", (string id, SessionStore store) =>
{
    MachineSession? session = store.Find(id);
    if (session is null) return Results.NotFound(new { message = "Unknown session." });

    StateDto state = session.Locked(() =>
    {
        session.Cpu.LoadProgram(session.Program);
        return ToDto(session.Cpu.GetState(), null);
    });
    return Results.Ok(new { state });
});

api.MapDelete("/sessions/{id}", (string id, SessionStore store) =>
    store.Remove(id) ? Results.NoContent() : Results.NotFound(new { message = "Unknown session." }));

app.Logger.LogInformation("Serving the visualizer from {WebRoot}", app.Environment.WebRootPath);
app.Run();

// When the host runs from the source tree (dotnet run, Visual Studio) the repository's web/ folder is served
// directly, so edits to the page show up without a rebuild. Published builds carry a copy in wwwroot/.
static string ResolveWebRoot()
{
    DirectoryInfo? directory = new(AppContext.BaseDirectory);
    for (int level = 0; level < 8 && directory is not null; level++, directory = directory.Parent)
    {
        string candidate = Path.Combine(directory.FullName, "web");
        if (File.Exists(Path.Combine(candidate, "index.html")) && File.Exists(Path.Combine(candidate, "js", "app.js")))
            return candidate;
    }
    return Path.Combine(AppContext.BaseDirectory, "wwwroot");
}
