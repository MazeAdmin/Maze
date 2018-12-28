using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Autofac;
using Microsoft.Extensions.Options;
using NuGet.Frameworks;
using NuGet.Versioning;
using Maze.Client.Library.Interfaces;
using Maze.Client.Library.Services;
using Maze.Core;
using Maze.Core.Connection;
using Maze.Core.Modules;
using Maze.ModuleManagement;
using Maze.Options;

namespace Maze
{
    public class AppContext : ApplicationContext, IApplicationInfo, IStaSynchronizationContext
    {
        public AppContext(ContainerBuilder builder)
        {
            builder.RegisterInstance(this).AsImplementedInterfaces();
            RootContainer = builder.Build();

            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomainOnAssemblyResolve;

            Container = LoadModules();
            StartConnecting();

            Container.Execute<IStartupAction>();
            Application.Idle += ApplicationOnIdle;
        }

        /// <summary>
        ///     The current synchronization context
        /// </summary>
        public SynchronizationContext Current { get; private set; }

        /// <summary>
        ///     The root container that contains all services of Maze
        /// </summary>
        private IContainer RootContainer { get; }

        /// <summary>
        ///     The core container, which inherits from <see cref="RootContainer" /> but also provides services from local modules
        /// </summary>
        public ILifetimeScope Container { get; }

        public NuGetFramework Framework { get; } = FrameworkConstants.CommonFrameworks.MazeClient10;
        public NuGetVersion Version { get; } = NuGetVersion.Parse("1.0");

        private ILifetimeScope LoadModules()
        {
            var loader = RootContainer.Resolve<IPackageLockLoader>();

            var modulesConfig = RootContainer.Resolve<IOptions<ModulesOptions>>().Value;

            var loadContext = loader.Load(modulesConfig.PackagesLock).Result;
            if (loadContext.PackagesLoaded)
            {
                return RootContainer.BeginLifetimeScope(builder => loadContext.Configure(builder));
            }

            return RootContainer;
        }

        private void StartConnecting()
        {
            Container.Resolve<IManagementCoreConnector>().StartConnecting(Container);
        }

        private static Assembly CurrentDomainOnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var name = args.Name.Split(',').First();
            return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.FullName.Split(',').First() == name);
        }

        private void ApplicationOnIdle(object sender, EventArgs e)
        {
            Current = SynchronizationContext.Current;
        }
    }
}