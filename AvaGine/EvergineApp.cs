using Evergine.Common.IO;
using Evergine.Components.Cameras;
using Evergine.Components.Environment;
using Evergine.Components.Graphics3D;
using Evergine.Framework;
using Evergine.Framework.Graphics;
using Evergine.Framework.Services;
using Evergine.Mathematics;
using Random = Evergine.Framework.Services.Random;

namespace AvaGine;

// Needs to be partial due to Roslyn Code generators associated with Evergine.
public partial class EvergineApp : Application
{
    public EvergineApp()
    {
        Container.Register<Settings>();
        Container.Register<Clock>();
        Container.Register<TimerFactory>();
        Container.Register<Random>();
        Container.Register<ErrorHandler>();
        Container.Register<ScreenContextManager>();
        Container.Register<GraphicsPresenter>();
        Container.Register<AssetsDirectory>();
        Container.Register<AssetsService>();
        Container.Register<ForegroundTaskSchedulerService>();
        Container.Register<WorkActionScheduler>();
    }

    public override void Initialize()
    {
        base.Initialize();
        
        // Set the initial Scene here
        Container.Resolve<ScreenContextManager>().To(new ScreenContext(new ExampleScene()));
    }
}

/// <summary>
/// This is an example scene. It is only referenced in <see cref="EvergineApp.Initialize"/> and may be freely modified /
/// removed.
/// </summary>
public class ExampleScene : Scene
{
    protected override void CreateScene()
    {
        base.CreateScene();

        var defaultMaterial = Application.Current.Container.Resolve<AssetsService>()
            .Load<Material>(EvergineContent.Materials.DefaultMaterial);
        
        Managers.EntityManager.Add(new Entity("camera")
            .AddComponent(new Transform3D { LocalPosition = Vector3.Forward * -5 })
            .AddComponent(new Camera3D())
            .AddComponent(new FreeCamera3D())
        );

        Managers.EntityManager.Add(new Entity("teapot")
            .AddComponent(new Transform3D())
            .AddComponent(new TeapotMesh())
            .AddComponent(new MaterialComponent { Material = defaultMaterial })
            .AddComponent(new MeshRenderer())
        );

        Managers.EntityManager.Add(new Entity("sun")
            .AddComponent(new Transform3D { LocalRotation = new Vector3(-2, 0, 0) })
            .AddComponent(new PhotometricDirectionalLight())
            .AddComponent(new SunComponent())
        );

        Managers.EntityManager.Add(new Entity("skydome")
            .AddComponent(new Transform3D())
            .AddComponent(new MaterialComponent())
            .AddComponent(new SphereMesh())
            .AddComponent(new MeshRenderer())
            .AddComponent(new AtmosphereController())
        );
    }
}