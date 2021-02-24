open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim
open FShade

module Shady = 
    let sam = 
        sampler2dArray {
            texture uniform?BlaBlub
            filter Filter.MinMagLinear
            addressU WrapMode.Clamp
            addressV WrapMode.Clamp
        }

    let frag (v : Effects.Vertex) =
        fragment {
            let slice : int = uniform?choice
            return sam.Sample(v.tc,slice)
        }

[<EntryPoint;STAThread>]
let main argv = 
    let rand = RandomSystem()
    Ag.initialize()
    Aardvark.Init()

    use app = new OpenGlApplication(true)
    use win = app.CreateGameWindow(8)

    let quadGeometry =
        IndexedGeometry(
            Mode = IndexedGeometryMode.TriangleList,
            IndexArray = ([|0;1;2; 0;2;3|] :> Array),
            IndexedAttributes =
                SymDict.ofList [
                    DefaultSemantic.Positions, [| V3f.OOO; V3f.IOO; V3f.IIO; V3f.OIO |] :> Array
                    DefaultSemantic.Colors, [| C4b.Red; C4b.Green; C4b.Blue; C4b.Yellow |] :> Array
                    DefaultSemantic.DiffuseColorCoordinates, [|V2d.OO; V2d.IO; V2d.II; V2d.OI|] :> Array
                ]
        )
       

    let initialView = CameraView.lookAt (V3d(6,6,6)) V3d.Zero V3d.OOI
    let view = initialView |> DefaultCameraController.control win.Mouse win.Keyboard win.Time
    let proj = win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))

    let size = V2i(64,64)

    // irgend ein limit gibts hier
    // GL_MAX_ARRAY_TEXTURE_LAYERS 2048 
    // GL_MAX_3D_TEXTURE_SIZE 16384
    let count = 1000
    let mkTexture() = 
        let pimg = PixImage<float32>(Col.Format.RGBA, size)
        let mat = pimg.GetMatrix<C4f>()
        let b() = rand.UniformC3f().ToC4f()
        mat.SetByCoord(fun _ -> b()) |> ignore
        pimg


    let rt = app.Runtime :> IRuntime
    let t : IBackendTexture = rt.CreateTextureArray(size, TextureFormat.Rgba32f, 1, 1, count)
    for slice in 0..count-1 do 
        let tex : PixImage<float32> = mkTexture()
        Log.line "upload texture %d" slice
        rt.Upload(t,0,slice,tex)

    let slice = Mod.init 0
    let sg =
        quadGeometry 
            |> Sg.ofIndexedGeometry
            |> Sg.effect [
                DefaultSurfaces.trafo |> toEffect
                DefaultSurfaces.vertexColor |> toEffect
                Shady.frag |> toEffect
               ]
            |> Sg.texture (Sym.ofString "BlaBlub") (Mod.constant (t :> ITexture))
            |> Sg.uniform "choice" slice
            |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo)
            |> Sg.projTrafo (proj |> Mod.map Frustum.projTrafo)

    
    let task =
        app.Runtime.CompileRender(win.FramebufferSignature, sg)

    win.Keyboard.DownWithRepeats.Values.Add (fun k ->
        match k with 
        | Keys.OemPlus ->  transact( fun _ -> let nv = max (slice.Value - 1) 0 in Log.line "slice %d" nv; slice.Value <- nv )
        | Keys.OemMinus -> transact( fun _ -> let nv = min (slice.Value + 1) (count-1) in Log.line "slice %d" nv; slice.Value <- nv )
        | _ -> ()
    )

    win.RenderTask <- task
    win.Run()
    0