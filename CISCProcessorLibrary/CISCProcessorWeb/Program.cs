using CISCProcessorLibrary;
using CISCProcessorLibrary.Timing;
using CISCProcessorWeb;
using static CISCProcessorWeb.Dtos;

// CISCProcessorWeb hosts the visualizer page (the repository's web/ folder, copied into wwwroot for published
// builds) and exposes a small JSON API so the page can run programs on the real .NET processor instead of
// its JavaScript mirror.

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
MicroarchitectureDto[] microarchitectures = Enum.GetValues<Microarchitecture>().Select(Dtos.ToDto).ToArray();

RouteGroupBuilder api = app.MapGroup("/api");

api.MapGet("/info", () => new Info($"{SimulatorInfo.Name} .NET core", SimulatorInfo.Version, runtime, InstructionSet.Default.All.Count, microarchitectures));

api.MapGet("/instructions", () => InstructionSet.Default.All.Select(ToDto));

api.MapGet("/samples", () => SamplePrograms.All);

api.MapGet("/microarchitectures", () => microarchitectures);

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

api.MapPost("/sessions", (SessionRequest request, SessionStore store) =>
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

    if (!Enum.TryParse(request.Microarchitecture ?? nameof(Microarchitecture.Classic1975), ignoreCase: true, out Microarchitecture microarchitecture))
        return Results.BadRequest(new { message = $"Unknown micro-architecture '{request.Microarchitecture}'." });

    MachineSession session = store.Create(program, microarchitecture, request.Input);
    return Results.Ok(ToDto(session));
});

api.MapGet("/sessions/{id}", (string id, SessionStore store) =>
{
    MachineSession? session = store.Find(id);
    return session is null ? Results.NotFound(new { message = "Unknown session." }) : Results.Ok(session.Locked(() => ToDto(session)));
});

api.MapPost("/sessions/{id}/step", (string id, SessionStore store) =>
{
    MachineSession? session = store.Find(id);
    if (session is null) return Results.NotFound(new { message = "Unknown session." });
    if (session.Cpu.Halted) return Results.Conflict(new { message = "The processor is halted. Reset it first." });

    TraceDto trace = session.Locked(() => ToDto(session.Cpu.Step(), session.Cpu));
    return Results.Ok(new { trace });
});

api.MapPost("/sessions/{id}/run", (string id, RunRequest request, SessionStore store) =>
{
    MachineSession? session = store.Find(id);
    if (session is null) return Results.NotFound(new { message = "Unknown session." });

    int budget = Math.Clamp(request.MaxSteps ?? 512, 1, 5000);
    var stopAt = new HashSet<int>(request.StopAt ?? Array.Empty<int>());
    List<TraceDto> traces = session.Locked(() =>
    {
        var list = new List<TraceDto>();
        CISCProcessor cpu = session.Cpu;
        while (list.Count < budget && !cpu.Halted)
        {
            if (list.Count > 0 && !cpu.Waiting && cpu.PendingInterrupts == 0 && stopAt.Contains(cpu.InstructionPointer)) break;
            list.Add(ToDto(cpu.Step(), cpu));
        }
        return list;
    });
    return Results.Ok(new { traces });
});

api.MapPost("/sessions/{id}/reset", (string id, SessionStore store) =>
{
    MachineSession? session = store.Find(id);
    if (session is null) return Results.NotFound(new { message = "Unknown session." });

    SessionDto dto = session.Locked(() =>
    {
        session.Load();
        return ToDto(session);
    });
    return Results.Ok(dto);
});

api.MapPost("/sessions/{id}/input", (string id, InputRequest request, SessionStore store) =>
{
    MachineSession? session = store.Find(id);
    if (session is null) return Results.NotFound(new { message = "Unknown session." });

    string text = request.Text ?? string.Empty;
    StateDto state = session.Locked(() =>
    {
        session.Cpu.InputDevice.Enqueue(text);
        return ToDto(session.Cpu);
    });
    return Results.Ok(new { state, queued = session.Cpu.InputDevice.PeekText() });
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
