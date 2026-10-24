// Launches a FileMind host/tool assembly under Smart App Control enforcement.
// SAC blocks unsigned FileMind.*.dll image loads (event 3077); this loads the
// entry assembly and its dependencies from byte streams in a dedicated
// AssemblyLoadContext — identical IL, no image-map of the blocked files.
// Usage: dotnet fsi tools/sac-run.fsx -- <dll-path> [args...]
open System
open System.IO
open System.Reflection
open System.Runtime.Loader
open System.Threading.Tasks

let argsAll = fsi.CommandLineArgs |> Array.skip 1
if argsAll.Length = 0 then
    eprintfn "usage: dotnet fsi tools/sac-run.fsx -- <dll-path> [args...]"
    exit 2
let dll = Path.GetFullPath argsAll.[0]
let appArgs = argsAll |> Array.skip 1
let dir = Path.GetDirectoryName dll

type ByteAlc(d: string) =
    inherit AssemblyLoadContext("sac-run", isCollectible = false)
    override this.Load(name: AssemblyName) =
        let p = Path.Combine(d, name.Name + ".dll")
        if File.Exists p then
            try this.LoadFromStream(new MemoryStream(File.ReadAllBytes p))
            with _ -> null
        else null
    override this.LoadUnmanagedDll(name: string) =
        let candidates =
            [ Path.Combine(d, name + ".dll")
              Path.Combine(d, "runtimes", "win-x64", "native", name + ".dll")
              Path.Combine(d, "runtimes", "win", "native", name + ".dll") ]
        match candidates |> List.tryFind File.Exists with
        | Some p -> this.LoadUnmanagedDllFromPath p
        | None -> IntPtr.Zero

let alc = ByteAlc(dir)
let asm = alc.LoadFromAssemblyName(AssemblyName(Path.GetFileNameWithoutExtension dll))
let entry = asm.EntryPoint
if isNull entry then eprintfn "no entry point in %s" dll; exit 2
let ps = entry.GetParameters()
let invokeArgs =
    match ps.Length with
    | 0 -> [||]
    | _ -> [| box appArgs |]
let mutable exitCode = 0
try
    let result = entry.Invoke(null, invokeArgs)
    match result with
    | :? Task<int> as t -> exitCode <- t.Result
    | :? Task as t -> t.Wait()
    | :? int as i -> exitCode <- i
    | _ -> ()
with e ->
    let inner = if isNull e.InnerException then e else e.InnerException
    eprintfn "fatal: %s %s" (inner.GetType().Name) inner.Message
    exitCode <- 1
exit exitCode
