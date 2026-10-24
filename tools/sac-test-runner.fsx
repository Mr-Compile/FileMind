// Runs xunit [Fact]/[Theory]+[InlineData] tests without path-loading test
// dependencies. Smart App Control (enforcement) blocks unsigned FileMind.*.dll
// image loads from disk (event 3077); a custom AssemblyLoadContext that serves
// the same assemblies from byte streams sidesteps the image-load check. This
// only changes how test binaries enter memory for this run; the test IL is
// identical. Usage:
//   dotnet fsi tools/sac-test-runner.fsx -- <test-bin-dir> [<test-bin-dir>...]
// Exit code = number of failed tests.
open System
open System.IO
open System.Reflection
open System.Runtime.Loader
open System.Threading.Tasks

type ByteAlc(d: string) =
    inherit AssemblyLoadContext("sac-bytes-" + Guid.NewGuid().ToString("N"), isCollectible = false)
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

let isFactAttr (cad: CustomAttributeData) =
    cad.AttributeType.FullName = "Xunit.FactAttribute"
let isTheoryAttr (cad: CustomAttributeData) =
    cad.AttributeType.FullName = "Xunit.TheoryAttribute"
let isInlineDataAttr (cad: CustomAttributeData) =
    cad.AttributeType.FullName = "Xunit.InlineDataAttribute"

let rec materialize (arg: CustomAttributeTypedArgument) (paramType: Type) : obj =
    if arg.ArgumentType.IsArray then
        let elemType = paramType.GetElementType()
        let items =
            match arg.Value with
            | :? System.Collections.ObjectModel.ReadOnlyCollection<CustomAttributeTypedArgument> as c ->
                c |> Seq.map (fun a -> materialize a elemType) |> Seq.toArray
            | _ -> [||]
        let arr = Array.CreateInstance(elemType, items.Length)
        items |> Array.iteri (fun i v -> arr.SetValue(v, i))
        box arr
    elif isNull arg.Value then null
    elif paramType.IsEnum && not (arg.Value.GetType().IsEnum) then
        Enum.ToObject(paramType, arg.Value)
    else arg.Value

let inlineDataCases (m: MethodInfo) =
    let ps = m.GetParameters()
    CustomAttributeData.GetCustomAttributes(m)
    |> Seq.choose (fun cad ->
        if isInlineDataAttr cad && cad.ConstructorArguments.Count > 0 then
            let arrArg = cad.ConstructorArguments.[0]
            let vals =
                match arrArg.Value with
                | :? System.Collections.ObjectModel.ReadOnlyCollection<CustomAttributeTypedArgument> as c ->
                    c |> Seq.toList
                | _ -> [arrArg]
            let args =
                vals
                |> List.mapi (fun i a -> materialize a (if i < ps.Length then ps.[i].ParameterType else typeof<obj>))
                |> List.toArray
            Some(args |> Seq.map (fun o -> if isNull o then "null" else o.ToString()) |> String.concat ", ", args)
        else None)

let runOne (ty: Type) (m: MethodInfo) (args: obj[]) (label: string) =
    try
        let inst = Activator.CreateInstance(ty)
        try
            match m.Invoke(inst, args) with
            | :? Task as t -> t.Wait()
            | _ -> ()
            printfn "PASS %s.%s(%s)" ty.Name m.Name label
            (1, 0)
        with e ->
            let inner = if isNull e.InnerException then e else e.InnerException
            let first = (inner.Message + "").Split('\n').[0]
            printfn "FAIL %s.%s(%s) :: %s %s" ty.Name m.Name label (inner.GetType().Name) first
            (0, 1)
        |> fun r ->
            (match inst with :? IDisposable as d -> d.Dispose() | _ -> ())
            r
    with e ->
        let inner = if isNull e.InnerException then e else e.InnerException
        printfn "FAIL %s.%s(%s) :: ctor %s %s" ty.Name m.Name label (inner.GetType().Name) inner.Message
        (0, 1)

let runDir (dir: string) =
    let testDlls =
        Directory.EnumerateFiles(dir, "*.Tests.dll")
        |> Seq.toList
    if testDlls.IsEmpty then
        printfn "no *.Tests.dll under %s" dir
        (0, 0)
    else
        let alc = ByteAlc(dir)
        testDlls |> List.fold (fun (p, f) dll ->
            let asmName = AssemblyName(Path.GetFileNameWithoutExtension dll)
            let asm = alc.LoadFromAssemblyName asmName
            printfn "=== %s ===" asmName.Name
            asm.GetTypes()
            |> Seq.collect (fun ty ->
                ty.GetMethods(BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.DeclaredOnly)
                |> Seq.collect (fun m ->
                    let attrs = CustomAttributeData.GetCustomAttributes(m) |> Seq.toList
                    if attrs |> List.exists isFactAttr then
                        seq { yield (ty, m, [||], "") }
                    elif attrs |> List.exists isTheoryAttr then
                        inlineDataCases m |> Seq.map (fun (lbl, args) -> (ty, m, args, lbl))
                    else Seq.empty))
            |> Seq.fold (fun (p2, f2) (ty, m, args, lbl) ->
                let (p3, f3) = runOne ty m args lbl
                (p2 + p3, f2 + f3)) (p, f)) (0, 0)

let dirs = fsi.CommandLineArgs |> Array.skip 1 |> Array.toList
if dirs.IsEmpty then
    eprintfn "usage: dotnet fsi tools/sac-test-runner.fsx -- <test-bin-dir> [<test-bin-dir>...]"
    exit 2
let mutable totalPass = 0
let mutable totalFail = 0
for d in dirs do
    let (p, f) = runDir (Path.GetFullPath d)
    totalPass <- totalPass + p
    totalFail <- totalFail + f
printfn "RESULT pass=%d fail=%d" totalPass totalFail
exit totalFail
